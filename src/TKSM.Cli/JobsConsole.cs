using System.Globalization;
using TKSM.Abstractions.Scheduling;

namespace TKSM.Host.Cli;

public static class JobsConsole
{
	public static void Dump(IJobsProjection jobs, JobStatus? status = null, string? tag = null)
	{
		var list = jobs.List(status, tag)
					   .OrderBy(j => j.EnqueuedUtc)
					   .ToList();

		if (list.Count == 0)
		{
			Console.WriteLine("(no jobs)");
			return;
		}

		Console.WriteLine(" ID    STATUS       NAME                             TAGS              ENQ(utc)            START(utc)           END(utc)");
		Console.WriteLine(" ----- ------------ -------------------------------- ----------------- ------------------- ------------------- -------------------");

		foreach (var j in list)
		{
			var tags = (j.Tags is { Length: > 0 }) ? string.Join(",", j.Tags) : "-";
			Console.WriteLine($"{j.Id.Value,5}  {j.Status,-12} {Trunc(j.Name, 32),-32} {Trunc(tags, 17),-17} " +
							  $"{Fmt(j.EnqueuedUtc),-19} {Fmt(j.StartedUtc),-19} {Fmt(j.CompletedUtc),-19}");
		}

		static string Fmt(DateTimeOffset? dto)
			=> dto?.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "-";

		static string Trunc(string s, int n)
			=> s.Length <= n ? s : s[..(n - 1)] + "…";
	}
}
