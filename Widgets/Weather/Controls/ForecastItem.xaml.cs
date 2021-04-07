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
using Home.Base;

namespace Weather.Controls
{
    /// <summary>
    /// Interaction logic for ForecastItem.xaml
    /// </summary>
    public partial class ForecastItem : UserControl
    {
        public ForecastItem()
        {
            InitializeComponent();
        }

        public string Url { get; set; }

        private Point mouseCoords;
        private void UserControlMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            mouseCoords = e.MouseDevice.GetPosition(this);
        }

        private void UserControlMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (mouseCoords == e.MouseDevice.GetPosition(this) && !string.IsNullOrEmpty(Url))
            {
                WinAPI.ShellExecute(IntPtr.Zero, "open", Url, string.Empty, string.Empty, 0);
            }
        }
    }
}
