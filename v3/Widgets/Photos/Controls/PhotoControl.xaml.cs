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

namespace Photos.Controls
{
    /// <summary>
    /// Interaction logic for PhotoControl.xaml
    /// </summary>
    public partial class PhotoControl : UserControl
    {
        public double Rotate
        {
            get { return Rotation.Angle; }
            set
            {
                Rotation.Angle = value;
            }
        }

        public ImageSource Source
        {
            get { return Image.Source; }
            set { Image.Source = value; }
        }

        public PhotoControl()
        {
            InitializeComponent();
        }

        private void UserControlMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount > 1 && Image.Source != null)
            {
                var path = ((BitmapImage) Image.Source).UriSource.OriginalString;
                WinAPI.ShellExecute(IntPtr.Zero, "open", path, string.Empty, string.Empty, 0);
            }
        }
    }
}
