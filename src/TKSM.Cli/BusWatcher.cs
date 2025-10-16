using TKSM.Abstractions.Kernel;
using TKSM.Abstractions.Scheduling;
using TKSM.Abstractions.VarStore;

namespace TKSM.Host.Cli;

public sealed class BusWatcher : IDisposable
{
	private readonly List<IDisposable> _subs = new();
	public BusWatcher(IKernelBus bus, ConsoleMux mux)
	{
		_subs.Add(bus.Subscribe<JobEnqueued>(e => mux.WriteLine($"[bus] enq  {e.Id.Value,4} {e.Name}")));
		_subs.Add(bus.Subscribe<JobStarted>(e => mux.WriteLine($"[bus] run  {e.Id.Value,4} {e.Name}")));
		_subs.Add(bus.Subscribe<JobSucceeded>(e => mux.WriteLine($"[bus] ok   {e.Id.Value,4} {e.Name}")));
		_subs.Add(bus.Subscribe<JobFailed>(e => mux.WriteLine($"[bus] fail {e.Id.Value,4} {e.Name} :: {e.Error}")));
		_subs.Add(bus.Subscribe<JobCancelled>(e => mux.WriteLine($"[bus] canc {e.Id.Value,4} {e.Name}")));
		_subs.Add(bus.Subscribe<VarCommitted>(v => mux.WriteLine($"[bus] var  {v.Key} = {v.Value ?? "(null)"}")));
	}
	public void Dispose() { foreach (var s in _subs) { try { s.Dispose(); } catch { } } _subs.Clear(); }
}
