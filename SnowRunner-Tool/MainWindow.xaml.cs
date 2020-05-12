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
using Label = System.Windows.Controls.Label;
using MessageBox = System.Windows.MessageBox;

namespace SnowRunner_Tool
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window

    {
        public MainWindow()
        {
            InitializeComponent();
            string @base = findBaseDirectory();
            string profile = findProfileName(@base);
            string backupdir = @base + @"\storage\BackupSlots\" + profile;
           
            dgBackups.AutoGenerateColumns = true;
            dgBackups.ItemsSource = getBackups(backupdir); ;
            
            sr_p.Content = @base;
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
            var clickedItem = FindClickedItem(sender);
            if (clickedItem != null)
            {
                MessageBox.Show(" Viewing: " + clickedItem.Content);
            }
        }

        private static Label FindClickedItem(object sender)
        {
            var mi = sender as MenuItem;
            if (mi == null)
            {
                return null;
            }

            var cm = mi.CommandParameter as ContextMenu;
            if (cm == null)
            {
                return null;
            }

            return cm.PlacementTarget as Label;
        }

    }
}
