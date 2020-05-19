using Serilog;
using System;
using System.IO;

namespace SnowRunner_Tool
{
    public class Backup
    {
        public string BackupName { get; set; }
        public DateTime Timestamp { get; set; }
        public string Type { get; set; }
        public string Money { get; set; }
        public string Xp { get; set; }
        private readonly ILogger _log = Log.ForContext<Backup>();

        /// <summary>
        /// Copies a directory to another directory
        /// </summary>
        /// <param name="sourceDirName"></param>
        /// <param name="destDirName"></param>
        /// <param name="copySubDirs"></param>
        /// <param name="overwriteExisting"></param>
        public static void DirCopy(string sourceDirName, string destDirName, bool overwriteExisting)
        {
            foreach (string newPath in Directory.GetFiles(sourceDirName, "*.*", SearchOption.AllDirectories))
                try
                {
                    File.Copy(newPath, newPath.Replace(sourceDirName, destDirName), overwriteExisting);
                }
                catch (IOException ex)
                {
                    Log.Error(ex, "File copy failed: {NewPath} {DestDirName}", newPath, destDirName);
                }
        }

    }
}
