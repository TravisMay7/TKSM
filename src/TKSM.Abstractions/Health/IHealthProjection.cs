namespace TKSM.Abstractions.Health;

public interface IHealthProjection
{
	DateTimeOffset? StartedUtc { get; }
	DateTimeOffset? LastHeartbeatUtc { get; }
	bool IsHealthy { get; }
}