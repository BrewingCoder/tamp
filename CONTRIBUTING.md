# Contributing to Tamp

Thanks for considering a contribution. Tamp is small enough that one maintainer reviews everything, so PRs that arrive with the conventions below get merged faster.

## Ground rules

- Be kind. The [Code of Conduct](CODE_OF_CONDUCT.md) applies in every issue, PR, and discussion.
- The architecture, governance, and naming conventions are recorded in [`docs/adr/`](docs/adr/). Read the ADR for an area before proposing a change to it.
- Decisions evolve via successor ADRs. Don't argue with an Accepted ADR in a code-review thread; open a follow-up ADR proposal.

## Getting set up

Tamp targets [.NET 8, 9, and 10](docs/adr/0015-target-framework-strategy.md) — every assembly multi-targets all three. You need all three SDKs installed locally to run the full test matrix.

```bash
# macOS — Microsoft pkg installers via Homebrew
brew install --cask dotnet-sdk@8 dotnet-sdk@9 dotnet-sdk

# Linux — Microsoft package feed (see https://learn.microsoft.com/dotnet/core/install/linux)
# Windows — winget install Microsoft.DotNet.SDK.8 / .9 / .10
```

Verify:

```bash
dotnet --list-sdks
# 8.0.x, 9.0.x, 10.0.x
```

Then:

```bash
git clone git@github.com:tamp-build/tamp.git
cd tamp
dotnet restore Tamp.slnx
dotnet build Tamp.slnx
dotnet test Tamp.slnx
```

A clean build is zero warnings, zero errors, every test green across all three TFMs. CI enforces this on every PR.

## Repository layout

Per [ADR 0006](docs/adr/0006-repo-layout-monorepo.md):

```
src/                 production code, one project per shipping NuGet
tests/               one xUnit project per src project, parallel naming
docs/adr/            Architecture Decision Records (MADR format)
.github/workflows/   CI definitions
```

## Pull request flow

1. **Open an issue first** for anything more involved than a typo. The maintainer team uses YouTrack internally for work tracking — for outside contributors a GitHub issue is fine; the maintainer mirrors it as needed.
2. **Branch from `main`.** Topic branches; no long-lived feature branches.
3. **Keep PRs scoped to one project's surface plus the Core changes that justify it** ([ADR 0006 §Negative](docs/adr/0006-repo-layout-monorepo.md)). A PR that touches everything is harder to review than three focused ones.
4. **Tests are mandatory** for new behavior — boundary values, null/empty inputs, unicode, concurrency where applicable. The bar is "tests find bugs," not "tests cover lines."
5. **Run the full matrix locally** before pushing — `dotnet test Tamp.slnx`. CI will catch what you missed; saving CI cycles is polite.
6. **Commit messages** use a leading conventional-style prefix (`feat:`, `fix:`, `docs:`, `build:`, `ci:`, `chore:`, `refactor:`, `test:`). Body explains the *why*; the diff already shows the *what*.

## Proposing an ADR

Per [ADR 0009 §3](docs/adr/0009-governance-and-namespace-policy.md):

1. Pick the next number from the existing files in `docs/adr/`.
2. Open a PR adding `docs/adr/NNNN-kebab-case-title.md` with `Status: Proposed` in the front matter.
3. Update the index in `docs/adr/README.md`.
4. Discussion happens in the PR. Lazy consensus moves it to `Accepted`.
5. ADRs are append-only after acceptance. To revise, write a successor that supersedes the old one; don't edit the substance of an Accepted ADR in place.

## Style and conventions

- **Editor:** Anything that respects `.editorconfig` and the central `Directory.Build.props` settings (`Nullable=enable`, `TreatWarningsAsErrors=true`, `LangVersion=latest`).
- **Line length:** Soft limit ~100 chars. Don't reflow other people's code on unrelated edits.
- **Comments:** explain *why*, not *what*. The compiler reads the code; the next maintainer reads the comments.
- **Tests use xUnit + Bogus.** Theory tests for boundary cases. Avoid mocks where a real object will do.
- **Public API:** every public type or member needs an XML doc summary. Internal types are exempt unless they're load-bearing.
- **Don't add transitive dependencies casually.** Tamp's small-core promise hinges on a tight dependency graph. New `<PackageVersion>` entries in `Directory.Packages.props` need a justification in the PR body.

## What we're NOT looking for

(See also [`README.md` § Out of Scope](README.md).)

- Build-script DSLs (YAML, JSON, scripted C#). Tamp builds are .NET console projects, period.
- Distributed-build remoting. Bazel-style remote execution is a different project.
- Editor plugins. Generated `tasks.json` / `launch.json` is enough.
- Wrappers that depend on `Tamp.Core` internals. Module wrappers consume the public surface; if you need Core internals, that's an ADR.

## Reporting security issues

See [`SECURITY.md`](SECURITY.md). Don't open public issues for vulnerabilities.

## Recognition

Substantial contributors are added to `MAINTAINERS.md` per [ADR 0009 §2.2](docs/adr/0009-governance-and-namespace-policy.md). The bar is sustained engagement and trust, not contribution count.
