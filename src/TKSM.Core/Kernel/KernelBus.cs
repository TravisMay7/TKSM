using System.Collections.Concurrent;
using TKSM.Abstractions.Kernel;

namespace TKSM.Core.Kernel;

public sealed class KernelBus : IKernelBus
{
	private readonly ConcurrentDictionary<Type, List<Delegate>> _subs = new();

	public IDisposable Subscribe<T>(Action<T> onEvent) where T : IKernelEvent
	{
		var list = _subs.GetOrAdd(typeof(T), _ => new List<Delegate>());
		lock (list) list.Add(onEvent);

		return new Unsub(() =>
		{
			lock (list) list.Remove(onEvent);
		});
	}

	public void Publish<T>(T ev) where T : IKernelEvent
	{
		if (!_subs.TryGetValue(typeof(T), out var list)) return;
		Delegate[] copy;
		lock (list) copy = list.ToArray();
		foreach (var d in copy)
		{
			try { ((Action<T>)d)(ev); } catch { /* swallow */ }
		}
	}

	private sealed class Unsub : IDisposable
	{
		private readonly Action _a; private int _done;
		public Unsub(Action a) => _a = a;
		public void Dispose() { if (Interlocked.Exchange(ref _done, 1) == 0) _a(); }
	}
}
