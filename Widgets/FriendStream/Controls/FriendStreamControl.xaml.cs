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
using Social.Base;

namespace FriendStream.Controls
{
    /// <summary>
    /// Interaction logic for FriendStreamControl.xaml
    /// </summary>
    public partial class FriendStreamControl : UserControl
    {
        private FriendStreamEntry entry;

        public string Id { get; set; }

        public FriendStreamEntry FriendStreamEntry
        {
            get { return entry; }
            set
            {
                entry = value;
                Id = entry.Id;

                FeedImage.Source = new BitmapImage(new Uri(entry.UserPic));
                UsernameText.Text = entry.FromName;
                MessageText.Text = entry.Message;

                NameText.Text = entry.Name;
                DescriptionText.Text = entry.Description;
                if (!string.IsNullOrEmpty(entry.Description))
                {
                    NamePanel.Visibility = System.Windows.Visibility.Visible;
                    DescriptionText.Visibility = System.Windows.Visibility.Visible;
                }

                if (!string.IsNullOrEmpty(entry.Picture))
                {
                    NamePanel.Visibility = System.Windows.Visibility.Visible;
                    NamePicture.Source = new BitmapImage(new Uri(entry.Picture));
                    NamePicture.Visibility = System.Windows.Visibility.Visible;
                }

                if (entry.CreatedTime.ToLocalTime().Day == DateTime.Now.Day)
                    DateBox.Text = entry.CreatedTime.ToLocalTime().ToShortTimeString();
                else
                    DateBox.Text = entry.CreatedTime.ToLocalTime().ToString("d MMMM hh:mm tt");
                if (entry.Comments > 0)
                {
                    CommentsIcon.Visibility = System.Windows.Visibility.Visible;
                    CommentsBox.Visibility = System.Windows.Visibility.Visible;
                    if (entry.Comments == 1)
                        CommentsBox.Text = entry.Comments + " " + Properties.Resources.Comment;
                    else
                        CommentsBox.Text = entry.Comments + " " + Properties.Resources.Comments;
                }

                if (entry.Likes > 0)
                {
                    LikesIcon.Visibility = System.Windows.Visibility.Visible;
                    LikesBox.Visibility = System.Windows.Visibility.Visible;
                    LikesBox.Text = entry.Likes + " " + Properties.Resources.Likes;
                }

                switch (entry.Provider)
                {
                    case "Facebook":
                        break;
                    case "Vkontakte":
                        OverlayIcon.Source = new BitmapImage(new Uri("/Resources/vk_overlay.png", UriKind.Relative));
                        break;
                }
            }
        }

        public ImageSource Image
        {
            get { return FeedImage.Source; }
            set { FeedImage.Source = value; }
        }

        public string Username
        {
            get { return UsernameText.Text; }
            set { UsernameText.Text = value; }
        }

        public string Message
        {
            get { return MessageText.Text; }
            set { MessageText.Text = value; }
        }

        private int order = 0;
        public int Order
        {
            get { return order; }
            set
            {
                order = value;
                TranslateAnim.BeginTime = TimeSpan.FromMilliseconds(550 + 150 * value);
                OpacityAnim.BeginTime = TimeSpan.FromMilliseconds(550 + 150 * value);
            }
        }

        public FriendStreamControl()
        {
            InitializeComponent();
        }

        public void Update(FriendStreamEntry newEntry)
        {
            entry = newEntry;
            this.Dispatcher.Invoke((Action) delegate
                                                {
                                                    if (entry.Comments > 0)
                                                    {
                                                        CommentsIcon.Visibility = System.Windows.Visibility.Visible;
                                                        CommentsBox.Visibility = System.Windows.Visibility.Visible;
                                                        if (entry.Comments == 1)
                                                            CommentsBox.Text = entry.Comments + " " + Properties.Resources.Comment;
                                                        else
                                                            CommentsBox.Text = entry.Comments + " " + Properties.Resources.Comments;
                                                    }
                                                    else
                                                    {
                                                        CommentsIcon.Visibility = System.Windows.Visibility.Collapsed;
                                                        CommentsBox.Visibility = System.Windows.Visibility.Collapsed;
                                                    }

                                                    if (entry.Likes > 0)
                                                    {
                                                        LikesIcon.Visibility = System.Windows.Visibility.Visible;
                                                        LikesBox.Visibility = System.Windows.Visibility.Visible;
                                                        LikesBox.Text = entry.Likes + " " + Properties.Resources.Likes;
                                                    }
                                                    else
                                                    {
                                                        LikesIcon.Visibility = System.Windows.Visibility.Collapsed;
                                                        LikesBox.Visibility = System.Windows.Visibility.Collapsed;
                                                    }
                                                });
        }

        private void UserControlMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            WinAPI.ShellExecute(IntPtr.Zero, "open", entry.Url, string.Empty, string.Empty, 0);
        }
    }
}
