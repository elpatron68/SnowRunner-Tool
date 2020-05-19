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
        private readonly ILogger _log = Log.ForContext<CheatGame>();

        public static string GetMoney(string saveGameFile)
        {
            string s = File.ReadAllText(saveGameFile);
            string sPattern = @"\""money\""\:\d+";
            string moneyAmount;
            if (Regex.IsMatch(s, sPattern, RegexOptions.IgnoreCase))
            {
                moneyAmount = Regex.Match(s, sPattern).Value;
                moneyAmount = moneyAmount.Replace("\"money\":", null);
                Log.Debug("Read money {MoneyFromSavegame}", moneyAmount);
                return moneyAmount;
            }
            else
            {
                Log.Warning("Money value not found in {SaveGameFile}", saveGameFile);
                return null;
            }
        }

        public static bool SaveMoney(string saveGameFile, string newMoney, int oldMoney)
        {

            // backupCurrentSavegame();
            // string amount = txtAmount.Text;
            // Check if money value is numeric
            Log.Information("SaveMoney");
            if (Regex.IsMatch(newMoney, @"^\d+$"))
            {
                try
                {
                    int chashFlow = int.Parse(newMoney) - oldMoney;
                    Log.Information("{MoneyAmount} {CashFlow}", newMoney, chashFlow);
                }
                catch
                {
                    Log.Debug("Failed to parse int at SaveMoney");
                }
                File.WriteAllText(saveGameFile, Regex.Replace(File.ReadAllText(saveGameFile), @"\""money\""\:\d+", "\"money\":" + newMoney));
                return true;
            }
            else
            {
                return false;
            }
        }

        public static string GetXp(string saveGameFile)
        {
            // string saveGameFile = SRSaveGameDir + @"\CompleteSave.dat";
            string s = File.ReadAllText(saveGameFile);
            string sPattern = @"\""experience\""\:\d+";
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

        public static bool SaveXp(string SRSaveGameDir, string newXP)
        {
            string saveGameFile = SRSaveGameDir + @"\CompleteSave.dat";
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

        public static string UnzipToTemp(string zipFile)
        {
            string tempDir = Path.GetTempPath();
            using (ZipArchive archive = ZipFile.OpenRead(zipFile))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.Equals("CompleteSave.dat"))
                    {
                        string destinationPath = Path.GetFullPath(Path.Combine(tempDir, entry.FullName));
                        entry.ExtractToFile(destinationPath, true);
                        return destinationPath;
                    }
                }
            }
            return null;
        }



    }
}
