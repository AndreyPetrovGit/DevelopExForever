using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Http;
using DevelopExParser.Models;
using System.ComponentModel.DataAnnotations;

namespace DevelopExParser.Services
{
    public class SiteScanner: ISiteScanner
    {
        #region Fields

        WorkerStatus _status;

        ScanerOptions _options;

        Queue<Site> _toParseQueue;
        List<Site> _handledSites;
        List<Site> _sitesToDownload;
        Dictionary<String, Int32> _alreadyDetected;
        IEnumerable<Site> SitesToParse()
        {
            while (0 < _toParseQueue.Count())
            {
                yield return _toParseQueue.Dequeue();
            }
        }

        public event Action ResetState;
        public event Action<Double> ProgressChanged;
        public event Action<String> WorkCompleted;
        public event Action<String> MessageEmitter;
        public event Action<Int32, String, String> MoveTo;
        public event Action<String, String, String> RenderSite;
        public event Action<Double> ProgressGlobalChanged;

        CancellationTokenSource _cancellationTokenSource;

        #endregion

        #region Methods

        void DownloadSites()
        {
            Object progressLocker = new Object();

            
            ParallelOptions parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = _options.ThreadCount, CancellationToken = _cancellationTokenSource.Token };

            int progress = 0;
            int TotlalCount = _sitesToDownload.Count();
           
                Parallel.ForEach(_sitesToDownload,
                parallelOptions,
                () => new HttpClient(),
                (currentSite, loopstate, index, httpClient) =>
                {
                    try
                    {
                        currentSite.Status = SiteStatus.Downloading;
                        RenderSite.Invoke(currentSite.Url, $"{currentSite.Id}", currentSite.Status.ToString());
                        HttpResponseMessage response = httpClient.GetAsync(currentSite.Url).Result;

                        Byte[] array = response.Content.ReadAsByteArrayAsync().Result;
                        currentSite.Content = System.Text.Encoding.UTF8.GetString(array);
                        _toParseQueue.Enqueue(currentSite);
                    }

                    catch (Exception ex)
                    {
                        currentSite.Status = SiteStatus.Error;
                        MoveTo.Invoke(currentSite.Id, currentSite.Status.ToString(), ex.InnerException?.InnerException?.Message);
                        currentSite.ErrorText = ex.InnerException?.InnerException?.Message;
                        _handledSites.Add(currentSite);
                        ProgressGlobalChanged.Invoke((Double)(_handledSites.Count * 100) / _options.ScanUrlCount);

                    }

                    lock (progressLocker)
                    {
                        progress++;
                        _sitesToDownload = _sitesToDownload.Where(site => site.Status == SiteStatus.Waiting).ToList();
                    }

                    ProgressChanged.Invoke((Double)(progress * 100) / TotlalCount);
                   
                    parallelOptions.CancellationToken.ThrowIfCancellationRequested();
                    return httpClient;
                },
                (httpClient) => { });

        }

        List<String> ParseLinks(String content)
        {
            List<String> links = new List<String>();
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

        void ParseSites()
        {
            foreach (var currentSite in SitesToParse())
            {
                try
                {
                    if (_alreadyDetected.Count < _options.ScanUrlCount)
                    {
                        var links = ParseLinks(currentSite.Content);
                        links.ForEach(link =>
                        {
                            if (!_alreadyDetected.ContainsKey(link) && _alreadyDetected.Count < _options.ScanUrlCount)
                            {
                                _sitesToDownload.Add(new Site(link) { Status = SiteStatus.Waiting, Id = _alreadyDetected.Count });
                                _alreadyDetected.Add(link, _alreadyDetected.Count());
                            }

                        });
                    }

                    currentSite.Status = currentSite.Content.IndexOf(_options.SearchedText) != -1 ? SiteStatus.Found : SiteStatus.NotFound;
                    _handledSites.Add(currentSite);
                    ProgressGlobalChanged.Invoke((Double)(_handledSites.Count * 100) / _options.ScanUrlCount);

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

        public void Run(ScanerOptions options)
        {
            
            if (_status == WorkerStatus.Stop)
            {

                var results = new List<ValidationResult>();
                var context= new ValidationContext(options);
                if (!Validator.TryValidateObject(options, context, results, true))
                {
                    MessageEmitter.Invoke(results.Select(res => res.ErrorMessage).Aggregate((se, ne) => se + "\n" + ne));
                    return;
                }

                _status = WorkerStatus.Active;
                _options = options;
                ResetState.Invoke();
                _sitesToDownload = new List<Site>();
                _toParseQueue = new Queue<Site>();
                _alreadyDetected = new Dictionary<String, Int32>();
                _alreadyDetected.Add(_options.BaseUrl, _alreadyDetected.Count());
                _handledSites = new List<Site>();
                _sitesToDownload.Add(new Site(_options.BaseUrl) { Status = SiteStatus.Waiting});
                _cancellationTokenSource = new CancellationTokenSource();
               
                while (_sitesToDownload.Count != 0 && _status != WorkerStatus.Stop)
                {
                    try
                    {
                        
                        Task.Factory.StartNew(() => DownloadSites(), _cancellationTokenSource.Token)
                            .ContinueWith((antecendent) => ParseSites(), _cancellationTokenSource.Token)
                            .Wait();
                    }
                    catch (AggregateException ex)
                    {
                        MessageEmitter.Invoke($"From Catch {ex.StackTrace}");
                        _cancellationTokenSource = new CancellationTokenSource();
                    }
                    catch (OperationCanceledException ex)
                    {
                        MessageEmitter.Invoke($"From Catch {ex.StackTrace}");
                    }
                    while (_status == WorkerStatus.Pause) Thread.Sleep(100);
  
                }
                WorkCompleted.Invoke(_status == WorkerStatus.Stop ? "Process canceled!": "Process completed successfully!");
                _status = WorkerStatus.Stop;
            }
            else if (_status == WorkerStatus.Pause)
            {
                _status = WorkerStatus.Active;
            }

        }

        public void Stop()
        {
            _status = WorkerStatus.Stop;
            _cancellationTokenSource?.Cancel();
        }

        public void Pause()
        {
            _status = WorkerStatus.Pause;
            _cancellationTokenSource?.Cancel();
        }

        #endregion

        #region Ctor

        public SiteScanner()
        {
            _status = WorkerStatus.Stop;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        }

        #endregion
        

    }
}
