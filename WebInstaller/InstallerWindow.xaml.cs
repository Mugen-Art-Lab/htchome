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
using WebInstaller.Pages;
using System.Xml;
using System.IO;
using System.Globalization;

namespace WebInstaller
{
    /// <summary>
    /// Interaction logic for InstallerWindow.xaml
    /// </summary>
    public partial class InstallerWindow : Window
    {
        public InstallerWindow()
        {
            InitializeComponent();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            if (!File.Exists(App.Path + "\\Localization\\" + CultureInfo.CurrentUICulture.Name + ".xaml"))
                Locale.Source = new Uri(App.Path + "\\Localization\\en-US.xaml");
            else
                Locale.Source = new Uri(App.Path + "\\Localization\\" + CultureInfo.CurrentUICulture.Name + ".xaml");

            MainGrid.Children.Clear();
            if (!E.SkipStartPage)
                MainGrid.Children.Add(new Page1());
            else
                MainGrid.Children.Add(new Page3());
        }
    }
}
