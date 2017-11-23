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

        WorkerStatus _status;

        String _baseUrl;
        Int32 _threadCount;
        String _searchedText;
        Int32 _scanUrlCount;

        Queue<Site> _toParseQueue;
        List<Site> _handledSites;
        List<Site> _sitesToDownload;
        Dictionary<String, Int32> _alreadyDetected;

        public event Action ResetState;
        public event Action<double> ProgressChanged;
        public event Action<String> WorkCompleted;
        public event Action<string> MessageEmitter;
        public event Action<int, string, string> MoveTo;
        public event Action<string, string, string> RenderSite;
        public event Action<double> ProgressGlobalChanged;

        CancellationTokenSource _cancellationTokenSource;

        #endregion

        #region Methods
        public bool ParseTarget(String content)
        {
            Regex targetPattern = new Regex($@"{_searchedText}");
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
            while (0 < _toParseQueue.Count())
            {
                yield return _toParseQueue.Dequeue();
            }
        }

        private void DownloadSites()
        {
            object locker = new object();

            _cancellationTokenSource = new CancellationTokenSource();
            ParallelOptions parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = _threadCount, CancellationToken = _cancellationTokenSource.Token };

            int progress = 0;
            int TotlalCount = _sitesToDownload.Count();
            try
            {
                Parallel.ForEach(_sitesToDownload,
                parallelOptions,
                () => new HttpClient(),
                (currentSite, loopstate, index, httpClient) =>
                {
                    try
                    {
                        currentSite.Status = SiteStatus.Downloading;
                        RenderSite.Invoke(currentSite.Url, $"{currentSite.Id}", currentSite.Status.ToString());
                        MessageEmitter.Invoke($"{Thread.CurrentThread.ManagedThreadId} start download {currentSite.Url}");
                        HttpResponseMessage response = httpClient.GetAsync(currentSite.Url).Result;
                        byte[] array = response.Content.ReadAsByteArrayAsync().Result;
                        currentSite.Content = System.Text.Encoding.UTF8.GetString(array);
                        _toParseQueue.Enqueue(currentSite);
                        MessageEmitter.Invoke($"{Thread.CurrentThread.ManagedThreadId} end download {currentSite.Url}");
                    }

                    catch (Exception ex)
                    {
                        currentSite.Status = SiteStatus.Error;
                        MoveTo.Invoke(currentSite.Id, currentSite.Status.ToString(), ex.InnerException?.InnerException?.Message);

                        currentSite.ErrorText = ex.InnerException?.InnerException?.Message;
                        _handledSites.Add(currentSite);
                        ProgressGlobalChanged.Invoke((double)(_handledSites.Count * 100) / _scanUrlCount);

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
                    if (_alreadyDetected.Count < _scanUrlCount)
                    {
                        var links = ParseLinks(currentSite.Content);
                        links.ForEach(link =>
                        {
                            if (!_alreadyDetected.ContainsKey(link) && _alreadyDetected.Count < _scanUrlCount)
                            {
                                _sitesToDownload.Add(new Site(link) { Status = SiteStatus.Waiting, Id = _alreadyDetected.Count });
                                _alreadyDetected.Add(link, _alreadyDetected.Count());
                            }

                        });
                    }

                    currentSite.Status = ParseTarget(currentSite.Content) ? SiteStatus.Found : SiteStatus.NotFound;
                    _handledSites.Add(currentSite);
                    ProgressGlobalChanged.Invoke((double)(_handledSites.Count * 100) / _scanUrlCount);

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

            if (_status == WorkerStatus.Stop)
            {
                ResetState.Invoke();
                _baseUrl = baseUrl;
                _sitesToDownload = new List<Site>();
                _toParseQueue = new Queue<Site>();
                _alreadyDetected = new Dictionary<String, Int32>();
                _alreadyDetected.Add(baseUrl, _alreadyDetected.Count());
                _handledSites = new List<Site>();
                _sitesToDownload.Add(new Site(baseUrl) { Status = SiteStatus.Downloading});
                _threadCount = threadCount;
                _searchedText = searchedText;
                _scanUrlCount = scanUrlCount;

                _status = WorkerStatus.Active;
                MessageEmitter.Invoke("Start Wrok!");
                while (_sitesToDownload.Count != 0 && _status != WorkerStatus.Stop)
                {
                    downloadStep:
                    DownloadSites();

                    _sitesToDownload = _sitesToDownload.Where(site => site.Status == SiteStatus.Waiting).ToList();
                    if (_status == WorkerStatus.Pause)
                    {
                        while (_status == WorkerStatus.Pause)
                        {
                            MessageEmitter.Invoke("Pause!");
                            Thread.Sleep(100);
                        }
                        goto downloadStep;
                    }
                   
                    ParseSites();
                }
                string finalMessage;

                if (_status == WorkerStatus.Stop)
                {
                    finalMessage = "Process canceled!";
                }
                else
                {
                    finalMessage = "Process completed successfully!";
                    _status = WorkerStatus.Stop;
                }

                 WorkCompleted.Invoke(finalMessage);
            }
            else if (_status == WorkerStatus.Pause)
            {
                _status = WorkerStatus.Active;
            }

        }

        public void Stop()
        {
            _status = WorkerStatus.Stop;
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
            }
        }

        public void Pause()
        {
            _status = WorkerStatus.Pause;
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
            }
        }
        #endregion

        public SiteScanner()
        {
            _status = WorkerStatus.Stop;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        }

       
    }
}
