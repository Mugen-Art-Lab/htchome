using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HTCHome.Core;
using System.Xml.Linq;
using System.Threading;
using System.Windows;

namespace NewsWidget
{
    public class Source
    {
        public string Url;

        public DateTime lastFeedDate;

        public string Title;

        public List<Feed> feeds = new List<Feed>();

        public event EventHandler GetNewsFinished;

        public void GetNews()
        {
            feeds.Clear();
            ThreadStart threadStarter = delegate
            {
                string s = GeneralHelper.GetXml(Url);
                if (String.IsNullOrEmpty(s))
                {
                    return;
                }

                XDocument doc = XDocument.Parse(s);
                //MessageBox.Show(doc.Root.Element("channel").Value);
                Title = /*doc.Root.Descendants("channel").First().Element("title").Value;*/ doc.Descendants("title").FirstOrDefault().Value;
                var news = from x in doc.Descendants("item")
                           select new
                           {
                               title = x.Element("title").Value,
                               description = x.Element("description").Value,
                               link = x.Element("link").Value,
                               date = x.Element("pubDate").Value
                           };
                int count = 0;
                foreach (var n in news)
                {
                    if (count < Widget.Sett.newsCount)
                    {
                        Feed f = new Feed();
                        f.Title = n.title;
                        f.Description = n.description;
                        f.Link = n.link;

                        string date = ProcessTimeZones(n.date);
                        DateTime d = DateTime.Parse(date);
                        if (DateTime.TryParse(date, out d))
                        {
                            f.PubDate = d;
                        }

                        feeds.Add(f);
                    }
                    count++;
                }

                GetNewsFinished(this, EventArgs.Empty);
            };
            var thread = new Thread(threadStarter);
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

        }

        private string ProcessTimeZones(string datestring)
        {
            if (datestring.Contains(" GMT"))
                return datestring.Replace(" GMT", "");
            if (datestring.Contains(" EDT"))
                return datestring.Replace(" EDT", "+04:00");
            if (datestring.Contains(" EST"))
                return datestring.Replace(" EST", "+05:00");
            if (datestring.Contains(" CDT"))
                return datestring.Replace(" CDT", "+05:00");
            if (datestring.Contains(" CST"))
                return datestring.Replace(" CST", "+06:00");
            if (datestring.Contains(" MDT"))
                return datestring.Replace(" MDT", "+06:00");
            if (datestring.Contains(" MST"))
                return datestring.Replace(" MST", "+07:00");
            if (datestring.Contains(" PDT"))
                return datestring.Replace(" PDT", "+07:00");
            if (datestring.Contains(" PST"))
                return datestring.Replace(" PST", "+08:00");
            return datestring;
        }
    }
}
