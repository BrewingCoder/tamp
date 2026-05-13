# TAMP001 — CommandPlan value is unobserved

> Canonical URL: <https://github.com/tamp-build/tamp/blob/main/docs/analyzers/TAMP001.md>

A method call that returns `CommandPlan` (or `IEnumerable<CommandPlan>`) appears as a value-discarded statement inside an `Executes(Action)` lambda. The plan is never executed; the target appears to complete in milliseconds with no work done.

## The trap

You write a Target that orchestrates several `DotNet.*` / `Vite.*` / `AzureCli.*` calls:

```csharp
Target Compile => _ => _
    .DependsOn(nameof(Restore))
    .Executes(() =>
    {
        DotNet.Build(s => s.SetProject(ApiProject).SetConfiguration(Release));
        DotNet.Build(s => s.SetProject(FunctionsProject).SetConfiguration(Release));
    });
```

Run it:

```
==> Compile

─── Build Summary ───
  Target    Status   Duration
  Compile   ✓ Done     5 ms
```

5 ms. The dotnet build process never started. The build summary reports "Done." Nothing failed.

This is the symptom: targets that appear to complete instantly with no console output. Suspicious "Done" times in the millisecond range for steps that should take seconds-to-minutes.

## Why it happens

`ITargetDefinition.Executes` has three overloads:

| Overload | What it does |
|---|---|
| `Executes(Action)` | Runs the lambda. **Discards any returned value.** |
| `Executes(Func<CommandPlan>)` | Runs the returned plan. |
| `Executes(Func<IEnumerable<CommandPlan>>)` | Runs each returned plan in order. |

When you write a block-bodied lambda `() => { DotNet.Build(...); DotNet.Build(...); }`, the C# compiler infers the lambda's return type as `void` and binds to the `Action` overload. The two `DotNet.Build` calls return `CommandPlan` values that get assigned to nothing — the values are valid, but the framework's plan-execution path never sees them.

`DotNet.Build` and its siblings (`DotNet.Test`, `Vite.BuildProject`, `Vite.Vitest.Run`, `AzureCli.*`, `Bicep.*`, etc.) all return `CommandPlan` ; the construct-then-discard shape is the universal trap.

## The fix

Return the plans from the lambda so one of the `Func<...>` overloads binds:

### Pattern A — single plan (expression-bodied lambda)

```csharp
Target Restore => _ => _
    .Executes(() => DotNet.Restore(s => s.SetProject(Solution.Path)));
```

`Restore` returns a `CommandPlan`; the lambda's inferred return type is `CommandPlan`; binds to `Executes(Func<CommandPlan>)`; runs.

### Pattern B — multiple plans in fixed order (array)

```csharp
Target Compile => _ => _
    .DependsOn(nameof(Restore))
    .Executes(() => new[]
    {
        DotNet.Build(s => s.SetProject(ApiProject).SetConfiguration(Release)),
        DotNet.Build(s => s.SetProject(FunctionsProject).SetConfiguration(Release)),
    });
```

The lambda returns `CommandPlan[]` which is `IEnumerable<CommandPlan>`; binds to `Executes(Func<IEnumerable<CommandPlan>>)`; framework runs each plan in declared order.

### Pattern C — plans with side-effect setup (block-bodied with `return`)

```csharp
Target Test => _ => _
    .DependsOn(nameof(Compile))
    .Executes(() =>
    {
        (Artifacts / "test-results").EnsureDirectoryExists();
        return DotNet.Test(s => s
            .SetProject(TestsProject)
            .AddLogger("trx;LogFileName=tests.trx")
            .SetResultsDirectory(Artifacts / "test-results"));
    });
```

The block creates directories / writes config files / does any side effects, then `return`s the plan. Same binding as Pattern A.

## Suppression — when you really do want `Action`

A target that has work but produces no `CommandPlan` (pure C# operations, file copies, computing values, throwing an exception on a precondition) legitimately wants the `Action` overload:

```csharp
Target Clean => _ => _
    .Executes(() =>
    {
        Artifacts.DeleteDirectory();
        (Artifacts / "fresh-marker.txt").WriteAllText(DateTime.UtcNow.ToString("o"));
    });
```

Neither call returns `CommandPlan`. No analyzer warning fires; the `Action` overload is the right choice. Don't suppress what doesn't warn.

If you have a target that mixes `CommandPlan`-returning calls (which you want discarded ; rare but legitimate, e.g. fire-and-forget tracing) with `Action` body:

```csharp
// Project-level: <NoWarn>$(NoWarn);TAMP001</NoWarn> in Build.csproj
// File-level:
#pragma warning disable TAMP001
Target Trace => _ => _.Executes(() =>
{
    AzureCli.Account.Show(Az, s => s.SetOutput("json"));  // value intentionally unused
});
#pragma warning restore TAMP001
```

Be specific. Project-wide suppression invites a re-introduction of the original trap.

## Why this matters

The failure mode is silent. Targets report success. CI runs to green. No log output flags the issue. Adopters typically discover it because something downstream of the silently-skipped target breaks — a deploy target tries to use artifacts that were never published, or a test summary report is missing because no tests ran. The diagnostic distance from the symptom to "your `Executes` overload bound wrong" is high.

The analyzer catches it at compile time so this distance is zero.

## See also

- `ITargetDefinition.Executes` ; XML doc comments cover the three overloads.
- `TAMP002` (forthcoming) ; "Tamp build project has no valid entry point" ; sibling structural analyzer.
