using TKSM.Abstractions.Observability;

namespace TKSM.Core.Observability;

public sealed class InMemoryEventLogger : IEventLogger
{
	private readonly object _gate = new();
	private readonly List<Action<ILogEvent>> _subs = new();

	private sealed class Ev : ILogEvent
	{
		public DateTimeOffset Timestamp { get; init; }
		public LogLevel Level { get; init; }
		public string Category { get; init; } = "";
		public string Message { get; init; } = "";
		public IReadOnlyDictionary<string, object?>? Data { get; init; }
	}

	private sealed class Sub : ILogSubscription
	{
		private readonly InMemoryEventLogger _owner;
		private readonly Action<ILogEvent> _cb;
		private bool _disposed;
		public Sub(InMemoryEventLogger owner, Action<ILogEvent> cb)
		{
			_owner = owner; _cb = cb;
		}
		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			lock (_owner._gate) _owner._subs.Remove(_cb);
		}
	}

	public void Write(LogLevel level, string category, string message,
					  IReadOnlyDictionary<string, object?>? data = null)
	{
		ILogEvent ev = new Ev
		{
			Timestamp = DateTimeOffset.UtcNow,
			Level = level,
			Category = category,
			Message = message,
			Data = data
		};

		Action<ILogEvent>[] snap;
		lock (_gate) snap = _subs.ToArray();

		foreach (var s in snap)
		{
			try { s(ev); } catch { /* swallow sink errors */ }
		}
	}

	public ILogSubscription Subscribe(Action<ILogEvent> onEvent)
	{
		lock (_gate) _subs.Add(onEvent);
		return new Sub(this, onEvent);
	}
}

public static class LoggerExtensions
{
	public static void Info(this IEventLogger log, string category, string message) =>
		log.Write(LogLevel.Information, category, message);

	public static void Debug(this IEventLogger log, string category, string message) =>
		log.Write(LogLevel.Debug, category, message);

	public static void Warn(this IEventLogger log, string category, string message) =>
		log.Write(LogLevel.Warning, category, message);

	public static void Error(this IEventLogger log, string category, string message,
		Exception? ex = null) =>
		log.Write(LogLevel.Error, category, message,
			ex is null ? null : new Dictionary<string, object?> { ["exception"] = ex.ToString() });

	/// <summary>Scoped category helper so you can write: using var boot = log.Cat("Boot"); boot.Info("Started");</summary>
	public static CatLog Cat(this IEventLogger log, string category) => new(log, category);

	public readonly struct CatLog
	{
		private readonly IEventLogger _log;
		public string Category { get; }
		public CatLog(IEventLogger log, string category) { _log = log; Category = category; }
		public void Info(string msg) => _log.Write(LogLevel.Information, Category, msg);
		public void Debug(string msg) => _log.Write(LogLevel.Debug, Category, msg);
		public void Warn(string msg) => _log.Write(LogLevel.Warning, Category, msg);
		public void Error(string msg, Exception? ex = null) =>
			LoggerExtensions.Error(_log, Category, msg, ex);
	}
}