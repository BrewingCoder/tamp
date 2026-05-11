# ADR 0016: Decision-maker silence within scope timeframe forfeits the right to weigh in

* Status: Accepted
* Date: 2026-05-11
* Deciders: scott
* Tracking: TAM-21 (forthcoming)

## Context and Problem Statement

ADR 0009 established lazy consensus as Tamp's default decision rule (rule 2.3): "a proposal stands once 7 days have passed with no objections from a maintainer." That's a specific timeout for PRs and ADRs. It doesn't generalize as a project-wide principle, and it doesn't explicitly address:

* What happens when a decision is heavier than a routine PR — when 7 days might be too short or too generous?
* What happens to a decision-maker who knows a decision is open and chooses not to engage?
* Whether silence equals consent, dissent, or forfeiture.
* Whether the rule applies to the BDFL themselves if they go dark.

This ADR fills that gap. Without an explicit principle, silence on an open decision is ambiguous — some readers infer "approval by default," some infer "blocked until they weigh in." Both interpretations are reasonable; neither is correct without the project saying so. This ADR makes the choice and the reasoning explicit.

## Decision Drivers

* **The project must move.** A solo-maintainer project with one BDFL can't afford to stall every decision indefinitely waiting for input that may never come. The resilience strategy (ADR 0001) explicitly assumes the maintainer team is small and time-constrained — process that requires constant engagement is process that fails on the first week the maintainer is sick.
* **Silence has to mean *something* explicit.** "Silence = approve" is uncomfortable when a decision affects someone who didn't know it was open. "Silence = block" stalls forever. The middle path — *silence within the scope-appropriate timeframe forfeits the right to weigh in on that decision* — is honest: you knew, you had time, you didn't engage, the project moves without you.
* **The timeframe must scale with weight.** A typo-fix PR doesn't need 30 days of public review. A breaking-change ADR doesn't deserve a 24-hour window. The framework needs to be explicit that timeframes are scoped to the decision's weight.
* **The rule must apply to everyone, including the BDFL.** A rule that exempts the project lead is theater. If Scott goes dark for a month, decisions still move. The maintainer team carries on. Scott's right to weigh in on what happened during his silence is forfeited.
* **Notification expectations.** The rule's legitimacy depends on participants being notified that a decision is open AND knowing the timeframe in advance. A surprise silence-forfeit is unfair; a published timeframe with normal notification channels is not.

## Considered Options

1. **No explicit rule.** Status quo. Pro: lowest ceremony. Con: ambiguous; encourages indefinite waiting.
2. **Silence = approval (lazy consensus everywhere).** ADR 0009 rule 2.3 already says this for PRs and ADRs at 7 days. Generalizing to all decisions risks "approval by exhaustion" — bad-faith actors could rush a decision past a slow reviewer.
3. **Silence = block (must hear from every decision-maker).** Maximum participation. Stalls the project on any single absent maintainer.
4. **Silence within scope-appropriate timeframe forfeits the right (chosen).** The decision opens with an explicit window proportional to its weight. After the window, silence is treated as forfeiture — neither approval nor objection — and the decision proceeds under the remaining maintainer consensus.

## Decision

**A decision-maker who does not weigh in on an open decision before its declared timeframe expires forfeits the right to weigh in on that decision after the fact.** The decision proceeds with the input it has.

### The timeframe is declared with the decision

Every decision that invites maintainer input MUST publish its review window when it opens. The window is set by the decision's proposer, scaled to weight, and visible to all maintainers from the start:

| Decision weight | Default window |
|---|---|
| Routine PR (bug fix, doc update, additive code change) | **7 days** (matching ADR 0009 rule 2.3) |
| Standard ADR (new architectural decision, non-breaking) | **7 days** |
| Heavy ADR (breaking change, governance amendment, security policy) | **14–21 days** |
| Major directional decision (new product surface, namespace policy, supersede an Accepted ADR) | **21–30 days** |
| Emergency / security (CVE response, malicious-package takedown) | **24–72 hours** |

These are defaults. The proposer may set a longer window if the decision is unusually consequential; the proposer may NOT set a shorter window than the default for the decision's category without unanimous maintainer-team agreement on the shortened timeline.

The window is recorded in the PR description, the ADR's frontmatter (when applicable), or the announcement message. Notification follows the project's normal channels — maintainer-team chat / GitHub @-mentions / the YouTrack issue assigned to the maintainer-team review.

### What forfeiture means in practice

After the window closes:

* The decision proceeds with the input it has — typically maintainer-team lazy consensus per ADR 0009 rule 2.3 (no objections from those who DID respond).
* A maintainer who didn't respond loses the right to retroactively object. They may, of course, propose a successor PR / ADR to revise the decision — the project is append-only, not punitive — but the *current* decision stands.
* Reopening the same decision because someone wishes they had spoken up is NOT a valid PR/ADR rationale.

### Self-application: the BDFL is not exempt

The rule applies to every decision-maker, including the BDFL position holder. If Scott goes dark for a maintainer-team window:

* Decisions that maintainer-team lazy consensus can resolve proceed without the BDFL.
* Decisions that ADR 0009 rule 2.4 normally reserves to the BDFL (tie-breaking when maintainer-team disagrees) fall through to whatever resolution the maintainer team can produce on their own. If the team genuinely cannot resolve, the decision waits or is deferred — silence doesn't make consensus appear; it just removes one veto.
* Scott does not have a right to retroactively veto decisions made during his silence.

When the BDFL knows they will be unreachable for a window longer than 14 days, they SHOULD post an out-of-office notice naming a delegate or noting that BDFL-reserved decisions are paused. But the protection of "I just didn't know" is not available to anyone post-hoc — including the BDFL.

### When timeframes can be extended

A decision-maker who needs more time may request an extension BEFORE the original window expires. The extension request goes through the normal maintainer channel and is granted by the proposer when the request is in good faith (sickness, travel, real disagreement that needs research).

Extensions requested AFTER the window expires are not honored. The forfeiture has already taken effect.

### Notification expectations

The legitimacy of forfeiture rests on the decision having been visible to the forfeiting party. Specifically:

* Decisions opened through the project's normal channels (GitHub PR / ADR / YouTrack-tracked proposal with @-mention to the maintainer team) ARE considered notified.
* Decisions opened in a side channel that not all maintainers monitor (a private DM thread, an unannounced doc edit) are NOT considered notified — forfeiture doesn't apply.
* The proposer is responsible for ensuring the decision lands in a channel the maintainer team is expected to read. The maintainer team is responsible for reading those channels with reasonable diligence given their declared availability.

A maintainer who has explicitly declared an out-of-office period gets that period excluded from their personal timeframe — but the decision itself still proceeds on its public window. The OOO maintainer just doesn't lose the right to weigh in WITHIN their personal extended window when they return.

## Consequences

* **Positive**: decisions can be relied upon to close. A heavy decision with a 21-day window will, in fact, resolve in 21 days regardless of who responds.
* **Positive**: maintainers know the rule going in. There's no surprise when their silence forfeits the right — the window was published.
* **Positive**: the BDFL position has fewer single-point-of-failure properties. If Scott is unavailable, the team continues; if Scott then returns and disagrees with a decision, the path forward is a successor PR/ADR, not a retroactive veto.
* **Negative**: requires proposers to set windows thoughtfully. A proposer who sets too-short a window for a heavy decision is being unilateral; the maintainer team's other members may push back on the timeframe before the window starts.
* **Negative**: introduces a tier system (window length scaled to weight). The taxonomy of "what's heavy" is necessarily fuzzy at the edges; some decisions will be miscategorized. The rule errs on the side of more time over less when in doubt.
* **Negative**: a decision-maker who is genuinely unable to monitor channels — sick, on leave, life event — can lose input on something they would have engaged with. This is a real cost. Mitigations: the OOO-period exception, the proposer's responsibility to set a window proportional to weight, and the option to revise via a successor PR/ADR.

## Relationship to ADR 0009

This ADR strengthens and generalizes the lazy-consensus rule (ADR 0009 rule 2.3) without superseding it. The 7-day default for PRs and ADRs stays; this ADR explicitly extends the same principle to broader decisions and to the BDFL personally.

If a future revision of ADR 0009 is needed to reflect this generalization, it would be a separate successor ADR — this one captures the policy; the governance ADR captures the role definitions.

## Notes

The rule was proposed by Scott in response to the ADR backfill exercise (TAM-9 through TAM-14, TAM-20). The motivating concern: "if someone, including me, goes dark for a week or a month it's on me, not on the project. it moves on without me."

This ADR makes that intent binding on the maintainer team going forward.
