using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace SnowRunner_Tool
{
    class BackupScheduler
    {
        public static void startScheduledBackup(string action, string SRSaveGameDir, string MyBackupDir)
        {
            int counter = 0;
            DispatcherTimer backupTimer = new DispatcherTimer();

            if (action != "start")
            {
                backupTimer.Stop();
            }
            else
            {
                backupTimer.Tick += delegate (object s, EventArgs args)
                {
                    // Console.WriteLine("Timer ticked: " + counter++);
                    if (!Directory.Exists(MyBackupDir))
                    {
                        Directory.CreateDirectory(MyBackupDir);
                    }
                    string startPath = SRSaveGameDir;
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd_hh-mm-ss", CultureInfo.CurrentCulture);
                    string zipPath = MyBackupDir + @"\scheduled_backup-" + timestamp + ".zip";
                    ZipFile.CreateFromDirectory(startPath, zipPath);
                    Console.WriteLine("Backup #" + counter++ + ": " + zipPath);
                    messageCon();
                };
                backupTimer.Interval = new TimeSpan(0, 0, 30);
                backupTimer.Start();
            }
        }

        public static void messageCon()
        {
            Console.WriteLine("Hello World!");
        }
        public static void processWatcher()
        {
            DispatcherTimer processSearchTimer = new DispatcherTimer();


        }

        public static bool isActive()
        {
            Process[] pname = Process.GetProcessesByName("notepad");
            if (pname.Length == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

    }
}
