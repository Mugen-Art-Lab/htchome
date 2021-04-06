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
using ICSharpCode.SharpZipLib.Zip;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Globalization;

namespace WebInstaller
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private WebClient downloader;
        private string dev = "http://store.htchome.org/dl/dev.xml";
        private string stable = "http://store.htchome.org/dl/stable.xml";
        private string beta = "http://store.htchome.org/dl/beta.xml";
        public const string TYPE = "stable";

        private string Root;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            /*if (!File.Exists(App.Path + "\\Localization\\" + CultureInfo.CurrentUICulture.Name + ".xaml"))
                Locale.Source = new Uri(App.Path + "\\Localization\\en-US.xaml");
            else
                Locale.Source = new Uri(App.Path + "\\Localization\\" + CultureInfo.CurrentUICulture.Name + ".xaml");*/
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {           
            string dir = "HTC Home";
            if (TYPE == "dev")
                dir += " Dev";

            InstallPath.Text = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Stealth Software\\" + dir;
        }

        void downloader_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            XDocument doc = XDocument.Parse(e.Result);

            StatusTextBlock.Dispatcher.Invoke((Action)delegate
            {
                StatusTextBlock.Text = Resources["Downloading"] + " " + doc.Descendants("Title").First().Value + "...";
            }, null);
            string dir = "HTC Home";
            if (TYPE == "dev")
                dir += " Dev";
            if (!Directory.Exists(Root))
                Directory.CreateDirectory(Root);
            downloader.DownloadProgressChanged += new DownloadProgressChangedEventHandler(downloader_DownloadProgressChanged);
            downloader.DownloadFileAsync(new Uri(doc.Descendants("Link").First().Value), System.IO.Path.GetTempPath() + "\\htchome.zip");

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

        void downloader_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            string dir = "HTC Home";
            if (TYPE == "dev")
                dir += " Dev";

            if (!e.Cancelled)
            {
                MainGrid.Dispatcher.Invoke((Action)delegate
                {
                    StatusTextBlock.Text = (string)Resources["DownloadCompleted"];
                    DownloadProgress.Visibility = System.Windows.Visibility.Hidden;
                    LaunchCheckBox.Visibility = System.Windows.Visibility.Visible;
                    CloseButton.IsEnabled = true;
                }, null);

                if (!IsProcessOpen("HTCHome"))
                    Unpack(Root, System.IO.Path.GetTempPath() + "\\htchome.zip");
                else
                    MessageBox.Show((string)Resources["IsRunning"]);
                //Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName
                if (!Directory.Exists(Root + "\\Uninstall"))
                    Directory.CreateDirectory(Root + "\\Uninstall");

                if (!Directory.Exists(Root + "\\Uninstall\\Localization"))
                    Directory.CreateDirectory(Root + "\\Uninstall\\Localization");

                File.Copy(Assembly.GetExecutingAssembly().Location, Root + "\\Uninstall\\Uninstall.exe", true);
                //File.Copy(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName + "\\ICSharpCode.SharpZipLib.dll", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Stealth Software\\" + dir + "\\Uninstall\\ICSharpCode.SharpZipLib.dll");
                foreach (string file in Directory.GetFiles(Directory.GetParent(Assembly.GetExecutingAssembly().Location) + "\\Localization"))
                {
                    FileInfo f = new FileInfo(file);
                    File.Copy(file, Root + "\\Uninstall\\Localization\\" + f.Name, true);
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (downloader != null && downloader.IsBusy)
                downloader.CancelAsync();
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
                                if (File.Exists(path + "\\" + entry.Name + ".old"))
                                    File.Delete(path + "\\" + entry.Name + ".old");
                                File.Move(path + "\\" + entry.Name, path + "\\" + entry.Name + ".old");
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            string exename = "HTCHome.exe";
            if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE").Contains("64"))
                exename = "HTCHome (x64).exe";
            string dir = "HTC Home";
            if (TYPE == "dev")
                dir += " Dev";

            if (!IsProcessOpen("HTCHome"))
            {
                AddShortcutToDesktop("HTC Home", Root + "\\" + exename);
            }
            if (LaunchCheckBox.IsChecked == true)
            {
                Process.Start(Root + "\\" + exename);
            }
            this.Close();
        }

        private void ChangeDirButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                InstallPath.Text = dialog.SelectedPath;
            }
        }

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            Root = InstallPath.Text;

            InstallButton.Visibility = System.Windows.Visibility.Collapsed;
            CloseButton.Visibility = System.Windows.Visibility.Visible;

            InstallGrid.Visibility = System.Windows.Visibility.Collapsed;
            ProgressGrid.Visibility = System.Windows.Visibility.Visible;

            downloader = new WebClient();
            StatusTextBlock.Text = (string)Resources["Connecting"];

            if (WebRequest.GetSystemWebProxy().Credentials != null)
            {
                downloader.Proxy = WebRequest.GetSystemWebProxy();
            }
            
            downloader.DownloadStringCompleted += new DownloadStringCompletedEventHandler(downloader_DownloadStringCompleted);
            downloader.DownloadFileCompleted += new System.ComponentModel.AsyncCompletedEventHandler(downloader_DownloadFileCompleted);
            ThreadStart threadStarter = delegate
            {
                switch (TYPE)
                {
                    case "stable":
                        downloader.DownloadStringAsync(new Uri(stable));
                        break;
                    case "beta":
                        downloader.DownloadStringAsync(new Uri(beta));
                        break;
                    case "dev":
                        downloader.DownloadStringAsync(new Uri(dev));
                        break;
                }
            };
            Thread thread = new Thread(threadStarter);
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
        }
    }
}
