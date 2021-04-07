using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using Social.Base;
using Vkontakte.VkApi;

namespace Vkontakte
{
    public class SocialProvider : ISocialProvider
    {
        private const string app_id = "1964576";
        private const string PostUrl = "http://vkontakte.ru/wall{0}_{1}";
        private const string PhotoUrl = "http://vkontakte.ru/photo{0}_{1}";
        private string access_token;

        private const string loginUrl =
            "http://api.vkontakte.ru/oauth/authorize?client_id={0}&scope={1}&redirect_uri=api.vkontakte.ru/blank.html&display=popup&response_type=token";

        public string GetName()
        {
            return "Вконтакте";
        }

        public void SignIn()
        {
            var form = new Form();
            form.Width = 640;
            form.Height = 480;
            form.StartPosition = FormStartPosition.CenterScreen;

            var browser = new WebBrowser();
            browser.Dock = DockStyle.Fill;
            browser.Navigated += BrowserNavigated;

            browser.Navigate(string.Format(loginUrl, app_id, "8192"));
            form.Controls.Add(browser);
            form.FormBorderStyle = FormBorderStyle.None;
            form.Opacity = 0;
            form.ShowInTaskbar = false;
            form.ShowDialog();
        }

        void BrowserNavigated(object sender, WebBrowserNavigatedEventArgs e)
        {
            if (e.Url.OriginalString.Contains("access_token="))
            {
                access_token = e.Url.OriginalString.Substring(e.Url.OriginalString.IndexOf("=") + 1,
                                                              e.Url.OriginalString.IndexOf("&") -
                                                              e.Url.OriginalString.IndexOf("=") - 1);
                if (SignedIn != null)
                    SignedIn(this, EventArgs.Empty);
                ((Form)(((WebBrowser)sender).Parent)).Close();
            }
            else
            {
                var form = ((Form)(((WebBrowser)sender).Parent));
                form.FormBorderStyle = FormBorderStyle.Sizable;
                form.Opacity = 1;
            }
        }

        public List<FriendStreamEntry> GetFriendStream()
        {
            var response = VkClient.InvokeMethod("newsfeed.get.xml", new List<string>() { "count=20" }, access_token);
            var xml = XElement.Parse(response);
            var result = new List<FriendStreamEntry>();
            var profiles = new List<VkProfile>();
            var groups = new List<VkProfile>();

            foreach (var user in xml.Descendants("user"))
            {
                var profile = new VkProfile();
                profile.Uid = user.Element("uid").Value;
                profile.Name = user.Element("first_name").Value + " " + user.Element("last_name").Value;
                profile.Photo = user.Element("photo").Value;
                profiles.Add(profile);
            }

            foreach (var group in xml.Descendants("group"))
            {
                var profile = new VkProfile();
                profile.Uid = group.Element("gid").Value;
                profile.Name = group.Element("name").Value;
                profile.Photo = group.Element("photo").Value;
                groups.Add(profile);
            }

            foreach (var item in xml.Descendants("item"))
            {
                var type = item.Element("type").Value;
                if (type == "friend" || type == "photo_tag" || type == "note")
                    continue;
                var entry = new FriendStreamEntry();
                entry.Provider = "Vkontakte";
                entry.Type = FriendStreamEntryType.Status;
                entry.FromId = item.Element("source_id").Value;

                if (item.Element("post_id") != null)
                {
                    entry.Id = item.Element("post_id").Value;
                }

                entry.Url = string.Format(PostUrl, entry.FromId, entry.Id);

                if (item.Element("photos") != null)
                {
                    var photo = item.Element("photos").Element("photo");
                    if (photo != null)
                    {
                        entry.Picture = photo.Element("src").Value;
                        var pid = photo.Element("pid").Value;
                        entry.Url = string.Format(PhotoUrl, entry.FromId, pid);
                    }
                }

                VkProfile matchProfile;
                if (entry.FromId.StartsWith("-"))
                {
                    entry.FromId = entry.FromId.Substring(1);
                    matchProfile = groups.Find(x => x.Uid == entry.FromId);
                }
                else
                    matchProfile = profiles.Find(x => x.Uid == entry.FromId);
                entry.FromName = Helper.StripTags(matchProfile.Name);
                entry.UserPic = matchProfile.Photo;
                if (item.Element("text") != null)
                    entry.Message = Helper.StripTags(item.Element("text").Value);

                if (item.Element("likes") != null)
                {
                    entry.Likes = Convert.ToInt32(item.Element("likes").Element("count").Value);
                }

                if (item.Element("comments") != null)
                {
                    entry.Comments = Convert.ToInt32(item.Element("comments").Element("count").Value);
                }

                entry.CreatedTime = ConvertFromUnixTimestamp(double.Parse(item.Element("date").Value)).ToLocalTime();
                result.Add(entry);
            }
            return result;
        }

        public void Post(string status)
        {
            VkClient.InvokeMethod("wall.post.xml", new List<string>() { "message=" + status }, access_token);
        }

        static DateTime ConvertFromUnixTimestamp(double timestamp)
        {
            var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return origin.AddSeconds(timestamp);
        }

        public event EventHandler SignedIn;
    }
}
