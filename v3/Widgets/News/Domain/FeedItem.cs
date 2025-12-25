using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace News.Domain
{
    public abstract class FeedItem
    {
        public abstract string Channel { get; set; }
        public abstract string Title { get; set; }
        public abstract string Description { get; set; }
        public abstract string Url { get; set; }
        public abstract DateTime PublicationDate { get; set; }
    }
}
