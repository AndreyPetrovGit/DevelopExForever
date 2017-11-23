using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Http;
using DevelopExParser.Models;

namespace DevelopExParser.Services
{
    public class SiteScanner: ISiteScanner
    {
        #region Fields

        public WorkerStatus Status { get; set; }

        String BaseUrl { get; set; }
        Int32 ThreadCount;
        String SearchedText;
        Int32 ScanUrlCount;

        Queue<Site> ToParseQueue;
        List<Site> HandledSites;

        static CancellationTokenSource _cancellationTokenSource;

        public Dictionary<String, Int32> AlreadyDetected { get; set; }

        public event Action ResetState;
        public event Action<double> ProgressChanged;
        public event Action<String> WorkCompleted;
        public event Action<string> MessageEmitter;
        public event Action<int, string, string> MoveTo;
        public event Action<string, string, string> RenderSite;
        public event Action<double> ProgressGlobalChanged;
        #endregion

        #region Methods
        public bool ParseTarget(String content)
        {
            Regex targetPattern = new Regex($@"{SearchedText}");
            if (targetPattern.IsMatch(content))
            {
                return true;
            }
            return false;
        }
        public List<String> ParseLinks(String content)
        {
            List<String> links = new List<string>();
            Regex linkPattern = new Regex("(?<=\")https?://\\S*(?=\")", RegexOptions.IgnoreCase);
            Regex anchorPattern = new Regex(@"<a[^>]+>");
            foreach (Match anchor in anchorPattern.Matches(content))
            {
                String anchorLine = anchor.Value;
                foreach (Match link in linkPattern.Matches(anchorLine))
                {
                    links.Add(link.Value);

                }
            }
            return links;
        }

        IEnumerable<Site> SitesToParse()
        {
            while (0 < ToParseQueue.Count())
            {
                yield return ToParseQueue.Dequeue();
            }
        }

        List<Site> SitesToDownload { get; set; }

        private void DownloadSites()
        {
            object locker = new object();

            _cancellationTokenSource = new CancellationTokenSource();
            ParallelOptions parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = ThreadCount, CancellationToken = _cancellationTokenSource.Token };

            int progress = 0;
            int TotlalCount = SitesToDownload.Count();
            try
            {
                Parallel.ForEach(SitesToDownload,
                parallelOptions,
                () => new HttpClient(),
                (currentSite, loopstate, index, httpClient) =>
                {
                    try
                    {
                        currentSite.Status = SiteStatus.Downloading;
                        RenderSite.Invoke(currentSite.Url, $"{AlreadyDetected[currentSite.Url]}", currentSite.Status.ToString());
                        MessageEmitter.Invoke($"{Thread.CurrentThread.ManagedThreadId} start download {currentSite.Url}");
                        HttpResponseMessage response = httpClient.GetAsync(currentSite.Url).Result;
                        byte[] array = response.Content.ReadAsByteArrayAsync().Result;
                        currentSite.Content = System.Text.Encoding.UTF8.GetString(array);
                        ToParseQueue.Enqueue(currentSite);
                        MessageEmitter.Invoke($"{Thread.CurrentThread.ManagedThreadId} end download {currentSite.Url}");
                    }

                    catch (Exception ex)
                    {
                        currentSite.Status = SiteStatus.Error;
                        MoveTo.Invoke(currentSite.Id, currentSite.Status.ToString(), ex.InnerException?.InnerException?.Message);

                        currentSite.ErrorText = ex.InnerException?.InnerException?.Message;
                        HandledSites.Add(currentSite);
                        ProgressGlobalChanged.Invoke((double)(HandledSites.Count * 100) / ScanUrlCount);

                    }
                    lock (locker)
                    {
                        progress++;
                        ProgressChanged.Invoke((double)(progress * 100) / TotlalCount);
                    }
                    parallelOptions.CancellationToken.ThrowIfCancellationRequested();
                    return httpClient;
                },
                (httpClient) => { });
            } catch (OperationCanceledException ex)
            {
                MessageEmitter.Invoke($"From Catch {ex.StackTrace}");
            }
        }
        private void ParseSites()
        {
            foreach (var currentSite in SitesToParse())
            {
                try
                {
                    if (AlreadyDetected.Count < ScanUrlCount)
                    {
                        var links = ParseLinks(currentSite.Content);
                        links.ForEach(link =>
                        {
                            if (!AlreadyDetected.ContainsKey(link) && AlreadyDetected.Count < ScanUrlCount)
                            {
                                SitesToDownload.Add(new Site(link) { Status = SiteStatus.Waiting, Id = AlreadyDetected.Count });
                                AlreadyDetected.Add(link, AlreadyDetected.Count());
                            }

                        });
                    }

                    currentSite.Status = ParseTarget(currentSite.Content) ? SiteStatus.Found : SiteStatus.NotFound;
                    HandledSites.Add(currentSite);
                    ProgressGlobalChanged.Invoke((double)(HandledSites.Count * 100) / ScanUrlCount);

                    MoveTo.Invoke(currentSite.Id, currentSite.Status.ToString(), null);
                    if (currentSite.Status == SiteStatus.Found)
                    {
                        MessageEmitter.Invoke(currentSite.Url);
                    }
                }
                catch (Exception ex)
                {

                    MessageEmitter.Invoke(ex.Data.ToString());
                }
            }
        }

        public void Run(Int32 scanUrlCount, Int32 threadCount, String searchedText, String baseUrl)
        {

            if (Status == WorkerStatus.Stop)
            {
                ResetState.Invoke();
                BaseUrl = baseUrl;
                SitesToDownload = new List<Site>();
                ToParseQueue = new Queue<Site>();
                AlreadyDetected = new Dictionary<String, Int32>();
                AlreadyDetected.Add(baseUrl, AlreadyDetected.Count());
                HandledSites = new List<Site>();
                SitesToDownload.Add(new Site(baseUrl) { Status = SiteStatus.Downloading});
                ThreadCount = threadCount;
                SearchedText = searchedText;
                ScanUrlCount = scanUrlCount;

                Status = WorkerStatus.Active;
                MessageEmitter.Invoke("Start Wrok!");
                while (SitesToDownload.Count != 0 && Status != WorkerStatus.Stop)
                {
                    downloadStep:
                    DownloadSites();

                    SitesToDownload = SitesToDownload.Where(site => site.Status == SiteStatus.Waiting).ToList();
                    if (Status == WorkerStatus.Pause)
                    {
                        while (Status == WorkerStatus.Pause)
                        {
                            MessageEmitter.Invoke("Pause!");
                            Thread.Sleep(100);
                        }
                        goto downloadStep;
                    }
                   
                    ParseSites();
                }
                string finalMessage;

                if (Status == WorkerStatus.Stop)
                {
                    finalMessage = "Process canceled!";
                }
                else
                {
                    finalMessage = "Process completed successfully!";
                    Status = WorkerStatus.Stop;
                }

                 WorkCompleted.Invoke(finalMessage);
            }
            else if (Status == WorkerStatus.Pause)
            {
                Status = WorkerStatus.Active;
            }

        }

        public void Stop()
        {
            Status = WorkerStatus.Stop;
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
            }
        }

        public void Pause()
        {
            Status = WorkerStatus.Pause;
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
            }
        }
        #endregion

        public SiteScanner()
        {
            Status = WorkerStatus.Stop;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        }

       
    }
}
