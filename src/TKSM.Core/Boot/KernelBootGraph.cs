using TKSM.Abstractions.Observability;
using TKSM.Abstractions.Scheduling;
using TKSM.Abstractions.VarStore;

namespace TKSM.Core.Boot;

internal static class KernelBootGraph
{
	internal sealed class Handles : IDisposable
	{
		public JobId SeedId { get; init; }
		public JobId HelloId { get; init; }
		public IDisposable? Heartbeat { get; init; }

		public void Dispose() => Heartbeat?.Dispose();
	}

	/// <summary>
	/// Schedule the kernel boot jobs and return handles (incl. heartbeat stopper).
	/// </summary>
	public static Handles Schedule(IScheduler scheduler, IEventLogger log, IVarStore vars)
	{
		// 1) seed vars
		var jSeed = scheduler.Enqueue(async ct =>
		{
			using var tx = vars.BeginTransaction();
			tx.Set("vars/kernel/startedUtc", DateTimeOffset.UtcNow);
			tx.Commit();
			await Task.CompletedTask;
		},
		name: "kernel.seed.startedUtc",
		priority: JobPriority.High,
		tags: ["boot", "vars"]);

		// 2) hello depends on seed
		var jHello = scheduler.Enqueue(async ct =>
		{
			log.Write(LogLevel.Information, "Boot", "Host boot tasks starting...");
			await Task.CompletedTask;
		},
		name: "kernel.hello",
		priority: JobPriority.Normal,
		tags: ["boot"],
		dependsOn: [jSeed.Id]);

		// 3) heartbeat (recurring) after hello (dependency guarantees we start after)
		var hb = (scheduler as Scheduling.WorkScheduler)?.EnqueueRecurring(
			initialDelay: TimeSpan.FromSeconds(1),
			period: TimeSpan.FromSeconds(5),
			work: ct =>
			{
				log.Write(LogLevel.Debug, "Beat", "tick");
				return Task.CompletedTask;
			},
			name: "kernel.heartbeat",
			priority: JobPriority.Low
		);

		return new Handles { SeedId = jSeed.Id, HelloId = jHello.Id };
	}
}
