using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SnowRunner_Tool
{
    /// <summary>
    /// Reads and updates money/XP in SnowRunner CompleteSave JSON without rewriting the file.
    /// Values are taken from persistentProfileData only (see issue #28).
    /// </summary>
    internal static class SaveGameJson
    {
        internal const string MoneyProperty = "money";
        internal const string ExperienceProperty = "experience";

        private static readonly Regex IntegerPattern = new Regex(@"^-?\d+$", RegexOptions.Compiled);

        internal static bool IsInteger(string value)
        {
            return !string.IsNullOrEmpty(value) && IntegerPattern.IsMatch(value);
        }

        /// <summary>
        /// True when the JSON contains a persistentProfileData object (a real save slot).
        /// Empty stubs or unrelated files without that object are treated as unoccupied.
        /// </summary>
        internal static bool HasPersistentProfileData(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                JToken root = JToken.Parse(json);
                foreach (JToken profile in root.SelectTokens("$..persistentProfileData"))
                {
                    if (profile.Type == JTokenType.Object)
                    {
                        return true;
                    }
                }
            }
            catch (JsonException)
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// True when the path is an occupied SnowRunner save slot file (exists + profile data).
        /// </summary>
        internal static bool IsOccupiedSaveFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                return HasPersistentProfileData(File.ReadAllText(path));
            }
            catch (IOException ex)
            {
                Log.Warning(ex, "Could not read save file {SaveFile}", path);
                return true;
            }
        }

        internal static bool TryGetProfileNumber(string json, string propertyName, out string numberText)
        {
            numberText = null;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            try
            {
                JToken root = JToken.Parse(json);
                foreach (JToken profile in root.SelectTokens("$..persistentProfileData"))
                {
                    if (profile.Type != JTokenType.Object)
                    {
                        continue;
                    }

                    JToken value = profile[propertyName];
                    if (value == null || (value.Type != JTokenType.Integer && value.Type != JTokenType.Float))
                    {
                        continue;
                    }

                    numberText = FormatNumber(value);
                    return true;
                }
            }
            catch (JsonException ex)
            {
                Log.Warning(ex, "Save game is not valid JSON");
            }

            return false;
        }

        internal static bool TryReplaceProfileNumber(string json, string propertyName, string newNumber, out string updated)
        {
            updated = json;
            if (!IsInteger(newNumber) || !TryGetProfileNumber(json, propertyName, out _))
            {
                return false;
            }

            if (!TryFindProfileNumberSpan(json, propertyName, out int start, out int length))
            {
                Log.Warning("Found {PropertyName} in JSON tree but not in the original save text", propertyName);
                return false;
            }

            updated = json.Substring(0, start) + newNumber + json.Substring(start + length);
            return true;
        }

        private static string FormatNumber(JToken token)
        {
            if (token is JValue value && value.Value != null)
            {
                return Convert.ToString(value.Value, CultureInfo.InvariantCulture);
            }

            return token.ToString();
        }

        /// <summary>
        /// Locates the number lexeme of a direct property of persistentProfileData in the original JSON.
        /// </summary>
        internal static bool TryFindProfileNumberSpan(string json, string propertyName, out int start, out int length)
        {
            start = 0;
            length = 0;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            int searchFrom = 0;
            while (TryFindPropertyColon(json, "persistentProfileData", searchFrom, out int afterColon))
            {
                int objectStart = SkipWhitespace(json, afterColon);
                if (objectStart >= json.Length || json[objectStart] != '{')
                {
                    searchFrom = afterColon;
                    continue;
                }

                if (TryFindDirectNumberProperty(json, objectStart, propertyName, out start, out length))
                {
                    return true;
                }

                searchFrom = objectStart + 1;
            }

            return false;
        }

        private static bool TryFindPropertyColon(string json, string propertyName, int searchFrom, out int afterColon)
        {
            afterColon = -1;
            string needle = "\"" + propertyName + "\"";
            int idx = searchFrom;
            while (idx < json.Length && (idx = json.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
            {
                if (IsEscapedQuote(json, idx))
                {
                    idx += needle.Length;
                    continue;
                }

                int afterName = SkipWhitespace(json, idx + needle.Length);
                if (afterName < json.Length && json[afterName] == ':')
                {
                    afterColon = afterName + 1;
                    return true;
                }

                idx += needle.Length;
            }

            return false;
        }

        private static bool TryFindDirectNumberProperty(string json, int objectStart, string propertyName, out int start, out int length)
        {
            start = 0;
            length = 0;
            int i = objectStart + 1;
            int depth = 1;

            while (i < json.Length && depth > 0)
            {
                i = SkipWhitespace(json, i);
                if (i >= json.Length)
                {
                    return false;
                }

                char c = json[i];
                if (c == '{')
                {
                    depth++;
                    i++;
                    continue;
                }

                if (c == '}')
                {
                    depth--;
                    i++;
                    continue;
                }

                if (c == '[')
                {
                    if (!TrySkipValue(json, ref i))
                    {
                        return false;
                    }
                    continue;
                }

                if (c == ',')
                {
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    if (!TryReadString(json, ref i, out string name))
                    {
                        return false;
                    }

                    i = SkipWhitespace(json, i);
                    if (i >= json.Length || json[i] != ':')
                    {
                        return false;
                    }

                    i++;
                    i = SkipWhitespace(json, i);
                    if (depth == 1 && name == propertyName && TryReadNumberSpan(json, i, out start, out length))
                    {
                        return true;
                    }

                    if (!TrySkipValue(json, ref i))
                    {
                        return false;
                    }

                    continue;
                }

                if (!TrySkipValue(json, ref i))
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TryReadNumberSpan(string json, int numberStart, out int start, out int length)
        {
            start = numberStart;
            length = 0;
            int i = numberStart;
            if (i < json.Length && json[i] == '-')
            {
                i++;
            }

            if (i >= json.Length || !char.IsDigit(json[i]))
            {
                return false;
            }

            while (i < json.Length && char.IsDigit(json[i]))
            {
                i++;
            }

            length = i - start;
            return length > 0;
        }

        private static bool TrySkipValue(string json, ref int i)
        {
            i = SkipWhitespace(json, i);
            if (i >= json.Length)
            {
                return false;
            }

            char c = json[i];
            if (c == '"')
            {
                return TryReadString(json, ref i, out _);
            }

            if (c == '{')
            {
                return TrySkipContainer(json, ref i, '{', '}');
            }

            if (c == '[')
            {
                return TrySkipContainer(json, ref i, '[', ']');
            }

            if (c == 't' || c == 'f' || c == 'n')
            {
                return TrySkipLiteral(json, ref i);
            }

            if (c == '-' || char.IsDigit(c))
            {
                return TrySkipNumber(json, ref i);
            }

            return false;
        }

        private static bool TrySkipContainer(string json, ref int i, char open, char close)
        {
            if (i >= json.Length || json[i] != open)
            {
                return false;
            }

            int depth = 1;
            i++;
            while (i < json.Length && depth > 0)
            {
                char c = json[i];
                if (c == '"')
                {
                    if (!TryReadString(json, ref i, out _))
                    {
                        return false;
                    }
                    continue;
                }

                if (c == open)
                {
                    depth++;
                }
                else if (c == close)
                {
                    depth--;
                }

                i++;
            }

            return depth == 0;
        }

        private static bool TrySkipNumber(string json, ref int i)
        {
            if (i < json.Length && json[i] == '-')
            {
                i++;
            }

            if (i >= json.Length || !char.IsDigit(json[i]))
            {
                return false;
            }

            while (i < json.Length && char.IsDigit(json[i]))
            {
                i++;
            }

            if (i < json.Length && json[i] == '.')
            {
                i++;
                while (i < json.Length && char.IsDigit(json[i]))
                {
                    i++;
                }
            }

            if (i < json.Length && (json[i] == 'e' || json[i] == 'E'))
            {
                i++;
                if (i < json.Length && (json[i] == '+' || json[i] == '-'))
                {
                    i++;
                }

                while (i < json.Length && char.IsDigit(json[i]))
                {
                    i++;
                }
            }

            return true;
        }

        private static bool TrySkipLiteral(string json, ref int i)
        {
            if (StartsWith(json, i, "true") || StartsWith(json, i, "null"))
            {
                i += 4;
                return true;
            }

            if (StartsWith(json, i, "false"))
            {
                i += 5;
                return true;
            }

            return false;
        }

        private static bool TryReadString(string json, ref int i, out string value)
        {
            value = null;
            if (i >= json.Length || json[i] != '"')
            {
                return false;
            }

            int start = i + 1;
            i++;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '\\')
                {
                    i++;
                    if (i >= json.Length)
                    {
                        return false;
                    }

                    if (json[i] == 'u')
                    {
                        i += 5;
                        if (i > json.Length)
                        {
                            return false;
                        }
                        continue;
                    }

                    i++;
                    continue;
                }

                if (c == '"')
                {
                    value = json.Substring(start, i - start);
                    i++;
                    return true;
                }

                i++;
            }

            return false;
        }

        private static bool IsEscapedQuote(string json, int quoteIndex)
        {
            int slashes = 0;
            for (int i = quoteIndex - 1; i >= 0 && json[i] == '\\'; i--)
            {
                slashes++;
            }

            return slashes % 2 == 1;
        }

        private static int SkipWhitespace(string json, int i)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i]))
            {
                i++;
            }

            return i;
        }

        private static bool StartsWith(string json, int i, string literal)
        {
            if (i + literal.Length > json.Length)
            {
                return false;
            }

            return string.CompareOrdinal(json, i, literal, 0, literal.Length) == 0;
        }
    }
}
