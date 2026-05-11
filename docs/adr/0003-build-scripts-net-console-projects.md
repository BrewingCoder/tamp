# ADR 0003: Build scripts are .NET console projects

* Status: Accepted
* Date: 2026-05-11
* Deciders: scott
* Tracking: TAM-9

## Context and Problem Statement

Every build automation framework has to answer "what does a build script LOOK like as a thing on disk?" The choices range from declarative YAML files (Azure Pipelines, GitHub Actions, GitLab CI) through DSLs embedded in their host's compiler (Cake, FAKE, Make) to general-purpose code in the framework's host language (NUKE, Earthly, Mage). Each has tradeoffs in expressiveness, IDE integration, testability, and onboarding curve.

Tamp inherits NUKE's answer: **a build is a regular .NET console project.** This ADR records that decision and the reasoning that makes it the right choice for Tamp specifically.

## Decision Drivers

* **IDE integration is non-negotiable.** Build scripts are code. Code that doesn't get full IntelliSense, refactor support, and step-through debugging is a productivity tax. Frameworks with custom DSLs lose all three immediately; frameworks that embed in script files (Cake's `.cake`) get most of it back but at the cost of a separate language server. Treating build scripts as plain .NET projects means existing tooling — Roslyn, OmniSharp, Rider's analyzers, every C# developer's muscle memory — works without modification.
* **No bootstrap layer to maintain.** A DSL requires a parser, a runtime, an evaluator, error reporting, and documentation. A plain .NET project requires `dotnet run`. Every minute we don't spend maintaining a parser is a minute spent on actual build features.
* **Composability with the rest of .NET.** Build scripts can `PackageReference` anything on nuget.org, including the same libraries used by the application code under build. Sharing a serializer between the app and the build script (for emitting telemetry, transforming configuration, etc.) needs no special bridging.
* **Onboarding from C#.** The audience for Tamp is .NET developers. Asking them to learn one more thing — "this is the build language" — is friction. Letting them write the same C# they already know is no friction.
* **Testability.** Build scripts are .NET classes. Their target dependencies, parameter bindings, and command-plan construction are unit-testable with xUnit, just like any other code. DSLs typically require integration-style test harnesses.

## Considered Options

1. **Plain .NET console project (NUKE-shaped).** Build code is `Main(args)` → `Execute<Build>(args)`, targets are `Target`-typed properties on a class derived from `TampBuild`. The framework discovers targets via reflection.
2. **Embedded C# scripting (`.csx` / dotnet-script).** Build is a script file evaluated by Roslyn at run time. Lighter file footprint; loses project-level IDE integration and slower startup due to JIT recompilation.
3. **Custom DSL (Cake / FAKE shape).** Build is written in a domain-specific language. Maximum readability for non-developers; biggest implementation cost and weakest IDE story.
4. **Declarative YAML / JSON.** Build is data, evaluated by a fixed runtime. Easiest for CI vendors to render; insufficient expressiveness for anything beyond linear pipelines.

## Decision

Option 1 — **plain .NET console project**. A `build/Build.csproj` of type `Exe` targeting `net10.0`, referencing `Tamp.Core` and whichever tool wrappers the build needs. The build class derives from `TampBuild` and calls `Execute<T>(args)` from `Main`.

The exact shape:

```csharp
class Build : TampBuild
{
    public static int Main(string[] args) => Execute<Build>(args);

    [Solution] readonly Solution Solution = null!;
    [Parameter] Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    Target Compile => _ => _
        .Default()
        .DependsOn(Restore)
        .Executes(() => DotNet.Build(s => s.SetProject(Solution.Path).SetConfiguration(Configuration)));

    Target Restore => _ => _.Executes(() => DotNet.Restore());
}
```

The global tools `Tamp.Cli` (`tamp <target>`) and `dotnet-tamp` (`dotnet tamp <target>`) are thin launchers that locate the build project and `dotnet run` it. Nothing in Tamp's runtime requires the launcher; `dotnet run --project build -- <target>` is equivalent.

## Consequences

* **Positive**: every IDE, debugger, refactor tool, package manager, and test framework "just works" on build scripts. Build code shares conventions with application code; one set of tools serves both.
* **Positive**: build scripts can grow arbitrarily complex (computed targets, helper methods, parameter validation) without the framework needing to add features. Tamp's surface stays small.
* **Positive**: no separate language to document. Onboarding material is "here's how the C# fits together" — everything else is normal C#.
* **Negative**: marginal overhead vs declarative formats. A `build.yml` that simply lists steps in order is shorter than the equivalent Tamp build. We accept this — the floor of expressiveness is high.
* **Negative**: build scripts are real compile-and-run binaries. The first `dotnet tamp` after a clone needs to restore + build the build project. Mitigated by the `dotnet run` cache; cold first run is ~5 seconds.

## Notes

The fluent target DSL (`_ => _.X().Y().Z()`) and the `[Parameter]` / `[Secret]` / `[Solution]` injection attributes are covered in ADRs 0011 and elsewhere. This ADR establishes only the file-shape decision: **build scripts are .NET console projects.**
