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

namespace WebInstaller.Pages
{
    /// <summary>
    /// Interaction logic for InstallFinishPage.xaml
    /// </summary>
    public partial class InstallFinishPage : UserControl
    {
        public InstallFinishPage()
        {
            InitializeComponent();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            string exename = "HTCHome.exe";
            if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE").Contains("64"))
                exename = "HTCHome (x64).exe";

            if (LaunchCheckBox.IsChecked == true)
            {
                ProcessStartInfo p = new ProcessStartInfo();
                if (E.ShowShield)
                    p.Verb = "runas";
                p.FileName = E.InstallPath + "\\" + exename;
                p.Arguments = "/skip";
                Process.Start(p);
            }
            App.Current.Shutdown();
        }

    }
}
