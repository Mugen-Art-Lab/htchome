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
using System.Windows.Shapes;
using System.Xml.Linq;
using System.Reflection;
using System.IO;
using System.Net;
using System.Globalization;
using System.Diagnostics;

namespace Updater
{
    /// <summary>
    /// Interaction logic for UpdateWindow.xaml
    /// </summary>
    public partial class UpdateWindow : Window
    {
        public string UpdateData;
        private WebClient downloader;
        private Dictionary<string, string> filesList;
        private int index;

        public UpdateWindow()
        {
            InitializeComponent();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            if (!File.Exists(App.Path + "\\Localization\\" + CultureInfo.CurrentUICulture.Name + ".xaml"))
                Locale.Source = new Uri(App.Path + "\\Localization\\en-US.xaml");
            else
                Locale.Source = new Uri(App.Path + "\\Localization\\" + CultureInfo.CurrentUICulture.Name + ".xaml");

            XDocument doc = XDocument.Parse(UpdateData);
            MessageText.Text = string.Format((string)Resources["UpdateAvailable"], doc.Root.Element("Title").Value);
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            if (YesButton.Visibility == System.Windows.Visibility.Hidden) //if Yes button is hidden, thats mean that download was finished, so we should start HTC Home
            {
                string exename = "HTCHome.exe";
                if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE").Contains("64")) //determine architecture to start right exe
                    exename = "HTCHome (x64).exe";
                Process.Start(App.Path + "\\" + exename);
            }
            this.Close();
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            CloseHTCHome();
            XDocument doc = XDocument.Parse(UpdateData);
            var dirs = from x in doc.Root.Descendants("Directories").First().Elements("Add") select x.Value;
            if (dirs.Count() > 0)
            {
                foreach (var d in dirs)
                {
                    if (!Directory.Exists(App.Path + "\\" + d))
                        Directory.CreateDirectory(App.Path + "\\" + d);
                }
            }

            var files = from x in doc.Root.Descendants("Files").First().Elements("Add")
                        select new
                        {
                            name = x.Value,
                            url = x.Attribute("Link").Value
                        };

            if (!Directory.Exists(App.Path + "\\old"))
                Directory.CreateDirectory(App.Path + "\\old");
            filesList = new Dictionary<string, string>();
            if (files.Count() > 0)
            {
                foreach (var file in files)
                {
                    if (File.Exists(App.Path + "\\" + file.name)) //if file with such name already exists, we just rename it to <filename>.old
                    {
                        if (File.Exists(App.Path + "\\old\\" + file.name + ".old")) //if <filename>.old already exists, we just delete it
                            File.Delete(App.Path + "\\old\\" + file.name + ".old");

                        File.Move(App.Path + "\\" + file.name, App.Path + "\\old\\" + file.name + ".old");
                    }

                    filesList.Add(App.Path + "\\" + file.name, file.url); //add each file to list, so downloader could download them by one-by-one
                }
            }

            if (filesList.Count > 0)
            {
                downloader = new WebClient();

                if (WebRequest.GetSystemWebProxy().Credentials != null)
                {
                    downloader.Proxy = WebRequest.GetSystemWebProxy();
                }
                
                MessageText.Text = Resources["Downloading"] + " " + filesList.ElementAt(index).Key + "...";
                DownloadProgress.Visibility = System.Windows.Visibility.Visible;
                YesButton.Visibility = System.Windows.Visibility.Hidden;
                NoButton.Content = (string)Resources["Close"];
                NoButton.IsEnabled = false;
                downloader.DownloadProgressChanged += new DownloadProgressChangedEventHandler(downloader_DownloadProgressChanged);
                downloader.DownloadFileCompleted += new System.ComponentModel.AsyncCompletedEventHandler(downloader_DownloadFileCompleted);
                downloader.DownloadFileAsync(new Uri(filesList.ElementAt(index).Value), filesList.ElementAt(index).Key);
            }

        }

        void downloader_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            DownloadProgress.Value = e.ProgressPercentage;
        }

        void downloader_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            index++;
            if (index < filesList.Count)
            {
                DownloadProgress.Value = 0;
                downloader.DownloadFileAsync(new Uri(filesList.ElementAt(index).Value), filesList.ElementAt(index).Key);
                MessageText.Text = Resources["Downloading"] + " " + filesList.ElementAt(index).Key + "...";
            }
            else
            {
                MessageText.Text = (string)Resources["DownloadFinished"];
                NoButton.IsEnabled = true;
            }
        }

        public void CloseHTCHome()
        {
            foreach (Process clsProcess in Process.GetProcesses())
            {

                if (clsProcess.ProcessName == "HTCHome" || clsProcess.ProcessName == "HTCHome (x64)")
                {
                    //clsProcess.Close(); //doesn't work :(
                    clsProcess.Kill();
                    //no return here to close all htc home processes
                }
            }
        }
    }
}
