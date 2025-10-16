using TKSM.Abstractions.Kernel;

namespace TKSM.Abstractions.VarStore;

public sealed record VarCommitted(string Key, object? Value, DateTimeOffset Ts) : IKernelEvent;