using System;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using DevelopExParser.Services;
using DevelopExParser.Models;

namespace DevelopExParser.Hubs
{
    public class ManagerHub: Hub
    {
        private IWebScanner _siteScanner;

        #region Methods for server events

        /// <summary>
        /// Invoke console.log function on client side.
        /// </summary>
        /// <param name="message"></param>
        private async void ConsoleLog(String message)
        {
           await Clients.All.InvokeAsync("consoleLog", message);
        }

        /// <summary>
        /// Delete all old messages on the client side.
        /// </summary>
        private async void ResetState()
        {
            await Clients.All.InvokeAsync("resetState");
        }

        /// <summary>
        /// Create div for vizualize url on client side.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="id"></param>
        /// <param name="status"></param>
        private async void RenderSite(String url, String id, String status )
        {
            await Clients.All.InvokeAsync("renderSite", new String[] { id, url, status });
        }

        /// <summary>
        /// Move div width url to some state column.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="status"></param>
        /// <param name="error"></param>
        private async void MoveTo(Int32 id, String status, String error)
        {
            String[] data = new String[] { $"{id}", status, ""};
            if (error != null && error != "")
            {
                data[2] = error;
            }
            await Clients.All.InvokeAsync("moveTo", data);
        }

        /// <summary>
        /// Invoke client side function for change value in progress bar.
        /// </summary>
        /// <param name="progress"></param>
        /// <param name="layerSitesCount"></param>
        private async void ProgressChanged(Int32 progress, Int32 layerSitesCount)
        {
            await Clients.All.InvokeAsync("progressChanged", (Double)(progress * 100) / layerSitesCount);
        }

        /// <summary>
        /// Invoke client side function for change value in global progress bar.
        /// </summary>
        /// <param name="handledSitesCount"></param>
        /// <param name="scanUrlCount"></param>
        private async void ProgressGlobalChanged(Int32 handledSitesCount, Int32 scanUrlCount)
        {
            await Clients.All.InvokeAsync("progressGlobalChanged", (Double)(handledSitesCount * 100) / scanUrlCount);
        }

        /// <summary>
        /// Invoke alert function on client side for report.
        /// </summary>
        /// <param name="status"></param>
        private async void WorkCompleted(WorkerStatus status)
        {
            await Clients.All.InvokeAsync("workCompleted", status == WorkerStatus.Stop ? "Process canceled!" : "Process completed successfully!");
        }

        #endregion

        #region Methods for client events

        public async Task Start(String url, Int32 maxThreadCount, String searchText, Int32 scanUrlCount)
        {
            ScanerOptions options = new ScanerOptions
            {
                ScanUrlCount = scanUrlCount,
                BaseUrl = url,
                SearchedText = searchText,
                ThreadCount = maxThreadCount
            };

            await Task.Run(() => _siteScanner.Run(options));
        }

        public async Task Stop()
        {
            await Task.Run(() => { _siteScanner.Stop(); });
        }

        public async Task Pause()
        {
            await Task.Run(() => { _siteScanner.Pause(); });
        }

        #endregion

        public ManagerHub(IWebScanner siteScanner)
        {
            _siteScanner = siteScanner;
            _siteScanner.ResetState += ResetState;
            _siteScanner.ProgressChanged += ProgressChanged;
            _siteScanner.MoveTo += MoveTo;
            _siteScanner.MessageEmitte += ConsoleLog;
            _siteScanner.RenderSite += RenderSite;
            _siteScanner.WorkCompleted += WorkCompleted;
            _siteScanner.ProgressGlobalChanged += ProgressGlobalChanged;
        }
    }
}
