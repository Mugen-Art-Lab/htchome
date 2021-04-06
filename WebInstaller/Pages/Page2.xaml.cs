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
using System.Diagnostics;
using System.Reflection;

namespace WebInstaller.Pages
{
    /// <summary>
    /// Interaction logic for Page2.xaml
    /// </summary>
    public partial class Page2 : UserControl
    {
        public Page2()
        {
            InitializeComponent();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            E.InstallPath = PathTextBox.Text;
            ((Grid)Parent).Children.Add(new Page1());
            ((Grid)Parent).Children.Remove(this);

        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.Shutdown();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            PathTextBox.Text = E.InstallPath;
            DesktopShortcut.IsChecked = E.DesktopShortcut;
            StartMenuShortcut.IsChecked = E.StartMenuShortcut;
        }

        private void PathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ShieldIcon != null)
            {
                if (!string.IsNullOrEmpty(PathTextBox.Text) && PathTextBox.Text.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)))
                {
                    ShieldIcon.Visibility = System.Windows.Visibility.Visible;
                    E.ShowShield = true;
                }
                else
                {
                    ShieldIcon.Visibility = System.Windows.Visibility.Collapsed;
                    E.ShowShield = false;
                }
            }
            E.InstallPath = PathTextBox.Text;
        }

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessStartInfo p = new ProcessStartInfo();
            if (E.ShowShield)
                p.Verb = "runas";
            p.FileName = Assembly.GetExecutingAssembly().Location;
            p.Arguments = "/skip";
            if (E.DesktopShortcut)
                p.Arguments += " /desktop";
            if (E.StartMenuShortcut)
                p.Arguments += " /startmenu";
            p.Arguments += " \"" + E.InstallPath + "\"";
            Process.Start(p);
            App.Current.Shutdown();
        }

        private void ChangeButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog d = new System.Windows.Forms.FolderBrowserDialog();
            d.SelectedPath = E.InstallPath;
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                PathTextBox.Text = d.SelectedPath;
                E.InstallPath = d.SelectedPath;
            }
        }

        private void DesktopShortcut_Click(object sender, RoutedEventArgs e)
        {
            E.DesktopShortcut = (bool)DesktopShortcut.IsChecked;
        }

        private void StartMenuShortcut_Click(object sender, RoutedEventArgs e)
        {
            E.StartMenuShortcut = (bool)StartMenuShortcut.IsChecked;
        }
    }
}
