# Tamp

> Pack the build down tight.

A small-core, plugin-driven build automation framework for .NET 10 and beyond. Cross-platform. Honest about resources. Forkable.

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
| `Tamp.Cli` | Global tool (`dotnet tool install -g Tamp.Cli`) |
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
    <PackageReference Include="Tamp.Core" Version="1.0.0" />
    <PackageReference Include="Tamp.NetCli.V10" Version="1.0.0" />
    <PackageReference Include="Tamp.Docker.V27" Version="1.0.0" />
  </ItemGroup>
</Project>
```

Run via the global tool (`tamp ci`) or directly (`dotnet run --project build -- ci`). Both routes produce identical behavior. The global tool exists for ergonomics; nothing depends on it.

---

## Target Authoring (Sketch)

Targets are properties on a build class. Phase, dependencies, parameters, and the work itself are all declared inline. The shape is similar to NUKE's, deliberately, because the syntax is good — it's the architecture underneath that needed rethinking.

```csharp
using Tamp.Core;
using Tamp.NetCli.V10;
using Tamp.Docker.V27;

class Build : TampBuild
{
    public static int Main(string[] args) => Execute<Build>(args);

    [Parameter("Build configuration")]
    Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Secret("Container registry password")]
    Secret RegistryPassword;

    [Parameter("Target environment")]
    string Environment = "development";

    Target Restore => _ => _
        .Phase(Phase.Restore)
        .Consumes(Resource.BuildCache.Dotnet, ConsumeMode.Exclusive)
        .RequiresNetwork()
        .Executes(() => DotNet.Restore());

    Target Compile => _ => _
        .Phase(Phase.Build)
        .DependsOn(Restore)
        .Consumes(Resource.BuildCache.Dotnet, ConsumeMode.Exclusive)
        .MemoryBudget(2_048)
        .Executes(() => DotNet.Build(s => s
            .SetConfiguration(Configuration)
            .SetNoRestore(true)));

    Target Test => _ => _
        .Phase(Phase.Test)
        .DependsOn(Compile)
        .Consumes(Resource.BuildCache.Dotnet, ConsumeMode.Shared)
        .MaxParallelism(4)
        .Executes(() => DotNet.Test(s => s
            .SetConfiguration(Configuration)
            .SetNoBuild(true)));

    Target Pack => _ => _
        .Phase(Phase.Pack)
        .DependsOn(Test)
        .Idempotent()
        .Produces("artifacts/*.nupkg")
        .Executes(() => DotNet.Pack(s => s
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
            Docker.Login(s => s
                .SetServer("registry.example.com")
                .SetUsername("ci")
                .SetPassword(RegistryPassword));    // ← typed Secret, never logged

            Docker.Push(s => s
                .SetImage($"registry.example.com/myapp:{Environment}"));
        });

    Target Ci => _ => _
        .DependsOn(Pack)
        .Description("Default CI target: restore, build, test, pack");
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

### v0 — Walking skeleton

- `Tamp.Core` with target executor, parameter injection, dry-run, secret type, host detection (mid tier)
- `Tamp.Cli` global tool with `tamp <target>`, `--dry-run`, `--plan`, `--list`
- `Tamp.NetCli.V8`, `Tamp.NetCli.V9`, `Tamp.NetCli.V10` covering the dotnet CLI subset needed for one real pipeline, with the V10 module as the canonical reference and V8/V9 maintained for locked-down and STS-adopter consumers
- One real pipeline (HoldFast's .NET SDK package) ported as the dogfooding target

### v1 — Real-world coverage

- `Tamp.Docker.V27`, `Tamp.SonarQube.V10`, `Tamp.Yarn`, `Tamp.Turbo.V2` to cover the rest of HoldFast's needs
- Schema-driven wrapper generation with AI-assisted bootstrapping from `--help`
- IDE integration via `tasks.json` / `launch.json` generation (`tamp :ide-config`)
- MCP server mode (`tamp :mcp-server`) exposing targets as callable tools

### v2 — Adoption

- Public NuGet release of core + first-party modules
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
