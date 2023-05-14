using ControlzEx.Standard;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SnowRunner_Tool
{
    class CheatGame
    {
        /// <summary>
        /// Cheat: Set amount of money in current save game
        /// </summary>
        public static string GetMoney(string saveGameFile, int saveGameSlot, string SavegameExtension)
        {
            switch (saveGameSlot)
            {
                case 2:
                    saveGameFile = Path.GetDirectoryName(saveGameFile) + @"\CompleteSave1." + SavegameExtension;
                    break;
                case 3:
                    saveGameFile = Path.GetDirectoryName(saveGameFile) + @"\CompleteSave2." + SavegameExtension;
                    break;
                case 4:
                    saveGameFile = Path.GetDirectoryName(saveGameFile) + @"\CompleteSave3." + SavegameExtension;
                    break;
            }
            if (!File.Exists(saveGameFile))
            {
                return "n/a";
            }
            string s = File.ReadAllText(saveGameFile);
            // https://github.com/elpatron68/SnowRunner-Tool/issues/28
            string sPattern = @"persistentProfileData.*,\""money\"":\d+";
            string moneyAmount;
            if (Regex.IsMatch(s, sPattern, RegexOptions.IgnoreCase))
            {
                moneyAmount = Regex.Match(s, sPattern).Value;
                moneyAmount=Regex.Match(moneyAmount, @",\""money\"":\d+").Value;
                moneyAmount = moneyAmount.Replace(",\"money\":", null);
                Log.Debug("Read money {MoneyFromSavegame}", moneyAmount);
                return moneyAmount;
            }
            else
            {
                Log.Warning("Money value not found in {SaveGameFile}", saveGameFile);
                return null;
            }
        }

        public static bool SaveMoney(string saveGameFile, string newAmount, int saveGameSlot, string SavegameExtension)
        {
            switch (saveGameSlot)
            {
                case 2:
                    saveGameFile = Path.GetDirectoryName(saveGameFile) + @"\CompleteSave1." + SavegameExtension;
                    break;
                case 3:
                    saveGameFile = Path.GetDirectoryName(saveGameFile) + @"\CompleteSave2." + SavegameExtension;
                    break;
                case 4:
                    saveGameFile = Path.GetDirectoryName(saveGameFile) + @"\CompleteSave3." + SavegameExtension;
                    break;
            }
            if (!File.Exists(saveGameFile))
            {
                return false;
            }

            // Check if money value is numeric
            Log.Information("SaveMoney");
            if (Regex.IsMatch(newAmount, @"^\d+$"))
            {
                File.WriteAllText(saveGameFile, Regex.Replace(File.ReadAllText(saveGameFile), @"\""money\""\:\d+", "\"money\":" + newAmount));
                return true;
            }
            else
            {
                return false;
            }
        }

        public static string GetXp(string saveGameFile, int saveGameSlot, string SavegameExtension)
        {
            switch (saveGameSlot)
            {
                case 2:
                    saveGameFile = Path.GetDirectoryName(saveGameFile) + @"\CompleteSave1." + SavegameExtension;
                    break;
                case 3:
                    saveGameFile = Path.GetDirectoryName(saveGameFile) + @"\CompleteSave2." + SavegameExtension;
                    break;
                case 4:
                    saveGameFile = Path.GetDirectoryName(saveGameFile) + @"\CompleteSave3." + SavegameExtension;
                    break;
            }
            if (!File.Exists(saveGameFile))
            {
                return "n/a";
            }

            string s = File.ReadAllText(saveGameFile);
            // https://github.com/elpatron68/SnowRunner-Tool/issues/28
            //string sPattern = @"\""experience\""\:\d+";
            string sPattern = @"\""persistentProfileData\""(.*?)\""experience\""\:\d+";
            string xpAmount;
            if (Regex.IsMatch(s, sPattern, RegexOptions.IgnoreCase))
            {
                xpAmount = Regex.Match(s, sPattern).Value;
                xpAmount = xpAmount.Replace("\"experience\":", null);
                Log.Debug("Read XP {XpFromSavegame}", xpAmount);
                return xpAmount;
            }
            else
            {
                Log.Warning("Money value not found in {SaveGameFile}", saveGameFile);
                return null;
            }

        }

        public static bool SaveXp(string SRSaveGameDir, string newXP, int SaveGameSlot, string SavegameExtension)
        {
            string saveGameFile;
            if (SaveGameSlot == 1)
            {
                saveGameFile = SRSaveGameDir + @"\CompleteSave." + SavegameExtension;
            }
            else
            {
                saveGameFile = SRSaveGameDir + @"\CompleteSave" + (SaveGameSlot - 1).ToString() + "." + SavegameExtension;
            }

            if (!File.Exists(saveGameFile))
            {
                return false;
            }

            Log.Information("SaveXp");
            try
            {
                File.WriteAllText(saveGameFile, Regex.Replace(File.ReadAllText(saveGameFile), @"\""experience\""\:\d+", "\"experience\":" + newXP));
                return true;
            }
            catch (IOException ex)
            {
                Log.Error(ex, "Error reading money in SaveXp");
                return false;
            }
        }

        public static string UnzipToTemp(string zipFile, string platform)
        {
            string tempDir = Path.GetTempPath() + @"\SRT-TMP";
            string extension = null;
            if (platform == "epic")
            {
                extension = "dat";
            }
            if (platform == "steam")
            {
                extension = "cfg";
            }

            if (!Directory.Exists(tempDir)) { Directory.CreateDirectory(tempDir); }
            string returnPath = "";
            using (ZipArchive archive = ZipFile.OpenRead(zipFile))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    // 
                    if (entry.FullName.Contains("CompleteSave"))
                    {
                        if (entry.FullName.Equals("CompleteSave." + extension)) 
                        {
                            returnPath = Path.GetFullPath(Path.Combine(tempDir, entry.FullName));
                        }
                        entry.ExtractToFile(Path.GetFullPath(Path.Combine(tempDir, entry.FullName)), true);
                    }
                }    
            }
            return returnPath;
        }

        /// <summary>
        /// Copy a save game slot to another. Slots are 1-based integer.
        /// </summary>
        /// <param name="slot1"></param>
        /// <param name="slot2"></param>
        /// <param name="SRProfile"></param>
        public static bool CopySlotToOtherSlot(int slot1, int slot2, string SRProfile, string platform)
        {
            string sourcefile;
            string destinationfile;
            string filename;
            string oldtext;
            string newtext;
            string extension;
            if (platform == "steam")
            {
                extension = "cfg";
            }
            else
            {
                extension = "dat";
            }

            if (slot1 > 1)
            {
                filename = string.Format("CompleteSave{0}.{1}", (slot1 - 1).ToString(), extension);
                oldtext = string.Format("CompleteSave{0}", (slot1 - 1).ToString());
            }
            else
            {
                filename = string.Format("CompleteSave.{0}", extension);
                oldtext = "CompleteSave";
            }
            sourcefile = Path.Combine(SRProfile, filename);
            
            if (slot2 > 1)
            {
                filename = string.Format("CompleteSave{0}.{1}", (slot2 - 1).ToString(), extension);
                newtext = string.Format("CompleteSave{0}", (slot2 - 1).ToString());
            }
            else
            {
                filename = string.Format("CompleteSave.{0}", extension);
                newtext = "CompleteSave";
            }
            destinationfile = Path.Combine(SRProfile, filename);

            string text = File.ReadAllText(sourcefile);
            text = Regex.Replace(text, oldtext, newtext, RegexOptions.IgnoreCase);
            try
            {
                File.WriteAllText(Path.Combine(SRProfile, destinationfile), text);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
