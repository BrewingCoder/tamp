# Releasing Tamp

This file is the operator checklist for cutting a release of `tamp` (the
main repo). Satellite repos follow the same shape — their own
`Directory.Build.props` carries the central `<Version>` for that family.

## The version-of-truth rule

**`Directory.Build.props` is the single source of truth for the version
of every packable project in this repo.** No `<Version>` element lives
in any csproj. The release workflow's `-p:Version=$PACKAGE_VERSION`
override (driven by the git tag) still wins for tag-driven CI publishes,
but local builds, sibling-clone consumers, and ad-hoc packs all read
from `Directory.Build.props`.

That's the lesson from TAM-81: when csproj `<Version>` is stale, the
in-tree assembly disagrees with the published assembly, and cross-repo
consumers blow up at load time with a `FileNotFoundException` for the
"wrong" version.

## Release checklist

1. **Bump `Directory.Build.props`** to the version you're about to ship:
   ```xml
   <PropertyGroup Label="Tamp central version ...">
     <Version>1.0.1</Version>
   </PropertyGroup>
   ```

2. **Commit the bump** as its own commit on `main`:
   ```bash
   git add Directory.Build.props
   git commit -m "release: bump central version to 1.0.1"
   git push origin main
   ```

3. **Tag the commit** with the matching `v`-prefixed tag:
   ```bash
   git tag -a v1.0.1 -m "Tamp 1.0.1"
   git push origin v1.0.1
   ```

4. **Watch the release workflow** at
   https://github.com/tamp-build/tamp/actions/workflows/release.yml
   The tag push triggers `pack + push to nuget.org via OIDC`.

5. **Verify on nuget.org** that the expected packages landed (typical
   propagation lag: 5-15 min for the search index; the flat container
   is usually within 1 min).

6. **Update `CHANGELOG.md`** if you didn't already — every release gets
   a section. Move entries out of `[Unreleased]` into the new version
   header.

## Why no automation (yet)

Two options were considered for TAM-81:

- **Automate the bump as a post-tag CI step.** Adds a bot-commit back
  to `main`, which means CI auth, merge-conflict handling, and
  workflow complexity. Worth it later.
- **GitVersion.MsBuild integration.** Tamp already wraps GitVersion's
  CLI (`Tamp.GitVersion.V6` in `tamp-build/tamp-gitversion`); wrapping
  the MSBuild integration too would let `<Version>` derive from git
  tags automatically. Even better, but a real package to design and
  ship.

For 1.0.x we chose discipline + a checklist. When release cadence
picks up enough that the manual bump becomes friction, escalate to
GitVersion.MsBuild and retire this checklist.

## Satellite repos

Each of the six satellite repos has its own `Directory.Build.props`
with its own central `<Version>`. The same checklist applies: bump the
satellite's `Directory.Build.props`, commit, tag with `v<satellite-version>`,
push. The satellites release independently of `tamp` core.
