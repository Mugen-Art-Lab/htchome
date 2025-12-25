using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;

namespace News
{
    public class Utilities
    {
        public static string StripTags(string input)
        {
            input = HttpUtility.HtmlDecode(input);
            input = RemoveComments(input);
            string r = StripTagsCharArray(input); // regHtml.Replace(input, string.Empty);
            while (r.Contains("\n"))
                r = r.Replace("\n", string.Empty);
            while (r.Contains("\t"))
                r = r.Replace("\t", string.Empty);
            while (r.Contains("\r"))
                r = r.Replace("\r", string.Empty);
            return r;
        }

        /// <summary>
        /// Remove HTML tags from string using char array.
        /// </summary>
        public static string StripTagsCharArray(string source)
        {
            char[] array = new char[source.Length];
            int arrayIndex = 0;
            bool inside = false;

            for (int i = 0; i < source.Length; i++)
            {
                char let = source[i];
                if (let == '<')
                {
                    inside = true;
                    continue;
                }
                if (let == '>')
                {
                    inside = false;
                    continue;
                }
                if (!inside)
                {
                    array[arrayIndex] = let;
                    arrayIndex++;
                }
            }
            return new string(array, 0, arrayIndex);
        }

        public static string GetFeedPciture(string s)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(s);
            var linkNodes = doc.DocumentNode.SelectNodes("//img/@src");

            // Run only if there are links in the document.
            if (linkNodes != null)
            {
                return linkNodes.Select(linkNode => linkNode.Attributes["src"]).Select(attrib => attrib.Value).FirstOrDefault();
            }
            return null;
        }

        public static string RemoveComments(string s)
        {
            return Regex.Replace(s, "<!--(\n|.)*?-->", string.Empty);
        }
    }
}
