using TKSM.Abstractions.Kernel;

namespace TKSM.Abstractions.Scheduling;

public sealed record JobEnqueued(JobId Id, string Name, JobPriority Priority, string[] Tags, DateTimeOffset Ts) : IKernelEvent;
public sealed record JobStarted(JobId Id, string Name, DateTimeOffset Ts) : IKernelEvent;
public sealed record JobSucceeded(JobId Id, string Name, DateTimeOffset Ts) : IKernelEvent;
public sealed record JobFailed(JobId Id, string Name, string Error, DateTimeOffset Ts) : IKernelEvent;
public sealed record JobCancelled(JobId Id, string Name, DateTimeOffset Ts) : IKernelEvent;