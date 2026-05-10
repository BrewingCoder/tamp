# Security Policy

## Reporting a Vulnerability

If you believe you have found a security issue in Tamp, please report it privately. Do **not** open a public GitHub issue.

Two channels, in order of preference:

1. **GitHub private security advisory.** Use the [Security tab → Report a vulnerability](https://github.com/tamp-build/tamp/security/advisories/new) on this repository.
2. **Email.** Send to `scott@gscottsingleton.com` with a subject prefix of `[tamp security]`.

Please include:
- A description of the issue and its impact.
- Steps or a minimal reproduction.
- The Tamp version (or commit SHA) you tested against.
- Whether you have already disclosed this elsewhere.

You can expect:
- An acknowledgment within 7 days.
- A triage assessment within 14 days.
- A fix or remediation plan within 60 days for confirmed issues, depending on severity.

We coordinate disclosure: once a fix is available, we will credit the reporter (with permission) in the release notes.

## Supported Versions

Tamp tracks Microsoft's official .NET support calendar exactly per [ADR 0015](docs/adr/0015-target-framework-strategy.md). A given Tamp release supports every TFM Microsoft considers in support at the time of release.

For shipped Tamp packages: only the latest minor of the most recent major track receives security fixes. We do not backport to older majors. Pre-1.0 releases are not under any backport guarantee.

## Out of Scope

The following are not considered security vulnerabilities for the purposes of this policy:

- A child process spawned by a tool wrapper exposes secrets in its command-line argument list to the OS process table while it runs. This is a fundamental OS limitation noted in the `Secret` type's documentation. Tool wrappers should prefer stdin or file-based secret passing where the wrapped tool supports it.
- A consumer mis-uses a `Parameter`-typed property to receive sensitive data. Use `[Secret]` for sensitive values.
- A consumer logs the result of `Secret.Reveal()` themselves. The redaction system covers writes through Tamp's logger and the `RedactingTextWriter`, not arbitrary code paths the build script chooses to take.

If your finding falls in one of these areas but you believe Tamp can do better than its current behavior, file a regular issue or PR — that's a feature improvement, not a vulnerability.

## Pre-Release Note

Tamp has not yet shipped its first NuGet release. This policy is in place ahead of public availability so the disclosure path is documented from day one. Once the first NuGet release ships, this section is removed.
