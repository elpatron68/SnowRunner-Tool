using System;
using System.IO;

namespace SnowRunner_Tool.Tests
{
    [Collection(SerilogCollection.Name)]
    public class SaveGameJsonTests
    {
        private const string MinifiedWithForeignMoney =
            "{\"CompleteSave\":{\"SslValue\":{\"persistentProfileData\":{\"experience\":10,\"trucks\":{\"money\":1},\"money\":12345}},\"other\":{\"money\":99}}}";

        private const string PrettyJson =
            "{\n  \"CompleteSave\": {\n    \"persistentProfileData\": {\n      \"money\": -8,\n      \"experience\": 42\n    }\n  }\n}";

        [Fact]
        public void TryGetProfileNumber_ReadsOnlyPersistentProfileMoney()
        {
            bool ok = SaveGameJson.TryGetProfileNumber(MinifiedWithForeignMoney, SaveGameJson.MoneyProperty, out string money);

            Assert.True(ok);
            Assert.Equal("12345", money);
        }

        [Fact]
        public void TryGetProfileNumber_ReadsNegativeMoney()
        {
            bool ok = SaveGameJson.TryGetProfileNumber(PrettyJson, SaveGameJson.MoneyProperty, out string money);

            Assert.True(ok);
            Assert.Equal("-8", money);
        }

        [Fact]
        public void TryGetProfileNumber_ReadsExperience()
        {
            bool ok = SaveGameJson.TryGetProfileNumber(MinifiedWithForeignMoney, SaveGameJson.ExperienceProperty, out string xp);

            Assert.True(ok);
            Assert.Equal("10", xp);
        }

        [Fact]
        public void TryReplaceProfileNumber_UpdatesOnlyProfileMoney()
        {
            bool ok = SaveGameJson.TryReplaceProfileNumber(MinifiedWithForeignMoney, SaveGameJson.MoneyProperty, "-50", out string updated);

            Assert.True(ok);
            Assert.Contains("\"money\":-50", updated);
            Assert.Contains("\"money\":1", updated);
            Assert.Contains("\"money\":99", updated);
            Assert.DoesNotContain("\"money\":12345", updated);
            Assert.Contains("\"experience\":10", updated);
        }

        [Fact]
        public void TryReplaceProfileNumber_PrettyPrintedKeepsSurroundingFormat()
        {
            bool ok = SaveGameJson.TryReplaceProfileNumber(PrettyJson, SaveGameJson.ExperienceProperty, "1000", out string updated);

            Assert.True(ok);
            Assert.Contains("\"experience\": 1000", updated);
            Assert.Contains("\"money\": -8", updated);
            Assert.DoesNotContain("\"experience\": 42", updated);
        }

        [Fact]
        public void TryGetProfileNumber_InvalidJson_ReturnsFalse()
        {
            Assert.False(SaveGameJson.TryGetProfileNumber("not json", SaveGameJson.MoneyProperty, out _));
        }

        [Theory]
        [InlineData("12.5")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("abc")]
        public void IsInteger_RejectsNonIntegers(string? value)
        {
            Assert.False(SaveGameJson.IsInteger(value));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("42")]
        [InlineData("-8")]
        public void IsInteger_AcceptsIntegers(string value)
        {
            Assert.True(SaveGameJson.IsInteger(value));
        }

        [Fact]
        public void TryReplaceProfileNumber_NonInteger_ReturnsFalse()
        {
            Assert.False(SaveGameJson.TryReplaceProfileNumber(MinifiedWithForeignMoney, SaveGameJson.MoneyProperty, "12.5", out _));
        }

        [Fact]
        public void TryGetProfileNumber_MissingPersistentProfileData_ReturnsFalse()
        {
            const string json = "{\"CompleteSave\":{\"other\":{\"money\":99}}}";
            Assert.False(SaveGameJson.TryGetProfileNumber(json, SaveGameJson.MoneyProperty, out _));
        }

        [Fact]
        public void TryReplaceProfileNumber_InvalidJson_ReturnsFalse()
        {
            Assert.False(SaveGameJson.TryReplaceProfileNumber("not json", SaveGameJson.MoneyProperty, "1", out _));
        }

        [Fact]
        public void HasPersistentProfileData_DetectsRealSave()
        {
            Assert.True(SaveGameJson.HasPersistentProfileData(MinifiedWithForeignMoney));
        }

        [Theory]
        [InlineData("")]
        [InlineData("{}")]
        [InlineData("{\"CompleteSave\":{\"foo\":1}}")]
        [InlineData("not json")]
        public void HasPersistentProfileData_RejectsEmptyOrStub(string json)
        {
            Assert.False(SaveGameJson.HasPersistentProfileData(json));
        }

        [Fact]
        public void IsOccupiedSaveFile_TreatsStubFileAsFree()
        {
            string path = Path.Combine(Path.GetTempPath(), "SRT-stub-" + Guid.NewGuid().ToString("N") + ".cfg");
            try
            {
                File.WriteAllText(path, "{}");
                Assert.False(SaveGameJson.IsOccupiedSaveFile(path));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void IsOccupiedSaveFile_TreatsProfileFileAsOccupied()
        {
            string path = Path.Combine(Path.GetTempPath(), "SRT-save-" + Guid.NewGuid().ToString("N") + ".cfg");
            try
            {
                File.WriteAllText(path, MinifiedWithForeignMoney);
                Assert.True(SaveGameJson.IsOccupiedSaveFile(path));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void IsOccupiedSaveFile_MissingPath_ReturnsFalse()
        {
            Assert.False(SaveGameJson.IsOccupiedSaveFile(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N") + ".cfg")));
        }
    }
}