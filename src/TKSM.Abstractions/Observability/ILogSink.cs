namespace TKSM.Abstractions.Observability;

// If you have an ILogSink contract, keep it here or in Abstractions:
public interface ILogSink
{
	void Attach(IEventLogger logger);
}
public enum LogLevel { Trace, Debug, Information, Warning, Error, Critical }

public interface ILogEvent
{
	DateTimeOffset Timestamp { get; }
	LogLevel Level { get; }
	string Category { get; }
	string Message { get; }
	IReadOnlyDictionary<string, object?>? Data { get; }
}

public interface ILogSubscription : IDisposable { }

public interface IEventLogger
{
	void Write(LogLevel level, string category, string message,
			   IReadOnlyDictionary<string, object?>? data = null);

	ILogSubscription Subscribe(Action<ILogEvent> onEvent);
}