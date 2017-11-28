using System;
using System.Net;
using System.Net.Http;

namespace DevelopExParser.Services
{
    public class HtmlDownloader : IHtmlDownloader
    {
        public string GetHtml(string url)
        {
            HttpResponseMessage response;

            using (var httpClient = new HttpClient())
            {
                response = httpClient.GetAsync(url).Result;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception(response.StatusCode.ToString());
            }

            Byte[] array = response.Content.ReadAsByteArrayAsync().Result;
            String content = System.Text.Encoding.UTF8.GetString(array);

            return content;
        }
    }
}
