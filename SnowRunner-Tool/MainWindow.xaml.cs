﻿using System;
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
        private string SRBackupDir;
        private string MyBackupDir;
        private string SRSaveGameDir;
        private string SRsaveGameFile;
        private readonly string guid;
        private readonly string aVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        private bool enableDebugLogging;

        public MainWindow()
        {
            // Command line options
            string[] args = Environment.GetCommandLineArgs();
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed<Options>(o =>
                   {
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
                myLog = new LoggerConfiguration().MinimumLevel.Debug().
                    Enrich.WithProperty("Application", "SnowRunnerTool").
                    Enrich.WithProperty("version", aVersion).
                    Enrich.WithProperty("guid", guid).
                    WriteTo.Graylog
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
                myLog = new LoggerConfiguration().MinimumLevel.Information().
                    Enrich.WithProperty("Application", "SnowRunnerTool").
                    Enrich.WithProperty("version", aVersion).
                    Enrich.WithProperty("guid", guid).
                    WriteTo.Graylog
                                    (new GraylogSinkOptions
                                    {
                                        HostnameOrAddress = Settings.Default.LogHostName,
                                        Port = Settings.Default.LogPort
                                    }
                                    ).CreateLogger();
            }
            Log.Logger = myLog;
            Log.Information("App started");

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
                SRProfile = DiscoverPaths.FindProfileName(SRBaseDir);
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
            UpdateTitle();
            

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
            var allBackups = Backup.GetBackups(SRBackupDir);
            // Add own zipped backups
            allBackups.AddRange(Backup.GetOtherBackups(MyBackupDir, "Tool-Backup"));
                        
            if (allBackups.Count > 0)
            {
                dgBackups.ItemsSource = allBackups;
                dgBackups.Items.SortDescriptions.Clear();
                dgBackups.Items.SortDescriptions.Add(new SortDescription("Timestamp", ListSortDirection.Descending));
                dgBackups.Items.Refresh();
            }
            UpdateTitle();
        }


        /// <summary>
        /// Try to find the profile directory name
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        //private string findProfileName(string snowRunnerBaseDirectory)
        //{
        //    string searchPath = @SRBaseDir + @"\storage";
        //    try
        //    {
        //        string[] subdirectoryEntries = Directory.GetDirectories(searchPath);
        //        string pattern = @"^[A-Fa-f0-9]+$";
        //        foreach (string subdirectory in subdirectoryEntries)
        //        {
        //            Log.Debug("Profile candidate {ProfileCandidate}", subdirectory);
        //            if (!subdirectory.Contains("backupSlots"))
        //            {
        //                // Check if subdirectory is hex string
        //                string dirName = new DirectoryInfo(subdirectory).Name;
        //                if (Regex.IsMatch(dirName, pattern))
        //                {
        //                    string profiledir = new DirectoryInfo(subdirectory).Name;
        //                    Log.Debug("Profile {ProfileDir} found", profiledir);
        //                    return profiledir;
        //                }
        //            }
        //        }
        //    }
        //    catch
        //    {
        //        Log.Warning("No profile directory found!");
        //        return null;
        //    }
        //    return null;
        //}

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
            Backup.BackupCurrentSavegame(SRSaveGameDir, MyBackupDir);
            readBackups();
            string backupSource = string.Empty;
            if (string.Equals(restoreItem.Type,"Game-Backup", StringComparison.OrdinalIgnoreCase))
            {
                backupSource = SRBackupDir + @"\" + restoreItem.BackupName;
            }
            else
            {
                backupSource = MyBackupDir + @"\" + restoreItem.BackupName;
            }
            Backup.BackupCurrentSavegame(SRSaveGameDir, MyBackupDir);
            Backup.RestoreBackup(backupSource, SRSaveGameDir);
            _ = MetroMessage("Next time better luck", "The selected saved game has been restored. A backup of your former save game has been saved.");
            readBackups();
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
            Process.Start("https://github.com/elpatron68/SnowRunner-Tool/issues");
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
            readBackups();
            var m = CheatGame.GetMoney(SRsaveGameFile);
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
            ShowSettingsDialog();
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
            var r = await UpdateCheck.CheckGithubReleses(aVersion);
            int result = r.Item1;
            string url = r.Item2;
            if (result > 0)
            {
                _ = MetroMessage("Update check", "An update is available.\n\nThe download will start after you clicked ok.");
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
                _ = CheatGame.SaveMoney(SRsaveGameFile, result);
                int moneyUpgrade = int.Parse(result) - oldMoney;
                Log.Information("MoneyUpgrade {MoneyUpgrade}", moneyUpgrade);
                _ = MetroMessage("Congratulations", "You won " + moneyUpgrade.ToString() + " coins.");
                UpdateTitle();
            }
        }


        private async void MnXp_Click(object sender, RoutedEventArgs e)
        {
            string xp = CheatGame.GetXp(SRsaveGameFile);
            string result = await MetroInputMessage("XP Cheat", "Enter the amount of XP you´d like to have",
                                                    xp);
            if (!string.IsNullOrEmpty(result))
            {
                CheatGame.SaveXp(SRSaveGameDir, result);
                _ = MetroMessage("Congratulations", "Nothing is better than experience!");
            }
        }

        private void UpdateTitle()
        {
            this.Title = "SnowRunner-Tool v" + aVersion;
            if (File.Exists(SRsaveGameFile))
            {
                string money = CheatGame.GetMoney(SRsaveGameFile);
                string xp = CheatGame.GetXp(SRsaveGameFile);
                Title += " | Money: " + money + " | XP: " + xp;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _ = Backup.BackupCurrentSavegame(SRSaveGameDir, MyBackupDir);
        }
    }
}
