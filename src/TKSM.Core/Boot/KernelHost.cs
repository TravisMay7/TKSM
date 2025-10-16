using TKSM.Abstractions.Events;
using TKSM.Abstractions.Health;
using TKSM.Abstractions.Kernel;
using TKSM.Abstractions.Metrics;
using TKSM.Abstractions.Observability;
using TKSM.Abstractions.Scheduling;
using TKSM.Abstractions.VarStore;
using TKSM.Core.EventSpine;
using TKSM.Core.Health;
using TKSM.Core.Kernel;
using TKSM.Core.Observability;
using TKSM.Core.Scheduling;
using TKSM.Core.VarStore;

namespace TKSM.Core.Boot
{
    /// <summary>
    /// Fluent bootstrap for the in-memory kernel. Start() spins up the scheduler and seeds boot jobs.
    /// </summary>
    public sealed class KernelHost : IKernelServices, IDisposable
    {
		private KernelBootGraph.Handles? _boot;
        private CancellationTokenSource _cts = new();
		private readonly List<IDisposable> _owned = [];
        private Task? _runner;

		public IKernelBus Bus { get; private set; } = default!;
        public IEventLog Events { get; private set; } = default!;
		public IHealthProjection Health { get; private set; } = default!;
		public IJobsProjection Jobs { get; private set; } = default!;
		public IEventLogger Logger { get; private set; } = default!;
		public IMetricsProjection Metrics { get; private set; } = default!;
        public IVarStore Vars { get; private set; } = default!;
        public IScheduler Scheduler { get; private set; } = default!;


		private KernelHost() { }


		public KernelHost AttachSink(ILogSink sink)
		{
			sink.Attach(Logger);
			if (sink is IDisposable d) _owned.Add(d);
			return this;
		}
        public static KernelHost Create() => new();
		public void Dispose()
		{
		}
		public KernelHost Start()
		{
			Logger.Info("Boot", "KernelHost starting");

			// Start scheduler loop
			_runner = (Scheduler as WorkScheduler)!.StartAsync(_cts.Token);

			// Boot job graph
			_boot = KernelBootGraph.Schedule(Scheduler, Logger, Vars);

			return this;
		}
		public void Stop()
		{
			_cts.Cancel();
			// dispose sinks
			foreach (var d in _owned) { try { d.Dispose(); } catch { } }
			_owned.Clear();
			try { _boot?.Dispose(); } catch { /* swallow */ }
		}

		public KernelHost UseEventSpine()
		{
			Events = new InMemoryEventLog();
			return this;
		}
		public KernelHost UseKernelBus()
		{
			Bus = new KernelBus();
			return this;
		}
		public KernelHost UseHealthProjection()
		{
			Health = new HealthProjection(Bus, TimeSpan.FromSeconds(10));
			return this;
		}
		public KernelHost UseInMemoryEventLogger()
		{
			Logger = new InMemoryEventLogger();
			return this;
		}
		public KernelHost UseJobsProjection()
		{
			// Scheduler must be set first
			Jobs = new JobsProjection(Scheduler);
			return this;
		}
		public KernelHost UseLayeredVarStore()
		{
			Vars = new LayeredVarStore(Bus); // changed
			return this;
		}
		public KernelHost UseMetricsProjection()
		{
			Metrics = new MetricsProjection(Bus);
			return this;
		}
		public KernelHost UseWorkScheduler()
		{
			Scheduler = new WorkScheduler(Logger, Bus); // changed
			return this;
		}

        public Task WaitAsync() => _runner ?? Task.CompletedTask;
	}
}
