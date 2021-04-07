using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Home.Base;
using Home.Updates;

namespace Update
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void WindowSourceInitialized(object sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;

            //WinAPI.RemoveWindowIcon(handle);

            if (App.AvailableUpdates != null)
            {
                foreach (var u in App.AvailableUpdates)
                {
                    UpdatesList.Items.Add(u);
                }
            }

            Activate();
        }

        private void InstallButtonClick(object sender, RoutedEventArgs e)
        {
            InstallButton.IsEnabled = false;
            CloseButton.IsEnabled = false;
            HeaderTextBlock.Text = Properties.Resources.Installing;
            ProgressBar.Visibility = System.Windows.Visibility.Visible;
            UpdatesList.Visibility = System.Windows.Visibility.Collapsed;
            RestartText.Visibility = System.Windows.Visibility.Visible;
            DetailsGrid.Visibility = System.Windows.Visibility.Collapsed;
            ThreadStart threadStarter = () =>
                                            {
                                                Updater.InstallUpdates(App.AvailableUpdates);
                                                HeaderPanel.Dispatcher.Invoke((Action)delegate
                                                                                           {
                                                                                               ProgressBar.Visibility = System.Windows.Visibility.Collapsed;
                                                                                               HeaderTextBlock.Text = Properties.Resources.InstallFinished;
                                                                                               RestartButton.Visibility = System.Windows.Visibility.Visible;
                                                                                           });
                                                CloseButton.Dispatcher.Invoke((Action)delegate { CloseButton.IsEnabled = true; });
                                            };

            var thread = new Thread(threadStarter);
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private void CloseButtonClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void RestartButtonClick(object sender, RoutedEventArgs e)
        {
            var list = new List<string>();
            foreach (Process clsProcess in Process.GetProcesses())
            {

                if (clsProcess.ProcessName == "Clock")
                {
                    clsProcess.Kill();
                    list.Add(E.Root + "\\Clock.exe");
                    continue;
                }

                if (clsProcess.ProcessName == "Weather")
                {
                    clsProcess.Kill();
                    list.Add(E.Root + "\\Weather.exe");
                    continue;
                }

                if (clsProcess.ProcessName == "Photos")
                {
                    clsProcess.Kill();
                    list.Add(E.Root + "\\Photos.exe");
                    continue;
                }

                if (clsProcess.ProcessName == "News")
                {
                    clsProcess.Kill();
                    list.Add(E.Root + "\\News.exe");
                    continue;
                }

                if (clsProcess.ProcessName == "FriendStream")
                {
                    clsProcess.Kill();
                    list.Add(E.Root + "\\FriendStream.exe");
                    continue;
                }

                if (clsProcess.ProcessName == "Calendar")
                {
                    clsProcess.Kill();
                    list.Add(E.Root + "\\Calendar.exe");
                    continue;
                }
            }

            foreach (var app in list)
            {
                Process.Start(app);
            }

            
            this.Close();
        }
    }
}
