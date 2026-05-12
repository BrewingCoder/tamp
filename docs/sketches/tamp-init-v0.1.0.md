# `tamp init` — v0.1.0 sketch (hybrid: embedded minimal + NuGet-distributed templates)

**Status:** directional sketch (not an ADR, not a spec). Captures the v0.1.0 scope ceiling and the extension points we leave open for later. Lives under `docs/sketches/` because the scope will firm up only after we implement v0.1.0 and see what actually pulls weight.

## North star

A new adopter clones an empty repo (or one with a `.slnx` solution), runs `tamp init`, and ten seconds later has a working `build/Build.cs` they can `tamp Test` against. **Works offline.** Zero documentation reading, zero copy-pasting from the wiki. HoldFast's first three frictions cease to exist.

## Distribution model — hybrid embedded + NuGet

Two channels with different tradeoffs:

| | **Embedded (CLI binary resources)** | **NuGet packages (`Tamp.Templates.*`)** |
|---|---|---|
| Works offline | ✅ | only if cached |
| Version-locked to CLI | ✅ (single SKU) | independent SemVer |
| First-touch latency | zero | NuGet restore (~1-5s on cold cache) |
| Iteration speed | tied to CLI release cadence | template-only release cuts |
| Community contributions | impossible (need PR + CLI release) | trivial (publish your own package) |

**v0.1.0 ships only the embedded channel**, but the architecture treats both channels as instances of the same `IScaffoldTemplateSource` abstraction. The NuGet channel is wired up behind a feature flag and lands in v0.2.0 — implementing it is "discover the package, restore it, load its `IScaffoldTemplate` impl from a typed entry point" with no CLI surface changes.

**Why embed the minimum + remote-distribute the rest?** Federal customers, locked-down corp environments, contractor laptops on hostile networks — `tamp init` is literally the first thing they try. A network round-trip on the first-touch path is the worst possible regression. NUKE's strength is the offline on-ramp; we keep that for the 80% case (`tamp init` with no flags) and lift the offline constraint only when the user explicitly opts into a richer template (`--template <name>`).

## v0.1.0 scope ceiling — minimal scaffold, embedded only

**In:**
- New `init` subcommand on `Tamp.Cli` / `dotnet-tamp` (ships as 1.4.0).
- Embedded `MinimalTemplate` produces exactly three files in the current directory:
  - `build/Build.cs` — minimal Build with Clean / Restore (Internal) / Compile / Test / Default targets, using the 1.3.0 surface.
  - `build/Build.csproj` — references `Tamp.Core` and `Tamp.NetCli.V10` at the CLI's own version (pinned exact, not floating).
  - `.config/dotnet-tools.json` — local tool manifest registering `dotnet-tamp`. Created if absent; left alone if present (we tell the user via stdout to add the entry themselves rather than mangle their existing JSON).
- Solution detection: if exactly one `*.slnx` (or `*.sln`) at the repo root, the scaffold uses it. Zero or multiple → `[Solution]` left without an explicit path and the user is told.
- Idempotency: refuses to overwrite an existing `build/Build.cs`. Exit 1 with a clean message. No `--force` yet.
- Architecture-wise: `IScaffoldTemplateSource` interface in place, with ONE implementation (`EmbeddedTemplateSource`) registered. NuGet source is parse-but-error.

**Out (deferred to 0.2.0+):**
- NuGet template distribution (`Tamp.Templates.Fullstack`, `Tamp.Templates.AspNet`, etc.). Architectural slot reserved; not enabled.
- Template selection (`--template <name>`). v0.1.0 has only one template; later versions add the flag with the existing infrastructure.
- Interactive prompts (`--interactive`).
- CI workflow generation (`--with-ci`). Hand-write CI YAML for now per [ADR 0007](../adr/).
- Dockerfile / Helm chart / Sonar wiring / etc. — these are template-package responsibilities, not CLI scope.
- Monorepo / Yarn-workspace / Turborepo detection. Pure .NET v0.1.0.
- `tamp upgrade` (migrate an existing Build.cs). Separate epic.

## Architecture — extension points

### `ScaffoldContext`

A record assembled by the CLI command before any template runs:

```csharp
public sealed record ScaffoldContext
{
    public required AbsolutePath RepoRoot { get; init; }
    public AbsolutePath? Solution { get; init; }
    public string TampCoreVersion { get; init; } = "1.3.0";          // CLI's own version at build time
    public bool DotnetToolsJsonExists { get; init; }
    public bool BuildCsAlreadyPresent { get; init; }
    public IReadOnlyDictionary<string, string> ProbeData { get; init; } = ReadOnlyDictionary<string, string>.Empty;
    // future probes contribute via ProbeData rather than growing this record
}
```

### `IRepoProbe` (one impl in v0.1.0)

Discovers facts about the working directory. v0.1.0 ships `DotnetSolutionProbe`. Future probes (`YarnWorkspaceProbe`, `DockerfileProbe`) register themselves and contribute to `ScaffoldContext.ProbeData`.

```csharp
public interface IRepoProbe
{
    void Probe(AbsolutePath repoRoot, ScaffoldContextBuilder ctx);
}
```

### `IScaffoldTemplate`

A template emits a sequence of `FileSpec`s. v0.1.0 ships `MinimalTemplate`.

```csharp
public interface IScaffoldTemplate
{
    string Name { get; }                                              // "minimal", future "fullstack", etc.
    string Description { get; }
    string MinimumTampCoreVersion { get; }                            // SemVer constraint; CLI enforces compat
    IEnumerable<FileSpec> Render(ScaffoldContext ctx);
}

public sealed record FileSpec(AbsolutePath Path, string Content, WriteMode Mode);

public enum WriteMode
{
    Create,        // fail if exists
    SkipIfExists,  // leave existing file alone
    Merge,         // deferred — for JSON / YAML merge semantics
}
```

### `IScaffoldTemplateSource` — the hybrid hinge

This is the abstraction that lets v0.2.0 add NuGet-distributed templates without touching `init` command code:

```csharp
public interface IScaffoldTemplateSource
{
    /// <summary>Source identifier (e.g. "embedded", "nuget:Tamp.Templates.Fullstack").</summary>
    string Source { get; }

    /// <summary>Enumerate all templates this source can produce. May be async (NuGet restore).</summary>
    ValueTask<IReadOnlyList<IScaffoldTemplate>> ListAsync(CancellationToken ct);

    /// <summary>Resolve a template by name. Returns null if this source doesn't carry it.</summary>
    ValueTask<IScaffoldTemplate?> ResolveAsync(string name, CancellationToken ct);
}
```

v0.1.0 implementations:
- `EmbeddedTemplateSource` — returns the single `MinimalTemplate` baked into `Tamp.Cli`. Implemented in v0.1.0.
- `NuGetTemplateSource` — interface implemented but throws `NotImplementedException("NuGet template distribution lands in 0.2.0; use embedded templates for now.")`. Scaffolded so the integration path is obvious.

The `InitCommand` walks registered sources in priority order (embedded first for offline guarantee; NuGet later if a flag opts in) and picks the first match.

### `ScaffoldRunner`

Takes a list of `FileSpec`s, applies write policy per spec, returns a summary.

### CLI shape

```
tamp init                                # default: minimal template, current dir, embedded
tamp init --solution path/to/foo.slnx    # explicit solution path
tamp init --dry-run                      # print what would be written; touch nothing
tamp init --list-templates               # list templates from all registered sources
```

Reserved (parse-but-error in v0.1.0):

```
tamp init --template <name>              # 0.2.0 — implies NuGet source if not embedded
tamp init --template-source <pkg-id>     # 0.2.0 — pin to a specific NuGet template package
tamp init --offline                      # 0.2.0 — refuse network fallback
tamp init --force                        # 0.2.0 — overwrite existing
tamp init --with-ci <vendor>             # 0.3.0 — CI workflow generation
tamp init --interactive                  # 0.4.0 — prompted scaffold
```

## NuGet template package shape (for v0.2.0; sketched now so v0.1.0 leaves the right hooks)

Template packages are .NET assemblies that expose `IScaffoldTemplate` implementations through a known entry point — likely an attribute on the assembly (`[assembly: TampTemplateProvider(typeof(FullstackTemplate))]`) or a conventional type name (`Tamp.Templates.Fullstack.TemplateProvider : IScaffoldTemplateProvider`).

Package metadata expected:

```xml
<PackageReference Include="Tamp.Templates.Fullstack" Version="0.1.0" />
<!-- Inside the package's csproj: -->
<MinimumTampCoreVersion>1.3.0</MinimumTampCoreVersion>
<TampTemplateName>fullstack</TampTemplateName>
<TampTemplateDescription>...</TampTemplateDescription>
```

`NuGetTemplateSource` invocation flow (v0.2.0):

1. User runs `tamp init --template fullstack`.
2. CLI looks at registered sources. `EmbeddedTemplateSource.Resolve("fullstack")` returns null.
3. `NuGetTemplateSource.Resolve("fullstack")` triggers a NuGet restore of `Tamp.Templates.Fullstack` (or the user-pinned `--template-source <pkg-id>` if specified).
4. Loaded assembly's `IScaffoldTemplateProvider.GetTemplate()` returns the `IScaffoldTemplate` instance.
5. CLI checks `template.MinimumTampCoreVersion` against the CLI's own version. Refuses with a clean message if mismatched.
6. `Render(ctx)` produces `FileSpec`s. `ScaffoldRunner` writes them.

NuGet's existing infrastructure handles offline package cache, corporate proxies, mirrors, auth, signing — we don't reinvent any of that.

## Drift protection

The risk: a template author bumps `Tamp.Templates.Fullstack` to require `Tamp.Core 1.5.0`, but an adopter still on `Tamp.Cli 1.4.0` runs `tamp init --template fullstack` and gets a generated `Build.cs` that doesn't compile.

Mitigation in v0.2.0 design:

- Every template declares `MinimumTampCoreVersion`. CLI compares against its OWN version (which equals the Tamp.Core version it ships with). Mismatch → friendly error: `"Template 'fullstack' requires Tamp.Core ≥ 1.5.0; this CLI ships 1.4.0. Upgrade: dotnet tool update -g dotnet-tamp"`.
- Template packages can also pin a max — `MaximumTampCoreVersion` — for cases where a template uses a feature that gets removed.
- The CLI's own version is the source of truth, not the installed Tamp.Core in the user's repo (templates would be useless against an empty repo otherwise).

## What `build/Build.cs` looks like at output

Pinned in a snapshot test in `Tamp.Cli.Tests`:

```csharp
using Tamp;
using Tamp.NetCli.V10;

class Build : TampBuild
{
    public static int Main(string[] args) => Execute<Build>(args);

    [Parameter("Build configuration")]
    Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution = null!;

    AbsolutePath Artifacts => RootDirectory / "artifacts";

    Target Clean => _ => _.Executes(() => CleanArtifacts());

    Target Restore => _ => _
        .Internal()
        .Executes(() => DotNet.Restore(s => s.SetProject(Solution.Path)));

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() => DotNet.Build(s => s
            .SetProject(Solution.Path)
            .SetConfiguration(Configuration)
            .SetNoRestore(true)));

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() => DotNet.Test(s => s
            .SetProject(Solution.Path)
            .SetConfiguration(Configuration)
            .SetNoBuild(true)));

    Target Default => _ => _
        .Default()
        .DependsOn(Compile);
}
```

`build/Build.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <RootNamespace>Build</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Tamp.Core" Version="1.3.0" />
    <PackageReference Include="Tamp.NetCli.V10" Version="1.3.0" />
  </ItemGroup>
</Project>
```

`.config/dotnet-tools.json` (only when absent):

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-tamp": {
      "version": "1.3.0",
      "commands": ["dotnet-tamp"]
    }
  }
}
```

## Tests

- Snapshot tests: render the minimal template against a fabricated `ScaffoldContext`, byte-compare against the expected `Build.cs` / `Build.csproj` / `dotnet-tools.json` strings. When templates evolve, snapshots update explicitly.
- Idempotency: `tamp init` twice in the same directory exits 1 the second time.
- Probe tests: solution detection on 0 / 1 / multi-solution layouts.
- Dry-run: `--dry-run` writes nothing; emits the would-write list.
- Architecture smoke: `IScaffoldTemplateSource` registration order — embedded source resolves before NuGet source (even when NuGet is enabled in 0.2.0).
- Reserved-flag stubs: `--template`, `--with-ci`, etc., exit with a "feature lands in 0.X" message rather than crashing on unknown args.

## What ships in v0.1.0

- `Tamp.Cli` / `dotnet-tamp` 1.4.0 — `init` subcommand, the abstractions above, `EmbeddedTemplateSource` with `MinimalTemplate`, `DotnetSolutionProbe`, `NuGetTemplateSource` stub.
- `Tamp.Core` 1.4.0 — version-aligned per TAM-81. No API change.
- Wiki Getting-Started rewritten around `tamp init`. Three-line quick start: `dotnet tool install -g dotnet-tamp` → `cd your-repo && dotnet tamp init` → `dotnet tamp Test`.

## What ships in v0.2.0 (preview, not committed)

- `NuGetTemplateSource` real implementation.
- `Tamp.Templates.Fullstack` (or a less-presumptuous starting name) as the first remote template package — covers a typical .NET-API-plus-React-frontend layout with Docker + Helm + Sonar wiring.
- `--template` and `--list-templates` actually do something.
- Possibly: a template-authoring template (`Tamp.Templates.MetaTemplate` — `tamp init --template meta-template` scaffolds a NEW template package). Recursive but useful for community contributors.

## Open questions to answer during implementation

1. Should the `--solution` flag accept a glob? v0.1.0 requires exact path; lift in 0.2.0.
2. Non-git-repo behavior — warn but proceed; `RootDirectory` resolution in generated `Build.cs` falls back to `AppContext.BaseDirectory`.
3. Pin generated `Tamp.*` versions exact (yes — reproducible-build win; floating versions burned HoldFast twice).
4. Shell shim scripts (`./tamp`, `./tamp.cmd`)? No — `dotnet tamp` via local tool manifest is canonical.
5. NuGet entry-point convention — attribute (`[assembly: TampTemplateProvider(typeof(T))]`) or conventional type name? Settle in v0.2.0; v0.1.0's `IScaffoldTemplateProvider` interface shape needs to accommodate either.

## Pre-implementation tasks

1. `Tamp.Cli` package architecture — where does `init` live? New `Commands/InitCommand.cs`? Existing arg-parsing infrastructure or new?
2. Abstractions land first (`ScaffoldContext`, `IRepoProbe`, `IScaffoldTemplate`, `IScaffoldTemplateSource`, `FileSpec`, `ScaffoldRunner`) — no functionality, just types + interfaces + one implementation each. Tested.
3. `EmbeddedTemplateSource` + `MinimalTemplate` + `DotnetSolutionProbe` wired through `InitCommand`. Snapshot tests pinned.
4. `NuGetTemplateSource` stub (throws on actual restore; passes interface tests).
5. Wiki/README rewrite.
6. Cut Tamp.Cli 1.4.0 + dotnet-tamp 1.4.0.

Total: one focused session for items 1-4, half a session for 5-6.
