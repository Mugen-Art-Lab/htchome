using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Facebook;
using Social.Base;

namespace FacebookProvider
{
    public class SocialProvider : ISocialProvider
    {
        private const string AppId = "233613346655758";
        private const string PostUrl = "http://www.facebook.com/{0}/posts/{1}";
        public static readonly string[] Permissions = new[] { "publish_stream", "read_stream" };

        private FacebookOAuthResult oauthResult;

        public event EventHandler SignedIn;

        public string GetName()
        {
            return "Facebook";
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

            IDictionary<string, object> loginParameters = new Dictionary<string, object>
                                                              {
                                                                  { "response_type", "token" },
                                                                  { "display", "popup" }
                                                              };
            browser.Navigate(FacebookOAuthClient.GetLoginUrl(AppId, null, Permissions, false, loginParameters));
            form.Controls.Add(browser);
            form.FormBorderStyle = FormBorderStyle.None;
            form.Opacity = 0;
            form.ShowInTaskbar = false;
            form.ShowDialog();
        }

        void BrowserNavigated(object sender, WebBrowserNavigatedEventArgs e)
        {
            if (FacebookOAuthResult.TryParse(e.Url, out oauthResult))
            {
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
            var client = new FacebookClient(oauthResult.AccessToken);

            dynamic r = client.Get("/me/home");
            var data = (IList<object>)r.data;
            if (data == null)
                return null;
            var result = new List<FriendStreamEntry>();

            foreach (IDictionary<string, object> o in data)
            {
                var entry = new FriendStreamEntry();
                entry.Provider = "Facebook";
                entry.Id = (string)o["id"];
                var from = (IDictionary<string, object>)o["from"];
                entry.FromName = (string)from["name"];
                entry.FromId = (string)from["id"];
                entry.Url = string.Format(PostUrl, entry.FromId, entry.Id.Split('_')[1]);
                if (o.ContainsKey("message"))
                    entry.Message = (string)o["message"];
                entry.UserPic = string.Format("http://graph.facebook.com/{0}/picture?type=square", entry.FromId);

                if (o.ContainsKey("description"))
                    entry.Description = (string)o["description"];
                if (o.ContainsKey("name"))
                    entry.Name = (string)o["name"];
                if (o.ContainsKey("picture"))
                    entry.Picture = (string)o["picture"];
                entry.CreatedTime = DateTime.Parse((string)o["created_time"]);
                entry.UpdatedTime = DateTime.Parse((string)o["updated_time"]);
                if (o.ContainsKey("comments"))
                {
                    var comments = (IDictionary<string, object>)o["comments"];
                    var count = Convert.ToInt32(comments["count"]);
                    entry.Comments = count;
                }
                if (o.ContainsKey("likes"))
                {
                    var likes = (IDictionary<string, object>)o["likes"];
                    var count = Convert.ToInt32(likes["count"]);
                    entry.Likes = count;
                }

                result.Add(entry);
            }

            return result;
        }

        public void Post(string status)
        {
            var client = new FacebookClient(oauthResult.AccessToken);
            client.Post("/me/feed", new Dictionary<string, object> { { "message", status } });
        }
    }
}
