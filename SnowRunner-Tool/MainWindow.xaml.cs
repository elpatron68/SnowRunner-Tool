using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Collections;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System.Threading.Tasks;
using System.ComponentModel;
using Serilog;
using System.Diagnostics;
using System.Windows.Media.Imaging;
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
        private static readonly string AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        private KeyboardHook _hook;
        private FileSystemWatcher fswGameBackup;
        private static int autoSaveCounter = 0;
        private readonly ILogger _logger;

        public MainWindow(ILogger logger)
        {
            _logger = logger;
            // Command line options
            //string[] args = Environment.GetCommandLineArgs();
            //Parser.Default.ParseArguments<Options>(args)
            //       .WithParsed<Options>(o =>
            //       {
            //           if (o.EnableLogging == true)
            //           {
            //               enableDebugLogging = true;
            //           }
            //       });
            //guid = GenGuid();

            InitializeComponent();
            ((App)Application.Current).WindowPlace.Register(this);

            _logger.Information("App started");

            bool manualPaths = false;

            // Read directories from settings or find them automatically
            SRBaseDir = string.IsNullOrEmpty(Settings.Default.SRbasedir) ? DiscoverPaths.FindBaseDirectory() : Settings.Default.SRbasedir;
            SRProfile = string.IsNullOrEmpty(Settings.Default.SRprofile) ? DiscoverPaths.FindProfileName(SRBaseDir) : Settings.Default.SRprofile;
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

            if (manualPaths)
            {
                _logger.Information("Manual path input required");
                ShowSettingsDialog();
            }

            // Create directory for our backups
            if (!Directory.Exists(MyBackupDir))
            {
                Directory.CreateDirectory(MyBackupDir);
                _logger.Debug("{MyBackupDir} created ", MyBackupDir);
            }

            // Send directories to log
            _logger.Debug("Set base directory: {SRBaseDir}", SRBaseDir);
            _logger.Information("Set profile directory: {SRProfile}", SRProfile);
            _logger.Debug("Set backup directory: {MyBackupDir}", MyBackupDir);
            _logger.Debug("Set Snowrunner backup directory: {SRBackupDir}", SRBackupDir);
            _logger.Debug("Set Snowrunner save game directory: {SRSaveGameDir}", SRSaveGameDir);

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
            _logger.Information("Operating system: {Os}", os);
            if (!os.ToLower().Contains("windows 7"))
            {
                CheckUpdate();
            }
        }


        /// <summary>
        /// Clear items in datagrid and (re)loads all backups
        /// </summary>
        private void ReadBackups()
        {
            // Add SnowRunner backup directories
            var allBackups = Backup.GetGameBackups(SRBackupDir);
            // Add own zipped backups
            allBackups.AddRange(Backup.GetSrtBackups(MyBackupDir));

            try
            {
                if (allBackups.Count > 0)
                {
                    Dispatcher.Invoke(() => {
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
            catch
            {
                _ = MetroMessage("No Backups", "Sorry, no backups found. Did you play the game, yet? Otherwise: Check path settings.");
            }
        }

        
        private void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            bool copyResult = false;
            if (BackupScheduler.IsActive())
            {
                _ = MetroMessage("Attention!", "The game has to be closed before a backup can be restored.");
            }
            else
            {
                int SavegameSlot=0;

                String source = e.Source.ToString();
                if (source.Contains("#_1"))
                {
                    SavegameSlot = 1;
                }
                if (source.Contains("#_2"))
                {
                    SavegameSlot = 2;
                }
                if (source.Contains("#_3"))
                {
                    SavegameSlot = 3;
                }
                if (source.Contains("#_4"))
                {
                    SavegameSlot = 4;
                }
                
                ContextMenu contextMenu = this.FindName("Restore") as ContextMenu;
                DataGrid item = (DataGrid)contextMenu.PlacementTarget;

                if (dgBackups.SelectedItems.Count > 1)
                {
                    _ = MetroMessage("Restore item", "You can only restore one item, please select a single row.");
                }
                else
                {
                    Backup restoreItem = (Backup)item.SelectedCells[0].Item;

                    // Create a backup before restore
                    _ = Backup.BackupCurrentSavegame(SRSaveGameDir, MyBackupDir, "safety-bak");
                    string backupSource = string.Equals(restoreItem.Type, "Game-Backup", StringComparison.OrdinalIgnoreCase)
                        ? SRBackupDir + @"\" + restoreItem.BackupName
                        : MyBackupDir + @"\" + restoreItem.BackupName;
                    
                    if (string.Equals(restoreItem.Type, "PAK-Backup"))
                    {
                        // Restore initial.pak
                        try
                        {
                            _ = MetroMessage("This function experimental!", "Please report any problems to Github issues, see Help - Web - Report a problem.");
                            copyResult = Backup.RestoreBackup(backupSource, SRPaksDir, -1);
                        }
                        catch
                        {
                            _ = MetroMessage("Something went wrong", "Your backup could not be restored, please restore it manually.");
                            _ = Process.Start("explorer.exe " + backupSource);
                        }
                    }
                    else
                    {
                        copyResult = Backup.RestoreBackup(backupSource, SRSaveGameDir, SavegameSlot);
                    }
                    if (copyResult)
                    {
                        _ = MetroDonateMessage("Next time better luck", "The selected saved game has successfully been restored. A backup of your former save game has been made.\n\n" +
                            "As I may have saved your a** this time (again?), consider to buy me a \U0001F37A or a \U00002615!");
                    }
                    else
                    {
                        _ = MetroMessage("File not found", "The selected backup slot contains no corresponding save game file. Select a valid slot or restore all slots.");
                    }

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
            MenuItem menuItem = (MenuItem)sender;
            ContextMenu contextMenu = (ContextMenu)menuItem.Parent;
            DataGrid item = (DataGrid)contextMenu.PlacementTarget;
            Backup restoreItem = (Backup)item.SelectedCells[0].Item;
            string backupSource = string.Equals(restoreItem.Type, "Game-Backup", StringComparison.OrdinalIgnoreCase)
                ? SRBackupDir + @"\" + restoreItem.BackupName
                : MyBackupDir + @"\" + restoreItem.BackupName;
            _ = Process.Start("explorer.exe", backupSource);
        }


        /// <summary>
        /// Display message dialog
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task<bool> MetroMessage(string title, string message)
        {
            string[] answers = { "Fine", "OK", "Make it so", "Hmmm", "Okay", "Hoot", "Ладно", "Хорошо", "d'accord",
                "Très bien", "Na gut", "Von mir aus", "Let´s go", "Lad os komme afsted", "Mennään", "Andiamo", "Chodźmy" };
            Random r = new Random();
            int rInt = r.Next(0, answers.Length);
            MetroDialogSettings dialogSettings = new MetroDialogSettings();
            dialogSettings.AffirmativeButtonText = answers[rInt] + " [OK]";

            MessageDialogResult dialogResult = await this.ShowMessageAsync(title,
                message,
                MessageDialogStyle.Affirmative, dialogSettings);

            return dialogResult == MessageDialogResult.Affirmative;
        }

        private async Task<bool> MetroDonateMessage(string title, string message)
        {
            string[] answers = { "Fine", "OK", "Make it so", "Hmmm", "Okay", "Hoot", "Ладно", "Хорошо", "d'accord",
                "Très bien", "Na gut", "Von mir aus", "Let´s go", "Lad os komme afsted", "Mennään", "Andiamo", "Chodźmy", "良い" };
            Random r = new Random();
            int rInt = r.Next(0, answers.Length);
            MetroDialogSettings dialogSettings = new MetroDialogSettings();
            dialogSettings.NegativeButtonText = answers[rInt] + " [OK]";
            dialogSettings.AffirmativeButtonText = "Donate (PayPal)";
            dialogSettings.DefaultButtonFocus = MahApps.Metro.Controls.Dialogs.MessageDialogResult.Affirmative;
            
            MessageDialogResult dialogResult = await this.ShowMessageAsync(title,
                message,
                MessageDialogStyle.AffirmativeAndNegative, dialogSettings);
            if (dialogResult == MessageDialogResult.Negative) { return true; }
            if (dialogResult == MessageDialogResult.Affirmative)
            {
                Process.Start("https://www.paypal.com/paypalme/MBusche");
                return true;
            }
            return false;
        }


        private async Task<string> MetroInputMessage(string title, string message, string defaultValue)
        {
            MetroDialogSettings dialogSettings = new MetroDialogSettings();
            dialogSettings.AffirmativeButtonText = "Save";
            dialogSettings.DefaultText = defaultValue;
            dialogSettings.NegativeButtonText = "Cancel";

            string result = await this.ShowInputAsync(title, message, dialogSettings);
            return result;
        }


        private void MnuAbout_Click(object sender, RoutedEventArgs e)
        {
            _ = MetroMessage("About", "SnowRunner-Tool\n\nVersion " + AssemblyVersion + "\n(c) 2020 elpatron68\nhttps://github.com/elpatron68/SnowRunner-Tool/");
        }


        private void MnuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }


        private void MnuIssues_Click(object sender, RoutedEventArgs e)
        {
            WebLinkMessage("https://github.com/elpatron68/SnowRunner-Tool/issues");
        }


        private void MnuSRTLicense_Click(object sender, RoutedEventArgs e)
        {
            _ = MetroMessage("License", "DO WHAT THE FUCK YOU WANT TO PUBLIC LICENSE\n\nDO WHAT THE FUCK YOU WANT TO PUBLIC LICENSE\nTERMS AND CONDITIONS FOR COPYING, DISTRIBUTION AND MODIFICATION\n\n0. You just DO WHAT THE FUCK YOU WANT TO.");
        }


        private void MnuReload_Click(object sender, RoutedEventArgs e)
        {
            ReadBackups();
            _ = CheatGame.GetMoney(SRsaveGameFile, 1);
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
                    (int, string) r = await UpdateCheck.CheckGithubReleses(AssemblyVersion);
                    int result = r.Item1;
                    string url = r.Item2;
                    if (result > 0)
                    {
                        _ = MetroMessage("Update check", "An update is available.\n\nThe download will start in your web browser immediately after you clicked ok.");
                        _ = Process.Start(url);
                    }
                    else
                    {
                        _ = result < 0
                            ? MetroMessage("Update check", "You are in front of the rest of the world!")
                            : MetroMessage("Update check", "You are using the latest version.");
                    }
                }
            }
        }


        private async void CheckUpdate()
        {
            (int, string) r = await UpdateCheck.CheckGithubReleses(AssemblyVersion);
            int result = r.Item1;
            if (result > 0)
            {
                ToastNote.Notify("Update available", "A new version of SnowRunner-Tool is available. See menu 'Help - Check for update' to download the latest version.");
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
            gamePath.fileToCheck = SRSaveGameDir + @"\CompleteSave.dat";
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
            int SavegameSlot = 0;

            String source = e.Source.ToString();
            if (source.Contains("#_1"))
            {
                SavegameSlot = 1;
            }
            if (source.Contains("#_2"))
            {
                SavegameSlot = 2;
            }
            if (source.Contains("#_3"))
            {
                SavegameSlot = 3;
            }
            if (source.Contains("#_4"))
            {
                SavegameSlot = 4;
            }

            string oldMoneyString = CheatGame.GetMoney(SRsaveGameFile, SavegameSlot);
            
            if (oldMoneyString == "n/a")
            {
                _ = MetroMessage("File not found", "The selected backup slot contains no corresponding save game file. Select a valid slot.");
                return;
            }
            
            int oldMoney = int.Parse(oldMoneyString);
            string result = await MetroInputMessage("Money Cheat", "Enter the amount of money you´d like to have", oldMoneyString);
            if (!string.IsNullOrEmpty(result))
            {
                // Create a backup before changing the file
                _ = Backup.BackupCurrentSavegame(SRSaveGameDir, MyBackupDir, "cheat-bak");
                _ = CheatGame.SaveMoney(SRsaveGameFile, result, SavegameSlot);
                int moneyUpgrade = int.Parse(result) - oldMoney;
                _ = MetroMessage("Congratulations", "You won " + moneyUpgrade.ToString() + " coins.");
                ReadBackups();
                UpdateTitle();
            }
        }


        private async void MnXpCheat_Click(object sender, RoutedEventArgs e)
        {
            int SavegameSlot = 0;

            String source = e.Source.ToString();
            if (source.Contains("#_1"))
            {
                SavegameSlot = 1;
            }
            if (source.Contains("#_2"))
            {
                SavegameSlot = 2;
            }
            if (source.Contains("#_3"))
            {
                SavegameSlot = 3;
            }
            if (source.Contains("#_4"))
            {
                SavegameSlot = 4;
            }

            string xp = CheatGame.GetXp(SRsaveGameFile, SavegameSlot);
            if (xp == "n/a")
            {
                _ = MetroMessage("File not found", "The selected backup slot contains no corresponding save game file. Select a valid slot.");
                return;
            }

            string result = await MetroInputMessage("XP Cheat", "Enter the amount of XP you´d like to have.",
                                                    xp);
            if (!string.IsNullOrEmpty(result))
            {
                // Create a backup before changing the file
                _ = Backup.BackupCurrentSavegame(SRSaveGameDir, MyBackupDir, "cheat-bak");
                bool success = CheatGame.SaveXp(SRSaveGameDir, result, SavegameSlot);
                if (success)
                {
                    _ = MetroMessage("Congratulations", "Nothing is better than experience!");
                }
                else
                {
                    _ = MetroMessage("File not found", "The selected backup slot contains no corresponding save game file. Select a valid slot.");
                }
                
                ReadBackups();
                UpdateTitle();
            }
        }


        private void UpdateTitle()
        {
            Dispatcher.Invoke(() => {
                Title = "SnowRunner-Tool v" + AssemblyVersion;
                if (File.Exists(SRsaveGameFile))
                {
                    string money = CheatGame.GetMoney(SRsaveGameFile, 1);
                    string xp = CheatGame.GetXp(SRsaveGameFile, 1);
                    Title += " | Money: " + money + " | XP: " + xp + " (Slot #1)";
                }
                else
                {
                    Title = "SnowRunner-Tool v" + AssemblyVersion;
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
            string result = await MetroInputMessage("INITIAL.PAK", "Enter path to the file \"initial.pak\" (find the file and copy-paste the directory name):", defaultPath);
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
            IList rows = dgBackups.SelectedItems;
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
                        _logger.Error(ex, "Failed to delete backup {BackupFile}", f);
                    }
                }
                if (wontDelete)
                {
                    _ = MetroMessage("Skipped deletion", "You have selected a backup the game made by itself. These backups will not be deleted.");
                }
            }
            if (changedList) { ReadBackups(); }
        }


        private async void MnRename_Click(object sender, RoutedEventArgs e)
        {
            if (dgBackups.SelectedItems.Count > 1)
            {
                _ = MetroMessage("Multiple rows selected", "You have selected more than one row. To rename a backup, select one single row.");
                return;
            }
            MenuItem menuItem = (MenuItem)sender;
            ContextMenu contextMenu = (ContextMenu)menuItem.Parent;
            DataGrid item = (DataGrid)contextMenu.PlacementTarget;
            Backup renameItem = (Backup)item.SelectedCells[0].Item;
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
                    _logger.Error(ex, "Failed to rename backup {BackupFile}", oldFileName);
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
                    _logger.Debug("Start backup from hotkey");
                    _ = Backup.BackupCurrentSavegame(SRSaveGameDir, MyBackupDir, "hotkey-bak");
                    ReadBackups();
                }
            }
        }

        private void WebLinkMessage(string url)
        {
            _ = MetroMessage("Just to let you know", "This will open a new page in your web browser.");
            _ = Process.Start(url);
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
                    MnAutoOff.Icon = new Image
                    {
                        Source = new BitmapImage(new Uri("images/baseline_done_black_18dp_1x.png", UriKind.Relative))
                    };
                    break;
                case 2:
                    MnAuto2.Icon = new Image
                    {
                        Source = new BitmapImage(new Uri("images/baseline_done_black_18dp_1x.png", UriKind.Relative))
                    };
                    break;
                case 5:
                    MnAuto5.Icon = new Image
                    {
                        Source = new BitmapImage(new Uri("images/baseline_done_black_18dp_1x.png", UriKind.Relative))
                    };
                    break;
                case 10:
                    MnAuto10.Icon = new Image
                    {
                        Source = new BitmapImage(new Uri("images/baseline_done_black_18dp_1x.png", UriKind.Relative))
                    };
                    break;
                default:
                    break;
            }
            fswGameBackup.EnableRaisingEvents = interval > 0;
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
                _logger.Debug("FSW-backup created");
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
