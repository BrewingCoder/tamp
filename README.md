# Tamp

> Pack the build down tight.

A small-core, plugin-driven build automation framework for .NET 10 and beyond. Cross-platform. Honest about resources. Forkable.

---

## Status

**Tamp.Core 1.6.0 shipped 2026-05-13.** API is stable; the `Tamp.*` NuGet prefix is reserved to the project. **40+ satellite packages** are live on nuget.org and pin against core via `PackageReference`.

**On-ramp (1.4.0+):**
```bash
dotnet tool install -g dotnet-tamp
cd your-repo
dotnet tamp init                          # scaffold build/Build.cs into the current directory
dotnet tool restore && dotnet tamp Test
```

`tamp init` is the canonical entry point for new adopters — it writes a minimal `build/Build.cs`, `build/Build.csproj`, `.config/dotnet-tools.json`, and `tamp.sh`/`tamp.cmd` shims. Works offline (template embedded in the CLI). Won't overwrite an existing scaffold.

Recent surface (1.1.0 → 1.6.0):
- **`Secret.Reveal()` is now `public`** + new **TAMP004 Roslyn analyzer** (1.6.0) — kills the IVT-bump-per-satellite churn. `Reveal()` is gated by an analyzer flag, not per-satellite `[InternalsVisibleTo]`. Net-new satellites no longer require a Tamp.Core release just to handle a service-principal secret or cert password.
- **Analyzer family TAMP001–TAMP004** (1.4.2 → 1.6.0) — bundled inside `Tamp.Core.nupkg` at `analyzers/dotnet/cs/`. TAMP001 (unobserved CommandPlan), TAMP002 (missing entry point), TAMP003 (`async` lambda passed to `Executes(Action)`), TAMP004 (`Secret.Reveal()` outside approved context).
- **Async `Executes(...)` overloads** (1.5.0) — `Executes(Func<Task>)`, `Executes(Func<Task<CommandPlan>>)`, `Executes(Func<Task<IEnumerable<CommandPlan>>>)`. Async lambdas now bind correctly without the `.GetAwaiter().GetResult()` bridge.
- **`tamp init` scaffolder** (1.4.0) — three-line on-ramp; extension architecture for future NuGet-distributed templates ([`tamp-templates`](https://github.com/tamp-build/tamp-templates)).
- **`params Target[]` overloads on lifecycle deps** (1.3.0) — `Ci.DependsOn(Test, Publish, FrontendBuild, DockerBuildBackend)` shape compiles via Method-handle reflection. NUKE's pattern.
- **Object-init overloads on every wrapper** (1.2.0, 167 across the fleet) — `DotNet.Build(new() { Project = ..., Configuration = ... })` alongside the canonical fluent shape.
- **`.Default()` / `.Internal()`** (1.1.0) — opt-in default-target marker; opt-out for internal helpers. `.TopLevel()` is now Obsolete (no-op).
- **`CleanArtifacts()` helper** (1.1.0) — safe `bin`/`obj` cleanup scoped to `Solution.Projects`.
- **`[CallerArgumentExpression]` overloads on `DependsOn`/`After`/`Before`/`Triggers`/`TriggeredBy`/`OnFailureOf`** (1.1.0).
- **`[FromPath("name")]` and `[FromNodeModules("name")]`** (1.1.0) — auto-inject native or workspace-local `Tool` references.
- **`HttpProbe.WaitForHealthy`** (`Tamp.Http` 0.1.1) — post-deploy smoke pattern for `SmokeQa` targets.

### Published satellites — full ecosystem

The wiki's [Module Catalog](https://github.com/tamp-build/tamp/wiki/Module-Catalog) is the canonical structured reference. The table below is the **complete picture from this repo's vantage point** — every first-party `Tamp.*` package currently shipping, what it wraps, and where its source lives. If you (or an agent) need to understand the Tamp surface, start here.

#### Framework + global tool (`tamp` repo)

| Package | Wraps / does | Latest |
|---|---|---|
| `Tamp.Core` | The framework itself — target dependency graph executor, parameter injection, path utilities, process invocation primitives, host detection, secret handling, dry-run support. Bundles the TAMP001-004 Roslyn analyzers at `analyzers/dotnet/cs/`. | **1.7.0** |
| `Tamp.Cli` | The global tool, bare-command flavor: `tamp <target>`. | **1.7.0** |
| `dotnet-tamp` | The global tool, dotnet-verb flavor: `dotnet tamp <target>`. | **1.7.0** |
| `Tamp.NetCli.V8` / `V9` / `V10` | The .NET 8 / 9 / 10 SDK CLI (`dotnet build`, `test`, `pack`, `publish`, `restore`, `clean`, `format`). One package per SDK major; pin to the SDK your project targets. | **1.4.0+** |
| `Tamp.DotNetCoverage.V18` | `dotnet-coverage` collector + merge. | **1.3.0** |

#### Containers + build

| Package | Wraps / does | Repo | Latest |
|---|---|---|---|
| `Tamp.Docker.V27` | Docker 27.x CLI. 0.3.0+ routes `Docker.Build` to BuildKit (`docker buildx build`); pre-BuildKit available as `Docker.LegacyBuild`. Sub-facades: `Docker.Compose.*`, `Docker.Buildx.*`. Also `tag`, `push`, `pull`, `login`. | [`tamp-docker`](https://github.com/tamp-build/tamp-docker) | **0.3.1** |
| `Tamp.Helm.V3` | Helm v3 CLI — Upgrade, Template, Lint, Package, Push. | [`tamp-helm`](https://github.com/tamp-build/tamp-helm) | **0.1.0** |
| `Tamp.Sccache` | [mozilla/sccache](https://github.com/mozilla/sccache) shared compilation cache. Transparent `RUSTC_WRAPPER`. Backends: local disk, S3, Azure Blob, GCS, Redis, memcached, GitHub Actions cache. | [`tamp-sccache`](https://github.com/tamp-build/tamp-sccache) | **0.1.0** |
| `Tamp.AdjacentContainer` | Fixture-side dual-mode container acquisition for integration tests — adjacent sidecar via env var, local Testcontainers spawn as fallback. Postgres, Azurite, Service Bus emulator. | [`tamp-adjacent-container`](https://github.com/tamp-build/tamp-adjacent-container) | **0.1.1** |
| `Tamp.AdjacentContainer.Provisioning` | CI-side companion. Builds deterministic `docker-compose.yml` for sidecar resources + emits the env-var contract `Tamp.AdjacentContainer` reads. Pairs with `Tamp.Docker.V27.Docker.Compose.Up/Down` for lifecycle. | [`tamp-adjacent-container-provisioning`](https://github.com/tamp-build/tamp-adjacent-container-provisioning) | **0.1.0** |
| `Tamp.Testcontainers.V4` | Diagnostic library for testcontainers-dotnet pipelines. Probes Docker capability + sibling-container restrictions for CI gating. | [`tamp-testcontainers`](https://github.com/tamp-build/tamp-testcontainers) | **0.1.0** |

#### .NET coverage + EF + version

| Package | Wraps / does | Repo | Latest |
|---|---|---|---|
| `Tamp.Coverlet.V6` | Coverlet 6 config-builder — type-safe Format / Include / Exclude / UseSourceLink for `dotnet test --collect "XPlat Code Coverage"`. | [`tamp-coverlet`](https://github.com/tamp-build/tamp-coverlet) | **0.1.0** |
| `Tamp.ReportGenerator.V5` | ReportGenerator — coverage HTML / badge / markdown emission. | [`tamp-reportgenerator`](https://github.com/tamp-build/tamp-reportgenerator) | **0.1.1** |
| `Tamp.EFCore.V8` / `V9` / `V10` | `dotnet ef` migrations per major. V10 includes `MigrationFanout` for multi-tenant SaaS migration loops (per-tenant invocation, retry, fail-fast, strict serial ordering at concurrency=1). | [`tamp-ef`](https://github.com/tamp-build/tamp-ef) | **0.3.1** |
| `Tamp.GitVersion.V6` | GitVersion 6 — SemVer from git history. | [`tamp-gitversion`](https://github.com/tamp-build/tamp-gitversion) | **0.1.1** |

#### JavaScript / TypeScript toolchain

| Package | Wraps / does | Repo | Latest |
|---|---|---|---|
| `Tamp.Yarn.V4` | Yarn Berry 4 — `install`, `run`, workspaces, `npm publish`. | [`tamp-yarn`](https://github.com/tamp-build/tamp-yarn) | **0.1.1** |
| `Tamp.Npm.V10` | npm 10+ CLI — sibling to `Tamp.Yarn.V4`. | [`tamp-npm`](https://github.com/tamp-build/tamp-npm) | **0.1.0** |
| `Tamp.Turbo.V2` | Turborepo 2 — `run`, `prune --docker`, `ls`, `info`, `daemon`. | [`tamp-turbo`](https://github.com/tamp-build/tamp-turbo) | **0.2.1** |
| `Tamp.Vite.V5` | Vite 5 (`dev`, `build`, `preview`, `optimize`) **and** Vitest 1 (`run`, `watch`, `related`, `bench`, `typecheck`). | [`tamp-vite`](https://github.com/tamp-build/tamp-vite) | **0.1.1** |
| `Tamp.GraphQLCodegen.V5` | `graphql-code-generator` 5 — `generate`, `init`, `--watch`, `--require`. | [`tamp-graphql-codegen`](https://github.com/tamp-build/tamp-graphql-codegen) | **0.1.1** |
| `Tamp.Playwright.V1` | Playwright 1 — `test`, `install`, `codegen`, `show-report`, `merge-reports`, sharded e2e. | [`tamp-playwright`](https://github.com/tamp-build/tamp-playwright) | **0.1.1** |

#### Rust + Microsoft Store desktop ship chain

End-to-end: Rust core → Tauri bundle → MSIX package → Partner Center submission. Each stage a typed Tamp target.

| Package | Wraps / does | Repo | Latest |
|---|---|---|---|
| `Tamp.Cargo` | `cargo` CLI — `build`, `test`, `check`, `clippy`, `fmt`, `run`, `bench`, `doc`, `update`. First non-.NET satellite. | [`tamp-cargo`](https://github.com/tamp-build/tamp-cargo) | **0.1.0** |
| `Tamp.Tauri.V2` | Tauri 2.x CLI — `build`, `info`, `icon`, `migrate`, `signer generate/sign`. Plus the load-bearing `Tauri.ExternalBinPath(srcTauri, name, target-triple)` helper that types Tauri's `binaries/<name>-<triple>[.exe]` sidecar contract. | [`tamp-tauri`](https://github.com/tamp-build/tamp-tauri) | **0.2.0** |
| `Tamp.Msix` | Windows MSIX toolchain — `makeappx` (pack / unpack / bundle), `signtool` (sign / verify, with password-protected PFX). Plus `Msix.SetAppxManifestVersion` for 3-part SemVer → 4-part MSIX version normalization. | [`tamp-msix`](https://github.com/tamp-build/tamp-msix) | **0.2.0** |
| `Tamp.MicrosoftStoreCli` | [microsoft/msstore-cli](https://github.com/microsoft/msstore-cli) — Microsoft Store Partner Center submission API. Reconfigure (service-principal auth via `Secret`), Publish (MSIX upgrade), Submission lifecycle, Flights, Rollout halt / finalize. Replaces the manual Partner Center web-UI submission. | [`tamp-msstore-cli`](https://github.com/tamp-build/tamp-msstore-cli) | **0.1.0** |

#### Azure + ADO ops

| Package | Wraps / does | Repo | Latest |
|---|---|---|---|
| `Tamp.AzureCli.V2` | `az` 2.x — full subscription / resource / identity surface. Access tokens typed as `Secret` via `Account.GetAccessTokenAsSecret`. | [`tamp-azure-cli`](https://github.com/tamp-build/tamp-azure-cli) | **0.1.2** |
| `Tamp.Bicep` | Bicep CLI (`build`, `lint`, `format`, `version`) plus `az deployment group create` via the unified facade. | [`tamp-bicep`](https://github.com/tamp-build/tamp-bicep) | **0.1.1** |
| `Tamp.AzureAppService` | App Service slot orchestration + lifecycle — `webapp deployment slot swap / list / create / delete`. | [`tamp-azure-app-service`](https://github.com/tamp-build/tamp-azure-app-service) | **0.1.0** |
| `Tamp.Kudu` | Azure App Service Kudu REST API + adjacent Management API endpoints. KuduClient: vfs read/write, command exec, deploy. ManagementClient: stop/start/restart, publishing credentials, config-references (KV-ref resolution), app settings list/set. DeploymentClient: ZipDeploy (sync + async-poll). | [`tamp-kudu`](https://github.com/tamp-build/tamp-kudu) | **0.2.2** |
| `Tamp.PostgresFlex` | Azure Database for PostgreSQL Flexible Server admin — lifecycle, firewall, parameters. | [`tamp-postgres-flex`](https://github.com/tamp-build/tamp-postgres-flex) | **0.1.0** |
| `Tamp.AzureFunctionsCoreTools.V4` | Azure Functions Core Tools (`func`) 4.x — publish, log streaming, settings sync. Access token typed as `Secret` via stdin. | [`tamp-azure-functions-core-tools`](https://github.com/tamp-build/tamp-azure-functions-core-tools) | **0.1.1** |
| `Tamp.AzureStaticWebApps.V2` | `@azure/static-web-apps-cli` (swa) — Azure SWA deploy CLI. | [`tamp-azure-static-web-apps`](https://github.com/tamp-build/tamp-azure-static-web-apps) | **0.1.1** |
| `Tamp.ServiceBus.V7` / `V8` | Azure Service Bus admin CRUD + topology convergence helper (`EnsureRuleAsync`, `DeleteXxxIfExistsAsync`). | [`tamp-servicebus`](https://github.com/tamp-build/tamp-servicebus) | **0.1.0** |
| `Tamp.AdoGit` | PAT-injected git wrapper for Azure DevOps — bakes `-c http.extraHeader=…` into every fetch / push / clone so adopters don't reinvent PAT-auth git plumbing. | [`tamp-ado-git`](https://github.com/tamp-build/tamp-ado-git) | **0.1.0** |
| `Tamp.AdoRest.V7` | Azure DevOps REST API 7.1 — typed surface for pull requests, builds, service endpoints, environments, agent pools, branch policies. PAT typed as `Secret`. Built on `Tamp.Http`. | [`tamp-ado-rest`](https://github.com/tamp-build/tamp-ado-rest) | **0.1.0** |
| `Tamp.AdoServiceConnection.V1` | End-to-end Azure DevOps WIF (Workload Identity Federation) service connection creation. Orchestrates `az` + ADO REST. | [`tamp-ado-service-connection`](https://github.com/tamp-build/tamp-ado-service-connection) | **0.1.1** |

#### Supply-chain security

One focused tool per axis. No overlap.

| Package | Wraps / does | Axis | Repo | Latest |
|---|---|---|---|---|
| `Tamp.TruffleHog.V3` | [trufflesecurity/trufflehog](https://github.com/trufflesecurity/trufflehog) — secret scanning across git / GitHub / filesystem / Docker / S3 / GCS / etc. ~800+ detectors with live verification. | leaked secrets | [`tamp-trufflehog`](https://github.com/tamp-build/tamp-trufflehog) | **0.1.1** |
| `Tamp.CodeQL.V2` | CodeQL 2 — database, github upload-results, resolve, pack, query. PAT via stdin. | code-pattern vulns (SQLi, XSS, taint) | [`tamp-codeql`](https://github.com/tamp-build/tamp-codeql) | **0.1.1** |
| `Tamp.Syft` | [anchore/syft](https://github.com/anchore/syft) — SBOM generator. Auto-detects 20+ ecosystems (Rust, npm, .NET, Go, Java, Python, PHP, …). Emits CycloneDX (JSON/XML) or SPDX (JSON/tag-value). | what's inside the artifact | [`tamp-syft`](https://github.com/tamp-build/tamp-syft) | **0.1.0** |
| `Tamp.Grype` | [anchore/grype](https://github.com/anchore/grype) — CVE scanner. Reads syft SBOMs. EPSS + KEV + CVSS composite 0-10 risk scoring. `--fail-on severity` for CI gating. | dep CVEs / vuln matching | [`tamp-grype`](https://github.com/tamp-build/tamp-grype) | **0.1.0** |
| `Tamp.SonarScanner.V10` / `Tamp.SonarScannerCli.V6` | SonarScanner for .NET 10 + the standalone CLI. SonarQube Community Edition branch-strip handling. | code quality + coverage gating | [`tamp-sonar`](https://github.com/tamp-build/tamp-sonar) | **0.3.1** |

#### Source control / issue tracking

| Package | Wraps / does | Repo | Latest |
|---|---|---|---|
| `Tamp.GitHubCli.V2` | `gh` CLI — release, pr, issue, api, repo, auth. | [`tamp-gh`](https://github.com/tamp-build/tamp-gh) | **0.1.1** |
| `Tamp.YouTrack` | YouTrack REST API — typed Issues client (create / update / search / set-state / project resolve). Bearer permanent token via `Secret`. Built on `Tamp.Http`. | [`tamp-youtrack`](https://github.com/tamp-build/tamp-youtrack) | **0.1.0** |

#### Foundation libraries + templates

| Package | Wraps / does | Repo | Latest |
|---|---|---|---|
| `Tamp.Http` | Foundation `TampApiClient` base class for HTTP-API satellites — `Secret`-redacted auth, JSON serialization, error mapping. Plus `HttpProbe.WaitForHealthy` for post-deploy smoke. Shared substrate beneath `Tamp.Kudu` / `Tamp.AdoRest.V7` / `Tamp.MicrosoftStoreCli` / `Tamp.PostgresFlex` / `Tamp.YouTrack`. | [`tamp-http`](https://github.com/tamp-build/tamp-http) | **0.1.1** |
| `Tamp.Templates.AspNet` | NuGet-distributed scaffold template loaded by `tamp init --template aspnet`. Preview; CLI 0.2.0+ resolves. | [`tamp-templates`](https://github.com/tamp-build/tamp-templates) | **0.1.0** |

NuGet listing: <https://www.nuget.org/profiles/tamp> · the `Tamp.*` prefix is reserved to the project so every package on the listing carries the verified-publisher checkmark.

Third-party tool wrappers ship from satellite repos so each tool's release cadence (Docker every 2 weeks, Vite every quarter, Playwright every 4–6 weeks, etc.) doesn't gate Tamp core releases. Same `PackageReference` story for the consumer; different release schedules for the maintainer.

#### Worked-example stacks

Common adopter scenarios pair specific satellites together:

- **Polyglot Microsoft Store desktop app**: `Tamp.Cargo` → `Tamp.Tauri.V2` → `Tamp.Msix` → `Tamp.MicrosoftStoreCli`, optionally + `Tamp.Sccache` for Rust compile cache.
- **Azure-deployed .NET service**: `Tamp.NetCli.V10` (build) + `Tamp.EFCore.V10` (migrations) + `Tamp.AzureCli.V2` (auth) + `Tamp.Kudu` (deploy) + `Tamp.AzureAppService` (slot swap).
- **Helm-deployed cluster app**: `Tamp.Docker.V27` (image) + `Tamp.Helm.V3` (chart) + `Tamp.GitHubCli.V2` (release).
- **Compliance-aware ship**: any stack + `Tamp.Syft` (SBOM) + `Tamp.Grype` (CVE gate) + `Tamp.TruffleHog.V3` (secrets) + `Tamp.CodeQL.V2` (SAST).
- **Integration tests with sidecars**: `Tamp.AdjacentContainer.Provisioning` (CI-side generator) + `Tamp.Docker.V27.Docker.Compose` (lifecycle) + `Tamp.AdjacentContainer` (fixture-side) + `Tamp.EFCore.V10` (migrations against the sidecar).

All satellites ship through **Tamp itself** — `dotnet tamp Ci && dotnet tamp Push` running in the satellite repos' CI, dogfooding the framework end-to-end. See any satellite's `build/Build.cs` and `.github/workflows/release.yml` for the pattern.

---

## Why Tamp Exists

NUKE was the right idea executed in a way that didn't survive its maintainer. Every tool wrapper lived in the framework's main assembly, every release was bottlenecked on one person's evenings, and every breaking change in `dotnet`, `docker`, or `sonar-scanner` waited for an upstream cut. When NUKE's lifecycle stalled, the .NET community had no fallback that wasn't also a lifecycle bet.

Tamp fixes the architecture, not the personality. Core stays small. Tool wrappers ship as independently-versioned NuGet packages. The host environment — Windows + Defender, Linux in a cgroup-limited pod, macOS with sandbox quirks — is a first-class concept rather than something the framework pretends doesn't exist. Builds run identically on a developer's laptop and in a runner pod, with the framework adapting to what it finds rather than assuming uniformity.

This is a pragmatic project. It does not aspire to be everything. It aspires to be the thing that's still working in five years when the next NUKE has gone quiet.

---

## Design Philosophy

**Core is lightweight.** Tamp.Core contains the target dependency graph executor, parameter injection, path utilities, process invocation primitives, host detection, secret handling, and dry-run support. Nothing else. No tool knowledge, no CI YAML generation, no Sonar integration. If a feature is "knows how to do X with tool Y," it lives in a module package.

**Modules are independently versioned.** Each tool wrapper is its own NuGet package, versioned to track the tool it wraps. New `dotnet` SDK ships → new wrapper package ships. Old wrapper keeps working for projects that haven't migrated. No forced flag day. This is the model Cake's addins got right and NUKE's monolith got wrong.

**The host is real.** Tamp detects the OS family, container status, cgroup limits, CI vendor, and tool availability. Targets can declare what they need; Tamp can warn or fail fast when the host can't deliver. A 45-minute build that times out at 10 minutes because of a misconfigured cgroup memory limit is exactly the kind of failure Tamp is designed to surface, not hide.

**No glass ceilings.** Targets can express the full surface of their resource expectations — memory, time, parallelism, capability requirements, idempotency, retry policies, declarative resource consumption. Most targets won't use most of this. When you need it, it's there.

**Dry runs are mandatory.** Every target must be able to declare *what* it would do without doing it. Tool wrappers produce command plans, not side effects. The runner either dispatches the plan or prints it. Dry-run output is exactly what would run, character for character.

**Secrets stay secret.** Sensitive parameters are typed differently from regular parameters. The runner redacts them in logs, dry-run output, error messages, and stack traces. The type system makes it hard to accidentally leak a secret; the runtime makes it harder.

**Forkable by default.** Core is small enough that one person can maintain it on weekends. Modules are decoupled enough that abandoning one doesn't break the rest. The architecture is the resilience strategy.

---

## Package Convention

```
Tamp.{ToolFamily}.{TargetVersion?}
```

- **Tamp** — fixed brand prefix
- **ToolFamily** — what's being wrapped: `Core`, `Cli`, `NetCli`, `Docker`, `SonarQube`, `Yarn`, `Turbo`, `Pac`, `Kubectl`
- **TargetVersion** — `V{major}` of the wrapped tool, *only when the tool's CLI surface breaks across majors*

### Examples

| Package | What It Is |
|---|---|
| `Tamp.Core` | Executor, parameter injection, path API, host detection, secret handling, dry-run |
| `Tamp.Cli` | Global tool — bare-command flavor. `dotnet tool install -g Tamp.Cli`; invoke as `tamp <target>` |
| `dotnet-tamp` | Global tool — dotnet-verb flavor. `dotnet tool install -g dotnet-tamp`; invoke as `dotnet tamp <target>` |
| `Tamp.NetCli.V10` | Wraps .NET 10 SDK (`dotnet build`, `dotnet test`, `dotnet publish`, etc.) |
| `Tamp.NetCli.V11` | Wraps .NET 11 SDK — separate package, separate semver track |
| `Tamp.Docker.V27` | Wraps Docker 27.x CLI |
| `Tamp.SonarQube.V10` | Wraps SonarScanner for SonarQube 10.x |
| `Tamp.Yarn` | Wraps Yarn (no major-version pin; CLI surface is stable) |
| `Tamp.Turbo.V2` | Wraps Turborepo 2.x |
| `Tamp.Pac` | Wraps Power Platform CLI |
| `Tamp.Kubectl` | Wraps kubectl |

### Versioning Rule

The package name encodes the *target tool's* major version when that tool breaks wrappers across majors. The NuGet semver field tracks the *plugin's own* evolution within that line.

```
Package:  Tamp.NetCli.V10
Version:  1.0.0      ← plugin v1
          1.0.1      ← plugin bug fix
          1.1.0      ← plugin feature add
          2.0.0      ← plugin breaking API change (still wraps .NET 10)
```

When .NET 11 ships, a new package `Tamp.NetCli.V11` is created with its own `1.0.0` track. Both packages can be maintained simultaneously. A project on .NET 10 stays on `Tamp.NetCli.V10` and never accidentally pulls in V11 changes.

### When to Pin Major Version in the Package Name

Ask: *does this tool break wrappers across major versions?*

- **Yes** (`dotnet`, `docker`, `kubectl-ish-but-actually-no`, `sonar-scanner`): pin in name → `Tamp.NetCli.V10`
- **No** (`yarn`, `kubectl`, `pac`, most stable CLIs): single package → `Tamp.Yarn`

For tools where within-major variation matters but doesn't break wrappers, the wrapper code branches on `dotnet --version` at runtime instead of fragmenting into more packages.

---

## Build Project Layout

A Tamp build is a regular .NET console project that references `Tamp.Core` and whatever tool modules it needs. There is no manifest format, no `tamp.json`, no DSL. Standard C#, standard NuGet, standard everything.

```
my-repo/
├── src/
│   └── ...                      ← your application code
└── build/
    ├── Build.csproj             ← references Tamp.Core + tool modules
    └── Build.cs                 ← target definitions
```

`build/Build.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Tamp.Core" Version="1.2.0" />
    <PackageReference Include="Tamp.NetCli.V10" Version="1.2.0" />
    <PackageReference Include="Tamp.Docker.V27" Version="0.3.0" />
  </ItemGroup>
</Project>
```

Run via the global tool or directly. The global tool ships as two NuGet packages — same code, different on-PATH command name; pick whichever convention fits your habit:

```bash
dotnet tool install -g Tamp.Cli         # then: tamp ci          (NUKE-style)
dotnet tool install -g dotnet-tamp      # then: dotnet tamp ci   (Cake-style)
dotnet run --project build -- ci        # always available; no install
```

All three produce identical behaviour. The global tool exists for ergonomics; nothing depends on it.

---

## Target Authoring (Sketch)

Targets are properties on a build class. Phase, dependencies, parameters, and the work itself are all declared inline. The shape is similar to NUKE's, deliberately, because the syntax is good — it's the architecture underneath that needed rethinking.

```csharp
using Tamp;
using Tamp.NetCli.V10;
using Tamp.Docker.V27;

class Build : TampBuild
{
    public static int Main(string[] args) => Execute<Build>(args);

    [Parameter("Build configuration")]
    Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Secret("Container registry password")]
    readonly Secret RegistryPassword = null!;

    [Parameter("Target environment")]
    readonly string Environment = "development";

    [Solution] readonly Solution Solution = null!;
    [FromPath("docker")] readonly Tool Docker = null!;

    Target Clean => _ => _.Executes(() => CleanArtifacts());     // safe by construction — scoped to Solution.Projects

    Target Restore => _ => _
        .Phase(Phase.Restore)
        .RequiresNetwork()
        .Executes(() => DotNet.Restore(s => s.SetProject(Solution.Path)));

    Target Compile => _ => _
        .Default()                                                // dotnet tamp → runs this
        .Phase(Phase.Build)
        .DependsOn(Restore)                                       // [CallerArgumentExpression] — no nameof()
        .Executes(() => DotNet.Build(new() {                      // object-init style
            Project = Solution.Path,
            Configuration = Configuration,
            NoRestore = true,
        }));

    Target Test => _ => _
        .Phase(Phase.Test)
        .DependsOn(Compile)
        .Executes(() => DotNet.Test(s => s                         // fluent style — equivalent
            .SetProject(Solution.Path)
            .SetConfiguration(Configuration)
            .SetNoBuild(true)
            .AddLogger("trx;LogFileName=test-results.trx")));      // solution-mode auto-disambiguates to LogFilePrefix

    Target Pack => _ => _
        .Phase(Phase.Pack)
        .DependsOn(Test)
        .Idempotent()
        .Produces("artifacts/*.nupkg")
        .Executes(() => DotNet.Pack(s => s
            .SetProject(Solution.Path)
            .SetConfiguration(Configuration)
            .SetOutput("artifacts")));

    Target PushImage => _ => _
        .Phase(Phase.Publish)
        .DependsOn(Pack)
        .RequiresDocker()
        .RequiresNetwork()
        .Retry(count: 3, backoff: Backoff.Exponential)
        .Executes(() =>
        {
            Tamp.Docker.V27.Docker.Login(s => s
                .SetServer("registry.example.com")
                .SetUsername("ci")
                .SetPassword(RegistryPassword));     // typed Secret — never logged, redacted in dry-runs

            Tamp.Docker.V27.Docker.Build(s => s      // 0.3.0+: routes to docker buildx build by default
                .SetContext(".")
                .AddTag($"registry.example.com/myapp:{Environment}")
                .AddPlatform("linux/amd64"));
        });

    Target Ci => _ => _
        .DependsOn(Pack)
        .Description("CI pipeline: restore, build, test, pack.");
}
```

Run it:

```bash
tamp ci                              # full pipeline
tamp pack --configuration Release    # one target with parameter
tamp push-image --environment prod   # one target with required secret prompt
tamp ci --dry-run                    # show what would happen, run nothing
tamp ci --plan                       # render execution plan as DAG, exit
tamp --list                          # list all targets
tamp --list-tree                     # list targets with dependencies
```

---

## The Agent Surface

A target can declare any subset of these. Defaults are sensible for the common case; specifying more enables smarter scheduling and clearer telemetry.

### Time

- `Timeout(TimeSpan)` — hard wall-clock kill at expiry
- `ExpectedDuration(TimeSpan)` — soft hint; powers "this is taking longer than usual" telemetry

### Memory

- `MemoryBudget(int megabytes)` — expected peak RSS; used for scheduling and post-run reporting
- `MemoryHardLimit(int megabytes)` — optional hard ceiling; applied via cgroup if available

### Parallelism

- `MaxParallelism(int)` — copies of *this* target running simultaneously in one build invocation
- `MaxHostParallelism(int)` — copies across the whole host (matters when multiple builds share infra)

### Resource Kinds

- `Consumes(Resource, ConsumeMode)` — declarative resource use. Modes: `Shared`, `Exclusive`. The scheduler serializes targets fighting over the same exclusive resource. This is what makes `dotnet build` and `dotnet test --no-build` not race when run in parallel.

Built-in resource kinds (extensible by modules):

```
Resource.BuildCache.Dotnet
Resource.BuildCache.Yarn
Resource.BuildCache.Nuget
Resource.Filesystem(path)
Resource.Network.Internet
Resource.Network.Registry(host)
Resource.Process.Docker
```

### Capabilities

- `RequiresNetwork()` — preflight: fail fast if offline mode is set
- `RequiresDocker()` — preflight: fail fast if Docker daemon unreachable
- `RequiresAdmin()` — preflight: fail fast if not elevated, with platform-specific guidance
- `RequiresTool(name, minVersion?)` — preflight: fail fast if tool not on PATH or below minimum

### Idempotency and Caching

- `Idempotent()` — running twice with same inputs produces same result
- `InputHash(Func<HashInput>)` — function returning a hash of inputs (file globs, env vars, parameters)
- `Produces(globPattern)` — declarative output paths
- `RunMode(RunMode)` — `Always` (default), `WhenInputsChanged`, `Manual`

### Failure Handling

- `FailureMode(Mode)` — `Fatal` (default), `Continue`, `Retry`
- `Retry(count, Backoff)` — count, strategy (`Linear`, `Exponential`, custom), retryable-exit-code matcher

### Telemetry

- `Tag(string...)` — labels for grouping in reports
- `Phase(Phase)` — `Restore`, `Build`, `Test`, `Pack`, `Publish`, `Deploy`, `Custom`
- `Description(string)` — shown in `tamp --list`

---

## Host Detection

`Tamp.Core` builds a `HostProfile` once at startup and freezes it. Targets and modules can read it but never mutate it.

```csharp
public sealed record HostProfile
{
    // Always available, all OSes
    public required OSFamily Os { get; init; }            // Windows, Linux, MacOS
    public required Architecture Arch { get; init; }      // X64, Arm64, X86
    public required int LogicalCpuCount { get; init; }
    public required int PhysicalCpuCount { get; init; }
    public required long TotalMemoryBytes { get; init; }
    public required long AvailableMemoryBytes { get; init; }

    // Container / sandboxing
    public required bool InContainer { get; init; }
    public bool InWsl { get; init; }
    public CgroupLimits? Cgroup { get; init; }            // null if not in cgroup

    // CI environment
    public CiVendor? Ci { get; init; }                    // null if not in CI

    // OS-specific signals
    public WindowsHostInfo? Windows { get; init; }
    public LinuxHostInfo? Linux { get; init; }
    public MacOsHostInfo? MacOs { get; init; }
}
```

### What Detection Enables

1. **Cgroup-aware parallelism.** Default `MaxParallelism` for the build is `min(LogicalCpuCount, ceil(CgroupCpuQuota))`. Builds in resource-limited pods don't oversubscribe and thrash.

2. **Memory budget warnings.** When a target's declared `MemoryBudget` exceeds 50% of available memory, log a warning. Above 80%, log loudly. Above 100%, fail fast with a clear message rather than letting the OS OOM-kill the process mid-build.

3. **`.NET` GC tuning in cgroups.** When `Cgroup` is detected and memory limits are tight, automatically set `DOTNET_GCHeapHardLimit` for child `dotnet` processes. The .NET 10 GC is much better at cgroup awareness than older versions, but explicit limits still help.

4. **Windows Defender awareness.** On Windows, when the build cache path is in a Defender-monitored directory, log a one-line warning at startup: *"Build cache `C:\...\.tamp\` is in a Defender-scanned path. Add an exclusion to improve build times."* Specific, actionable, only logged once.

5. **CI vendor in summary.** Every build summary logs the detected CI vendor (or `local`). Invaluable for triaging "works locally, breaks in ADO" issues — the answer is always somewhere in the gap between the two host profiles.

6. **WSL detection.** When running in WSL, Tamp logs the fact and warns about the common WSL trap of running builds in `/mnt/c/...` (10-100x slower than `/home/user/...` due to the 9P filesystem bridge).

### Cross-Platform Discipline

OS-specific code lives behind interfaces in `Tamp.Core.Hosts`. The public surface accepts a `HostProfile`; it never branches on `OSPlatform` directly. Adding (e.g.) FreeBSD support later is a single implementation drop-in.

Tamp invokes processes via `Process.Start` directly. It does not shell out unless absolutely necessary. When it does need a shell, it picks `pwsh` (which ships on .NET 10 cross-platform), never assumes `bash` or `cmd`.

---

## Dry Runs

Every tool wrapper produces a `CommandPlan` rather than directly executing:

```csharp
public sealed record CommandPlan
{
    public required string Executable { get; init; }
    public required IReadOnlyList<string> Arguments { get; init; }
    public required IReadOnlyDictionary<string, string> Environment { get; init; }
    public string? WorkingDirectory { get; init; }
    public IReadOnlyList<SecretReference> Secrets { get; init; } = [];
}
```

The runner either dispatches the plan or prints it. Dry-run output is exactly what would run:

```
$ tamp ci --dry-run

[DRY RUN] No commands will execute.

Restore (Tamp.NetCli.V10)
  $ dotnet restore
  cwd: /repo
  env: DOTNET_NOLOGO=1, DOTNET_CLI_TELEMETRY_OPTOUT=1

Compile (Tamp.NetCli.V10)
  $ dotnet build --configuration Release --no-restore
  cwd: /repo

Test (Tamp.NetCli.V10)
  $ dotnet test --configuration Release --no-build
  cwd: /repo

Pack (Tamp.NetCli.V10)
  $ dotnet pack --configuration Release --output artifacts
  cwd: /repo
  produces: artifacts/*.nupkg

PushImage (Tamp.Docker.V27)
  $ docker login registry.example.com --username ci --password ***
  $ docker push registry.example.com/myapp:development
  cwd: /repo
```

The exact command, the exact arguments, the exact working directory, the exact env vars, the exact order. Secrets are redacted to `***`. Nothing executes. This is what tells you whether the pipeline you just wrote is going to do what you think before you wait 20 minutes for a CI runner to find out.

`--plan` is similar but renders the target DAG instead of commands — useful for understanding dependency order and parallelism opportunities.

---

## Secrets and Environment Variables

Two types, treated differently throughout the system.

### `Parameter` — Configuration

Non-sensitive values that vary per environment. Logged freely.

```csharp
[Parameter("Build configuration")]
Configuration Configuration = Configuration.Debug;

[Parameter("Target environment", EnvironmentVariable = "DEPLOY_ENVIRONMENT")]
string Environment = "development";
```

Resolution order: command-line argument → environment variable → property default. Logged in build summary.

### `Secret` — Sensitive

API keys, tokens, passwords. Marked at declaration time with the `Secret` type. Cannot be implicitly converted to `string`; cannot appear in `ToString()` output; cannot be logged through the standard logger; redacted in dry-run output, error messages, and stack traces.

```csharp
[Secret("Container registry password", EnvironmentVariable = "REGISTRY_PASSWORD")]
Secret RegistryPassword;

[Secret("NuGet API key", EnvironmentVariable = "NUGET_API_KEY")]
Secret NuGetApiKey;
```

The `Secret` type is a wrapper, not a string. To pass a secret to a tool wrapper, the wrapper accepts `Secret` parameters explicitly:

```csharp
Docker.Login(s => s
    .SetServer("registry.example.com")
    .SetUsername("ci")
    .SetPassword(RegistryPassword));    // takes Secret, not string
```

Internally, the wrapper records the secret's identity in the `CommandPlan.Secrets` list. The runner substitutes the actual value only at process spawn time, never in any logged or printed surface.

### Resolution Order

Secrets resolve from, in order:

1. CI vendor's secret store (when running in CI and a known vendor is detected — ADO, GitHub Actions, etc.)
2. Local secret store (DPAPI on Windows, libsecret on Linux, Keychain on macOS) when configured
3. Environment variable (named via `EnvironmentVariable` attribute)
4. Interactive prompt (if running attached to a TTY and the secret isn't already provided)

If none of the above provides a value and a target requires the secret, the build fails preflight with a clear error naming the missing secret. **Secrets are never resolved at build-class instantiation** — they're resolved when a target that requires them is about to run, so unrelated targets don't fail just because an unrelated secret isn't available.

### Redaction

The runner maintains a redaction table mapping secret values to placeholders (`***`, or named like `${REGISTRY_PASSWORD}`). All log output, all error messages, all stack traces, all dry-run output, and all process stderr/stdout *passed through Tamp's logger* are scrubbed against this table before display.

A subtlety: secrets passed *through* a child process (e.g., `docker login --password $SECRET`) are visible to that child process's process list while it runs. This is a fundamental OS limitation, not something Tamp can prevent. Tool wrappers should prefer stdin or file-based secret passing where the underlying tool supports it (`docker login --password-stdin`, `gh auth login --with-token`, etc.). When this matters for a specific tool, the wrapper documents it.

---

## Module Authoring

A module is a NuGet package that exposes one or more tool wrappers. The contract:

1. Targets a specific tool and major version (or no version pin if surface is stable)
2. Provides typed wrappers that produce `CommandPlan` rather than executing directly
3. Declares its required `RequiresTool(...)` so preflight catches missing tools cleanly
4. Versions independently of `Tamp.Core` and other modules

A minimal wrapper looks like:

```csharp
namespace Tamp.MyTool.V1;

public static class MyTool
{
    public static CommandPlan Run(Action<MyToolSettings> configure)
    {
        var s = new MyToolSettings();
        configure(s);
        return new CommandPlan
        {
            Executable = "mytool",
            Arguments = s.BuildArguments(),
            Environment = s.BuildEnvironment(),
        };
    }
}
```

Schemas (the source-of-truth for what flags exist, their types, defaults, mutex groups) live in the module repo and drive code generation. Updating the schema and regenerating is the normal workflow when the wrapped tool ships a new version.

---

## Roadmap

### v0 — Walking skeleton ✅ (shipped)

- `Tamp.Core` with target executor, parameter injection, dry-run via `CommandPlan`, `Secret` type, host detection
- `Tamp.Cli` + `dotnet-tamp` global tools
- `Tamp.NetCli.V8` / `V9` / `V10` covering the dotnet CLI subset needed for real pipelines
- Tamp self-hosts; the `tamp` repo's own `build/Build.cs` drives its `Ci` / `Coverage` targets via Tamp

### v1 — Real-world coverage ✅ (shipped)

- `Tamp.Core 1.0.0 → 1.0.3` shipped 2026-05-10. API contract is what satellites pin against.
- **`Tamp.*` NuGet prefix reserved** (confirmed by NuGet support 2026-05-10).
- Tier-1 .NET tooling (in `tamp` core repo): `Tamp.NetCli.V8` / `V9` / `V10` (`Format` verbs added), `Tamp.DotNetCoverage.V18`.
- Tier-1 satellite tooling: `Tamp.Docker.V27` (compose + buildx in 0.2.0), `Tamp.SonarScanner.V10` / `SonarScannerCli.V6`, `Tamp.EFCore.V8` / `V9` / `V10`, `Tamp.GitVersion.V6`, `Tamp.ReportGenerator.V5`, `Tamp.GitHubCli.V2`.
- HoldFast wrapper sprint (TAM-85 through TAM-92): `Tamp.Yarn.V4`, `Tamp.Turbo.V2`, `Tamp.GraphQLCodegen.V5`, `Tamp.Vite.V5` (Vite + Vitest), `Tamp.Playwright.V1`, `Tamp.TruffleHog.V3`, `Tamp.CodeQL.V2`.
- **`[Secret]` resolution chain** (TAM-78 / TAM-79 / TAM-83) — CI vendor masking, env var, OS keychain (macOS `security`, Linux `secret-tool`, Windows `Advapi32`), interactive prompt.
- **CI safety net**: 3-OS matrix (ubuntu/windows/macos) × net8/9/10 on every satellite. Release workflow refuses to pack + push if the commit's CI hasn't passed (TAM-84). Branch protection on `main` requires all three OS legs green before merge.
- Dogfood release pipeline validated end-to-end: every satellite ships through `dotnet tamp Ci && dotnet tamp Push` running in its own GitHub Actions.

### v1.x — Patch + ecosystem fill

- ADR backfill (TAM-78 → ADR series 0003/0004/0005/0008/0010-0014 already drafted, need to land)
- HoldFast pipeline port (TAM-93) — first external (non-Tamp) project on Tamp; owned by the holdfast agent
- Per-wrapper wiki page sweep

### v2 — Adoption

- Schema-driven wrapper generation with AI-assisted bootstrapping from `--help` output (the EF integration tests showed this catches real bugs the unit tests miss; codifying that workflow)
- IDE integration via `tasks.json` / `launch.json` generation (`tamp :ide-config`)
- MCP server mode (`tamp :mcp-server`) exposing targets as callable tools
- Documentation site
- Migration guide from NUKE and Cake
- Community module template and contribution guide

### Out of Scope (for now)

- IDE plugins (Rider, VS extensions). Generated `launch.json` covers VS Code and Rider's run-config import; that's enough.
- Distributed builds. Tamp runs locally and in single CI runners. Bazel-style remote execution is a different project.
- Build script DSLs (YAML, scripted C#). Tamp builds are .NET console projects, period.

---

## Governance

Tamp is community-maintained. Contributions from humans and AI agents are welcome.

The package convention is the contract. Anyone can publish `Tamp.{ToolFamily}.V{n}` packages without coordinating with core maintainers. Core maintainers reserve the right to bless packages as "official first-party" but do not gatekeep what can exist.

Tamp is MIT-licensed. See [LICENSE](LICENSE) and [docs/adr/0007-license-mit.md](docs/adr/0007-license-mit.md) for the rationale.

---

## License

MIT. See [LICENSE](LICENSE).

---

## Supported .NET Versions

Tamp's first-party assemblies (`Tamp.Core`, `Tamp.Cli`, all `Tamp.NetCli.V{N}` modules, future modules) multi-target every .NET release that Microsoft considers in support — both LTS and STS. We track [Microsoft's support calendar](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) exactly: a TFM gets added the day a new release ships and dropped the day Microsoft EOLs it. No Tamp-specific support definition; no skipping STS.

Today (2026), that's `net8.0;net9.0;net10.0`. The full rationale, including the federal/regulated/locked-down VDI consumer cohort that drives the multi-target requirement, is recorded in [ADR 0015 — Target framework strategy](docs/adr/0015-target-framework-strategy.md).

The `Tamp.NetCli.V{N}` package version is independent of the TFM list: a wrapper for `dotnet 8`'s CLI surface (`Tamp.NetCli.V8`) can continue to exist for as long as consumers still have the `dotnet 8` SDK installed, even after `net8.0` is dropped from the wrapper assembly's TFM list. Module retirement is a separate decision, made per module.

---

## Attribution

Tamp draws design lessons from NUKE (target authoring style, IDE integration goals), Bullseye (small-core philosophy, target DAG executor), Bazel (declarative resource consumption, dependency-driven scheduling), and the operator pain documented in NUKE's Discussion #1564 (governance lessons learned the hard way).
