# ADR 0012: Declarative resource consumption (`Shared` / `Exclusive`)

* Status: Accepted (surface in v1; runtime enforcement deferred)
* Date: 2026-05-11
* Deciders: scott
* Tracking: TAM-18

## Context and Problem Statement

Build targets compete for resources — the NuGet cache, the dotnet build cache, the local Docker daemon, the filesystem, the network, named external services (a SQL Server instance, an integration test database, a Service Bus emulator). When targets run in parallel, two-targets-touching-the-same-resource is often the source of:

* Race conditions (concurrent writes to the same artifact).
* Lock contention (Postgres on shared catalog tables; multi-process MSBuild on the same project).
* Transient failures that look like flake but are actually correctness bugs (Docker daemon serializing image-build operations under load).

A build framework that schedules targets in parallel needs a way for targets to *declare* what they consume so the scheduler can avoid conflicts. The question this ADR answers: what does that declaration look like?

## Decision Drivers

* **Declarative beats imperative.** A target that says "I need exclusive access to the dotnet build cache" is easier to reason about and easier to schedule than a target that internally takes a lock.
* **Composable with parallel execution.** Once parallel target execution is real (currently the v0 executor is sequential), the resource declarations become the input to a scheduler. The surface needs to exist before the scheduler so we don't retrofit it.
* **Two access modes are usually enough.** "Multiple targets can share this concurrently" (read-mostly) vs "only one target can hold this at a time" (write or otherwise serializing) covers the common patterns. Multi-level locking (read-shared, write-exclusive, intent-write, etc.) is database-shaped; build tools rarely need that nuance.
* **Resources are values, not strings.** Stringly-typed resources (e.g., `Consumes("dotnet-cache")`) are easy to typo. Typed `Resource` values are checked at compile time.

## Considered Options

1. **Declarative typed resources with Shared / Exclusive modes (chosen).** Targets call `.Consumes(Resource resource, ConsumeMode mode)`. The framework records the declaration; a future scheduler honors it.
2. **Imperative locks taken inside target bodies.** Each target grabs a named lock at the start of its action. Pro: simple, no framework support. Con: not visible to the scheduler — parallel execution naively schedules everything and locks serialize at runtime.
3. **No declarations; rely on the user to ensure compatibility.** Sequential execution by default; user opts into parallelism per target. Pro: minimum framework surface. Con: doesn't help when parallel execution is the goal.

## Decision

Option 1 — **declarative typed resources with two consume modes**.

API surface:

```csharp
.Consumes(Resource.BuildCache.Dotnet, ConsumeMode.Exclusive)
.Consumes(Resource.Network.Internet, ConsumeMode.Shared)
.Consumes(Resource.Filesystem("artifacts"), ConsumeMode.Exclusive)
```

`Resource` is a value type (or sealed-record hierarchy) with categories:
- `Resource.BuildCache.{Dotnet, Yarn, Nuget, MsBuild}` — well-known build caches.
- `Resource.Network.{Internet, Registry(host)}` — network endpoints; `Registry` parameterized by URL/host.
- `Resource.Process.{Docker, MSBuild}` — long-lived processes the host owns.
- `Resource.Filesystem(path)` — a directory or file path.
- `Resource.Custom(name)` — escape hatch for site-specific resources.

`ConsumeMode`:
- `Shared` — multiple targets holding this in Shared mode can run concurrently.
- `Exclusive` — at most one target holding this (in either mode) at a time. Other targets wanting Shared or Exclusive wait.

The framework records the declarations on `TargetSpec.Resources`. The runtime — when parallel scheduling lands — uses them to build a wait graph and schedule targets without conflicts.

## Decision: surface now, enforcement later

**The surface ships in v1; the runtime enforcement is deferred.** The v0 executor is sequential; resource declarations are recorded but don't affect execution order (everything runs in dependency order, one at a time). When parallel execution lands (probably v2), the scheduler honors the declarations without requiring consumers to rewrite their builds.

This is the pattern Tamp uses for multiple surfaces — declare in the data model now, enforce at runtime later. ADR 0015 (target framework strategy) is similar. The wiki notes "forthcoming" against the enforcement-side of these.

## Consequences

* **Positive**: build scripts already in production can declare resource consumption today, knowing the framework will honor it when parallel scheduling lands.
* **Positive**: the surface is consumer-facing and stable — no migration when the scheduler ships.
* **Positive**: the resource categories cover the common cases without forcing users to coin custom names. `Resource.BuildCache.Dotnet` is more discoverable than `Consumes("dotnet-cache")`.
* **Negative**: the surface exists today but doesn't do anything functional. Consumers may use it expecting different behavior. The wiki Build-Script-Authoring page explicitly notes this.
* **Negative**: the v0 sequential executor is artificially slow for builds that COULD parallelize. Targets that don't share resources still execute one at a time. Mitigation: parallel-target execution is on the roadmap; until then, users dispatch wide work (e.g., per-tenant migrations) through composition wrappers like `Tamp.EFCore.V10.EFCoreMigrationFanout` that handle their own internal parallelism.

## Notes

The two-mode (`Shared` / `Exclusive`) surface is deliberately minimal. If real-world usage surfaces patterns the current modes don't cover (e.g., "shared but only N concurrent"), we can extend with a `ConsumeMode.SharedWithMax(int)` shape later — that's additive.

The `Resource.Custom(name)` escape hatch is the extension point for cases the built-in categories don't cover. Two different custom names that happen to collide on the same underlying resource (e.g., two custom names both meaning "the integration-test database") would NOT be detected — the framework treats `Resource.Custom("X")` and `Resource.Custom("Y")` as distinct. Consumers using custom resources should establish naming conventions.
