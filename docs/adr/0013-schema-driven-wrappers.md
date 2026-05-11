# ADR 0013: Schema-driven wrapper generation as the canonical workflow

* Status: Accepted (workflow); Generator implementation deferred
* Date: 2026-05-11
* Deciders: scott
* Tracking: TAM-19

## Context and Problem Statement

Tamp ships wrappers for ~25 external tools, each translating ergonomic typed settings into the wire-level CLI invocation that tool expects. Today every wrapper is hand-authored: `DotNetBuildSettings.SetProject(...)`, `DotNetBuildSettings.BuildVerbArguments()`, `DotNet.Build(Action<DotNetBuildSettings>...)`, tests covering shape and edge cases. Per wrapper, this is several hundred lines of mechanical code.

Hand-authoring works at the current scale (25 wrappers) but won't scale to 100 wrappers without becoming maintenance overhead — every new flag in `docker buildx build` or `dotnet test` requires a hand-edit across settings class, fluent surface, BuildVerbArguments emit, and tests.

The question this ADR answers: **what's the long-term canonical workflow for wrapper authoring, and what should we build to support it?**

## Decision Drivers

* **CLI surfaces drift.** Every wrapped tool ships breaking flag changes across majors and additive flags within majors. Manual maintenance accumulates a backlog of "we don't wrap the new flag yet" tickets.
* **Wrappers are mostly mechanical.** Most of a wrapper's code is "settings class with one property per flag + fluent setter + emit loop." A schema generator can produce 90% of this from a flag inventory.
* **Discovery of flags is automatable.** Many CLIs expose machine-readable flag inventories (`docker buildx build --help-formatted-as-json` style; OpenAPI specs for HTTP APIs; SDK reflection for .NET libraries). Where they don't, scraping `--help` is feasible.
* **The hand-authored fluent surface is the consumer's contract.** Whatever generator we build has to produce code that *looks the same* as the existing hand-authored wrappers. Generated code that consumers don't recognize as Tamp-shaped defeats the consistency story.

## Considered Options

1. **Stay hand-authored.** Pro: no generator to build/maintain. Con: scales poorly past 50 wrappers.
2. **Schema-driven generator that produces source files at build time (chosen as canonical workflow).** Schemas (JSON or YAML) describe each CLI verb + flags. A Roslyn-based generator (or a code-gen tool run from the build) produces the settings classes + fluent surface + emit logic + skeleton tests. Wrappers ship as generated source + hand-curated overrides.
3. **Reflection-driven dynamic wrappers (no generation).** Wrappers built at run time from a runtime schema. Pro: zero generated code on disk. Con: loses IDE IntelliSense and compile-time type safety — the whole point of typed wrappers.

## Decision

The schema-driven generator is the **canonical workflow** — the long-term shape of how new wrappers are authored. The generator itself is **deferred** until either (a) a critical mass of new wrappers warrants it, or (b) we burn enough hand-authored effort to justify the build cost.

### What the workflow looks like once the generator exists

1. A new wrapper starts with a `tamp-<tool>` repo containing only `schema.json` describing the verbs and flags.
2. The generator (an MSBuild task or `tamp-codegen` tool) produces `Generated/*.g.cs` files containing the settings classes, fluent surface, and `BuildVerbArguments` emit.
3. Hand-authored extension files in the same repo override specific behaviors (Secret-typed properties needing manual `BuildSecrets()`, computed flag combinations, custom validation, etc.).
4. Tests are split between generator-produced "every flag round-trips" tests and hand-authored behavior tests.
5. New flags land via PR to `schema.json`; the generator regenerates; the wrapper ships as a minor bump.

### What's in scope of the generator

* Settings classes with one property per flag.
* Fluent `SetX` / `AddX` / `EnableX` methods.
* `BuildVerbArguments` emit, ordered per the schema.
* Secret-typed property recognition (schema marks flags as sensitive).
* Skeleton tests asserting every flag round-trips correctly.

### What's NOT in scope

* Hand-curated wrapper logic (e.g., the TRX-overwrite mitigation in `DotNetTestSettings`, the secret-arg redaction in `Docker.Login`'s `--password-stdin`, the `EFCoreMigrationFanout` composition wrapper). Those stay hand-authored.
* Cross-wrapper composition wrappers (`Tamp.EFCore.V10` migration fan-out, `Tamp.SonarScanner.V10` CE-mode adjustments) — these are first-class hand-authored code by nature.

### Schema format

To-be-determined. The Microsoft `command-tool-config.json` shape used internally by some .NET tools is one candidate; OpenAPI 3.1 for HTTP-API wrappers; a Tamp-bespoke shape if neither fits. The schema language is **out of scope of this ADR**.

## Consequences

* **Positive (eventual)**: new wrappers ship as `schema.json` + a release pipeline. Onboarding contributors is "write a schema."
* **Positive (eventual)**: tool maintainers who ship machine-readable flag inventories get auto-generated Tamp wrappers as a side effect — the schema is just the inventory.
* **Positive (today)**: the existing wrappers are hand-authored in a shape that the generator can replicate. We're not currently going against the canonical workflow; we're just doing it manually for now.
* **Negative (eventual)**: building the generator is a substantial effort — Roslyn source generators, schema validation, golden-file tests, debugger support. Multi-week effort minimum.
* **Negative (today)**: this ADR commits to a workflow without committing to a date. Risk: we never build it because the manual cadence is sustainable. Mitigation: this ADR establishes the intent so when we DO build the generator, the existing wrappers can migrate without surprise.

## Notes

The decision to *defer* the generator is pragmatic. With 25 wrappers and one maintainer, hand-authoring is sustainable. The threshold where the generator pays for itself is somewhere around 50-100 wrappers, or when a single tool's flag surface starts updating faster than hand-edits can keep up.

When we do build the generator, it'll likely live in a `tamp-codegen` repo with its own release cadence — like every other Tamp module. The schema files will live alongside the wrapper code in each satellite repo, owned by that wrapper's maintainer.
