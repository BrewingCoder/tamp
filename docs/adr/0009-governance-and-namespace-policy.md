# ADR 0009: Governance and namespace policy

* Status: Accepted
* Date: 2026-05-09
* Deciders: scott
* Tracking: TAM-15

## Context and Problem Statement

Tamp's headline architectural promise is forkability: small core, plugin-driven, *"anyone can publish `Tamp.{ToolFamily}.V{n}` packages without coordinating with core maintainers."* That sentence is in the README, but on its own it does not actually describe how the project is run, how the NuGet namespace behaves, what "official" means, or what happens when the maintainer team grows beyond one person.

This ADR fills that gap. It answers four concrete questions:

1. **NuGet namespace.** Who can publish `Tamp.*`? What does "official" mean to a consumer reading a package listing?
2. **Maintainer model.** Who decides things today? Who decides things tomorrow when there's more than one maintainer?
3. **ADR process.** How does a decision become accepted, and how does it get superseded?
4. **Community modules.** Where do they live, how are they discovered, and what stops the namespace from being colonised by abandonware?

Adjacent concerns explicitly out of scope: Code of Conduct, CLA/DCO, security disclosure policy, and any specific maintainer voting threshold beyond the one stated below. Each gets its own document or successor ADR.

## Decision Drivers

* **Forkability without anarchy.** The "anyone can publish" promise must be real, but consumers must be able to distinguish first-party from third-party at a glance.
* **Maintainer overhead must stay weekend-scale.** The architecture is the resilience strategy precisely because no individual is expected to spend their evenings reviewing the world. Governance must not contradict that.
* **Provenance.** The single biggest failure mode for a permissive ecosystem is squatting and typosquatting. Consumers need a reliable signal of "this came from the maintainer team," not just "this name starts with `Tamp.`".
* **Future-proofing without ceremony.** The maintainer team is one person today. The governance model must accommodate growth to 2–5 without rewriting the rules; growth past 5 is a separate problem we'll solve when it materialises.
* **Honesty about authority.** A BDFL model with one person is fine if it's stated. A pretend-democracy with one person is worse than a stated BDFL.

## Considered Options

The decision is structurally three sub-decisions; each is recorded with the option chosen.

### Namespace

1. **Reserve `Tamp.*` on NuGet and gate publication through the maintainer team.** Maximum control, minimum forkability — contradicts the README.
2. **Don't reserve; rely on naming convention and trust alone.** Maximum openness, no provenance signal, high squatting risk.
3. **Reserve `Tamp.*` for the maintainer NuGet account; let community publish without the reserved-prefix badge.** ← chosen.

NuGet's reserved-prefix mechanic is permissive: reserving `Tamp.*` does *not* prevent third parties from publishing under that prefix. It marks the maintainer team's packages with a "Prefix Reserved" verified badge, giving consumers a one-glance provenance signal while leaving the namespace open. This is exactly the property we want.

### Maintainer model

1. **BDFL forever (one maintainer).** Honest about reality but a single point of failure.
2. **Formal committee with voting thresholds from day one.** Theatre; we are one person.
3. **BDFL today with a documented path to a small maintainer team.** ← chosen.

### Community-module promotion

1. **No promotion; community modules live wherever, undiscoverable from upstream.** Lowest overhead but loses ecosystem cohesion.
2. **Centralised `tamp-contrib` GitHub org hosting community modules.** Cake/NUKE precedent, but adds organisational overhead and shifts perceived ownership from the author to "the project."
3. **Curated public registry, PR-driven, modules stay in their authors' repos.** ← chosen.

## Decision Outcome

The policy below is binding for the maintainer team's own conduct and is the contract third parties can rely on. Each subsection is a numbered rule so successor ADRs can amend specific clauses cleanly.

### 1. NuGet namespace

1.1. The maintainer team will register a NuGet account associated with the project and reserve the `Tamp.*` prefix as soon as the first official package (`Tamp.Core`) ships.

1.2. Packages published from that account are **first-party** and carry the NuGet "Prefix Reserved" badge. The badge is the canonical provenance signal.

1.3. Anyone may publish a package whose name starts with `Tamp.` from any other NuGet account. Such packages are **community packages**. They will not carry the reserved-prefix badge, and that absence is the signal to the consumer.

1.4. The maintainer team will not request takedowns of community packages solely on naming grounds. Takedowns may be requested only for packages that contain malicious code, impersonate first-party packages with confusable names (e.g., a typosquat of `Tamp.Core`), or violate applicable law. The bar is high and the request goes through NuGet's standard reporting process, not by maintainer fiat.

1.5. The maintainer team will publicly claim and reserve the literal names of every shipped first-party package even before they are first published, to prevent grabs of `Tamp.Core`, `Tamp.Cli`, and the announced module names.

### 2. Maintainer model

2.1. Tamp is a Benevolent Dictator project today. As of this ADR, the sole maintainer is **scott (BrewingCoder)**, recorded in [`MAINTAINERS.md`](../../MAINTAINERS.md). That file is the source of truth; it is updated, not this ADR, when the team changes.

2.2. New maintainers are added by invitation from an existing maintainer with concurrence from any other existing maintainer. Removal is by maintainer resignation, by maintainer-team consensus, or by 90 days of unresponsiveness on flagged escalations.

2.3. Decisions are by **lazy consensus** on PRs and ADRs: a proposal stands once 7 days have passed with no objections from a maintainer, *or* immediately upon explicit acceptance by a maintainer. Objections are blocking and must be resolved by discussion or by a follow-up proposal.

2.4. If the maintainer team disagrees and lazy consensus cannot resolve it, the BDFL (the person at position 1 in MAINTAINERS.md) breaks the tie. This rule expires automatically when the maintainer team reaches 4 active members, at which point a successor ADR specifying a voting model must be in place.

2.5. The BDFL position is held by the project originator (scott) until they explicitly designate a successor in MAINTAINERS.md.

### 3. ADR process

3.1. Anyone may propose an ADR by opening a PR adding a file to `/docs/adr/` with `Status: Proposed`. The ADR file's number is allocated at PR-open time from the next free number after consulting the YouTrack ADR list, and a corresponding tracking issue is created.

3.2. An ADR moves from `Proposed` to `Accepted` via standard maintainer lazy consensus (rule 2.3).

3.3. ADRs are append-only. The substance of an `Accepted` ADR is never edited after acceptance. Typo fixes, link repairs, and clarifications that do not change the decision are exempt — but the bar is "would a reasonable reader interpret this differently?" If yes, write a successor ADR.

3.4. To revise an accepted decision, write a new ADR that supersedes it. The new ADR sets the old one's status to `Superseded by ADR NNNN` (and the old one is updated only to add that pointer).

### 4. Community modules

4.1. Community modules live in their authors' own repositories. The project does **not** maintain a centralised hosting organisation for community modules.

4.2. The maintainer team publishes a curated registry at [`docs/community-modules.md`](../community-modules.md). Inclusion is via PR.

4.3. Inclusion criteria — all of the following:
   * Actively maintained (commits, releases, or issue activity in the last 12 months).
   * Follows the package naming convention from ADR 0002.
   * Licensed under MIT, Apache 2.0, or another OSI-approved permissive license compatible with consumption from MIT-licensed projects.
   * Repository is publicly accessible.
   * No malicious code, no telemetry beyond what the wrapped tool itself emits, no obvious typosquatting of first-party names.

4.4. Removal from the registry: opened as a discussion, with the listed maintainer of the module given 60 days to respond. Removal is decided by maintainer-team lazy consensus (rule 2.3) and recorded in the registry's history (git log).

4.5. The registry is informational. Inclusion does not imply endorsement of code quality, security review, or fitness for any purpose. Consumers are responsible for their own due diligence. The registry's value is discoverability, not vouching.

### 5. Branding and trademark

5.1. The project does not currently hold a registered trademark on the name "Tamp." Until and unless one is registered, the name is used informally and the project asserts no trademark rights via the license or otherwise. (This is consistent with ADR 0007: MIT does not grant trademark rights regardless.)

5.2. The maintainer team retains the moral expectation that the project name not be used to identify code that materially diverges from upstream Tamp without clear differentiation in the package and repository naming. This is a request, not a legal claim.

### 6. Outside this ADR

6.1. **Code of Conduct.** A separate document (Contributor Covenant or equivalent) will be added before the project accepts external contributions. Tracking issue to be created when that work begins.

6.2. **Contributor License Agreement / DCO.** No CLA. The project may adopt DCO sign-off (`git commit --signoff`) as a contribution-traceability mechanic if and when external contributions warrant it; that decision is not made here.

6.3. **Security disclosure policy.** A `SECURITY.md` will be added before the first public NuGet release. Until then, the maintainer email in `MAINTAINERS.md` is the contact path.

## Consequences

### Positive

- The forkability promise is now operational, not aspirational. Anyone can publish; the badge tells consumers what's official.
- Consumers can identify first-party packages at a glance — provenance is solved by NuGet's existing primitives, no novel mechanism required.
- Maintainer overhead is bounded: no hosting community modules, no formal review process for inclusion, no per-PR lawyering. The registry is a curated PR queue, nothing more.
- The maintainer-growth path (rules 2.1–2.5) is documented but doesn't require any structural rebuild — adding a second maintainer is a `MAINTAINERS.md` edit, full stop, with the BDFL tie-break rule covering the edge case of 2-vs-2 disagreement until the team is large enough to need formal voting.
- Squatting risk is concretely addressed by 1.5 (claim names early) and 1.4 (typosquats of first-party names *do* warrant takedown requests).

### Negative

- Reserved-prefix reservation can't happen until the first package ships. There's a window between repo-public and first-package-published where someone could grab `Tamp.Core` on NuGet. We accept this and note it as a pre-launch task: claim the literal package names before any public announcement.
- Lazy consensus with one maintainer is functionally just "the BDFL decides." We're documenting it that way deliberately rather than pretending otherwise.
- The community-module registry's "actively maintained in the last 12 months" criterion will require maintenance work over time. We accept that — the registry is itself a project, in miniature, and will be reviewed at most annually.

### Neutral / future-facing

- The 4-maintainer threshold in rule 2.4 is a placeholder that forces the question rather than answering it. When the team approaches that size, a successor ADR will specify a voting model. Doing so now is premature.
- Section 4 commits to a community registry but does not, today, commit to any review cadence. If the registry grows beyond ~20 entries, it becomes worth specifying a review process; until then, treat it as PR-driven only.
- Should the project ever register a "Tamp" trademark, section 5 will be superseded by an ADR that revisits brand-protection commitments. That is not currently a planned move.

## Pros and Cons of the Options

### Namespace: reserve-with-badge (chosen)

* Pro: keeps the namespace open while solving provenance.
* Pro: uses an existing NuGet primitive; nothing custom to operate.
* Con: requires the project to register a NuGet account and apply for the reservation — minor administrative work.

### Namespace: closed reservation

* Pro: maximum control over what's published as `Tamp.*`.
* Con: contradicts the project's foundational forkability commitment.
* Con: shifts every community module into a non-`Tamp.*` namespace, breaking the discoverability story.

### Namespace: no reservation

* Pro: zero administrative overhead.
* Con: no provenance signal at all. Consumers cannot tell first-party from third-party. Squatting and typosquatting are unmitigated.

### Maintainer model: BDFL with team path (chosen)

* Pro: honest about today; cheap to scale to a small team.
* Pro: lazy-consensus-with-tie-break is well-understood across OSS; no novelty risk.
* Con: the model has to be revisited when the team grows past 3–4. We accept this; the alternative is over-engineering on day one.

### Maintainer model: BDFL forever

* Pro: simplest.
* Con: discourages contributors who would want to know their potential growth path. The cost of writing the team-expansion clauses is approximately zero.

### Maintainer model: formal committee with voting

* Pro: clear rules.
* Con: pure theatre with one maintainer.

### Community-module promotion: curated registry (chosen)

* Pro: lowest-overhead model that still produces a discoverability surface.
* Pro: keeps ownership and accountability with the module's actual author.

### Community-module promotion: tamp-contrib org

* Pro: ecosystem precedent (Cake, NUKE).
* Con: shifts perceived ownership from the author to the project, which we don't want.
* Con: more administrative overhead than registered names + a markdown registry.

### Community-module promotion: no registry

* Pro: zero overhead.
* Con: ecosystem becomes silently fragmented; consumers can't find what exists.

## Links

* Tracker: TAM-15.
* Maintainer roster: [`MAINTAINERS.md`](../../MAINTAINERS.md).
* Community module registry: [`docs/community-modules.md`](../community-modules.md).
* Naming convention referenced in 1.3 and 4.3: ADR 0002.
* License referenced in 4.3 and 5.1: ADR 0007.
* ADR process referenced from this ADR onward: section 3 of this ADR.
