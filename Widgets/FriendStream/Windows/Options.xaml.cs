using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Home.Base;
using Home.Packaging;
using Microsoft.Win32;
using Application = System.Windows.Application;
using CheckBox = System.Windows.Controls.CheckBox;
using MessageBox = System.Windows.MessageBox;

namespace FriendStream.Windows
{
    /// <summary>
    /// Interaction logic for Options.xaml
    /// </summary>
    public partial class Options : Window
    {
        private readonly List<string> langCodes = new List<string>();
        private bool restartRequired;

        public event EventHandler UpdateSettings;

        public Options()
        {
            InitializeComponent();
        }

        private void WindowSourceInitialized(object sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;

            if (!Dwm.IsGlassAvailable() || !Dwm.IsGlassEnabled())
            {
                this.Background = new SolidColorBrush(Color.FromRgb(185, 209, 234));
            }

            double dpiY = 1.0f;
            var source = PresentationSource.FromVisual(this);

            if (source != null)
            {
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            var margins = new Home.Base.WinAPI.Margins { cyTopHeight = (int)(34 * dpiY) };

            HwndSource.FromHwnd(handle).CompositionTarget.BackgroundColor = Color.FromArgb(0, 0, 0, 0);

            Home.Base.Dwm.ExtendGlassFrame(handle, ref margins);

            var fileInfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
            WidgetBuildTag.Text = Assembly.GetExecutingAssembly().GetName().Version.ToString() + ".beta." + fileInfo.LastWriteTimeUtc.ToString("yyMMdd-HHmm");
            HomeBuildTag.Text = Home.Base.E.VersionString;


            LanguageComboBox.Items.Add(new ComboBoxItem() { Content = CultureInfo.GetCultureInfo("en-US").NativeName });
            langCodes.Add("en-US");
            var langs = from x in Directory.GetDirectories(E.Root) where x.Contains("-") select System.IO.Path.GetFileNameWithoutExtension(x);
            foreach (var l in langs)
            {
                try
                {
                    var c = CultureInfo.GetCultureInfo(l);
                    langCodes.Add(c.Name);
                    LanguageComboBox.Items.Add(new ComboBoxItem() { Content = c.NativeName });
                }
                catch { }
            }

            LanguageComboBox.Text = CultureInfo.GetCultureInfo(App.Settings.Language).NativeName;

            SizeSlider.Value = Math.Round(App.Settings.Scale * 100);
            SizeValueTextBlock.Text = SizeSlider.Value + "%";

            TransparencySlider.Value = Math.Round((1 - App.Settings.Opacity) * 100);
            TransparencyValueTextBlock.Text = TransparencySlider.Value + "%";

            UpdateFreqSlider.Value = App.Settings.UpdateInterval;
            UpdateFreqValueTextBlock.Text = UpdateFreqSlider.Value + " " + Properties.Resources.OptionsIntervalMinutes;

            ShowTaskbarIconCheckBox.IsChecked = !App.Settings.UseTrayIcon;
            if (Environment.OSVersion.Version.Major <= 6 && Environment.OSVersion.Version.Minor < 1)
                ShowTaskbarIconCheckBox.Visibility = System.Windows.Visibility.Collapsed;

            var extrasList = ExtrasManager.GetInstalledExtrasInfo();
            if (extrasList != null)
            {
                foreach (var item in extrasList)
                {
                    ExtrasList.Items.Add(item);
                }
            }

            UpdatesCheckBox.IsChecked = App.Settings.CheckForUpdates;
            AutostartCheckBox.IsChecked = App.Settings.Autostart;

            var updates = Home.Updates.Updater.GetInstalledUpdatesInfoList();
            foreach (var updateInfo in updates)
            {
                UpdatesList.Items.Add(updateInfo);
            }

            SilentUpdateCheckBox.IsChecked = App.Settings.SilentUpdate;

            foreach (var provider in App.ProviderManager.Providers)
            {
                var checkBox = new CheckBox();
                checkBox.Margin = new Thickness(0,3,0,0);
                checkBox.Content = provider.Name;
                if (App.Settings.ActiveProviders.Contains(provider.Name))
                    checkBox.IsChecked = true;
                checkBox.Checked += CheckBoxChecked;
                checkBox.Unchecked += CheckBoxUnchecked;
                ProvidersPanel.Children.Add(checkBox);
            }

            RefreshFreqSlider.Value = App.Settings.RefreshInterval;
            RefreshFreqValueTextBlock.Text = RefreshFreqSlider.Value + " " + Properties.Resources.OptionsIntervalMinutes;

            switch (App.Settings.Style)
            {
                case Styles.Classic:
                    ClassicStyle.IsChecked = true;
                    break;
                case Styles.Modern:
                    ModernStyle.IsChecked = true;
                    break;
            }

            ApplyButton.IsEnabled = false;
        }

        void CheckBoxUnchecked(object sender, RoutedEventArgs e)
        {
            var checkBox = (CheckBox)sender;
            var providerName = (string)checkBox.Content;
            if (App.Settings.ActiveProviders.Contains(providerName))
            {
                App.Settings.ActiveProviders.Remove(providerName);
            }
            App.ProviderManager.DeactivateProvider(providerName);
            ApplyButton.IsEnabled = true;
        }

        void CheckBoxChecked(object sender, RoutedEventArgs e)
        {
            var checkBox = (CheckBox)sender;
            var providerName = (string)checkBox.Content;
            if (!App.Settings.ActiveProviders.Contains(providerName))
            {
                App.Settings.ActiveProviders.Add(providerName);
                App.ProviderManager.ActivateProvider(providerName);
            }
            ApplyButton.IsEnabled = true;
        }

        private void WindowClosed(object sender, EventArgs e)
        {
            App.Settings.OptionsWidth = Width;
            App.Settings.OptionsHeight = Height;

            if (restartRequired)
            {
                Process.Start(Application.ResourceAssembly.Location, "/c \"" + App.ConfigFile + "\"");
                Application.Current.Shutdown();
            }
        }

        private void OkButtonClick(object sender, RoutedEventArgs e)
        {
            if (ApplyButton.IsEnabled)
                ApplySettings();
            this.Close();
        }

        private void CancelButtonClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ApplyButtonClick(object sender, RoutedEventArgs e)
        {
            ApplySettings();
            ApplyButton.IsEnabled = false;
        }

        private void CheckBoxClick(object sender, RoutedEventArgs e)
        {
            ApplyButton.IsEnabled = true;
        }

        private void SizeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ApplyButton.IsEnabled = true;
            SizeValueTextBlock.Text = SizeSlider.Value + "%";
        }

        private void ComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyButton.IsEnabled = true;
        }

        private void ExtrasListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && ((ExtrasInfo)e.AddedItems[0]).Removable)
                DeleteExtButton.IsEnabled = true;
            else
                DeleteExtButton.IsEnabled = false;
        }

        private void InstallExtButtonClick(object sender, RoutedEventArgs e)
        {
            var d = new System.Windows.Forms.OpenFileDialog();
            d.DefaultExt = "*.hhpack";
            d.Filter = "HTC Home Package (*.hhpack)|*.hhpack|All files (*.*)|*.*";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var packageManager = new PackageManager();
                packageManager.BeginUnpack(d.FileName, E.Root);
            }
        }

        private void DeleteExtButtonClick(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(Properties.Resources.OptionsExtrasDeleteMessage, "HTC Home", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes)
            {
                ExtrasManager.DeleteExtra((ExtrasInfo)ExtrasList.SelectedItem);
                ExtrasList.Items.Remove(ExtrasList.SelectedItem);
            }
        }

        private void SiteLinkMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            WinAPI.ShellExecute(IntPtr.Zero, "open", "http://htchome.org", string.Empty, string.Empty, 0);
        }

        private void CheckUpdatesButtonClick(object sender, RoutedEventArgs e)
        {
            CheckUpdatesButton.IsEnabled = false;

            var args = App.Settings.Language + " /wcheck";
            Process.Start(E.Root + "\\Update.exe", args);
        }

        private void ApplySettings()
        {
            App.Settings.Scale = SizeSlider.Value / 100;
            App.Settings.Opacity = 1 - (TransparencySlider.Value / 100);
            App.Settings.UseTrayIcon = !(bool)ShowTaskbarIconCheckBox.IsChecked;
            App.Settings.CheckForUpdates = (bool)UpdatesCheckBox.IsChecked;
            App.Settings.SilentUpdate = (bool)SilentUpdateCheckBox.IsChecked;
            App.Settings.RefreshInterval = (int)RefreshFreqSlider.Value;

            if (App.Settings.UpdateInterval != UpdateFreqSlider.Value)
            {
                App.UpdateTimer.Interval = TimeSpan.FromMinutes(UpdateFreqSlider.Value);
                App.UpdateTimer.Stop();
                App.UpdateTimer.Start();
            }

            if (!App.Settings.CheckForUpdates)
            {
                App.UpdateTimer.Stop();
            }

            App.Settings.UpdateInterval = (int)UpdateFreqSlider.Value;

            var lastStyle = App.Settings.Style;
            if ((bool)ClassicStyle.IsChecked)
                App.Settings.Style = Styles.Classic;
            if ((bool)ModernStyle.IsChecked)
                App.Settings.Style = Styles.Modern;
            restartRequired = lastStyle != App.Settings.Style;   

            if (App.Settings.UseTrayIcon)
                ((App)Application.Current).AddTrayIcon();
            else
                ((App)Application.Current).RemoveTrayIcon();

            var lastLang = App.Settings.Language;
            if (LanguageComboBox.SelectedIndex >= 0)
                App.Settings.Language = langCodes[LanguageComboBox.SelectedIndex];
            if (!restartRequired)
                restartRequired = lastLang != App.Settings.Language;

            if (App.Settings.Autostart != (bool)AutostartCheckBox.IsChecked)
            {
                App.Settings.Autostart = (bool)AutostartCheckBox.IsChecked;
                if (App.Settings.Autostart)
                {
                    try
                    {
                        using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software", RegistryKeyPermissionCheck.ReadWriteSubTree).OpenSubKey("Microsoft").OpenSubKey("Windows").OpenSubKey("CurrentVersion").OpenSubKey("Run", true))
                        {
                            key.SetValue("FriendStream Widget (HTC Home)", "\"" + Assembly.GetExecutingAssembly().Location + "\"", RegistryValueKind.String);
                            key.Close();
                        }
                    }
                    catch { }
                }
                else
                {
                    try
                    {
                        using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software", RegistryKeyPermissionCheck.ReadWriteSubTree).OpenSubKey("Microsoft").OpenSubKey("Windows").OpenSubKey("CurrentVersion").OpenSubKey("Run", true))
                        {
                            key.DeleteValue("FriendStream Widget (HTC Home)", false);
                            key.Close();
                        }
                    }
                    catch { }
                }
            }

            App.Settings.Save(App.ConfigFile);

            if (UpdateSettings != null)
                UpdateSettings(null, EventArgs.Empty);
        }

        private void TransparencySliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ApplyButton.IsEnabled = true;
            TransparencyValueTextBlock.Text = TransparencySlider.Value + "%";
        }

        private void UpdateFreqSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ApplyButton.IsEnabled = true;
            UpdateFreqValueTextBlock.Text = UpdateFreqSlider.Value + " " + Properties.Resources.OptionsIntervalMinutes;
        }

        private void RefreshFreqSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ApplyButton.IsEnabled = true;
            RefreshFreqValueTextBlock.Text = RefreshFreqSlider.Value + " " + Properties.Resources.OptionsIntervalMinutes;
        }
    }
}
