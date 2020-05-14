using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO.Compression;
using MahApps.Metro.Controls;
using System.Text.RegularExpressions;
using MahApps.Metro.Controls.Dialogs;
using System.Threading.Tasks;
using System.Globalization;
using System.ComponentModel;
using Serilog;
using Serilog.Sinks.Graylog;
using CommandLine;
using System.Linq;
using System.Windows.Documents;
using System.Diagnostics;
using Serilog.Events;

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
        private string @ThirdPartyBackupDir;
        private string @SRSaveGameDir;
        private string guid;
        private readonly string aVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        // private string logPrefix;
        // private MessageTemplate logPrefix;
        private bool enableRemoteLogging;

        public MainWindow()
        {
            // Command line options
            // 3rd party backup directory
            string[] args = Environment.GetCommandLineArgs();
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed<Options>(o =>
                   {
                       if (!string.IsNullOrEmpty(o.ThirdPartyDirectory))
                       {
                           @ThirdPartyBackupDir = o.ThirdPartyDirectory;
                       }
                       if (o.EnableLogging == true)
                       {
                           enableRemoteLogging = true;
                       }
                   });
            guid = genGuid();
            InitializeComponent();
            
            // Initialize Logging
            var myLog = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();

            if (Properties.Settings.Default.graylog == true || enableRemoteLogging == true)
            {
                cbLogging.IsChecked = true;
                myLog = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Graylog
                                    (new GraylogSinkOptions
                                    {
                                        HostnameOrAddress = "markus.medisoftware.org",
                                        Port = 12201
                                    }
                                    ).CreateLogger();
            }
            else
            {
                cbLogging.IsChecked = false;
            }
            Log.Logger = myLog;

            SRBaseDir = findBaseDirectory();
            SRProfile = findProfileName();
            @MyBackupDir = Directory.GetParent(SRBaseDir) + @"\SRToolBackup";
            @SRBackupDir = SRBaseDir + @"\storage\BackupSlots\" + SRProfile;
            @SRSaveGameDir = SRBaseDir + @"\storage\" + SRProfile;

            Log.Information("{guid} {version} Program started", guid, aVersion);
            Log.Debug("{guid} {version} {SRBaseDir} ", guid, aVersion, SRBaseDir);
            Log.Debug("{guid} {version} {SRProfile}", guid, aVersion, SRProfile);
            Log.Debug("{guid} {version} {MyBackupDir}", guid, aVersion, MyBackupDir);
            Log.Debug("{guid} {version} {SRBackupDir}", guid, aVersion, SRBackupDir);
            Log.Debug("{guid} {version} }{SRSaveGameDir}", guid, aVersion, SRSaveGameDir);

            // Fill Datagrid
            dgBackups.AutoGenerateColumns = true;
            readBackups();
            sr_p.Content = SRBaseDir;
            _ = MetroMessage("Heads Up", "This tool creates backups of your current SnowRunner save game whenever changes are made.\n\n");
            txtAmount.Text = getMoney();
        }


        private string genGuid()
        {
            if (Properties.Settings.Default.guid == "")
            {
                string g = Guid.NewGuid().ToString();
                Properties.Settings.Default.guid = g;
                Properties.Settings.Default.Save();
                return g;
            }
            else
            {
                string g = Properties.Settings.Default.guid;
                return g;
            }
        }

        /// <summary>
        /// Clear items in datagrid and (re)loads backups
        /// </summary>
        private void readBackups()
        {
            var allBackups = getBackups();
            allBackups.AddRange(getOtherBackups(MyBackupDir, "Tool-Backup"));
            if (!string.IsNullOrEmpty(ThirdPartyBackupDir))
            {
                allBackups.AddRange(getOtherBackups(ThirdPartyBackupDir, "3rd party -Backup"));
            }
            dgBackups.ItemsSource = allBackups;
            dgBackups.Items.SortDescriptions.Clear();
            dgBackups.Items.SortDescriptions.Add(new SortDescription("Timestamp", ListSortDirection.Descending));
            dgBackups.Items.Refresh();
        }

        /// <summary>
        /// Load backups made by SnorRunner-Tool
        /// </summary>
        /// <returns></returns>
        private List<Backup> getOtherBackups(string directory, string backupType)
        {
            Log.Information("{guid} {version} {OtherBackupDirectory} {BackupType}", guid, aVersion, directory, backupType);
            List<Backup> backups = new List<Backup>();
            string[] fileEntries = Directory.GetFiles(directory);
            Log.Debug("{guid} {version} {BackupFilesFound}.", guid, aVersion, fileEntries.Length.ToString());
            foreach (string f in fileEntries)
            {
                string fName = new FileInfo(f).Name;
                DateTime timestamp = File.GetCreationTime(f);
                backups.Add(new Backup() { BackupName = fName, Timestamp = timestamp, Type = backupType });
            }
            return backups;
        }

        /// <summary>
        /// Collect save game backups in list
        /// </summary>
        /// <returns></returns>
        private List<Backup> getBackups()
        {
            Log.Information("{guid} {version} Reading game backups", guid, aVersion);
            List<Backup> backups = new List<Backup>();
            string[] subdirectoryEntries = Directory.GetDirectories(@SRBackupDir);
            Log.Debug("{guid} {version} {SubDirCount}", guid, aVersion, subdirectoryEntries.Length.ToString());
            foreach (string subdirectory in subdirectoryEntries)
            {
                string dir = new DirectoryInfo(subdirectory).Name;
                DateTime timestamp = Directory.GetCreationTime(subdirectory);
                backups.Add(new Backup() { BackupName = dir, Timestamp = timestamp, Type = "Game-Backup" });
            }
            return backups;
        }

        /// <summary>
        /// Try to find the profile directory name
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        private string findProfileName()
        {
            string searchPath = @SRBaseDir + @"\storage";
            string[] subdirectoryEntries = Directory.GetDirectories(searchPath);
            string pattern = @"^[A-Fa-f0-9]+$";
            foreach (string subdirectory in subdirectoryEntries)
            {
                Log.Debug("{guid} {version} {ProfileCandidate}", guid, aVersion, subdirectory);
                if (!subdirectory.Contains("backupSlots"))
                {
                    // Check if subdirectory is hex string
                    string dirName = new DirectoryInfo(subdirectory).Name;
                    if (Regex.IsMatch(dirName, pattern))
                    {
                        string profiledir = new DirectoryInfo(subdirectory).Name;
                        Log.Debug("{guid} {version} {ProfileFound}", guid, aVersion, profiledir);
                        return profiledir;
                    }
                }
            }
            Log.Warning("{guid} {version} No profile directory found!", guid, aVersion);
            return null;
        }

        private string findBaseDirectory()
        {
            var p = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\My Games\SnowRunner\base";
            return p;
        }


        private void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = (MenuItem)sender;
            var contextMenu = (ContextMenu)menuItem.Parent;
            var item = (DataGrid)contextMenu.PlacementTarget;
            var restoreItem = (Backup)item.SelectedCells[0].Item;
            backupCurrentSavegame();
            restoreBackup(restoreItem.BackupName, restoreItem.Type);
            _ = MetroMessage("Next time better luck", "The selected save game backup has been restored. A backup of your former save game has been saved in " + MyBackupDir);
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
                Log.Debug("{guid} {version} {RestoreSource} {RestoreDestination}", guid, aVersion, source, @SRSaveGameDir);
                dCopy(source, SRSaveGameDir, false, true);
            }
            else
            {
                string zipFile = MyBackupDir + @"\" + backupItem;
                Log.Debug("{guid} {version} {ZipRestoreSource} {ZipRestoreDestination}", guid, aVersion, zipFile, @SRSaveGameDir);
                ZipExtractHelperClass.ZipFileExtractToDirectory(zipFile, SRSaveGameDir);
            }
        }

        /// <summary>
        /// Create a zip compressed backup of the current save game
        /// </summary>
        /// <returns></returns>
        private string backupCurrentSavegame()
        {
            Log.Information("{guid} {version}Starting backup of current save game", guid, aVersion);
            if (!Directory.Exists(MyBackupDir))
            {
                Directory.CreateDirectory(MyBackupDir);
            }
            string startPath = SRSaveGameDir;
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_hh-mm-ss", CultureInfo.CurrentCulture);
            string zipPath = MyBackupDir + @"\backup" + timestamp + ".zip";

            try
            {
                ZipFile.CreateFromDirectory(startPath, zipPath);
                Log.Debug("{guid} {version} {ZipCreateSource} {ZipCreateTarget}", guid, aVersion, startPath, zipPath);
            }
            catch (IOException ex)
            {
                Log.Error("{guid} {version} {ZipCreateException}", guid, aVersion, ex.Message);
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
        private void dCopy(string sourceDirName, string destDirName, bool copySubDirs, bool overwriteExisting)
        {
            foreach (string newPath in Directory.GetFiles(sourceDirName, "*.*", SearchOption.AllDirectories))
                try
                {
                    File.Copy(newPath, newPath.Replace(sourceDirName, destDirName), true);
                }
                catch (IOException ex)
                {
                    Log.Warning("{guid} {version} {CopyFileToDestination} {Exception}", guid, aVersion, destDirName, ex.Message);
                }
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

            _ = MetroMessage("Just for sure", "Your current save game was backed up to the folder " + MyBackupDir + ".");
        }

        /// <summary>
        /// "Set Money" Button click event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            saveMoney();
            _ = MetroMessage("Congratulations", "You are rich now.");
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
                Log.Information("{guid} {version} {MoneyFromSavegame}", guid, aVersion, moneyAmount);
                return moneyAmount;
            }
            else
            {
                moneyAmount = "failed";
                Log.Warning("{guid} {version} {MoneyNotFoundInFile}", guid, aVersion, saveGameFile);
                return null;
            }
        }
        private async Task<bool> MetroMessage(string title, string message)
        {
            string[] answers = { "Fine", "OK", "Make it so", "Hmmm", "Okay", "Hoot", "Ладно", "Хорошо", "d'accord", "Très bien", "Na gut", "Von mir aus" };
            Random r = new Random();
            int rInt = r.Next(0, answers.Length);
            var dialogSettings = new MetroDialogSettings();
            dialogSettings.AffirmativeButtonText = answers[rInt] + " [OK]";

            var dialogResult = await this.ShowMessageAsync(title,
                message,
                MessageDialogStyle.Affirmative, dialogSettings);

            if (dialogResult == MessageDialogResult.Affirmative)
            {
                return true;
            }

            return false;
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (cbLogging.IsChecked == true)
            {
                Properties.Settings.Default.graylog = true;
            }
            else
            {
                Properties.Settings.Default.graylog = false;
            }
            Properties.Settings.Default.Save();
            _ = MetroMessage("Hey trucker", "You have to restart the app to activate the new setting.");
        }

        private void MnuAbout_Click(object sender, RoutedEventArgs e)
        {
            _ = MetroMessage("About", "SnowRunner-Tool\n\nVersion " + aVersion + "\n(c) 2020 elpatron68\nhttps://github.com/elpatron68/SnowRunner-Tool/");
        }

        private void MnuLatestVersion_Click(object sender, RoutedEventArgs e)
        {
            // /owner/name/releases/latest/download/asset-name.zip
            // https://github.com/elpatron68/SnowRunner-Tool/releases/latest/Release.zip
            Process.Start("https://github.com/elpatron68/SnowRunner-Tool/releases/latest");
        }

        private void MnuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MnuIssues_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/elpatron68/SnowRunner-Tool/issues");
        }

        private void MnuSupportID_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(guid);
            _ = MetroMessage("Support ID copied", "Your support ID has been copied to the clipboard. Make sure, \"Send log\" is activated when the problem occured before filing an issue.");
        }


        private void MnuSRTLicense_Click(object sender, RoutedEventArgs e)
        {
            _ = MetroMessage("License", "DO WHAT THE FUCK YOU WANT TO PUBLIC LICENSE\n\nDO WHAT THE FUCK YOU WANT TO PUBLIC LICENSE\nTERMS AND CONDITIONS FOR COPYING, DISTRIBUTION AND MODIFICATION\n\n0. You just DO WHAT THE FUCK YOU WANT TO.");
        }

        private void MnuReload_Click(object sender, RoutedEventArgs e)
        {
            readBackups();            
        }

    }
}
