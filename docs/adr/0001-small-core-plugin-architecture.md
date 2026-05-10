# ADR 0001: Small core, plugin-driven architecture

* Status: Accepted
* Date: 2026-05-10
* Deciders: scott
* Tracking: TAM-7

## Context and Problem Statement

Tamp's existence is justified by exactly one bet: that the way NUKE was structured — every tool wrapper, every CI integration, every helper class living in one monolithic assembly with one release cadence — is not the right shape for a long-lived community-maintained .NET build framework. The framework's lifecycle stalled when the maintainer's evenings ran out, and the community had no fallback because every wrapper was downstream of one repo's release.

This ADR was deliberately deferred from Phase 0 until v0 had been built. At Phase 0 it would have been an opinion piece. With v0 shipped — `Tamp.Core`, `Tamp.Cli`, three `Tamp.NetCli.V{N}` modules, all separately versioned, all referencing each other through the public NuGet contract — we now know precisely where the seam between Core and modules belongs and which pieces of "framework" go on which side.

The question this ADR answers: **what does "small core" actually mean in Tamp, structurally, and where does the line get drawn?**

## Decision Drivers

The drivers behind small-core are the failure modes a monolithic core invites:

* **Maintainer-bus-factor.** A monolithic core means every tool wrapper's release is gated on the core repo's release cadence. If the maintainer's bandwidth shrinks, the entire ecosystem stalls. Splitting wrappers into independent packages lets each maintain its own pulse.
* **Forced-flag-day upgrades.** When `dotnet 11` ships and `Tamp.NetCli` is monolithic, every consumer is jostled into an upgrade — even those still on `dotnet 10`. With per-major packages, .NET-10 consumers stay on `Tamp.NetCli.V10` indefinitely while V11 ships independently.
* **Surface bloat.** Every tool wrapper added to a monolithic core grows the dependency surface every consumer picks up. A consumer using only `dotnet build` shouldn't transitively depend on Docker integration code.
* **Contribution friction.** A community contributor adding a new wrapper to a monolithic core has to navigate the maintainer's release calendar. As an independent package they can ship whenever they're ready.

A correct answer to "what goes in Core" is *the smallest set of facilities that every wrapper or build script must depend on, with no opinionated tool knowledge*.

## Considered Options

1. **Monolithic core** — every tool wrapper, CI integration, and helper in one assembly. NUKE's status-quo. Rejected by the entire premise of this project.
2. **Smallest possible core** — only what cannot be moved out without breaking abstraction. Maximum forkability. The risk: if Core is too small, every module ends up reimplementing the same primitives.
3. **Goldilocks core** — small but with shared infrastructure that every wrapper would otherwise duplicate. ← chosen.
4. **No core; modules talk to each other directly** — pure ecosystem with no center. Loses the dependency-graph executor and the dry-run-by-construction property; rejected.

## Decision Outcome

**Chosen: Option 3 — a `Tamp.Core` package containing exactly the facilities that have no defensible home anywhere else, every other package referencing Core but not each other.**

The seam is drawn here:

### What lives in `Tamp.Core`

After v0 is shipped, Core contains exactly these concerns:

| Concern                              | Files (v0)                                                         |
|--------------------------------------|--------------------------------------------------------------------|
| Build-class authoring (DSL)          | `TampBuild.cs`, `Target.cs`, `Phase.cs`, `Resource.cs`, `Backoff.cs`, `Configuration.cs`, `ParameterAttribute.cs` |
| Parameter resolution                 | `ParameterBinder.cs`                                               |
| Plan model                           | `CommandPlan.cs`                                                   |
| Secret model + redaction             | `Secret.cs`, `RedactionTable.cs`, `RedactingTextWriter.cs`         |
| Host detection                       | `HostProfile.cs`, `HostProfileBuilder.cs`                          |
| Target DAG + execution               | `TargetGraph.cs`, `Executor.cs`, `ExecutionMode.cs`, `ProcessRunner.cs` |

What unifies these: every module would have to either implement them or duplicate them. The DSL surface is the user contract; the plan model is the wrapper-to-runner contract; redaction is correctness across modules; host detection is what every wrapper reads to decide if it can run; the executor is the single point of truth for what runs and in what order. Putting any of them in a module would either fragment them across modules or pin every module to one module's release.

### What does NOT live in Core

* Knowledge of any specific tool. `Tamp.Core` does not know what `dotnet`, `docker`, or `kubectl` are. `DotNet.Build()` lives in `Tamp.NetCli.V10`, full stop. Adding `Tamp.Docker.V27` will not require a Core change, only a new package referencing Core.
* CI YAML generation. A future `Tamp.Pipelines.GitHubActions` (or similar) would emit YAML; Core would not.
* IDE configuration emitters (`tasks.json`, `launch.json`). A future `Tamp.IDE` package owns those; Core would not.
* Process execution policy beyond what the v0 sequential executor covers. Resource scheduling, parallelism, retry-with-backoff, capability preflight are all *recorded* on `TargetSpec` but the v0 executor consumes them minimally; the eventual richer executor still belongs in Core (it's the user contract for execution semantics), but is built up additively from the v0 surface.
* Schema-driven wrapper generation tooling. ADR 0013 (deferred) governs this. The codegen lives in `Tamp.Tooling.Generator` or similar, not Core.

### What modules look like, in this architecture

A first-party module — `Tamp.NetCli.V10` is the worked example — is shaped like:

* A NuGet package whose name encodes the wrapped tool's major (per ADR 0002) when relevant.
* A reference to `Tamp.Core` as a `<PackageReference>` (or `<ProjectReference>` in this monorepo per ADR 0006).
* Public types organised around the wrapped tool's verbs: a settings record per command, a fluent configurer, a `ToCommandPlan()` that emits a `CommandPlan` Core's runner can dispatch.
* Zero shared state between modules. `Tamp.Docker.V27` and `Tamp.NetCli.V10` are independent and do not reference each other.

Modules are *thicker* than Core in code volume (each wrapper carries a verb-by-verb settings surface for the tool it wraps), but they're independently sliceable: nothing in Core knows or cares about any module's internals.

### What this looks like at the dependency level

```
                ┌─────────────┐
                │  Tamp.Core  │
                └──────┬──────┘
                       │
        ┌──────────────┼──────────────┬───────────────────┐
        │              │              │                   │
┌───────▼──────┐ ┌─────▼────┐ ┌───────▼──────┐  ┌─────────▼────────┐
│ Tamp.NetCli  │ │ Tamp.Cli │ │ Tamp.NetCli  │  │ (future modules) │
│      .V8     │ │          │ │      .V10    │  │ Docker, Yarn, …  │
└──────────────┘ └──────────┘ └──────────────┘  └──────────────────┘
```

Every module references Core. No module references another module. `Tamp.Cli` references Core for the same reason: it forwards CLI invocations into a build script that uses Core's `TampBuild.Execute<T>` entry, but it has no tool-specific knowledge of its own.

### Validation: the v0 evidence

This ADR is being written against shipped code, so the question "is the seam drawn correctly?" has empirical answers:

* `Tamp.Core` builds and tests pass with no module dependencies — confirmed by `Tamp.Core.Tests` running 210 tests against Core alone.
* Adding the V10 module required no Core changes — confirmed by the V10 module's source files (only DotNet wrapper surface; no edits to Core types).
* Cloning V10 to V9 and V8 was a `sed` away from byte-for-byte identical — confirmed by the actual Phase-1 commit.
* The CLI tool is a thin process-spawn around `dotnet run --project <build>` — confirmed by `Tamp.Cli/Program.cs` being roughly 80 lines of dispatch code.
* User build scripts compile against Core only and pull in modules they actually use — verified through the `ExecuteEntryTests` test suite which exercises `TampBuild.Execute<T>` end-to-end without referencing any module.

The seam holds.

## Consequences

### Positive

* **Lifecycle independence.** A `dotnet 11` ship date is not a Tamp release date. A new `Tamp.NetCli.V11` package opens a fresh semver track without touching Core.
* **Contributor onboarding scales linearly with surface area, not core complexity.** Adding `Tamp.Yarn` is a self-contained PR; the contributor doesn't need to understand the executor or the redaction system.
* **Consumer dependency graph is exactly the modules they pick.** A build that uses only `dotnet` pulls in Core + V10. It does not transitively pull in Docker integration.
* **Forking the project preserves modules.** If Core ever stalls, individual modules are still useful and individually maintainable.
* **The architecture is the resilience strategy.** This is the headline framing from the README; the code now backs it up.

### Negative

* **Cross-module changes are coordinated.** When a Core API evolves in a way that wrappers need to track, every module repo (or every module project in this monorepo) gets a corresponding update PR. ADR 0006's monorepo choice is what makes this tractable rather than catastrophic.
* **Per-package versioning is a tooling concern.** Independent versioning means per-project changelogs, per-project release tags, and per-project semver discipline. Tamp's release tooling (out of scope for v0) has to handle that fan-out.
* **Some duplication is permanent.** V8/V9/V10 wrappers share ~95% of their code today. ADR 0013's schema-driven codegen will eliminate this but requires its own infrastructure. Until then, the duplication is the cost of independent versioning.

### Neutral / future-facing

* The Core boundary may shift as v1 modules surface needs we haven't yet anticipated. Specifically, `Tamp.Components` (analogous to NUKE's component interfaces — pre-composed target bundles) is a candidate to live somewhere other than Core if it grows opinionated. Successor ADRs may revise this drawing.
* "Smallest core that earns its place" implies an ongoing pruning discipline: any new facility proposed for Core should be tested against the question *"can this live in a module without forcing every module to reinvent it?"* If yes, it goes in a module.

## Pros and Cons of the Options

### Option 1 — Monolithic core

* Pro: simplest dependency graph; one PackageReference and you have everything.
* Pro: cross-cutting refactors are trivial.
* Con: every tool wrapper is gated on one release cadence — the failure mode that broke NUKE.
* Con: surface bloat; consumers pay for code they don't use.
* Con: contributors face the maintainer's bus factor.

### Option 2 — Smallest-possible core

* Pro: maximum forkability.
* Con: high risk of cross-module duplication (every wrapper reimplements parameter binding, redaction, host detection).
* Con: API contracts between modules become fuzzy without a center.

### Option 3 — Goldilocks core (chosen)

* Pro: avoids both the monolith trap and the no-center trap.
* Pro: drawing the line at "must every module depend on this?" produces a defensible, defensible-each-time-asked seam.
* Con: requires ongoing discipline; the wrong answer to "should this go in Core?" creates either bloat or duplication.

### Option 4 — No core

* Pro: maximum decentralisation.
* Con: loses the dependency-graph executor (somebody has to own scheduling).
* Con: loses the dry-run-by-construction property (`CommandPlan` is a Core type that wrappers produce; without Core, every wrapper invents its own plan model).

## Links

* Tracker: TAM-7.
* Naming convention that gives modules their independent identity: ADR 0002.
* Repository layout that makes cross-module changes tractable in a monorepo: ADR 0006.
* License under which both Core and modules are distributed: ADR 0007.
* Governance and namespace policy that lets the ecosystem grow without core-team coordination: ADR 0009.
* TFM strategy that all Core and modules share: ADR 0015.
* Schema-driven codegen that will eliminate cross-module duplication in module wrappers: ADR 0013 (deferred).
