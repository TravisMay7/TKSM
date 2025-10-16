using System;
using TKSM.Abstractions.Observability;

namespace TKSM.Host.Cli
{
    /// <summary>Example sink; kept in host so core remains sink-agnostic.</summary>
    public sealed class ConsoleLogSink : ILogSink, IDisposable
    {
        private ILogSubscription? _sub;
        public void Attach(IEventLogger logger)
        {
            _sub = logger.Subscribe(e =>
            {
                Console.WriteLine($"[{e.Timestamp:HH:mm:ss}] {e.Level,-11} {e.Category,-10} {e.Message}");
            });
        }
        public void Dispose() => _sub?.Dispose();
    }
}
