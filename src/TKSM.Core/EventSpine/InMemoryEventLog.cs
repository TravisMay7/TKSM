using System;
using System.Collections.Generic;
using System.Threading;
using TKSM.Abstractions.Events;

namespace TKSM.Core.EventSpine
{
    /// <summary>Append-only in-memory event spine with basic subscription.</summary>
    public sealed class InMemoryEventLog : IEventLog
    {
        private readonly object _gate = new();
        private long _seq = 0;
        private readonly List<IEvent> _events = new();
        private readonly List<(long fromSeq, Action<IEvent> onEvent)> _subs = new();

        private sealed class E : IEvent
        {
            public long Seq { get; init; }
            public DateTimeOffset Timestamp { get; init; }
            public string Kind { get; init; } = string.Empty;
            public string Source { get; init; } = string.Empty;
            public IReadOnlyDictionary<string, object?>? Payload { get; init; }
            public string? CausalityId { get; init; }
        }

        public long Append(IEvent e)
        {
            lock (_gate)
            {
                long next = ++_seq;
                var ev = new E
                {
                    Seq = next,
                    Timestamp = e.Timestamp == default ? DateTimeOffset.UtcNow : e.Timestamp,
                    Kind = e.Kind,
                    Source = e.Source,
                    Payload = e.Payload,
                    CausalityId = e.CausalityId
                };
                _events.Add(ev);
                foreach (var (from, cb) in _subs)
                    if (next >= from) { try { cb(ev); } catch { } }
                return next;
            }
        }

        public async IAsyncEnumerable<IEvent> ReadFrom(long seq, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            int i;
            lock (_gate) { i = (int)Math.Max(0, seq - 1); }
            while (true)
            {
                IEvent? ev = null;
                lock (_gate)
                {
                    if (i < _events.Count) ev = _events[i++];
                }
                if (ev != null) { yield return ev; }
                else { await Task.Delay(25, ct); if (ct.IsCancellationRequested) yield break; }
            }
        }

        public IDisposable Subscribe(long fromSeq, Action<IEvent> onEvent)
        {
            lock (_gate) { _subs.Add((fromSeq, onEvent)); }
            return new Sub(_subs, (fromSeq, onEvent));
        }

        private sealed class Sub : IDisposable
        {
            private readonly List<(long, Action<IEvent>)> _subs;
            private readonly (long, Action<IEvent>) _entry;
            public Sub(List<(long, Action<IEvent>)> subs, (long, Action<IEvent>) entry) { _subs = subs; _entry = entry; }
            public void Dispose() { lock (_subs) { _subs.Remove(_entry); } }
        }
    }
}
