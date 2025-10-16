using System.Collections.Concurrent;
using System.ComponentModel;
using TKSM.Abstractions.Kernel;
using TKSM.Abstractions.Observability;
using TKSM.Abstractions.Scheduling;

namespace TKSM.Core.Scheduling;

public sealed class WorkScheduler : IScheduler
{
	private readonly IEventLogger _log;

	private long _idSeq;
	private readonly object _gate = new();

	// priority queues (highest first)
	private readonly ConcurrentQueue<Scheduled> _crit = new();
	private readonly ConcurrentQueue<Scheduled> _high = new();
	private readonly ConcurrentQueue<Scheduled> _norm = new();
	private readonly ConcurrentQueue<Scheduled> _low = new();

	private readonly PriorityQueue<Scheduled, long> _timerWheel = new(); // by ticks (UTC)
	private readonly SemaphoreSlim _signal = new(0);

	private readonly ConcurrentDictionary<JobId, JobRecord> _jobs = new();
	// map: dependency -> list of jobs waiting on it
	private readonly ConcurrentDictionary<JobId, ConcurrentBag<JobRecord>> _dependents = new();

	private readonly IKernelBus _bus;

	private volatile bool _running;

	private sealed record Scheduled(JobRecord Job, DateTimeOffset DueUtc);
	private sealed class JobRecord : IJobHandle
	{
		private readonly IEventLogger _log;
		private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

		public JobId Id { get; }
		public string Name { get; }
		public string[] Tags { get; }
		public JobPriority Priority { get; }
		public Func<CancellationToken, Task> Work { get; }
		public CancellationTokenSource Cts { get; } = new();
		public DateTimeOffset EnqueuedUtc { get; } = DateTimeOffset.UtcNow;
		public DateTimeOffset? StartedUtc { get; private set; }
		public DateTimeOffset? CompletedUtc { get; private set; }
		public JobStatus Status { get; private set; } = JobStatus.Queued;
		public double? Progress { get; private set; }

		public int RemainingDeps;              // set when enqueued with deps
		public volatile bool DependencyFailed; // if any dep fails/cancels

		public JobRecord(JobId id, string name, string[] tags, JobPriority prio,
						 Func<CancellationToken, Task> work, IEventLogger log)
		{ Id = id; Name = name; Tags = tags; Priority = prio; Work = work; _log = log; }

		public void SetRunning() { Status = JobStatus.Running; StartedUtc = DateTimeOffset.UtcNow; }
		public void SetProgress(double value) { Progress = value; }
		public void SetResultOk()
		{ Status = JobStatus.Succeeded; CompletedUtc = DateTimeOffset.UtcNow; _tcs.TrySetResult(); }
		public void SetResultCancelled()
		{ Status = JobStatus.Cancelled; CompletedUtc = DateTimeOffset.UtcNow; _tcs.TrySetCanceled(); }
		public void SetResultError(Exception ex)
		{
			Status = JobStatus.Failed; CompletedUtc = DateTimeOffset.UtcNow;
			_tcs.TrySetException(ex);
		}

		public Task AwaitAsync(CancellationToken ct = default) =>
			ct.CanBeCanceled ? Task.WhenAny(_tcs.Task, Task.Delay(Timeout.Infinite, ct)).Unwrap() : _tcs.Task;

		public bool TryCancel()
		{
			if (Status is JobStatus.Queued or JobStatus.Running)
			{
				try { Cts.Cancel(); return true; } catch { }
			}
			return false;
		}
	}

	public WorkScheduler(IEventLogger log, IKernelBus bus) { _log = log; _bus = bus; }

	private void NotifyDependents(JobRecord finished)
	{
		if (!_dependents.TryRemove(finished.Id, out var waiters)) return;

		foreach (var depJob in waiters)
		{
			if (finished.Status is JobStatus.Failed or JobStatus.Cancelled)
				depJob.DependencyFailed = true;

			var left = Interlocked.Decrement(ref depJob.RemainingDeps);

			if (left == 0)
			{
				if (depJob.DependencyFailed)
				{
					depJob.SetResultCancelled();
					_log.Write(LogLevel.Warning, "Scheduler",
						$"Job skipped (dependency failure): {depJob.Name}");
				}
				else
				{
					// it’s due now? if original time was in future, let timers promote it
					QueuePriority(depJob);
					_signal.Release();
				}
			}
		}
	}

	private void RegisterDependencies(JobRecord job, IEnumerable<JobId>? deps)
	{
		if (deps is null) return;
		var count = 0;
		foreach (var depId in deps)
		{
			if (!_jobs.TryGetValue(depId, out var dep)) { job.DependencyFailed = true; continue; }
			_dependents.GetOrAdd(dep.Id, _ => new()).Add(job);
			count++;
		}
		job.RemainingDeps = count;
	}

	internal async Task StartAsync(CancellationToken token)
	{
		_running = true;
		_log.Write(LogLevel.Information, "Scheduler", "WorkScheduler started");
		try
		{
			while (!token.IsCancellationRequested)
			{
				var wait = NextDueDelay();

				// Wait until either a timed job is due OR a new item is signaled
				var delayTask = Task.Delay(wait, token);
				var signalTask = _signal.WaitAsync(token);
				await Task.WhenAny(delayTask, signalTask).ConfigureAwait(false);
				token.ThrowIfCancellationRequested();

				// promote timers that became due
				PromoteDueTimers();

				// now drain one job by priority…
				if (!TryDequeue(out var scheduled))
					continue;

				var job = scheduled.Job;

				try
				{
					job.SetRunning();
					_bus.Publish(new JobStarted(job.Id, job.Name, DateTimeOffset.UtcNow));
					_log.Write(LogLevel.Debug, "Scheduler", $"Executing job: {job.Name}");
					await job.Work(job.Cts.Token).ConfigureAwait(false);
					job.SetResultOk();
					_bus.Publish(new JobSucceeded(job.Id, job.Name, DateTimeOffset.UtcNow));
					NotifyDependents(job);
					_log.Write(LogLevel.Information, "Scheduler", $"Job completed: {job.Name}");
				}
				catch (OperationCanceledException)
				{
					job.SetResultCancelled();
					_bus.Publish(new JobCancelled(job.Id, job.Name, DateTimeOffset.UtcNow));
					_log.Write(LogLevel.Warning, "Scheduler", $"Job cancelled: {job.Name}");
				}
				catch (Exception ex)
				{
					job.SetResultError(ex);
					_bus.Publish(new JobFailed(job.Id, job.Name, ex.Message, DateTimeOffset.UtcNow));
					_log.Write(LogLevel.Error, "Scheduler", $"Job failed: {job.Name} ({ex.Message})",
						new Dictionary<string, object?> { ["exception"] = ex.ToString(), ["jobId"] = job.Id.Value });
				}
			}
		}
		catch (OperationCanceledException) { }
		finally
		{
			_running = false;
			_log.Write(LogLevel.Information, "Scheduler", "WorkScheduler stopped");
		}
	}

	// ----- IScheduler -----
	public IJobHandle Enqueue(Func<CancellationToken, Task> work, string name,
	JobPriority priority = JobPriority.Normal, IEnumerable<string>? tags = null,
	IEnumerable<JobId>? dependsOn = null)
	=> EnqueueAt(DateTimeOffset.UtcNow, work, name, priority, tags, dependsOn);

	public IJobHandle EnqueueAfter(TimeSpan delay, Func<CancellationToken, Task> work, string name,
		JobPriority priority = JobPriority.Normal, IEnumerable<string>? tags = null,
		IEnumerable<JobId>? dependsOn = null)
		=> EnqueueAt(DateTimeOffset.UtcNow.Add(delay), work, name, priority, tags, dependsOn);

	public IJobHandle EnqueueAt(DateTimeOffset whenUtc, Func<CancellationToken, Task> work, string name,
		JobPriority priority = JobPriority.Normal, IEnumerable<string>? tags = null,
		IEnumerable<JobId>? dependsOn = null)
	{
		var id = new JobId(Interlocked.Increment(ref _idSeq));
		var rec = new JobRecord(id, name, (tags ?? Array.Empty<string>()).ToArray(), priority, work, _log);
		_bus.Publish(new JobEnqueued(id, name, priority, rec.Tags, DateTimeOffset.UtcNow));
		_jobs.TryAdd(id, rec);

		RegisterDependencies(rec, dependsOn);

		if (rec.RemainingDeps == 0 && whenUtc <= DateTimeOffset.UtcNow)
			QueuePriority(rec);
		else if (rec.RemainingDeps == 0)
			lock (_gate) _timerWheel.Enqueue(new Scheduled(rec, whenUtc), whenUtc.UtcTicks);
		// else: wait for dependencies; first to release will queue it

		_signal.Release();
		return rec;
	}

	public bool TryGet(JobId id, out IJobHandle handle)
	{
		var ok = _jobs.TryGetValue(id, out var rec);
		handle = rec!;
		return ok;
	}

	public IReadOnlyList<IJobHandle> Snapshot(Func<IJobHandle, bool>? predicate = null)
	{
		var list = new List<IJobHandle>();
		foreach (var kv in _jobs.Values)
			if (predicate is null || predicate(kv)) list.Add(kv);
		return list;
	}

	// ----- Helpers -----

	private void QueuePriority(JobRecord rec) =>
		(rec.Priority switch
		{
			JobPriority.Critical => _crit,
			JobPriority.High => _high,
			JobPriority.Low => _low,
			_ => _norm
		}).Enqueue(new Scheduled(rec, DateTimeOffset.UtcNow));

	private bool TryDequeue(out Scheduled s)
	{
		if (_crit.TryDequeue(out s)) return true;
		if (_high.TryDequeue(out s)) return true;
		if (_norm.TryDequeue(out s)) return true;
		if (_low.TryDequeue(out s)) return true;
		return false;
	}

	private TimeSpan NextDueDelay()
	{
		lock (_gate)
		{
			if (_timerWheel.Count == 0) return TimeSpan.FromMilliseconds(50);
			_timerWheel.TryPeek(out var _, out var ticks);
			var due = new DateTimeOffset(ticks, TimeSpan.Zero);
			var now = DateTimeOffset.UtcNow;
			return due > now ? due - now : TimeSpan.Zero;
		}
	}

	private void PromoteDueTimers()
	{
		lock (_gate)
		{
			var nowTicks = DateTimeOffset.UtcNow.UtcTicks;
			while (_timerWheel.Count > 0 && _timerWheel.TryPeek(out var s, out var ticks) && ticks <= nowTicks)
			{
				_timerWheel.Dequeue();
				QueuePriority(s.Job);
			}
		}
	}

	// QoL: recurring (non-blocking controller)
	public IDisposable EnqueueRecurring(TimeSpan initialDelay, TimeSpan period,
		Func<CancellationToken, Task> work, string name = "recurring", JobPriority priority = JobPriority.Normal, params string[] tags)
	{
		var gate = new CancellationTokenSource();

		Task Controller(CancellationToken _)
		{
			if (gate.IsCancellationRequested) return Task.CompletedTask;
			Enqueue(work, $"{name}@{DateTimeOffset.UtcNow:HHmmss}", priority, tags);
			if (!gate.IsCancellationRequested)
				EnqueueAfter(period, Controller, $"{name}.controller", priority, tags);
			return Task.CompletedTask;
		}

		if (initialDelay > TimeSpan.Zero)
			EnqueueAfter(initialDelay, Controller, $"{name}.controller", priority, tags);
		else
			Enqueue(Controller, $"{name}.controller", priority, tags);

		return new CancelOnlyDisposable(gate);
	}

	private sealed class CancelOnlyDisposable : IDisposable
	{
		private readonly CancellationTokenSource _cts; private int _done;
		public CancelOnlyDisposable(CancellationTokenSource cts) => _cts = cts;
		public void Dispose()
		{
			if (Interlocked.Exchange(ref _done, 1) == 1) return;
			try { _cts.Cancel(); } catch { }
		}
	}
}
