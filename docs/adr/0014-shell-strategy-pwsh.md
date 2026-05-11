# ADR 0014: Shell strategy — `pwsh` when shell is required, never assume `bash`/`cmd`

* Status: Accepted
* Date: 2026-05-11
* Deciders: scott
* Tracking: TAM-20

## Context and Problem Statement

Build automation occasionally needs to run shell-flavored work — multi-command pipelines, glob expansions, here-docs, redirection, signal handling — that isn't cleanly expressible as a `CommandPlan` invoking a single executable. The question this ADR answers: which shell does Tamp invoke when shell-flavored execution is required?

Three concrete cases where the question matters:

1. **Bootstrap scripts.** `tamp init` (TAM-119 epic) generates a `build.sh` / `build.ps1` pair at the repo root. They locate dotnet, ensure it's installed (auto-installing the SDK from `dot.net/v1/dotnet-install.sh` if missing), and dispatch to the build project. Which shell handles which platform?
2. **Wrappers that need shell.** Most wrappers spawn a single executable directly (no shell). Some — like a hypothetical `Pipe` helper that chains commands or a `RawShell` wrapper for one-off needs — would invoke a shell.
3. **Documentation examples.** When the docs show "run this command," is it `bash`-shaped, `cmd`-shaped, or shell-neutral?

## Decision Drivers

* **Cross-platform parity.** Tamp ships on Windows, macOS, and Linux. A shell strategy that requires three branches per shell use is maintenance overhead and a frequent source of bugs.
* **Modern .NET ecosystem norms.** PowerShell 7+ (`pwsh`) is officially cross-platform, ships native installers for all three OSes, and is the standard for .NET-adjacent shell scripting. Microsoft Learn, Azure docs, GitHub Actions defaults — `pwsh` is the modern .NET shell of record.
* **`bash` is not universally present.** macOS shipped with `bash` 3.2 (2007) until 2019 when Catalina switched the default shell to `zsh`. Modern macOS doesn't have a current `bash` without manual install. Windows ships no `bash` at all without WSL.
* **`cmd.exe` is impoverished.** Lacks the shell features (variable expansion, pipelines, here-docs) that justify using a shell in the first place. Adequate for "run one program," superfluous for that case.
* **Predictability over preference.** Build scripts read identically on every machine when they invoke one shell. A "use whichever shell is available" policy invites OS-dependent script divergence.

## Considered Options

1. **Assume `bash` everywhere.** Standard Unix convention. Breaks on Windows without WSL; breaks on macOS without a recent `bash` install.
2. **Assume `cmd` on Windows, `bash` everywhere else.** Common pattern in legacy .NET tooling. Doubles the shell surface; requires every script to exist in two flavors.
3. **Assume `pwsh` everywhere shell is required (chosen).** One shell, all three OSes. Installable on every host the framework cares about. Native to the .NET ecosystem.
4. **Provide no shell facility — every wrapper invokes a single executable directly.** Most principled but precludes legitimate shell-shaped use cases (bootstrap scripts, multi-command compositions). Punts the problem to consumers.

## Decision

**Tamp's shell of record is `pwsh` (PowerShell 7+).** Specifically:

* Where Tamp itself generates shell-shaped scripts, they are `.ps1` PowerShell files. On POSIX, a thin `build.sh` shim invokes `pwsh build.ps1 "$@"` — three lines of bash that exist only to bootstrap into PowerShell.
* Where wrappers need to invoke a shell, they invoke `pwsh -NoProfile -File <script>` or `pwsh -NoProfile -Command <inline>` — never `bash`, never `cmd`, never "discover whatever shell is available."
* Where the framework explicitly requires the user to have a shell installed (e.g., `tamp init` generates scripts that need `pwsh` on POSIX), the `build.sh` shim prints a friendly install hint when `pwsh` isn't found: `brew install --cask powershell` (macOS) / `sudo snap install powershell --classic` (Linux) / point to the Microsoft Learn install guide (Windows).
* Documentation examples for "run this command" assume the user is in `pwsh`. If the example is shell-neutral (`dotnet tamp Ci`) it's shown without prompt prefix; if it's shell-flavored (`for f in *.csproj; do ... done` shape), the example shows the PowerShell equivalent.

`bash` and `cmd` are NOT assumed anywhere in framework code. Consumers who prefer their own shell are free to invoke `dotnet tamp` from any shell — Tamp doesn't care; it's spawning processes either way. But framework-generated artifacts (bootstrap scripts, wrapper-internal scripts) are PowerShell.

## Consequences

* **Positive**: one shell to test against. Tamp's shell-shaped code paths get one test matrix, not three. The maintenance overhead drops accordingly.
* **Positive**: bootstrap scripts ship as a single PowerShell file + a 3-line POSIX shim. No duplicated `dotnet-install` logic across `.sh` and `.ps1` (cf. NUKE's pattern, which has parallel implementations).
* **Positive**: aligns with where Microsoft has been steering the .NET ecosystem for the last decade. `pwsh` is increasingly assumed in CI templates, runbook examples, and Azure tooling.
* **Negative**: `pwsh` is an extra install on macOS and most Linux distros. Adopters who already have it installed (typical for .NET developers) see no cost. Adopters who don't get a friendly install prompt from the bootstrap shim. The dependency is explicit, not silent.
* **Negative**: `pwsh` startup is slower than `bash` cold-start (~300ms vs ~50ms). For bootstrap scripts that run a few times per developer per day, the cost is negligible. For wrappers that invoke a shell inside a target body (rare), the latency adds up only at extreme call counts.
* **Negative**: requires consumers used to bash idioms to translate to PowerShell when extending Tamp itself or when writing wrapper-side shell. Documentation provides translation patterns; PowerShell's broad adoption inside .NET shops makes the learning curve manageable.

## Notes

This ADR codifies a decision that was already in force — `Tamp.Cli/Program.cs` shells out via `dotnet run` rather than via a shell, and the `tamp init` design (TAM-123) generates `build.ps1` as the canonical bootstrap with `build.sh` as a thin shim.

When a Tamp-generated PowerShell script needs OS-specific behavior (e.g., the `dotnet-install` logic differs between platforms), the branch is INSIDE the PowerShell script — using `$IsWindows` / `$IsLinux` / `$IsMacOS` — not in separate per-OS files.

The decision deliberately doesn't address whether Tamp ships a `Shell` wrapper for arbitrary shell-shaped execution. If that's added later, it'll be `Tamp.Pwsh.V7` (or similar), a named satellite with its own ADR if needed.
