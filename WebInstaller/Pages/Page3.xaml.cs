using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.Xml.Linq;
using System.IO;
using System.Threading;
using System.Diagnostics;
using ICSharpCode.SharpZipLib.Zip;
using System.Reflection;
using System.Globalization;

namespace WebInstaller.Pages
{
    /// <summary>
    /// Interaction logic for Page3.xaml
    /// </summary>
    public partial class Page3 : UserControl
    {
        private WebClient downloader;
        private const string dev = "http://store.htchome.org/dl/dev.xml";
        private const string stable = "http://store.htchome.org/dl/stable.xml";
        private const string beta = "http://store.htchome.org/dl/beta.xml";

        private ResourceDictionary locale = new ResourceDictionary();

        public Page3()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(App.Path + "\\Localization\\" + CultureInfo.CurrentUICulture.Name + ".xaml"))
                locale.Source = new Uri(App.Path + "\\Localization\\en-US.xaml");
            else
                locale.Source = new Uri(App.Path + "\\Localization\\" + CultureInfo.CurrentUICulture.Name + ".xaml");

            downloader = new WebClient();

            if (WebRequest.GetSystemWebProxy().Credentials != null)
            {
                downloader.Proxy = WebRequest.GetSystemWebProxy();
            }

            downloader.DownloadStringCompleted += new DownloadStringCompletedEventHandler(downloader_DownloadStringCompleted);
            downloader.DownloadFileCompleted += new System.ComponentModel.AsyncCompletedEventHandler(downloader_DownloadFileCompleted);

            ThreadStart threadStarter = delegate
            {
                downloader.DownloadStringAsync(new Uri(stable));
            };
            Thread thread = new Thread(threadStarter);
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
        }

        void downloader_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if (!e.Cancelled)
            {

                if (!IsProcessOpen("HTCHome"))
                    Unpack(E.InstallPath, System.IO.Path.GetTempPath() + "\\htchome2.zip");
                else
                    MessageBox.Show((string)Resources["IsRunning"]);
                if (!Directory.Exists(E.InstallPath + "\\Uninstall"))
                    Directory.CreateDirectory(E.InstallPath + "\\Uninstall");

                if (!Directory.Exists(E.InstallPath + "\\Uninstall\\Localization"))
                    Directory.CreateDirectory(E.InstallPath + "\\Uninstall\\Localization");

                File.Copy(Assembly.GetExecutingAssembly().Location, E.InstallPath + "\\Uninstall\\Uninstall.exe", true);
                foreach (string file in Directory.GetFiles(Directory.GetParent(Assembly.GetExecutingAssembly().Location) + "\\Localization"))
                {
                    FileInfo f = new FileInfo(file);
                    File.Copy(file, E.InstallPath + "\\Uninstall\\Localization\\" + f.Name, true);
                }

                string exename = "HTCHome.exe";
                if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE").Contains("64"))
                    exename = "HTCHome (x64).exe";

                if (E.DesktopShortcut)
                {
                    AddShortcutToDesktop("HTC Home", E.InstallPath + "//" + exename);
                }

                if (E.StartMenuShortcut)
                {
                    AddShortcutToStartmenu("HTC Home", E.InstallPath + "//" + exename);
                }

                ((Grid)Parent).Dispatcher.Invoke((Action)delegate
                {
                    ((Grid)Parent).Children.Add(new InstallFinishPage());
                    ((Grid)Parent).Children.Remove(this);
                }, null);
            }
        }

        void downloader_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            XDocument doc = XDocument.Parse(e.Result);

            StatusTextBlock.Dispatcher.Invoke((Action)delegate
            {
                StatusTextBlock.Text = (string)locale["Downloading"] + " " + doc.Descendants("Title").First().Value + "...";
            }, null);

            if (!Directory.Exists(E.InstallPath))
                Directory.CreateDirectory(E.InstallPath);
            downloader.DownloadProgressChanged += new DownloadProgressChangedEventHandler(downloader_DownloadProgressChanged);
            downloader.DownloadFileAsync(new Uri(doc.Descendants("Link").First().Value), System.IO.Path.GetTempPath() + "\\htchome2.zip");

            DownloadProgress.Dispatcher.Invoke((Action)delegate
            {
                DownloadProgress.IsIndeterminate = false;
            }, null);
        }

        void downloader_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            DownloadProgress.Dispatcher.Invoke((Action)delegate
            {
                DownloadProgress.Value = e.ProgressPercentage;
            }, null);
        }

        public bool IsProcessOpen(string name)
        {
            foreach (Process clsProcess in Process.GetProcesses())
            {

                if (clsProcess.ProcessName == name)
                {
                    return true;
                }
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

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show((string)locale["ConfirmCancel"], (string)locale["Title"], MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                if (downloader != null && downloader.IsBusy)
                {
                    downloader.CancelAsync();
                    downloader.Dispose();
                }

                ((Grid)Parent).Dispatcher.Invoke((Action)delegate
                {
                    ((Grid)Parent).Children.Add(new InstallCancelPage());
                    ((Grid)Parent).Children.Remove(this);
                }, null);
            }
        }

        private void AddShortcutToDesktop(string linkName, string file)
        {
            string deskDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            using (StreamWriter writer = new StreamWriter(deskDir + "\\" + linkName + ".url", false, Encoding.Unicode))
            {
                string app = System.Reflection.Assembly.GetExecutingAssembly().Location;
                writer.WriteLine("[InternetShortcut]");
                writer.WriteLine("URL=file:///" + file);
                writer.WriteLine("IconIndex=0");
                string icon = app.Replace('\\', '/');
                writer.WriteLine("IconFile=" + icon);
                writer.Flush();
            }
        }

        private void AddShortcutToStartmenu(string linkName, string file)
        {
            string startmenuDir = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            if (!Directory.Exists(startmenuDir + "\\HTC Home"))
                Directory.CreateDirectory(startmenuDir + "\\HTC Home");

            using (StreamWriter writer = new StreamWriter(startmenuDir + "\\HTC Home\\" + linkName + ".url", false, Encoding.Unicode))
            {
                string app = System.Reflection.Assembly.GetExecutingAssembly().Location;
                writer.WriteLine("[InternetShortcut]");
                writer.WriteLine("URL=file:///" + file);
                writer.WriteLine("IconIndex=0");
                string icon = app.Replace('\\', '/');
                writer.WriteLine("IconFile=" + icon);
                writer.Flush();
            }
        }
    }
}
