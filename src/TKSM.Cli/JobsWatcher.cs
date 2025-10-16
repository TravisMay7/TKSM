using TKSM.Abstractions.Kernel;
using TKSM.Abstractions.Scheduling;
using TKSM.Host.Cli;

public sealed class JobsWatcher : IDisposable
{
	private readonly IKernelBus _bus;
	private readonly ConsoleMux _mux;
	private readonly List<IDisposable> _subs = new();

	public JobsWatcher(IKernelBus bus, ConsoleMux mux)
	{
		_bus = bus; _mux = mux;
		_subs.Add(_bus.Subscribe<JobEnqueued>(e => _mux.WriteLine(JobLine("ENQ ", e.Id, e.Name, e.Priority, e.Tags))));
		_subs.Add(_bus.Subscribe<JobStarted>(e => _mux.WriteLine(JobLine("RUN ", e.Id, e.Name))));
		_subs.Add(_bus.Subscribe<JobSucceeded>(e => _mux.WriteLine(JobLine("OK  ", e.Id, e.Name))));
		_subs.Add(_bus.Subscribe<JobCancelled>(e => _mux.WriteLine(JobLine("CANC", e.Id, e.Name))));
		_subs.Add(_bus.Subscribe<JobFailed>(e => _mux.WriteLine(JobLine("FAIL", e.Id, e.Name, err: e.Error))));
	}

	private static string JobLine(string tag, JobId id, string name, JobPriority? prio = null, string[]? tags = null, string? err = null)
	{
		var p = prio is null ? "" : $" p={prio}";
		var t = (tags is { Length: > 0 }) ? $" [{string.Join(',', tags)}]" : "";
		var e = string.IsNullOrWhiteSpace(err) ? "" : $" :: {err}";
		return $"[jobs] {tag} {id.Value,4} {name}{p}{t}{e}";
	}

	public void Dispose()
	{
		foreach (var s in _subs) { try { s.Dispose(); } catch { } }
		_subs.Clear();
	}
}
