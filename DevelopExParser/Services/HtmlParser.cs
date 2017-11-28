using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DevelopExParser.Services
{
    public class HtmlParser: IHtmlParser
    {

        /// <summary>
        /// Get list of links from html content
        /// </summary>
        /// <param name="content">Html content</param>
        /// <returns>List of links</returns>
        public List<String> GetLinks(String content)
        {
            List<String> links = new List<String>();
            Regex linkPattern = new Regex("(?<=\")https?://\\S*(?=\")", RegexOptions.IgnoreCase);
            Regex anchorPattern = new Regex(@"<a[^>]+>");

            foreach (Match anchor in anchorPattern.Matches(content))
            {
                String anchorLine = anchor.Value;
                String url = linkPattern.Match(anchorLine).Value;
                if (!String.IsNullOrEmpty(url))
                {
                    links.Add(url);
                }
            }
            return links;
        }
    }
}
