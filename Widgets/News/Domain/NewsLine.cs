using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Argotic.Syndication;

namespace News.Domain
{
    public class NewsLine
    {
        private readonly List<Source> sources;

        public delegate void RefreshFinishedHandler(IEnumerable<FeedItem> newItems);
        public event RefreshFinishedHandler RefreshFinished;

        public List<Source> Sources
        {
            get { return sources; }
        }

        public List<FeedItem> Items { get; set; }

        private DateTime lastFeedDate;

        public NewsLine()
        {
            sources = new List<Source>();
            Items = new List<FeedItem>();
        }

        public void AddSource(string url)
        {
            var source = new Source(url);
            sources.Add(source);
        }

        public void RemoveSource(string url)
        {
            var index = sources.FindIndex(x => x.Url == url);
            if (index >= 0)
                sources.RemoveAt(index);
        }

        public void Refresh()
        {
            ThreadStart threadStarter = delegate
                                            {
                                                foreach (var source in sources)
                                                {
                                                    var newItems = source.Refresh();
                                                    if (newItems != null)
                                                    {
                                                        Items.AddRange(newItems);
                                                    }
                                                }

                                                Items = Items.OrderByDescending(x => x.PublicationDate).ToList();
                                                if (Items.Count > App.Settings.MaxItems)
                                                {
                                                    //remove some 
                                                    Items.RemoveRange(App.Settings.MaxItems, Items.Count - App.Settings.MaxItems);
                                                }

                                                //make list with new items that was added since last feed was published
                                                var items = (from x in Items
                                                                       where x.PublicationDate.CompareTo(lastFeedDate) == 1
                                                                       select x).Reverse().ToList();
                                                if (items.Count > 0)
                                                {
                                                    foreach (var syndicationItem in items)
                                                    {
                                                        if (syndicationItem.PublicationDate.CompareTo(lastFeedDate) == 1)
                                                            lastFeedDate = syndicationItem.PublicationDate;
                                                    }
                                                }

                                                if (RefreshFinished != null)
                                                    RefreshFinished(items);
                                            };
            var thread = new Thread(threadStarter);
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        public void Clear()
        {
            lastFeedDate = new DateTime(0);
            Items.Clear();
            foreach (var source in sources)
            {
                source.Clear();
            }
        }
    }
}
