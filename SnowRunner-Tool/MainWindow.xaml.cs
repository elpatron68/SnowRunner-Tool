using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MahApps.Metro.Controls;
using System.Text.RegularExpressions;
using MahApps.Metro.Controls.Dialogs;
using System.Threading.Tasks;
using System.ComponentModel;
using Serilog;
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

namespace SnowRunner_Tool
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private string SRBaseDir;
        private string SRPaksDir;
        private string SRProfile;
        private string SRBackupDir;
        private string MyBackupDir;
        private string SRSaveGameDir;
        private string SRsaveGameFile;
        private readonly string guid;
        private readonly string aVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        private KeyboardHook _hook;
        private FileSystemWatcher fswGameBackup;
        private static int autoSaveCounter = 0;

        public MainWindow()
        {
            guid = GenGuid();

            InitializeComponent();
            ((App)Application.Current).WindowPlace.Register(this);

            // Initialize Logging
            var myLog = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();
            myLog.Information("App started");

            bool manualPaths = false;

            // Read directories from settings or find them automatically
            if (string.IsNullOrEmpty(Settings.Default.SRbasedir))
            {
                SRBaseDir = DiscoverPaths.FindBaseDirectory();
            }
            else
            {
                SRBaseDir = Settings.Default.SRbasedir;
            }
            if (string.IsNullOrEmpty(Settings.Default.SRprofile))
            {
                SRProfile = DiscoverPaths.FindProfileName(SRBaseDir);
            }
            else
            {
                SRProfile = Settings.Default.SRprofile;
            }
            SRPaksDir = Settings.Default.SRPaksDir;

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
            SRsaveGameFile = SRSaveGameDir + @"\CompleteSave.dat";

            // Check for existance
            if (!Directory.Exists(SRSaveGameDir))
            {
                manualPaths = true;
            }

            if (manualPaths == true)
            {
                Log.Information("Manual path input required");
                ShowSettingsDialog();
            }

            // Create directory for our backups
            if (!Directory.Exists(MyBackupDir))
            {
                Directory.CreateDirectory(MyBackupDir);
                Log.Debug("{MyBackupDir} created ", MyBackupDir);
            }

            // Send directories to log
            Log.Debug("Set {SRBaseDir}", SRBaseDir);
            Log.Information("Set {SRProfile}", SRProfile);
            Log.Debug("Set {MyBackupDir}", MyBackupDir);
            Log.Debug("Set {SRBackupDir}", SRBackupDir);
            Log.Debug("Set {SRSaveGameDir}", SRSaveGameDir);

            // Set value of some UI elements, load backup data
            lblSnowRunnerPath.Content = SRBaseDir;
            UpdateTitle();
            

            // Fill Datagrid
            dgBackups.AutoGenerateColumns = true;
            ReadBackups();

            // Initialize Autobackup FileSystemWatcher
            fswGameBackup = new FileSystemWatcher
            {
                Path = SRSaveGameDir,
                Filter = "CompleteSave.dat"
            };
            fswGameBackup.Changed += FileSystemWatcher_Changed;
            SetAutobackup(Settings.Default.autobackupinterval);

            // Register global hotkey
            _hook = new KeyboardHook();
            _hook.KeyDown += new KeyboardHook.HookEventHandler(OnHookKeyDown);

            // Check for update (Win8+)
            string os = OsInfo.GetOSInfo();
            Log.Information("{Os}", os);
            if (!os.ToLower().Contains("windows 7"))
            {
                CheckUpdate();
            }
        }

        /// <summary>
        /// Generates unique support-id for logging
        /// </summary>
        /// <returns></returns>
        private string GenGuid()
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
        private void ReadBackups()
        {
            // Add SnowRunner backup directories
            var allBackups = Backup.GetBackups(SRBackupDir);
            // Add own zipped backups
            allBackups.AddRange(Backup.GetOtherBackups(MyBackupDir));
                        
            if (allBackups.Count > 0)
            {
                this.Dispatcher.Invoke(() => {
                    dgBackups.ItemsSource = allBackups;
                    dgBackups.Items.SortDescriptions.Clear();
                    dgBackups.Items.SortDescriptions.Add(new SortDescription("Timestamp", ListSortDirection.Descending));
                    dgBackups.Items.Refresh();
                });
            }
            else
            {
                dgBackups.ItemsSource = allBackups;
                dgBackups.Items.SortDescriptions.Clear();
                dgBackups.Items.Refresh();
            }
            UpdateTitle();
        }

        
        private void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            if (BackupScheduler.IsActive())
            {
                _ = MetroMessage("Attention!", "The game has to be closed before a backup can be restored.");
            }
            else
            {
                var menuItem = (MenuItem)sender;
                var contextMenu = (ContextMenu)menuItem.Parent;
                var item = (DataGrid)contextMenu.PlacementTarget;
                if (dgBackups.SelectedItems.Count > 1)
                {
                    _ = MetroMessage("Restore item", "You can only restore one item, please select a single row.");
                }
                else
                {
                    var restoreItem = (Backup)item.SelectedCells[0].Item;

                    // Create a backup before restore
                    _ = Backup.BackupCurrentSavegame(SRSaveGameDir, MyBackupDir, "safety-bak");
                    string backupSource;
                    if (string.Equals(restoreItem.Type, "Game-Backup", StringComparison.OrdinalIgnoreCase))
                    {
                        backupSource = SRBackupDir + @"\" + restoreItem.BackupName;
                    }
                    else
                    {
                        backupSource = MyBackupDir + @"\" + restoreItem.BackupName;
                    }

                    if (string.Equals(restoreItem.Type, "PAK-Backup"))
                    {
                        // Restore initial.pak
                        try
                        {
                            _ = MetroMessage("This function experimental!", "Please report any problems to Github issues, see Help - Web - Report a problem.");
                            Backup.RestoreBackup(backupSource, SRPaksDir);
                        }
                        catch
                        {
                            _ = MetroMessage("Something went wrong", "Your backup could not be restored, please restore it manually.");
                            Process.Start("explorer.exe " + backupSource);
                        }
                    }
                    else
                    {
                        Backup.RestoreBackup(backupSource, SRSaveGameDir);
                    }
                    _ = MetroMessage("Next time better luck", "The selected saved game has been restored. A backup of your former save game has been saved.");
                    ReadBackups();
                }
            }
        }

        private void MnRevealExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (dgBackups.SelectedItems.Count > 1)
            {
                _ = MetroMessage("Multiple rows selected", "Select a singe row to be revealed.");
                return;
            }
            var menuItem = (MenuItem)sender;
            var contextMenu = (ContextMenu)menuItem.Parent;
            var item = (DataGrid)contextMenu.PlacementTarget;
            var restoreItem = (Backup)item.SelectedCells[0].Item;
            string backupSource = string.Empty;
            if (string.Equals(restoreItem.Type, "Game-Backup", StringComparison.OrdinalIgnoreCase))
            {
                backupSource = SRBackupDir + @"\" + restoreItem.BackupName;
            }
            else
            {
                backupSource = MyBackupDir + @"\" + restoreItem.BackupName;
            }
            Process.Start("explorer.exe", backupSource);
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

        private async Task<string> MetroInputMessage(string title, string message, string defaultValue)
        {
            var dialogSettings = new MetroDialogSettings();
            dialogSettings.AffirmativeButtonText = "Save";
            dialogSettings.DefaultText = defaultValue;
            dialogSettings.NegativeButtonText = "Cancel";

            var result = await this.ShowInputAsync(title, message, dialogSettings);
            return result;
        }


        private void MnuAbout_Click(object sender, RoutedEventArgs e)
        {
            _ = MetroMessage("About", "SnowRunner-Tool\n\nVersion " + aVersion + "\n(c) 2020 elpatron68\nhttps://github.com/elpatron68/SnowRunner-Tool/");
        }

        private void MnuExit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void MnuIssues_Click(object sender, RoutedEventArgs e)
        {
            WebLinkMessage("https://github.com/elpatron68/SnowRunner-Tool/issues");
        }

        private void MnuSupportID_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(guid);
            _ = MetroMessage("Support ID copied", "Your support ID has been copied to the clipboard. Make sure, \"Remote logging\" is activated when the problem occured before filing an issue.\n\nSupport ID: " + guid);
        }


        private void MnuSRTLicense_Click(object sender, RoutedEventArgs e)
        {
            _ = MetroMessage("License", "DO WHAT THE FUCK YOU WANT TO PUBLIC LICENSE\n\nDO WHAT THE FUCK YOU WANT TO PUBLIC LICENSE\nTERMS AND CONDITIONS FOR COPYING, DISTRIBUTION AND MODIFICATION\n\n0. You just DO WHAT THE FUCK YOU WANT TO.");
        }

        private void MnuReload_Click(object sender, RoutedEventArgs e)
        {
            ReadBackups();
            var m = CheatGame.GetMoney(SRsaveGameFile);
        }

        private void MnPaths_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsDialog();
            ReadBackups();
        }

        private void MnProjectGithub_Click(object sender, RoutedEventArgs e)
        {
            WebLinkMessage("https://github.com/elpatron68/SnowRunner-Tool");
        }

        private void MnProjectModio_Click(object sender, RoutedEventArgs e)
        {
            WebLinkMessage("https://snowrunner.mod.io/snowrunner-tool");
        }

        private async void MnChkUpd_Click(object sender, RoutedEventArgs e)
        {
            string os = OsInfo.GetOSInfo();
            if (os.ToLower().Contains("windows 7"))
            {
                _ = MetroMessage("OS not fully supported", "You are using " + os + ". Update check is not supported, all other functions are not tested!");
                return;
            }
            else
            {
                {
                    var r = await UpdateCheck.CheckGithubReleses(aVersion);
                    int result = r.Item1;
                    string url = r.Item2;
                    if (result > 0)
                    {
                        _ = MetroMessage("Update check", "An update is available.\n\nThe download will start in your web browser immediately after you clicked ok.");
                        Process.Start(url);
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
            }
        }

        private async void CheckUpdate()
        {
            var r = await UpdateCheck.CheckGithubReleses(aVersion);
            int result = r.Item1;
            if (result > 0)
            {
                ToastNote.Notify("Update available", "A new version of SnowRunner-Tool is available. See menu Help - Check for update to download the new version.");
            }
        }

        private void ShowSettingsDialog()
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
                SRsaveGameFile = SRSaveGameDir + @"\CompleteSave.dat";
                Settings.Default.SRbasedir = SRBaseDir;
                Settings.Default.SRprofile = SRProfile;
                Settings.Default.Save();
            }
            else
            {
                if (!Directory.Exists(SRBaseDir))
                {
                    _ = MetroMessage("Sorry!", "You can`t leave the settings without entering a valid path!");
                    ShowSettingsDialog();
                }
            }
        }

        private async void MnMoneyCheat_Click(object sender, RoutedEventArgs e)
        {
            int oldMoney = int.Parse(CheatGame.GetMoney(SRsaveGameFile));
            string result = await MetroInputMessage("Money Cheat", "Enter the amount of money you´d like to have", oldMoney.ToString());
            if (!string.IsNullOrEmpty(result))
            {
                // Create a backup before changing the file
                _ = Backup.BackupCurrentSavegame(SRSaveGameDir, MyBackupDir, "cheat-bak");
                _ = CheatGame.SaveMoney(SRsaveGameFile, result);
                int moneyUpgrade = int.Parse(result) - oldMoney;
                Log.Information("MoneyUpgrade {MoneyUpgrade}", moneyUpgrade);
                _ = MetroMessage("Congratulations", "You won " + moneyUpgrade.ToString() + " coins.");
                ReadBackups();
                UpdateTitle();
            }
        }

        private async void MnXpCheat_Click(object sender, RoutedEventArgs e)
        {
            string xp = CheatGame.GetXp(SRsaveGameFile);
            string result = await MetroInputMessage("XP Cheat", "Enter the amount of XP you´d like to have",
                                                    xp);
            if (!string.IsNullOrEmpty(result))
            {
                // Create a backup before changing the file
                _ = Backup.BackupCurrentSavegame(SRSaveGameDir, MyBackupDir, "cheat-bak");
                _ = CheatGame.SaveXp(SRSaveGameDir, result);
                _ = MetroMessage("Congratulations", "Nothing is better than experience!");
                ReadBackups();
                UpdateTitle();
            }
        }

        private void UpdateTitle()
        {
            this.Dispatcher.Invoke(() => {
                this.Title = "SnowRunner-Tool v" + aVersion;
                if (File.Exists(SRsaveGameFile))
                {
                    string money = CheatGame.GetMoney(SRsaveGameFile);
                    string xp = CheatGame.GetXp(SRsaveGameFile);
                    Title += " | Money: " + money + " | XP: " + xp;
                }
                else
                {
                    Title = "SnowRunner-Tool v" + aVersion;
                }
            });
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _ = Backup.BackupCurrentSavegame(SRSaveGameDir, MyBackupDir, "manual-bak");
            ReadBackups();
        }

        private async void MnPaths2_Click(object sender, RoutedEventArgs e)
        {
            string defaultPath;
            if (!string.IsNullOrEmpty(SRPaksDir))
            {
                defaultPath = SRPaksDir;
            }
            else
            {
                defaultPath = @"C:\Program Files\Epic Games\SnowRunner\en_us\preload\paks\client";
            }
            var result = await MetroInputMessage("INITIAL.PAK", "Enter path to the file \"initial.pak\" (find the file and copy-paste the directory name):", defaultPath);
            // Make sure we have a directory, not a file
            FileAttributes attr = File.GetAttributes(result);
            if (!attr.HasFlag(FileAttributes.Directory))
            {
                result = Directory.GetParent(result).ToString();
            }

            // Make sure we have the correct directory
            if (File.Exists(result + @"\initial.pak"))
            {
                SRPaksDir = result;
                Settings.Default.SRPaksDir = SRPaksDir;
                Settings.Default.Save();
            }
            else
            {
                _ = MetroMessage("File not found!", "The file \"initial.pak\" was not found in " + @result + "!");
            }
        }

        private void MnBackupPak_Click(object sender, RoutedEventArgs e)
        {
            string f = SRPaksDir + @"\initial.pak";
            if (File.Exists(f))
            {
                _ = Backup.BackupSingleFile(f, MyBackupDir, "pakbackup");
                ReadBackups();
            }
            else
            {
                _ = MetroMessage("Settings missing!", "To create a backup of \"initial.pak\" you have to set the path in the menu Settings - Set pak file path.");
            }
        }

        private void MnDeleteBackup_Click(object sender, RoutedEventArgs e)
        {
            var rows = dgBackups.SelectedItems;
            bool wontDelete = false;
            bool changedList = false;
            foreach(var row in rows)
            {
                if (((Backup)row).Type == "Game-Backup")
                {
                    wontDelete = true;
                }
                else
                {
                    string f = MyBackupDir + @"\" + ((Backup)row).BackupName;
                    try
                    {
                        File.Delete(f);
                        changedList = true;
                    }
                    catch (IOException ex)
                    {
                        Log.Error(ex, "Failed to delete backup {BackupFile}", f);
                    }
                }
                if (wontDelete == true)
                {
                    _ = MetroMessage("Skipped deletion", "You have selected a backup the game made by itself. These backups will not be deleted.");
                }
            }
            if (changedList == true) { ReadBackups(); }       
        }

        private async void MnRename_Click(object sender, RoutedEventArgs e)
        {
            if (dgBackups.SelectedItems.Count > 1)
            {
                _ = MetroMessage("Multiple rows selected", "You have selected more than one row. To rename a backup, select one single row.");
                return;
            }
            var menuItem = (MenuItem)sender;
            var contextMenu = (ContextMenu)menuItem.Parent;
            var item = (DataGrid)contextMenu.PlacementTarget;
            var renameItem = (Backup)item.SelectedCells[0].Item;
            if (renameItem.Type == "Game-Backup")
            {
                _ = MetroMessage("Sorry, I won´t do that", "You selected a backup the game made by itself. These backups cannot be deleted.");
            }
            else
            {
                string oldFileName = MyBackupDir + @"\" + renameItem.BackupName;
                string newFileName = await MetroInputMessage("Rename backup file", "Enter the new file name:", renameItem.BackupName);
                newFileName = MyBackupDir + @"\" + newFileName;
                try
                {
                    File.Move(oldFileName, newFileName);
                }
                catch (IOException ex)
                {
                    Log.Error(ex, "Failed to rename backup {BackupFile}", oldFileName);
                }
                ReadBackups();
            }
        }

        private void MnReadme_Click(object sender, RoutedEventArgs e)
        {
            WebLinkMessage("https://github.com/elpatron68/SnowRunner-Tool/blob/master/Readme.md");
        }

        /// <summary>
        /// Create backup when hotkey F2 is pressed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnHookKeyDown(object sender, HookEventArgs e)
        {
            if (e.KeyCode == System.Windows.Forms.Keys.F2)
            {
                if (BackupScheduler.IsActive())
                {
                    Log.Debug("Start backup from hotkey");
                    _ = Backup.BackupCurrentSavegame(SRSaveGameDir, MyBackupDir, "hotkey-bak");
                    ReadBackups();
                }
            }
        }

        private void WebLinkMessage(string url)
        {
            _ = MetroMessage("Just to let you know", "This will open a new page in your web browser.");
            Process.Start(url);
        }

        private void SetAutobackup(int interval)
        {
            // Remove icon from all menu items
            MnAutoOff.Icon = null;
            MnAuto2.Icon = null;
            MnAuto5.Icon = null;
            MnAuto10.Icon = null;
            // Save new setting if changed
            Settings.Default.autobackupinterval = interval;
            Settings.Default.Save();
            // Set check mark icon
            switch (interval)
            {
                case 0:
                    MnAutoOff.Icon = new System.Windows.Controls.Image
                    {
                        Source = new BitmapImage(new Uri("images/baseline_done_black_18dp_1x.png", UriKind.Relative))
                    };
                    break;
                case 2:
                    MnAuto2.Icon = new System.Windows.Controls.Image
                    {
                        Source = new BitmapImage(new Uri("images/baseline_done_black_18dp_1x.png", UriKind.Relative))
                    };
                    break;
                case 5:
                    MnAuto5.Icon = new System.Windows.Controls.Image
                    {
                        Source = new BitmapImage(new Uri("images/baseline_done_black_18dp_1x.png", UriKind.Relative))
                    };
                    break;
                case 10:
                    MnAuto10.Icon = new System.Windows.Controls.Image
                    {
                        Source = new BitmapImage(new Uri("images/baseline_done_black_18dp_1x.png", UriKind.Relative))
                    };
                    break;
            }
            if (interval > 0)
            {
                fswGameBackup.EnableRaisingEvents = true;
            }
            else
            {
                fswGameBackup.EnableRaisingEvents = false;
            }
        }

        private void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            //Wait a second, just to be sure
            Thread.Sleep(1000);
            autoSaveCounter += 1;
            ReadBackups();
            if (autoSaveCounter == Settings.Default.autobackupinterval)
            {
                autoSaveCounter = 0;
                _ = Backup.BackupCurrentSavegame(SRSaveGameDir, MyBackupDir, "auto-bak");
                Log.Debug("FSW-backup created");
            }
        }

        private void MnAutoOff_Click(object sender, RoutedEventArgs e)
        {
            SetAutobackup(0);
        }

        private void MnAuto2_Click(object sender, RoutedEventArgs e)
        {
            SetAutobackup(2);
        }

        private void MnAuto5_Click(object sender, RoutedEventArgs e)
        {
            SetAutobackup(5);
        }

        private void MnAuto10_Click(object sender, RoutedEventArgs e)
        {
            SetAutobackup(10);
        }
    }
}
