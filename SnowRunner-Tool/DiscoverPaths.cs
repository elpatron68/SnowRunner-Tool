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
    class DiscoverPaths
    {
        private readonly ILogger _log = Log.ForContext<DiscoverPaths>();
        /// <summary>
        /// Try to find the profile directory name
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static string FindProfileName(string snowRunnerBaseDirectory)
        {
            string searchPath = snowRunnerBaseDirectory + @"\storage";
            try
            {
                string[] subdirectoryEntries = Directory.GetDirectories(searchPath);
                string pattern = @"^[A-Fa-f0-9]+$";
                foreach (string subdirectory in subdirectoryEntries)
                {
                    Log.Debug("Profile candidate {ProfileCandidate}", subdirectory);
                    if (!subdirectory.Contains("backupSlots"))
                    {
                        // Check if subdirectory is hex string
                        string dirName = new DirectoryInfo(subdirectory).Name;
                        if (Regex.IsMatch(dirName, pattern))
                        {
                            string profiledir = new DirectoryInfo(subdirectory).Name;
                            Log.Debug("Profile {ProfileDir} found", profiledir);
                            return profiledir;
                        }
                    }
                }
            }
            catch
            {
                Log.Warning("No profile directory found!");
                return null;
            }
            return null;
        }

    }
}
