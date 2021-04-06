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
using HTCHome.Core;
using System.Windows.Interop;

namespace CalendarWidget
{
    /// <summary>
    /// Interaction logic for Options.xaml
    /// </summary>
    public partial class Options : Window
    {
        private IntPtr handle;

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

            GeneralTab.Header = Widget.LocaleManager.GetString("General");
            SynchronizeCheckBox.Content = Widget.LocaleManager.GetString("Synchronize");
            UsernameTextBlock.Text = Widget.LocaleManager.GetString("Username");
            PassTextBlock.Text = Widget.LocaleManager.GetString("Password");

            SynchronizeCheckBox.IsChecked = Widget.Sett.synchronizeWithGoogle;
            UsernameTextBox.Text = Widget.Sett.username;
            PassTextBox.Password = Widget.Sett.password;
        }

        private void SynchronizeCheckBox_Click(object sender, RoutedEventArgs e)
        {
            ApplyButton.IsEnabled = true;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
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

        private void ApplySettings()
        {
            Widget.Sett.synchronizeWithGoogle = (bool)SynchronizeCheckBox.IsChecked;
            Widget.Sett.username = UsernameTextBox.Text;
            Widget.Sett.password = PassTextBox.Password;
        }
    }
}
