using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Home.Base;
using Application = System.Windows.Application;
using ContextMenu = System.Windows.Controls.ContextMenu;

namespace Photos
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static Settings Settings;
        public static string ConfigFile;
        public static List<string> Pics;

        public static int Index = 0;

        private NotifyIcon trayIcon;
        private System.Windows.Forms.ContextMenu trayMenu;

        public static DispatcherTimer UpdateTimer;

        private void ApplicationStartup(object sender, System.Windows.StartupEventArgs e)
        {
            logger.Info("App started");
            logger.Info("HTC Home version: " + E.VersionString);
            logger.Info("Widget version: " + Assembly.GetExecutingAssembly().GetName().Version);

            try
            {
                //check if we must run as administrator
                if (!Directory.Exists(E.Root + "\\Temp"))
                    Directory.CreateDirectory(E.Root + "\\Temp");
                Directory.Delete(E.Root + "\\Temp");
            }
            catch (UnauthorizedAccessException)
            {
                //logger.Warn("Admin privilegies are required. Restarting as admin.");
                var p = new ProcessStartInfo { Verb = "runas", FileName = Assembly.GetExecutingAssembly().Location };
                Process.Start(p);
                Shutdown();
            }

            if (string.IsNullOrEmpty(ConfigFile))
                ConfigFile = E.Root + "\\Photos.config";
            Settings = (Settings)XmlSerializable.Load(typeof(Settings), ConfigFile) ?? new Settings();
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(Settings.Language);
            System.Threading.Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(Settings.Language);

            if (Settings.UseSoftwareRendering)
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            UpdateTimer = new DispatcherTimer();
            if (Settings.CheckForUpdates && File.Exists(E.Root + "\\Update.exe"))
            {
                var a = Settings.Language;
                if (Settings.SilentUpdate)
                    a += " /silent";
                Process.Start(E.Root + "\\Update.exe", a);

                if (App.Settings.UpdateInterval > 0)
                {
                    UpdateTimer.Interval = TimeSpan.FromMinutes(App.Settings.UpdateInterval);
                    UpdateTimer.Tick += UpdateTimerTick;
                    UpdateTimer.Start();
                }
            }


            Pics = new List<string>();
            //History = new Queue<string>();
            if (!string.IsNullOrEmpty(App.Settings.PicsFolder))
            {
                var files = from x in Directory.GetFiles(App.Settings.PicsFolder, "*.*", SearchOption.AllDirectories)
                            where (x.ToLower().EndsWith(".jpg")) || (x.ToLower().EndsWith(".png"))
                            select x;
                Pics.AddRange(files);
            }

            if (Settings.UseTrayIcon)
            {
                if (!e.Args.Contains("-noicon"))
                    AddTrayIcon();

            }
            else
            {
                var jumpList = new JumpList();
                JumpList.SetJumpList(this, jumpList);

                if (File.Exists(E.Root + "\\Weather.exe"))
                {
                    var weatherTask = new JumpTask();
                    weatherTask.Title = Photos.Properties.Resources.JumpListWeatherWidget;
                    weatherTask.WorkingDirectory = E.Root;
                    weatherTask.CustomCategory = Photos.Properties.Resources.JumpListWidgets;
                    weatherTask.ApplicationPath = E.Root + "\\Weather.exe";
                    weatherTask.IconResourcePath = E.Root + "\\Weather.exe";
                    weatherTask.IconResourceIndex = 0;

                    jumpList.JumpItems.Add(weatherTask);
                }


                if (File.Exists(E.Root + "\\Clock.exe"))
                {
                    var clockTask = new JumpTask();
                    clockTask.Title = Photos.Properties.Resources.JumpListClockWidget;
                    clockTask.WorkingDirectory = E.Root;
                    clockTask.CustomCategory = Photos.Properties.Resources.JumpListWidgets;
                    clockTask.ApplicationPath = E.Root + "\\Clock.exe";
                    clockTask.IconResourcePath = E.Root + "\\Clock.exe";
                    clockTask.IconResourceIndex = 0;

                    jumpList.JumpItems.Add(clockTask);
                }

                if (File.Exists(E.Root + "\\News.exe"))
                {
                    var newsTask = new JumpTask();
                    newsTask.Title = Photos.Properties.Resources.JumpListNewsWidget;
                    newsTask.WorkingDirectory = E.Root;
                    newsTask.CustomCategory = Photos.Properties.Resources.JumpListWidgets;
                    newsTask.ApplicationPath = E.Root + "\\News.exe";
                    newsTask.IconResourcePath = E.Root + "\\News.exe";
                    newsTask.IconResourceIndex = 0;

                    jumpList.JumpItems.Add(newsTask);
                }

                if (File.Exists(E.Root + "\\FriendStream.exe"))
                {
                    var friendsTask = new JumpTask();
                    friendsTask.Title = Photos.Properties.Resources.JumpListFriendStreamWidget;
                    friendsTask.WorkingDirectory = E.Root;
                    friendsTask.CustomCategory = Photos.Properties.Resources.JumpListWidgets;
                    friendsTask.ApplicationPath = E.Root + "\\FriendStream.exe";
                    friendsTask.IconResourcePath = E.Root + "\\FriendStream.exe";
                    friendsTask.IconResourceIndex = 0;

                    jumpList.JumpItems.Add(friendsTask);
                }

                if (File.Exists(E.Root + "\\Calendar.exe"))
                {
                    var calendarTask = new JumpTask();
                    calendarTask.Title = Photos.Properties.Resources.JumpListCalendarWidget;
                    calendarTask.WorkingDirectory = E.Root;
                    calendarTask.CustomCategory = Photos.Properties.Resources.JumpListWidgets;
                    calendarTask.ApplicationPath = E.Root + "\\Calendar.exe";
                    calendarTask.IconResourcePath = E.Root + "\\Calendar.exe";
                    calendarTask.IconResourceIndex = 0;

                    jumpList.JumpItems.Add(calendarTask);
                }

                jumpList.Apply();
            }
        }


        void UpdateTimerTick(object sender, EventArgs e)
        {
            if (File.Exists(E.Root + "\\Update.exe"))
            {
                var a = Settings.Language;
                if (Settings.SilentUpdate)
                    a += " /silent";
                Process.Start(E.Root + "\\Update.exe", a);
            }
        }


        public void AddTrayIcon()
        {
            if (trayIcon != null)
            {
                return;
            }
            trayMenu = new System.Windows.Forms.ContextMenu();

            trayIcon = new NotifyIcon
            {
                Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location),
                Text = "HTC Home Apis"
            };
            trayIcon.MouseClick += TrayIconMouseClick;
            trayIcon.Visible = true;
            trayIcon.ContextMenu = trayMenu;

            var closeItem = new System.Windows.Forms.MenuItem();
            closeItem.Text = Photos.Properties.Resources.CloseItem;
            closeItem.Click += (s, e) =>
            {
                foreach (Window window in Windows)
                {
                    window.Close();
                }
            };
            trayMenu.MenuItems.Add(closeItem);

            var widgetsItem = new System.Windows.Forms.MenuItem();
            widgetsItem.Text = Photos.Properties.Resources.JumpListWidgets;

            trayMenu.MenuItems.Add(0, widgetsItem);
            widgetsItem.Visible = false;

            if (File.Exists(E.Root + "\\Weather.exe"))
            {
                widgetsItem.Visible = true;
                var weatherItem = new System.Windows.Forms.MenuItem();
                weatherItem.Text = Photos.Properties.Resources.JumpListWeatherWidget;
                weatherItem.Click += (s, e) => Process.Start(E.Root + "\\Weather.exe");

                widgetsItem.MenuItems.Add(weatherItem);
            }

            if (File.Exists(E.Root + "\\Clock.exe"))
            {
                widgetsItem.Visible = true;
                var clockItem = new System.Windows.Forms.MenuItem();
                clockItem.Text = Photos.Properties.Resources.JumpListClockWidget;
                clockItem.Click += (s, e) => Process.Start(E.Root + "\\Clock.exe");

                widgetsItem.MenuItems.Add(clockItem);
            }

            if (File.Exists(E.Root + "\\News.exe"))
            {
                widgetsItem.Visible = true;
                var newsItem = new System.Windows.Forms.MenuItem();
                newsItem.Text = Photos.Properties.Resources.JumpListNewsWidget;
                newsItem.Click += (s, e) => Process.Start(E.Root + "\\News.exe");

                widgetsItem.MenuItems.Add(newsItem);
            }

            if (File.Exists(E.Root + "\\FriendStream.exe"))
            {
                widgetsItem.Visible = true;
                var friendsItem = new System.Windows.Forms.MenuItem();
                friendsItem.Text = Photos.Properties.Resources.JumpListFriendStreamWidget;
                friendsItem.Click += (s, e) => Process.Start(E.Root + "\\FriendStream.exe");

                widgetsItem.MenuItems.Add(friendsItem);
            }

            if (File.Exists(E.Root + "\\Calendar.exe"))
            {
                widgetsItem.Visible = true;
                var calendarItem = new System.Windows.Forms.MenuItem();
                calendarItem.Text = Photos.Properties.Resources.JumpListCalendarWidget;
                calendarItem.Click += (s, e) => Process.Start(E.Root + "\\Calendar.exe");

                widgetsItem.MenuItems.Add(calendarItem);
            }
        }

        void TrayIconMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                foreach (Window w in Application.Current.Windows)
                {
                    w.Activate();
                }
            }
        }

        public void RemoveTrayIcon()
        {
            if (trayIcon != null)
            {
                trayIcon.MouseClick -= TrayIconMouseClick;
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
        }

        private void ApplicationExit(object sender, ExitEventArgs e)
        {
            RemoveTrayIcon();
            logger.Info("App closed");
        }


        private void ApplicationDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            logger.Error("An error occured.\n" + e.Exception);
        }
    }
}
