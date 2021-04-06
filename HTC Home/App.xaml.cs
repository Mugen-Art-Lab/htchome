using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Xml.Serialization;
using System.Linq;

using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using HTCHome.Core;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Drawing;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Net;
using ICSharpCode.SharpZipLib.Zip;

namespace HTCHome
{
    /// <summary>6
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static NotifyIcon trayIcon;

        public static readonly string Path = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public static Settings sett;

        public static List<Widget> widgets;

        private static System.Windows.Controls.ContextMenu trayMenu = new System.Windows.Controls.ContextMenu();

        private Options options;

        public static LocaleManager LocaleManager;

        private HotKey moveForegroundHotkey;
        private HotKey galleryHotkey;

        public static Gallery.Gallery Gallery;

        private void ApplicationDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log("Unhandled exception!");
            Log(e.Exception.ToString());
            MessageBox.Show(e.Exception.Message, "HTC Home error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        public static void Log(string message)
        {
            if (!File.Exists(App.Path + "\\log.txt"))
            {
                File.WriteAllText(App.Path + "\\log.txt", string.Empty);
            }

            try
            {
                File.AppendAllText(App.Path + "\\log.txt",
                   DateTime.Now + " -------------- " + message + (char)(13) + (char)(10));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Can't write log. " + ex.Message);
            }
        }

        private void ApplicationStartup(object sender, StartupEventArgs e)
        {
            try
            {
                //check if we must run as administrator
                if (!Directory.Exists(Path + "\\Temp"))
                    Directory.CreateDirectory(Path + "\\Temp");
                Directory.Delete(Path + "\\Temp");
            }
            catch (UnauthorizedAccessException ex)
            {
                var p = new ProcessStartInfo { Verb = "runas", FileName = Assembly.GetExecutingAssembly().Location };
                Process.Start(p);
                Shutdown();
            }

            if (!Directory.Exists(Path + "\\Config"))
            {
                Directory.CreateDirectory(Path + "\\Config");
            }

            sett = Settings.Read(Path + "\\Config\\Home.conf");

            if (sett.useProxy)
            {
                var proxy = new WebProxy();
                proxy.Address = new Uri(sett.proxyAddress + ":" + sett.proxyPort.ToString());
                proxy.Credentials = new NetworkCredential(sett.proxyUsername, sett.proxyPassword);
                HTCHome.Core.GeneralHelper.Proxy = proxy;
            }

            if (sett.EnableUpdates && File.Exists(Path + "\\Updater.exe"))
                Process.Start(Path + "\\Updater.exe");

            LocaleManager = new LocaleManager(Path + "\\Localization");

            if (string.IsNullOrEmpty(sett.Locale))
                sett.Locale = CultureInfo.CurrentUICulture.Name;
            if (!File.Exists(Path + "\\Localization\\" + sett.Locale + ".xaml"))
            {
                if (IsRemoteFileExists("http://store.htchome.org/localization/home2/" + sett.Locale + ".zip"))
                {
                    var w = new LocaleDownloadWindow(sett.Locale);
                    w.ShowDialog();
                }
                else
                    sett.Locale = "en-US";
            }

            LocaleManager.LoadLocale(sett.Locale);

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(sett.Locale);
            }
            catch { }

            HTCHome.Core.Environment.Path = Path + "\\Widgets";
            HTCHome.Core.Environment.ConfigDirectory = Path + "\\Config";
            HTCHome.Core.Environment.Locale = sett.Locale;

            if (e.Args.Length != 0)
            {
                var skins = from x in e.Args
                            where x.EndsWith(".hhskin") && File.Exists(x)
                            select x;
                if (skins.Count() > 0)
                {
                    foreach (var s in skins)
                    {
                        try
                        {
                            Unpack(Path, s);
                            MessageBox.Show(App.LocaleManager.GetString("SkinInstalled"), "", MessageBoxButton.OK,
                                            MessageBoxImage.Information);
                            Shutdown();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(App.LocaleManager.GetString("SkinNotInstalled"), "", MessageBoxButton.OK, MessageBoxImage.Error);
                            Log("Can't install skin " + s + "\n" + ex.ToString());
                        }
                    }
                }

                var extensions = from x in e.Args
                                 where x.EndsWith(".hhext") && File.Exists(x)
                                 select x;
                if (extensions.Count() > 0)
                {
                    foreach (var ext in extensions)
                    {
                        try
                        {
                            Unpack(Path, ext);
                            MessageBox.Show(App.LocaleManager.GetString("ExtensionInstalled"), "", MessageBoxButton.OK,
                                            MessageBoxImage.Information);
                            Shutdown();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(App.LocaleManager.GetString("ExtensionNotInstalled"), "", MessageBoxButton.OK, MessageBoxImage.Error);
                            Log("Can't install exntension " + ext + "\n" + ex.ToString());
                        }
                    }
                }
            }

            if (e.Args.Contains("-addicon"))
                AddTrayIcon();

            if (Directory.Exists(HTCHome.Core.Environment.Path) && Directory.GetDirectories(HTCHome.Core.Environment.Path).Length > 0)
            {
                widgets = new List<Widget>();
                foreach (string dir in Directory.GetDirectories(HTCHome.Core.Environment.Path))
                {
                    var d = new DirectoryInfo(dir);
                    var file = string.Format("{0}\\{1}\\{1}.dll", HTCHome.Core.Environment.Path, d.Name);
                    if (File.Exists(file))
                    {
                        var w = new Widget();
                        w.Initalize(file);
                        //w.Closing += new System.ComponentModel.CancelEventHandler(w_Closing);
                        if (!w.HasErrors)
                        {
                            widgets.Add(w);

                            if (e.Args.Contains("-addicon"))
                            {
                                var item = new System.Windows.Controls.MenuItem { Header = w.WidgetName };
                                var icon = new System.Windows.Controls.Image
                                               {
                                                   Source = new BitmapImage(new Uri(w.WidgetIcon)),
                                                   Width = 25,
                                                   Height = 25
                                               };
                                item.Icon = icon;
                                item.Click += WidgetItem_Click;
                                ((System.Windows.Controls.MenuItem)trayMenu.Items[0]).Items.Add(item);
                            }
                            if (sett.LoadedWidgets != null && sett.LoadedWidgets.Contains(w.WidgetName))
                            {
                                w.Load();
                            }
                        }
                    }
                }
            }

            options = new Options();

            if (widgets != null && widgets.Count > 0)
            {
                moveForegroundHotkey = new HotKey(System.Windows.Input.ModifierKeys.Windows, Keys.H, IntPtr.Zero);
                moveForegroundHotkey.HotKeyPressed += (k) =>
                {
                    foreach (Widget w in widgets)
                        w.Activate();
                };

                galleryHotkey = new HotKey(System.Windows.Input.ModifierKeys.Windows, Keys.J, IntPtr.Zero);
                galleryHotkey.HotKeyPressed += (k) =>
                {
                    var gallery = new Gallery.Gallery()
                    {
                        Left = 0,
                        Top = 0,
                        Width = SystemParameters.PrimaryScreenWidth,
                        Height = SystemParameters.PrimaryScreenHeight
                    };
                    gallery.ShowDialog();
                };
            }
        }

        private void ApplicationExit(object sender, ExitEventArgs e)
        {
            //RemoveTrayIcon();

            if (moveForegroundHotkey != null)
                moveForegroundHotkey.Dispose();

            if (galleryHotkey != null)
                galleryHotkey.Dispose();

            sett.LoadedWidgets = new List<string>();

            foreach (Widget w in widgets)
            {
                if (w.IsWidgetLoaded)
                    sett.LoadedWidgets.Add(w.WidgetName);
            }

            try
            {
                sett.Write(Path + "\\Config\\Home.conf");
            }
            catch (Exception)
            {

            }
        }


        private void AddTrayIcon()
        {
            if (trayIcon != null)
            {
                return;
            }
            trayIcon = new NotifyIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location),
                //System.Drawing.Icon.FromHandle(WinAPI.ExtractIcon(IntPtr.Zero, Assembly.GetExecutingAssembly().Location, 0)),
                Text = "HTC Home 2"
            };
            trayIcon.MouseClick += TrayIconMouseClick;
            trayIcon.Visible = true;

            System.Windows.Controls.MenuItem closeItem = new System.Windows.Controls.MenuItem();
            closeItem.Header = LocaleManager.GetString("CloseHome");
            closeItem.Click += new RoutedEventHandler(closeItem_Click);


            System.Windows.Controls.MenuItem optionsItem = new System.Windows.Controls.MenuItem();
            optionsItem.Header = LocaleManager.GetString("Options");
            optionsItem.Click += new RoutedEventHandler(optionsItem_Click);

            System.Windows.Controls.MenuItem addItem = new System.Windows.Controls.MenuItem();
            addItem.Header = LocaleManager.GetString("Add");

            trayMenu.Items.Add(addItem);
            trayMenu.Items.Add(optionsItem);
            trayMenu.Items.Add(closeItem);
        }

        void optionsItem_Click(object sender, RoutedEventArgs e)
        {
            ShowOptions();
        }

        public void ShowOptions()
        {
            if (options.IsVisible)
            {
                options.Activate();
                return;
            }
            options = new Options();

            if (Core.Environment.Locale == "he-IL" || Core.Environment.Locale == "ar-SA")
            {
                options.FlowDirection = System.Windows.FlowDirection.RightToLeft;
            }
            else
            {
                options.FlowDirection = System.Windows.FlowDirection.LeftToRight;
            }

            options.ShowDialog();
        }

        void closeItem_Click(object sender, RoutedEventArgs e)
        {
            this.Shutdown();
        }

        private void RemoveTrayIcon()
        {
            if (trayIcon != null)
            {
                trayIcon.MouseClick -= TrayIconMouseClick;
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
        }

        private static void TrayIconMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                foreach (Window w in Application.Current.Windows)
                {
                    w.Activate();
                }
            }
            else
            {
                trayMenu.StaysOpen = false;
                trayMenu.IsOpen = true;
            }
        }

        public void WidgetItem_Click(object sender, EventArgs e)
        {
            int index = ((System.Windows.Controls.MenuItem)trayMenu.Items[0]).Items.IndexOf(sender); /*trayIcon.ContextMenu.MenuItems[0].MenuItems.IndexOf((MenuItem)sender)*/
            if (!widgets[index].IsWidgetLoaded || !widgets[index].IsVisible)
            {
                Widget w = new Widget();
                w.Initalize(widgets[index].path);
                widgets[index] = w;
                widgets[index].Load();
            }
        }

        private bool IsRemoteFileExists(string url)
        {
            try
            {
                WebRequest request = HttpWebRequest.Create(url);
                request.Method = "HEAD"; // Just get the document headers, not the data.
                request.Credentials = System.Net.CredentialCache.DefaultCredentials;
                // This may throw a WebException:
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        // If no exception was thrown until now, the file exists and we 
                        // are allowed to read it. 
                        return true;
                    }
                    else
                    {
                        // Some other HTTP response - probably not good.
                        // Check its StatusCode and handle it.
                    }
                }
            }
            catch (Exception)
            {

            }
            return false;
        }

        public static void Unpack(string path, string file)
        {
            using (FileStream fileStreamIn = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                using (ZipInputStream zipInStream = new ZipInputStream(fileStreamIn))
                {
                    ZipEntry entry;
                    FileInfo info = new FileInfo(file);
                    while (true)
                    {
                        entry = zipInStream.GetNextEntry();
                        if (entry == null)
                            break;
                        if (!entry.IsDirectory)
                        {
                            if (File.Exists(path + "\\" + entry.Name))
                            {

                                File.Delete(path + "\\" + entry.Name);
                            }

                            using (FileStream fileStreamOut = new FileStream(string.Format(@"{0}\{1}", path, entry.Name), FileMode.Create, FileAccess.Write))
                            {
                                int size;
                                byte[] buffer = new byte[1024];
                                do
                                {
                                    size = zipInStream.Read(buffer, 0, buffer.Length);
                                    fileStreamOut.Write(buffer, 0, size);
                                } while (size > 0);
                                fileStreamOut.Close();
                            }
                        }
                        else
                            if (!Directory.Exists(string.Format(@"{0}\{1}", path, entry.Name)))
                                Directory.CreateDirectory(string.Format(@"{0}\{1}", path, entry.Name));
                    }
                    zipInStream.Close();
                }
                fileStreamIn.Close();
            }
        }

        private void Application_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            sett.LoadedWidgets = new List<string>();

            foreach (Widget w in widgets)
            {
                if (w.IsWidgetLoaded)
                    sett.LoadedWidgets.Add(w.WidgetName);
            }

            sett.Write(Path + "\\Config\\Home.conf");
        }
    }
}