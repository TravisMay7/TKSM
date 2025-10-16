public interface IEvent
{
	long Seq { get; }
	DateTimeOffset Timestamp { get; }
	string Kind { get; }
	string Source { get; }
	IReadOnlyDictionary<string, object?>? Payload { get; }
	string? CausalityId { get; }
}
