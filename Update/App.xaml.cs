using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Xml.Linq;
using Home.Base;
using Home.Updates;
using Microsoft.Shell;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Update
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, ISingleInstanceApp
    {
        public static Updater Updater;
        public static List<UpdateInfo> AvailableUpdates;
        private NotifyIcon trayIcon;
        private const string Unique = "HTC Home Update";
        public static Settings Settings;

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        [STAThread]
        public static void Main()
        {
            if (SingleInstance<App>.InitializeAsFirstInstance(Unique))
            {
                var application = new App();

                application.InitializeComponent();
                application.Run();

                // Allow single instance code to perform cleanup operations
                SingleInstance<App>.Cleanup();
            }
        }

        private void ApplicationStartup(object sender, StartupEventArgs e)
        {
            Settings = (Settings)XmlSerializable.Load(typeof(Settings), E.Root + "\\Update.config") ?? new Settings();

            Updater = new Updater();
            if (e.Args.Length == 0)
                AvailableUpdates = Updater.GetAvailableUpdatesList("en-US");
            else
            {
                if (!e.Args[0].StartsWith("/"))
                {
                    System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(e.Args[0]);
                    System.Threading.Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(e.Args[0]);
                }


                AvailableUpdates = Updater.GetAvailableUpdatesList(e.Args[0], e.Args.Contains("/force"), e.Args.Contains("/dev"));
            }

            if (AvailableUpdates == null || AvailableUpdates.Count == 0)
            {
                if (e.Args.Contains("/wcheck")) //indicates that check was launched by user from widget options
                {
                    AddTrayIcon(Update.Properties.Resources.NoUpdatesHeader, Update.Properties.Resources.NoUpdates, 10, ToolTipIcon.Info);
                }
                else
                {
                    Shutdown();
                    return;
                }
            }

            if (e.Args.Contains("/silent"))
            {
                Updater.InstallUpdates(App.AvailableUpdates);
                AddTrayIcon(Update.Properties.Resources.NotifyHeader, Update.Properties.Resources.NotifyDescription);
            }
            else
            {
                AddTrayIcon(Update.Properties.Resources.UpdatesAvailable, Update.Properties.Resources.NotifyClickHere, 45);
                trayIcon.BalloonTipClicked += TrayIconBalloonTipClicked;
                //this.StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
            }

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
        }

        void TrayIconBalloonTipClicked(object sender, EventArgs e)
        {
            if (AvailableUpdates != null && AvailableUpdates.Count > 0)
            {
                trayIcon.BalloonTipClicked -= TrayIconBalloonTipClicked;
                var w = new MainWindow();
                w.Show();
            }
            else
            {
                RemoveTrayIcon();
                Shutdown();
            }
        }

        public void AddTrayIcon(string title, string text, int timeOut = 10, ToolTipIcon icon = ToolTipIcon.Warning)
        {
            if (trayIcon != null)
            {
                return;
            }

            trayIcon = new NotifyIcon
            {
                Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location),
                Text = "HTC Home Update"
            };
            trayIcon.Visible = true;
            trayIcon.BalloonTipIcon = icon;
            trayIcon.BalloonTipTitle = title;//Update.Properties.Resources.NotifyHeader;
            trayIcon.BalloonTipText = text;// Update.Properties.Resources.NotifyDescription;
            trayIcon.ShowBalloonTip(timeOut);
            trayIcon.BalloonTipClosed += TrayIconBalloonTipClosed;

            trayIcon.ContextMenu = new ContextMenu();
            trayIcon.ContextMenu.MenuItems.Add(new MenuItem("Open", delegate
            {
                TrayIconBalloonTipClicked(null, EventArgs.Empty);
            }));

            trayIcon.ContextMenu.MenuItems.Add(new MenuItem("Close", delegate
            {
                ApplicationExit(null, null);
            }));
        }

        public void RemoveTrayIcon()
        {
            if (trayIcon != null)
            {
                trayIcon.BalloonTipClosed -= TrayIconBalloonTipClosed;
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
        }

        void TrayIconBalloonTipClosed(object sender, EventArgs e)
        {
            RemoveTrayIcon();
            Shutdown();
        }

        public bool SignalExternalCommandLineArgs(IList<string> args)
        {
            return true;
        }

        private void ApplicationExit(object sender, ExitEventArgs e)
        {
            RemoveTrayIcon();
        }

        private void ApplicationDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            logger.Fatal("Updater error. " + e.Exception);
            MessageBox.Show("An error occured in Updater.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            this.Shutdown();
        }
    }
}
