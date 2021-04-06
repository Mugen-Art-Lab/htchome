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
    /// Interaction logic for Page1.xaml
    /// </summary>
    public partial class Page1 : UserControl
    {
        public Page1()
        {
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            App.Current.Shutdown();
        }

        private void OptionsButton_Click(object sender, RoutedEventArgs e)
        {
            ((Grid)Parent).Children.Add(new Page2());
            ((Grid)Parent).Children.Remove(this);
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (E.ShowShield)
                ShieldIcon.Visibility = System.Windows.Visibility.Visible;
            else
                ShieldIcon.Visibility = System.Windows.Visibility.Collapsed;
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
    }
}
