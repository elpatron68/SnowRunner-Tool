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
using System.Windows.Shapes;
using MahApps.Metro.Controls;
using Microsoft.Win32;

namespace SnowRunner_Tool
{
    /// <summary>
    /// Interaktionslogik für InputGamePath.xaml
    /// </summary>
    public partial class InputGamePath : MetroWindow
    {
        public InputGamePath()
        {
            InitializeComponent();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                // FileName = "CompleteSave.dat",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Filter = "SnowRunner save game files (*.dat)|*.dat|All files (*.*)|*.*",
                CheckPathExists = true,
            };
            if (ofd.ShowDialog() == true) {
                string filepath = ofd.FileName;
                string gamedir = Directory.GetParent(Directory.GetParent(Directory.GetParent(filepath).ToString()).ToString()).ToString();
                TxSaveGamePath.Text = gamedir;
            }
        }
    }
}
