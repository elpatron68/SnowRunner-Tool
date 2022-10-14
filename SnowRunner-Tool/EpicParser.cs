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

        public static void MoveOldBackupsToNewLocation(string SRProfileDirectory, string MyBackupDir)
        {
            string oldBackupLocation = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\My Games\SnowRunner\SRToolBackup";
            string[] backupFiles = Directory.GetFiles(oldBackupLocation);
            foreach(string backupFile in backupFiles)
            {
                try
                {
                    File.Move(backupFile, MyBackupDir + Path.GetFileName(backupFile));
                }
                catch
                {
                    // Something went wrong
                }
            }
        }
    }
}
