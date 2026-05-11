# ADR 0008: SemVer policy for `Tamp.Core` and modules

* Status: Accepted
* Date: 2026-05-11
* Deciders: scott
* Tracking: TAM-14

## Context and Problem Statement

Tamp ships ~25 independently-versioned NuGet packages. Consumers pin specific versions in `Directory.Packages.props` and expect predictable upgrade behavior. The framework needs an explicit SemVer policy that covers:

1. What counts as a breaking change for `Tamp.Core` (the central package every wrapper depends on).
2. What counts as breaking for satellite wrappers (Tamp.Docker.V27, Tamp.SonarScanner.V10, etc.).
3. How the satellite-pinned-to-Tamp.Core relationship is versioned when Core changes.
4. When pre-release tags are used and when version is bumped without code changes (the monolithic-version sibling case).

## Decision Drivers

* **Predictability beats convention.** Consumers should be able to read a version bump and infer the upgrade risk without reading the changelog. `1.1.0 → 1.2.0` is safe; `1.2.0 → 2.0.0` requires reading migration notes.
* **The ecosystem is small.** Tamp is one maintainer's effort + adopters. Aggressive major-version churn breaks consumer trust. SemVer is enforced strictly post-1.0 (which Tamp.Core hit on 2026-05-10).
* **Monolithic versioning within `tamp` main repo.** Per TAM-81, every packable project in the tamp main repo (`Tamp.Core`, `Tamp.Cli`, `dotnet-tamp`, `Tamp.NetCli.V8/V9/V10`, `Tamp.DotNetCoverage.V18`, `Tamp.Http`) ships at the SAME version. This is convenience, not semantic — when one bumps, they all bump. The CHANGELOG annotates which packages actually changed.
* **Satellite repos version independently.** Each `tamp-*` satellite has its own version track and its own release cadence. `Tamp.Docker.V27` is at 0.3.0 while `Tamp.SonarScanner.V10` is at 0.3.0 by coincidence, not by coordination.

## Considered Options

1. **Strict SemVer 2.0.0 post-1.0 (chosen).** Major = breaking. Minor = additive. Patch = bug fix. No exceptions.
2. **Calendar versioning (CalVer).** `2026.05.11`. Pro: implicit timeline. Con: doesn't communicate upgrade risk.
3. **Loose SemVer with maintainer judgment.** "Breaking" is what the maintainer says it is. Pro: flexibility. Con: erodes consumer trust over time.

## Decision

**SemVer 2.0.0 post-1.0**, with these specific interpretations:

### Breaking changes (major bump) for `Tamp.Core`

* Removing a public type, method, property, or field.
* Changing the signature of an existing public API in a way that breaks source compatibility.
* Changing the behavior of an existing public API in a way that breaks build scripts compiled against the previous version (e.g., a method that used to return a value now throws).
* Changing the default behavior of a decorator in a way that materially affects existing builds — covered as a case-by-case judgment. Examples:
  * `1.1.0` inverted the `.TopLevel()` default (targets are now listable + callable by default; `.Internal()` is the new opt-out). Marked breaking.
  * The `[Obsolete]` attribute on `.TopLevel()` is non-breaking because the call still compiles (with a warning).

### Additive changes (minor bump)

* New public types, methods, properties, fields, attributes, or extension methods.
* New overloads of existing methods that don't conflict with existing call sites.
* New optional parameters added with default values.
* Behavior changes that *strictly improve* observable behavior (e.g., a method that used to throw on a previously-undefined input now returns a sensible result).

### Bug fixes (patch bump)

* Fixing behavior that was documented one way and worked another.
* Performance improvements with no API surface change.
* Internal refactors with no observable effect.

### Satellite wrapper packages

Satellites follow the same rules **for their own public API**. The relationship between the satellite and `Tamp.Core` is:

* The satellite's `Directory.Packages.props` pins to a specific `Tamp.Core` version (e.g., `1.2.0`).
* When `Tamp.Core` bumps (major or minor), satellites do NOT automatically re-publish. They bump their pin on their own schedule.
* When a satellite bumps its `Tamp.Core` pin, the satellite's own version bumps follow the satellite's API change rules — not Tamp.Core's. (A satellite that only bumps its dependency without changing its own API can ship as a patch.)

### Pre-1.0 (`0.x`) packages

The `0.x` line allows breaking changes between minor versions. Used for satellites still finding their shape — currently `Tamp.Docker.V27`, `Tamp.SonarScanner.V10`, `Tamp.EFCore.V10`, `Tamp.Turbo.V2`, etc. The expectation is that satellites graduate to `1.0` once their wrapper surface stabilizes against the underlying tool's stable surface area.

### Monolithic-version side effect

When `Tamp.Core` releases a new version, every sibling package in the tamp main repo bumps too (per TAM-81). The CHANGELOG annotates which packages actually changed; consumers using only the unchanged ones can stay on the prior version.

Example: `1.0.9 → 1.0.10` shipped `Tamp.Core` with the `CleanArtifacts()` helper. `Tamp.NetCli.V8/V9/V10` also published `1.0.10`, functionally identical to `1.0.9`. Consumers using only Tamp.NetCli can stay on `1.0.9`.

## Consequences

* **Positive**: consumers can read a version diff and infer risk. `1.2.0 → 1.3.0`? Safe — additive only. `1.x → 2.0`? Migration reading required.
* **Positive**: satellite maintainers have independent cadence. Tamp.SonarScanner.V10 can ship patches without coordinating with Tamp.Core releases.
* **Positive**: the monolithic-version convention in the main repo is documented; consumers don't get confused when `Tamp.NetCli.V10` ships a "new version" with no NetCli changes.
* **Negative**: post-1.0, breaking changes carry the major-bump tax. We will accumulate `1.x` minor versions and only bump to `2.0` for substantial breaking work.
* **Negative**: pre-1.0 satellites can confuse consumers who expect strict SemVer everywhere. The status table in the main README explicitly marks pre-1.0 packages.

## Notes

This ADR was deferred until enough versions had shipped to know which interpretations of SemVer would matter in practice. The `1.1.0` breaking-change wave (TAM-159 concision epic) is the first post-1.0 breaking ship under this policy. The 1.0.x → 1.0.10 → 1.1.0 trajectory and the satellite ripple are documented in CHANGELOG.md.
