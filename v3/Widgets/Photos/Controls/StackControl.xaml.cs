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
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Photos.Windows;

namespace Photos.Controls
{
    /// <summary>
    /// Interaction logic for StockControl.xaml
    /// </summary>
    public partial class StackControl : UserControl
    {
        private bool isAnim;
        private Random r;
        private int direction; //0 - forward, 1 - backward
        private string nextPhoto;
        private Stack<string> history;

        public string CurrentPicturePath
        {
            get
            {
                var currentTopPhoto = (PhotoControl)Root.Children[Root.Children.Count - 1];
                var imageSource = (BitmapImage)currentTopPhoto.Image.Source;
                if (imageSource != null)
                    return imageSource.UriSource.OriginalString;
                return string.Empty;
            }
        }

        public StackControl()
        {
            InitializeComponent();

            r = new Random(Environment.TickCount);
            history = new Stack<string>();

            //Width = App.Settings.Scale * App.Settings.MaxSize;
            //Height = Width;
        }

        private void UserControlMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (isAnim)
                return;

            if (e.Delta > 0)

                MoveForward();
            else
                MoveBackward();
        }

        public void MoveForward()
        {
            //    if (!string.IsNullOrEmpty(nextPhoto))
            //        App.History.Enqueue(nextPhoto);

            if (App.Pics.Count > 0 && App.Settings.SwitchRandom)
            {
                var currentTopPhoto = (PhotoControl)Root.Children[Root.Children.Count - 1];
                var imageSource = (BitmapImage)currentTopPhoto.Image.Source;
                if (imageSource != null)
                    history.Push(imageSource.UriSource.OriginalString);
            }

            if (direction == 1)
                App.Index += 2;
            else
                App.Index++;

            if (App.Index >= App.Pics.Count)
                App.Index = 0;
            direction = 0;

            var s = (Storyboard)Resources["TopPhotoAnimForward"];

            var topPhoto = (PhotoControl)Root.Children[Root.Children.Count - 1];

            s.Begin(topPhoto);

            s = (Storyboard)Resources["BottomPhotoAnimForward"];

            var bottomPhoto = (PhotoControl)Root.Children[0];

            s.Begin(bottomPhoto);
        }


        public void MoveBackward()
        {
            string pic;

            if (!App.Settings.SwitchRandom)
            {
                if (direction == 0)
                    App.Index -= 2;
                else
                    App.Index--;

                if (App.Index < 0)
                    App.Index = App.Pics.Count - 1;
                pic = App.Pics[App.Index];
            }
            else
            {
                if (history.Count > 0)
                    pic = history.Pop();
                else
                    return;
            }


            direction = 1;

            var s = (Storyboard)Resources["BottomPhotoAnimBackward"];

            var bottomPhoto = (PhotoControl)Root.Children[Root.Children.Count - 1];

            s.Begin(bottomPhoto);

            if (App.Pics.Count > 0)
                AddNewPhoto(true, pic);
            else
                AddNewPhoto(true);


            s = (Storyboard)Resources["TopPhotoAnimBackward"];

            var topPhoto = (PhotoControl)Root.Children[Root.Children.Count - 1];

            s.Begin(topPhoto);

            s = (Storyboard)Resources["FadeOutAnim"];

            var bgPhoto = (PhotoControl)Root.Children[0];

            s.Begin(bgPhoto);
        }

        public void AddNewPhoto(bool atTop, string file = null)
        {
            //remove previous top photo, previous bottom photo will become top
            var newPhoto = new PhotoControl(); //now we need new bottom photo
            newPhoto.Opacity = 0;
            var bi = new BitmapImage();
            bi.BeginInit();
            if (App.Pics.Count == 0 || file == null)
                bi.UriSource = new Uri("/Photos;component/Resources/preview_portrait.png", UriKind.Relative);
            else
            {
                bi.UriSource = new Uri(file, UriKind.RelativeOrAbsolute);
            }
            bi.DecodePixelWidth = (int)(App.Settings.Scale * App.Settings.MaxSize);
            bi.EndInit();

            newPhoto.Image.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.SystemIdle, (ThreadStart)delegate()
            {
                newPhoto.Image.Source = bi;

                if (newPhoto.Image.Source.Height > newPhoto.Image.Source.Width) //portrait photo
                {
                    newPhoto.Height = App.Settings.Scale * App.Settings.MaxSize;
                    Canvas.SetLeft(newPhoto, 50);
                }
                else
                {
                    newPhoto.Width = App.Settings.Scale * App.Settings.MaxSize;
                    Canvas.SetTop(newPhoto, 25);
                }

                var s = (Storyboard)Resources["FadeInAnim"];

                s.Begin(newPhoto);

            });

            if (!atTop)
            {
                newPhoto.Rotation.Angle = 10;
            }
            //newPhoto.Opacity = 0;
            newPhoto.HorizontalAlignment = HorizontalAlignment.Left;
            newPhoto.VerticalAlignment = VerticalAlignment.Top;
            //newBottomPhoto.MaxWidth = 300;
            //newBottomPhoto.MaxHeight = 300;);
            if (!atTop)
                Root.Children.Insert(0, newPhoto);
            else
                Root.Children.Add(newPhoto);

        }

        public void Resize()
        {
            foreach (PhotoControl p in Root.Children)
            {
                if (p.Image.Source.Height > p.Image.Source.Width) //portrait photo
                {
                    p.Height = App.Settings.Scale * App.Settings.MaxSize;
                }
                else
                {
                    p.Width = App.Settings.Scale * App.Settings.MaxSize;
                }
            }
        }

        private void StoryboardCompleted(object sender, EventArgs e)
        {
            Root.Children.RemoveAt(Root.Children.Count - 1);
            if (App.Pics.Count > 0)
                if (App.Settings.SwitchRandom)
                {
                    App.Index = r.Next(App.Pics.Count);
                    var p = App.Pics[App.Index];
                    if (!history.Contains(p))
                        nextPhoto = p;
                    //    App.History.Enqueue(p);
                    AddNewPhoto(false, p);
                }
                else
                {
                    var p = App.Pics[App.Index];

                    AddNewPhoto(false, App.Pics[App.Index]);
                }
            else
            {
                AddNewPhoto(false);
            }
        }

        private void StoryboardCurrentStateInvalidated(object sender, EventArgs e)
        {
            if (((Clock)sender).CurrentState == ClockState.Active)
                isAnim = true;
            else
            {
                isAnim = false;
            }
        }

        private void FadeOutCompleted(object sender, EventArgs e)
        {
            Root.Children.RemoveAt(0);
        }
    }
}
