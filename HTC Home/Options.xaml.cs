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
using System.Windows.Interop;
using HTCHome.Core;
using System.IO;
using Microsoft.Win32;
using System.Reflection;

namespace HTCHome
{
    /// <summary>
    /// Interaction logic for Options.xaml
    /// </summary>
    public partial class Options : Window
    {
        private IntPtr handle;
        private List<string> localeCodes = new List<string>();

        public Options()
        {
            InitializeComponent();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            handle = new WindowInteropHelper(this).Handle;

            WinAPI.MARGINS margins = new WinAPI.MARGINS();
            margins.cyTopHeight = 24;

            HwndSource.FromHwnd(handle).CompositionTarget.BackgroundColor = Color.FromArgb(0, 0, 0, 0);

            WinAPI.ExtendGlassFrame(handle, ref margins);

            System.Reflection.Assembly _AsmObj = System.Reflection.Assembly.GetExecutingAssembly();
            System.Reflection.AssemblyName _CurrAsmName = _AsmObj.GetName();
            string _Major = _CurrAsmName.Version.Major.ToString();
            string _Minor = _CurrAsmName.Version.Minor.ToString();
            string _Build = _CurrAsmName.Version.Build.ToString();

            VersionString.Text = string.Format("Version {0}.{1} Build {2}", _Major, _Minor, _Build);
            TranslatedBy.Text = App.LocaleManager.GetString("LocaleAuthor");

            OkButton.Content = App.LocaleManager.GetString("OK");
            CancelButton.Content = App.LocaleManager.GetString("Cancel");
            ApplyButton.Content = App.LocaleManager.GetString("Apply");

            Title = App.LocaleManager.GetString("Options");
            AutostartCheckBox.Content = App.LocaleManager.GetString("Autostart");
            EnableGlassCheckBox.Content = App.LocaleManager.GetString("EnableGlass");
            CheckForUpdatesCheckBox.Content = App.LocaleManager.GetString("CheckForUpdates");
            LangTextBlock.Text = App.LocaleManager.GetString("Language");
            RestartText.Text = App.LocaleManager.GetString("RestartText");

            EnableGlassCheckBox.IsChecked = App.sett.EnableGlass;
            AutostartCheckBox.IsChecked = App.sett.Autostart;
            CheckForUpdatesCheckBox.IsChecked = App.sett.EnableUpdates;

            if (Directory.Exists(App.Path + "\\Localization"))
            {
                foreach (string f in Directory.GetFiles(App.Path + "\\Localization", "*.xaml"))
                {
                    
                    LangComboBox.Items.Add(LocaleManager.GetLocaleName(f));
                    localeCodes.Add(LocaleManager.GetLocaleCode(f));
                }
            }

            LangComboBox.SelectedIndex = localeCodes.IndexOf(Core.Environment.Locale);

            GeneralTab.Header = App.LocaleManager.GetString("General");
            AboutTab.Header = App.LocaleManager.GetString("About");
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            ApplyButton.IsEnabled = true;
        }

        private void ComboBox_SelectionChanged_1(object sender, SelectionChangedEventArgs e)
        {
            ApplyButton.IsEnabled = true;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ApplySettings();
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplySettings();
            ApplyButton.IsEnabled = false;
        }

        private void ContactString_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            WinAPI.ShellExecute(IntPtr.Zero, "open", ContactString.Text, string.Empty, string.Empty, 0);
        }

        private void ApplySettings()
        {
            
            App.sett.EnableGlass = (bool)EnableGlassCheckBox.IsChecked;
            App.sett.Autostart = (bool)AutostartCheckBox.IsChecked;
            App.sett.Locale = localeCodes[LangComboBox.SelectedIndex];
            App.sett.EnableUpdates = (bool)CheckForUpdatesCheckBox.IsChecked;
            HTCHome.Core.Environment.Locale = localeCodes[LangComboBox.SelectedIndex];
            App.LocaleManager.LoadLocale(localeCodes[LangComboBox.SelectedIndex]);

            if (AutostartCheckBox.IsChecked == true)
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software", RegistryKeyPermissionCheck.ReadWriteSubTree).OpenSubKey("Microsoft").OpenSubKey("Windows").OpenSubKey("CurrentVersion").OpenSubKey("Run", true))
                    {
                        key.SetValue("HTC Home", "\"" + Assembly.GetExecutingAssembly().Location + "\"", RegistryValueKind.String);
                        key.Close();
                    }
                }
                catch (Exception ex)
                {
                    Core.Logger.Log(ex.ToString());
                }
            }
            else
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software", RegistryKeyPermissionCheck.ReadWriteSubTree).OpenSubKey("Microsoft").OpenSubKey("Windows").OpenSubKey("CurrentVersion").OpenSubKey("Run", true))
                    {
                        key.DeleteValue("HTC Home", false);
                        key.Close();
                    }
                }
                catch (Exception ex)
                {
                    Core.Logger.Log(ex.ToString());
                }
            }

            App.sett.LoadedWidgets = new List<string>();

            foreach (Widget w in App.widgets)
            {
                if (w.IsWidgetLoaded)
                    App.sett.LoadedWidgets.Add(w.WidgetName);
            }

            App.sett.Write(App.Path + "\\Config\\Home.conf");
        }
    }
}
