# PROJECT_LEADER_ADAPTIVE_REVISIT_COVERAGE_COMPLETION_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Goal: make the OpenWorld bounded revisit serve **container coverage
> completion**, not single-branch recovery — every discovered pending branch
> must either dispatch (with a verified return) or be given a re-grounding
> opportunity within the bounded budget; budget exhaustion with never-exposed
> branches fails closed with unresolved-branch evidence. Scope limited to the
> revisit seam; no DFS / FSM / Traversal / Semantic / SourceGrounding change.

---

## 1. Problem Confirmed

The adaptive revisit (previous task) recovered **some** branches, but the
revisit termination condition was "a branch became CURRENTLY_VISIBLE", and the
budget-exhausted path could declare **"Verified bounded traversal completion"**
as soon as every AUTHORIZED (already-dispatched) child had completed — even
while discovered branches that were never given a re-grounding opportunity
remained pending. Capstone empirical evidence: Visited 6/8 with the final
failure "Verified bounded traversal completion but fresh GoalEvidence remains
unsatisfied" — the coverage gap was real but invisible (no evidence of WHICH
branches were never re-grounded).

The revisit therefore served single-branch recovery, not container coverage
completion:

```text
Pending branch → revisit → see a branch → dispatch     (coverage not tracked)
```

## 2. Fix (scope-limited, evidence-driven)

Added **CONTAINER COVERAGE COMPLETION** semantics to the existing bounded
revisit seam (no new loop, no new authority, no DFS change):

```text
Container inventory (frozen epoch, unchanged)
        |
        v
Track unresolved branches (per-Container coverage ledger)
        |   - freshly exposed = pending branch CURRENTLY_VISIBLE in a
        |     dispatch pass (recorded at the existing visibility check)
        v
Adaptive viewport recovery (0.4 → 0.2 → 0.1 → floor, unchanged)
        |   - trace now reports coverage: discovered=.. resolved=.. unresolved=[..]
        v
Fresh grounding (unchanged) → Dispatch grounded branch (unchanged) → Return
        |
        v
Budget exhausted?
        |  unresolved = pending − freshly-exposed
        |  unresolved non-empty  → FAIL CLOSED with unresolved-branch evidence
        |                         (discovered / resolved counts + identities)
        v
(root terminal / zero-dispatch fail, only when no coverage gap)
```

- **Revisit termination now depends on the unresolved-branch set**: every
  discovered pending branch must have either dispatched successfully
  (completed) or been freshly exposed — a freshly exposed branch that remains
  pending carries its OWN failure evidence (fresh-authorization denial /
  fresh-grounding mismatch), so it never silently blocks; a branch NEVER
  freshly exposed was never given a re-grounding opportunity.
- **Budget exhausted with a coverage gap** → fail closed with the unresolved
  branch evidence:
  `bounded revisit coverage INCOMPLETE: discovered=N, resolved=M, unresolved=[...] (bounded budget exhausted; these branches were never given a re-grounding opportunity); zero dispatch。`
- **Denied-with-evidence branches do not block** (EBD semantics preserved):
  a denied branch was visible at dispatch time, hence freshly exposed, hence
  excluded from the unresolved set — the root terminal / verified-return paths
  behave exactly as before for them.
- No Settings logic, no child index / list-size assumptions, no top jump, no
  coordinate memory, no OCR special rules, no forced dispatch were introduced.

### Production delta

| File | Change |
|------|--------|
| `src/UniClaw.Runtime/Agent/Agent.cs` | New Agent-owned **revisit coverage ledger** `_revisitCoverage` (per-Container set of freshly-exposed pending branch identities; sole mutable owner = Agent, mirroring `_branchProgress`) + public `RevisitCoverage` evidence snapshot. |
| `src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs` | (1) dispatch pass records freshly-exposed identities at the existing CURRENTLY_VISIBLE check; (2) budget-exhausted path gains the **coverage-completion gate** (never-exposed pending branches → fail closed with unresolved evidence) in front of the root-terminal / generic zero-dispatch paths; (3) revisit trace now reports the tracked coverage progress (`coverage: discovered=.. resolved=.. unresolved=[..]`); (4) helpers `RecordFreshlyExposed` / `BuildCoverageSummary`. |

## 3. Authority Verification

- **No new decision authority**: the Agent remains the sole run-level semantic
  authority; the coverage gate is an Agent-owned evidence computation inside
  the existing budget-exhausted decision (Agent owns the decision, Traversal
  executes only authorized actions, Container owns page-local state).
- **No contract relaxation**: every dispatch gate is unchanged (identity safety
  → explicit grounding → validator → current visibility → fresh grounding →
  fresh authorization); the change only tightens the fail-closed outcome at
  budget exhaustion (coverage gap can no longer be misreported as "verified
  bounded traversal completion").
- **No new state owner**: the ledger is Agent-owned (same owner as
  `_branchProgress`); no new component / facade / lifecycle.
- **Scope guard**: EBD / Settings-layout / Normalizer ordered-overlap
  continuity issues remain explicitly OUT of scope (§6).

## 4. DFS / FSM / Traversal Impact

- **DFS**: `RunOpenWorldAsync` structure unchanged — frozen discovery epoch,
  deterministic pending ordering, verified-return trigger, subtree terminal,
  root terminal, adaptive revisit steps all untouched.
- **FSM**: no state-machine change.
- **Traversal**: no lowering / verification change.
- **Semantic boundary / SourceGrounding contract**: untouched.

## 5. New Tests (4/4 PASS)

`tests/UniClaw.Runtime.Tests/Evidence/AdaptiveRevisitCoverageCompletionTests.cs`
drives the real Agent over `CoverageWorld` — a scrollable list whose FORWARD
scrolls honor the adaptive StepFraction (fast exploration) but whose REVERSE
scrolls move one row per swipe (a physical world where reverse recedes more
slowly — the precondition for a bounded-budget coverage gap). The branch
inventory is frame-spanning (first-appearance grounding), mirroring a real
caller's discovery aggregation.

| Test | Proof |
|------|-------|
| `SixBranches_BottomToTop_CoverageCompletes_AllDispatch` | Bottom → adaptive reverse → TOP recovered: all 6 branches dispatched from fresh grounding within budget; the run fails ONLY at the root terminal (proof GoalEvidence false) — never a coverage gap, never a blind zero-dispatch. |
| `UnreachableBranchesWithinBudget_CoverageGap_FailsClosedWithEvidence` | 10 children / viewport 4, budget 4: every reachable branch (06-10) dispatches; the slow reverse cannot re-enter the top viewport in time — Node 01..05 are NEVER freshly exposed → the run FAILS CLOSED with `coverage INCOMPLETE` + `unresolved=[...]` listing them, NOT a premature "verified bounded traversal completion"; the `RevisitCoverage` ledger evidences they were never exposed. |
| `VisionOnly_NoAdbDependency` | Coverage completion runs on primary-Vision evidence only — zero ADB actions. |
| `NoSettingsVocabulary_ArchitectureGuard` | The generic coverage path contains no Settings/WiFi/Android scenario vocabulary. |

Also fixed in `tests/UniClaw.Runtime.Tests/Evidence/AdaptiveRevisitRecoveryTests.cs`:
the `RecoveryWorld` forward scroll now clamps at the LAST window (a list's
physical end) instead of over-shooting a tail frame — this makes the
`GenericTree` proof genuinely recover all 10 branches bottom→top (it had been
recovering 9/10 with the 10th silently uncovered, which the new coverage gate
now correctly exposes).

Result: **4/4 new tests pass**; the revisit-related suites
(AdaptiveRevisitRecovery + AdaptiveScrollGrounding + BranchGroundingBeforeDispatch
+ AdaptiveRevisitCoverageCompletion + OpenWorldBoundedSourceRevisit +
OpenWorldBranchAcceptanceProvenanceRepair + OpenWorldTraversalIdentitySafety)
are **48/48 green**.

## 6. Full Regression

```
Total:     1957   (was 1953; +4 new coverage tests)
Passed:    1955
Failed:     2      (both known real-device failures, unchanged)
```

Stable across 4 full runs (1955/2 each). `scripts/check-consistency.sh`: ALL
PASS. `git diff --check`: clean. (One run observed a third, transient failure
that did not reproduce in any subsequent run — classified as an environment
flake, likely emulator/vision-socket timing under parallel real-device load;
not reproducible and not attributable to this change.)

Remaining failures (classified — **both out of scope**):
1. `CapstoneSingleAgentRunTests.Capstone_OneAgentOneRun_RealEmulator_ReachesCapstoneComplete`
   — code issue, NOT environment. The run now fails with the NEW coverage
   evidence (`coverage INCOMPLETE`) instead of the misleading "Verified bounded
   traversal completion": the top-of-list children still cannot all be
   re-grounded within the frozen RevisitBudget (empirically 6-7/8 reached).
   This is the intended new observability; the remaining fix (out of scope) is
   making the discovery/inventory policy leave enough budget for the
   top-of-list children.
2. `ExternalBoundaryRealDeviceTests.ExternalBoundary_RealDevice`
   — code issue, NOT environment: the Settings "Location" row moves off the
   first screen; adjacent scroll frames share a PREFIX (not a suffix) of rows,
   so the ordered-overlap normalizer cannot prove continuity. Fix direction
   (out of scope): align the normalizer's ordered-overlap contract with the
   actual scroll-frame relationship or adjust test data.

## 7. Verification Summary

- Production: coverage ledger + gate + trace implemented; **build clean**;
  revisit-related suites green; full regression stable at 1955/1957.
- The three implementation questions are answered in the code:
  1. **Revisit termination** now depends on the unresolved-branch set
     (dispatched or freshly-exposed-with-evidence), not "a branch was found".
  2. **Viewport recovery progress** is tracked as evidence: discovered count /
     resolved count / unresolved identities (per revisit trace + fail reason)
     and the freshly-exposed occurrence set (`RevisitCoverage` ledger).
  3. **Bounded**: the frozen RevisitBudget remains the hard stop; exhaustion
     with a coverage gap fails closed with the unresolved branch evidence.
