namespace TKSM.Abstractions.Scheduling;

public interface IJobHandle
{
	JobId Id { get; }
	string Name { get; }
	JobStatus Status { get; }
	double? Progress { get; }          // 0..1 if reported
	DateTimeOffset EnqueuedUtc { get; }
	DateTimeOffset? StartedUtc { get; }
	DateTimeOffset? CompletedUtc { get; }
	string[] Tags { get; }

	Task AwaitAsync(CancellationToken ct = default); // completes when job finishes
	bool TryCancel();
}
