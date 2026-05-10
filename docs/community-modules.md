# Community Modules Registry

A curated list of community-maintained Tamp modules. Inclusion governed by [ADR 0009](adr/0009-governance-and-namespace-policy.md) §4.

> **Inclusion is informational, not endorsement.** The maintainer team does not review listed modules for code quality, security, or fitness for any purpose. Consumers are responsible for their own due diligence. The registry exists to make community modules discoverable, not to vouch for them.

## Inclusion criteria

A module qualifies for the registry if **all** of the following are true:

- Actively maintained — commits, releases, or substantive issue activity within the last 12 months.
- Follows the package naming convention from [ADR 0002](adr/0002-package-naming-convention.md): `Tamp.{ToolFamily}{.V{Major}}?`.
- Licensed under MIT, Apache 2.0, or another OSI-approved permissive license compatible with consumption from MIT-licensed projects.
- Repository is publicly accessible.
- No malicious code, no telemetry beyond what the wrapped tool itself emits, no obvious typosquatting of first-party names.

## Removal

A module may be removed by the procedure in ADR 0009 §4.4: a public discussion is opened, the listed maintainer is given 60 days to respond, and removal is decided by maintainer-team lazy consensus. Removal history is preserved in `git log`.

## Registry

| Package | Wraps | Repository | Maintainer | Last reviewed |
|---------|-------|------------|------------|---------------|
| *(none yet — be the first)* | | | | |

## Adding your module

Open a PR adding a row to the table above. Include:

- The exact NuGet package name.
- The tool the module wraps, with version pin if applicable.
- The repository URL.
- Your maintainer contact (GitHub handle is fine).
- The date you submitted the PR (in `Last reviewed`).

The maintainer team will verify the inclusion criteria and merge.
