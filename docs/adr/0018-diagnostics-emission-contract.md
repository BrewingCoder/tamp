# ADR 0018: Diagnostics emission contract

**Status:** Accepted (2026-05-12, shipped in Tamp.Core 1.4.0).

**Context.** Tamp builds want first-class observability — traces of which target ran for how long, what allocated, what spawned a child process, whether the CI runner had headroom, which project (and which area of which project) the build belongs to. Burying that in a Tamp.Otel side-package would mean the framework can't observe itself; baking a third-party telemetry SDK into Tamp.Core would saddle every adopter with dependencies they may not want.

**Polyrepo correlation.** Single products often span multiple repositories — HoldFast across frontend / backend / infra; Strata across api / functions / spa. Telemetry from every repo needs to roll up under one logical name (`service.name="HoldFast"` in OTel terms) with the component recoverable as a secondary facet (`service.namespace="frontend"`). Tamp.Core supports this via the `[BuildProject(Name=…, Area=…)]` attribute on the build class. **Language-agnostic by design**: a pure-React, Python, Rust, or mixed-stack build uses this the same way as a .NET build — the attribute identifies the LOGICAL project, not anything stack-specific. The `Tamp.Otel` satellite maps the resulting tags to OpenTelemetry resource attributes at subscription time.

**Tamp does not require a .NET solution file.** The build script itself is .NET (that's the framework's runtime), but what it builds can be anything — Yarn workspaces, Python packages, Rust crates, Helm charts, raw shell commands. The fallback resolver below treats `[Solution]` as a .NET-only sweetener that's silently skipped when absent; repo-directory naming covers every other case.

ASP.NET Core resolved the same tension with `System.Diagnostics.ActivitySource`: the framework emits spans against named sources using only the .NET BCL; consumers wire up OpenTelemetry (or Application Insights, Datadog, Honeycomb, etc.) by subscribing to those source names. Zero coupling, zero new dependencies, fully replaceable.

Tamp follows the same pattern.

## Decision

Tamp.Core emits `System.Diagnostics.ActivitySource` spans and `System.Diagnostics.Metrics.Meter` counters/histograms from `Tamp.Core` directly. **No third-party telemetry library is referenced from Tamp.Core.** The Tamp.Otel satellite (separate repo, separate cadence) is a thin convenience that calls `AddSource("Tamp.Build.*")` for OpenTelemetry consumers; other backends subscribe directly.

### Sources

Three namespaced `ActivitySource` instances, all version-tagged to Tamp.Core's assembly version. Consumers can `AddSource` any subset.

| Source | Spans | Kind |
|---|---|---|
| `Tamp.Build` | one root span per `TampBuild.Execute<T>` invocation; operation name `"build"` | `Internal` |
| `Tamp.Build.Targets` | one span per executed target in the plan; operation name `"target:<Name>"` | `Internal` |
| `Tamp.Build.Commands` | one span per `CommandPlan` dispatched through `ProcessRunner.Execute`; operation name `"command:<executable>"` | `Client` |

Wildcard subscription (`"Tamp.Build*"`) picks all three; exact-name subscription is supported.

### Meter

Single `Meter` named `"Tamp.Build"`, same version.

**Counters:**
- `tamp.builds.total` (unit `{builds}`) — tag: `outcome`
- `tamp.targets.executed` (unit `{targets}`) — tags: `tamp.target.name`, `outcome`
- `tamp.commands.executed` (unit `{commands}`) — tags: `tamp.cmd.executable`, `outcome`

**Histograms:**
- `tamp.builds.duration` (`ms`) — tag: `outcome`
- `tamp.builds.peak_memory` (`By`) — tag: `outcome`
- `tamp.targets.duration` (`ms`) — tags: `tamp.target.name`, `outcome`
- `tamp.targets.memory.allocated` (`By`) — tags: `tamp.target.name`, `outcome`
- `tamp.commands.duration` (`ms`) — tags: `tamp.cmd.executable`, `outcome`
- `tamp.commands.memory.peak` (`By`) — tags: `tamp.cmd.executable`, `outcome`

### Tag keys — stability contract

Every tag key below is **permanent**. Renaming or removing one is a breaking change to the diagnostics contract and requires an ADR amendment.

#### Build span (`Tamp.Build`)
| Key | Type | Source |
|---|---|---|
| `tamp.build.targets` | string (comma-list of executed target names) | plan |
| `tamp.build.invocation` | string | reserved (CLI may populate) |
| `tamp.build.solution` | string (resolved solution path) | `[Solution]` |
| `tamp.build.exit_code` | int | executor outcome |
| `tamp.build.cli_version` | string | assembly version of `TampBuild` |
| `tamp.build.duration_ns` | long | high-res, complements `Activity.Duration` |
| `tamp.build.peak_working_set_bytes` | long | `Process.PeakWorkingSet64` |
| `tamp.build.targets.total` | int | record count |
| `tamp.build.targets.succeeded` | int | record count |
| `tamp.build.targets.failed` | int | record count |
| `tamp.build.targets.skipped` | int | record count |
| `tamp.build.targets.not_run` | int | record count |
| `tamp.build.commands.total` | int | commands dispatched |
| `tamp.build.failure.target` | string | first failing target |
| `tamp.build.failure.exit_code` | int | failure exit code |
| `tamp.build.failure_handlers_invoked` | string (comma-list) | OnFailureOf handler names |
| `tamp.build.project.name` | string | `[BuildProject(Name=…)]` (language-agnostic, recommended) → solution filename if .NET → repo-dir name → `"unknown"` |
| `tamp.build.project.area` | string (optional) | `[BuildProject(Area=…)]` |
| `tamp.build.project.name_source` | string | one of `attribute / solution / repodirectory / default` (diagnosability — which fallback resolved the name; `solution` only fires for .NET builds that declared `[Solution]`) |
| `tamp.host.os` | string | `RuntimeInformation.OSDescription` |
| `tamp.host.os.version` | string | `Environment.OSVersion.VersionString` |
| `tamp.host.arch` | string | `RuntimeInformation.OSArchitecture` |
| `tamp.host.cpu_count` | int | `Environment.ProcessorCount` |
| `tamp.host.total_memory_bytes` | long | `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` |
| `tamp.dotnet.runtime_description` | string | `RuntimeInformation.FrameworkDescription` |
| `tamp.ci.is_ci` | bool | non-`local` vendor detected |
| `tamp.ci.vendor` | string | one of `github-actions / azure-devops / teamcity / gitlab-ci / circleci / buildkite / generic / local` |
| `outcome` | string | one of `success / failure` |

The build span also emits a single point-in-time event `tamp.build.summary` carrying outcome counts as event-tags — useful for log parsers that don't process span-attribute payloads.

#### Target span (`Tamp.Build.Targets`)
| Key | Type | Source |
|---|---|---|
| `tamp.target.name` | string | spec.Name |
| `tamp.target.phase` | string | spec.Phase |
| `tamp.target.status` | string | one of `success / failure / skipped / not_run` |
| `tamp.target.skip_reason` | string | OnlyWhen / Requires failure reason |
| `tamp.target.depends_on` | string (comma-list) | direct deps only, not the closure |
| `tamp.target.assured_after_failure` | bool | when true |
| `tamp.target.failure_mode` | string | Fatal / Continue / Retry |
| `tamp.target.attempt` | int | 1-based; reserved for retry-mode |
| `tamp.target.attempts_total` | int | final count at terminal state |
| `tamp.target.duration_ns` | long | high-res |
| `tamp.target.start_working_set_bytes` | long | snapshot at target entry |
| `tamp.target.end_working_set_bytes` | long | snapshot at target exit |
| `tamp.target.gc_allocated_bytes` | long | `GC.GetTotalAllocatedBytes` delta |
| `tamp.target.gc.gen0.collections` | int | delta over the run |
| `tamp.target.gc.gen1.collections` | int | delta over the run |
| `tamp.target.gc.gen2.collections` | int | delta over the run |
| `tamp.target.cpu_time_ms` | double | `Process.TotalProcessorTime` delta |
| `tamp.target.actions.count` | int | non-plan actions invoked |
| `tamp.target.commands.count` | int | CommandPlans dispatched in this target |

#### Command span (`Tamp.Build.Commands`)
| Key | Type | Source |
|---|---|---|
| `tamp.cmd.executable` | string | plan.Executable |
| `tamp.cmd.args.count` | int | plan.Arguments.Count (never the args themselves — secret-leak surface) |
| `tamp.cmd.working_directory` | string | plan.WorkingDirectory |
| `tamp.cmd.had_stdin` | bool | whether the plan fed stdin |
| `tamp.cmd.had_secrets` | bool | whether the plan declared Secrets |
| `tamp.cmd.source_target` | string | parent target name (correlation aid) |
| `tamp.cmd.exit_code` | int | child exit code |
| `tamp.cmd.duration_ns` | long | high-res |
| `tamp.cmd.child.peak_working_set_bytes` | long | `Process.PeakWorkingSet64` at exit |
| `tamp.cmd.child.private_memory_bytes` | long | `Process.PrivateMemorySize64` at exit |
| `tamp.cmd.child.virtual_memory_bytes` | long | `Process.VirtualMemorySize64` at exit |
| `tamp.cmd.child.cpu_time.user_ms` | double | `Process.UserProcessorTime` |
| `tamp.cmd.child.cpu_time.system_ms` | double | `Process.PrivilegedProcessorTime` |
| `tamp.cmd.child.cpu_time.total_ms` | double | `Process.TotalProcessorTime` |
| `tamp.cmd.child.thread_count` | int | `Process.Threads.Count` |
| `tamp.cmd.child.handle_count` | int | `Process.HandleCount` (Windows-only; 0 elsewhere) |
| `tamp.cmd.stdout_bytes` | long | byte count of redirected stdout |
| `tamp.cmd.stderr_bytes` | long | byte count of redirected stderr |
| `outcome` | string | `success / failure` |

### Deliberately NOT emitted

- `plan.Arguments` content — even with the redaction table running upstream, args can carry user data (paths, repo names, ticket IDs). Counts only.
- `plan.Environment` values — env vars are a known secret-leak vector. Never as tag values.
- `plan.StandardInput` content — passwords flow through here (e.g. `docker login --password-stdin`).
- Git state (branch, commit, dirty flag) — useful but belongs in a downstream enricher (planned: `Tamp.Otel.Git`), not the contract.
- Full target dep closure — direct deps are sufficient; closure is reconstructible from the parent-child span relationships.

### Outcome vocabulary

Pinned values for the `outcome` tag, used identically across spans and counters:

- `success` — exit 0 / no exception
- `failure` — non-zero exit or exception
- `skipped` — OnlyWhen condition rejected the target
- `not_run` — earlier target failed, this one wasn't AssuredAfterFailure

### Performance / zero-overhead

`ActivitySource.StartActivity` returns `null` when no listener is registered for the source. All tag-setters are guarded against `Activity?`, so a build with no telemetry subscribed pays only the cost of the null check on each potential emission point. Measured overhead on the trivial-build benchmark: indistinguishable from baseline.

## Consequences

**Positive.**
- Adopters opting into telemetry get a rich, OTel-idiomatic surface without Tamp.Core ever depending on `OpenTelemetry.*`.
- Multiple consumer libraries (Tamp.Otel, in-house dashboards, log aggregators) coexist without conflict — they're all just ActivityListeners subscribing to the same sources.
- The contract is enforceable by a unit-test suite (see `tests/Tamp.Core.Tests/Diagnostics/TampDiagnosticsEmissionTests.cs`) that pins source names + tag keys, so accidental renames fail CI.

**Negative.**
- Adding a NEW tag is non-breaking and welcome; renaming or removing one IS breaking. We have to be disciplined about it.
- Tag values are bounded by `Activity`'s tag system — for very-high-cardinality data (e.g. one tag per file in a 50k-file solution) consumers should aggregate at the listener side, not push more tag values.

**Neutral.**
- The Tamp.Otel satellite is now unblocked but not yet authored. When it ships it'll be a sub-100-line package that adds `AddTamp()` extension methods to `TracerProviderBuilder` and `MeterProviderBuilder`.

## Related

- ADR 0009 — Secret type. Reinforces why arg / env / stdin content never reaches tags.
- ADR 0015 — Target framework strategy. `System.Diagnostics.ActivitySource` is netstandard-uniform; the contract holds identically across net8/net9/net10.
