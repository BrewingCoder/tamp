# ADR 0004: CommandPlan as universal wrapper output

* Status: Accepted
* Date: 2026-05-11
* Deciders: scott
* Tracking: TAM-10

## Context and Problem Statement

Tool wrappers — `DotNet.Build`, `Docker.Build`, `Yarn.Install`, `Sonar.Scan`, `EFCore.MigrationsBundle` — exist to translate ergonomic typed settings into the wire-level command invocation each CLI expects. The wrappers can produce one of several shapes:

1. They can return nothing and execute the command directly when called (Cake-style task functions).
2. They can return a `Task` and execute asynchronously (FAKE-style).
3. They can return a *plan* — a typed description of what would run — which the framework's runner separately dispatches.

Tamp picked option 3 as the universal contract. This ADR records why.

## Decision Drivers

* **Dry-run as a first-class capability.** A build framework that can't accurately preview what it would do is hard to trust. With wrappers that execute directly, dry-run requires every wrapper to have a "don't actually execute" mode — a parallel code path per wrapper. With wrappers that return a plan, the framework dispatches the plan in normal mode and prints it in dry-run mode. One plan, one dispatcher, one preview formatter. The dry-run output is *literally* what would run.
* **Redaction visibility.** Secrets — passwords, API keys, connection strings — flow into wrappers as typed `Secret` values. The plan carries the secrets as a list separate from the argument array; the dispatcher resolves them at process-spawn time and registers them with the runner's redaction table. The dry-run printer renders them as `<Secret:Name>` rather than the actual value. Wrappers that execute directly would have to thread redaction through every output channel themselves.
* **Composability of plans.** Targets that emit multiple plans (publish to N tenants, build M images, etc.) build a list of plans and the runner dispatches each. Wrappers that execute directly can't be sequenced or parallelized externally — the wrapper owns the dispatch.
* **Testability.** A plan is a value. Tests assert against plan shape (`Arguments.Contains("--no-restore")`) without spawning a process or stubbing a network layer. Wrappers that execute directly require integration-style tests; wrappers that return plans support pure unit tests.
* **Pluggable execution.** A plan can be dispatched by `ProcessRunner.Capture` (synchronous, captures stdout/stderr), `ProcessRunner.Execute` (synchronous, streams), or future async / remote / cached executors. The wrapper doesn't know which.

## Considered Options

1. **Direct execution (Cake-style).** Each wrapper invokes the CLI itself. Simple per-wrapper code; loses dry-run, redaction, and pluggable execution.
2. **Task-returning async wrappers (FAKE-style).** Each wrapper returns `Task` and the build calls `await`. Marginally better than direct execution but doesn't address the dry-run or pluggable-execution problems.
3. **CommandPlan as universal output (chosen).** Every wrapper returns a typed `CommandPlan { Executable, Arguments, Environment, WorkingDirectory, Secrets, StandardInput }`. The runner dispatches it.

## Decision

Every Tamp wrapper that produces a CLI invocation returns a `CommandPlan`. The settings type's `ToCommandPlan(Tool tool)` method (or equivalent) produces the plan. The build target either calls `ProcessRunner.Execute(plan)` directly or returns the plan from `Executes(() => DotNet.Build(...))` and lets the runner dispatch it.

The `CommandPlan` record:

```csharp
public sealed record CommandPlan
{
    public required string Executable { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> Environment { get; init; } = ImmutableDictionary<string, string>.Empty;
    public string? WorkingDirectory { get; init; }
    public IReadOnlyList<Secret> Secrets { get; init; } = Array.Empty<Secret>();
    public string? StandardInput { get; init; }
}
```

Library-mode wrappers (those that talk to HTTP APIs or SDKs rather than spawning processes — e.g. `Tamp.ServiceBus.V7`, `Tamp.AdoRest.V7`) sit outside this ADR; they expose async methods that return typed results directly, with `Secret.Reveal()` called inside the assembly's IVT grant. CommandPlan is the contract for *process-spawning* wrappers specifically.

## Consequences

* **Positive**: dry-run is genuinely faithful. `dotnet tamp Compile --dry-run` prints the exact command that would run, character for character.
* **Positive**: redaction is centralized in `RedactionTable` + `RedactingTextWriter`. Wrappers don't write their own redaction logic.
* **Positive**: extensions like the IDE plan-preview side panel (TAM-148) read CommandPlans directly to surface them in VS Code/Fleet UI.
* **Positive**: tenant fan-out patterns (`Tamp.EFCore.V10` migration fanout) compose by building a list of plans and dispatching them with a parallelism cap.
* **Negative**: wrappers can't easily express "do A, then read its output, then do B based on that output." Targets needing that pattern call `ProcessRunner.Capture(plan)` themselves and branch on the captured result.
* **Negative**: stdin-only secret passing (`docker login --password-stdin`) requires `CommandPlan.StandardInput` plumbing, which has to be Secret-aware. Done in 1.0; documented in `Tamp.Docker.V27`'s README.

## Notes

This decision is one of the load-bearing differentiators against NUKE. NUKE wrappers execute directly; Tamp wrappers describe. The cost is small (one extra layer of indirection) and the payoff (dry-run / redaction / composability / pluggable execution) is large.
