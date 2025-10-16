using System.Collections.Concurrent;
using TKSM.Abstractions.Kernel;
using TKSM.Abstractions.VarStore;

namespace TKSM.Core.VarStore;

public sealed class LayeredVarStore : IVarStore
{
	private readonly ConcurrentDictionary<string, object?> _data =
		new(StringComparer.OrdinalIgnoreCase);

	public LayeredVarStore(IKernelBus bus)
	{
	}

	public IVarTxn BeginTransaction() => new Txn(this);

	public object? Get(string key) => _data.TryGetValue(key, out var v) ? v : null;

	// Optional direct set (no txn)
	public void Set(string key, object? value) => _data[key] = value;

	private sealed class Txn : IVarTxn
	{
		private readonly LayeredVarStore _store;
		private readonly Dictionary<string, object?> _staged =
			new(StringComparer.OrdinalIgnoreCase);
		private bool _done;

		public Txn(LayeredVarStore store) => _store = store;

		public void Set(string key, object? value)
		{
			if (_done) throw new ObjectDisposedException(nameof(Txn));
			_staged[key] = value;
		}

		public void Commit()
		{
			if (_done) return;

			foreach (var kvp in _staged)
				_store._data[kvp.Key] = kvp.Value;

			_done = true;
		}

		public void Rollback()
		{
			_staged.Clear();
			_done = true;
		}

		public void Dispose()
		{
			if (!_done) Rollback();
		}
	}
}
