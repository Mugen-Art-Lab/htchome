using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Argotic.Syndication;

namespace News.Domain
{
    public class RssFeedItem : FeedItem
    {
        public override string Channel { get; set; }
        public override sealed string Title { get; set; }
        public override sealed string Description { get; set; }
        public override sealed string Url { get; set; }
        public override sealed DateTime PublicationDate { get; set; }

        public RssFeedItem(RssItem originalItem)
        {
            //Channel = originalItem.Source.Title;
            Title = originalItem.Title;
            Description = originalItem.Description;
            Url = originalItem.Link.OriginalString;
            PublicationDate = originalItem.PublicationDate;
        }
    }
}
