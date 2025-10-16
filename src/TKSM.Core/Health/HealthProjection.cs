using TKSM.Abstractions.Health;
using TKSM.Abstractions.Kernel;
using TKSM.Abstractions.Scheduling;
using TKSM.Abstractions.VarStore;

namespace TKSM.Core.Health;

public sealed class HealthProjection : IHealthProjection, IDisposable
{
	private readonly TimeSpan _beatWindow;
	private readonly IDisposable _subVar;
	private readonly IDisposable _subJob;

	public DateTimeOffset? StartedUtc { get; private set; }
	public DateTimeOffset? LastHeartbeatUtc { get; private set; }
	public bool IsHealthy => LastHeartbeatUtc is { } t && (DateTimeOffset.UtcNow - t) <= _beatWindow;

	public HealthProjection(IKernelBus bus, TimeSpan? beatWindow = null)
	{
		_beatWindow = beatWindow ?? TimeSpan.FromSeconds(10);

		_subVar = bus.Subscribe<VarCommitted>(v =>
		{
			if (v.Key.Equals("vars/kernel/startedUtc", StringComparison.OrdinalIgnoreCase))
				StartedUtc = v.Ts;
		});

		_subJob = bus.Subscribe<JobSucceeded>(j =>
		{
			if (string.Equals(j.Name, "kernel.seed.startedUtc", StringComparison.OrdinalIgnoreCase))
				StartedUtc = j.Ts;
		});
	}

	public void Dispose()
	{
		_subVar.Dispose();
		_subJob.Dispose();
	}
}
