namespace TKSM.Abstractions.Scheduling;

public interface IJobsProjection
{
	IReadOnlyList<IJobHandle> List(
		JobStatus? status = null,
		string? tag = null);

	IJobHandle? Get(JobId id);
}