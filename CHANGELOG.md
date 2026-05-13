# Changelog

All notable changes to Tamp are documented in this file.

The format follows [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and the project follows [Semantic Versioning 2.0.0](https://semver.org/) per [ADR 0008](docs/adr/) (forthcoming) once we cut a non-pre-release version.

Pre-1.0 versions may break public API freely between minor versions; the `0.x` line is intentionally a stabilization run.

## [Unreleased]

## [1.8.0] — 2026-05-13 — OS temp-path factories + build-scoped scratch dirs (TAM-202)

### Added

DasBook canary feedback was: adopters coming from NUKE expect FS operations
on `AbsolutePath` (path.CreateDirectory(), path.WriteText(), etc.). Most of
that surface was already on the type — we mirrored NUKE's idiom early — but
two real gaps remained: temp-path factories and lifecycle-managed scratch
directories.

**`AbsolutePath` — new static factories**

- **`AbsolutePath.GetTempDirectoryRoot()`** — host OS's temp root as an
  `AbsolutePath`. Non-creating; useful when composing temp paths manually.

- **`AbsolutePath.CreateTempDirectory(string? namePrefix = null)`** — creates
  a uniquely-named subdirectory under the OS temp root, returns the path.
  Default prefix `tamp`; explicit prefix makes post-mortem grepping of
  `/tmp` straightforward.

- **`AbsolutePath.CreateTempFile(string? extension = null)`** — creates a
  uniquely-named empty file under the OS temp root. Leading dot on the
  extension is optional (`".pfx"` and `"pfx"` produce the same result).

These are non-tracked — caller manages cleanup. For build scripts use
`TampBuild.Scratch(...)` instead, which tracks the directory and deletes
it at end of build.

**`TampBuild` — build-scoped scratch lifecycle**

- **`protected AbsolutePath Scratch(string? namePrefix = null)`** — allocate
  a temp directory tied to the build instance's lifetime. Auto-deleted at
  end of `Execute<T>` on every exit path (success, target failure,
  `InvalidOperationException`).

  ```csharp
  class Build : TampBuild
  {
      Target StageMsix => _ => _
          .Executes(() =>
          {
              var staging = Scratch("msix-staging");
              // Build the MSIX layout under `staging`; auto-cleaned at exit.
              Msix.SetAppxManifestVersion(staging / "AppxManifest.xml", Version);
          });
  }
  ```

- **`TAMP_KEEP_SCRATCH=1`** env override — set to preserve scratch dirs
  after the build exits. Accepts any non-empty value except `0` /
  `false` (case-insensitive). Useful for post-mortem inspection. Without
  it, every build cleans up its own `/tmp` debris.

**`AbsolutePath` — new convenience methods**

- **`CreateDirectory()`** — NUKE-style alias for `EnsureDirectoryExists()`.
  Idempotent, returns this for chaining.
- **`EnsureParentDirectoryExists()`** — ensures the parent dir exists.
  Returns this for chaining. Idiomatic before writing a file whose
  containing dir may not exist.
- **`Touch()`** — create an empty file if missing, update mtime if present.
  Parent dir is created as needed.
- **`AppendAllText(string)`** — append to file (create if missing).
- **`SizeBytes()`** — file size in bytes. Throws `FileNotFoundException`
  if the path isn't a file.
- **`CopyToDirectory(AbsolutePath dir, bool overwrite = true)`** —
  file-into-directory copy preserving the filename. Distinct from
  `CopyTo(...)` which treats its arg as the full destination path.

### Notes

- Additive surface only — no breaking changes. Adopters on 1.7.0 upgrade
  without code edits.
- `TampBuild.TemporaryDirectory` (the shared `<repo>/.tamp/temp` location)
  is unchanged; `Scratch(...)` is for per-allocation isolation and
  automatic cleanup, which `TemporaryDirectory` doesn't provide.
- Tests: 30 new unit tests in `AbsolutePathTests` + 12 new in
  `TampBuildScratchTests` (concurrency, env-override variants, idempotence,
  cleanup-with-content, dir-as-source rejection, missing-parent creation).
  Total Tamp.Core.Tests count: 647 → all passing across net8/net9/net10.

## [1.7.0] — 2026-05-13 — `WrapperSettingsBase` inheritance helper (TAM-197)

### Added

- **`public abstract class WrapperSettingsBase`** in `Tamp.Core` — optional base
  class for satellite wrapper settings classes that handle `Secret` values.
  Exposes a `protected static string Reveal(Secret)` helper. Inheritance from
  this base is now an additional approved context for the
  [TAMP004](https://github.com/tamp-build/tamp/wiki/Tamp-Analyzers#tamp004)
  analyzer.

  Discovery benefit: new contributors see "what's the canonical way to reveal
  a secret for a CommandPlan?" in the type system rather than only in analyzer
  documentation. Adopters can derive arbitrarily-named classes (not just
  `*Settings` / `*SettingsBase`) and still pass TAMP004 via the inheritance
  heuristic.

- **TAMP004 analyzer** extended to walk the inheritance chain — any class
  deriving (directly or transitively) from `Tamp.WrapperSettingsBase` is
  treated as approved regardless of its class name. Pure additive change; the
  existing `*Settings` / `*SettingsBase` name heuristic still works for
  non-deriving classes.

  4 new analyzer tests cover the inheritance path: direct inheritance with a
  non-`*Settings`-suffixed name, transitive inheritance through a mid-layer,
  and the negative case where an impostor `FakeBase` outside the `Tamp`
  namespace doesn't auto-approve a derived class.

### Migration

- **No retrofit required.** Existing satellites continue to work via the
  `*Settings` / `*SettingsBase` name heuristic. New satellites can opt in to
  `WrapperSettingsBase` inheritance if they prefer type-system-level
  blessing over the name heuristic.

- Pre-1.7.0 satellites that want stronger discipline can migrate at their own
  cadence. The migration is a single base-class change in the `*SettingsBase`
  file (`public abstract class FooSettingsBase : Tamp.WrapperSettingsBase`)
  + replacing `secret.Reveal()` call sites with `Reveal(secret)`.

## [1.6.0] — 2026-05-13 — kill the IVT-bump-per-satellite churn (TAM-196)

### Changed

- **`Secret.Reveal()` is now `public`** (was `internal`). The `[InternalsVisibleTo]`
  gate had become friction without protection — any IVT-listed assembly could already
  leak the value, and the real masking primitives (`Secret.ToString()` returning
  `<Secret:Name>`, the `CommandPlan.Secrets` collection for process-trace masking,
  and runner-side env-var masking) keep working regardless of `Reveal()`'s
  visibility.

  Every new satellite that handled a service-principal client secret / API key /
  cert password used to require a Tamp.Core minor bump + nuget propagation wait +
  satellite version-constraint update. Across the DasBook + Strata + HoldFast
  onboarding wave that was killing velocity. This change ends it.

  Visibility widening is non-breaking in C# — existing satellites continue to
  compile and run unchanged. Net-new satellites just call `secret.Reveal()`
  without needing to be added to any list anywhere.

### Added

- **TAMP004 Roslyn analyzer** — flags `Secret.Reveal()` calls outside an approved
  context. Approved contexts:
  - Classes whose name ends in `Settings` or `SettingsBase` (the canonical
    Tamp wrapper-settings shape every satellite uses)
  - Tamp framework internals: `Tamp.Core`, `Tamp.Cli`, `Tamp.NetCli.V*`
  - Test code: classes ending in `Tests`, or namespaces containing `.Tests`

  Catches the typical accidental-leak shape: `Logger.LogInformation("token={}",
  secret.Reveal())` from a build script or random helper class. `Secret.ToString()`
  would have masked the value; `Reveal()` defeats the masking.

  Bundled inside `Tamp.Core.nupkg` at `analyzers/dotnet/cs/` (same pattern as
  TAMP001 / TAMP002 / TAMP003) — adopters get it automatically with no extra
  PackageReference.

- 13 unit tests cover the approved-context heuristics + the negative cases that
  should fire.

### Removed (kind of)

- Stopped adding net-new satellites to `Tamp.Core/AssemblyInfo.cs`'s
  `[InternalsVisibleTo]` list. Existing entries are retained — they were there
  for non-Reveal internal surfaces too, and removing them mid-flight is a
  separate audit task. Going forward, no new satellite needs to be added.

### Migration

- **Adopters:** none needed. `Secret.Reveal()` going public is non-breaking; the
  TAMP004 analyzer warns rather than errors by default.
- **Satellite authors:** call `secret.Reveal()` from inside your `*Settings.cs` or
  `*SettingsBase.cs` classes; the analyzer recognizes the shape. If you're
  calling `Reveal()` from elsewhere, you'll get a TAMP004 warning pointing you
  at the recommended pattern (route the value through a `*Settings` class building
  a `CommandPlan`, or use `secret.ToString()` for diagnostic / logging
  purposes).
- **Follow-up:** TAM-197 layers a `WrapperSettingsBase` in Tamp.Core on top of
  this, replacing the analyzer heuristic with type-system-level enforcement.
  Backlogged until current canary-onboarding wave (DasBook + Strata + HoldFast)
  completes.

## [1.5.2] — 2026-05-13 — grant `InternalsVisibleTo` to `Tamp.Sccache`

### Added

- `InternalsVisibleTo("Tamp.Sccache")` so the new compile-cache satellite can
  `Reveal()` Azure connection strings and Redis passwords when emitting the
  backend env vars (`SCCACHE_AZURE_CONNECTION_STRING`, `SCCACHE_REDIS_PASSWORD`).
  Tamp.Sccache 0.1.0 declares a minimum Tamp.Core dependency of `[1.5.2,)`.

## [1.5.1] — 2026-05-13 — grant `InternalsVisibleTo` to `Tamp.MicrosoftStoreCli`

### Added

- `InternalsVisibleTo("Tamp.MicrosoftStoreCli")` so the new satellite can `Reveal()`
  Partner Center service-principal client secrets and certificate passwords when
  building the `msstore reconfigure` command line. No public API surface change in
  this package; pure access grant. Tamp.MicrosoftStoreCli 0.1.0 declares a minimum
  Tamp.Core dependency of `[1.5.1,)` for this reason.

## [1.5.0] — 2026-05-13 — async Executes overloads + TAMP002/TAMP003 analyzers + TAMP001.md doc

### Added

- **Three async `Executes(...)` overloads** on `ITargetDefinition` (TAM-181). Targets can
  now use `async () => await ...` lambdas directly without the `.GetAwaiter().GetResult()`
  bridge ceremony. The framework awaits the returned Task synchronously at the target
  boundary inside the Executor.

  ```csharp
  ITargetDefinition Executes(Func<Task> asyncAction);
  ITargetDefinition Executes(Func<Task<CommandPlan>> asyncPlanFactory);
  ITargetDefinition Executes(Func<Task<IEnumerable<CommandPlan>>> asyncPlanFactory);
  ```

  Surfaced during Strata's PR #226 adoption sweep where `async () => await mgmt.GetConfigReferencesAsync()`
  matched the existing `Executes(Action)` overload and silently no-op'd (state machine became
  `async void`, control returned to Tamp before the await completed). The natural shape now
  binds to `Func<Task>` and behaves correctly.

- **TAMP002 — `TampBuild subclass missing Execute<T>(args) dispatch in Main`** (TAM-179).
  Severity: Error. Fires when the compilation contains a class derived from `Tamp.TampBuild`
  but the assembly's `Main(string[])` method doesn't call `Execute` on a TampBuild-derived
  type. Catches the regression pattern that bit `tamp-ado-git` during the 2026-05-13 wave
  (default empty `Program.cs` Main + missing `build/Build.cs`) — `dotnet tamp Ci` failed
  with CS5001 at Release-pipeline time even though the satellite's slnx CI passed. Scoped
  strictly-to-Main for v0.1 per strata-scott's design vote (b); the call-graph-walk
  refinement (accepting `Execute` in a method Main delegates to) lands when an adopter
  hits it.

- **TAMP003 — `async lambda passed to Executes(Action) becomes async void`** (TAM-182).
  Severity: Warning. Fires at compile time when an async lambda binds to `Executes(Action)`
  rather than `Executes(Func<Task>)` AND the lambda body contains at least one `await`.
  Doubles as a soft adoption signal for the 1.5.0 overloads — adopters who haven't bumped
  Tamp.Core still get a build-time signal that the shape is wrong.

### Docs

- **`docs/analyzers/TAMP001.md`** — help-page target for the `HelpLinkUri` referenced
  by the TAMP001 diagnostic since 1.4.2. Patch authored by strata-scott from the
  adopter-who-just-hit-it perspective; structure trap → why → fix (three patterns) →
  suppression → why this matters. Resolves the 404 from earlier in the wave.

## [1.4.2] — 2026-05-13 — TAMP001 analyzer bundled in Tamp.Core (TAM-175 part a)

### Added

- **`Tamp.Analyzers` ships bundled inside the Tamp.Core nupkg** at `analyzers/dotnet/cs/`.
  Adopters who reference Tamp.Core get the analyzer automatically — no separate package
  install. Disable per-project via `<NoWarn>$(NoWarn);TAMP001</NoWarn>` if needed.

- **TAMP001 — `CommandPlan value is unobserved`** — warns at compile time when a method
  invocation that returns `CommandPlan` (or `IEnumerable<CommandPlan>`) appears as a
  statement expression inside an `Executes(Action)` lambda body. Catches the silent-no-op
  footgun from TAM-175 at compile time rather than at "why did my target report Done in
  6ms with no output?" time. 7 unit tests cover the rule's positive + negative cases.

  Wrong shape that fires TAMP001:
  ```csharp
  Target Build => _ => _.Executes(() => { DotNet.Restore(); DotNet.Build(); });
  //                                       ^^^^^^^^^^^^^^^^^   ^^^^^^^^^^^^^^^
  //                              TAMP001 fires on both — both plans are constructed and dropped.
  ```
  Right shape:
  ```csharp
  Target Build => _ => _.Executes(() => new[] { DotNet.Restore(), DotNet.Build() });
  ```

## [1.4.1] — 2026-05-13 — InternalsVisibleTo additions + Executes(Action) doc warning (TAM-175 part c)

### Added

- **`InternalsVisibleTo` for the new tamp-build satellites** so they can consume `Secret.Reveal()` for command-line construction:
  `Tamp.AdjacentContainer`, `Tamp.AdoGit`, `Tamp.Npm.V10`, `Tamp.AzureAppService`,
  `Tamp.PostgresFlex`, `Tamp.Kudu`. (Pre-existing satellites unchanged.)

### Changed — docs

- **`ITargetDefinition.Executes(...)` XML docs** now call out the three overloads explicitly and warn about the silent-no-op footgun on `Executes(Action)` when the lambda body contains unobserved `CommandPlan` return values. Reference: TAM-175 part c. The companion Roslyn analyzer (TAMP001) is filed separately and lands in a follow-up.

  Wrong shape (silent no-op):
  ```csharp
  Target Build => _ => _.Executes(() => { DotNet.Restore(...); DotNet.Build(...); });
  ```
  Right shape:
  ```csharp
  Target Build => _ => _.Executes(() => new[] { DotNet.Restore(...), DotNet.Build(...) });
  ```

### Notes

- Surfaced during the Strata Tamp adoption (STRATA-426 cutover). strata-scott hit the
  `Executes(Action)` footgun in PR #226; one of the new packages from tonight's wave
  exercises the `Secret.Reveal()` pattern that motivated the `InternalsVisibleTo` adds.

## [1.4.0] — 2026-05-12 — `tamp init` scaffolder + diagnostics emission contract (additive)

### Added — diagnostics emission via `System.Diagnostics.ActivitySource` (ADR 0018)

Tamp.Core now emits native OTel-compatible activity spans + meter signals from build execution, with **zero new dependencies** — uses only the .NET BCL. Same pattern ASP.NET Core uses; adopters wire up OpenTelemetry (or any other backend) by subscribing to the source names. The `Tamp.Otel` satellite (forthcoming) is a sub-100-line convenience that calls `AddSource("Tamp.Build.*")`; in-house pipelines can subscribe directly.

**Three activity sources** (consumers subscribe to any subset):
- `Tamp.Build` — root span per build invocation
- `Tamp.Build.Targets` — one span per executed target
- `Tamp.Build.Commands` — one span per `CommandPlan` dispatched

**One meter** (`Tamp.Build`) — counters for builds/targets/commands executed, histograms for durations, peak memory, GC allocations.

**Tags emitted include** (full taxonomy in ADR 0018):
- **Build span:** target plan, exit code, outcome, host facets (os/arch/cpu/memory), .NET runtime, CI vendor detection, outcome counts (succeeded/failed/skipped/not_run), failure pointer, high-res `duration_ns`, peak working set, failure-handler list.
- **Target span:** name, phase, status, deps, failure mode, duration_ns, start/end working-set RSS, GC allocations + gen0/gen1/gen2 collection counts, CPU time, action/command counts, attempt number (retry-mode reserved).
- **Command span:** executable, args count (never the args themselves — secret-leak surface), working dir, exit code, source target, duration_ns, child-process peak/private/virtual memory, child CPU times (user/system/total), thread/handle counts, stdout/stderr byte counts, had-stdin/had-secrets flags.

**Activity events:** `tamp.build.summary` snapshot event on the root span.

**Polyrepo project identification:** new `[BuildProject(Name=..., Area=...)]` attribute on the build class — **language-agnostic; pure-JS, Python, Rust, or mixed-stack builds use it identically to .NET builds**. Designed for products spread across multiple repos (HoldFast frontend / backend / infra; Strata api / functions / spa) — surfaces as `tamp.build.project.name` / `tamp.build.project.area` tags on the root span. Fallback chain when the attribute is absent: `[Solution]` filename if the build script happens to declare one (.NET-only sweetener; silently skipped otherwise) → repo directory name (works for any stack) → `"unknown"`. The recovery path is itself a tag (`tamp.build.project.name_source = attribute / solution / repodirectory / default`) so consumers can tell which fallback fired. Tamp does NOT require a .slnx / .sln to function — the build script itself is .NET (that's the framework's runtime), but what it builds can be anything (Yarn workspaces, Python packages, Rust crates, Helm charts, raw shell). The `Tamp.Otel` satellite maps the resulting tags to `service.name` / `service.namespace` OTel resource attributes at subscription time.

**Stability contract:** source names, span operation names, tag keys, and outcome vocabulary are pinned by ADR 0018 and verified by unit tests (`TampDiagnosticsEmissionTests`, `BuildProjectInfoTests`). Renaming or removing any tag is a breaking change requiring an ADR amendment. Additions are non-breaking and welcome.

**Zero-overhead when nothing subscribes:** `ActivitySource.StartActivity` returns null with no listeners; all tag-setters are null-guarded.

**Privacy by construction:** `plan.Arguments` content, env-var values, and stdin content never become tag values. Counts and metadata only.

### Added — Wave 10 (bootstrapping epic, v0.1.0 scope)

- **`tamp init`** — `Tamp.Cli` / `dotnet-tamp` gain a new top-level `init` subcommand that scaffolds a Tamp build into the current directory. Three files land:
  - `build/Build.cs` — minimal Build script using the 1.3.0 surface (`.Default()`, `.Internal()`, `CleanArtifacts()`, target-typed `DependsOn`)
  - `build/Build.csproj` — pins `Tamp.Core` and `Tamp.NetCli.V10` at the CLI's own version
  - `.config/dotnet-tools.json` — registers `dotnet-tamp` as a local tool (only if absent — existing tool manifests are preserved)

  Works offline by construction; the minimal template is embedded in the CLI binary. The federal / locked-down-environment on-ramp keeps working.

  ```
  cd your-empty-repo
  dotnet tool install -g dotnet-tamp
  dotnet tamp init
  dotnet tool restore && dotnet tamp Test
  ```

- **Solution detection** — `DotnetSolutionProbe` finds a single `.slnx` (or `.sln`) at the repo root and reports the detection result. Zero / multiple solution layouts surface via a probe-diagnostic message and the generated `Build.cs` falls back to `[Solution]` auto-discovery.

- **Idempotency** — `tamp init` refuses to overwrite an existing `build/Build.cs`. `--force` reserved for 0.2.0.

- **Flags** (v0.1.0): `--solution <path>`, `--dry-run`, `--list-templates`, `--help`.

- **Reserved flags** (parsed with helpful errors; implementations land in later versions): `--template <name>` (0.2.0), `--template-source <pkg>` (0.2.0), `--offline` (0.2.0), `--force` (0.2.0), `--with-ci <vendor>` (0.3.0), `--interactive` (0.4.0).

### Extension architecture (forward-looking, v0.2.0+)

The scaffolding stack is structured around two abstractions so future expansion is additive, not a refactor:

- **`IScaffoldTemplate`** — declares its `Name`, `Description`, and `MinimumTampCoreVersion`. Future templates implement this without touching the `init` command.
- **`IScaffoldTemplateSource`** — pluggable provider of templates. v0.1.0 ships:
  - `EmbeddedTemplateSource` (real) — the minimal template baked into the CLI binary, offline-capable.
  - `NuGetTemplateSource` (stub; throws with a clean "lands in 0.2.0" message) — the slot reserved for the NuGet-distributed template channel. When the implementation lands, `Tamp.Templates.Fullstack` / `Tamp.Templates.AspNet` / community packages become available via `tamp init --template <name>`. CLI registers sources in priority order (embedded first → offline on-ramp guarantee preserved).
- **`IRepoProbe`** — discovers facts about the working dir, contributes to `ScaffoldContext`. v0.1.0 ships `DotnetSolutionProbe`; future probes (`YarnWorkspaceProbe`, `DockerfileProbe`, `HelmChartProbe`) layer in additively.
- **Drift protection** — every template declares `MinimumTampCoreVersion`; CLI refuses on mismatch with an upgrade message. Template version skew across the ecosystem is bounded.

### Documentation

- Wiki **[Getting-Started](https://github.com/tamp-build/tamp/wiki/Getting-Started)** rewritten around `tamp init` as the three-line on-ramp.
- **`docs/sketches/tamp-init-v0.1.0.md`** captures the v0.1.0 scope ceiling, the extension architecture, and the v0.2.0 preview.

[1.4.0]: https://github.com/tamp-build/tamp/releases/tag/v1.4.0

## [1.3.0] — 2026-05-12 — `params Target[]` overloads + HoldFast cutover surface (additive)

### Added — TAM-162

- **`params Target[]` overloads on lifecycle dependency methods** — `DependsOn`, `After`, `Before`, `Triggers`, `TriggeredBy`, `OnFailureOf`. Resolves HoldFast's friction #14 from the 1.2.0 refactor. User writes:

  ```csharp
  Target Ci => _ => _
      .Default()
      .DependsOn(Test, Publish, FrontendBuild, DockerBuildBackend);
  ```

  Names resolve via a `Method → property-name` map the framework builds during target collection (the same reflection pass that registers targets). NUKE's approach — delegate equality via `MethodInfo` is stable across property-getter invocations because the lambda body compiles to a single method.

  Single-arg overloads (`DependsOn(Restore)`) keep using `[CallerArgumentExpression]` for source-identifier capture. The `params string[]` overloads stay for dynamic name composition. All three forms produce byte-equal `TargetSpec.Dependencies` lists.

  10 new tests in `CallerArgExprDependsOnTests` cover varargs for each of the 6 lifecycle methods, the 4-arg HoldFast-shape case, plus null-target and unmapped-delegate guards.

### Companion satellite ships in this wave

- **`Tamp.Helm.V3` 0.1.0** — new satellite. Verbs: `Upgrade`, `Template`, `Lint`, `Package`, `Push`. Both fluent `(Tool, Action<TSettings>)` and object-init `(Tool, TSettings)` overloads from day-1. Settings spec per HoldFast's cutover ask covers the full `helm upgrade --install` surface (Chart, Release, Namespace, Version, Wait, Timeout, CreateNamespace, Atomic, AddValuesFile, SetValue, AddValues, Force, ReuseValues, ResetValues, WaitForJobs, HistoryMax, Description). `Push.SetPlainHttp(bool)` for in-cluster `localhost:32000`-style registries. `Package.SetSign(true)` requires a `Secret` passphrase (Helm v3 has no `--passphrase-file`; the wrapper attaches it to `CommandPlan.Secrets` for redaction and documents the gpg-agent / loopback-pinentry CI setup).

- **`Tamp.Http` 0.1.1** — adds `HttpProbe.WaitForHealthy(url, timeout, ...)` static helper for post-deploy smoke targets. Optional params: `interval` (default 2s), `headers`, `isHealthy` async predicate (for body-content checks), `HttpClient` (for self-signed certs / proxies), `CancellationToken`. Transient errors (`HttpRequestException`, per-request `HttpClient` timeout) are treated as retryable. `TimeoutException` on budget exhaustion includes last status, attempt count, last transport error.

### Documentation

- **Wiki Pitfalls page** — new. Covers the CleanArtifacts blast-radius pattern (HoldFast's friction #12 headliner), yarn berry post-disaster recovery (`rm -rf node_modules .yarn/install-state.gz`), and BuildKit migration warnings (`SecretsUsedInArgOrEnv` informational lint).
- **`Build-Script-Authoring` → Common idioms section** — image-tag idiom, fan-out target with `params Target[]`, post-deploy smoke probe with `HttpProbe.WaitForHealthy`.
- **`Tamp-Docker` page** — Docker.Push insecure-registry / multi-tag / `[Secret]`-credential idioms sections, buildx-warnings migration callout.

[1.3.0]: https://github.com/tamp-build/tamp/releases/tag/v1.3.0

## [1.2.0] — 2026-05-11 — Object-init overloads + ADR backfill (additive)

### Added — TAM-161

- **Object-init overloads** on every `Tamp.NetCli.V8/V9/V10` wrapper method that accepts settings: `Restore`, `Build`, `Clean`, `Test`, `Pack`, `Publish`, `NuGetPush`, `Format`, `FormatWhitespace`, `FormatStyle`, `FormatAnalyzers`. Both authoring styles are supported:

  ```csharp
  // Fluent (canonical in docs and `tamp init` templates):
  DotNet.Build(s => s.SetProject(Solution.Path).SetConfiguration(Configuration));

  // Object-init (alternative):
  DotNet.Build(new() { Project = Solution.Path, Configuration = Configuration });
  ```

  Both produce byte-equal CommandPlans. NUKE migrants stay in the fluent paradigm; new adopters who prefer C# object-initializer syntax get a matching shape.

### Satellite fanout — coordinated with this release

The 1.2.0 cut also lands object-init overloads across every first-party satellite that exposes verb wrappers (17 satellites; 6 satellites are typed-client/orchestrator shape and N/A). Tool-bound wrappers (`(Tool, Action<TSettings>)`) gain `(Tool, TSettings)` siblings — same pattern, preserves tool resolution. Each satellite cuts its own patch bump alongside this release; see each satellite's CHANGELOG for verb counts. Adopters who pin satellites get the new shape on next restore.

Surface summary (overload counts per satellite with verb wrappers):

| Satellite | New overloads | Version |
|---|---|---|
| `Tamp.Docker.V27` | 27 (top-level + Compose + Buildx) | 0.3.0 → 0.3.1 |
| `Tamp.EFCore.V8/V9/V10` | 13 × 3 = 39 | 0.2.0 → 0.2.1 |
| `Tamp.AzureCli.V2` | 15 (top-level + Group + Account + Bicep) | 0.1.0 → 0.1.1 |
| `Tamp.CodeQL.V2` | 14 (Database + GitHub + Resolve + Pack + Query + top-level) | 0.1.0 → 0.1.1 |
| `Tamp.Yarn.V4` | 13 (top-level + Workspaces + Npm) | 0.1.0 → 0.1.1 |
| `Tamp.GitHubCli.V2` | 10 (Pr + Issue + Release + Api) | 0.1.0 → 0.1.1 |
| `Tamp.Turbo.V2` | 9 | 0.2.0 → 0.2.1 |
| `Tamp.Vite.V5` | 9 (Vite + Vitest) | 0.1.0 → 0.1.1 |
| `Tamp.Playwright.V1` | 9 | 0.1.0 → 0.1.1 |
| `Tamp.Bicep` | 5 | 0.1.0 → 0.1.1 |
| `Tamp.AzureFunctionsCoreTools.V4` | 5 (`Func.*`) | 0.1.0 → 0.1.1 |
| `Tamp.TruffleHog.V3` | 4 | 0.1.0 → 0.1.1 |
| `Tamp.GraphQLCodegen.V5` | 2 | 0.1.0 → 0.1.1 |
| `Tamp.AzureStaticWebApps.V2` | 2 (`Swa.*`) | 0.1.0 → 0.1.1 |
| `Tamp.SonarScanner.V10` | 2 (Begin + End) | 0.3.0 → 0.3.1 |
| `Tamp.GitVersion.V6` | 1 | 0.1.0 → 0.1.1 |
| `Tamp.ReportGenerator.V5` | 1 | 0.1.0 → 0.1.1 |

N/A (typed-client/orchestrator shape — no `CommandPlan`-returning verb wrappers): `Tamp.Http`, `Tamp.Coverlet.V6`, `Tamp.AdoRest.V7`, `Tamp.AdoServiceConnection.V1`, `Tamp.ServiceBus.V7`, `Tamp.Testcontainers.V4`.

### Documentation backfill

- **9 ADRs written** (TAM-9 through TAM-20): ADR 0003 (build scripts are .NET console projects), 0004 (CommandPlan as universal wrapper output), 0005 (Secret as a distinct type), 0008 (SemVer policy), 0010 (HostProfile built once at startup), 0011 (Target authoring via fluent property DSL), 0012 (declarative resource consumption), 0013 (schema-driven wrapper generation), 0014 (shell strategy: pwsh-only when shell is required). All under `docs/adr/`.
- **README + `docs/index.md` refresh** — version table reflects every shipped satellite; status line current; new-surface highlights from 1.1.0 + 1.2.0 (`.Default()`, `.Internal()`, `CleanArtifacts()`, `[FromPath]`, `[FromNodeModules]`, `[CallerArgumentExpression]` overloads, object-init overloads).
- **Wiki refresh** — Build-Script-Authoring + Module-Catalog + per-wrapper pages reflect the 1.1.0+ shape (no `.TopLevel()`, no `nameof()`, examples lead with the modern surface).

[1.2.0]: https://github.com/tamp-build/tamp/releases/tag/v1.2.0

## [1.1.0] — 2026-05-11 — Build.cs concision pass (BREAKING)

### Breaking — `.TopLevel()` default inverted

- **Targets are listable + callable by default.** `.TopLevel()` is now a no-op marked `[Obsolete]` for back-compat.
- **`.Internal()` is the new opt-out.** Internal targets are hidden from `--list` AND non-callable from CLI. Direct invocation fails with a friendly error listing dependent targets.

**Migration**:

| Before (1.0.x) | After (1.1.0) |
|---|---|
| `Target Pack => _ => _.TopLevel().Executes(...);` | `Target Pack => _ => _.Executes(...);` (TopLevel was the default-explicit marker; now implicit) |
| `Target _Helper => _ => _.Executes(...);` (was hidden via "no TopLevel anywhere") | `Target _Helper => _ => _.Internal().Executes(...);` (explicit opt-out) |

The `.TopLevel()` call sites still compile (no-op + obsolete warning). The TAMP001 Roslyn analyzer (forthcoming, separate package) will offer a code-fix to delete them.

`.Internal()` and `.Default()` are mutually exclusive — declaring both throws at startup. Internal targets with no incoming dependency edges emit a stranded-internal warning ("will never run") but the build continues.

### Added — kill `nameof()`

- **`[CallerArgumentExpression]` overloads** on `DependsOn`, `After`, `Before`, `Triggers`, `TriggeredBy`, `OnFailureOf`. User writes `.DependsOn(Restore)`; the C# 11+ compiler injects `"Restore"` as the captured-expression string. No `nameof()`, no Expression trees, zero runtime overhead.
- Existing string overloads remain — `.DependsOn("Restore")` and `.DependsOn(nameof(Restore))` still compile and work.
- Validator normalizes the captured expression (strips leading `this.`) and rejects complex expressions (`Restore ?? Compile`, method calls) with helpful error messages.
- IntelliSense filters to `Target`-typed members of the build class when the user types `_.DependsOn(`. Significantly better discovery than `nameof()` which surfaced every member.

### Added — `.Default()` decorator

- Any target can opt into being the default invocation target via `.Default()`, regardless of its name. Replaces the convention-based "target literally named `Default` or `Ci`" fallback as the canonical mechanism.
- At most one target may be marked default per build (across partial-class files too — reflection collects them uniformly). Multiple defaults throw at startup with all names listed.
- Name-based `Default`/`Ci` fallback preserved for back-compat when nothing is marked.

### Reference

Concision impact on the canonical Compile target:

```csharp
// 1.0.x (7 lines):
Target Compile => _ => _
    .TopLevel()
    .DependsOn(nameof(Restore))
    .Executes(() => DotNet.Build(s => s.SetProject(Solution.Path).SetConfiguration(Configuration)));

// 1.1.0 (5 lines):
Target Compile => _ => _
    .DependsOn(Restore)
    .Executes(() => DotNet.Build(s => s.SetProject(Solution.Path).SetConfiguration(Configuration)));
```

Total: 561 Tamp.Core tests green (up from 525) across net8/9/10.

[1.1.0]: https://github.com/tamp-build/tamp/releases/tag/v1.1.0

## [1.0.10] — 2026-05-11

### Added — HoldFast trial wave 6 (TAM-127, Critical)

- **`TampBuild.CleanArtifacts()` helper.** Replaces the unsafe `RootDirectory.GlobDirectories("**/bin", "**/obj")` pattern previously documented as the recommended `Clean` shape. The helper iterates `Solution.Projects` (typed contract) and deletes only each project's `bin/` and `obj/` directories plus the conventional `artifacts/` root. NEVER globs the repo tree.
  - **Self-deletion guard built in**: skips the project whose entry assembly is currently executing (detected via `Assembly.GetEntryAssembly().Location`). No `build/`-exclusion boilerplate required.
  - **Solution resolution**: when called without arguments, reflects over the build instance for a `[Solution]`-injected `Solution` field or property and uses it.
  - **Idempotent**: re-runs are safe.
  - Canonical usage:
    ```csharp
    class Build : TampBuild
    {
        [Solution] readonly Solution Solution = null!;
        Target Clean => _ => _.Executes(() => CleanArtifacts());
    }
    ```

### Background

HoldFast trial friction #12 demonstrated catastrophic blast radius on the `**/bin` glob pattern previously documented in TAM-118: 531 tracked files deleted in 14 seconds on the real HoldFast monorepo. The pattern matched `node_modules/*/bin/`, tracked SDK source dirs (Ruby `bin/console`, Node `bin/clean-dist.sh`), and Playwright-fixture-bearing `tests/**/bin/Debug/.playwright/`. The fix at the framework level — not just the docs — closes the foot-gun for every adopter.

[1.0.10]: https://github.com/tamp-build/tamp/releases/tag/v1.0.10

## [1.0.9] — 2026-05-11

### Added — HoldFast trial wave 2 (Tamp.NetCli.V8 / V9 / V10)

- **`DotNet.Clean()` verb (TAM-112).** Wraps `dotnet clean` across all three NetCli majors. Settings cover the standard knobs: Project (positional), Configuration, Framework, Runtime, Output, NoLogo, Verbosity. Closes the wrapper-completeness gap holdfast hit when porting their Clean target.
- **TRX overwrite mitigation for solution-mode `dotnet test` (TAM-111).** When `SetProject` targets a `.sln`/`.slnx` AND a TRX logger string carries a static `LogFileName=foo.trx`, the wrapper rewrites it to `LogFilePrefix=foo` so VSTest auto-disambiguates the output per assembly. Previously, solution-mode runs against multiple test projects overwrote the same TRX once per project — only the LAST assembly's results survived (holdfast saw 1,152 of 3,172 tests in the final file). Default-on for the friendlier shape; opt out via `SetAutoExpandTrxForSolution(false)` to preserve literal logger strings. Non-TRX loggers, TRX loggers without `LogFileName`, and `.csproj` projects pass through unchanged.

### Notes

- Tamp.Core itself is unchanged from 1.0.8 — the version bump is a Directory.Build.props artifact of TAM-81's monolithic-version convention. Consumers of Tamp.Core alone can stay on 1.0.8.

[1.0.9]: https://github.com/tamp-build/tamp/releases/tag/v1.0.9

## [1.0.8] — 2026-05-11

### Added — HoldFast trial wave 1 (TAM-108, 109, 113, 115, 116)

- **`[FromPath("name")]` attribute + `Tool.FromPath()` / `Tool.TryFromPath()` factories (TAM-115).** Resolve native executables on `PATH` with Windows extension probing (`.cmd / .exe / .bat / .ps1 / no-ext`). `Optional = true` injects null when missing instead of throwing. Closes the Yarn / Turbo / Docker / git native-tool gap — previously every consumer hand-rolled 25 lines of `ResolveOnPath` boilerplate.
- **`[FromNodeModules("name")]` attribute + `Tool.FromNodeModules()` / `Tool.TryFromNodeModules()` factories (TAM-116).** Resolve tools installed as workspace devDeps under `<projectRoot>/node_modules/.bin/<name>`. On Windows probes the `.cmd` shim first. `ProjectRoot` defaults to `TampBuild.RootDirectory`; override for nested workspaces. Pair with a `Yarn.Install`-DependsOn so the resolution runs after install. Error message includes a `yarn install` hint when the binary isn't present.
- **`[Solution]` positional ctor (TAM-109).** `[Solution("src/dotnet/Foo.slnx")]` now compiles and matches `[Solution(Path = "src/dotnet/Foo.slnx")]`. Friction-#3 DWIM-fix.
- **`[Solution]` subtree discovery (TAM-108).** When no `.slnx`/`.sln` lives at `RootDirectory`, walks the subtree (skipping `node_modules`, `bin`, `obj`, `.git`, `artifacts`, `.vs`, `.idea`, `TestResults`). A single subtree match auto-resolves; multiple matches throw a helpful error listing candidates and pointing at `[Solution("...")]` as the fix. Monorepo-friendly.

### Fixed

- **`AbsolutePath.GlobDirectories("**/bin")` returning 0 hits (TAM-113).** The underlying `Microsoft.Extensions.FileSystemGlobbing.Matcher` is file-oriented — `**/bin` returned nothing because no FILE was literally named `bin`. Rewrote to walk every directory and test each relative path against the matcher. `"**/bin"`, `"**/obj"`, `"*/bin"` (top-level only), and overlapping-pattern dedupe all behave as a build-script consumer would expect.

[1.0.8]: https://github.com/tamp-build/tamp/releases/tag/v1.0.8

## [1.0.7] — 2026-05-11

### Added

- **Stale-branch pre-PR gate (TAM-105).** New `GitRepository.AssertNotStale(maxCommitsBehind, baseRef, fetch)` extension method that fetches the comparison ref and counts commits ahead via `git rev-list --count`. Throws `StaleBranchException` with a structured `StaleBranchReport` (BaseRef / CommitsBehind / MaxAllowed / IsStale / FetchPerformed). Companion `CheckStaleness` overload returns the report without throwing.
  - Default threshold: 20 commits behind `origin/main`.
  - Tunable per-call: thresholds (including 0 for strict-mode "ANY commits behind"), base ref (`origin/master`, `upstream/develop`, etc.), fetch opt-out, timeout.
  - Pluggable `IGitRunner` seam (internal) so tests don't shell out — 21 new tests cover threshold boundaries, fetch parsing, error propagation (fetch failure, rev-list failure, non-numeric output), and argument guards.
  - Background: GitHub / ADO's three-way auto-merge can succeed mechanically against a branch that's far behind, resurrecting deleted files or replaying old logic against a new schema. The gate forces conflicts to surface locally before push.

[1.0.7]: https://github.com/tamp-build/tamp/releases/tag/v1.0.7

## [1.0.6] — 2026-05-11

### Added

- `InternalsVisibleTo` entry for `Tamp.ServiceBus.V7`. The original pre-load (1.0.4) speculatively granted V8, but `Azure.Messaging.ServiceBus` is still on major 7.x — and the Tamp convention is "V tracks SDK major" (see `Tamp.AzureCli.V2`, `Tamp.AzureStaticWebApps.V2`). V8 stays in place for the future SDK bump.

[1.0.6]: https://github.com/tamp-build/tamp/releases/tag/v1.0.6

### Ecosystem

- **`Tamp.*` NuGet prefix reserved** to the `Tamp` account on nuget.org (confirmed by NuGet support 2026-05-10). All future `Tamp.*` packages publish with the verified-publisher checkmark; only the `Tamp` account can claim package IDs under the prefix.
- **HoldFast wrapper sprint shipped** — TAM-85 through TAM-92. Seven new satellite packages live on nuget.org: `Tamp.Yarn.V4`, `Tamp.Turbo.V2`, `Tamp.GraphQLCodegen.V5`, `Tamp.Vite.V5` (Vite + Vitest), `Tamp.Playwright.V1`, `Tamp.TruffleHog.V3`, `Tamp.CodeQL.V2`. `Tamp.Docker.V27` extended with `compose` + `buildx` sub-facades in 0.2.0 (TAM-87).
- **Strata adoption roadmap landed in TAM project** — TAM-94 through TAM-106 cover Azure / ADO / IaC / testing wrappers requested by Strata-Scott. Tier 1 (blockers): `Tamp.AzureCli.V2`, `Tamp.AzureStaticWebApps.V2`, `Tamp.Bicep`, `Tamp.AdoRest.V7`. Naming: full-word `Azure*` prefix for the Azure family (matches Microsoft SDK convention).

## [1.0.5] — 2026-05-11

### Added

- `InternalsVisibleTo` entry for `Tamp.Http` — the new foundation library for HTTP-API wrappers (TAM-97 and beyond). Downstream library-mode wrappers (`Tamp.AdoRest.V7`, future `Tamp.GitHubApi`, `Tamp.YouTrackApi`, `Tamp.JiraApi`) subclass `TampApiClient` from Tamp.Http and don't need direct Tamp.Core IVT — only Tamp.Http does the `Secret.Reveal()` for auth-header construction.

[1.0.5]: https://github.com/tamp-build/tamp/releases/tag/v1.0.5

## [1.0.4] — 2026-05-11

### Added

- `InternalsVisibleTo` entries pre-loaded for the Strata-roadmap wrapper assemblies (TAM-94 through TAM-103): `Tamp.AzureCli.V2`, `Tamp.AzureStaticWebApps.V2`, `Tamp.Bicep`, `Tamp.AdoRest.V7`, `Tamp.AdoServiceConnection.V1`, `Tamp.AzureFunctionsCoreTools.V4`, `Tamp.Coverlet.V6`, `Tamp.Testcontainers.V4`, `Tamp.ServiceBus.V8`. Same pre-loading pattern used for 1.0.3 — avoids a cascade of patch bumps as the wrappers ship.

[1.0.4]: https://github.com/tamp-build/tamp/releases/tag/v1.0.4

## [1.0.3] — 2026-05-10

### Added

- `InternalsVisibleTo` entries for all 7 HoldFast satellite assemblies (`Tamp.Yarn.V4`, `Tamp.Turbo.V2`, `Tamp.Vite.V5`, `Tamp.GraphQLCodegen.V5`, `Tamp.Playwright.V1`, `Tamp.TruffleHog.V3`, `Tamp.CodeQL.V2`) so their settings classes can call `Secret.Reveal()` for stdin/argv emission. Added preemptively at 1.0.3 ahead of the wrapper roll-out to avoid a cascade of patch bumps.

[1.0.3]: https://github.com/tamp-build/tamp/releases/tag/v1.0.3

## [1.0.2] — 2026-05-10

### Added

- **OS keychain leg** for `[Secret]` resolution (TAM-83). `SecretBinder.Bind` consults the host's native secret store after the env-var leg, before interactive prompt. Backends:
  - **macOS**: `security find-generic-password` CLI.
  - **Linux**: `secret-tool lookup` (libsecret).
  - **Windows**: P/Invoke to `Advapi32.CredReadW`.
  - Service / target name fixed at `tamp`; account is the resolved env-var name.
  - Live keychain validated end-to-end via the macOS `security` CLI.
  - 7 new tests (447 → 454 in `Tamp.Core.Tests`).
  - Opt out per-secret with `[Secret(UseKeychain = false)]` (default `true`). Env still wins over keychain when both are present.
- **CI status gate** in `release.yml` plus a templated `ci.yml` rolled out to the 6 satellite repos. Tag-driven release polls `gh run list --workflow CI --commit $SHA` for up to 10 minutes; refuses to pack + publish if CI conclusion isn't `success`. Skipped on `workflow_dispatch` (manual land-grab path).
- **Branch protection** on `main` requires all three `build & test (<os>)` status checks before merge. Applied across all 7 tamp-build repos.

### Fixed

- **TAM-84** — two `Tamp.DotNetCoverage.V18` tests previously failing on `windows-latest` (`Collect_Executable_Is_The_Tool_Path`, `Merge_AddInputs_Accepts_AbsolutePath_Sequence`). Both had hardcoded forward-slash path assertions that didn't survive `AbsolutePath`'s `Path.GetFullPath` normalization on Windows. Now compare through the same `AbsolutePath.Value` they emit.

[Unreleased]: https://github.com/tamp-build/tamp/compare/v1.0.5...HEAD
[1.0.2]: https://github.com/tamp-build/tamp/releases/tag/v1.0.2

## [1.0.1] — 2026-05-10

### Added

- **`SecretBinder`** wires `[Secret]`-annotated members to the env-var resolution chain promised by `SecretAttribute`'s docstring (TAM-78). Resolution order: explicit assignment in the build script > `EnvironmentVariable` override > `UPPER_SNAKE_CASE` of member name. `Secret.Name` is sourced from the attribute's `Description` (or explicit `Name` override) so the redaction label is human-readable.
- **Interactive prompt leg** via `SecretBinder.EnsureResolved` (TAM-79). When a `[Secret]` field is still null at .Requires() time AND a TTY is attached AND the attribute's `AllowInteractivePrompt` is true (default), the runner can prompt for the value. Opt out per-secret with `[Secret(AllowInteractivePrompt = false)]` for CI-only secrets that must never block on input.
- **CI vendor masking** integration (TAM-79). When a `Secret` resolves under GitHub Actions, Tamp emits `::add-mask::<value>` so the runner scrubs the value from subsequent log lines (defense in depth beyond Tamp's in-process `RedactingTextWriter`). Azure DevOps gets `##vso[task.setvariable variable=...;issecret=true]<value>`. Other vendors fall back to in-process redaction only.
- Tamp main's own `Sonar` target now uses `[Secret] readonly Secret SonarToken` (was a manual `Environment.GetEnvironmentVariable` workaround). The pattern propagates to all satellite repos in the same release.

### Changed

- `SecretAttribute` gains an `AllowInteractivePrompt` property (default `true`).

[1.0.1]: https://github.com/tamp-build/tamp/releases/tag/v1.0.1

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

[1.0.0]: https://github.com/tamp-build/tamp/releases/tag/v1.0.0
[0.0.1-alpha]: https://github.com/tamp-build/tamp/releases/tag/v0.0.1-alpha
