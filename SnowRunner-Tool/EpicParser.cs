using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnowRunner_Tool
{
    internal class EpicParser
    {
        /// <summary>
        /// Discover save game directory (Epic Games)
        /// </summary>
        /// <param name="EpicBaseFolder"></param>
        /// <returns></returns>
        public static string EpicSaveGameDirectory(string EpicBaseFolder)
        {
            foreach (string subdir in Directory.GetDirectories(EpicBaseFolder + @"\storage"))
            {
                if (File.Exists(subdir + @"\CompleteSave.dat"))
                {
                    return subdir;
                }
            }
            return null;
        }

        /// <summary>
        /// Migrate files from backup location of SRT < 1.0.5
        /// </summary>
        /// <param name="SRProfileDirectory"></param>
        /// <param name="MyBackupDir"></param>
        public static void MoveOldBackupsToNewLocation(string SRProfileDirectory, string MyBackupDir)
        {
            string oldBackupLocation = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\My Games\SnowRunner\SRToolBackup";
            if (Directory.Exists(oldBackupLocation))
            {
                string[] backupFiles = Directory.GetFiles(oldBackupLocation, "*.zip");
                foreach (string backupFile in backupFiles)
                {
                    try
                    {
                        if (!Directory.Exists(MyBackupDir))
                        {
                            Directory.CreateDirectory(MyBackupDir);
                        }
                        File.Move(backupFile, MyBackupDir + @"\" + Path.GetFileName(backupFile));
                    }
                    catch
                    {
                        // Something went wrong
                    }
                }
                try
                {
                    Directory.Delete(oldBackupLocation, true);
                }
                catch
                {
                    // Something went wrong
                }
            }
        }
    }
}
