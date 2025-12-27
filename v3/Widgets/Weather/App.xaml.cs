using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using Home.Base;
using Weather.Domain;
using Application = System.Windows.Application;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MessageBox = System.Windows.MessageBox;
using Home.Packaging;
using System.Windows.Media;
using System.Windows.Interop;

namespace Weather
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public static Settings Settings;
        public static WeatherProviderManager WpManager;
        public static string ConfigFile;

        private NotifyIcon trayIcon;
        private System.Windows.Forms.ContextMenu trayMenu;

        public static DispatcherTimer UpdateTimer;
        public static SoundPlayer SoundPlayer;

        private void ApplicationStartup(object sender, StartupEventArgs e)
        {
            logger.Info("App started");
            logger.Info("HTC Home version: " + E.VersionString);
            logger.Info("Widget version:" + Assembly.GetExecutingAssembly().GetName().Version);

            if (e.Args.Contains("-c") || e.Args.Contains("/c"))
            {
                ConfigFile = e.Args.Single(x => x.EndsWith(".config"));
                logger.Info("Using custom config: " + ConfigFile);
            }

            try
            {
                //check if we must run as administrator
                if (!Directory.Exists(E.Root + "\\Temp"))
                    Directory.CreateDirectory(E.Root + "\\Temp");
                Directory.Delete(E.Root + "\\Temp");
            }
            catch (UnauthorizedAccessException)
            {
                logger.Warn("Admin privilegies are required. Restarting as admin.");
                var p = new ProcessStartInfo { Verb = "runas", FileName = Assembly.GetExecutingAssembly().Location };
                Process.Start(p);
                Shutdown();
            }

            //var fileInfo = new FileInfo(E.Root + "\\Home.Base.dll");
            //if (DateTime.Now.CompareTo(fileInfo.LastWriteTimeUtc.AddMonths(3)) >= 0)
            //{
            //    logger.Error("This version has expired.");
            //    MessageBox.Show(Weather.Properties.Resources.Expired, "HTC Home 3 Expired", MessageBoxButton.OK, MessageBoxImage.Information);
            //}
            if (string.IsNullOrEmpty(ConfigFile))
                ConfigFile = E.Root + "\\Weather.config";
            Settings = (Settings)XmlSerializable.Load(typeof(Settings), ConfigFile) ?? new Settings();
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(Settings.Language);
            System.Threading.Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(Settings.Language);

            if (Settings.UseSoftwareRendering)
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            if (Settings.UseProxy)
            {
                var proxy = new WebProxy();
                if (string.IsNullOrEmpty(Settings.ProxyAddress))
                    GeneralHelper.Proxy = WebRequest.GetSystemWebProxy();
                else
                {
                    proxy.Address = new Uri(Settings.ProxyAddress + ":" + Settings.ProxyPort);
                    proxy.Credentials = new NetworkCredential(Settings.ProxyUsername, Settings.ProxyPassword);
                    GeneralHelper.Proxy = proxy;
                }
            }

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

            if (ExtrasManager.IsExtrasInstalled("4fe4eee3-6480-4007-b1ba-2c5d1d86386d"))
            {
                SoundPlayer = new SoundPlayer();
            }

            WpManager = new WeatherProviderManager();
            WpManager.FindProviders();

            switch (Settings.Style)
            {
                case Styles.Large:
                    StartupUri = new Uri("/Windows/WeatherLarge.xaml", UriKind.Relative);
                    break;
                case Styles.Medium:
                    StartupUri = new Uri("/Windows/WeatherMedium.xaml", UriKind.Relative);
                    break;
                case Styles.Small:
                    StartupUri = new Uri("/Windows/WeatherSmall.xaml", UriKind.Relative);
                    break;
                case Styles.Metro:
                    StartupUri = new Uri("/Windows/WeatherMetro.xaml", UriKind.Relative);
                    break;
            }

            if (e.Args.Length > 0)
            {
                var extensions = from x in e.Args
                                 where x.EndsWith(".hhpack") && File.Exists(x)
                                 select x;
                if (extensions.Count() > 0)
                {
                    var packageManager = new PackageManager();
                    foreach (var ext in extensions)
                    {
                        packageManager.BeginUnpack(ext, E.Root);
                    }
                }
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

                if (File.Exists(E.Root + "\\Clock.exe"))
                {
                    var weatherTask = new JumpTask();
                    weatherTask.Title = Weather.Properties.Resources.JumpListClockWidget;
                    weatherTask.WorkingDirectory = E.Root;
                    weatherTask.CustomCategory = Weather.Properties.Resources.JumpListWidgets;
                    weatherTask.ApplicationPath = E.Root + "\\Clock.exe";
                    weatherTask.IconResourcePath = E.Root + "\\Clock.exe";
                    weatherTask.IconResourceIndex = 0;

                    jumpList.JumpItems.Add(weatherTask);
                }

                if (File.Exists(E.Root + "\\Photos.exe"))
                {
                    var photosTask = new JumpTask();
                    photosTask.Title = Weather.Properties.Resources.JumpListPhotosWidget;
                    photosTask.WorkingDirectory = E.Root;
                    photosTask.CustomCategory = Weather.Properties.Resources.JumpListWidgets;
                    photosTask.ApplicationPath = E.Root + "\\Photos.exe";
                    photosTask.IconResourcePath = E.Root + "\\Photos.exe";
                    photosTask.IconResourceIndex = 0;

                    jumpList.JumpItems.Add(photosTask);
                }

                if (File.Exists(E.Root + "\\News.exe"))
                {
                    var newsTask = new JumpTask();
                    newsTask.Title = Weather.Properties.Resources.JumpListNewsWidget;
                    newsTask.WorkingDirectory = E.Root;
                    newsTask.CustomCategory = Weather.Properties.Resources.JumpListWidgets;
                    newsTask.ApplicationPath = E.Root + "\\News.exe";
                    newsTask.IconResourcePath = E.Root + "\\News.exe";
                    newsTask.IconResourceIndex = 0;

                    jumpList.JumpItems.Add(newsTask);
                }

                if (File.Exists(E.Root + "\\FriendStream.exe"))
                {
                    var friendsTask = new JumpTask();
                    friendsTask.Title = Weather.Properties.Resources.JumpListFriendStreamWidget;
                    friendsTask.WorkingDirectory = E.Root;
                    friendsTask.CustomCategory = Weather.Properties.Resources.JumpListWidgets;
                    friendsTask.ApplicationPath = E.Root + "\\FriendStream.exe";
                    friendsTask.IconResourcePath = E.Root + "\\FriendStream.exe";
                    friendsTask.IconResourceIndex = 0;

                    jumpList.JumpItems.Add(friendsTask);
                }

                if (File.Exists(E.Root + "\\Calendar.exe"))
                {
                    var calendarTask = new JumpTask();
                    calendarTask.Title = Weather.Properties.Resources.JumpListCalendarWidget;
                    calendarTask.WorkingDirectory = E.Root;
                    calendarTask.CustomCategory = Weather.Properties.Resources.JumpListWidgets;
                    calendarTask.ApplicationPath = E.Root + "\\Calendar.exe";
                    calendarTask.IconResourcePath = E.Root + "\\Calendar.exe";
                    calendarTask.IconResourceIndex = 0;

                    jumpList.JumpItems.Add(calendarTask);
                }

                jumpList.Apply();
            }

            foreach (Window w in Windows)
            {
                var u = new Unminimizer();
                var handle = new WindowInteropHelper(w).Handle;
                u.Initialize(handle);
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
                Text = "HTC Home"
            };
            trayIcon.MouseClick += TrayIconMouseClick;
            trayIcon.Visible = true;
            trayIcon.ContextMenu = trayMenu;

            var closeItem = new System.Windows.Forms.MenuItem();
            closeItem.Text = Weather.Properties.Resources.CloseItem;
            closeItem.Click += (s, e) =>
            {
                foreach (Window window in Windows)
                {
                    window.Close();
                }
            };
            trayMenu.MenuItems.Add(closeItem);

            var widgetsItem = new System.Windows.Forms.MenuItem();
            widgetsItem.Text = Weather.Properties.Resources.JumpListWidgets;

            trayMenu.MenuItems.Add(0, widgetsItem);
            widgetsItem.Visible = false;

            if (File.Exists(E.Root + "\\Clock.exe"))
            {
                widgetsItem.Visible = true;
                var clockItem = new System.Windows.Forms.MenuItem();
                clockItem.Text = Weather.Properties.Resources.JumpListClockWidget;
                clockItem.Click += (s, e) => Process.Start(E.Root + "\\Clock.exe");

                widgetsItem.MenuItems.Add(clockItem);
            }

            if (File.Exists(E.Root + "\\Photos.exe"))
            {
                widgetsItem.Visible = true;
                var photosItem = new System.Windows.Forms.MenuItem();
                photosItem.Text = Weather.Properties.Resources.JumpListPhotosWidget;
                photosItem.Click += (s, e) => Process.Start(E.Root + "\\Photos.exe");

                widgetsItem.MenuItems.Add(photosItem);
            }

            if (File.Exists(E.Root + "\\News.exe"))
            {
                widgetsItem.Visible = true;
                var newsItem = new System.Windows.Forms.MenuItem();
                newsItem.Text = Weather.Properties.Resources.JumpListNewsWidget;
                newsItem.Click += (s, e) => Process.Start(E.Root + "\\News.exe");

                widgetsItem.MenuItems.Add(newsItem);
            }

            if (File.Exists(E.Root + "\\FriendStream.exe"))
            {
                widgetsItem.Visible = true;
                var friendsItem = new System.Windows.Forms.MenuItem();
                friendsItem.Text = Weather.Properties.Resources.JumpListFriendStreamWidget;
                friendsItem.Click += (s, e) => Process.Start(E.Root + "\\FriendStream.exe");

                widgetsItem.MenuItems.Add(friendsItem);
            }

            if (File.Exists(E.Root + "\\Calendar.exe"))
            {
                widgetsItem.Visible = true;
                var calendarItem = new System.Windows.Forms.MenuItem();
                calendarItem.Text = Weather.Properties.Resources.JumpListCalendarWidget;
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

        private void ApplicationDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            logger.FatalException("An unhandled exception occured", e.Exception);
        }

        public static void UpdateOverlayIcon(int count)
        {
            if (!Settings.EnableOverlayIcon)
                return;
            if (count <= 0)
            {
                Current.MainWindow.TaskbarItemInfo.Overlay = null;
                return;
            }
            const int iconWidth = 20;
            const int iconHeight = 20;

            var bmp = new RenderTargetBitmap(iconWidth, iconHeight, 96, 96, PixelFormats.Default);

            var root = new ContentControl();

            root.ContentTemplate = ((DataTemplate)App.Current.Resources["OverlayIcon"]);
            root.Content = count;

            root.Arrange(new Rect(0, 0, iconWidth, iconHeight));

            bmp.Render(root);

            Current.MainWindow.TaskbarItemInfo.Overlay = bmp;
        }
    }
}
