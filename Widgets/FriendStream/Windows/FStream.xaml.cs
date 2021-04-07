using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using FriendStream.Controls;
using FriendStream.Domain;
using Home.Base;
using Social.Base;

namespace FriendStream.Windows
{
    /// <summary>
    /// Interaction logic for FriendStream.xaml
    /// </summary>
    public partial class FStream : Window
    {
        private Options optionsWindow;
        private IntPtr handle;
        private DispatcherTimer timer;
        private Wall wall;

        public FStream()
        {
            InitializeComponent();
        }

        private void WindowSourceInitialized(object sender, EventArgs e)
        {
            handle = new WindowInteropHelper(this).Handle;

            if (!App.Settings.DisableUnminimizer)
            {
                var u = new Unminimizer();
                u.Initialize(handle);
            }

            Dwm.RemoveFromAeroPeek(handle);
            Dwm.RemoveFromAltTab(handle);
            Dwm.RemoveFromFlip3D(handle);

            this.Left = App.Settings.Left;
            this.Top = App.Settings.Top;


            if (this.Left == -100.0f || this.Top == -100.0f)
            {
                this.Left = SystemParameters.WorkArea.Width / 2 - 150;
                this.Top = SystemParameters.WorkArea.Height / 2 - 150;
            }

            this.Opacity = App.Settings.Opacity;
            this.ShowInTaskbar = !App.Settings.UseTrayIcon;
            Scale.ScaleX = App.Settings.Scale;

            if (App.Settings.TopMost)
            {
                this.Topmost = true;
                TopMostItem.IsChecked = true;
            }


            if (App.Settings.Pin)
                PinItem.IsChecked = true;

            if (App.Settings.UseAero)
            {
                UpdateAero();
            }

            RefreshTime.Text = DateTime.Now.ToShortTimeString();

            wall = new Wall();
            wall.RefreshFinished += WallRefreshFinished;
            wall.EntryUpdated += new Wall.EntryUpdatedHandler(WallEntryUpdated);

            RefreshTime.Text = Properties.Resources.Refreshing;

            StatusBox.IsEnabled = false;
            ThreadStart threadStarter = delegate
                                            {
                                                wall.Initialize();
                                                wall.Refresh();
                                                RefreshStatusPanel.Dispatcher.Invoke((Action)delegate
                                                                                                  {
                                                                                                      RefreshTime.Text = DateTime.Now.ToShortTimeString();
                                                                                                  });

                                                StatusBox.Dispatcher.Invoke((Action)delegate { StatusBox.IsEnabled = true; });
                                            };
            var thread = new Thread(threadStarter);
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMinutes(App.Settings.RefreshInterval);
            timer.Tick += TimerTick;
            timer.Start();
        }

        void TimerTick(object sender, EventArgs e)
        {
            Refresh();
        }

        void WallEntryUpdated(FriendStreamEntry entry)
        {
            foreach (FriendStreamControl item in NewsFeedList.Items)
            {
                if (item.Id == entry.Id)
                {
                    item.Update(entry); //update UI
                    break;
                }
            }
        }

        void WallRefreshFinished(IEnumerable<FriendStreamEntry> newItems)
        {
            var index = newItems.Count();
            foreach (var entry in newItems)
            {
                this.Dispatcher.Invoke((Action)delegate
                {
                    var control = new FriendStreamControl();
                    control.FriendStreamEntry = entry;
                    control.Order = index;

                    NewsFeedList.Items.Insert(0, control);
                    while (NewsFeedList.Items.Count > App.Settings.MaxEntries)
                        NewsFeedList.Items.RemoveAt(NewsFeedList.Items.Count - 1);
                });
                index--;
            }
        }


        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            Activate();
        }

        private void WindowMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !App.Settings.Pin && Mouse.Captured == null)
                DragMove();
        }

        private void WindowMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            App.Settings.Left = this.Left;
            App.Settings.Top = this.Top;
            App.Settings.Save(App.ConfigFile);
        }

        private void ThisMouseEnter(object sender, MouseEventArgs e)
        {
            var mouseEnterAnim = (Storyboard)Resources["MouseEnter"];
            mouseEnterAnim.Begin(this);
        }

        private void ThisMouseLeave(object sender, MouseEventArgs e)
        {
            var mouseLeaveAnim = (Storyboard)Resources["MouseLeave"];
            ((DoubleAnimation)mouseLeaveAnim.Children[0]).To = App.Settings.Opacity;
            mouseLeaveAnim.Begin(this);
        }

        private void PinItemClick(object sender, RoutedEventArgs e)
        {
            App.Settings.Pin = PinItem.IsChecked;
            App.Settings.Save(App.ConfigFile);
        }

        private void TopMostItemClick(object sender, RoutedEventArgs e)
        {
            if (App.Settings.TopMost)
            {
                App.Settings.TopMost = false;
                this.Topmost = false;
            }
            else
            {
                App.Settings.TopMost = true;
                this.Topmost = true;
            }
            App.Settings.Save(App.ConfigFile);
        }

        private void OptionsItemClick(object sender, RoutedEventArgs e)
        {
            if (optionsWindow != null && optionsWindow.IsVisible)
            {
                optionsWindow.Activate();
                return;
            }

            optionsWindow = new Options();
            optionsWindow.UpdateSettings += OptionsWindowUpdateSettings;

            optionsWindow.Width = App.Settings.OptionsWidth;
            optionsWindow.Height = App.Settings.OptionsHeight;

            if (App.Settings.Language == "he-IL" || App.Settings.Language == "ar-SA")
            {
                optionsWindow.FlowDirection = System.Windows.FlowDirection.RightToLeft;
            }
            else
            {
                optionsWindow.FlowDirection = System.Windows.FlowDirection.LeftToRight;
            }

            optionsWindow.ShowDialog();
        }

        void OptionsWindowUpdateSettings(object sender, EventArgs e)
        {
            this.ShowInTaskbar = !App.Settings.UseTrayIcon;
            this.Opacity = App.Settings.Opacity;
            Scale.ScaleX = App.Settings.Scale;

            timer.Stop();
            timer.Interval = TimeSpan.FromMinutes(App.Settings.RefreshInterval);
            timer.Start();

            if (App.Settings.UseAero)
            {
                UpdateAero();
            }
        }

        private void CloseItemClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MouseEnterCompleted(object sender, EventArgs e)
        {
            this.Opacity = 1;
        }

        private void MouseLeaveCompleted(object sender, EventArgs e)
        {
            this.Opacity = App.Settings.Opacity;
        }

        private void UpdateAero()
        {
            double dpiX = 0.0f;
            double dpiY = 0.0f;
            var source = PresentationSource.FromVisual(this);
            if (source != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            var rgn = WinAPI.CreateRoundRectRgn(0, (int)(25 * App.Settings.Scale * dpiY), (int)(this.Width * App.Settings.Scale * dpiX),
                (int)(this.Height * App.Settings.Scale * dpiY), (int)(12 * App.Settings.Scale * dpiX), (int)(dpiY * App.Settings.Scale * 12));
            Dwm.MakeGlassRegion(ref handle, rgn);
        }

        private void RefreshItemClick(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void StatusBoxGotFocus(object sender, RoutedEventArgs e)
        {
            if (StatusBox.Text == Properties.Resources.StatusTip)
                StatusBox.Text = string.Empty;
            else
                StatusBox.SelectAll();
        }

        private void StatusBoxLostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(StatusBox.Text))
            {
                StatusBox.Text = Properties.Resources.StatusTip;
            }
        }

        private void StatusBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var status = StatusBox.Text;
                ThreadStart threadStarter = delegate
                {
                    foreach (var p in App.ProviderManager.Providers)
                    {
                        if (p.IsActive)
                        {
                            p.Post(status);
                        }
                    }

                    Refresh();
                };
                var thread = new Thread(threadStarter);
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                StatusBox.Text = string.Empty;
            }
        }

        private void Refresh()
        {
            RefreshStatusPanel.Dispatcher.Invoke((Action)delegate
            {
                RefreshTime.Text = Properties.Resources.Refreshing;
            });

            StatusBox.Dispatcher.Invoke((Action) delegate { StatusBox.IsEnabled = false; });

            ThreadStart threadStarter = delegate
            {
                wall.Refresh();

                RefreshStatusPanel.Dispatcher.Invoke((Action)delegate
                {
                    RefreshTime.Text = DateTime.Now.ToShortTimeString();
                });

                StatusBox.Dispatcher.Invoke((Action)delegate { StatusBox.IsEnabled = true; });
            };
            var thread = new Thread(threadStarter);
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private void RefreshStatusPanelMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Refresh();
        }

        private void ClearItemClick(object sender, RoutedEventArgs e)
        {
            NewsFeedList.Items.Clear();
            wall.Clear();
        }
    }
}
