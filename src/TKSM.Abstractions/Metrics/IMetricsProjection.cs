namespace TKSM.Abstractions.Metrics;

public interface IMetricsProjection
{
	long Total { get; }
	long Succeeded { get; }
	long Failed { get; }
	long Cancelled { get; }
	TimeSpan? AvgDuration { get; }
	IReadOnlyDictionary<string, (long count, TimeSpan avg)> ByName { get; } // job name
}
