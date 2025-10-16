using TKSM.Abstractions.Observability;
using TKSM.Host.Cli;

public sealed class LogTail : IDisposable
{
	private readonly IEventLogger _log;
	private readonly ConsoleMux _mux;
	private ILogSubscription? _sub;

	public LogTail(IEventLogger log, ConsoleMux mux) { _log = log; _mux = mux; }

	public void Start()
	{
		if (_sub != null) return;
		_sub = _log.Subscribe(e => _mux.WriteLine($"[{e.Timestamp:HH:mm:ss}] {e.Level,-11} {e.Category,-10} {e.Message}"));
	}

	public void Stop()
	{
		_sub?.Dispose(); _sub = null;
	}

	public void Dispose() => Stop();
}
