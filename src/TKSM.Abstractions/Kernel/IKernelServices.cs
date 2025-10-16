namespace TKSM.Abstractions.Kernel;

public interface IKernelServices { }

public interface IKernelEvent { DateTimeOffset Ts { get; } }

public interface IKernelBus
{
	IDisposable Subscribe<T>(Action<T> onEvent) where T : IKernelEvent;
	void Publish<T>(T ev) where T : IKernelEvent;
}

public sealed record CustomEvent(string Name, string? Payload, DateTimeOffset Ts) : IKernelEvent;