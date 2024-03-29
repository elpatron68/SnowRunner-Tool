﻿using MahApps.Metro.Controls.Dialogs;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;

namespace SnowRunner_Tool
{
    public class Backup
    {
        public string BackupName { get; set; }
        public DateTime Timestamp { get; set; }
        // public string Timestamp { get; set; }
        public string Type { get; set; }
        public string MoneySlot1 { get; set; }
        public string XpSlot1 { get; set; }
        public string MoneySlot2 { get; set; }
        public string XpSlot2 { get; set; }
        public string MoneySlot3 { get; set; }
        public string XpSlot3 { get; set; }
        public string MoneySlot4 { get; set; }
        public string XpSlot4 { get; set; }
        
        /// <summary>
        /// Copies a directory to another directory
        /// </summary>
        /// <param name="sourceDirName"></param>
        /// <param name="destDirName"></param>
        /// <param name="copySubDirs"></param>
        /// <param name="overwriteExisting"></param>
        public static bool DirCopy(string sourceDirName, string destDirName, bool overwriteExisting, int SavegameSlot, string SavegameExtension)
        {
            bool result = false;
            string saveGameSlotFile = string.Empty;
            List<string> filesToSkip = new List<string>();

            switch (SavegameSlot)
            {
                // Recover all backup slots
                case 0:
                    result = true;
                    break;
                // Recover backup slot 1
                case 1:
                    saveGameSlotFile = "CompleteSave." + SavegameExtension;
                    filesToSkip.Add("CompleteSave1." + SavegameExtension);
                    filesToSkip.Add("CompleteSave2." + SavegameExtension);
                    filesToSkip.Add("CompleteSave3." + SavegameExtension);
                    break;
                // Recover backup slot 2
                case 2:
                    saveGameSlotFile = "CompleteSave1." + SavegameExtension;
                    filesToSkip.Add("CompleteSave." + SavegameExtension);
                    filesToSkip.Add("CompleteSave2." + SavegameExtension);
                    filesToSkip.Add("CompleteSave3." + SavegameExtension);
                    break;
                // Recover backup slot 3
                case 3:
                    saveGameSlotFile = "CompleteSave2." + SavegameExtension;
                    filesToSkip.Add("CompleteSave." + SavegameExtension);
                    filesToSkip.Add("CompleteSave1." + SavegameExtension);
                    filesToSkip.Add("CompleteSave3." + SavegameExtension);
                    break;
                // Recover backup slot 4
                case 4:
                    saveGameSlotFile = "CompleteSave3." + SavegameExtension;
                    filesToSkip.Add("CompleteSave." + SavegameExtension);
                    filesToSkip.Add("CompleteSave1." + SavegameExtension);
                    filesToSkip.Add("CompleteSave2." + SavegameExtension);
                    break;
            }
            if(File.Exists(sourceDirName + @"\" + saveGameSlotFile) | SavegameSlot == 0)
            {
                result = true;
            }
            else
            {
                result = false;
                return result;
            }

            foreach (string sourceFile in Directory.GetFiles(sourceDirName, "*.*", SearchOption.AllDirectories))
                try
                {
                    foreach (string file in filesToSkip)
                    {
                        if (!(Path.GetFileName(sourceFile) == file))
                        {
                            File.Copy(sourceFile, sourceFile.Replace(sourceDirName, destDirName), overwriteExisting);
                        }
                    }

                }
                catch (IOException ex)
                {
                    Log.Error(ex, "File copy failed: {sourceFile} {DestDirName}", sourceFile, destDirName);
                    result = false;
                }
            return result;
        }

        /// <summary>
        /// Load backups made by SnorRunner-Tool and 3rd party zipped backups
        /// </summary>
        /// <returns></returns>
        public static List<Backup> GetSrtBackups(string directory, string platform)
        {
            Log.Debug("getOtherBackups: {OtherBackupDirectory}", directory);
            string SavegameExtension = "dat";
            if (platform == "epic")
            {
                SavegameExtension = "dat";
            }
            else if (platform == "steam")
            {
                SavegameExtension = "cfg";
            }

            List<Backup> backups = new List<Backup>();
            string[] sgMoney = { "n/a", "n/a", "n/a", "n/a" };
            string backupType = string.Empty;
            string[] sgXp = { "n/a", "n/a", "n/a", "n/a" };
            if (Directory.Exists(directory))
            {
                string[] fileEntries = Directory.GetFiles(directory);
                Log.Debug("Found {BackupFilesFound} backups.", fileEntries.Length);
                foreach (string f in fileEntries)
                {
                    string fName = new FileInfo(f).Name;
                    if (fName.EndsWith(".pak.zip", StringComparison.OrdinalIgnoreCase))
                    {
                        backupType = "PAK-Backup";
                    }
                    else
                    {
                        backupType = "SRT-Backup";
                        string tmpSaveGameFile = CheatGame.UnzipToTemp(f, platform);
                        if (File.Exists(tmpSaveGameFile))
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                sgMoney[i] = CheatGame.GetMoney(tmpSaveGameFile, i + 1, SavegameExtension);
                                sgXp[i] = CheatGame.GetXp(tmpSaveGameFile, i + 1, SavegameExtension);
                            }
                        }
                    }
                    DateTime timestamp = File.GetCreationTime(f);
                    // string ts = timestamp.ToString();
                    backups.Add(new Backup()
                    {
                        BackupName = fName,
                        Timestamp = timestamp, 
                        Type = backupType, 
                        MoneySlot1 = sgMoney[0], 
                        XpSlot1 = sgXp[0],
                        MoneySlot2 = sgMoney[1],
                        XpSlot2 = sgXp[1],
                        MoneySlot3 = sgMoney[2],
                        XpSlot3 = sgXp[2],
                        MoneySlot4 = sgMoney[3],
                        XpSlot4 = sgXp[3]
                    });
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
        public static List<Backup> GetGameBackups(string directory, string SavegameExtension)
        {
            if (Directory.Exists(directory))
            {
                string[] sgMoney = { "n/a", "n/a", "n/a", "n/a" };
                string[] sgXp = { "n/a", "n/a", "n/a", "n/a" };
                Log.Debug("Reading game backups");
                List<Backup> backups = new List<Backup>();
                string[] subdirectoryEntries = Directory.GetDirectories(directory);
                Log.Debug("{SubDirCount} backups found.", subdirectoryEntries.Length.ToString());

                foreach (string subdirectory in subdirectoryEntries)
                {
                    string dir = new DirectoryInfo(subdirectory).Name;
                    DateTime timestamp = Directory.GetCreationTime(subdirectory);
                    // string ts = timestamp.ToString();
                    string backupSaveGameFile = subdirectory + @"\CompleteSave." + SavegameExtension;
                    if (File.Exists(backupSaveGameFile))
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            sgMoney[i] = CheatGame.GetMoney(backupSaveGameFile, i + 1, SavegameExtension);
                            sgXp[i] = CheatGame.GetXp(backupSaveGameFile, i + 1, SavegameExtension);
                        }
                        backups.Add(new Backup() { 
                            BackupName = dir, 
                            Timestamp = timestamp, 
                            Type = "Game-Backup", 
                            MoneySlot1 = sgMoney[0], 
                            XpSlot1 = sgXp[0],
                            MoneySlot2 = sgMoney[1],
                            XpSlot2 = sgXp[2],
                            MoneySlot3 = sgMoney[2],
                            XpSlot3 = sgXp[2],
                            MoneySlot4 = sgMoney[3],
                            XpSlot4 = sgXp[3]
                        });
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
        public static string BackupCurrentSavegame(string backupSource, string backupDestination, string prefix)
        {
            Log.Information("Backing up current save game");
            if (!Directory.Exists(backupDestination))
            {
                Directory.CreateDirectory(backupDestination);
            }
            string sourcePath = backupSource;
            string p = @"\" + prefix + "_";
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_hh-mm-ss", CultureInfo.CurrentCulture);
            string zipPath = backupDestination + p + timestamp + ".zip";

            if (!File.Exists(zipPath))
            {
                try
                {
                    ZipFile.CreateFromDirectory(sourcePath, zipPath);
                    Log.Debug("Zipped {SaveGameDir} to {ZipFileTarget}", sourcePath, zipPath);
                }
                catch (IOException ex)
                {
                    Log.Error(ex, "ZipFile.CreateFromDirectory failed");
                }
            }
            else
            {
                return null;
            }
            return zipPath;
        }

        public static string BackupSingleFile(string backupSourceFileName, string backupDestinationPath, string prefix)
        {
            string sourceFileExtension = Path.GetExtension(backupSourceFileName);
            Log.Debug("Backing up single file {SingeFileBackupSource}", backupSourceFileName);
            if (!Directory.Exists(backupDestinationPath))
            {
                Directory.CreateDirectory(backupDestinationPath);
            }
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_hh-mm-ss", CultureInfo.CurrentCulture);
            string zipPath = backupDestinationPath + @"\" + prefix + "_" + timestamp + sourceFileExtension + ".zip";

            try
            {
                string tempDir = Path.GetTempPath() + "SRT";
                string fName = Path.GetFileName(backupSourceFileName);
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
                File.Copy(backupSourceFileName, tempDir + @"\" + fName, true);
                ZipFile.CreateFromDirectory(tempDir, zipPath, CompressionLevel.NoCompression, false);
                Directory.Delete(tempDir, true);
            }
            catch (IOException ex)
            {
                Log.Error(ex, "ZipFile.BackupSingleFile failed");
                return null;
            }

            return zipPath;
        }


        /// <summary>
        /// Restores a game backup (overwrites current save game)
        /// </summary>
        /// <param name="sourceFileOrDirectory"></param>
        public static bool RestoreBackup(string sourceFileOrDirectory, string targetDirectory, int SavegameSlot, string SavegameExtension)
        {
            string TmpExtractionDirectory = Path.GetTempPath() + "SRT";
            // Delete temporary folder
            if (Directory.Exists(TmpExtractionDirectory)) 
            {
                Directory.Delete(TmpExtractionDirectory, true);
            }
            Directory.CreateDirectory(TmpExtractionDirectory);

            Log.Information("Restore backup {BackupItem}", sourceFileOrDirectory);
            bool result;
            // SnowRunner Backup: Copy directory
            if (!String.Equals(Path.GetExtension(sourceFileOrDirectory), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                Log.Debug("Copy directory from {Source} to {Destination}", sourceFileOrDirectory, targetDirectory);
                result = DirCopy(sourceFileOrDirectory, targetDirectory, true, SavegameSlot, SavegameExtension);
            }
            // Zipped backup: Extract zip file, see ZipExtractHelperClass
            else
            {
                Log.Debug("Unzipping all saves from save game {Source} to temporary directory {Destination}", sourceFileOrDirectory, TmpExtractionDirectory);
                ZipExtractHelperClass.ZipFileExtractToDirectory(sourceFileOrDirectory, TmpExtractionDirectory);

                result = DirCopy(TmpExtractionDirectory, targetDirectory, true, SavegameSlot, SavegameExtension);
                Directory.Delete(TmpExtractionDirectory, true);
            }
            return result;
        }
    }
}
