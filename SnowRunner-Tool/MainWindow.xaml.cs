using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO.Compression;
using MahApps.Metro.Controls;
using System.Text.RegularExpressions;
using System.Collections;
using System.Reflection;
using System.Text;

namespace SnowRunner_Tool
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private string SRBaseDir;
        private string SRProfile;
        private string @SRBackupDir;
        private string @MyBackupDir;
        private string @SRSaveGameDir;

        public MainWindow()
        {
            InitializeComponent();
            SRBaseDir = findBaseDirectory();

            SRProfile = findProfileName(SRBaseDir);
            @MyBackupDir = Directory.GetParent(SRBaseDir) + @"\SRToolBackup";
            @SRBackupDir = SRBaseDir + @"\storage\BackupSlots\" + SRProfile;
            @SRSaveGameDir = SRBaseDir + @"\storage\" + SRProfile;

            // Fill Datagrid
            dgBackups.AutoGenerateColumns = true;
            readBackups();
            sr_p.Content = SRBaseDir;
            txtAmount.Text = getMoney();
        }

        /// <summary>
        /// Clear items in datagrid and (re)loads backups
        /// </summary>
        private void readBackups()
        {
            var allBackups = getBackups();
            allBackups.AddRange(getMyBackups());
            dgBackups.ItemsSource = allBackups;
            dgBackups.Items.Refresh();
        }

        /// <summary>
        /// Load backups made by SnorRunner-Tool
        /// </summary>
        /// <returns></returns>
        private List<Backup> getMyBackups()
        {
            List<Backup> backups = new List<Backup>();
            string[] fileEntries = Directory.GetFiles(MyBackupDir);
            foreach (string f in fileEntries)
            {
                string fName = new FileInfo(f).Name;
                DateTime timestamp = File.GetCreationTime(f);
                backups.Add(new Backup() { DirectoryName = fName, Timestamp = timestamp, Type = "Tool-Backup" });
            }
            return backups;
        }

        /// <summary>
        /// Collect save game backups in list
        /// </summary>
        /// <param name="backupdir"></param>
        /// <returns></returns>
        private List<Backup> getBackups()
        {
            List<Backup> backups = new List<Backup>();
            string[] subdirectoryEntries = Directory.GetDirectories(@SRBackupDir);
            foreach (string subdirectory in subdirectoryEntries)
            {
                string dir = new DirectoryInfo(subdirectory).Name;
                DateTime timestamp = Directory.GetCreationTime(subdirectory);
                backups.Add(new Backup() { DirectoryName = dir, Timestamp = timestamp, Type = "Game-Backup" });
            }
            return backups;
        }

        /// <summary>
        /// Try to find the profile directory name
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        private string findProfileName(string p)
        {
            p += "\\storage";
            string[] subdirectoryEntries = Directory.GetDirectories(p);
            foreach (string subdirectory in subdirectoryEntries)
            {
                if (! subdirectory.Contains("backupSlots"))
                {
                    string profiledir = new DirectoryInfo(subdirectory).Name;
                    return profiledir;
                        }
            }
            return null;
        }

        private string findBaseDirectory()
        {
            var p = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\My Games\\SnowRunner\\base";
            return p;
        }

        
        private void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = (MenuItem)sender;
            var contextMenu = (ContextMenu)menuItem.Parent;
            var item = (DataGrid)contextMenu.PlacementTarget;
            var restoreItem = (Backup)item.SelectedCells[0].Item;
            backupCurrentSavegame();
            restoreBackup(restoreItem.DirectoryName, restoreItem.Type);
            MessageBox.Show("The selected save game backup has been restored. A backup of your former save game has been saved in " + MyBackupDir);
        }

        /// <summary>
        /// Restore a game backup (overwrites current save game)
        /// </summary>
        /// <param name="backupItem"></param>
        private void restoreBackup(string backupItem, string type)
        {
            if (type == "Game-Backup")
            {
                string source = SRBackupDir + @"\" + backupItem;
                dCopy(source, SRSaveGameDir, false, true);
            }
            else
            {
                string zipFile = MyBackupDir + @"\" + backupItem;
                ZipExtractHelperClass.ZipFileExtractToDirectory(zipFile, SRSaveGameDir);
            }
        }

        /// <summary>
        /// Create a zip compressed backup of the current save game
        /// </summary>
        /// <returns></returns>
        private string backupCurrentSavegame()
        {
            if (!Directory.Exists(MyBackupDir))
            {
                Directory.CreateDirectory(MyBackupDir);
            }
            string startPath = SRSaveGameDir;
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_hh-mm-ss");
            string zipPath = MyBackupDir + @"\backup" + timestamp + ".zip";
            
            try
            {
                ZipFile.CreateFromDirectory(startPath, zipPath);
            }
            catch (IOException ex)
            {
                throw new IOException("Target file exists: " + zipPath + Environment.NewLine + Environment.NewLine + ex.Message);
            }
            readBackups();
            return zipPath;
        }
        /// <summary>
        /// Copies a directory to another directory
        /// </summary>
        /// <param name="sourceDirName"></param>
        /// <param name="destDirName"></param>
        /// <param name="copySubDirs"></param>
        /// <param name="overwriteExisting"></param>
        private static void dCopy(string sourceDirName, string destDirName, bool copySubDirs, bool overwriteExisting)
        {
            foreach (string newPath in Directory.GetFiles(sourceDirName, "*.*", SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(sourceDirName, destDirName), true);
        }

        private void BackupCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void SaveCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        /// <summary>
        /// "Backup" Button click event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BackupCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            backupCurrentSavegame();
            MessageBox.Show("Your current save game was backed up to the folder " + MyBackupDir + ".");
        }

        /// <summary>
        /// "Set Money" Button click event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            saveMoney();
            MessageBox.Show("You are rich now.");
        }

        /// <summary>
        /// Cheat: Set amount of money in current save game
        /// </summary>
        private void saveMoney()
        {
            backupCurrentSavegame();
            string saveGameFile = SRSaveGameDir + @"\CompleteSave.dat";
            string amount = txtAmount.Text;
            File.WriteAllText(saveGameFile, Regex.Replace(File.ReadAllText(saveGameFile), @"\""money\""\:\d+", "\"money\":" + amount));
        }
        /// <summary>
        /// Get amount of money from current save game
        /// </summary>
        /// <returns></returns>
        private string getMoney()
        {
            string saveGameFile = SRSaveGameDir + @"\CompleteSave.dat";
            string s = File.ReadAllText(saveGameFile);
            string sPattern = @"\""money\""\:\d+";
            string moneyAmount = null;
            if (Regex.IsMatch(s, sPattern, RegexOptions.IgnoreCase))
            {
                moneyAmount = Regex.Match(s, sPattern).Value;
                moneyAmount = moneyAmount.Replace("\"money\":", null);
                return moneyAmount;
            }
            else
            {
                moneyAmount = "failed";
                return null;
            }
        }
    }
}
