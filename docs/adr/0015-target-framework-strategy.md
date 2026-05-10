# ADR 0015: Target framework strategy

* Status: Accepted
* Date: 2026-05-09
* Deciders: scott
* Tracking: TAM-21 (ADR), TAM-32 (work item)

## Context and Problem Statement

Tamp ships first-party assemblies (`Tamp.Core`, `Tamp.Cli`, `Tamp.NetCli.V{N}`, future modules) as NuGet packages consumed from build scripts that are themselves .NET console projects. Two questions, one ADR:

1. **Which .NET runtimes do Tamp's own assemblies have to run on?** — i.e., what TFMs do `Tamp.Core` and friends list in `<TargetFrameworks>`?
2. **What's the lifecycle policy** — when do new TFMs get added, when do old ones get dropped?

The answers are surprisingly load-bearing. Tamp's value proposition includes consumers in regulated and locked-down environments — federal contracts where the dev fleet is fixed on .NET 8, client VDIs that haven't approved newer SDKs, partner orgs that intentionally adopted .NET 9 STS to surface early features and plan to roll forward to the next LTS before ship. If Tamp targets only the latest, those consumers can't use it. The architecture is the resilience strategy, but it doesn't help if half the audience can't load the assembly.

The original deferred ADR for this decision said *"`net10.0` only for v0; multi-targeting rules"*. Both halves of that proved wrong on first contact: V8/V9 retroactive support is a v0 must-have, not a future enhancement, and "rules" need to be one rule, not several.

## Decision Drivers

* **Locked-down consumer environments.** Federal, regulated, and corporate-IT-managed dev shops frequently cannot install newer SDKs. The user's `Build.csproj` may be pinned to `net8.0` because that's what the host machine actually has. Every package they reference must support `net8.0` for Tamp to be usable at all.
* **STS adopters are not a fringe case.** Long-term projects routinely start on STS to surface early features, planning to land on the next LTS before shipping to production. Dismissing STS support breaks that workflow and excludes a real consumer cohort.
* **Microsoft owns the calendar.** Whether a TFM is in or out of support is decided by Microsoft, not by us. Inventing our own definition of "supported" is unnecessary and would drift from the ecosystem's actual reality.
* **TFM ≠ module version.** Which .NET runtime a Tamp assembly *runs on* is a separate question from which version of the wrapped tool a *module* targets. `Tamp.NetCli.V8` wraps the `dotnet 8` CLI; the wrapper assembly itself can run on net10. Conflating these is a category error that leads to bad rules.
* **One rule, no judgement calls.** Maintainer overhead must stay weekend-scale (ADR 0009). Whether a TFM is in or out should be answered by checking a Microsoft web page, not by deliberating.

## Considered Options

1. **Single TFM (`net10.0` only).** Smallest support burden; excludes the entire net8 / net9 consumer base.
2. **LTS-only (`net8.0;net10.0`).** Skips STS releases by policy. Simplifies CI matrix slightly. Excludes consumers deliberately targeting STS for early-feature access.
3. **All Microsoft-supported releases (`net8.0;net9.0;net10.0` today, adjusts automatically with the calendar).** ← chosen.
4. **All Microsoft-supported plus N years past EOL.** Extended-support model; we explicitly rejected this — supporting EOL'd runtimes is the caller's problem (e.g., Microsoft's commercial paid-support tier) and not something a small-core project takes on.

## Decision Outcome

**Chosen: Option 3 — track Microsoft's official .NET release calendar exactly.**

The policy is one rule:

> **Tamp's first-party assemblies multi-target every .NET release that Microsoft considers "in support" — both LTS and STS. When Microsoft's calendar moves, we move with it: we add a TFM the day a new release ships and drop a TFM the day Microsoft ends support for it.**

That's it. No judgement calls. No "we decided to skip net N." No version we added because we like it and now have to drag along after EOL.

### Specifics as of this ADR (2026-05-09)

Per [Microsoft's .NET support policy page](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core):

| Version  | Type | GA            | End of Support  |
|----------|------|---------------|------------------|
| .NET 8   | LTS  | 2023-11-14    | 2026-11-10       |
| .NET 9   | STS  | 2024-11-12    | 2026-11-10       |
| .NET 10  | LTS  | 2025-11-11    | 2028-11-14       |

(Note: .NET 8 and .NET 9 happen to EOL on the same day. STS support is 24 months, LTS is 36; the .NET 9 STS clock and the .NET 8 LTS clock both expire 2026-11-10.)

Therefore today, every first-party Tamp assembly multi-targets:

```xml
<TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
```

When .NET 11 STS ships in November 2026 (per Microsoft's cadence), `net11.0` is added on the same day. When .NET 8 and .NET 9 reach EOL on 2026-11-10, both are dropped from `<TargetFrameworks>` in the next Tamp minor release.

### TFM lifecycle vs. module lifecycle

These are independent. Specifically:

* **TFM lifecycle** — *which runtimes Tamp's own DLLs load on* — follows the rule above strictly.
* **Module lifecycle** — *which version of a wrapped tool's CLI a module targets* — is a per-module decision driven by whether anyone actually still uses that tool version. A `Tamp.NetCli.V8` package can continue to exist (and be useful) for as long as any consumer still has the `dotnet 8` CLI installed, even if `net8.0` has been dropped from the wrapper assembly's TFM list.

Concretely: when .NET 8 reaches EOL, `Tamp.NetCli.V8` does *not* automatically get retired. The package's `<TargetFrameworks>` drops `net8.0` per the rule above (so the wrapper itself runs on supported runtimes), but the package continues to wrap `dotnet 8` invocations because plenty of locked-down environments will still have the `dotnet 8` SDK installed for years past Microsoft's support window. Module retirement is a separate decision, made per module, based on consumer demand — and is properly the subject of a future ADR if and when retirement becomes a meaningful question.

### What "in support" means precisely

The rule says "Microsoft considers in support." We define that, for purposes of this ADR, as: a release is in support if it appears as supported (either Active or Maintenance phase) on Microsoft's `dotnet/core` support page on the day we cut a Tamp release. That's an unambiguous, externally-verifiable test. We don't track Microsoft's preliminary announcements, blog posts, or community speculation — only the support page state at release-cut time.

## Consequences

### Positive

* **Consumer reach is maximised within reason.** Federal, regulated, locked-down VDI, and STS-adopter consumers can all install Tamp and use it. No artificial gates.
* **The rule is mechanical.** Whether a TFM is in or out is a one-page check, not a deliberation. Maintainer overhead is bounded.
* **No drift from the ecosystem.** Tamp's notion of "supported" is identical to Microsoft's. We don't have to defend a Tamp-specific definition.
* **Safe under Microsoft cadence shifts.** If Microsoft changes STS to 18 months, or extends LTS, or invents a new tier, the rule keeps working — we read the calendar, we adjust.

### Negative

* **CI matrix grows linearly with supported releases.** Today: 3 TFMs × N projects. When net11 ships in late 2026, momentarily 4 (until 8/9 drop the same week). Acceptable; matrix builds are cheap and selective-build optimisations are a known technique.
* **Multi-targeting requires occasional `#if NET10_0_OR_GREATER` guards** when Tamp wants to use a newer-runtime API that doesn't exist on net8.0. We accept this. Where the API surface diverges meaningfully, the guard makes the divergence explicit, which is better than pretending it doesn't exist.
* **Brief "ghost" windows around Microsoft's EOL dates.** A consumer running an EOL'd runtime can't pull the latest Tamp build that drops their TFM. They have to either upgrade their runtime or pin to the last Tamp release that still included it. We document this in the README and call it out in release notes; we do not extend support past Microsoft's window to smooth the transition.

### Neutral / future-facing

* **STS support adds CI surface but no real maintenance burden** in the common case, because the .NET CLI surface across STS and LTS is largely additive. Where it isn't, the wrapper code branches — a normal day's work, not a structural problem.
* **`Tamp.NetCli.V11` (and onward) appears the day .NET 11 ships.** Per ADR 0002 it gets its own package; per this ADR its TFM list mirrors Core's at that moment.
* **Retirement of a `Tamp.NetCli.V{N}` module is a separate ADR** when it ever happens. This ADR explicitly does not commit to or against retiring modules; it only commits to TFM lifecycle.

## Pros and Cons of the Options

### Option 1 — `net10.0` only

* Pro: smallest CI matrix; smallest support footprint for the maintainer team.
* Con: excludes net8 LTS (still in support, large enterprise base) and net9 STS (intentional STS adopters).
* Con: contradicts Tamp's "build framework that meets you where you are" positioning.

### Option 2 — LTS-only (`net8.0;net10.0`)

* Pro: still excludes the smallest population (STS adopters).
* Pro: avoids the "STS is a shit show" rep some shops have.
* Con: relies on a Tamp-specific rule ("we don't do STS") that drifts from Microsoft's calendar.
* Con: penalises the legitimate workflow of starting a long project on STS to surface early features.
* Con: contributors and consumers have to remember our rule on top of Microsoft's.

### Option 3 — Microsoft's calendar (chosen)

* Pro: zero judgement calls; check the page.
* Pro: maximum consumer reach within Microsoft's stated support boundary.
* Pro: rule is stable under Microsoft policy changes.
* Con: CI matrix tracks Microsoft's release cadence — not really a downside, just reality.

### Option 4 — extended support past EOL

* Con: every month past EOL is uncompensated maintenance work for the maintainer team.
* Con: the consumer running an EOL runtime is, by Microsoft's definition, accepting unsupported risk; we should not be the safety net.
* Con: nobody asked for this.

## Links

* Tracker: TAM-21 (ADR), TAM-32 (work item).
* Microsoft .NET support policy: `https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core` (canonical source for the calendar).
* Package naming: ADR 0002 — separately governs `Tamp.NetCli.V{N}` package boundaries.
* Repo layout: ADR 0006 — central package management already in place to make TFM additions a one-file edit.
