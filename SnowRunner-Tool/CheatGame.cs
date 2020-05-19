using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
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
        public static bool saveMoney(string SRSaveGameDir, string amount)
        {

            // backupCurrentSavegame();
            
            string saveGameFile = SRSaveGameDir + @"\CompleteSave.dat";
            // string amount = txtAmount.Text;
            // Check if money value is numeric
            if (Regex.IsMatch(amount, @"^\d+$"))
            {
                //try
                //{
                //    int chashFlow = int.Parse(amount) - money;
                //    money = int.Parse(amount);
                //    Log.Information("{guid} {version} {moneyamount} {cashflow}", guid, aVersion, amount, chashFlow);
                //}
                //catch
                //{
                //    Log.Debug("{guid} {version} Failed to parse int at saveMoney", guid, aVersion);
                //}
                File.WriteAllText(saveGameFile, Regex.Replace(File.ReadAllText(saveGameFile), @"\""money\""\:\d+", "\"money\":" + amount));
                return true;
            }
            else
            {
                return false;
            }
        }

    }
}
