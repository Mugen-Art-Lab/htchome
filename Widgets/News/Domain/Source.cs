using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace News.Domain
{
    public class Source
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public string Url { get; set; }
        public bool IsEnabled { get; set; }
        public string Title { get; set; }

        private Feed feed;
        private DateTime lastFeedDate;

        public IEnumerable<FeedItem> Items
        {
            get { return feed.Items; }
        }

        public Source(string url)
        {
            Url = url;
        }

        public List<FeedItem> Refresh()
        {
            try
            {
                feed = new Feed(Url);

                //Title = feed.Channel;
                    List<FeedItem> newItems =
                        (from x in feed.Items
                         where x.PublicationDate.CompareTo(lastFeedDate) == 1
                         select x).ToList();
                    if (newItems.Count > 0)
                    {
                        foreach (var newItem in newItems)
                        {
                            if (newItem.PublicationDate.CompareTo(lastFeedDate) == 1)
                                lastFeedDate = newItem.PublicationDate;
                            newItem.Channel = feed.Channel;
                            //newItem. = new RssSource(new Uri("http://www.engadget.com/rss.xml"), Title);
                        }
                    }
                    return newItems;
            }
            catch (Exception ex)
            {
                logger.Error("An error occured during refreshing news feed " + Url + ".\n" + ex);
            }

            return null;
        }

        public void Clear()
        {
            lastFeedDate = new DateTime(0);
            if (feed != null)
                feed.Clear();
        }
    }
}
