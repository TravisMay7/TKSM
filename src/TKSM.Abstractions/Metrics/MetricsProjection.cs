using System.Collections.Concurrent;
using System.Diagnostics;
using TKSM.Abstractions.Kernel;
using TKSM.Abstractions.Scheduling;

namespace TKSM.Abstractions.Metrics;

public sealed class MetricsProjection : IMetricsProjection, IDisposable
{
	private readonly IDisposable _s1, _s2, _s3, _s4;
	private long _total, _ok, _fail, _cancel;
	private readonly ConcurrentDictionary<JobId, Stopwatch> _sw = new();
	private readonly ConcurrentDictionary<string, (long n, double ms)> _byName = new();

	public MetricsProjection(IKernelBus bus)
	{
		_s1 = bus.Subscribe<JobStarted>(e => _sw[e.Id] = Stopwatch.StartNew());
		_s2 = bus.Subscribe<JobSucceeded>(e => StopAndAccumulate(e.Id, e.Name, ok: true));
		_s3 = bus.Subscribe<JobFailed>(e => StopAndAccumulate(e.Id, e.Name, ok: false));
		_s4 = bus.Subscribe<JobCancelled>(e => { Interlocked.Increment(ref _cancel); _sw.TryRemove(e.Id, out _); Interlocked.Increment(ref _total); });
	}

	private void StopAndAccumulate(JobId id, string name, bool ok)
	{
		if (_sw.TryRemove(id, out var w)) w.Stop();
		var ms = w?.Elapsed.TotalMilliseconds ?? 0;
		if (ok) Interlocked.Increment(ref _ok); else Interlocked.Increment(ref _fail);
		Interlocked.Increment(ref _total);
		_byName.AddOrUpdate(name, _ => (1, ms), (_, cur) => (cur.n + 1, cur.ms + ms));
	}

	public long Total => Interlocked.Read(ref _total);
	public long Succeeded => Interlocked.Read(ref _ok);
	public long Failed => Interlocked.Read(ref _fail);
	public long Cancelled => Interlocked.Read(ref _cancel);
	public TimeSpan? AvgDuration
		=> Total == 0 ? null : TimeSpan.FromMilliseconds(_byName.Sum(kv => kv.Value.ms) / Math.Max(1, _byName.Sum(kv => kv.Value.n)));

	public IReadOnlyDictionary<string, (long count, TimeSpan avg)> ByName
		=> _byName.ToDictionary(kv => kv.Key, kv => (kv.Value.n, TimeSpan.FromMilliseconds(kv.Value.ms / Math.Max(1, kv.Value.n))));

	public void Dispose() { _s1.Dispose(); _s2.Dispose(); _s3.Dispose(); _s4.Dispose(); }
}