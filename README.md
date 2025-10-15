# TKSM — Scheduler-Centric Kernel

> **Version:** Concept Draft  
> **Author:** Trefoil Data LLC / Travis May  
> **Purpose:** Define the first-principles foundation of the TKSM runtime.

---

## 1. Purpose

**TKSM** exists to provide a **minimal, modular, verifiable, and observable runtime** capable of hosting arbitrary extensions (plugins, jobs, and states) in a **deterministic and inspectable** way.

It is the **kernel and backplane** of the Trefoil ecosystem — the foundation on which CLI, web, and service hosts can all build.

---

## 2. Core Principles

1. **Everything is observable** — every event, decision, and transition is logged and replayable.  
2. **Nothing is hard-coded** — all behavior flows from registered components.  
3. **Minimal viable kernel** — only the scheduler is always-on; all else is modular.  
4. **Deterministic lifecycle** — startup, run, drain, and shutdown are explicit jobs.  
5. **Extensibility is a contract** — all modules conform to stable abstractions.  
6. **Trust first** — signatures, authorship, and metadata verification precede execution.

---

## 3. Layered Model

| Layer | Role | Examples |
|-------|------|-----------|
| **Abstractions** | Public contracts defining kernel expectations | `IEventLogger`, `IPlugin`, `IJobHandler`, `ITrustPolicy` |
| **Core** | Core implementations and registries | Scheduler, Event Log, State Machine, Plugin Manager |
| **Host** | Bootstrapping, DI, startup orchestration | CLI Host, Daemon Host, Web Host |
| **Extensions** | Optional, pluggable systems | File Sink, Authority, Users, Documents |

---

## 4. Scheduler-First Architecture

### One Beating Heart
The **Job Scheduler** is the only always-on subsystem.  
Everything else — including logging, plugins, CLI, trust, and health — is represented as **jobs** managed by this scheduler.

### The Job Model
Each job is defined by a compact contract:

```
{ Id, Kind, Priority, Inputs, Deadline, RetryPolicy, CapabilityClaims, Affinity, Version }
```

- **Idempotent** and **replayable**  
- **Isolated** (reads from VarStore, writes via events)  
- **Observable** (every state change is logged)

---

## 5. Event Spine & Catch-Up

- **Append-only Event Log**: captures all kernel and job events.
- **Per-Job Inboxes**: allow catch-up and replay on reload or restart.
- **Projections**: derived views like Health, Metrics, and State can be rebuilt anytime from the event stream.

---

## 6. VarStore — Versioned Shared Memory

- **Versioned Key-Value Store** with `etag` and metadata.
- **Transactional Apply** for atomic updates.  
- **Layered Sources** (Environment → Config Files → Plugins → Runtime).
- **Change Propagation** via event stream (reactive updates).

---

## 7. Hot-Swap Protocol

Subsystems and plugins follow a strict reload lifecycle:

1. **Quiesce** — stop admitting new work.  
2. **Drain** — finish or checkpoint active work.  
3. **Handoff** — transfer state/inboxes to replacement.  
4. **Cutover** — route new work to the new version.  
5. **Retire** — unload or archive old instance.

Hot reloads are safe, deterministic, and observable.

---

## 8. Trust at the Scheduler Edge

- **Admission Control**: jobs are authorized by Trust Policy before enqueue.  
- **Plugin activation** is itself a scheduled job with failure isolation.  
- **Sandboxing**: plugins execute under declared capability sets (`file:write`, `net:outbound`, etc.).

---

## 9. Scheduling Policies

- **Priority Queues** and per-capability pools  
- **Timing Modes**: `RunNow`, `RunAt(T)`, `RunEvery(spec)`  
- **Backpressure & Fairness**: quotas per origin to avoid starvation  
- **Leases**: long-running jobs maintain heartbeats; on timeout, work is reassigned

---

## 10. Observability Contract

- **Everything emits events** — no silent subsystems.  
- **Structured logs** with correlation IDs (`jobId`, `pluginId`, `spanId`, etc.).  
- **Replayable timelines** — reconstruct health, metrics, and state from logs.  
- **Projections are disposable** — rebuild any view from source events.

---

## 11. Kernel Boot as a Job Graph

Startup itself is a **graph of supervised jobs**:

1. Start **Scheduler Core**  
2. Mount **VarStore**  
3. Start **Event Spine**  
4. Build **Config Projection**  
5. Warm-up **Trust / Authority**  
6. **Discover Plugins**  
7. **Verify & Activate Plugins**  
8. Start **Health, CLI, and Management** jobs  

Each phase is observable, retriable, and fault-tolerant.

---

## 12. Failure Semantics

- **Supervisor Trees** with restart strategies (`one_for_one`, `one_for_all`, etc.)  
- **Retry Policies**: exponential backoff, jitter, and circuit breakers  
- **Escalation Policy**: persistent offenders get demoted or sandboxed  
- **Never Panic**: kernel always degrades gracefully, never crashes

---

## 13. Capabilities & Sandboxes

- Jobs request **capability tokens** for side effects.  
- **No token = no effect.**  
- Security scales from soft policy to full process isolation.

---

## 14. Runtime Modes

| Mode | Description |
|------|--------------|
| **CLI Mode** | Foreground shell binding STDIN/OUT; command jobs and projections. |
| **Daemon Mode** | Background service with health endpoints and job loop. |
| **Hybrid Mode** | Both; switchable at runtime via VarStore flag. |

---

## 15. Kernel-Resident Responsibilities

All seven original systems remain, but expressed through jobs:

| Responsibility | Expression |
|----------------|-------------|
| Event System | Event Log & projections as scheduled jobs |
| State System | Scheduler orchestrates kernel states |
| Plugin System | Discovery, verify, activate via job pipelines |
| Scheduler | Always-on core |
| Health Projection | Projection job over event + VarStore streams |
| VarStore | Mounted early; mutations as transactional jobs |
| Configuration | Layered VarStore namespace with live updates |

---

## 16. Minimal Working Surface (MWS)

To validate the model:

1. Scheduler Core with queues, retries, and leases.  
2. In-Memory Event Log + Health Projection.  
3. VarStore v1 with versioned KV.  
4. Plugin Discovery → Verify → Activate pipeline.  
5. Hot-Swap demo (e.g., reload a subsystem safely).  
6. CLI as a job: submit “var set/get”, “plugin list”, “health”.

---

## 17. Summary

The TKSM Kernel is a **scheduler-first architecture**: one living heartbeat coordinating a world of modular, verifiable, reloadable subsystems.  
Everything else is just a job.

---

## 18. Namespace → folder rules (simple and future-proof)

TKSM.Abstractions.* → src/TKSM.Abstractions/**

TKSM.Core.* → src/TKSM.Core/**

TKSM.Observability.* → src/TKSM.Observability/**

TKSM.Extensibility.* → src/TKSM.Extensibility/**

TKSM.Host.Cli.* → src/TKSM.Host.Cli/**

TKSM.Host.Daemon.* → src/TKSM.Host.Daemon/**

TKSM.Host.Web.* → src/TKSM.Host.Web/** (only when needed)

TKSM.Tools.* → src/TKSM.Tools/**

Samples and test projects mirror the same namespace boundaries.