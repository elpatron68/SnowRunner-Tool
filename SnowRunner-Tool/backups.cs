using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;

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

        /// <summary>
        /// Load backups made by SnorRunner-Tool and 3rd party zipped backups
        /// </summary>
        /// <returns></returns>
        public static List<Backup> GetOtherBackups(string directory, string backupType)
        {
            Log.Debug("getOtherBackups: {OtherBackupDirectory} / {BackupType}", directory, backupType);
            List<Backup> backups = new List<Backup>();
            if (Directory.Exists(directory))
            {
                string[] fileEntries = Directory.GetFiles(directory);
                Log.Debug("Found {BackupFilesFound} backups.", fileEntries.Length);
                foreach (string f in fileEntries)
                {
                    string fName = new FileInfo(f).Name;
                    DateTime timestamp = File.GetCreationTime(f);
                    string tmpSaveGameFile = CheatGame.UnzipToTemp(f);
                    if (File.Exists(tmpSaveGameFile))
                    {
                        string sgMoney = CheatGame.GetMoney(tmpSaveGameFile);
                        string sgXp = CheatGame.GetXp(tmpSaveGameFile);
                        backups.Add(new Backup() { BackupName = fName, Timestamp = timestamp, Type = backupType, Money = sgMoney, Xp = sgXp });
                    }
                }
                return backups;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Collect SnowRunner save game backup directory names  in list
        /// </summary>
        /// <returns></returns>
        public static List<Backup> GetBackups(string directory)
        {
            if (Directory.Exists(directory))
            {
                Log.Debug("Reading game backups");
                List<Backup> backups = new List<Backup>();
                string[] subdirectoryEntries = Directory.GetDirectories(directory);
                Log.Debug("{SubDirCount} backups found.", subdirectoryEntries.Length.ToString());

                foreach (string subdirectory in subdirectoryEntries)
                {
                    string dir = new DirectoryInfo(subdirectory).Name;
                    DateTime timestamp = Directory.GetCreationTime(subdirectory);
                    string backupSaveGameFile = subdirectory + @"\CompleteSave.dat";
                    if (File.Exists(backupSaveGameFile))
                    {
                        string sgMoney = CheatGame.GetMoney(backupSaveGameFile);
                        string sgXp = CheatGame.GetXp(backupSaveGameFile);
                        backups.Add(new Backup() { BackupName = dir, Timestamp = timestamp, Type = "Game-Backup", Money = sgMoney, Xp = sgXp });
                    }
                }
                return backups;
            }
            else
            {
                Log.Warning("Directory {SRBackupDir} does not exist", directory);
                return null;
            }
        }

        /// <summary>
        /// Create a zip compressed backup of the current save game
        /// </summary>
        /// <returns></returns>
        public static string BackupCurrentSavegame(string backupSource, string backupDestination)
        {
            Log.Debug("Backuping up current save game");
            if (!Directory.Exists(backupDestination))
            {
                Directory.CreateDirectory(backupDestination);
            }
            string sourcePath = backupSource;
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_hh-mm-ss", CultureInfo.CurrentCulture);
            string zipPath = backupDestination + @"\backup" + timestamp + ".zip";

            try
            {
                ZipFile.CreateFromDirectory(sourcePath, zipPath);
                Log.Debug("{SaveGameDir} {ZipFileTarget}", sourcePath, zipPath);
            }
            catch (IOException ex)
            {
                Log.Error(ex, "ZipFile.CreateFromDirectory failed");
            }

            // Reread all backups
            return zipPath;
        }

        /// <summary>
        /// Restores a game backup (overwrites current save game)
        /// </summary>
        /// <param name="sourceFileOrDirectory"></param>
        public static void RestoreBackup(string sourceFileOrDirectory, string saveGameDirectory)
        {
            Log.Information("Restore backup {BackupItem}", sourceFileOrDirectory);
            // SnowRunner Backup: Copy directory
            if (!String.Equals(Path.GetExtension(sourceFileOrDirectory), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                // string source = SRBackupDir + @"\" + sourceDirectory;
                Log.Debug("Copy directory from {Source} to {Destination}", sourceFileOrDirectory, saveGameDirectory);
                Backup.DirCopy(sourceFileOrDirectory, saveGameDirectory, true);
            }
            // Zipped backup: Extract zip file, see ZipExtractHelperClass
            else
            {
                // string zipFile = MyBackupDir + @"\" + sourceFileOrDirectory;
                Log.Debug("Unzipping {Source} to {Destination}", sourceFileOrDirectory, saveGameDirectory);
                ZipExtractHelperClass.ZipFileExtractToDirectory(sourceFileOrDirectory, saveGameDirectory);
            }
        }

    }
}
