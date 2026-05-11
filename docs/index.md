---
title: Tamp
description: Pack the build down tight.
---

# Tamp

> Pack the build down tight.

A small-core, plugin-driven build automation framework for **.NET 8, 9, and 10**. Cross-platform. Honest about resources. Forkable.

Tamp is the architectural rethink of the .NET build-tool ecosystem's monolith problem: every tool wrapper ships as an independently-versioned NuGet package, the host environment is a first-class concept, and the architecture is the resilience strategy.

## Try it

```bash
dotnet tool install -g Tamp.Cli         # then: tamp ci
# or
dotnet tool install -g dotnet-tamp      # then: dotnet tamp ci
```

→ **[5-minute Getting Started ↗](https://github.com/tamp-build/tamp/wiki/Getting-Started)**

## Documentation

The reference and guides live in the [Wiki ↗](https://github.com/tamp-build/tamp/wiki). Quick links:

- [Build Script Authoring](https://github.com/tamp-build/tamp/wiki/Build-Script-Authoring) — the fluent target DSL
- [Parameter & Secret Injection](https://github.com/tamp-build/tamp/wiki/Parameter-And-Secret-Injection)
- [Module Catalog](https://github.com/tamp-build/tamp/wiki/Module-Catalog) — what we ship today
- [Failure Handling](https://github.com/tamp-build/tamp/wiki/Failure-Handling)
- [CI Host Integrations](https://github.com/tamp-build/tamp/wiki/CI-Host-Integrations) — GitHub Actions, Azure DevOps, TeamCity
- [Migrating from NUKE](https://github.com/tamp-build/tamp/wiki/Migrating-From-NUKE) · [Migrating from Cake](https://github.com/tamp-build/tamp/wiki/Migrating-From-Cake)

## Architecture decisions

Every load-bearing design choice is recorded as an ADR. Read these before proposing a change to a corresponding area:

- [ADR 0001 — Small core, plugin-driven architecture](adr/0001-small-core-plugin-architecture)
- [ADR 0002 — Package naming convention](adr/0002-package-naming-convention)
- [ADR 0006 — Repository layout: monorepo for core and first-party modules](adr/0006-repo-layout-monorepo)
- [ADR 0007 — License (MIT)](adr/0007-license-mit)
- [ADR 0009 — Governance and namespace policy](adr/0009-governance-and-namespace-policy)
- [ADR 0015 — Target framework strategy](adr/0015-target-framework-strategy)

→ [Full ADR index](adr/)

## Releases

`Tamp.Core 1.0.3` is live, the `Tamp.*` NuGet prefix is reserved to the project, and 14 satellite packages are shipping. See the [changelog ↗](https://github.com/tamp-build/tamp/blob/main/CHANGELOG.md) and the [main README ↗](https://github.com/tamp-build/tamp#readme) for the current package matrix.

## Project meta

- [Repository ↗](https://github.com/tamp-build/tamp)
- [NuGet packages ↗](https://www.nuget.org/profiles/tamp)
- [Maintainers ↗](https://github.com/tamp-build/tamp/blob/main/MAINTAINERS.md) · [Contributing ↗](https://github.com/tamp-build/tamp/blob/main/CONTRIBUTING.md) · [Code of Conduct ↗](https://github.com/tamp-build/tamp/blob/main/CODE_OF_CONDUCT.md) · [Security ↗](https://github.com/tamp-build/tamp/blob/main/SECURITY.md)
- License: [MIT](https://github.com/tamp-build/tamp/blob/main/LICENSE)
