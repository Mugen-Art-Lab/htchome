using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Argotic.Syndication;

namespace News.Domain
{
    public class AtomFeedItem : FeedItem
    {
        public override sealed string Channel { get; set; }

        public override sealed string Title { get; set; }

        public override sealed string Description { get; set; }

        public override sealed string Url { get; set; }

        public override sealed DateTime PublicationDate { get; set; }

        public AtomFeedItem(AtomEntry entry)
        {
            Title = entry.Title.Content;
            if (entry.Source != null)
                Channel = entry.Source.Title.Content;
            if (entry.Content != null)
                Description = entry.Content.Content;
            Url = entry.Links.FirstOrDefault()?.Uri?.OriginalString;
            PublicationDate = entry.PublishedOn;
        }
    }
}
