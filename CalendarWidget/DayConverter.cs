using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using E = HTCHome.Core.Environment;
using System.IO;
using System.Xml.Serialization;
using System.Xml.Linq;

namespace CalendarWidget
{
    public class DayConverter : IValueConverter
    {
        public static Dictionary<DateTime, CalendarEvent> dict = new Dictionary<DateTime, CalendarEvent>();

        static DayConverter()
        {

        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var dates = from x in DayConverter.dict.Keys
                        where ((x.Year == ((DateTime)value).Year) && (x.Month == ((DateTime)value).Month) && (x.Day == ((DateTime)value).Day))
                        select x;
            if (dates.Count() > 0)
                return true;
            else
                return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }

        public static void Read(string path)
        {
            if (File.Exists(path))
            {
                dict.Clear();
                XDocument doc = XDocument.Load(path);
                foreach (XElement el in doc.Root.Elements())
                {
                    CalendarEvent ev = new CalendarEvent();
                    ev.Title = el.Value;
                    ev.Url = el.Attribute("url").Value;
                    ev.StartTime = DateTime.Parse(el.Attribute("start").Value);
                    ev.EndTime = DateTime.Parse(el.Attribute("end").Value);
                    dict.Add(DateTime.Parse(el.Attribute("date").Value), ev);
                }
            }
        }

        public static void Write(string path)
        {
            XDocument doc = new XDocument(new XDocument(
                                           new XDeclaration("1.0", "utf-8", "yes")));

            XElement root = new XElement("Calendar",
                from keyValue in dict
                select new XElement("Item",
                    new XAttribute("date", keyValue.Key.ToString()), new XAttribute("url", keyValue.Value.Url),
                    new XAttribute("start", keyValue.Value.StartTime.ToShortTimeString()), new XAttribute("end", keyValue.Value.EndTime.ToShortTimeString()), keyValue.Value.Title)
                );

            doc.Add(root);
            doc.Save(path);
        }
    }
}
