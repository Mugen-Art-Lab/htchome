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

namespace Clock.Controls
{
    /// <summary>
    /// Interaction logic for OptionsLocationItem.xaml
    /// </summary>
    public partial class OptionsLocationItem : UserControl
    {
        public OptionsLocationItem()
        {
            InitializeComponent();
        }

        public string City
        {
            get { return CityName.Text; }
            set { CityName.Text = value; }
        }

        public string Country
        {
            get { return CountryName.Text; }
            set { CountryName.Text = value; }
        }

        //private void UserControlMouseEnter(object sender, MouseEventArgs e)
        //{
        //    BgRect.Visibility = System.Windows.Visibility.Visible;
        //    BgBorderRect.Visibility = System.Windows.Visibility.Visible;
        //}

        //private void UserControlMouseLeave(object sender, MouseEventArgs e)
        //{
        //    BgRect.Visibility = System.Windows.Visibility.Collapsed;
        //    BgBorderRect.Visibility = System.Windows.Visibility.Collapsed;
        //}
    }
}
