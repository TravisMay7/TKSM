namespace TKSM.Abstractions.Scheduling;

public interface IScheduler
{
	IJobHandle Enqueue(Func<CancellationToken, Task> work, string name,
		JobPriority priority = JobPriority.Normal, IEnumerable<string>? tags = null,
		IEnumerable<JobId>? dependsOn = null);

	IJobHandle EnqueueAfter(TimeSpan delay, Func<CancellationToken, Task> work, string name,
		JobPriority priority = JobPriority.Normal, IEnumerable<string>? tags = null,
		IEnumerable<JobId>? dependsOn = null);

	IJobHandle EnqueueAt(DateTimeOffset whenUtc, Func<CancellationToken, Task> work, string name,
		JobPriority priority = JobPriority.Normal, IEnumerable<string>? tags = null,
		IEnumerable<JobId>? dependsOn = null);

	// observation
	bool TryGet(JobId id, out IJobHandle handle);
	IReadOnlyList<IJobHandle> Snapshot(Func<IJobHandle, bool>? predicate = null);
}
