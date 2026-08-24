# PROJECT_LEADER_CAPSTONE_REVISIT_COVERAGE_ANALYSIS_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Mode: **ANALYSIS ONLY — no fix.** Evidence-driven proof of why the Capstone
> coverage did not close, per the A/B/C classification and the Budget
> recoverability CASE judgment. All claims are grounded in the real-device run
> evidence dump (`/tmp/capstone_evidence.txt`, produced by
> `CapstoneSingleAgentRunTests.Capstone_OneAgentOneRun_RealEmulator_ReachesCapstoneComplete`
> on the live emulator).

---

## 1. Root Cause

**The frozen RevisitBudget (= discovery observations − 1 = 6 − 1 = **5**) is
exactly one reverse step short of walking the viewport from the exploration
end position back to the top of the list.**

- All 8 children were discovered (epoch `seq=[2,3,4,5,6,7]`, `sources=8`,
  `unresolved=0`) and 7 were recovered + dispatched + verified-returned
  (`resolved=7`).
- **Child 01 — the only remaining branch — was never re-exposed**: it is one
  row above the final reverse frame (`Child 02 | Child 03 | Child 04`,
  seq=48), and the budget hit 0 at exactly that point.
- The recovery **direction and step strategy are correct** (every executed
  reverse revealed one new row: 04 → 03 → 02); the halving policy engaged
  (0.40 → 0.20 → 0.10) and the last successful exposure (Child 02) happened
  on the final budget unit. Coverage was one unit short — a **budget quantity**
  shortfall, not a semantic/grounding/authority defect.

The new coverage gate (previous task) is therefore working as designed: it
correctly refused a premature "verified bounded traversal completion" and
failed closed with the unresolved-branch evidence.

## 2. Evidence Timeline (real-device run)

| seq | frame (OCR text) | phase |
|-----|------------------|-------|
| 2 | `Fixture Root ×3 \| Child 01 \| Child 02 \| Child 03` | **launch frame — top of list; Child 01 visible** |
| 3-5 | `Child 02-04`, `Child 03-05`, `Child 04-06` | forward exploration (scrolls 1-3) |
| 6-7 | `Visited 0/8 \| Child 06 \| Child 07 \| Child 08` | forward exploration end (bottom; scrolls 4-5) |
| 8 | `Child 05-08` | epoch FROZEN (`seq=[2,3,4,5,6,7]`, budget=5) |
| 10-28 | 4 × (child page → verified return): **06, 05, 07, 08** | dispatch from bottom viewport (Steps 6-13) |
| 30-31 | `Child 05-08` | start of reverse recovery |
| 32 / 33 | `Child 05-07` / `Child 04-06` | reverses 0.40 / 0.20 → **Child 04 exposed** |
| 36 | verified return **04** (Step-17) | dispatch from seq=33's frame |
| 40 | `Child 03-06` | reverse 0.40 → **Child 03 exposed** |
| 43 | verified return **03** (Step-20) | dispatch |
| 46-47 | `Child 03-06` / `Visited 6/8 \| Child 03-05` | reverses 0.40 → near-top |
| 48 | `Child 02 \| Child 03 \| Child 04` | reverse 0.20 → **Child 02 exposed** (budget 5→0) |
| 51 | verified return **02** (Step-24) | dispatch on final budget unit |
| 53-54 | `Child 02 \| Child 03 \| Child 04` | re-inventory: pending = {Child 01}, budget = 0 |
| — | `Failed: bounded revisit coverage INCOMPLETE: discovered=8, resolved=7, unresolved=[Child 01] (bounded budget exhausted; ...)` | coverage gate fail-closed |

`REVISIT_COVERAGE Fixture Root = freshly-exposed=[06,08,04,02,07,05,03]` —
**Child 01 absent** (never CURRENTLY_VISIBLE in any dispatch pass).

## 3. Unresolved Branch Analysis

| branch | category | evidence |
|--------|----------|----------|
| Child 01 | **A — never re-entered the viewport** | OCR text appears ONLY in seq=2 (launch). Absent from every reverse frame (seq 30-48: top-most row sequence 05→04→03→02; the top-of-list frame `Child 01-03` was never re-entered). No fresh-grounding trace, no authorization trace, not in `RevisitCoverage` → it never reached any grounding/dispatch attempt. |
| Child 02 | recovered (dispatched on final budget unit, seq 48→51) | reverse 0.20 at budget=0 exposed it; verified return |
| Child 03-08 | recovered (7 verified returns) | — |
| (B) grounding-entered-but-failed | **no branch** | no fresh-grounding-mismatch or authorization-rejection trace for any branch in this run |
| (C) grounding-ok-but-dispatch/return-failed | **no branch** | all 7 recovered branches completed verified returns; no settle/return failure |

Conclusion: the ONLY unresolved branch is **A**, and its non-resolution is a
**budget-quantity** effect, not a grounding or dispatch defect.

## 4. Recoverability Judgment

**CASE A — the current recovery direction / step strategy is correct; a larger
bounded budget would complete coverage.**

- Direction: reverse = toward the top of the list where Child 01 lives —
  correct (each executed reverse revealed one new row above).
- Step strategy: the 0.40 → 0.20 → 0.10 halving engaged and successfully
  re-grounded 04, 03, 02. On this fixture every executed swipe moved ≈1 row
  (tall rows), so step granularity is not the binding constraint.
- Grounding chain for Child 01 is intact and was proven at discovery:
  signature `Child 01|row` is in the frozen 8-source normalization; the OCR
  channel detected "Child 01" cleanly at seq 2; authorization criterion
  (`Text.StartsWith("Child ")`) passes. When Child 01 re-enters the top
  viewport, the unchanged chain (identity safety → explicit grounding →
  validator → current visibility → fresh grounding → fresh authorization)
  should dispatch it — there is **no evidence** of a B or C obstacle.
- Theoretical completion: **YES with RevisitBudget ≥ 6** (one more 1-row
  reverse from seq 53-54's frame re-enters `Child 01-03`). Empirically the run
  was exactly 1 unit short: budget 5, needed 6.

Caveat (grounded in observed frames, NOT an attribution of this failure): OCR
detection is intermittent for auxiliary text (`Visited` line present at seq 6-7
and 47, dropped at seq 45-46; `Child 08` duplicated at seq 27-29). If a future
top-frame OCR pass were to drop "Child 01", the same budget-quantity gap would
surface as a B-variant; the evidence in THIS run shows Child 01 was cleanly
detected at the top, so no B-path is proven.

## 5. Recommended Next Action (fix phase — NOT performed now)

1. **Budget sufficiency (primary)**: derive RevisitBudget from the coverage
   requirement instead of `discovery observations − 1`. Minimum sufficient
   bound on this evidence: `budget ≥ forward exploration distance` (here 6,
   not 5). Options: `discoveryObs` (drop the −1), or `discoveryObs +
   slack` (covers OCR/overscroll inefficiency), or a coverage-proportional
   bound (`max(discoveredSources, discoveryObs)`).
2. **Progress-based termination (optional, more principled)**: terminate the
   bounded reverse not on a fixed count but on **no-new-source evidence** —
   a reverse that exposes zero new (unresolved) sources proves the viewport
   reached the list boundary; only then report the remaining branches as
   unreachable-with-evidence. This converts "one unit short" into a
   self-proving stop condition.
3. **Re-verify** with the same real-device run + `/tmp/capstone_evidence.txt`
   until `STATE=Completed` / `Visited 8/8 [CAPSTONE COMPLETE]`, and confirm
   the deterministic `AdaptiveRevisitCoverageCompletionTests` still pass
   (their coverage-gap semantics must remain — the gap case must still fail
   closed with evidence, now only after a proven boundary).

## 6. Architecture Impact

| dimension | impact |
|-----------|--------|
| DFS ownership | **NONE** — run loop, epoch freeze, pending ordering, terminal paths untouched |
| Agent authority | **NONE** — Agent remains sole decision authority; no new authority |
| Traversal | **NONE** — no lowering/verification change |
| Semantic capability | **NONE** — fixture role classifier untouched |
| Source grounding | **NONE** — grounding chain untouched; Child 01's grounding was valid at discovery |
| GoalEvidence | **NONE** — the failure is the coverage gate's fail-closed reporting, not GoalEvidence logic |

**ArchitectureDelta: NONE.** The finding is a bounded-budget quantity
shortfall (CASE A) with full evidence; the fix phase (§5) touches only the
budget derivation / revisit termination criterion inside the existing Agent
revisit seam.
