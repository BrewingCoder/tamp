# ADR 0002: Package naming convention

* Status: Accepted
* Date: 2026-05-09
* Deciders: scott
* Tracking: TAM-8

## Context and Problem Statement

Tamp's value proposition depends on tool wrappers shipping as independently-versioned NuGet packages, decoupled from `Tamp.Core` and from each other. The package naming scheme is the contract publishers, consumers, and build authors all depend on. It must be picked once and held: a rename after publication is a breaking change for every project that referenced the old name.

The scheme has to answer four questions:

1. How do we make Tamp packages recognisable at a glance?
2. Where (if anywhere) does the wrapped tool's major version appear in the package name?
3. When does a new tool major mean a new package vs. a runtime branch inside the existing one?
4. How do we discourage the failure mode that killed NUKE — every wrapper bottlenecking on one repo's release cadence?

## Decision Drivers

* **No forced flag day.** When `dotnet 11` ships, projects on `dotnet 10` must not be jostled into upgrading their wrappers to keep getting bug fixes.
* **Stable names.** A package name, once published, is permanent. Revising the convention later is dramatically more expensive than over-thinking it now.
* **Forkability.** A community contributor must be able to publish `Tamp.SomeTool.V2` without coordinating with core maintainers. The naming convention is what makes the namespace self-organising.
* **Avoid fragmentation theatre.** Pinning every wrapped-tool major in the package name when the CLI surface didn't actually change creates ecosystem churn for no benefit.
* **Clarity at the consumer.** A `<PackageReference>` line should make the wrapped tool's expected major version obvious without consulting docs.

## Considered Options

1. **Pin the wrapped tool's major version in every package name** — `Tamp.NetCli.V10`, `Tamp.Yarn.V4`, `Tamp.Kubectl.V1`, etc., always.
2. **Never pin the wrapped tool's version in the name** — single package per tool family (`Tamp.NetCli`); branch on the tool's actual version at runtime; major bumps live in NuGet semver.
3. **Pin only when the wrapped tool's CLI surface breaks across majors** — `Tamp.NetCli.V10` (because `dotnet 10` will not be wire-compatible with future majors of `dotnet`'s CLI surface), `Tamp.Yarn` (because Yarn's CLI is stable across majors).
4. **Pin minor versions or per-feature flags** — `Tamp.NetCli.V10_0_100` etc.

## Decision Outcome

**Chosen: Option 3 — pin in the name only when the wrapped tool's CLI surface breaks across majors.**

The full convention is:

```
Tamp.{ToolFamily}{.V{Major}}?
```

* `Tamp` — fixed brand prefix.
* `{ToolFamily}` — Pascal-case identifier for what's being wrapped: `Core`, `Cli`, `NetCli`, `Docker`, `SonarQube`, `Yarn`, `Turbo`, `Pac`, `Kubectl`.
* `.V{Major}` — present **only when** the wrapped tool's CLI surface materially breaks across major versions. The number tracks the *wrapped tool's* major, not the package's own NuGet version.

The decision rule for whether a new tool major requires a new package is one question:

> Does the new tool major break wrapper code that worked against the previous major?

If yes (CLI flag removed, semantics changed, output schema changed): create a new package. If no: keep the existing package and branch on the tool's reported version at runtime inside the wrapper.

The package's own NuGet semver tracks the wrapper's evolution within that line:

```
Package: Tamp.NetCli.V10
  1.0.0   first release
  1.0.1   bug fix in the wrapper itself
  1.1.0   wrapper feature add
  2.0.0   wrapper API breaking change (still wraps .NET 10)
```

When `dotnet 11` ships and breaks the wrapper, a new package `Tamp.NetCli.V11` opens at `1.0.0`. Projects on .NET 10 keep referencing `Tamp.NetCli.V10` and never get nudged.

### Examples

| Package                | What it wraps                              | Why this name                               |
|------------------------|--------------------------------------------|---------------------------------------------|
| `Tamp.Core`            | The framework itself                       | No tool to pin                              |
| `Tamp.Cli`             | The global tool                            | No tool to pin                              |
| `Tamp.NetCli.V10`      | .NET 10 SDK CLI                            | Pinned: `dotnet` majors break wrappers      |
| `Tamp.NetCli.V11`      | .NET 11 SDK CLI                            | Sibling package, separate semver track      |
| `Tamp.Docker.V27`      | Docker 27.x CLI                            | Pinned: Docker majors break wrappers        |
| `Tamp.SonarQube.V10`   | SonarScanner targeting SonarQube 10.x      | Pinned: scanner CLI shifts across majors    |
| `Tamp.Yarn`            | Yarn CLI                                   | Unpinned: surface stable across majors      |
| `Tamp.Turbo.V2`        | Turborepo 2.x                              | Pinned: Turbo 1 → 2 broke wrapper code      |
| `Tamp.Pac`             | Power Platform CLI                         | Unpinned: stable enough                     |
| `Tamp.Kubectl`         | kubectl                                    | Unpinned: deliberately backward-compatible  |

## Consequences

### Positive

* `dotnet add package Tamp.NetCli.V10` is self-documenting: the consumer sees which `dotnet` major is being targeted before reading anything.
* No forced upgrade path. A team frozen on .NET 10 can keep getting `Tamp.NetCli.V10` patches indefinitely.
* The naming rule is a one-question test, not a judgment call — easier to apply consistently across the ecosystem.
* Community contributors can publish `Tamp.<X>.V<N>` without core-team coordination. The namespace self-organises.

### Negative

* Two packages per tool family during multi-major support windows means doubled CI matrices, doubled changelog noise, and the obligation to backport.
* "Does the new major break the wrapper?" is occasionally judgment-call territory (a single deprecated flag may not warrant a new package). We accept that ambiguity rather than the worse alternative of pinning every major reflexively.
* Within-major variation handled by runtime branching pushes complexity into wrapper code rather than the package graph. We accept that — branching on `--version` output is a normal ten-line code path, not a structural problem.

### Neutral / future-facing

* This convention makes no claim about what `Tamp.{ToolFamily}` *without* a `.V{N}` should do when the upstream tool eventually breaks. The answer at that point will be: introduce `.V{N}`, deprecate the old, supersede with a follow-up ADR.
* Namespace-squatting concerns and the official-vs-community line are out of scope here and tracked separately under ADR 0009 (Governance and namespace policy).

## Pros and Cons of the Options

### Option 1 — Pin every major

* Pro: zero ambiguity in the rule.
* Con: forces a new package every time `kubectl` releases a major even when nothing breaks; ecosystem-wide busywork.
* Con: doubles the publishing matrix for tools that didn't need it.

### Option 2 — Never pin

* Pro: smallest namespace; one package per tool family.
* Pro: simplest publisher experience.
* Con: every breaking upstream major becomes a flag day for every consumer at once. This is the failure mode that broke NUKE; we're explicitly designing against it.

### Option 3 — Pin only when the CLI surface breaks (chosen)

* Pro: matches semver intent — package boundary tracks consumer-visible compatibility.
* Pro: rule is a single question.
* Con: requires a judgment call at the boundary (is this break "material"?).
* Con: occasional churn when a tool we expected to stay stable surprises us.

### Option 4 — Pin minor versions

* Con: absurd. Cataloguing rejected for completeness only.

## Links

* Design doc: project README `## Package Convention` (the surface this ADR formalises).
* Tracker: TAM-8.
* Governance and namespace policy: ADR 0009 (deferred).
