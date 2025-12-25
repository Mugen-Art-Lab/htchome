using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Home.Base;

namespace News.Windows
{
    /// <summary>
    /// Interaction logic for PreviewWindow.xaml
    /// </summary>
    public partial class PreviewWindow : Window
    {
        public string Header
        {
            get { return TitleBox.Text; }
            set { TitleBox.Text = value; }
        }

        public string Url { get; set; }

        public string Description
        {
            set
            {
                //TODO make encoding detection
                const string header = "<head><meta http-equiv='Content-Type' content='text/html;charset=UTF-8'></head>";
                Browser.NavigateToString(header + value);
                Browser.Navigating += BrowserNavigating;
            }
        }

        void BrowserNavigating(object sender, System.Windows.Navigation.NavigatingCancelEventArgs e)
        {
            e.Cancel = true;
            WinAPI.ShellExecute(IntPtr.Zero, "open", e.Uri.OriginalString, string.Empty, string.Empty, 0);
            this.Close();
        }

        public PreviewWindow()
        {
            InitializeComponent();
        }

        private void WindowSourceInitialized(object sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            var margins = new WinAPI.Margins();
            margins.cxLeftWidth = 4;
            margins.cxRightWidth = 4;
            margins.cyTopHeight = 4;
            margins.cyBottomHeight = 4;

            HwndSource.FromHwnd(handle).CompositionTarget.BackgroundColor = Color.FromArgb(0, 0, 0, 0);
            Dwm.ExtendGlassFrame(handle, ref margins);

            if (App.Current.MainWindow.Left + App.Current.MainWindow.Width * App.Settings.Scale + this.Width > SystemParameters.PrimaryScreenWidth)
                this.Left = App.Current.MainWindow.Left - this.Width;
            else
                this.Left = App.Current.MainWindow.Left + App.Current.MainWindow.Width * App.Settings.Scale;
            this.Top = App.Current.MainWindow.Top + 15 * App.Settings.Scale; 

        }

        private void TitleBoxMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            WinAPI.ShellExecute(IntPtr.Zero, "open", Url, string.Empty, string.Empty, 0);
            this.Close();
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Browser.Navigating -= BrowserNavigating;
        }
    }
}
