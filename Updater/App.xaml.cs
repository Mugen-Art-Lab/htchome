using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Net;
using System.Threading;
using System.Xml.Linq;
using System.IO;
using System.Reflection;
using System.Diagnostics;

namespace Updater
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static readonly string Path = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private WebClient downloader;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (!File.Exists(Path + "\\HTCHome.exe"))
            {
                App.Current.Shutdown();
                return;
            }

            int build = FileVersionInfo.GetVersionInfo(Path + "\\HTCHome.exe").FileBuildPart;
            string link = "http://store.htchome.org/dl/stable/updates/" + build + ".xml";
            downloader = new WebClient();
            downloader.DownloadStringCompleted += new DownloadStringCompletedEventHandler(downloader_DownloadStringCompleted);

            if (WebRequest.GetSystemWebProxy().Credentials != null)
            {
                downloader.Proxy = WebRequest.GetSystemWebProxy();
            }

            if (!IsRemoteFileExists(link))
                App.Current.Shutdown();

            ThreadStart threadStarter = delegate
            {
                try
                {
                    downloader.DownloadStringAsync(new Uri(link));
                }
                catch (WebException)
                {
                    App.Current.Shutdown();
                }
            };
            Thread thread = new Thread(threadStarter);
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
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

        UpdateWindow w = new UpdateWindow();
        void downloader_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            // XDocument doc = XDocument.Parse(e.Result);
            w.Dispatcher.Invoke((Action)delegate
            {
                //w.MessageText.Text = string.Format("There is \"{0}\" available. Would you like to update?", doc.Root.Element("Title").Value);
                w.UpdateData = e.Result;
                w.Show();
            }, null);

        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log("Unhandled exception!");
            Log(e.Exception.ToString());
            MessageBox.Show(e.Exception.Message);
            e.Handled = true;
            App.Current.Shutdown();
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
    }
}
