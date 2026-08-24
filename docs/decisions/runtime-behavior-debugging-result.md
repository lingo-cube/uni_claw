# PROJECT_LEADER_RUNTIME_BEHAVIOR_DEBUGGING_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_RUNTIME_BEHAVIOR_DEBUGGING_AND_RECOVERY_FIX —
> establish the evidence-first Runtime Behavior Debugging workflow, then use it
> to fix the Capstone revisit-coverage failure (Phase 1 → 6).
> Fix mode: **Option A (coverage-driven bounded budget) + Option B
> (boundary-proven revisit termination)**, implemented ONLY after evidence was
> collected and classified.

---

## 1. Evidence Summary

Real-device evidence (`/tmp/capstone_evidence.txt`, `CapstoneSingleAgentRunTests`):

| artifact | pre-fix evidence |
|----------|------------------|
| discovery | epoch `seq=[2,3,4,5,6,7]`, sources=8, unresolved=0 — **all 8 children discovered** |
| verified returns | 7 (Child 06,05,07,08,04,03,02) — every return passed post-completeness consistency |
| revisit coverage ledger | freshly-exposed=[06,08,04,02,07,05,03] — **Child 01 absent** |
| observation timeline | Child 01 OCR text ONLY in seq=2 (launch frame); every reverse frame (seq 30-54) top-row sequence 05→04→03→02 — Child 01 never re-entered the viewport |
| action history | 5 forward scrolls, 7 dispatch+return taps, 5 reverse scrolls (0.40/0.20/0.10 halving engaged) |
| grounding result | zero "grounding rejected" / "fresh grounding mismatch" / "authorization rejected" — grounding chain never failed |
| terminal reason | `bounded revisit coverage INCOMPLETE: discovered=8, resolved=7, unresolved=[Child 01] (bounded budget exhausted...)` |

Evidence-level: **E4** (trace + observation timeline + fact/evidence ledger).

## 2. Root Cause

**Insufficient bounded policy (Phase 2 classification: C) — a budget-quantity
shortfall, not a logic defect.** The frozen RevisitBudget
(`discoveryObservations − 1` = 5) was exactly one reverse step short of walking
the viewport from the exploration end back to the top of the list: Child 01 is
one row above the final reverse frame (`Child 02 | Child 03 | Child 04`,
seq=48/53-54) and the budget hit 0 at exactly that point. Failure type
(Phase 1): **E — recovery/revisit failure**; the recovery direction, adaptive
step halving, grounding chain, and coverage gate all worked correctly — only
the budget quantity was insufficient. No evidence of B (grounding) or C
(dispatch/return) failures; no premature OCR/device/emulator attribution
(Child 01 was cleanly OCR-detected at the top frame; the reverse mechanics are
uniform ~1 row per swipe).

## 3. Ownership Decision

**Owner: Agent — revisit budget derivation (+ coverage tracking).** The budget
is frozen by the Agent at discovery-epoch creation inside `RunOpenWorldAsync`;
the coverage ledger/gate are Agent-owned. Traversal (action execution),
Environment (observation acquisition), and Semantic Capability (evidence
production) are NOT touched. No change to DFS ownership, GoalEvidence
authority, or Semantic authority; no scenario knowledge introduced.

## 4. Production Change List

| file | change |
|------|--------|
| `src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs` | **Option A — COVERAGE-DRIVEN REVISIT BUDGET**: `RevisitBudget = Math.Max(discoveryObservations.Length - 1, withFrozenSources.UniqueNavigationSourceIdentities.Length)` — the reverse budget can now walk the viewport back to the position where EVERY discovered source was seen (bounded: a fixed finite number; each reverse decrements it). |
| `src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs` | **Option B — BOUNDARY-PROVEN REVISIT TERMINATION**: a reverse whose post-scroll frame carries the SAME navigation-occurrence set as the pre-reverse frame, at the floor step (`revisitStepFraction <= RevisitStepFloor`), is boundary-confirmed → sets `RevisitBudget = 0` and re-enters the existing coverage gate (fail closed with unresolved-branch evidence when a gap exists; root-terminal paths when not). Prevents budget burning on no-op scrolls and strengthens the no-infinite-loop guarantee. Trace: `bounded revisit boundary CONFIRMED: reverse produced no new viewport occurrences ...`. |
| `src/UniClaw.Runtime/Agent/Agent.cs` | (from the prior coverage-completion task, unchanged this task) `_revisitCoverage` ledger + `RevisitCoverage` snapshot. |

Test-side changes (Phase 5, evidence-spec model):

| file | change |
|------|--------|
| `tests/.../AdaptiveRevisitCoverageCompletionTests.cs` | `TenBranches_AsymmetricReverse_CoverageDrivenBudget_CompletesAll` (Option A proof: the slow 1-row reverse now completes 10/10 — old budget would gap); `OneWayWorld_UnreachableBranches_CoverageGap_FailsClosedWithEvidence` (genuine gap: one-way list — reverse is a physical no-op — fails closed with unresolved evidence after `bounded revisit boundary CONFIRMED`); assertions are semantic (no fixed click counts). |
| `tests/.../OpenWorldBoundedSourceRevisitTests.cs` | RVT21011: replaced the fixed `Equal(3, ScrollBackward count)` with a bounded range check `1..3` (evidence-based boundary termination may stop earlier; no fixed action sequence). |
| `tests/.../AdaptiveRevisitRecoveryTests.cs` | (prior task, retained) RecoveryWorld forward clamp. |
| 7 real-device test classes | added `[Collection("RealDevice")]` — serializes emulator-touching tests (Capstone, EBD, Settings phases) to remove concurrent foreground contention on the shared emulator. |

## 5. Architecture Impact

| dimension | impact |
|-----------|--------|
| DFS ownership | NONE — run loop, epoch freeze, pending ordering, terminal paths unchanged |
| Agent authority | NONE — budget derivation/termination stay Agent-owned decisions |
| Traversal | NONE |
| Semantic capability | NONE |
| Source grounding | NONE — grounding chain untouched |
| GoalEvidence | NONE — the coverage gate fail-closed reporting is unchanged; GoalEvidence still terminates only satisfied runs |

**ArchitectureDelta: NONE** — the fix touches only the budget quantity and the
reverse-termination criterion inside the existing Agent revisit seam.

## 6. Regression Result

```
Total:     1958
Passed:    1957
Failed:     1  (ExternalBoundary_RealDevice — KNOWN out-of-scope EBD failure,
                normalization ordered-overlap; unchanged by this task)
```

- **Capstone real-device: PASS** (standalone 32-33s and inside the full suite;
  `STATE=Completed`, `GOAL_EVIDENCE=True@61`, `Visited8/8CAPSTONECOMPLETE`,
  revisit coverage ledger contains all 8 children). Pre-fix: Failed
  (resolved=7, unresolved=[Child 01]).
- Revisit-related suites: **117/117** green (AdaptiveRevisitRecovery,
  AdaptiveScrollGrounding, BranchGroundingBeforeDispatch,
  AdaptiveRevisitCoverageCompletion, OpenWorldBoundedSourceRevisit,
  OpenWorldBranchAcceptanceProvenanceRepair, OpenWorldTraversalIdentitySafety,
  OpenWorldPostExplorationCurrentRepair, PostCompletenessConsistency,
  OpenWorldCompletenessNonMonotonicExtension, SettingsTreeCapstone, U2OpenWorld).
- `scripts/check-consistency.sh`: ALL PASS; `git diff --check`: clean.
- Test-infrastructure fix: the full suite previously showed a flaky Capstone
  failure (Settings app foregrounded by the concurrently-running EBD test →
  `初始语义页面解析失败`); serializing the real-device tests removed it.

## 7. Remaining Uncertainty

1. **Option B residual risk (LOW)**: the boundary condition fires only at the
   floor step (0.2/0.1 already tried with no viewport change). A world where a
   0.2/0.1 swipe is a no-op but a hypothetical finer step would move is not
   physically plausible (swipes are monotonic in distance); the floor guard is
   the designed mitigation. No evidence of premature firing in any test or the
   real-device run.
2. **Budget formula sufficiency**: `max(observations−1, discovered sources)` is
   evidence-proven for this fixture (needed 6, got 8). For pathological worlds
   (per-reverse movement < 1 row), the boundary-proven termination (Option B)
   is the second line of defense — the coverage gate still fails closed with
   evidence. Not proven for such worlds beyond the one-way deterministic test.
3. **EBD real-device failure** remains out of scope (normalizer ordered-overlap
   continuity — adjacent scroll frames share a prefix, not a suffix).
4. **Independent review**: three independent-review subagent attempts failed
   before completing (session subagent infrastructure). The final minimal
   review attempt is recorded below; the review checklist (STOP CONDITIONS /
   boundedness / premature-fire risk / test model / evidence-first) was
   executed and its outcome is appended. Per the no-self-graduation rule, the
   user should treat this RESULT as pending until the independent review
   verdict is recorded.

---

## Independent Review

**STATUS: PENDING — NOT self-graduated.**

Five independent-review subagent attempts were made (3 fresh subagents, 1 fork,
1 minimal-scope prompt) — all failed before producing a closing message
(session subagent infrastructure failure, not a review finding). Per the
no-self-graduation rule, this RESULT is therefore NOT graduated; it is
submitted for review.

For the reviewer (user or a future session), the complete checklist with the
implementer's self-executed verification — to be independently re-checked, not
trusted:

| # | check | implementer's finding (verify independently) |
|---|-------|-----------------------------------------------|
| 1 | STOP: Agent authority / DFS ownership / GoalEvidence modified? | NO — only budget quantity + reverse-termination criterion inside the existing Agent revisit seam (see §4 diff) |
| 2 | STOP: scenario knowledge introduced? | NO — zero Settings/child-index/list-size/coordinate/OCR tokens in the production diff (`git diff` reviewable) |
| 3 | STOP: fixed action sequences / click counts as proof? | NO — new tests assert semantic outcomes (coverage achieved, boundary CONFIRMED trace, coverage INCOMPLETE evidence, RevisitCoverage ledger); RVT21011's fixed `Equal(3, count)` was replaced by a bounded range |
| 4 | Boundedness (Option A) | budget = max(obs−1, discovered) — fixed finite; each reverse decrements; budget==0 → terminate |
| 5 | Boundedness (Option B) | fires only at floor step after an identical-frame reverse → sets budget 0 → coverage gate terminates; at most 2 no-progress reverses per dispatch-loop iteration |
| 6 | Premature-fire risk (Option B) | LOW: floor guard means a 0.4/0.2/0.1 sequence already failed to change the viewport before firing; identical sig sets ⟹ no new grounding opportunity |
| 7 | Coverage gate | fail message carries discovered/resolved counts + unresolved identities; root terminal still reachable when never-exposed empty (denied branches don't block) |
| 8 | Evidence-first | classification (type E, root cause C) grounded in `/tmp/capstone_evidence.txt` (E4); no OCR/device/emulator attribution |
| 9 | Regression | 1957/1958 (only known out-of-scope EBD fails); Capstone passes standalone and in-suite; 117/117 revisit suites; check-consistency ALL PASS |

Evidence files for the reviewer: `docs/decisions/capstone-revisit-evidence-analysis.md`,
`docs/decisions/capstone-revisit-coverage-analysis-result.md`, `/tmp/capstone_evidence.txt`,
`/tmp/full_reg_fix2.txt` (1957/1), and `git diff` of the §4 file list.

