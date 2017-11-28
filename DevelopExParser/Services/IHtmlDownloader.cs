using System;

namespace DevelopExParser.Services
{
    public interface IHtmlDownloader
    {
        String GetHtml(String url);
    }
}
