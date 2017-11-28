using System;
using System.Collections.Generic;

namespace DevelopExParser.Models
{
    class Site
    {
        public Int32 Id { get; set; }
        public String Url { get; set; }
        public List<String> Links { get; set; }
        public SiteStatus Status { get; set; }
        public String Content { get; set; }
        public String ErrorText { get; set; }

        public Site(String url)
        {
            Url = url;
        }
    }
}
