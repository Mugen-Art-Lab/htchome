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
        private IntPtr handle;
        private News widget;

        private NumberFormatInfo f = new NumberFormatInfo();

        public Options(News w)
        {
            InitializeComponent();

            widget = w;
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            handle = new WindowInteropHelper(this).Handle;

            f.CurrencyDecimalSeparator = ".";

            WinAPI.MARGINS margins = new WinAPI.MARGINS();
            margins.cyTopHeight = 24;

            HwndSource.FromHwnd(handle).CompositionTarget.BackgroundColor = Color.FromArgb(0, 0, 0, 0);

            WinAPI.ExtendGlassFrame(handle, ref margins);

            GeneralTab.Header = Widget.LocaleManager.GetString("General");
            SourcesTab.Header = Widget.LocaleManager.GetString("Sources");

            NewsCountTextBlock.Text = Widget.LocaleManager.GetString("NewsCount");
            OpacityTextBlock.Text = Widget.LocaleManager.GetString("Opacity");
            IntervalTextBlock.Text = Widget.LocaleManager.GetString("Interval");
            PreviewFontSizeTextBlock.Text = Widget.LocaleManager.GetString("PreviewFontSize");

            OkButton.Content = Widget.LocaleManager.GetString("OK");
            CancelButton.Content = Widget.LocaleManager.GetString("Cancel");
            ApplyButton.Content = Widget.LocaleManager.GetString("Apply");

            AddSourceTextBlock.Text = Widget.LocaleManager.GetString("AddSource");

            IntervalTextBox.Text = Widget.Sett.interval.ToString(f);
            NewsCountTextBox.Text = Widget.Sett.newsCount.ToString();
            OpacityTextBox.Text = Widget.Sett.opacity.ToString(f);
            //SourceTextBox.Text = Widget.Sett.source;
            PreviewFontSizeTextBox.Text = Widget.Sett.previewFontSize.ToString();

            if (Widget.Sett.source != null)
            {
                foreach (string s in Widget.Sett.source)
                {
                    SourceItem item = new SourceItem();
                    item.Header = s;
                    item.ItemRemoved += new SourceItem.ItemRemovedHandler(item_ItemRemoved);
                    SourcesPanel.Children.Add(item);
                }
            }

            ApplyButton.IsEnabled = false;
        }

        void item_ItemRemoved(int index)
        {
            widget.sources.RemoveAt(index);
            Widget.Sett.source.RemoveAt(index);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ApplyButton.IsEnabled)
            {
                ApplySettings();
            }

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

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyButton.IsEnabled = true;
        }

        private void ApplySettings()
        {

            Widget.Sett.interval = Convert.ToDouble(IntervalTextBox.Text, f);
            Widget.Sett.newsCount = Convert.ToInt32(NewsCountTextBox.Text);
            Widget.Sett.opacity = Convert.ToDouble(OpacityTextBox.Text, f);
            //Widget.Sett.source = SourceTextBox.Text;
            Widget.Sett.previewFontSize = Convert.ToInt32(PreviewFontSizeTextBox.Text);

            widget.UpdateSettings();
        }

        private void AddSourceBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrEmpty(AddSourceBox.Text) && AddSourceBox.Text.StartsWith("http://"))
            {
                Source source = new Source();
                source.Url = AddSourceBox.Text;
                source.GetNewsFinished += widget.source_GetNewsFinished;
                widget.sources.Add(source);
                Widget.Sett.source.Add(AddSourceBox.Text);

                SourceItem item = new SourceItem();
                item.Header = AddSourceBox.Text;
                item.ItemRemoved += new SourceItem.ItemRemovedHandler(item_ItemRemoved);
                SourcesPanel.Children.Add(item);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(AddSourceBox.Text) && AddSourceBox.Text.StartsWith("http://"))
            {
                Source source = new Source();
                source.Url = AddSourceBox.Text;
                source.GetNewsFinished += widget.source_GetNewsFinished;
                widget.sources.Add(source);
                if (Widget.Sett.source == null)
                    Widget.Sett.source = new List<string>();
                Widget.Sett.source.Add(AddSourceBox.Text);

                SourceItem item = new SourceItem();
                item.Header = AddSourceBox.Text;
                item.ItemRemoved += new SourceItem.ItemRemovedHandler(item_ItemRemoved);
                SourcesPanel.Children.Add(item);
            }
        }
    }
}
