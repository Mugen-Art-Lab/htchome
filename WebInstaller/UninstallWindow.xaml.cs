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
using System.Globalization;
using System.IO;
using System.Diagnostics;

namespace WebInstaller
{
    /// <summary>
    /// Interaction logic for UninstallWindow.xaml
    /// </summary>
    public partial class UninstallWindow : Window
    {
        public UninstallWindow()
        {
            InitializeComponent();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            if (!File.Exists(App.Path + "\\Uninstall\\Localization\\" + CultureInfo.CurrentUICulture.Name + ".xaml"))
                Locale.Source = new Uri(App.Path + "\\Uninstall\\Localization\\en-US.xaml");
            else
                Locale.Source = new Uri(App.Path + "\\Uninstall\\Localization\\" + CultureInfo.CurrentUICulture.Name + ".xaml");
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            CancelButton.IsEnabled = false;
            string dir = "HTC Home";
            if (MainWindow.TYPE == "dev")
                dir += " Dev";

            if (Directory.Exists(App.Path))
            {
                foreach (string f in Directory.GetFiles(App.Path, "*.*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.Delete(f);
                    }
                    catch (Exception ex)
                    {

                    }
                }
                RemoveShortcutFromDesktop("HTC Home");
                RemoveShortcutFromStartmenu();

                foreach (string d in Directory.GetDirectories(App.Path))
                {
                    if (!d.EndsWith("Uninstall"))
                        Directory.Delete(d, true);
                }
            }
            CancelButton.IsEnabled = true;
            CancelButton.Content = (string)Resources["Close"];
            OkButton.Visibility = System.Windows.Visibility.Hidden;
            MessageTextBlock.Text = (string)Resources["UninstallFinished"];
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void RemoveShortcutFromDesktop(string linkName)
        {
            string deskDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (File.Exists(deskDir + "\\" + linkName + ".url"))
                File.Delete(deskDir + "\\" + linkName + ".url");
        }

        private void RemoveShortcutFromStartmenu()
        {
            string startMenuDir = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            if (Directory.Exists(startMenuDir + "\\HTC Home"))
                Directory.Delete(startMenuDir + "\\HTC Home", true);
        }
    }
}
