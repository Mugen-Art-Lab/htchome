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
using System.Reflection;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace HTCHome
{
    /// <summary>
    /// Interaction logic for Widget.xaml
    /// </summary>
    public partial class Widget : Window
    {
        private IntPtr handle;
        private IWidget widget;
        public string path;

        public string WidgetName
        {
            get;
            set;
        }

        public string WidgetIcon
        {
            get;
            set;
        }

        public bool IsWidgetLoaded
        {
            get;
            set;
        }

        public bool HasErrors
        {
            get;
            set;
        }

        public Widget()
        {
            InitializeComponent();
        }

        public void Initalize(string path)
        {
            this.path = path;

            Assembly assembly = Assembly.LoadFrom(path);

            Type widgetType = null;

            try
            {
                widgetType = assembly.GetTypes().FirstOrDefault(type => typeof(IWidget).IsAssignableFrom(type));
            }
            catch (Exception ex)
            {
                App.Log(ex.ToString());
            }

            if (widgetType == null)
            {
                App.Log(path + " is not a widget.");
                HasErrors = true;
                return;
            }

            widget = Activator.CreateInstance(widgetType) as IWidget;
            WidgetName = widget.GetWidgetName();
            WidgetIcon = widget.GetIcon();

            widget.UpdateAeroEvent += new EventHandler(widget_UpdateAero);

            CloseItem.Header = App.LocaleManager.GetString("Close");
            CloseHomeItem.Header = App.LocaleManager.GetString("CloseHome");
            AddWidgetItem.Header = App.LocaleManager.GetString("Add");
            HomeOptionsItem.Header = App.LocaleManager.GetString("HomeOptions");
            PinItem.Header = App.LocaleManager.GetString("Pin");
            TopMostItem.Header = App.LocaleManager.GetString("TopMost");
            SizeItem.Header = App.LocaleManager.GetString("Size");
        }

        void widget_UpdateAero(object sender, EventArgs e)
        {
            if (App.sett.EnableGlass)
            {
                WinAPI.RemoveGlassRegion(ref handle);
                WinAPI.MakeGlassRegion(ref handle, widget.GetRegion());
            }
        }

        public void Load()
        {
            UserControl w = widget.Load();
            w.SizeChanged += new SizeChangedEventHandler(w_SizeChanged);
            widget.SetParent(this);
            this.Width = w.Width;
            this.Height = w.Height;
            this.Left = widget.GetWindowPosition().X;
            this.Top = widget.GetWindowPosition().Y;
            if (this.Left == -1 || this.Top == -1)
            {
                this.Left = System.Windows.Forms.SystemInformation.WorkingArea.Width / 2 - w.Width / 2;
                this.Top = System.Windows.Forms.SystemInformation.WorkingArea.Height / 2 - w.Height / 2 - 100;
            }
            this.Show();

            SizeSlider.Value = widget.GetScalefactor() * 100;

            this.Topmost = widget.GetTopMost();
            TopMostItem.IsChecked = this.Topmost;

            PinItem.IsChecked = widget.GetPin();

            if (App.sett.EnableGlass)
                WinAPI.MakeGlassRegion(ref handle, widget.GetRegion());

            foreach (Widget widget1 in App.widgets)
            {
                System.Windows.Controls.MenuItem item = new System.Windows.Controls.MenuItem();
                item.Header = widget1.WidgetName;
                System.Windows.Controls.Image icon = new System.Windows.Controls.Image();
                icon.Source = new BitmapImage(new Uri(widget1.WidgetIcon));
                icon.Width = 25;
                icon.Height = 25;
                item.Icon = icon;
                item.Click += AddWidgetItem_Click;
                AddWidgetItem.Items.Add(item);
                //((System.Windows.Controls.MenuItem)trayMenu.Items[0]).Items.Add(item);
            }

            WinAPI.SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, -1, -1); //наверное так делать не стоит, но зато теперь те, кто говорит "ОМГ он жрет столько памяти!!!11" могут успокоиться
        }

        void w_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.Width = e.NewSize.Width;
            this.Height = e.NewSize.Height;
        }

        public void Unload()
        {
            widget.SetWindowPosition(this.Left, this.Top);
            widget.Unload();
            //IsWidgetLoaded = false;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !PinItem.IsChecked)
            {
                DragMove();
                widget.SetWindowPosition(this.Left, this.Top);
            }
        }

        private void CloseItem_Click(object sender, RoutedEventArgs e)
        {
            IsWidgetLoaded = false;
            Unload();
            this.Close();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            handle = new WindowInteropHelper(this).Handle;

            WinAPI.RemoveFromAeroPeek(handle);
            WinAPI.RemoveFromAltTab(handle);
            WinAPI.RemoveFromFlip3D(handle);

            MainGrid.Children.Add(widget.GetWidgetControl());
            IsWidgetLoaded = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (IsWidgetLoaded)
            {
                Unload();
            }

            int count = 0;
            foreach (Widget w in App.widgets)
            {
                if (w.IsLoaded)
                    count++;
            }
            if (count == 1)
                App.Current.Shutdown();
        }

        private void TopMostItem_Checked(object sender, RoutedEventArgs e)
        {
            this.Topmost = true;
            widget.SetTopMost(true);
        }

        private void TopMostItem_Unchecked(object sender, RoutedEventArgs e)
        {
            this.Topmost = false;
            widget.SetTopMost(false);
        }

        private void SizeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            double scale = 1.0f - ((SizeItem.Items.IndexOf(sender)) / 10.0f);
            widget.SetScalefactor(scale);
            foreach (MenuItem item in SizeItem.Items)
            {
                if (sender != item)
                    item.IsChecked = false;
                else
                    item.IsChecked = true;
            }

            if (App.sett.EnableGlass)
            {
                WinAPI.RemoveGlassRegion(ref handle);
                WinAPI.MakeGlassRegion(ref handle, widget.GetRegion());
            }
        }

        private void PinItem_Checked(object sender, RoutedEventArgs e)
        {
            widget.SetPin(true);
        }

        private void PinItem_Unchecked(object sender, RoutedEventArgs e)
        {
            widget.SetPin(false);
        }

        private void SizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            /*SizeSlider.ToolTip = SizeSlider.Value.ToString();
            ((ToolTip)SizeSlider.ToolTip).IsOpen = true;*/
            if (widget != null)
            {
                widget.SetScalefactor(SizeSlider.Value / 100);
                if (App.sett.EnableGlass)
                {
                    WinAPI.RemoveGlassRegion(ref handle);
                    WinAPI.MakeGlassRegion(ref handle, widget.GetRegion());
                }
            }
        }

        private void HomeOptionsItem_Click(object sender, RoutedEventArgs e)
        {
            ((App)App.Current).ShowOptions();
        }

        private void CloseHomeItem_Click(object sender, RoutedEventArgs e)
        {
            App.Current.Shutdown();
        }

        private void AddWidgetItem_Click(object sender, RoutedEventArgs e)
        {
            int index = AddWidgetItem.Items.IndexOf(sender);
            if (!App.widgets[index].IsWidgetLoaded || !App.widgets[index].IsVisible)
            {
                Widget w = new Widget();
                w.Initalize(App.widgets[index].path);
                App.widgets[index] = w;
                App.widgets[index].Load();
            }
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            var files = from x in ((string[])e.Data.GetData(DataFormats.FileDrop, true))
                        where x.EndsWith(".hhskin")
                        select x;
            if (files != null)
            {
                foreach (string f in files)
                {
                    App.Unpack(App.Path, f);
                }
            }
        }
    }
}
