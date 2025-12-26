using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Xsl;
using Argotic.Syndication;
using System.Threading;

namespace NewsWidget
{
    public class Source
    {
        public string Url { get; set; }

        //private DateTimeOffset _lastFeedDate;
        private DateTime _lastFeedDate;

        public string Title;

        public event RefreshFinishedDelegate RefreshFinished;
        public delegate void RefreshFinishedDelegate(Source sender, IEnumerable<SyndicationItem> newItems);

        //public SyndicationFeed Feed;

        //public List<Feed> Items { get; private set; }


        public void Refresh()
        {
            ThreadStart threadStarter = delegate
                                            {
                                                try
                                                {
                                                    var uri = new Uri(Url);
                                                    List<SyndicationItem> newItems;
                                                    if (Url.EndsWith(".atom"))
                                                    {
                                                        var atomFeed = AtomFeed.Create(uri);
                                                        Title = atomFeed.Title.Content;
                                                        newItems =
                                                            (from x in atomFeed.Entries
                                                             where (x.PublishedOn.CompareTo(_lastFeedDate) == 1 || x.PublishedOn.CompareTo(_lastFeedDate) == 0)
                                                             select new SyndicationItem
                                                             {
                                                                 Title = x.Title.Content,
                                                                 Description = x.Content.Content,
                                                                 PublicationDate = x.UpdatedOn,
                                                                 Link = x.Links.FirstOrDefault()?.Uri
                                                             }).ToList();
                                                    } 
                                                    else
                                                    {
                                                        var feed = RssFeed.Create(uri);
                                                        Title = feed.Channel.Title;
                                                        newItems =
                                                        (from x in feed.Channel.Items
                                                         where (x.PublicationDate.CompareTo(_lastFeedDate) == 1 || x.PublicationDate.CompareTo(_lastFeedDate) == 0)
                                                         select new SyndicationItem
                                                         {
                                                             Title = x.Title,
                                                             Description = x.Description,
                                                             PublicationDate = x.PublicationDate,
                                                             Link = x.Link
                                                         }).ToList();
                                                    }
                                                    if (newItems.Count > 0)
                                                    {
                                                        foreach (var newItem in newItems)
                                                        {
                                                            if (newItem.PublicationDate.CompareTo(_lastFeedDate) == 1)
                                                                _lastFeedDate = newItem.PublicationDate;
                                                            newItem.Source = Title;
                                                        }
                                                    }
                                                    RefreshFinished(this, newItems);
                                                } 
                                                catch (Exception ex)
                                                {
                                                    HTCHome.Core.Logger.Log(ex.ToString());
                                                    RefreshFinished(this, null);
                                                }
                                                //XmlTextReader reader = null;
                                                //try
                                                //{
                                                //    string xml = GeneralHelper.GetXml(Url);
                                                //    xml = RemoveEmptyTags(xml);
                                                //    string[] s = xml.Split(new string[] { "\n" }, StringSplitOptions.None);
                                                //    reader = new XmlTextReader(xml, XmlNodeType.Document, null);
                                                //    Feed = SyndicationFeed.Load(reader);
                                                //}
                                                //catch (Exception ex)
                                                //{
                                                //    HTCHome.Core.Logger.Log("Error while getting news. URL: " + Url + "\n" + ex.ToString());
                                                //    RefreshFinished(this, null);
                                                //}
                                                //if (reader != null)
                                                //    reader.Close();
                                                //if (Feed != null)
                                                //{
                                                //    Title = Feed.Title.Text;
                                                //    List<SyndicationItem> newItems = (from x in Feed.Items
                                                //                                      where x.PublishDate.CompareTo(_lastFeedDate) == 1
                                                //                                      select x).ToList();
                                                //    if (newItems.Count > 0)
                                                //    {
                                                //        foreach (var syndicationItem in newItems)
                                                //        {
                                                //            if (syndicationItem.PublishDate.CompareTo(_lastFeedDate) == 1)
                                                //                _lastFeedDate = syndicationItem.PublishDate;
                                                //            syndicationItem.SourceFeed = new SyndicationFeed(Feed.Title.Text, null, null);
                                                //        }
                                                //    }
                                                //    RefreshFinished(this, newItems);
                                                //}
                                                //else
                                                //    RefreshFinished(this, null);
                                            };
            var thread = new Thread(threadStarter);
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        public void Clear()
        {
            //_lastFeedDate = new DateTimeOffset();
            _lastFeedDate = new DateTime();
        }

        public static string RemoveEmptyTags(string sXML)
        {
            var sb = new StringBuilder();

            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.Append("<xsl:stylesheet ");
            sb.Append("     version=\"1.0\" ");
            sb.Append("     xmlns:msxsl=\"urn:schemas-microsoft-com:xslt\"");
            sb.Append("     xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\">");
            sb.Append("     <xsl:output method=\"xml\" version=\"1.0\" encoding=\"UTF-8\"/>");
            sb.Append("   <!-- Whenever you match any node or any attribute -->");
            sb.Append("   <xsl:template match=\"node()|@*\">");
            sb.Append("      <!-- Copy the current node -->");
            sb.Append("     <xsl:if test=\"normalize-space(.) != '' or normalize-space(./@*) != '' \">");
            sb.Append("          <xsl:copy>");
            sb.Append("              <!-- Including any attributes it has and any child nodes -->");
            sb.Append("               <xsl:apply-templates select=\"@*|node()\"/>");
            sb.Append("          </xsl:copy>");
            sb.Append("     </xsl:if>");
            sb.Append("   </xsl:template>");
            sb.Append("</xsl:stylesheet>");
            return transXMLStringThroughXSLTString(sXML, sb.ToString());
        }




        private static string transXMLStringThroughXSLTString(string sXML, string sXSLT)
        {
            //This is the logic of the application.
            XslCompiledTransform objTransform = new XslCompiledTransform();

            StringReader xmlStream = new StringReader(sXML);
            XmlReader xmlReader = new XmlTextReader(xmlStream);


            StringReader stream = new StringReader(sXSLT);
            XmlReader xmlReaderXslt = new XmlTextReader(stream);

            objTransform.Load(xmlReaderXslt, null, null);

            StringWriter objStream = new StringWriter();
            objTransform.Transform(xmlReader, null, objStream);

            return objStream.ToString().Replace(@"encoding=""utf-16""?>", @"encoding=""utf-8""?>");
        }
    }
}
