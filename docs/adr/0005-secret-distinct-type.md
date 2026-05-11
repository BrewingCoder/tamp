# ADR 0005: `Secret` as a distinct type, not a `[Sensitive]` attribute

* Status: Accepted
* Date: 2026-05-11
* Deciders: scott
* Tracking: TAM-11

## Context and Problem Statement

Build scripts handle sensitive values constantly — NuGet API keys, Docker registry passwords, Sonar tokens, Service Bus connection strings, signing keys, KMS GUIDs. The framework needs to make leaking those values into logs / dry-run output / stack traces *difficult*, ideally requiring deliberate action to expose.

Two design shapes are commonly seen:

1. **`[Sensitive]` attribute on a `string` field.** Properties keep their normal `string` type; the framework reads the attribute to know which values to redact.
2. **A distinct `Secret` type.** Properties are typed `Secret` (or `Secret?`); the type system enforces that the raw string can't be obtained without an explicit `.Reveal()` call.

Tamp picked option 2. This ADR records why and what the consequences are.

## Decision Drivers

* **Type system enforcement is stronger than convention.** A `[Sensitive] string Password` value can be passed to `Console.WriteLine`, `string.Format`, or any other API expecting a string — the compiler doesn't know it should be careful. A `Secret Password` value can only be passed where a `Secret` is accepted. Accidental coercion to `string` for logging requires explicit `.Reveal()`, which is grep-able and review-able.
* **Reveal is an internal-only API.** `Secret.Reveal()` is `internal`, visible only to assemblies in Tamp.Core's `InternalsVisibleTo` list — the runner's process-spawn path, the wrapper assemblies, and the test project. Build scripts and arbitrary library code cannot call it. Leaking a secret value from a build script body requires either calling a method on Tamp.Core (which goes through redaction) or shelling out to a process directly (which Tamp's `ProcessRunner` redacts on stdout/stderr).
* **Discoverability.** A reviewer scanning a build script can grep for `Secret` declarations to find every sensitive parameter. A `[Sensitive]` attribute requires looking at each `string` declaration and checking whether it's annotated — easy to miss.
* **Integration with the redaction table.** Every Secret carries a registered name (e.g. `"RegistryPassword"`). When a Secret is passed to a CommandPlan, it's registered with `RedactionTable.Register(Name, Value)`. The redacting writer then scrubs any line containing the value, replacing it with `<Secret:RegistryPassword>`. This works because the framework owns the type — it knows when a Secret gets handed off and to whom.

## Considered Options

1. **`[Sensitive]` attribute on `string` field.** Common in older frameworks. Pro: zero migration cost from existing string-typed parameters. Con: no type enforcement, easy to accidentally leak.
2. **`Secret` as a distinct sealed type with internal `Reveal()` (chosen).** Pro: type enforcement, grep-able, reveal is auditable. Con: parameter binding has to bridge from environment-variable strings to `Secret` instances at startup.
3. **`Secret<T>` generic for non-string secrets (certificates, keys, etc.).** Considered for future expansion. Not implemented in v1; current `Secret` wraps a `string`. The generic shape is reserved.

## Decision

The type is `sealed class Secret { public string Name { get; } public Secret(string name, string value) { ... } internal string Reveal() { ... } public override string ToString() => $"<Secret:{Name}>"; }`.

Build scripts declare secrets with `[Secret]`:

```csharp
[Secret("NuGet API key", EnvironmentVariable = "NUGET_API_KEY")]
readonly Secret NuGetApiKey = null!;
```

The framework binds the value at parameter-binding time (reads the environment variable, constructs a Secret, sets the field). Wrappers that accept Secret-typed arguments call `.Reveal()` internally (allowed via IVT) to thread the raw value through to the process spawn or HTTP body — but the value never appears in a managed string variable inside the wrapper's public API surface.

`Secret.ToString()` returns `<Secret:Name>` — never the value. This is the *first* defense against logging leaks: even `Console.WriteLine(secret)` doesn't expose the value.

`RedactionTable` is the *second* defense: when a Secret is registered (which happens automatically when it appears in a `CommandPlan.Secrets` list), every subsequent string written through `RedactingTextWriter` is scrubbed for the value.

## Consequences

* **Positive**: leaking a secret to a log requires either (a) deliberately calling `.Reveal()` from a place that has IVT access (rare; auditable) or (b) the secret being included as a substring in a process's stdout/stderr (caught by redaction). The "I forgot to add `[Sensitive]`" failure mode is impossible — secrets are typed.
* **Positive**: the type signature of every wrapper that accepts a secret is honest. `Docker.Login(s => s.SetPassword(Secret))` makes it clear what kind of value is expected. No "this string is sensitive, careful with it" comments needed.
* **Positive**: the redaction table is automatic. Wrappers that put a Secret on a CommandPlan get the value scrubbed from all subsequent process output without any per-wrapper work.
* **Negative**: parameter binding has more shapes than `string`. The `SecretBinder` runs before normal parameter binding and constructs Secret instances from env vars / interactive prompts / future credential providers. More plumbing than `[Sensitive] string`.
* **Negative**: existing code that uses `string` for sensitive values can't be migrated trivially. Every `string Password` becomes `Secret Password` and the call sites that build CommandPlans have to call `.Reveal()`. We accept this — Tamp is greenfield enough that the migration cost is bounded.

## Notes

The internal-only `Reveal()` works because Tamp.Core controls the IVT grant list. Every wrapper assembly that needs to call `.Reveal()` (Tamp.Docker.V27, Tamp.AdoRest.V7, etc.) is explicitly listed in Tamp.Core's `AssemblyInfo.cs`. Third-party wrappers contributed from outside the `tamp-build` org would either be added to the IVT list (with review) or use the public surface (`secret.ToString()`, which doesn't expose the value).
