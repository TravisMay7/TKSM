using System.Reflection.Metadata;
using TKSM.Abstractions.Kernel;
using TKSM.Abstractions.Scheduling;
using TKSM.Core.Boot;
using TKSM.Core.Observability;
using TKSM.Core.Scheduling;
using TKSM.Host.Cli;

var host = KernelHost
	.Create()
	.UseInMemoryEventLogger()
	.UseKernelBus()
	.UseEventSpine()
	.UseLayeredVarStore()
	.UseWorkScheduler()
	.UseJobsProjection()
	.UseHealthProjection()
	.UseMetricsProjection()
	.Start();

using var mux = new ConsoleMux("> ");

var tail = new LogTail(host.Logger, mux);
tail.Start(); // default on

BusWatcher? busWatch = null;
JobsWatcher? watcher = null;

mux.Start(async line =>
{
	var cmd = (line ?? "").Trim();
	if (cmd.Length == 0) return;

	switch (cmd)
	{
		case "quit":
		case "exit":
			host.Stop();
			break;

		case var s when s.StartsWith("bus emit "):
			{
				// bus emit {name} {optional payload text}
				var parts = s.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length < 3) { mux.WriteLine("usage: bus emit {name} {payload}"); break; }
				var name = parts[2].Contains(' ') ? parts[2][..parts[2].IndexOf(' ')] : parts[2];
				var payload = s.Substring(s.IndexOf(name, StringComparison.Ordinal) + name.Length).Trim();
				host.Bus.Publish(new CustomEvent(name, payload, DateTimeOffset.UtcNow));
				mux.WriteLine($"bus: emitted {name}");
				break;
			}

		case "bus watch":
			busWatch?.Dispose(); busWatch = new BusWatcher(host.Bus, mux);
			mux.WriteLine("bus: watching (Job*, VarCommitted)...");
			break;

		case "bus stop":
			busWatch?.Dispose(); busWatch = null;
			mux.WriteLine("bus: watch stopped.");
			break;

		case "health":
			mux.WriteLine($"Health: started={host.Health.StartedUtc?.ToString("HH:mm:ss") ?? "-"} " +
						  $"lastBeat={host.Health.LastHeartbeatUtc?.ToString("HH:mm:ss") ?? "-"} " +
						  $"ok={host.Health.IsHealthy}");
			break;

		case "jobs":
			foreach (var j in host.Jobs.List().OrderBy(j => j.EnqueuedUtc))
				mux.WriteLine($"{j.Id.Value,4} {j.Status,-10} {j.Name} [{string.Join(',', j.Tags)}]");
			break;

		case "jobs watch":
			watcher?.Dispose();
			watcher = new JobsWatcher(host.Bus, mux);
			mux.WriteLine("jobs: watching (JobEnqueued/Started/Succeeded/Failed/Cancelled)...");
			break;

		case "jobs stop":
			watcher?.Dispose(); watcher = null;
			mux.WriteLine("jobs: watch stopped.");
			break;

		case var s when s.StartsWith("jobs submit "):
			{
				if (host.Scheduler is not WorkScheduler sched) break;
				var rest = s["jobs submit ".Length..].Trim();

				if (rest.StartsWith("echo "))
				{
					var msg = rest["echo ".Length..];
					sched.Enqueue(async ct => { host.Logger.Debug("Echo", msg); await Task.CompletedTask; }, "echo", JobPriority.Low, new[] { "demo" });
					mux.WriteLine("queued: echo");
				}
				else if (rest.StartsWith("sleep "))
				{
					if (int.TryParse(rest["sleep ".Length..], out var ms) && ms >= 0)
					{
						sched.Enqueue(async ct => { await Task.Delay(ms, ct); host.Logger.Debug("Sleep", $"slept {ms}ms"); }, $"sleep.{ms}", JobPriority.Low, new[] { "demo" });
						mux.WriteLine($"queued: sleep {ms}ms");
					}
					else mux.WriteLine("usage: jobs submit sleep {ms}");
				}
				else if (rest.StartsWith("set "))
				{
					// jobs submit set {key} to {value}
					var r = rest["set ".Length..];
					var toIdx = r.IndexOf(" to ", StringComparison.OrdinalIgnoreCase);
					if (toIdx < 1) { mux.WriteLine("usage: jobs submit set {key} to {value}"); break; }
					var key = r[..toIdx].Trim();
					var val = r[(toIdx + 4)..].Trim();
					sched.Enqueue(async ct => { using var tx = host.Vars.BeginTransaction(); tx.Set(key, val); tx.Commit(); await Task.CompletedTask; }, $"vars.set.{key}", JobPriority.Normal, new[] { "vars", "demo" });
					mux.WriteLine($"queued: set {key}");
				}
				else mux.WriteLine("usage: jobs submit echo {text} | sleep {ms} | set {key} to {value}");
				break;
			}

		case "tail on":
			tail.Start(); mux.WriteLine("log tail: on"); break;

		case "tail off":
			tail.Stop(); mux.WriteLine("log tail: off"); break;

		case "metrics":
			{
				var m = host.Metrics;
				mux.WriteLine($"total={m.Total} ok={m.Succeeded} fail={m.Failed} cancel={m.Cancelled} avg={(m.AvgDuration?.TotalMilliseconds.ToString("0") ?? "-")}ms");
				foreach (var kv in m.ByName.OrderBy(k => k.Key))
					mux.WriteLine($" - {kv.Key}: n={kv.Value.count} avg={(kv.Value.avg.TotalMilliseconds.ToString("0") ?? "-")}ms");
				break;
			}

		case var s when s.StartsWith("vars get "):
			{
				var key = s["vars get ".Length..].Trim();
				var v = host.Vars.Get(key);
				mux.WriteLine($"{key} = {v ?? "(null)"}");
				break;
			}

		case var s when s.StartsWith("vars set "):
			{
				// syntax: vars set {key} to {value}
				var rest = s["vars set ".Length..];
				var toIdx = rest.IndexOf(" to ", StringComparison.OrdinalIgnoreCase);
				if (toIdx < 1) { mux.WriteLine("usage: vars set {key} to {value}"); break; }
				var key = rest[..toIdx].Trim();
				var value = rest[(toIdx + 4)..].Trim();

				if (host.Scheduler is WorkScheduler sched)
				{
					sched.Enqueue(async ct =>
					{
						using var tx = host.Vars.BeginTransaction();
						tx.Set(key, value);
						tx.Commit();
						host.Logger.Debug("Vars", $"set {key} -> \"{value}\"");
						await Task.CompletedTask;
					}, $"vars.set.{key}", JobPriority.Normal, new[] { "vars" });
					mux.WriteLine($"queued: set {key}");
				}
				break;
			}

		default:
			mux.WriteLine($"unknown: {cmd}");
			break;
	}

	await Task.CompletedTask;
});

Console.CancelKeyPress += (_, a) => { a.Cancel = true; host.Stop(); };
await host.WaitAsync();
watcher?.Dispose();
tail.Dispose();
