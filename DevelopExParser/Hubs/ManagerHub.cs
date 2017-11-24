using System;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using DevelopExParser.Services;
using DevelopExParser.Models;

namespace DevelopExParser.Hubs
{
    public class ManagerHub: Hub
    {
        private ISiteScanner _siteScanner;
        public ManagerHub(ISiteScanner siteScanner)
        {
            _siteScanner = siteScanner;
            _siteScanner.ResetState += ResetState;
            _siteScanner.ProgressChanged += ProgressChanged;
            _siteScanner.MoveTo += MoveTo;
            _siteScanner.MessageEmitter += ConsoleLog;
            _siteScanner.RenderSite += RenderSite;
            _siteScanner.WorkCompleted += WorkCompleted;
            _siteScanner.ProgressGlobalChanged += ProgressGlobalChanged;
        }

        public async Task Start(String url, Int32 maxThreadCount, String searchText, Int32 scanUrlCount)
        {
            ScanerOptions options = new ScanerOptions
            {
                ScanUrlCount = scanUrlCount,
                BaseUrl = url,
                SearchedText = searchText,
                ThreadCount = maxThreadCount
            };

            await Task.Run(() =>_siteScanner.Run(options));
        }
        public async Task Stop()
        {
            await Task.Run(() => { _siteScanner.Stop(); });
        }
        public async Task Pause()
        {
            await Task.Run(() => { _siteScanner.Pause(); });
        }

        private async void ConsoleLog(String message)
        {
           await Clients.All.InvokeAsync("consoleLog", message);
        }
        private async void ResetState()
        {
            await Clients.All.InvokeAsync("resetState");
        }
        private async void RenderSite(String url, String id, String status )
        {
            await Clients.All.InvokeAsync("renderSite", new String[] { id, url, status });
        }
        private async void MoveTo(Int32 id, String status, String error)
        {
            String[] data = new String[] { $"{id}", status, ""};
            if (error != null && error != "")
            {
                data[2] = error;
            }
            await Clients.All.InvokeAsync("moveTo", data);
        }
        private async void ProgressChanged(Double progress)
        {
            await Clients.All.InvokeAsync("progressChanged", progress);
        }
        private async void WorkCompleted(String message)
        {
            await Clients.All.InvokeAsync("workCompleted", message);
        }
        private async void ProgressGlobalChanged(Double progress)
        {
            await Clients.All.InvokeAsync("progressGlobalChanged", progress);
        }
    }
}
