using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using HTCHome.Core;
using System.Xml.Linq;
using System.Threading;
using System.Windows;

namespace NewsWidget
{
    public class Source
    {
        public string Url { get; set; }

        public DateTime lastFeedDate;

        public string Title;

        public event EventHandler GetNewsFinished;

        public SyndicationFeed Feed { get; private set; }

        public void GetNews()
        {
            ThreadStart threadStarter = delegate
            {
                XmlReader reader = XmlReader.Create(Url);
                Feed = SyndicationFeed.Load(reader);
                reader.Close();
                GetNewsFinished(this, EventArgs.Empty);
                //string s = GeneralHelper.GetXml(Url);
                //if (String.IsNullOrEmpty(s))
                //{
                //    return;
                //}

                //XDocument doc = XDocument.Parse(s);
                ////MessageBox.Show(doc.Root.Element("channel").Value);
                //Title = /*doc.Root.Descendants("channel").First().Element("title").Value;*/ doc.Descendants("title").FirstOrDefault().Value;
                //var news = from x in doc.Descendants("item")
                //            select new
                //            {
                //                title = x.Element("title").Value,
                //                description = x.Element("description").Value,
                //                link = x.Element("link").Value,
                //                date = x.Element("pubDate").Value
                //            };
                //int count = 0;
                //foreach (var n in news)
                //{
                //    if (count < Widget.Sett.newsCount)
                //    {
                //        Feed f = new Feed();
                //        f.Title = n.title;
                //        f.Description = n.description;
                //        f.Link = n.link;

                //        string date = ProcessTimeZones(n.date);
                //        DateTime d = DateTime.Parse(date);
                //        if (DateTime.TryParse(date, out d))
                //        {
                //            f.PubDate = d;
                //        }

                //        feeds.Add(f);
                //    }
                //    count++;
                //}

                //GetNewsFinished(this, EventArgs.Empty);
            };
            var thread = new Thread(threadStarter);
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
    }
}
