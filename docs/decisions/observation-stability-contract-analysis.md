# Observation Stability Contract Analysis

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_OBSERVATION_STABILITY_CONTRACT_ANALYSIS — analyze whether
> the Runtime's Observation acceptance / stability contract has a gap, based on
> the ExternalBoundary real-device failure. **Analysis only — no code changed**;
> the ad-hoc fresh-bounds dispatch re-observe from the previous iteration was
> REVERTED (U2OpenWorld suites re-verified green, 22/22) pending this
> higher-level stability-owner decision.
>
> Constraints honored: no OCR/Vision/transform changes; no ADB/XML bounds
> correction; no SourceGroundingNormalizer / Semantic Capability changes; no
> Settings rules; no temporary waits for the EBD test.

---

## Phase 1 — Evidence Collection

### Timeline (EBD real-device, `/tmp/ebd_real_evidence.txt`, `ebd_obs_*.xml`)

| stage | artifact | evidence |
|-------|----------|----------|
| scroll 前 observation | accepted frame N | rows at rest positions (OCR-vs-settled offset ≈ 0) |
| scroll 后 raw observation | screenshot captured right after the scroll action | mid-settle positions (offset grows with scroll depth) |
| accepted observation | same raw frame accepted | bounds-valid + same-Container continuity |
| grounding / decision observation | the accepted frame | branch grounding resolved on it |
| dispatch observation | the accepted frame | tap bounds from it |
| settle / reobserve trace | `SettlePostScrollEvidenceQualityAsync` | NO re-observe fired (bounds were valid) — no stability check exists |
| viewport signature changes | per-frame navigation signatures | new rows appended; ordered overlap passes after OCR-only evidence assembly |
| bounds 变化时间线 | OCR vs settled (uiautomator) | offset: rest ≈ 0 → +0.04 (seq3) → +0.07 (seq5) → +0.10-0.11 (seq6); tap at 0.661 hit "Safety & emergency" |

### Current acceptance conditions (code-verified)

| condition | where | checks |
|-----------|-------|--------|
| viewport exploration | caller evaluator | continue while NEW navigation signatures appear |
| post-scroll evidence quality | `SettlePostScrollEvidenceQualityAsync` | re-observe only when an interaction-relevant affordance has NULL bounds (malformed capture) — **no motion/settling check** |
| same-Container continuity | `TryVerifyViewportContinuity` | semantic page unchanged |
| normalization | NORM4 at completeness | ordered suffix/prefix overlap across ACCEPTED frames (validates internal consistency, NOT that the last frame is settled) |
| **time / stability** | **MISSING** | **no condition that the screen stopped moving before a frame is accepted as the decision frame** |
| bounds stability | **MISSING** | bounds of the decision frame are not re-confirmed before dispatch |

## Required Answers

1. **是否区分 Raw vs Stable/Accepted Observation?** — Partially. There IS an
   acceptance chain (raw → settle(quality) → continuity → accept), but the
   acceptance predicate treats "evidence-quality-valid + same-Container" as
   "stable". There is NO Stable-Observation state and NO stability criterion.
2. **当前 acceptance 条件?** — time: none; frame: bounds-valid + same-container;
   viewport signature: exploration-new-source only; occurrence overlap: NORM4 at
   completeness; bounds stability: none; container identity: continuity check.
3. **哪一步错误地认为 observation 已稳定?** — the post-scroll acceptance path
   (`SettlePostScrollEvidenceQualityAsync` → continuity → `AcceptFreshObservation`):
   it promotes a mid-settle screenshot to the accepted decision frame based only
   on evidence quality and container continuity.

## Phase 2 — Failure Classification

**A — Observation acceptance 缺少稳定性判断** (primary). The acceptance
predicate has no scroll-stability (motion-stop) criterion, so a mid-settle
frame becomes the decision frame; its bounds are stale by execution time.

- **B — Environment acquisition timing** (contributing reality): the screenshot
  is captured while the screen is still moving after a scroll; a stability
  acceptance is exactly what must compensate — B is the input condition, A is
  the missing mechanism.
- **C — Agent dispatch 使用过期 evidence**: consequence of A (the frame was
  wrongly accepted as stable), not the root cause.
- **D — Traversal execution timing**: NO evidence.
- **E — Vision perception**: NO (detection/transform verified correct at rest).

## Phase 3 — Ownership Analysis

**Owner: Agent — the exploration / observation-acceptance seam**
(`ExploreCurrentContainerViewportsAsync` + `SettlePostScrollEvidenceQualityAsync`
are Agent-owned). The Environment provides raw frames; whether a frame counts as
the STABLE decision frame is an acceptance-policy decision that lives in the
Agent. The fix belongs there (a stability confirmation in the acceptance
predicate), NOT in the Environment, Traversal, or Semantic Capability.

Confirmed unchanged: Agent authority, DFS ownership, Traversal ownership,
GoalEvidence ownership, Vision-first contract, ADB auxiliary-only contract.

---

## Phase 4 — Architecture Impact

```
AuthorityDelta:   NONE   (analysis only)
ArchitectureDelta: NONE  (analysis only)
```

**Would a fix need a new cross-layer contract?** NO. The stability judgment is
fully observable within the Agent's existing acceptance seam: a bounded
confirmation re-observe whose viewport signature (and/or occurrence bounds)
match the previous frame proves the screen has stopped changing. This is an
ADDITIVE acceptance-policy extension inside the Agent — no Environment
contract change, no ownership change, no new state owner. A formal "Stable
Observation" state (e.g., a field marking the accepted frame as
stability-confirmed) is a plausible small addition if desired, but it is an
Agent-internal acceptance artifact, not a cross-layer contract.

## Phase 5 — Decision Boundary

**Class 1 — current-layer mechanism gap → can proceed to a minimal fix task.**

The gap is an incomplete acceptance criterion in the Agent's observation
acceptance mechanism (no stability condition), not a cross-layer contract
absence, not a test-only assumption (the test exposed a genuine Runtime
acceptance gap). Recommended next step: a scoped implementation task that adds
a BOUNDED stability confirmation to the post-scroll acceptance predicate
(second observation with identical navigation-signature set → accept as
STABLE; budget exhausted → existing fail-closed semantics), plus deterministic
coverage via a motion-physics EvidenceFixture (frame positions shift between
observations until stability), then EBD/Capstone real-device re-verification.

**Fixture fragility note (test side, class 3):** the U2OpenWorld scripted
fixtures key drift injections to observation SEQUENCE NUMBERS
(e.g. `staleSequences = { [3] = 2 }`); any legitimate observation-cadence
change (stability confirmation OR the reverted fresh-bounds re-observe) shifts
those numbers and breaks the fixtures. They should be re-keyed to events
(actions/screens) rather than sequence numbers, independent of whichever
stability fix is chosen.
