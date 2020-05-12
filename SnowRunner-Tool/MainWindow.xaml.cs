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

namespace SnowRunner_Tool
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string @SRBaseDir;
        private string SRProfile;
        private string @SRBackupDir;

        public MainWindow()
        {
            InitializeComponent();
            @SRBaseDir = findBaseDirectory();

            SRProfile = findProfileName(@SRBaseDir);
            @SRBackupDir = @SRBaseDir + @"\storage\BackupSlots\" + SRProfile;
           
            dgBackups.AutoGenerateColumns = true;
            dgBackups.ItemsSource = getBackups(@SRBackupDir); 
            
            sr_p.Content = @SRBaseDir;
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
            restoreBackup(restoreItem.DirectoryName);
        }

        private void restoreBackup(string directory)
        {

        }

        private void backupCurrentSavegame(string directory)
        {
            string startPath = directory;
            string zipPath = @".\result.zip";

            ZipFile.CreateFromDirectory(startPath, zipPath);
        }

    }
}
