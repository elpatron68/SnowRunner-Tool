using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO.Compression;
using Path = System.IO.Path;

namespace SnowRunner_Tool
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
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
            dgBackups.ItemsSource = getBackups(@SRBackupDir);
            sr_p.Content = SRBaseDir;
        }

        private List<Backup> getBackups(string backupdir)
        {
            List<Backup> backups = new List<Backup>();
            string[] subdirectoryEntries = Directory.GetDirectories(backupdir);
            foreach (string subdirectory in subdirectoryEntries)
            {
                string dir = new DirectoryInfo(subdirectory).Name;
                DateTime timestamp = Directory.GetCreationTime(subdirectory);
                backups.Add(new Backup() { DirectoryName = dir, Timestamp = timestamp });
            }
            return backups;
        }

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
            restoreBackup(restoreItem.DirectoryName);
            MessageBox.Show("The selected save game backup has been restored. A backup of your former save game has been saved in " + MyBackupDir);
        }

        private void restoreBackup(string directory)
        {
            string source = SRBackupDir + @"\" + directory;
            dCopy(source, SRSaveGameDir, false, true);
        }

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
            return zipPath;
        }
        private static void dCopy(string sourceDirName, string destDirName, bool copySubDirs, bool overwriteExisting)
        {
            foreach (string newPath in Directory.GetFiles(sourceDirName, "*.*", SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(sourceDirName, destDirName), true);
        }

        private void BackupCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void BackupCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            backupCurrentSavegame();
            MessageBox.Show("Your current save game was backed up to the folder " + MyBackupDir + ".");
        }

    }
}
