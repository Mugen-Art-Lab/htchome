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
using FriendStream.Domain;
using Home.Base;
using Application = System.Windows.Application;
using ContextMenu = System.Windows.Controls.ContextMenu;

namespace FriendStream
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public static Settings Settings;
        public static string ConfigFile;

        private NotifyIcon trayIcon;
        private System.Windows.Forms.ContextMenu trayMenu;

        public static DispatcherTimer UpdateTimer;
        public static ProviderManager ProviderManager;

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
                ConfigFile = E.Root + "\\FriendStream.config";
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

            if (Settings.ActiveProviders.Count == 0)
                Settings.ActiveProviders.Add("Facebook");

            switch (Settings.Style)
            {
                case Styles.Classic:
                    StartupUri = new Uri("/Windows/FStream.xaml", UriKind.Relative);
                    break;
                case Styles.Modern:
                    StartupUri = new Uri("/Windows/ModernFStream.xaml", UriKind.Relative);
                    break;
            }

            ProviderManager = new ProviderManager();
            ProviderManager.FindProviders();

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
                    weatherTask.Title = FriendStream.Properties.Resources.JumpListWeatherWidget;
                    weatherTask.WorkingDirectory = E.Root;
                    weatherTask.CustomCategory = FriendStream.Properties.Resources.JumpListWidgets;
                    weatherTask.ApplicationPath = E.Root + "\\Weather.exe";
                    weatherTask.IconResourcePath = E.Root + "\\Weather.exe";
                    weatherTask.IconResourceIndex = 0;

                    jumpList.JumpItems.Add(weatherTask);
                }


                if (File.Exists(E.Root + "\\Clock.exe"))
                {
                    var clockTask = new JumpTask();
                    clockTask.Title = FriendStream.Properties.Resources.JumpListClockWidget;
                    clockTask.WorkingDirectory = E.Root;
                    clockTask.CustomCategory = FriendStream.Properties.Resources.JumpListWidgets;
                    clockTask.ApplicationPath = E.Root + "\\Clock.exe";
                    clockTask.IconResourcePath = E.Root + "\\Clock.exe";
                    clockTask.IconResourceIndex = 0;

                    jumpList.JumpItems.Add(clockTask);
                }

                if (File.Exists(E.Root + "\\Photos.exe"))
                {
                    var photosTask = new JumpTask();
                    photosTask.Title = FriendStream.Properties.Resources.JumpListPhotosWidget;
                    photosTask.WorkingDirectory = E.Root;
                    photosTask.CustomCategory = FriendStream.Properties.Resources.JumpListWidgets;
                    photosTask.ApplicationPath = E.Root + "\\Photos.exe";
                    photosTask.IconResourcePath = E.Root + "\\Photos.exe";
                    photosTask.IconResourceIndex = 0;

                    jumpList.JumpItems.Add(photosTask);
                }

                if (File.Exists(E.Root + "\\News.exe"))
                {
                    var newsTask = new JumpTask();
                    newsTask.Title = FriendStream.Properties.Resources.JumpListNewsWidget;
                    newsTask.WorkingDirectory = E.Root;
                    newsTask.CustomCategory = FriendStream.Properties.Resources.JumpListWidgets;
                    newsTask.ApplicationPath = E.Root + "\\News.exe";
                    newsTask.IconResourcePath = E.Root + "\\News.exe";
                    newsTask.IconResourceIndex = 0;

                    jumpList.JumpItems.Add(newsTask);
                }

                if (File.Exists(E.Root + "\\Calendar.exe"))
                {
                    var calendarTask = new JumpTask();
                    calendarTask.Title = FriendStream.Properties.Resources.JumpListCalendarWidget;
                    calendarTask.WorkingDirectory = E.Root;
                    calendarTask.CustomCategory = FriendStream.Properties.Resources.JumpListWidgets;
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
            closeItem.Text = FriendStream.Properties.Resources.CloseItem;
            closeItem.Click += (s, e) =>
            {
                foreach (Window window in Windows)
                {
                    window.Close();
                }
            };
            trayMenu.MenuItems.Add(closeItem);

            var widgetsItem = new System.Windows.Forms.MenuItem();
            widgetsItem.Text = FriendStream.Properties.Resources.JumpListWidgets;

            trayMenu.MenuItems.Add(0, widgetsItem);
            widgetsItem.Visible = false;

            if (File.Exists(E.Root + "\\Weather.exe"))
            {
                widgetsItem.Visible = true;
                var weatherItem = new System.Windows.Forms.MenuItem();
                weatherItem.Text = FriendStream.Properties.Resources.JumpListWeatherWidget;
                weatherItem.Click += (s, e) => Process.Start(E.Root + "\\Weather.exe");

                widgetsItem.MenuItems.Add(weatherItem);
            }

            if (File.Exists(E.Root + "\\Clock.exe"))
            {
                widgetsItem.Visible = true;
                var clockItem = new System.Windows.Forms.MenuItem();
                clockItem.Text = FriendStream.Properties.Resources.JumpListClockWidget;
                clockItem.Click += (s, e) => Process.Start(E.Root + "\\Clock.exe");

                widgetsItem.MenuItems.Add(clockItem);
            }

            if (File.Exists(E.Root + "\\Photos.exe"))
            {
                widgetsItem.Visible = true;
                var photosItem = new System.Windows.Forms.MenuItem();
                photosItem.Text = FriendStream.Properties.Resources.JumpListPhotosWidget;
                photosItem.Click += (s, e) => Process.Start(E.Root + "\\Photos.exe");

                widgetsItem.MenuItems.Add(photosItem);
            }

            if (File.Exists(E.Root + "\\News.exe"))
            {
                widgetsItem.Visible = true;
                var newsItem = new System.Windows.Forms.MenuItem();
                newsItem.Text = FriendStream.Properties.Resources.JumpListClockWidget;
                newsItem.Click += (s, e) => Process.Start(E.Root + "\\News.exe");

                widgetsItem.MenuItems.Add(newsItem);
            }

            if (File.Exists(E.Root + "\\Calendar.exe"))
            {
                widgetsItem.Visible = true;
                var calendarItem = new System.Windows.Forms.MenuItem();
                calendarItem.Text = FriendStream.Properties.Resources.JumpListClockWidget;
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
    }
}
