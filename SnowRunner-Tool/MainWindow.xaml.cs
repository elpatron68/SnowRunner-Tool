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
using System.Collections.Generic;
using CommandLine;
using System.Linq;
using Serilog.Core;
using System.Linq.Expressions;
using Windows.Foundation.Metadata;
using Microsoft.Xaml.Behaviors;
using Winforms = System.Windows.Forms;

namespace SnowRunner_Tool
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private string SRProfile;
        private string SRBackupDir;
        private string MyBackupDir;
        private string SRsaveGameFile;
        private string Platform;
        private string SavegameExtension;
        private static readonly string AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        private KeyboardHook _hook;
        private FileSystemWatcher fswGameBackup;
        private static int autoSaveCounter = 0;
        private readonly ILogger _logger;
        private readonly string[] answers = { "Fine", "OK", "Make it so", "Hmmm", "Okay", "Hoot", "Ладно", "Хорошо", "D'accord",
                "Très bien", "Na gut", "Von mir aus", "Let´s go", "Lad os komme afsted", "Mennään", "Andiamo", "Chodźmy", "良い", 
                "Hau rein", "Go on" };

        public MainWindow(ILogger logger, string[] args)
        {
            _logger = logger;
            // Command line options
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed<Options>(o =>
                   {
                       Platform = o.Platform.ToLower();
                   });

            InitializeComponent();
            ((App)Application.Current).WindowPlace.Register(this);

            _logger.Information("App started");
            _logger.Information("Version: " + AssemblyVersion);

            if (Platform == null)
            {
                int result = DiscoverPaths.Autodiscover();
                switch (result)
                {
                    case 0:
                        // No platform found
                        Winforms.MessageBox.Show("No saved games were found. You have to have at least one save game to use SnowRunner-Tool.\n\n" + 
                            "Restart SnowRunner-Tool after you have saved the game at least once.", "Platform detection");
                        // Close();
                        Application.Current.Shutdown();
                        break;
                    case 1:
                        // Epic
                        Platform = "epic";
                        break;
                    case 2:
                        // Steam
                        Platform = "steam";
                        break;
                    case 3:
                        // Epic and Steam
                        Winforms.MessageBoxManager.OK = "Steam";
                        Winforms.MessageBoxManager.Cancel = "Epic Games";
                        Winforms.MessageBoxManager.Register();
                        var answer = Winforms.MessageBox.Show("Saved games from the Epic Games- and Steam- version of SnowRunner were found.\n\n" + 
                            "Select the platform you want to use.", "Select SnowRunner platform", Winforms.MessageBoxButtons.OKCancel);
                        Winforms.MessageBoxManager.Unregister();
                        switch (answer)
                        {
                            case Winforms.DialogResult.OK:
                                Platform = "steam";
                                break;
                            case Winforms.DialogResult.Cancel:
                                Platform = "epic";
                                break;
                        }
                        break;
                    default:
                        break;
                }
            }

            _logger.Information("Platform: " + Platform);

            // Read directories from settings or find them automatically
            SRProfile = DiscoverPaths.FindBaseDirectory(Platform);
            MyBackupDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\SRToolBackup\" + Platform;

            if (Platform == "epic")
            {
                SRBackupDir = Directory.GetParent(SRProfile) + @"\BackupSlots\" + SRProfile.Split('\\').Last();
                // Move old backups from former versions to new location
                EpicParser.MoveOldBackupsToNewLocation(SRProfile, MyBackupDir);
            }
            
            // Set derived directories
            if (Platform == "epic")
            {
                SavegameExtension = "dat";
            }
            else if (Platform == "steam")
            {
                SavegameExtension = "cfg";
            }
            
            SRsaveGameFile = SRProfile + @"\CompleteSave." + SavegameExtension;

            // Create directory for our backups
            if (!Directory.Exists(MyBackupDir))
            {
                Directory.CreateDirectory(MyBackupDir);
                _logger.Debug("{MyBackupDir} created ", MyBackupDir);
            }

            // Send directories to log
            _logger.Information("Set profile directory: {SRProfile}", SRProfile);
            _logger.Debug("Set backup directory: {MyBackupDir}", MyBackupDir);
            _logger.Debug("Set Snowrunner backup directory: {SRBackupDir}", SRBackupDir);
            _logger.Debug("Set Snowrunner save game directory: {SRSaveGameDir}", SRProfile);

            // Fill Datagrid
            dgBackups.AutoGenerateColumns = true;
            ReadBackups();

            // Register Autobackup FileSystemWatcher
            _logger.Information("Registering FileSystemWatcher");
            fswGameBackup = new FileSystemWatcher
            {
                Path = SRProfile,
                Filter = "CompleteSave*.*"
            };
            fswGameBackup.Changed += FileSystemWatcher_Changed;
            SetAutobackup(Settings.Default.autobackupinterval);

            // Register global hotkey
            _logger.Information("Registering hotkey");
            _hook = new KeyboardHook();
            _hook.KeyDown += new KeyboardHook.HookEventHandler(OnHookKeyDown);

            // Set value of some UI elements
            lblSnowRunnerPath.Content = SRProfile;
            lblBackupDirectory.Content = MyBackupDir;
            lbTotalBackups.Content = "Total backups: " + dgBackups.Items.Count;

            // Check for update (Win8+)
            string os = OsInfo.GetOSInfo();
            _logger.Information("Operating system: {Os}", os);
            if (!os.ToLower().Contains("windows 7"))
            {
                _logger.Information("Searching update");
                CheckUpdate();
            }
        }


        /// <summary>
        /// Clear items in datagrid and (re)loads all backups
        /// </summary>
        private void ReadBackups()
        {
            _logger.Information("Reading list of existin backups");
            List<Backup> allBackups = new List<Backup>();
            // Add SnowRunner backup directories
            try
            {
                allBackups.AddRange(Backup.GetGameBackups(SRBackupDir, SavegameExtension));
            }
            catch
            {
                // No existing SnowRunner backups
            }
            _logger.Information("Added backups made by SnowRunner: " + allBackups.Count);

            // Add own zipped backups
            var MyBackups = Backup.GetSrtBackups(MyBackupDir, Platform);
            _logger.Information("Added backups made by SnowRunner: " + MyBackups.Count);
            allBackups.AddRange(MyBackups);
            
            try
            {
                if (allBackups.Count > 0)
                {
                    Dispatcher.Invoke(() =>
                    {
                        dgBackups.ItemsSource = allBackups;
                        dgBackups.Items.SortDescriptions.Clear();
                        dgBackups.Items.SortDescriptions.Add(new SortDescription("Timestamp", ListSortDirection.Descending));
                        _logger.Debug("Refreshing table");
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
                UpdateSaveGameSlotMenus();
            }
            catch
            {
                _ = MetroMessage("No Backups", "Sorry, no backups found. Did you play the game, yet? Otherwise: Check path settings.");
            }
        }

        private void UpdateSaveGameSlotMenus()
        {
            _logger.Debug("Updating save game slot menu items");
            bool saveGameExists;
            string saveFile;

            saveFile = SRProfile + @"\CompleteSave." + SavegameExtension;
            saveGameExists = File.Exists(saveFile);
            MnMoneyCheat1.IsEnabled = saveGameExists;
            MnCopySaveGame1.IsEnabled = saveGameExists;
            MnCopySaveGame2To1.IsEnabled = !saveGameExists;
            MnCopySaveGame3To1.IsEnabled = !saveGameExists;
            MnCopySaveGame4To1.IsEnabled = !saveGameExists;
            MnXp1.IsEnabled = saveGameExists;

            saveFile = SRProfile + @"\CompleteSave1." + SavegameExtension; ;
            saveGameExists = File.Exists(saveFile);
            MnMoneyCheat2.IsEnabled = saveGameExists;
            MnCopySaveGame2.IsEnabled = saveGameExists;
            MnCopySaveGame1To2.IsEnabled = !saveGameExists;
            MnCopySaveGame3To2.IsEnabled = !saveGameExists;
            MnCopySaveGame4To2.IsEnabled = !saveGameExists;
            MnXp2.IsEnabled = saveGameExists;

            saveFile = SRProfile + @"\CompleteSave2." + SavegameExtension; ;
            saveGameExists = File.Exists(saveFile);
            MnMoneyCheat3.IsEnabled = saveGameExists;
            MnCopySaveGame3.IsEnabled = saveGameExists;
            MnCopySaveGame1To3.IsEnabled = !saveGameExists;
            MnCopySaveGame2To3.IsEnabled = !saveGameExists;
            MnCopySaveGame4To3.IsEnabled = !saveGameExists;
            MnXp3.IsEnabled = saveGameExists;

            saveFile = SRProfile + @"\CompleteSave3." + SavegameExtension; ;
            saveGameExists = File.Exists(saveFile);
            MnMoneyCheat4.IsEnabled = saveGameExists;
            MnCopySaveGame4.IsEnabled = saveGameExists;
            MnCopySaveGame1To4.IsEnabled = !saveGameExists;
            MnCopySaveGame2To4.IsEnabled = !saveGameExists;
            MnCopySaveGame3To4.IsEnabled = !saveGameExists;
            MnXp4.IsEnabled = saveGameExists;
        }

        private void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            _logger.Information("Start restoring a backup");
            fswGameBackup.EnableRaisingEvents=false;

            bool copyResult = false;
            if (BackupScheduler.IsActive())
            {
                _ = MetroMessage("Attention!", "The game has to be closed before a backup can be restored.");
            }
            else
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
                _logger.Debug("Slot: " + SavegameSlot);

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
                    _logger.Debug("Backing up current save game before restoring");
                    _ = Backup.BackupCurrentSavegame(SRProfile, MyBackupDir, "safety-bak");

                    string backupSource = string.Equals(restoreItem.Type, "Game-Backup", StringComparison.OrdinalIgnoreCase)
                        ? SRBackupDir + @"\" + restoreItem.BackupName
                        : MyBackupDir + @"\" + restoreItem.BackupName;

                    _logger.Information(String.Format("Restoring {0}, slot {1} to {2}", backupSource, SavegameSlot, SRProfile));
                    copyResult = Backup.RestoreBackup(backupSource, SRProfile, SavegameSlot, SavegameExtension);
                    if (copyResult)
                    {
                        _logger.Debug("Restore was successful");
                        _ = MetroDonateMessage("Next time better luck", "The selected saved game has successfully been restored. A backup of your former save game has been made.\n\n" +
                            "As I may have saved your a** this time (again?), consider to buy me a \U0001F37A or a \U00002615!");
                    }
                    else
                    {
                        _logger.Warning("Restore failed");
                        _ = MetroMessage("File not found", "The selected backup slot contains no corresponding save game file. Select a valid slot or restore all slots.");
                    }

                    ReadBackups();
                    fswGameBackup.EnableRaisingEvents = true;
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
            Random r = new Random();
            int rInt = r.Next(0, answers.Length);
            MetroDialogSettings dialogSettings = new MetroDialogSettings();
            dialogSettings.NegativeButtonText = answers[rInt] + " [OK]";
            dialogSettings.AffirmativeButtonText = "Donate (PayPal)";
            dialogSettings.DefaultButtonFocus = MessageDialogResult.Affirmative;
            
            MessageDialogResult dialogResult = await this.ShowMessageAsync(title,
                message,
                MessageDialogStyle.AffirmativeAndNegative, dialogSettings);
            if (dialogResult == MessageDialogResult.Negative) { return true; }
            if (dialogResult == MessageDialogResult.Affirmative)
            {
                Process.Start("https://www.paypal.com/donate/?hosted_button_id=4HC7YCMXQK3N8");
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
            _ = MetroDonateMessage("About", "SnowRunner-Tool\n\nVersion " + AssemblyVersion + "\n(c) 2020-2022 elpatron68\nhttps://github.com/elpatron68/SnowRunner-Tool/");
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
            _ = CheatGame.GetMoney(SRsaveGameFile, 1, SavegameExtension);
        }


        //private void MnPaths_Click(object sender, RoutedEventArgs e)
        //{
        //    ShowSettingsDialog();
        //    ReadBackups();
        //}


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
            try
            {
                (int, string) r = await UpdateCheck.CheckGithubReleses(AssemblyVersion);
                int result = r.Item1;
                if (result > 0)
                {
                    ToastNote.Notify("Update available", "A new version of SnowRunner-Tool is available. See menu 'Help - Check for update' to download the latest version.");
                }
            }
            catch
            {
                _logger.Warning("Update check failed.");
                // No internet connection
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

            string oldMoneyString = CheatGame.GetMoney(SRsaveGameFile, SavegameSlot, SavegameExtension);
            
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
                _ = Backup.BackupCurrentSavegame(SRProfile, MyBackupDir, "cheat-bak");
                _ = CheatGame.SaveMoney(SRsaveGameFile, result, SavegameSlot, SavegameExtension);
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

            string xp = CheatGame.GetXp(SRsaveGameFile, SavegameSlot, SavegameExtension);
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
                _ = Backup.BackupCurrentSavegame(SRProfile, MyBackupDir, "cheat-bak");
                bool success = CheatGame.SaveXp(SRProfile, result, SavegameSlot, SavegameExtension);
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
                Title = "SnowRunner-Tool v" + AssemblyVersion + " (" + Platform + ")";
                if (File.Exists(SRsaveGameFile))
                {
                    string money = CheatGame.GetMoney(SRsaveGameFile, 1, SavegameExtension);
                    string xp = CheatGame.GetXp(SRsaveGameFile, 1, SavegameExtension);
                    Title += " | Money: " + money + " | XP: " + xp + " (Slot #1)";
                }
                else
                {
                    Title = "SnowRunner-Tool v" + AssemblyVersion;
                }
            });
        }


        private void BtnBackupCurrentSave_Click(object sender, RoutedEventArgs e)
        {
            _ = Backup.BackupCurrentSavegame(SRProfile, MyBackupDir, "manual-bak");
            ReadBackups();
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
                        NativeMethods.DeleteFileOrFolder(f);
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
                    _ = Backup.BackupCurrentSavegame(SRProfile, MyBackupDir, "hotkey-bak");
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
            if (Platform != null)
            {
                fswGameBackup.EnableRaisingEvents = interval > 0;
            }
        }

        private void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            //Wait a second, just to be sure
            Thread.Sleep(1000);
            autoSaveCounter += 1;
            if (autoSaveCounter == Settings.Default.autobackupinterval)
            {
                autoSaveCounter = 0;
                _ = Backup.BackupCurrentSavegame(SRProfile, MyBackupDir, "auto-bak");
                ReadBackups();
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


        /// <summary>
        /// Rename Columns Headers and set width
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgBackups_AutoGeneratingColumn(object sender, EventArgs e)
        {
            try
            {
                dgBackups.Columns[3].Header = "Money #1";
                dgBackups.Columns[4].Header = "XP #1";
                dgBackups.Columns[5].Header = "Money #2";
                dgBackups.Columns[6].Header = "XP #2";
                dgBackups.Columns[7].Header = "Money #3";
                dgBackups.Columns[8].Header = "XP #3";
                dgBackups.Columns[9].Header = "Money #4";
                dgBackups.Columns[10].Header = "XP #4";

                for (int i = 3; i < 11; i++)
                {
                    dgBackups.Columns[i].Width = 80;
                }
                dgBackups.Columns[1].Width = 160;
                dgBackups.Columns[2].Width = 120;
            }
            catch
            {
            }
        }

        private void lblSnowRunnerPath_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start("explorer.exe", lblSnowRunnerPath.Content.ToString());
        }

        private void lblBackupDirectory_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start("explorer.exe", lblBackupDirectory.Content.ToString());
        }

        private void MnShowLogFiles_Click(object sender, RoutedEventArgs e)
        {
            string logfiledir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\SRT\";
            Process.Start("explorer.exe", logfiledir);
        }

        private void MnCopySaveGame1To2_Click(object sender, RoutedEventArgs e)
        {
            CheatGame.CopySlotToOtherSlot(1, 2, SRProfile, Platform);
            CopyComplete(1, 2);
        }

        private void MnCopySaveGame1To3_Click(object sender, RoutedEventArgs e)
        {
            CheatGame.CopySlotToOtherSlot(1, 3, SRProfile, Platform);
            CopyComplete(1, 3);
        }

        private void MnCopySaveGame1To4_Click(object sender, RoutedEventArgs e)
        {
            CheatGame.CopySlotToOtherSlot(1, 4, SRProfile, Platform);
            CopyComplete(1, 4);
        }

        private void MnCopySaveGame2To1_Click(object sender, RoutedEventArgs e)
        {
            CheatGame.CopySlotToOtherSlot(2, 1, SRProfile, Platform);
            CopyComplete(2, 1);
        }

        private void MnCopySaveGame2To3_Click(object sender, RoutedEventArgs e)
        {
            CheatGame.CopySlotToOtherSlot(2, 3, SRProfile, Platform);
            CopyComplete(2, 3);
        }

        private void MnCopySaveGame2To4_Click(object sender, RoutedEventArgs e)
        {
            CheatGame.CopySlotToOtherSlot(2, 4, SRProfile, Platform);
            CopyComplete(2, 4);
        }

        private void MnCopySaveGame3To1_Click(object sender, RoutedEventArgs e)
        {
            CheatGame.CopySlotToOtherSlot(3, 1, SRProfile, Platform);
            CopyComplete(3, 1);
        }

        private void MnCopySaveGame3To2_Click(object sender, RoutedEventArgs e)
        {
            CheatGame.CopySlotToOtherSlot(3, 2, SRProfile, Platform);
            CopyComplete(3, 2);
        }

        private void MnCopySaveGame3To4_Click(object sender, RoutedEventArgs e)
        {
            CheatGame.CopySlotToOtherSlot(3, 4, SRProfile, Platform);
            CopyComplete(3, 4);
        }

        private void MnCopySaveGame4To1_Click(object sender, RoutedEventArgs e)
        {
            CheatGame.CopySlotToOtherSlot(4, 1, SRProfile, Platform);
            CopyComplete(4, 1);
        }

        private void MnCopySaveGame4To2_Click(object sender, RoutedEventArgs e)
        {
            CheatGame.CopySlotToOtherSlot(4, 2, SRProfile, Platform);
            CopyComplete(4, 2);
        }

        private void MnCopySaveGame4To3_Click(object sender, RoutedEventArgs e)
        {
            CheatGame.CopySlotToOtherSlot(4, 3, SRProfile, Platform);
            CopyComplete(4, 3);
        }

        private void CopyComplete(int slot1, int slot2)
        {
            _ = MetroMessage("Save game copied", string.Format("Save game slot {0} has been copied to save game slot {1}.", 
                slot1.ToString(), slot2.ToString()));
        }
    }
}
