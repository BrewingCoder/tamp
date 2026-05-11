# HoldFast → Tamp 1.2.0 handoff

**Draft.** Send as PR comment or Slack message once 1.2.0 is on nuget.org.

---

Hey — Tamp 1.2.0 just dropped. This release bundles everything we surfaced during the integration trial (TAM-107..117 frictions) plus the concision pass (TAM-159) and the satellite-wide object-init fanout (TAM-161). One coordinated cut.

## What's new for your pipeline

### Concision — your `Build.cs` gets shorter

**Before** (1.0.x):
```csharp
Target Compile => _ => _
    .TopLevel()
    .DependsOn(nameof(Restore))
    .Executes(() => DotNet.Build(s => s.SetConfiguration(Configuration)));

Target Clean => _ => _
    .TopLevel()
    .Executes(() =>
    {
        foreach (var d in RootDirectory.GlobDirectories("**/bin", "**/obj")) d.Delete();
        Artifacts.Delete();
    });
```

**After** (1.2.0):
```csharp
Target Compile => _ => _
    .DependsOn(Restore)                      // CallerArgumentExpression — no nameof()
    .Executes(() => DotNet.Build(s => s.SetConfiguration(Configuration)));

Target Clean => _ => _
    .Executes(() => CleanArtifacts());       // self-evict-safe helper
```

Migration cost: ~10 minutes per build script. The legacy `.TopLevel()` and `nameof()` calls keep compiling (obsoletion warnings only); strip them at your own pace.

### `CleanArtifacts()` — the fix for the 531-file blast radius

You saw the incident first-hand on our end during the trial. `CleanArtifacts()` ships the safe wipe pattern:

- scoped to `Solution.Projects` (no globbing across unrelated trees)
- self-deletion guard via `Assembly.GetEntryAssembly()` (won't evict its own bin/obj while running from there)
- pair-wise project-level call works too: `CleanArtifacts(RootDirectory / "src" / "MyLib")`

If you've been carrying a custom Clean target with the pre-1.0.8 GlobDirectories pattern, replace it. The helper is the canonical path forward.

### Object-init overloads — pick the style you prefer

Every wrapper verb now ships both shapes. Pick whichever reads cleaner per-call; you can mix freely.

```csharp
// fluent (canonical in docs and `tamp init` templates)
DotNet.Pack(s => s
    .SetProject("src/HoldFast.Api/HoldFast.Api.csproj")
    .SetOutput(Artifacts)
    .SetConfiguration(Configuration)
    .SetIncludeSymbols(true));

// object-init (alternative — flatter, target-typed new())
DotNet.Pack(new()
{
    Project = "src/HoldFast.Api/HoldFast.Api.csproj",
    Output = Artifacts,
    Configuration = Configuration,
    IncludeSymbols = true,
});
```

Both produce byte-equal `CommandPlan`s. Fanout covers every `Tamp.*` package you're using: `DotNet`, `Docker`, `Yarn`, `Vite`, `Playwright`, `TruffleHog`, `CodeQL`, `Bicep`, `Sonar`, `EFCore`, `Func`, `Swa`, `Turbo`, `GitHubCli`, `GitVersion`, `ReportGenerator`, `GraphQLCodegen`, `AzureCli`. The HoldFast pipeline doesn't touch `Tamp.Http` / `Tamp.Coverlet` / `Tamp.AdoRest` / `Tamp.AdoServiceConnection` / `Tamp.ServiceBus` / `Tamp.Testcontainers`, but FYI those are typed-client/orchestrator shape and don't participate in the verb-wrapper API — they stay on their existing surface.

## Updating

```bash
# in HoldFast's Directory.Packages.props (or where you pin Tamp.*):
<PackageVersion Include="Tamp.Core" Version="1.2.0" />
<PackageVersion Include="Tamp.NetCli.V10" Version="1.2.0" />
# satellites bump independently — see each package's CHANGELOG for their version
```

Then a clean restore should pull everything cohesively.

## Governance update (ADRs 0016 + 0017)

While you're caught up: we added two governance ADRs that affect contributor-side timing.

- **ADR 0016** — decision-maker silence forfeiture. If a named reviewer doesn't weigh in within the decision's scope-appropriate timeframe (7d routine → 30d major), the decision proceeds without them. Applies to BDFL too; no retroactive vetoes.
- **ADR 0017** — PR staleness auto-close. PRs idle on author response after a review request get warned at 14d / 30d / 45d, auto-closed at 60d. Three pause mechanisms (`needs-time` label, draft mode, pinned PR). Reopen-by-push is the recovery path.

Both ADRs ship in this release. If HoldFast contributes upstream, these are the timing rules.

## What to flag back

If you hit anything during the upgrade, file against `tamp-build/tamp` with the `holdfast-trial` label so we keep the feedback loop tight.

---

— Scott
