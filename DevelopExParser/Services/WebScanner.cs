using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using DevelopExParser.Models;
using System.ComponentModel.DataAnnotations;

namespace DevelopExParser.Services
{
    public class WebScanner : IWebScanner
    {
        #region Fields

        private ScanerOptions _options;
        private WorkerStatus _status;
        private CancellationTokenSource _cancellationTokenSource;
        private IHtmlParser _htmlParser;
        private IHtmlDownloader _htmlDownloader;

        private ConcurrentQueue<Site> _sitesToDownload;
        private List<Site> _downloadedSites;
        private List<Site> _handledSites;
        private Dictionary<String, Int32> _alreadyDetected;

        #endregion

        #region Events

        public event Action ResetState;
        public event Action<Int32, Int32> ProgressChanged;
        public event Action<WorkerStatus> WorkCompleted;
        public event Action<String> MessageEmitte;
        public event Action<Int32, String, String> MoveTo;
        public event Action<String, String, String> RenderSite;
        public event Action<Int32, Int32> ProgressGlobalChanged;

        #endregion

        #region Methods

        /// <summary>
        /// Wrapper under _sitesToDownload collection for convenience.
        /// </summary>
        /// <returns></returns>
        private IEnumerable<Site> SitesToDownload()
        {
                Site site = null;
                while (_sitesToDownload.Count > 0)
                {
                    while (!_sitesToDownload.TryDequeue(out site));
                    yield return site;
                }
        }

        /// <summary>
        /// Download and parse sites from _sitesToDownload collection.
        /// </summary>
        private void DownloadSites()
        {
            Object progressLocker = new Object();

            ParallelOptions parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = _options.ThreadCount, CancellationToken = _cancellationTokenSource.Token };

            Int32 progress = 0;
            Int32 layerSitesCount = _sitesToDownload.Count();

            Parallel.ForEach(SitesToDownload(),
            parallelOptions,
            (currentSite, loopstate, index) =>
            {
                try
                {
                    currentSite.Status = SiteStatus.Downloading;
                    RenderSite.Invoke(currentSite.Url, $"{currentSite.Id}", currentSite.Status.ToString());
                    String content = _htmlDownloader.GetHtml(currentSite.Url);
                    
                    currentSite.Links = _htmlParser.GetLinks(content);
                    currentSite.Status = content.IndexOf(_options.SearchedText) != -1 ? SiteStatus.Found : SiteStatus.NotFound;

                    _handledSites.Add(currentSite);
                    _downloadedSites.Add(currentSite);
                    MoveTo.Invoke(currentSite.Id, currentSite.Status.ToString(), null);
                }

                catch (Exception ex)
                {
                    currentSite.Status = SiteStatus.Error;
                    String errorMessage = $"{ex?.Message} {ex.InnerException?.InnerException?.Message} ";
                    currentSite.ErrorText = errorMessage;
                    MoveTo.Invoke(currentSite.Id, currentSite.Status.ToString(), errorMessage);
                    _handledSites.Add(currentSite);
                }

                lock (progressLocker)
                {
                    progress++; 
                }

                ProgressChanged.Invoke(progress, layerSitesCount);
                ProgressGlobalChanged.Invoke(_handledSites.Count, _options.ScanUrlCount);
                parallelOptions.CancellationToken.ThrowIfCancellationRequested();

            });
        }

        /// <summary>
        /// Fill _sitesToDownload collection for next layer of downloading.
        /// </summary>
        private void FillSitesToDownload()
        {
            foreach (var currentSite in _downloadedSites)
            {
                if (_alreadyDetected.Count < _options.ScanUrlCount)
                {
                    foreach (var link in currentSite.Links)
                    {
                        if (!_alreadyDetected.ContainsKey(link) && (_alreadyDetected.Count < _options.ScanUrlCount))
                        {
                            _sitesToDownload.Enqueue(new Site(link) { Status = SiteStatus.Waiting, Id = _alreadyDetected.Count });
                            _alreadyDetected.Add(link, _alreadyDetected.Count());
                        }
                    }
                }
            }
            _downloadedSites.Clear();
        }

        /// <summary>
        /// Start process of scanning.
        /// </summary>
        /// <param name="options">Scaner options.</param>
        public void Run(ScanerOptions options)
        {

            if (_status == WorkerStatus.Stop)
            {

                var results = new List<ValidationResult>();
                var context = new ValidationContext(options);
                if (!Validator.TryValidateObject(options, context, results, true))
                {
                    MessageEmitte.Invoke(results.Select(res => res.ErrorMessage).Aggregate((totalMessage, currentMessage) => totalMessage + "\n" + currentMessage));
                    return;
                }

                _status = WorkerStatus.Active;
                _options = options;
                _sitesToDownload = new ConcurrentQueue<Site>();
                _downloadedSites = new List<Site>();
                _alreadyDetected = new Dictionary<String, Int32>();
                _sitesToDownload.Enqueue(new Site(_options.BaseUrl) { Status = SiteStatus.Waiting });
                _alreadyDetected.Add(_options.BaseUrl, _alreadyDetected.Count());
                _handledSites = new List<Site>();

                _cancellationTokenSource = new CancellationTokenSource();

                ResetState.Invoke();

                while ((_sitesToDownload.Count != 0) && (_status != WorkerStatus.Stop))
                {
                    try
                    {

                        Task.Factory.StartNew(() => DownloadSites(), _cancellationTokenSource.Token)
                            .ContinueWith((antecendent) => FillSitesToDownload(), _cancellationTokenSource.Token)
                            .Wait();
                    }
                    catch (AggregateException ex)
                    {
                        MessageEmitte.Invoke($"From Catch {ex.StackTrace}");
                        _cancellationTokenSource = new CancellationTokenSource();
                    }
                    catch (OperationCanceledException ex)
                    {
                        MessageEmitte.Invoke($"From Catch {ex.StackTrace}");
                    }

                    while (_status == WorkerStatus.Pause)
                    {
                        Thread.Sleep(100);
                    }
                }

                WorkCompleted.Invoke(_status);
                _status = WorkerStatus.Stop;
            }
            else if (_status == WorkerStatus.Pause)
            {
                _status = WorkerStatus.Active;
            }

        }

        /// <summary>
        /// Stop process of scanning.
        /// </summary>
        public void Stop()
        {
            _status = WorkerStatus.Stop;
            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// Pause process of scanning.
        /// </summary>
        public void Pause()
        {
            _status = WorkerStatus.Pause;
            _cancellationTokenSource?.Cancel();
        }

        #endregion

        public WebScanner(IHtmlParser htmlParser, IHtmlDownloader htmlDownloader)
        {
            _status = WorkerStatus.Stop;
            _htmlParser = htmlParser;
            _htmlDownloader = htmlDownloader;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        }

    }
}
