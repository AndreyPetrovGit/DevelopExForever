using System;
using DevelopExParser.Models;

namespace DevelopExParser.Services
{
    public interface IWebScanner
    {
        event Action ResetState;
        event Action<Int32, Int32> ProgressChanged;
        event Action<WorkerStatus> WorkCompleted;
        event Action<String> MessageEmitte;
        event Action<Int32, String, String> MoveTo;
        event Action<String, String, String> RenderSite;
        event Action<Int32, Int32> ProgressGlobalChanged;

        void Run(ScanerOptions options);
        void Stop();
        void Pause();
    }
}
