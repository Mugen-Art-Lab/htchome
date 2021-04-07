using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Argotic.Common;
using Argotic.Extensions;
using Argotic.Syndication;

namespace News.Domain
{
    public class Feed
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private FeedType type = FeedType.Rss;
        private RssFeed rssFeed;
        private AtomFeed atomFeed;
        private List<FeedItem> items = new List<FeedItem>();

        public string Channel { get; set; }

        public List<FeedItem> Items
        {
            get { return items; }
        }

        public Feed(string url)
        {
            if (url.ToLower().Contains("atom"))
                type = FeedType.Atom;

            if (type == FeedType.Rss)
            {
                rssFeed = RssFeed.Create(new Uri(url));
                Channel = rssFeed.Channel.Title;
                if (rssFeed == null)
                    return;
                foreach (var rssItem in rssFeed.Channel.Items  )
                {
                    var feedItem = new RssFeedItem(rssItem);
                    items.Add(feedItem);
                }
            }
            else
            {
                atomFeed = AtomFeed.Create(new Uri(url));
                Channel = atomFeed.Title.Content;
                if (atomFeed == null)
                    return;
                foreach (var atomEntry in atomFeed.Entries)
                {
                    var feedItem = new AtomFeedItem(atomEntry);
                    items.Add(feedItem);
                }
            }
        }

        public void Clear()
        {
            items.Clear();
        }
    }
}
