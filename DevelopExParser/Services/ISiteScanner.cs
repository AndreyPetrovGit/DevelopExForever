using System;
using DevelopExParser.Models;

namespace DevelopExParser.Services
{
    public interface ISiteScanner
    {      
        event Action ResetState;
        event Action<Double> ProgressChanged; 
        event Action<String> WorkCompleted;
        event Action<String> MessageEmitter;
        event Action<Int32, String, String> MoveTo;
        event Action<String, String, String> RenderSite;
        event Action<Double> ProgressGlobalChanged;

        void Run(ScanerOptions options);
        void Stop();
        void Pause();
    }
}
