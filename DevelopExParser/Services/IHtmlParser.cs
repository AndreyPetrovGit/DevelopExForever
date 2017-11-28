using System;
using System.Collections.Generic;

namespace DevelopExParser.Services
{
    public interface IHtmlParser
    {
        List<String> GetLinks(String content);
    }
}
