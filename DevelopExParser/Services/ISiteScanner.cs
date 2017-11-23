using DevelopExParser.Models;
using System;
using System.Threading.Tasks;

namespace DevelopExParser.Services
{
    public interface ISiteScanner
    {
        WorkerStatus Status { get; set; }
        
        event Action ResetState;
        event Action<double> ProgressChanged; 
        event Action<String> WorkCompleted;
        event Action<string> MessageEmitter;
        event Action<int, string, string> MoveTo;
        event Action<string, string, string> RenderSite;
        event Action<double> ProgressGlobalChanged;

        void Run(int scanUrlCount, int maxThreadCount, string searchText, string url);
        void Stop();
        void Pause();
    }
}
