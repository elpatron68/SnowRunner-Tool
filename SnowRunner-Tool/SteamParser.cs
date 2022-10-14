using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnowRunner_Tool
{
    internal class SteamParser
    {
        public static string SteamSaveGameDirectory(string SteamFolder)
        {
            string[] ProfileDirectories=Directory.GetDirectories(SteamFolder);

            foreach (string ProfileDirectory in ProfileDirectories)
            {
                string SnowRunnerUserdata = ProfileDirectory + @"\1465360\remote";
                if (Directory.Exists(SnowRunnerUserdata))
                {
                    return SnowRunnerUserdata;
                }
            }
            return null;
        }
    }
}
