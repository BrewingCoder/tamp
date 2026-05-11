# ADR 0011: Target authoring via fluent property DSL (NUKE-shaped)

* Status: Accepted
* Date: 2026-05-11
* Deciders: scott
* Tracking: TAM-17

## Context and Problem Statement

Given that build scripts are .NET console projects (ADR 0003), Tamp needs a *shape* for how targets are declared inside that console project. The framework has to:

1. Reflect over the build class to find targets.
2. Build a dependency graph from declared edges.
3. Resolve parameters and conditional execution.
4. Dispatch the resulting plan.

The question this ADR answers: **what does a target declaration look like, syntactically?**

## Decision Drivers

* **NUKE muscle memory.** Tamp's primary audience is NUKE refugees. The fluent `_ => _.DependsOn(...).Executes(...)` shape is what they know. Adopting a familiar shape lowers migration cost dramatically.
* **C# idiomatic.** The shape should look like normal C# — properties, lambdas, method chains. Not magic. Not annotations everywhere. Not source-generated stubs.
* **Discoverability via IntelliSense.** Typing `_.` should show every decorator available. Method chaining lets the IDE walk the consumer through what's available.
* **Compositional.** Decorators compose freely. A target can be `.Default()` + `.DependsOn(X)` + `.OnlyWhen(p)` + `.RetryCount(3)` without the framework imposing precedence or ordering rules.
* **No source generators.** Roslyn source generators are powerful but DX-fragile (debugger doesn't always step in, IDE rebuilds can confuse them, not every consumer toolchain supports them well). Tamp avoids them when there's a no-generator alternative.

## Considered Options

1. **NUKE-shaped fluent property DSL (chosen).** `Target X => _ => _.Y().Z();` where the property body is a lambda that decorates an `ITargetDefinition`. Reflection discovers the property; the lambda is invoked once per build.
2. **Method-based with attributes.** `[Target] void X() { ... }` with target metadata declared via `[DependsOn(typeof(Y))]` etc. More like xUnit `[Fact]` tests. Requires source generators or runtime reflection over methods.
3. **Records or class-per-target.** `class CompileTarget : Target { protected override void Run() { ... } }`. Most OO-orthodox. Highest line count per target.
4. **Computational expression / DSL builder.** F#-style or LINQ-shaped. Powerful but alien to C#-only developers.

## Decision

Option 1 — **NUKE-shaped fluent property DSL**. Targets are `Target`-typed properties on a class derived from `TampBuild`:

```csharp
Target Compile => _ => _
    .Default()
    .DependsOn(Restore)
    .Executes(() => DotNet.Build(s => s.SetProject(Solution.Path)));
```

Where:
- `Target` is `delegate ITargetDefinition Target(ITargetDefinition definition)` — a delegate that decorates an empty definition.
- `_ => _.X().Y()` is a lambda that takes the empty `ITargetDefinition`, calls decorators on it, and returns it.
- Reflection in `TampBuild.CollectTargets` iterates the build class for `Target`-typed properties; for each one, it gets the property value (a delegate), invokes it with a fresh `TargetDefinition`, and freezes the result into a `TargetSpec`.

The `_ =>` lambda parameter name is a convention; any single-letter or descriptive name works (`def => def.X()`, `t => t.X()`). The `_ => _` shorthand is idiomatic for "ignore the parameter shape, just chain on it."

The decorator surface (`ITargetDefinition`) carries every target-level capability — dependencies, conditions, retry policy, resource consumption, parameter requirements, etc. ADR 0017 (forthcoming) covers the surface in detail.

## Consequences

* **Positive**: NUKE migrants port their build scripts with minimal syntax changes. The shape is the same; only namespaces and a few method names change.
* **Positive**: target declarations stay one expression — no per-target boilerplate methods, no class hierarchy, no source generators.
* **Positive**: IntelliSense walks consumers through the surface. Typing `_.` shows every decorator; the IDE filters by parameter type as the user types arguments.
* **Positive**: the lambda is invoked once per build at target-collection time. After that, the resulting `TargetSpec` is immutable — no runtime mutation surprises.
* **Negative**: requires reflecting over the build class, which means the build class can't be trimmed or AOT-compiled trivially. Build scripts are short-lived `dotnet run` processes; the trim/AOT tradeoff is accepted.
* **Negative**: a target body that wants to be split across helper methods can't easily share the `_` parameter. The convention is to write helper methods that return `Target` (the delegate type) and assign them: `Target X => MakeCompileTarget;`. Works fine.

## Notes

This decision pairs tightly with ADR 0003 (build scripts are .NET console projects) and ADR 0004 (CommandPlan as universal output). Together they establish the fundamental shape of a Tamp build:

1. C# console project.
2. Targets are fluent-property `Target` declarations on a class derived from `TampBuild`.
3. Target bodies produce or dispatch `CommandPlan`s.

The 1.1.0 concision pass (TAM-159) refined the surface — adding `.Default()`, `.Internal()`, and `[CallerArgumentExpression]` overloads on dependency methods — but the fundamental shape stayed.
