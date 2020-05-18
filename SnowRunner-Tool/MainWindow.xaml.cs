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
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media;
using MahApps.Metro.Converters;
using SnowRunner_Tool.Properties;
using System.Threading;
using Octokit;

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
        private bool enableDebugLogging;
        private int money;

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
                           enableDebugLogging = true;
                       }
                   });
            guid = genGuid();
            InitializeComponent();

            // Initialize Logging
            var myLog = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();

            if (Settings.Default.graylog == true || enableDebugLogging == true)
            {
                myLog = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Graylog
                                    (new GraylogSinkOptions
                                    {
                                        HostnameOrAddress = Settings.Default.LogHostName,
                                        Port = Settings.Default.LogPort
                                    }
                                    ).CreateLogger();
                enableDebugLogging = true;
            }
            else if (Settings.Default.usagelog == true)
            {
                myLog = new LoggerConfiguration().MinimumLevel.Information().WriteTo.Graylog
                                    (new GraylogSinkOptions
                                    {
                                        HostnameOrAddress = Settings.Default.LogHostName,
                                        Port = Settings.Default.LogPort
                                    }
                                    ).CreateLogger();
            }
            Log.Logger = myLog;
            Log.Information("{guid} {version} App started", guid, aVersion);

            bool manualPaths = false;

            // Read directories from settings or find them automatically
            if (string.IsNullOrEmpty(Settings.Default.SRbasedir))
            {
                SRBaseDir = findBaseDirectory();
            }
            else
            {
                SRBaseDir = Settings.Default.SRbasedir;
            }
            if (string.IsNullOrEmpty(Settings.Default.SRprofile))
            {
                SRProfile = findProfileName();
            }
            else
            {
                SRProfile = Settings.Default.SRprofile;
            }

            // Check base directory for existance
            if (!Directory.Exists(SRBaseDir))
            {
                manualPaths = true;
            }

            // Set derived directory
            SRBackupDir = SRBaseDir + @"\storage\BackupSlots\" + SRProfile;

            // Check for existance
            if (!Directory.Exists(SRBackupDir))
            {
                manualPaths = true;
            }

            // Set derived directories
            MyBackupDir = Directory.GetParent(SRBaseDir) + @"\SRToolBackup";
            SRSaveGameDir = SRBaseDir + @"\storage\" + SRProfile;

            // Check for existance
            if (!Directory.Exists(SRSaveGameDir))
            {
                manualPaths = true;
            }

            if (manualPaths == true)
            {
                Log.Information("{guid} {version} Manual path input", guid, aVersion);
                ShowInputPathDialog();
            }

            // Create directory for our backups
            if (!Directory.Exists(MyBackupDir))
            {
                Directory.CreateDirectory(MyBackupDir);
                Log.Debug("{guid} {version} {MyBackupDirCreated} ", guid, aVersion, MyBackupDir);
            }

            // Send directories to log
            Log.Debug("{guid} {version} {SRBaseDir} ", guid, aVersion, SRBaseDir);
            Log.Information("{guid} {version} {SRProfile}", guid, aVersion, SRProfile);
            Log.Debug("{guid} {version} {MyBackupDir}", guid, aVersion, MyBackupDir);
            Log.Debug("{guid} {version} {SRBackupDir}", guid, aVersion, SRBackupDir);
            Log.Debug("{guid} {version} }{SRSaveGameDir}", guid, aVersion, SRSaveGameDir);

            // Get log user settings and set icons for Menuitems
            if (Settings.Default.graylog == true || enableDebugLogging == true)
            {
                MnEnableLog.Icon = new System.Windows.Controls.Image
                {
                    Source = new BitmapImage(new Uri("images/baseline_done_black_18dp_1x.png", UriKind.Relative))
                };
            }
            if (Settings.Default.usagelog == true)
            {
                MnUsageLog.Icon = new System.Windows.Controls.Image
                {
                    Source = new BitmapImage(new Uri("images/baseline_done_black_18dp_1x.png", UriKind.Relative))
                };
            }

            // Set value of some UI elements, load backup data
            lblSnowRunnerPath.Content = SRBaseDir;
            txtAmount.Text = getMoney();
            this.Title = this.Title + " v" + aVersion;

            // Fill Datagrid
            dgBackups.AutoGenerateColumns = true;
            readBackups();

            // Test scheduled backup
            // BackupScheduler.startScheduledBackup("start", SRSaveGameDir, MyBackupDir);
        }

        /// <summary>
        /// Generates unique support-id for logging
        /// </summary>
        /// <returns></returns>
        private string genGuid()
        {
            if (Settings.Default.guid == "")
            {
                string g = Guid.NewGuid().ToString();
                Settings.Default.guid = g;
                Settings.Default.Save();
                return g;
            }
            else
            {
                string g = Settings.Default.guid;
                return g;
            }
        }

        /// <summary>
        /// Clear items in datagrid and (re)loads all backups
        /// </summary>
        private void readBackups()
        {
            // Add SnowRunner backup directories
            var allBackups = getBackups();
            // Add own zipped backups
            allBackups.AddRange(getOtherBackups(MyBackupDir, "Tool-Backup"));
            // Add 3rd party zipped backups
            if (!string.IsNullOrEmpty(ThirdPartyBackupDir))
            {
                if (Directory.Exists(ThirdPartyBackupDir))
                {
                    allBackups.AddRange(getOtherBackups(ThirdPartyBackupDir, "3rd party -Backup"));
                }
                else
                {
                    _ = MetroMessage("Directory not found", "Directory with 3rd party backups " + ThirdPartyBackupDir + " doesn´t exist.");
                    Log.Warning("{guid} {version} {NonExistant3rdPartyDir}", guid, aVersion, ThirdPartyBackupDir);
                }
            }
            if (allBackups.Count > 0)
            {
                dgBackups.ItemsSource = allBackups;
                dgBackups.Items.SortDescriptions.Clear();
                dgBackups.Items.SortDescriptions.Add(new SortDescription("Timestamp", ListSortDirection.Descending));
                dgBackups.Items.Refresh();
            }            
        }

        /// <summary>
        /// Load backups made by SnorRunner-Tool and 3rd party zipped backups
        /// </summary>
        /// <returns></returns>
        private List<Backup> getOtherBackups(string directory, string backupType)
        {
            Log.Debug("{guid} {version} {OtherBackupDirectory} {BackupType}", guid, aVersion, directory, backupType);
            List<Backup> backups = new List<Backup>();
            if (Directory.Exists(directory))
            {
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
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Collect SnowRunner save game backup directory names  in list
        /// </summary>
        /// <returns></returns>
        private List<Backup> getBackups()
        {
            if (Directory.Exists(@SRBackupDir))
            {
                Log.Debug("{guid} {version} Reading game backups", guid, aVersion);
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
            else
            {
                Log.Warning("{guid} {version} {NonExistantSRBackupDir}", guid, aVersion, SRBackupDir);
                return null;
            }
        }

        /// <summary>
        /// Try to find the profile directory name
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        private string findProfileName()
        {
            string searchPath = @SRBaseDir + @"\storage";
            try
            {
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
            }
            catch
            {
                Log.Warning("{guid} {version} No profile directory found!", guid, aVersion);
                return null;
            }
            return null;
        }

        /// <summary>
        /// SnowRunner base directory, usually %userprofofile%\documents\my games\Snowrunner\base
        /// </summary>
        /// <returns></returns>
        private string findBaseDirectory()
        {
            string p = null;
                p = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\My Games\SnowRunner\base";
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
        /// Restores a game backup (overwrites current save game)
        /// </summary>
        /// <param name="backupItem"></param>
        private void restoreBackup(string backupItem, string type)
        {
            Log.Information("{guid} {version} Restores backup", guid, aVersion);
            // SnowRunner Backup: Copy directory
            if (type == "Game-Backup")
            {
                string source = SRBackupDir + @"\" + backupItem;
                Log.Debug("{guid} {version} {RestoreSource} {RestoreDestination}", guid, aVersion, source, @SRSaveGameDir);
                dCopy(source, SRSaveGameDir, true);
            }
            // Zipped backup: Extract zip file, see ZipExtractHelperClass
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
            Log.Debug("{guid} {version}Starting backup of current save game", guid, aVersion);
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
            }
            
            // Reread all backups
            readBackups();
            return zipPath;
        }

        /// <summary>
        /// Cheat: Set amount of money in current save game
        /// </summary>
        private bool saveMoney()
        {
            backupCurrentSavegame();
            string saveGameFile = SRSaveGameDir + @"\CompleteSave.dat";
            string amount = txtAmount.Text;
            // Check if money value is numeric
            if (Regex.IsMatch(amount, @"^\d+$"))
            {
                try
                {
                    int chashFlow = int.Parse(amount) - money;
                    money = int.Parse(amount);
                    Log.Information("{guid} {version} {moneyamount} {cashflow}", guid, aVersion, amount, chashFlow);
                }
                catch
                {
                    Log.Debug("{guid} {version} Failed to parse int at saveMoney", guid, aVersion);
                }
                File.WriteAllText(saveGameFile, Regex.Replace(File.ReadAllText(saveGameFile), @"\""money\""\:\d+", "\"money\":" + amount));
                return true;
            }
            else
            {
                return false;
            }
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
            string moneyAmount;
            if (Regex.IsMatch(s, sPattern, RegexOptions.IgnoreCase))
            {
                moneyAmount = Regex.Match(s, sPattern).Value;
                moneyAmount = moneyAmount.Replace("\"money\":", null);
                Log.Debug("{guid} {version} {MoneyFromSavegame}", guid, aVersion, moneyAmount);
                try
                {
                    money = int.Parse(moneyAmount);
                }
                catch
                {
                    money = 0;
                }
                return moneyAmount;
            }
            else
            {
                Log.Warning("{guid} {version} {MoneyNotFoundInFile}", guid, aVersion, saveGameFile);
                return null;
            }
        }

        /// <summary>
        /// Copies a directory to another directory
        /// </summary>
        /// <param name="sourceDirName"></param>
        /// <param name="destDirName"></param>
        /// <param name="copySubDirs"></param>
        /// <param name="overwriteExisting"></param>
        private void dCopy(string sourceDirName, string destDirName, bool overwriteExisting)
        {
            foreach (string newPath in Directory.GetFiles(sourceDirName, "*.*", SearchOption.AllDirectories))
                try
                {
                    File.Copy(newPath, newPath.Replace(sourceDirName, destDirName), overwriteExisting);
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
            if (saveMoney() == true)
            {
                _ = MetroMessage("Congratulations", "You are rich now.");
            }
            else
            {
                _ = MetroMessage("Sorry", "Just give me the number! \"Extremely rich\" is no number.");
            }
        }

        /// <summary>
        /// Display message dialog
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <returns></returns>
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
            System.Windows.Application.Current.Shutdown();
        }

        private void MnuIssues_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/elpatron68/SnowRunner-Tool/issues");
        }

        private void MnuSupportID_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(guid);
            _ = MetroMessage("Support ID copied", "Your support ID has been copied to the clipboard. Make sure, \"Send log\" is activated when the problem occured before filing an issue.\n\nSupport ID: " + guid);
        }


        private void MnuSRTLicense_Click(object sender, RoutedEventArgs e)
        {
            _ = MetroMessage("License", "DO WHAT THE FUCK YOU WANT TO PUBLIC LICENSE\n\nDO WHAT THE FUCK YOU WANT TO PUBLIC LICENSE\nTERMS AND CONDITIONS FOR COPYING, DISTRIBUTION AND MODIFICATION\n\n0. You just DO WHAT THE FUCK YOU WANT TO.");
        }

        private void MnuReload_Click(object sender, RoutedEventArgs e)
        {
            readBackups();
            getMoney();
        }

        private void MnuToggleRemoteLog_Click(object sender, RoutedEventArgs e)
        {
            if (Settings.Default.graylog == false)
            {
                enableDebugLogging = true;
                Settings.Default.graylog = true;
                MnEnableLog.Icon = new System.Windows.Controls.Image
                {
                    Source = new BitmapImage(new Uri("images/baseline_done_black_18dp_1x.png", UriKind.Relative))
                };
            }
            Settings.Default.Save();
            _ = MetroMessage("Hey trucker", "You have to restart the app to activate the new setting.");
        }

        private void MnuReportUsage_Click(object sender, RoutedEventArgs e)
        {
            if (Settings.Default.usagelog == false)
            {
                Settings.Default.usagelog = true;
                MnUsageLog.Icon = new System.Windows.Controls.Image
                {
                    Source = new BitmapImage(new Uri("images/baseline_done_black_18dp_1x.png", UriKind.Relative))
                };
            }
            Settings.Default.Save();
            _ = MetroMessage("Hey trucker", "You have to restart the app to activate the new setting.");
        }

        private void MnPaths_Click(object sender, RoutedEventArgs e)
        {
            ShowInputPathDialog();
            readBackups();
        }

        private void MnProjectGithub_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/elpatron68/SnowRunner-Tool");
        }

        private void MnProjectModio_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://snowrunner.mod.io/snowrunner-tool");
        }

        private async void MnChkUpd_Click(object sender, RoutedEventArgs e)
        {
            var latest = await UpdateTestAsync();
            var thisVersion = new Version(aVersion);
            var latestVersion = new Version(latest);
            var result = latestVersion.CompareTo(thisVersion);
            if (result > 0)
            {
                _ = MetroMessage("Update check", "An update to version " + latest + " is available.\n\nThe download page will be opened after you clicked ok.");
                Process.Start("https://github.com/elpatron68/SnowRunner-Tool/releases/latest");
            }
            else if (result < 0)
            {
                _ = MetroMessage("Update check", "You are in front of the rest of the world!");
            }
            else
            {
                _ = MetroMessage("Update check", "You are using the latest version.");
            }
        }

        private async Task<string> UpdateTestAsync()
        {
            var client = new GitHubClient(new ProductHeaderValue("SnowRunner-Tool"));
            var releases = await client.Repository.Release.GetAll("elpatron68", "SnowRunner-Tool");
            var latest = releases[0];
            Console.WriteLine(
                        "The latest release is tagged at {0} and is named {1}",
                        latest.TagName,
                        latest.Name);
            return latest.TagName;
        }

        private void ShowInputPathDialog()
        {
            InputGamePath gamePath = new InputGamePath();
            // Fill paths in settings window
            if (!string.IsNullOrEmpty(SRBaseDir))
            {
                gamePath.TxSRBaseDir.Text = SRBaseDir;
            }
            if (!string.IsNullOrEmpty(SRBackupDir))
            {
                gamePath.TxSRBackupDir.Text = SRBackupDir;
            }
            if (!string.IsNullOrEmpty(SRSaveGameDir))
            {
                gamePath.TxSaveGamePath.Text = SRSaveGameDir;
            }
            if (!string.IsNullOrEmpty(SRProfile))
            {
                gamePath.TxSRProfileName.Text = SRProfile;
            }
            gamePath.ShowDialog();
            if (!string.IsNullOrEmpty(gamePath.TxSRBaseDir.Text))
            {
                SRBaseDir = gamePath.TxSRBaseDir.Text;
                SRProfile = gamePath.TxSRProfileName.Text;
                SRBackupDir = gamePath.TxSRBackupDir.Text;
                MyBackupDir = Directory.GetParent(SRBaseDir) + @"\SRToolBackup";
                SRSaveGameDir = SRBaseDir + @"\storage\" + SRProfile;
                Settings.Default.SRbasedir = SRBaseDir;
                Settings.Default.SRprofile = SRProfile;
                Settings.Default.Save();
            }
            else
            {
                if (!Directory.Exists(SRBaseDir))
                {
                    _ = MetroMessage("Sorry!", "You can`t leave the settings without entering a valid path!");
                    ShowInputPathDialog();
                }
            }
        }
    }
}
