using RestoreWindowPlace;
using Serilog;
using SnowRunner_Tool.Properties;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SnowRunner_Tool
{
    /// <summary>
    /// Interaktionslogik für "App.xaml"
    /// </summary>
    public partial class App : Application
    {
        public WindowPlace WindowPlace { get; }

        public App()
        {
            // Set a name of config file
            this.WindowPlace = new WindowPlace("placement.config");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            this.WindowPlace.Save();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            string logfile = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\SRT\log.txt";
            ILogger log = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(logfile, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            MainWindow window = new MainWindow(log, e.Args);
            Current.MainWindow = window;
            window.InitializeComponent();
            window.Show();
        }
    }
}
