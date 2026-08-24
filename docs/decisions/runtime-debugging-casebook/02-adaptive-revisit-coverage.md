# Adaptive Revisit Coverage

## Human Symptom

The Agent visited 6 of 8 discovered branches during open-world traversal, but
the remaining 2 branches were never given a re-grounding opportunity. The
system declared "Verified bounded traversal completion" anyway, failing to
detect that the coverage gap was real. The final `GoalEvidence` remained
unsatisfied, but the failure message did not identify WHICH branches were
never re-exposed.

## Expected Reality

The bounded revisit mechanism should track container coverage completeness:
every discovered pending branch must either be dispatched (with a verified
return) or be given at least one re-grounding opportunity within the viewport
recovery budget. If budget is exhausted with never-exposed branches, the system
should fail closed with explicit unresolved-branch evidence.

## Observed Reality

The adaptive revisit termination condition was "a branch became
CURRENTLY_VISIBLE" — once every AUTHORIZED (already-dispatched) child had
completed, the revisit declared "Verified bounded traversal completion" even
while discovered branches that were never given a re-grounding opportunity
remained pending. The revisit served **single-branch recovery**, not
**container coverage completion**. The coverage gap was real but invisible:
no evidence of WHICH branches were never re-grounded.

## Reality Gap

The revisit loop tracked "has this branch been dispatched?" but not "has every
discovered branch been given a re-grounding chance?" — so budget exhaustion
with never-exposed branches was indistinguishable from full coverage.

## Evidence Reference

- Decision: `docs/decisions/adaptive-revisit-coverage-completion-result.md`
  (full fix — added CONTAINER COVERAGE COMPLETION semantics to the existing
  bounded revisit seam)
- Decision: `docs/decisions/capstone-revisit-coverage-analysis-result.md`
  (empirical evidence: visited 6/8, final failure "Verified bounded traversal
  completion but fresh GoalEvidence remains unsatisfied")
- Trace: revisit ledger showing `discovered_branches: 8, resolved: 6,
  unresolved: [branch_3, branch_7]` (after fix)
- Test: `OpenWorldBranchAcceptanceProvenanceRepairTests` (coverage completion
  assertions)

## First Divergence Point

The revisit termination condition: "a branch became CURRENTLY_VISIBLE" was
used as a proxy for "every branch has been given a chance". The loop never
maintained a separate `unresolved_branches` set (discovered − freshly-exposed)
as a termination criterion. The divergence happened when the first
never-exposed branch was skipped by the visibility-based termination.

## Owner

**Agent — revisit / viewport-recovery seam.** The DFS, Traversal, and
Semantic capability are not at fault. The revisit loop is an Agent-owned
exploration mechanism; its termination condition was incomplete.

## Minimal Change

Add a **container coverage ledger** to the existing bounded revisit seam:
- each newly exposed pending branch is recorded in a `freshly_exposed` set
  (at the existing visibility check)
- `unresolved = pending_branches − freshly_exposed`
- budget exhaustion with `unresolved` non-empty → fail closed with
  unresolved-branch evidence (discovered/resolved counts + identities)
- budget exhaustion with `unresolved` empty → the existing "Verified bounded
  traversal completion" path

No new loop, no new authority, no DFS change. The revisit termination now
depends on the unresolved-branch set, not just visibility.

## Rejected Alternatives

- **Increase the revisit budget:** rejected — would not fix the conceptual
  gap; a larger budget could still exhaust while never-exposed branches remain.
  The problem is not the budget size but the termination condition.
- **Add a separate loop for missed branches:** rejected — would create a new
  authority surface and bypass the bounded-budget discipline. The existing
  revisit seam is the correct boundary.
- **Make DFS track exposure:** rejected — DFS owns branch discovery and
  dispatch, not viewport re-exposure. The coverage ledger is an Agent-revisit
  concern.

## Engineering Lesson

**"Every branch was dispatched" and "every branch was given a chance to be
reached" are different states.** When a bounded budget is used for viewport
recovery, the termination condition must track the set of discovered branches
that were never given a re-grounding opportunity — not just the completion of
already-authorized children. A coverage gap that is invisible to the system is
worse than a fail-closed gap that produces explicit evidence.