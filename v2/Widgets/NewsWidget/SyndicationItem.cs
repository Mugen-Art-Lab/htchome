using System;

namespace NewsWidget
{
    public class SyndicationItem
    {
        public string Title { get; set; }

        public string Description { get; set; }

        public DateTime PublicationDate { get; set; }

        public Uri Link { get; set; }

        public string Source { get; set; }
    }
}
