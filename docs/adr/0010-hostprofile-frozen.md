# ADR 0010: HostProfile is built once at startup and frozen

* Status: Accepted
* Date: 2026-05-11
* Deciders: scott
* Tracking: TAM-16

## Context and Problem Statement

Tamp targets can declare host requirements — `RequiresDocker()`, `RequiresNetwork()`, `RequiresAdmin()`, `MemoryBudget()`, `MaxParallelism()`. The framework needs to compare those requirements against what the host actually provides. That requires inspecting the host: OS family, container status, cgroup limits, CPU count, RAM, CI vendor, tool availability.

The question: when does the host get inspected — once at startup, on every target, or lazily on demand — and is the result mutable or immutable thereafter?

## Decision Drivers

* **Inspection has cost.** Detecting Docker availability requires shelling out (`docker info`) which can take 100-500ms. Reading cgroup files is cheap but multiple-redundant calls add up. CI vendor detection involves reading 5-10 env vars. Inspecting on every target wastes time.
* **Consistency across a build.** A target inspecting "is Docker available" at minute 1 and another inspecting it at minute 10 could see different answers (someone stopped the Docker daemon mid-build). For a build to be reproducible, the host view must be consistent for the build's lifetime.
* **Predictability for the build script author.** `HostProfile.OperatingSystem` is read in many places (Build.cs sketch examples, wrapper internals, `[Parameter]` default expressions). The value being stable across reads is a baseline expectation.
* **Testability.** A `HostProfile` instance built at startup can be swapped via a constructor argument or test seam. A lazily-inspected `HostProfile.OperatingSystem` requires mocking the inspection layer per test.

## Considered Options

1. **Inspect once at startup, freeze (chosen).** `HostProfileBuilder.Build()` runs at startup; the result is a single `HostProfile` value accessible via `TampBuild.HostProfile` (or similar). All subsequent reads return the same value.
2. **Lazy inspection on first read.** Each property of `HostProfile` is computed on demand and cached. Reads are cheap after the first; first read of a slow property (Docker) takes the hit.
3. **Re-inspect on demand.** Build scripts opt into a refresh by calling `HostProfile.Refresh()`. Maximum flexibility; high risk of inconsistent reads across a build.

## Decision

Option 1 — **build once at startup, freeze**. The startup path in `TampBuild.Execute<T>(args)`:

1. Parse arguments.
2. `HostProfileBuilder.Build()` — inspect the host. Cost is paid once.
3. Bind parameters and secrets.
4. Run the target graph against the frozen `HostProfile`.

`HostProfile` is a sealed record (immutable by construction). Every property is set at build time:

```csharp
public sealed record HostProfile(
    OsFamily OperatingSystem,
    string ArchName,
    int LogicalCores,
    long TotalMemoryBytes,
    long FreeMemoryBytes,
    bool IsContainerized,
    CgroupLimits? Cgroup,
    CiHost? Ci,
    // ... etc
);
```

The build is paused at "freeze time" — anything that happens after startup uses the cached values. If a target wants to re-inspect (rare), it calls into the public `HostProfileBuilder.Build()` itself and gets a new value, but the framework doesn't expose a hot-reload.

## Consequences

* **Positive**: every target sees the same host view. A build is reproducible against a given host state for its full duration.
* **Positive**: inspection cost is paid once per build. Targets that read `HostProfile.Cgroup.MemoryLimitBytes` 50 times across the run pay one `/proc/self/cgroup` read.
* **Positive**: tests pass a synthetic `HostProfile` into `TampBuild` and exercise capability-requirement logic without touching the OS.
* **Positive**: the host view is part of the build summary / telemetry — printed once at start, identical at end. No "wait, the OS changed?" debugging.
* **Negative**: a target that wants to react to changing host state (e.g., "wait until Docker is up" + then build) has to bypass the cached profile and re-inspect manually. Edge case; not exercised in production builds today.
* **Negative**: long-running builds (8+ hours) where the cgroup limit was actually raised mid-build don't pick up the change. We accept this — the use case is rare and the workaround (relaunch the build) is acceptable.

## Notes

The inspection itself is in `HostProfileBuilder` — see `src/Tamp.Core/HostProfileBuilder.cs` for the per-property detection logic. The CI vendor detection is layered on top (see `src/Tamp.Core/CiHost.cs`) and merges into `HostProfile.Ci`.

The banner printed at the start of every Tamp invocation includes the resolved host stats:

```
Host:     Linux x64 · 4 cores · 15.6 GB total / 15.6 GB free
Runtime:  .NET 10.0.7 · Runner: GitHub Actions
```

That banner is the on-screen evidence of the frozen profile. If a build is suspicious, that line is the first thing to check.
