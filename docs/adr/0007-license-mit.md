# ADR 0007: License — MIT

* Status: Accepted
* Date: 2026-05-09
* Deciders: scott
* Tracking: TAM-13

## Context and Problem Statement

Tamp's README initially marked the license as "permissive (MIT or Apache 2.0, TBD before v0)." The choice has to be settled before any code lands, because the `LICENSE` file ships with the first push and changing license retroactively is awkward at best — every contributor's grant is implicitly tied to the license in effect when they contributed.

The choice is between the two permissive licenses any project in this neighbourhood would seriously consider. Copyleft (GPL family, MPL) is off the table: it conflicts with the "anyone can publish a `Tamp.<X>` module without coordinating with us" forkability commitment.

## Decision Drivers

* **Ecosystem fit.** Tamp lives in the .NET build-tool neighbourhood. License choice should not make it the odd one out for no payoff.
* **Patent surface.** Whether the project's actual subject matter is the kind of thing where patent grants matter.
* **Adoption friction.** Corporate legal review costs differ between MIT and Apache 2.0; for a project whose growth depends on community modules, friction matters.
* **Forkability commitment.** ADR 0009 (Governance) commits us to letting anyone publish in the `Tamp.<X>` namespace. The license must support that, not contradict it.

## Considered Options

1. **MIT.** ~170 words. Permissive. Implicit (interpreted) patent grant from contributors.
2. **Apache 2.0.** ~3000 words. Permissive. Explicit patent grant + retaliation clause; NOTICE-file redistribution requirement; explicit trademark non-grant.
3. **Dual license MIT + Apache 2.0** (Rust-style).
4. **BSD-3-Clause / BSD-2-Clause.** Effectively MIT-equivalent for our purposes; no advantage.

## Decision Outcome

**Chosen: Option 1 — MIT.**

Reasons in priority order:

### 1. Ecosystem alignment

The .NET build-tool space is uniformly MIT: NUKE, Cake, Bullseye, MSBuild, NuGet client, the .NET runtime itself. Tamp lives next to all of these in dependency graphs. Picking Apache 2.0 makes Tamp the odd one out and produces a cross-license attribution-bookkeeping headache for nobody's benefit. The "boring choice" is correct here.

### 2. No realistic patent surface

Apache 2.0's headline feature over MIT is the explicit contributor patent grant and the retaliation clause that auto-revokes a contributor's patent license if they sue downstream users. That matters for codecs, ML model architectures, distributed-systems algorithms, crypto primitives — domains where patents plausibly read on implementation. Wrapping CLIs and orchestrating processes is not patentable subject matter in any meaningful sense. We're not paying the complexity cost of Apache 2.0 for protection that isn't relevant.

### 3. Lower legal-review friction

Corporate legal departments approve MIT in seconds. Apache 2.0 is also widely approved but occasionally triggers longer review because the NOTICE-file redistribution mechanics need to be understood by anyone repackaging the code. For a project whose value proposition is community modules and forkability, every per-fork friction point compounds.

### 4. License doesn't fix what broke NUKE

NUKE is also MIT. The thing that broke NUKE's lifecycle wasn't the license — it was governance and architecture (monolith). Tamp fixes those (small core, independent module versioning per ADR 0002, no gatekeeping per ADR 0009). License choice is downstream of that fix, not the fix itself.

### Copyright attribution

Copyright is held by **Scott Singleton** as the legal author. The `BrewingCoder` GitHub handle is a brand identity, not a legal entity, so attribution uses the legal name. Future contributors are credited via standard MIT mechanics: the original copyright line is preserved; significant contributors may add their own copyright lines at the top of files they substantially authored, per common MIT practice.

## Consequences

### Positive

- One-file `LICENSE` drop. Done.
- Maximal compatibility with everything else in the .NET tooling ecosystem; no friction when Tamp is referenced from MIT- or Apache-licensed projects.
- Lowest plausible barrier for community contributors and corporate adopters.
- Compatible with the forkability story: anyone can fork, modify, sublicense, ship.

### Negative

- No explicit patent retaliation clause. If a contributor were to ship patented code and later sue downstream users, MIT provides less defensive structure than Apache 2.0. This is a theoretical risk for a build framework; in practice, the patent-aggressor population for this kind of project is empty.
- A contributor whose corporate legal team specifically requires Apache 2.0 may face friction; they'll need to confirm their org accepts MIT for outbound contributions. Empirically, this is rare — most orgs that accept Apache 2.0 also accept MIT.

### Neutral / future-facing

- This ADR commits Tamp's *outbound* license. It does not constrain what license community modules choose for themselves; modules in the `Tamp.<X>` namespace can be published under any compatible OSS license their authors prefer. (A separate community governance note may eventually recommend MIT/Apache for ecosystem consistency, but won't mandate it.)
- If the project's subject matter ever expands into something with real patent surface (e.g., a binary verification component, a build-cache hashing scheme novel enough to be patentable), a follow-up ADR can supersede this one and re-license outbound code. MIT → Apache 2.0 is straightforward at the right inflection point; the reverse is harder.

## Pros and Cons of the Options

### MIT (chosen)

- Pro: ecosystem-aligned with every comparable project.
- Pro: shortest possible legal-review path for adopters.
- Pro: simplest mental model — copyright preservation only, no NOTICE files, no special handling.
- Con: implicit (interpreted) patent grant rather than explicit; theoretically weaker if a contributor ever became hostile.

### Apache 2.0

- Pro: explicit patent grant from contributors; retaliation clause.
- Pro: explicit non-grant of trademarks (clarifies the brand-vs-license boundary).
- Pro: corporate legal teams that prefer it specifically often have the framework to handle NOTICE-file mechanics already.
- Con: makes Tamp the only Apache 2.0 project in a neighbourhood of MIT projects.
- Con: NOTICE-file mechanics require ongoing discipline from every distributor, not just the upstream maintainer.
- Con: longer legal review for some adopters.

### Dual MIT + Apache 2.0 (Rust-style)

- Pro: gives consumers the option to pick whichever fits their constraints.
- Con: every contribution must be made compatible with both, doubling the contributor-license-grant footprint.
- Con: explanatory complexity ("which license applies?") for a project whose entire value proposition is reducing complexity.
- Con: Rust adopted this for ecosystem-historical reasons specific to early Rust crate publishing. Those reasons don't apply in .NET.

### BSD-3-Clause / BSD-2-Clause

- Pro: equivalent permissiveness to MIT.
- Con: not the .NET ecosystem default. No upside over MIT; small downside in being "different for no reason."

## Links

- Tracker: TAM-13.
- License file: [`LICENSE`](../../LICENSE) at repo root.
- Governance and namespace policy: ADR 0009 (deferred — interacts with this ADR on the question of community-module licensing).
