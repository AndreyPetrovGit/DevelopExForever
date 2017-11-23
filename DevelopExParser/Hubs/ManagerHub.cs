using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using DevelopExParser.Services;
using DevelopExParser.Models;
using System.Threading;

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

        public async Task Start(string url, int maxThreadCount, string searchText, int scanUrlCount)
        {
            await Task.Run(() =>_siteScanner.Run(scanUrlCount, maxThreadCount, searchText, url));
        }
        public async Task Stop()
        {
            await Task.Run(() => { _siteScanner.Stop(); });
        }
        public async Task Pause()
        {
            await Task.Run(() => { _siteScanner.Pause(); });
        }



        private async void ConsoleLog(string message)
        {
           await Clients.All.InvokeAsync("consoleLog", message);
        }
        private async void ResetState()
        {
            await Clients.All.InvokeAsync("resetState");
        }
        private async void RenderSite(string url, string id, string status )
        {
            await Clients.All.InvokeAsync("renderSite", new string[] { id, url, status });
        }
        private async void MoveTo(int id, string status, string error)
        {
            string[] data = new string[] { $"{id}", status, ""};
            if (error != null && error != "")
            {
                data[2] = error;
            }
            await Clients.All.InvokeAsync("moveTo", data);
        }

        private async void ProgressChanged(double progress)
        {
            await Clients.All.InvokeAsync("progressChanged", progress);
        }
        private async void WorkCompleted(string message)
        {
            await Clients.All.InvokeAsync("workCompleted", message);
        }
        private async void ProgressGlobalChanged(double progress)
        {
            await Clients.All.InvokeAsync("progressGlobalChanged", progress);
        }
    }
}
