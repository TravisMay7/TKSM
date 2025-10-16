using TKSM.Abstractions.Scheduling;

namespace TKSM.Core.Scheduling;

public sealed class JobsProjection : IJobsProjection
{
	private readonly IScheduler _sched;

	public JobsProjection(IScheduler scheduler) => _sched = scheduler;

	public IReadOnlyList<IJobHandle> List(JobStatus? status = null, string? tag = null)
	{
		return _sched.Snapshot(j =>
		{
			if (status is not null && j.Status != status) return false;
			if (!string.IsNullOrEmpty(tag) && (j.Tags?.Contains(tag) != true)) return false;
			return true;
		});
	}

	public IJobHandle? Get(JobId id)
		=> _sched.TryGet(id, out var h) ? h : null;
}
