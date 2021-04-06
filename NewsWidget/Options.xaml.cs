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
using System.Globalization;

namespace NewsWidget
{
    /// <summary>
    /// Interaction logic for Options.xaml
    /// </summary>
    public partial class Options : Window
    {
        private IntPtr _handle;
        private News widget;

        //private NumberFormatInfo f = new NumberFormatInfo();

        public Options(News w)
        {
            InitializeComponent();

            widget = w;
        }

        private void WindowSourceInitialized(object sender, EventArgs e)
        {
            _handle = new WindowInteropHelper(this).Handle;

            var margins = new WinAPI.MARGINS {cyTopHeight = 24};

            HwndSource.FromHwnd(_handle).CompositionTarget.BackgroundColor = Color.FromArgb(0, 0, 0, 0);

            WinAPI.ExtendGlassFrame(_handle, ref margins);

            GeneralTab.Header = Widget.Instance.LocaleManager.GetString("General");
            SourcesTab.Header = Widget.Instance.LocaleManager.GetString("Sources");

            NewsCountTextBlock.Text = Widget.Instance.LocaleManager.GetString("NewsCount");
            IntervalTextBlock.Text = Widget.Instance.LocaleManager.GetString("Interval");

            OkButton.Content = Widget.Instance.LocaleManager.GetString("OK");
            CancelButton.Content = Widget.Instance.LocaleManager.GetString("Cancel");
            ApplyButton.Content = Widget.Instance.LocaleManager.GetString("Apply");

            AddSourceTextBlock.Text = Widget.Instance.LocaleManager.GetString("AddSource");

            IntervalComboBox.Text = Widget.Instance.Sett.Interval.ToString();
            NewsCountComboBox.Text = Widget.Instance.Sett.NewsCount.ToString();

            if (Widget.Instance.Sett.Sources != null)
            {
                foreach (string s in Widget.Instance.Sett.Sources)
                {
                    var item = new SourceItem();
                    item.Header = s;
                    item.ItemRemoved += ItemItemRemoved;
                    SourcesPanel.Children.Add(item);
                }
            }
            
            ApplyButton.IsEnabled = false;
        }

        void ItemItemRemoved(int index)
        {
            widget.Sources.RemoveAt(index);
            Widget.Instance.Sett.Sources.RemoveAt(index);
        }

        private void OkButtonClick(object sender, RoutedEventArgs e)
        {
            if (ApplyButton.IsEnabled)
            {
                ApplySettings();
            }

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

        private void TextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyButton.IsEnabled = true;
        }

        private void ApplySettings()
        {

            Widget.Instance.Sett.Interval = Convert.ToInt32(IntervalComboBox.Text);
            Widget.Instance.Sett.NewsCount = Convert.ToInt32(NewsCountComboBox.Text);

            //widget.UpdateSettings();
        }

        private void AddSourceBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrEmpty(AddSourceBox.Text) && AddSourceBox.Text.StartsWith("http://"))
            {
                var source = new Source {Url = AddSourceBox.Text};
                source.GetNewsFinished += widget.GetNewsFinished;
                widget.Sources.Add(source);   
                if (Widget.Instance.Sett.Sources == null)
                    Widget.Instance.Sett.Sources = new List<string>();
                Widget.Instance.Sett.Sources.Add(AddSourceBox.Text);

                var item = new SourceItem {Header = AddSourceBox.Text};
                item.ItemRemoved += ItemItemRemoved;
                SourcesPanel.Children.Add(item);

                AddSourceBox.Text = "";
            }
        }

        private void ButtonClick(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(AddSourceBox.Text) && AddSourceBox.Text.StartsWith("http://"))
            {
                var source = new Source();
                source.Url = AddSourceBox.Text;
                source.GetNewsFinished += widget.GetNewsFinished;
                widget.Sources.Add(source);
                if (Widget.Instance.Sett.Sources == null)
                    Widget.Instance.Sett.Sources = new List<string>();
                Widget.Instance.Sett.Sources.Add(AddSourceBox.Text);

                SourceItem item = new SourceItem();
                item.Header = AddSourceBox.Text;
                item.ItemRemoved += new SourceItem.ItemRemovedHandler(ItemItemRemoved);
                SourcesPanel.Children.Add(item);
                AddSourceBox.Text = "";
            }
        }
    }
}
