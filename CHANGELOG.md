# Changelog

All notable changes to Tamp are documented in this file.

The format follows [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and the project follows [Semantic Versioning 2.0.0](https://semver.org/) per [ADR 0008](docs/adr/) (forthcoming) once we cut a non-pre-release version.

Pre-1.0 versions may break public API freely between minor versions; the `0.x` line is intentionally a stabilization run.

## [Unreleased]

### Known issues

- **TAM-78** — `[Secret]` env-var resolver missing. The attribute and `Secret` type are wired through `CommandPlan.Secrets` and the redaction table, but `ParameterBinder` doesn't bind `[Secret]` fields. Build scripts must read env vars manually until 1.0.1. See satellite READMEs for the workaround pattern.
- **TAM-79** — full `[Secret]` resolution chain (CI vendor store / keychain / env / interactive prompt) deferred to a v1.x feature release.
- **Windows CI** — two `Tamp.DotNetCoverage.V18` tests fail on `windows-latest` with path-separator assertions. Linux + macOS CI green; tests pass locally on macOS arm64. Tracked separately.

## [1.0.0] — 2026-05-10

First v1 release. Core API is now stable; satellite repos can pin against it via PackageReference.

### Published from this repo

- `Tamp.Core` — small core: target executor, parameter binding, `Secret` type with redaction table, host detection, `CommandPlan`, `ProcessRunner`, multi-edge target graph (`DependsOn` / `Before` / `After` / `Triggers` / `TriggeredBy` / `OnFailureOf` / `OnlyWhen` / `Requires` / `AssuredAfterFailure`), `AbsolutePath`, `Solution` + `GitRepository` models, `Tool` + `[NuGetPackage]`, `CiHost` adapters (GitHub Actions, Azure DevOps, TeamCity), `Logger` + verbosity controls.
- `Tamp.Cli` — global tool, bare-command flavor (`tamp <target>`).
- `dotnet-tamp` — global tool, dotnet-verb flavor (`dotnet tamp <target>`). Dispatches via `BuildProjectLocator` walking up from CWD looking for `build/Build.csproj`.
- `Tamp.NetCli.V8` / `V9` / `V10` — wrappers for the .NET 8 / 9 / 10 SDK CLIs. Verbs: `Restore`, `Build`, `Test`, `Pack`, `Publish`, `NuGetPush`, `Format` / `FormatWhitespace` / `FormatStyle` / `FormatAnalyzers`. `dotnet test` accepts `AddDataCollector("Code Coverage")` for cross-platform coverage; the underlying `dotnet-coverage collect` profiler-attach path is broken on macOS arm64 (Hardened Runtime strips `CORECLR_PROFILER`), the data-collector path works everywhere.
- `Tamp.DotNetCoverage.V18` — wrapper for Microsoft's `dotnet-coverage` tool (Collect + Merge verbs; Cobertura output for downstream tools).

### Moved to satellite repos

Per the satellite-repo convention (third-party tools with their own release cadence don't belong in `tamp` main), these packages now ship from their own repos under the `tamp-build` org:

| Package | Now at |
|---|---|
| `Tamp.Docker.V27` | [`tamp-build/tamp-docker`](https://github.com/tamp-build/tamp-docker) |
| `Tamp.SonarScanner.V10` + `Tamp.SonarScannerCli.V6` | [`tamp-build/tamp-sonar`](https://github.com/tamp-build/tamp-sonar) |
| `Tamp.EFCore.V8` / `V9` / `V10` | [`tamp-build/tamp-ef`](https://github.com/tamp-build/tamp-ef) |
| `Tamp.GitVersion.V6` | [`tamp-build/tamp-gitversion`](https://github.com/tamp-build/tamp-gitversion) |
| `Tamp.ReportGenerator.V5` | [`tamp-build/tamp-reportgenerator`](https://github.com/tamp-build/tamp-reportgenerator) |
| `Tamp.GitHubCli.V2` | [`tamp-build/tamp-gh`](https://github.com/tamp-build/tamp-gh) |

Package IDs are unchanged; future releases come from the satellite repos via the same OIDC trusted publishing or org-secret-driven release flow. `Tamp.Core`'s `[InternalsVisibleTo]` list still grants the satellite packages access to `Secret.Reveal()`.

### Validated end-to-end through the dogfood pipeline

`Tamp.GitVersion.V6 0.1.0` and `Tamp.GitHubCli.V2 0.1.0` were both released by **Tamp itself** — `dotnet tamp Ci` + `dotnet tamp Push` running in their satellite repos' GitHub Actions, against the `tamp_nuget_api_key` org secret. First proof that the framework can ship the framework's own ecosystem.

The dogfood loop also surfaced TAM-78 (the `[Secret]` resolver gap above) as a real find — exactly the kind of bug that only shows up under real-tool execution.

## [0.0.1-alpha] — 2026-05-10

Initial pre-launch placeholder publish to nuget.org. The seven first-party package names are claimed under the `tamp` org as squat-protection ahead of the prefix reservation review. Functionality at this version is the v0 walking skeleton + Tier 1 + Tier 2 surface, not a stable consumer release.

### Published

- `Tamp.Core` — small core: target executor, parameter binding, secret + redaction, host detection, CommandPlan, ProcessRunner, multi-edge target graph (DependsOn / Before / After / Triggers / TriggeredBy / OnFailureOf / OnlyWhen / Requires / AssuredAfterFailure), AbsolutePath, Solution + GitRepository models, Tool + `[NuGetPackage]`, CiHost adapters (GitHub Actions, Azure DevOps, TeamCity), Logger + verbosity controls.
- `Tamp.Cli` — global tool, bare-command flavor (`tamp <target>`).
- `dotnet-tamp` — global tool, dotnet-verb flavor (`dotnet tamp <target>`).
- `Tamp.NetCli.V8` / `V9` / `V10` — wrappers for the .NET 8 / 9 / 10 SDK CLIs (restore, build, test, pack, publish).
- `Tamp.Docker.V27` — wrapper for the Docker 27.x CLI (login with `--password-stdin`, logout, build, tag, push, pull).

### Architecture

The architectural decisions captured before and during this run are recorded as ADRs in [`docs/adr/`](docs/adr/):

- ADR 0001 — Small core, plugin-driven architecture
- ADR 0002 — Package naming convention
- ADR 0006 — Repository layout: monorepo for core and first-party modules
- ADR 0007 — License (MIT)
- ADR 0009 — Governance and namespace policy
- ADR 0015 — Target framework strategy (multi-target net8/net9/net10, follow Microsoft support calendar)

[Unreleased]: https://github.com/tamp-build/tamp/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/tamp-build/tamp/releases/tag/v1.0.0
[0.0.1-alpha]: https://github.com/tamp-build/tamp/releases/tag/v0.0.1-alpha
