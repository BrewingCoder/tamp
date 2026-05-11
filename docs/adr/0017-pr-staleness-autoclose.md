# ADR 0017: Pull-request staleness — auto-close after author silence on review feedback

* Status: Accepted
* Date: 2026-05-11
* Deciders: scott
* Tracking: TAM-22 (forthcoming)

## Context and Problem Statement

Every healthy long-lived OSS project accumulates PRs that opened, received maintainer feedback (clarification request, change request, "can you rebase / add a test / split this in half"), and then stalled because the author never came back. Over years these accumulate into a tail of dozens or hundreds of half-finished proposals. Newcomers reading the PR list see noise instead of signal. Maintainers see guilt. The graveyard is the worst aesthetic in OSS — visible permanent evidence of unfinished work that nobody is going to finish.

ADR 0016 established the rule that decision-maker silence within a scope-appropriate timeframe forfeits the right to weigh in. This ADR records the contributor-side mirror: PR-author silence on review feedback triggers auto-close after a scope-appropriate timeframe.

The principle is the same — silence has explicit consequences, the project moves — but the actor and the action differ:

* ADR 0016: a maintainer / decision-maker goes dark; the decision proceeds without their input.
* This ADR: a PR author goes dark on requested changes; the PR closes.

## Decision Drivers

* **The PR list is a signal channel.** Maintainers, contributors, and casual readers use the open-PR list to understand "what's actively being worked on." A list dominated by year-old half-finished work signals "nothing is happening here" — even when active work is happening in newer PRs.
* **Closing isn't punitive.** A closed PR can be reopened. Authors who return with the requested changes pick up exactly where they left off. The close is a hygiene action, not a rejection.
* **Maintainer time spent re-reading old PRs is a tax.** When a year-old PR finally gets a response, the maintainer has to re-read the original context, re-evaluate against current main, re-check whether their original feedback even still applies. That cost is paid every time someone resurrects an old PR — and most of the time, the author never does, so the cost was wasted.
* **Authors need fairness.** Auto-close without notice would be hostile. The rule needs notification windows and a clear "you have N more days" reminder before the close.
* **Some PRs are legitimately long-running.** Architecture-shaped work, multi-stage refactors, dependencies-on-an-external-event. The rule needs an explicit pause mechanism for those cases.

## Considered Options

1. **No auto-close (status quo).** Pro: never lose a PR that might eventually come back. Con: the graveyard.
2. **Hard timeout: close every PR with no author activity for X days, regardless of state.** Too aggressive — catches active discussions where the author is waiting on a maintainer reply.
3. **Conditional auto-close: only when the PR has unaddressed maintainer feedback (chosen).** Targets the specific failure mode (author silence after a change request) without affecting PRs blocked on the maintainer side.

## Decision

A PR auto-closes after the PR author has been silent on requested changes for a scope-appropriate timeframe. The mechanism:

### The clock starts when a maintainer requests changes

A "change request" means any of the following on an open PR:

* GitHub "Request changes" review.
* A `@<author>` mention in a maintainer review comment asking for a change or clarification.
* A `needs-changes` / `needs-info` / `awaiting-author` label applied by a maintainer.

The clock does NOT start on:

* General review comments that don't request a change (e.g., "looks good, will merge after the test run").
* Suggestions in a review that don't block merge.
* PRs in `draft` state (drafts are explicitly not ready for review).

When the clock starts, the maintainer-team's automation posts a comment that names the deadline explicitly:

```
This PR has open change requests from a maintainer review.
Per ADR 0017, if there's no author response by 2026-06-25 (30 days from
today), the PR will be marked stale and auto-closed.

You can:
- Push commits addressing the requested changes (resets the clock).
- Reply with progress / blockers / questions (resets the clock; counts as engagement).
- Add the `needs-time` label and comment why (pauses the clock).
- Convert the PR to draft (pauses the clock; reopen as ready when ready).
```

### Scope-appropriate timeframes

Same tiering principle as ADR 0016, scaled to PR size:

| Change request scope | Default window |
|---|---|
| Trivial (typo, one-line fix, minor doc) | **14 days** |
| Standard (single-file change, bug fix, small feature) | **30 days** |
| Substantial (multi-file refactor, new feature with tests) | **45 days** |
| Major (architectural change, breaking change, new wrapper satellite) | **60 days** |

The maintainer making the change request picks the tier. If the PR contains multiple change-request shapes, the LONGEST applicable tier wins (be generous).

### What counts as "author response" (resets the clock)

* A new commit on the PR branch.
* A reply comment from the PR author.
* The author adds the `needs-time` label with an explanation.
* The author marks the PR as draft (effectively saying "I'll come back when ready").

Anything from the author within the window counts. Even "I'm still working on this, give me another two weeks" — that's engagement. The clock resets and the discussion continues.

### The auto-close sequence

When the clock runs out without author activity:

1. The maintainer-team's bot posts a final notice: `This PR has been stale for [N] days with open change requests. Closing per ADR 0017. Reopen anytime by pushing commits — the conversation history stays.`
2. The bot adds the `stale-autoclosed` label.
3. The PR is closed.

The PR's commit history, comments, and review state remain visible. Anyone — including the original author — can reopen with a push.

### Pause mechanisms (legitimate long-running work)

Three explicit pauses:

1. **`needs-time` label** — applied by the author with a comment explaining why. Pauses the clock indefinitely. A maintainer who disagrees with the pause can remove the label; the clock resumes where it left off.
2. **Draft state** — converting to draft signals "not ready for review." The clock is paused while the PR is a draft. Ready-for-review converts the draft back to open and the clock resumes.
3. **Maintainer-applied `pinned` label** — for PRs the maintainer team explicitly wants kept open regardless of age (rare; reserved for architectural placeholders, ongoing experiments).

### Author courtesy: maintainer-side silence

If the author has responded to the change request and is now waiting on maintainer re-review, the clock does NOT run against the author. The PR is blocked on the maintainer side; that's an ADR-0016 problem (the project's open-decision timeframe), not an ADR-0017 problem (PR-author silence).

The bot automation distinguishes these states by looking at the most recent activity on the PR: if it's from the author, the clock is on the maintainers (per 0016); if it's from a maintainer with a request-changes review, the clock is on the author (per 0017).

### Reopening a closed PR

Closed PRs can be reopened by anyone with push access OR by the original author. No re-justification is needed — the previous review context is still in the PR. The author addresses the original change request and the review continues from where it stopped.

If a substantial amount of main has moved past the PR's branch, the author may need to rebase. That's normal; it's not a re-litigation of the decision to merge.

## Consequences

* **Positive**: the open-PR list reflects active work. Newcomers see what's happening now, not what stalled three years ago.
* **Positive**: authors get explicit windows and explicit reminders. The close is never a surprise.
* **Positive**: maintainers stop paying the re-read tax on year-old PRs. When an author returns to a closed-but-reopenable PR, the maintainer engages with current context, not stale context.
* **Positive**: legitimate long-running PRs have explicit pause mechanisms. The rule doesn't punish careful, slow-burn work — it punishes silence on requested changes.
* **Negative**: requires automation. The bot needs to track change-request labels, post deadline reminders, and execute the close action. Implementable via a GitHub Actions workflow; not free.
* **Negative**: some authors will lose PRs they would have eventually finished. The reopen path mitigates this. The lost-work risk is acceptable given the alternative graveyard.
* **Negative**: the tier taxonomy of "what scope" is fuzzy at edges. Maintainer picks the tier when requesting changes; an author who thinks the tier is wrong can request an extension via comment.

## Relationship to ADR 0016

ADR 0016 and ADR 0017 are sibling rules with the same underlying philosophy — silence has consequences, the project moves. They apply to different actors:

* ADR 0016: silence from a decision-maker (maintainer team, BDFL) on an open decision. After the window: the decision proceeds without their input.
* ADR 0017: silence from a PR author on a maintainer's change request. After the window: the PR closes.

Both are append-only forfeitures. Both have published windows. Both reserve the right to revise via fresh proposals (a successor ADR for 0016; a fresh PR or reopen-and-push for 0017).

## Implementation

The automation lives in `.github/workflows/pr-staleness.yml` (forthcoming). It uses GitHub's API to:

1. Detect change-request reviews and `needs-changes` / `needs-info` labels.
2. Track the last author activity on each PR.
3. Post deadline reminders at the 50% and 90% marks of the window.
4. Auto-close + label when the window expires.
5. Honor pauses (`needs-time` label, draft state, `pinned` label).

The workflow runs daily.

## Notes

This ADR was proposed by Scott after observing the OSS graveyard pattern: *"there is NOTHING worse than seeing an OSS with 100 year old PRs."*

The rule is opinionated. We're choosing project hygiene over maximum-backwards-compatibility-with-every-stalled-PR. The reopen path means no work is truly lost; only the PR-as-active-thing closes.
