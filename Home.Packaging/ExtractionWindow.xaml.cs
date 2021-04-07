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
using System.Windows.Interop;
using Home.Base;

namespace Home.Packaging
{
    /// <summary>
    /// Interaction logic for ExtractionWindow.xaml
    /// </summary>
    public partial class ExtractionWindow : Window
    {
        public ExtractionWindow()
        {
            InitializeComponent();
        }

        private void CloseButtonClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void WindowSourceInitialized(object sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;

            //WinAPI.RemoveWindowIcon(handle);

            //if (!Dwm.IsGlassAvailable() || !Dwm.IsGlassEnabled())
            //{
            //    this.Background = new SolidColorBrush(Color.FromRgb(185, 209, 234));
            //}

            //var margins = new Home.Base.WinAPI.Margins { cyTopHeight = 34 };

            //HwndSource.FromHwnd(handle).CompositionTarget.BackgroundColor = Color.FromArgb(0, 0, 0, 0);

            //Home.Base.Dwm.ExtendGlassFrame(handle, ref margins);
        }
    }
}
