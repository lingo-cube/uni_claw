# PHASE_C_PRESSURE_REALITY_MATRIX_RESULT

> Generated: 2026-08-09
> Role: Reality Governance Architect — PHASE_C classification
> Inputs: Unified 14-CP portfolio · Accepted RM-01..RM-09 corpus · S0 graduation artifacts · Architecture invariants
> Authority: `HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT` (2026-08-09)
> Roadmap: `docs/system/post-s0-reality-grounded-usability-roadmap.md` §PHASE_C

---

## Classification Methodology

Each CP classified on four axes:

| Axis | Source | Values |
|---|---|---|
| **Reality Coverage** | Accepted RM corpus (RM-01..RM-09) | COVERED / EMBEDDED / GAP |
| **S0 Runtime Capability** | S0 graduation (415/415, architecture guards 8/8, frozen invariants) | PROVEN / FROZEN / DEFERRED |
| **Evidence Maturity** | B4 deferred register + portfolio evidence grades | S0 / S1-replay-needed / S2-perception / S3-live |
| **Classification** | Contract + roadmap phase classification vocabulary | FOUNDATION_COMPLETE / EVIDENCE_UPGRADE_READY / CHALLENGE_REQUIRED / FUTURE_CAPABILITY / USABILITY_BLOCKER |

---

## Pressure × Reality × Capability Matrix

### CP-01 — Entry Must Verify Foreground App Before Traversal

| Axis | Value |
|---|---|
| **Domain** | World Truth / Action Effect |
| **Primary RD** | RD-01 (ActionExecution != ActionEffect) |
| **Reality Model** | `RM-04` — Entry Verification Before World Interaction (ACCEPTED, CONDITIONAL_PASS) |
| **S0 Runtime** | FROZEN_CAPABILITY_COVERED — `Startup.cs` foreground verify; `Agent.cs` pre-loop | PROVEN |
| **Evidence Maturity** | S0 (deterministic + E4 EP-03 manifest) with 1 deferred WF at E0 (entry fake-success path) |
| **Evidence Gap** | WF-14 (entry action fake success) at E0 — DEF-01 in deferred register. No committed reproduction of a real entry-verification failure. |
| **Upgrade Path** | S1 replay of entry-failure scenario |
| **Classification** | **EVIDENCE_UPGRADE_READY** — RM accepted, Runtime proven, E0 upgrade pending |

---

### CP-02 — Navigation Must Verify Observable Page Change

| Axis | Value |
|---|---|
| **Domain** | World Truth / Action Effect |
| **Primary RD** | RD-01 (ActionExecution != ActionEffect) |
| **Reality Model** | `RM-05` — Navigation Action Effect Observable as Page Change (ACCEPTED, PASS) |
| **S0 Runtime** | FROZEN_CAPABILITY_COVERED — `IsStillMine`, `Observe→Verify`, stale-click fuse 3× | PROVEN |
| **Evidence Maturity** | S0 (E4 EP-03 success/failure) + E1 (E-09 L4 circuit breaker) + E0 (VE-09 false-success) |
| **Evidence Gap** | VE-09 (20% byte-length false success) at E0 — historical, not reproduced. Stale-click circuit breaker is proven (E-09 L4) but only in simulation. |
| **Upgrade Path** | S1 replay of a real stale-navigation event |
| **Classification** | **FOUNDATION_COMPLETE** — RM accepted (PASS), Runtime proven, evidence E4-anchored |

---

### CP-03 — Plan Validity Must Not Imply Execution Success

| Axis | Value |
|---|---|
| **Domain** | World Truth / Action Effect |
| **Primary RD** | RD-11 (PlanConstructed != ExecutionGuaranteed) |
| **Reality Model** | **EMBEDDED** — cross-cutting principle in RM-02 (hub completion), RM-05 (page-change verification), RM-06 (depth enforcement). No standalone RM. |
| **S0 Runtime** | FROZEN_CAPABILITY_COVERED — Architecture Invariants I-5 (Plan ≠ reality), I-10 (Completion requires Goal Evidence) | PROVEN |
| **Evidence Maturity** | S0 — embedded in multiple models at E4/E3/E1 |
| **Evidence Gap** | None — the principle is a meta-constraint proven by the existence of the other RMs' fail oracles |
| **Upgrade Path** | N/A — principle proven at S0 |
| **Classification** | **FOUNDATION_COMPLETE** — cross-cutting principle, proven by embedded models |

---

### CP-04 — Multi-Branch Hub Must Not Report Complete With Unvisited Branch

| Axis | Value |
|---|---|
| **Domain** | Completion / Progress |
| **Primary RD** | RD-02 (WorkDispatched != WorkCompleted) |
| **Reality Model** | `RM-02` — Multi-Branch Hub with Independent Subtrees (ACCEPTED, PASS) |
| **S0 Runtime** | FROZEN_CAPABILITY_COVERED — `BranchProgressEvidence`, `ApprovedSiblingEvidence`, CAND-004/005/009, Capstone assertions 6/7/8 | PROVEN |
| **Evidence Maturity** | S0 (E1 deterministic simulation). E-07 is the strongest false-completion evidence — **unfixed bug**, deterministic, reproducible without scroll. |
| **Evidence Gap** | No E4/E3 evidence of a real multi-branch false-completion event. E-07 is deterministic simulation (E1). The bug is UNFIXED — the Runtime's capability is declared FROZEN but the simulation test FAILS. |
| **Upgrade Path** | S1 replay of a real multi-branch run. Fix the unfixed E-07 bug (this is a behavior gap, not an evidence gap — the capability is declared covered but the test fails). |
| **Classification** | **EVIDENCE_UPGRADE_READY** — RM accepted (PASS), Runtime declared covered but E-07 test FAILS. High priority for S1 because the evidence gap is also a behavior gap. |

---

### CP-05 — Revisiting a Page Must Not Reset Exploration State

| Axis | Value |
|---|---|
| **Domain** | Completion / Progress |
| **Primary RD** | RD-09 (PreviouslyVisited != Unexplored) |
| **Reality Model** | **EMBEDDED** — via RM-02 ER-07 (branch revisit idempotence). No standalone RM. |
| **S0 Runtime** | FROZEN_CAPABILITY_COVERED — idempotence in BranchProgressEvidence, Capstone assertion 7 | PROVEN |
| **Evidence Maturity** | S0 — embedded in RM-02 at E1 |
| **Evidence Gap** | None — idempotence is a property of the branch-progress mechanism |
| **Upgrade Path** | N/A |
| **Classification** | **FOUNDATION_COMPLETE** — embedded principle, Runtime proven |

---

### CP-06 — Goal Satisfaction Must Be Recognizable Without Execution

| Axis | Value |
|---|---|
| **Domain** | Completion / Progress |
| **Primary RD** | RD-10 (GoalExpression != GoalState) |
| **Reality Model** | `RM-03` — Goal Satisfaction Recognizable from Current Observation (ACCEPTED, PASS) |
| **S0 Runtime** | **FULLY_CLOSED** — plan-length-independent initial GoalEvidence proven (Assertion6–9, 415/415 pass, production `Agent.cs` unconditional pre-loop evaluation) | PROVEN |
| **Evidence Maturity** | S0 — all 3 WFs DIRECT from executable proofs. Strongest-validated model in corpus. |
| **Evidence Gap** | None. The proof is non-vacuous for both empty and non-empty plans. The only upgrade path is E4 (real-device run) but not required — the deterministic proof suffices. |
| **Upgrade Path** | Optional S3 live-device run (not required for evidence maturity) |
| **Classification** | **FOUNDATION_COMPLETE** — RM accepted (PASS, strongest in corpus), FULLY_CLOSED in Phase A, no evidence gap |

---

### CP-07 — Declared Depth Bound Must Be Enforced During Discovery

| Axis | Value |
|---|---|
| **Domain** | Constraints / Boundaries |
| **Primary RD** | RD-03 (ConstraintDeclared != ConstraintEnforced) |
| **Reality Model** | `RM-06` — Depth Bound Declared Separately from Discovery (ACCEPTED, PASS) |
| **S0 Runtime** | FROZEN_CAPABILITY_COVERED — CAND-008 depth bound, `MaxSubframeDepth`, `leaf_info` degradation at depth≥MaxDepth+1 | PROVEN |
| **Evidence Maturity** | S0 (E3 replay + E1 permanent regression + E1 FixVerificationTests L2/L3/L7) |
| **Evidence Gap** | None at S0. E-11 is a permanent regression — depth bound enforcement is regression-guarded. |
| **Upgrade Path** | S1 replay (already partially done via E-08 TraceReplay) |
| **Classification** | **FOUNDATION_COMPLETE** — RM accepted (PASS), Runtime proven, regression-guarded |

---

### CP-08 — Observation Failure Must Not Become Content Exhaustion

| Axis | Value |
|---|---|
| **Domain** | Constraints / Boundaries |
| **Primary RD** | RD-04 (ObservationFailed != ContentExhausted) |
| **Reality Model** | `RM-07` — Observation Failure Distinct from Content Exhaustion (ACCEPTED, CONDITIONAL_PASS) |
| **S0 Runtime** | COVERED_BUT_REPLAY_EVIDENCE_NEEDED — tri-state `ViewportExplorationEvidence` exists but the production `scroll_roi_end_reached` signal is ignored by verifier (VE-10) | PARTIAL |
| **Evidence Maturity** | S0 (E1 deterministic simulation for E-12). **2 deferred WFs at E0/E1** (DEF-02, DEF-03) — highest-priority deferred evidence. |
| **Evidence Gap** | **SIGNIFICANT.** No E4/E3 evidence of a real observation-failure→exhaustion-conflation event. The E-13 documentation (EntryPolicy fake success, ADB failure→IsEnd=true) is E0. The production code gap (VE-10: ROI signal ignored) is E1 but the verifier bug is real. This is the portfolio's highest-value S1 replay target. |
| **Upgrade Path** | **S1 replay — HIGHEST PRIORITY.** Portfolio explicitly flags CP-08 as "highest-value S1 upgrade." |
| **Classification** | **EVIDENCE_UPGRADE_READY — PRIORITY HIGH** |

---

### CP-09 — Unchanging Content Must Not Loop Forever

| Axis | Value |
|---|---|
| **Domain** | Constraints / Boundaries |
| **Primary RD** | RD-04 (ObservationFailed != ContentExhausted) |
| **Reality Model** | **EMBEDDED** — via RM-07 (same Primary RD). E-12 Pattern 1 (content stability K=3 → false AllVisited) is the evidence. No standalone RM. |
| **S0 Runtime** | FROZEN_CAPABILITY_COVERED | PROVEN |
| **Evidence Maturity** | S0 — embedded in RM-07 at E1 |
| **Evidence Gap** | No E4 evidence of a real scroll-loop event |
| **Upgrade Path** | S1 replay |
| **Classification** | **FOUNDATION_COMPLETE** — embedded principle, Runtime proven, E1 evidence |

---

### CP-10 — Recovery Attempt Must Not Imply Error Resolution

| Axis | Value |
|---|---|
| **Domain** | Recovery / Error |
| **Primary RD** | RD-06 (RecoveryAction != ErrorStateReset) |
| **Reality Model** | `RM-08` — Recovery Action Effect Distinct from Error Resolution (ACCEPTED, PASS) |
| **S0 Runtime** | FROZEN_CAPABILITY_COVERED — `RecoveryResult.Verified|Failed`, I-9 (Recovery must be verified) | PROVEN |
| **Evidence Maturity** | S0 (E1 deterministic simulation, historical Bug #2, post-CP-06 AgentRecoveryTests with honest probe Goals) |
| **Evidence Gap** | No E4 recovery trace. Bug #2 (consecutive errors accumulate) is simulation-only. |
| **Upgrade Path** | S1 replay of a real recovery event with post-recovery verification |
| **Classification** | **FOUNDATION_COMPLETE** — RM accepted (PASS), Runtime proven |

---

### CP-11 — Element Visibility Must Not Imply Navigability

| Axis | Value |
|---|---|
| **Domain** | Perception / Navigability |
| **Primary RD** | RD-05 (ElementPresence != ElementNavigability) |
| **Reality Model** | `RM-09` — Element Visibility and Type Classification Distinct from Navigability (ACCEPTED, CONDITIONAL_PASS) |
| **S0 Runtime** | FROZEN_CAPABILITY_COVERED — `CandidateAuthorizationEvidence`, `dangerousSemantics` safety policy | PROVEN |
| **Evidence Maturity** | S0 (E3 VE-06 recorded-reality-derived + E1 VE-05/VE-07/VE-03/VE-04). Richest evidence corpus. |
| **Evidence Gap** | The chevron heuristic root cause (WF-28, RI-14) is precisely diagnosed (`fusion.py:292-343`) with a reproduction test (FixVerificationTests L8). RI-17 (empty OCR) is a non-essential edge case. No S2 production-perception evidence (the evidence is from the legacy vision pipeline, not from a production-shaped perception system). |
| **Upgrade Path** | S2 production-shaped perception (CP-11 is the portal to S2 — perception errors are the domain) |
| **Classification** | **FOUNDATION_COMPLETE** — RM accepted, Runtime proven, evidence E3-anchored. S2 upgrade is a perception pipeline concern (Phase D CP-12 challenge adjacent). |

---

### CP-12 — Target Grounding Must Verify Semantic Identity Beyond Coordinate/Text Match

| Axis | Value |
|---|---|
| **Domain** | Perception / Grounding |
| **Primary RD** | VRD-03 (Coordinate/Text Match != Semantic Target Identity) |
| **Reality Model** | **GAP** — No standalone RM. RM-09 covers CP-12 as a Secondary CP via ER-24 (semantic text matching) and RI-16 (substring ≠ identity), but this is perception-level grounding, not a full target-grounding model. |
| **S0 Runtime** | **CHALLENGE_REQUIRED** — the one genuinely new canonical pressure from the unified portfolio. Assessed as a gap in the current capability. | **NOT COVERED** |
| **Evidence Maturity** | S0 (VE-07 substring overmatch at E1, VE-02 coordinate-only tap at E2, VE-01 golden matching at E2). Evidence exists for the FAILURE MODE but no model describes the POSITIVE behavior (what SHOULD happen). |
| **Evidence Gap** | **CRITICAL.** No evidence of a system correctly grounding a target through semantic identity verification. VE-07 documents the failure (substring overmatch), VE-02 documents the risk (coordinate-only tap without post-tap verification). The POSITIVE path — "system correctly identifies target element by semantic identity, not coordinate/text match" — has no evidence in the corpus. |
| **Upgrade Path** | **Phase D CP-12 Challenge.** Requires: (a) define what "semantic target identity verification" means as observable behavior; (b) produce evidence of correct grounding (E4/E3); (c) admit a CP-12 Reality Model; (d) validate; (e) candidate generation (Phase D). |
| **Classification** | **CHALLENGE_REQUIRED — Phase D** |

---

### CP-13 — Raw Page Evidence Must Not Be Conflated With Semantic Page Identity

| Axis | Value |
|---|---|
| **Domain** | Page / Container Identity |
| **Primary RD** | RD-08 (RawPageEvidence != SemanticPageIdentity) |
| **Reality Model** | `RM-01` — Android Device Screen as Page Inventory (ACCEPTED, CONDITIONAL_PASS) |
| **S0 Runtime** | FROZEN_CAPABILITY_COVERED — `SemanticPageName`, `IsStillMine`, Container identity verification | PROVEN |
| **Evidence Maturity** | S0 (E4 EP-03 trace.jsonl + EP-04 sim-replay, E3, E2, E1). ER-04 marked DERIVED. |
| **Evidence Gap** | RM-01's core claims (WF-01..WF-05) are well-evidenced at E4/E3. ER-04 (source attribution) is a derived requirement. No critical evidence gap. |
| **Upgrade Path** | S1 replay + S2 perception (page identity in production-shaped perception) |
| **Classification** | **FOUNDATION_COMPLETE** — RM accepted, Runtime proven, E4-anchored |

---

### CP-14 — Task Intent Must Not Be Conflated With Execution Method

| Axis | Value |
|---|---|
| **Domain** | Intent / Plan |
| **Primary RD** | RD-07 (TaskIntent != ExecutionMethod) |
| **Reality Model** | **GAP** — No standalone RM. RM-03 covers CP-14 as a Secondary CP (ER-10: Plan length doesn't gate GoalEvidence authority). The portfolio explicitly defers CP-14 to Phase 5/6 (Intent→Goal/Plan synthesis). |
| **S0 Runtime** | EXPLICITLY_DEFERRED_CAPABILITY — the portfolio states "This capability is explicitly deferred to later development phases (Phase 5 Intent Slot Unification / Phase 6 Global Policy-Native Goal Integration)." | **DEFERRED** |
| **Evidence Maturity** | S0 (partial — E-05 IntentExtractor stub, E-14 PlanCompiler, E-15 IntentExtractor, E-16 ScenarioPlanLoader). Evidence exists for the CURRENT two-mode system (closed-world concrete plan vs open-world type-level spec) but not for the FUTURE unified Intent→Goal/Plan synthesis. |
| **Evidence Gap** | This is a product-frontier gap, not an evidence gap. The capability does not exist. The evidence describes the current two-mode system, not the desired unified one. |
| **Upgrade Path** | Phase 5/6 — Intent Slot Unification + Global Policy-Native Goal Integration. **Not a Phase C concern.** |
| **Classification** | **FUTURE_CAPABILITY — Phase 5/6** |

---

## Summary Matrix

| CP | Domain | RM | Reality Coverage | S0 Runtime | Evidence Maturity | Classification |
|---|---|---|---|---|---|---|
| CP-01 | World Truth / Action Effect | RM-04 | COVERED (COND) | PROVEN | S0 (1 E0 WF) | EVIDENCE_UPGRADE_READY |
| CP-02 | World Truth / Action Effect | RM-05 | COVERED (PASS) | PROVEN | S0 (E4-anchored) | FOUNDATION_COMPLETE |
| CP-03 | World Truth / Action Effect | — | EMBEDDED | PROVEN | S0 | FOUNDATION_COMPLETE |
| CP-04 | Completion / Progress | RM-02 | COVERED (PASS) | PROVEN† | S0 (E1, unfixed bug) | EVIDENCE_UPGRADE_READY |
| CP-05 | Completion / Progress | — | EMBEDDED | PROVEN | S0 | FOUNDATION_COMPLETE |
| CP-06 | Completion / Progress | RM-03 | COVERED (PASS) | PROVEN (FULLY_CLOSED) | S0 (DIRECT proofs) | FOUNDATION_COMPLETE |
| CP-07 | Constraints / Boundaries | RM-06 | COVERED (PASS) | PROVEN | S0 (E3-anchored) | FOUNDATION_COMPLETE |
| CP-08 | Constraints / Boundaries | RM-07 | COVERED (COND) | PARTIAL | S0 (2 E0 WFs) | **EVIDENCE_UPGRADE_READY — HIGH** |
| CP-09 | Constraints / Boundaries | — | EMBEDDED | PROVEN | S0 | FOUNDATION_COMPLETE |
| CP-10 | Recovery / Error | RM-08 | COVERED (PASS) | PROVEN | S0 (E1) | FOUNDATION_COMPLETE |
| CP-11 | Perception / Navigability | RM-09 | COVERED (COND) | PROVEN | S0 (E3-anchored) | FOUNDATION_COMPLETE |
| CP-12 | Perception / Grounding | — | **GAP** | **NOT COVERED** | S0 (failure only) | **CHALLENGE_REQUIRED** |
| CP-13 | Page / Container Identity | RM-01 | COVERED (COND) | PROVEN | S0 (E4-anchored) | FOUNDATION_COMPLETE |
| CP-14 | Intent / Plan | — | **GAP** | **DEFERRED** | S0 (current mode only) | **FUTURE_CAPABILITY** |

† CP-04 Runtime is declared FROZEN_CAPABILITY_COVERED but E-07 test FAILS — behavior gap, not just evidence gap.

---

## Classification Summary

| Classification | Count | CPs |
|---|---|---|
| **FOUNDATION_COMPLETE** | 8 | CP-02, CP-03, CP-05, CP-06, CP-07, CP-09, CP-10, CP-11, CP-13 |
| **EVIDENCE_UPGRADE_READY** | 3 | CP-01 (E0 WF), CP-04 (E1 + unfixed bug), CP-08 (**HIGH** — 2 E0 WFs, highest-value S1 target) |
| **CHALLENGE_REQUIRED** | 1 | CP-12 (Phase D — target grounding) |
| **FUTURE_CAPABILITY** | 1 | CP-14 (Phase 5/6 — Intent→Goal/Plan synthesis) |

---

## Usability Blocker Assessment

Per roadmap §2: "Canonical Pressure + Accepted Reality Model + Minimum Usable Vertical Slice → development."

| Blocker | CP | Severity | Rationale |
|---|---|---|---|
| **Target grounding** | CP-12 | **CRITICAL — U1 blocker** | Without target grounding verification, the system cannot reliably tap the correct element on a real device. This blocks the U1 slice ("确保 WiFi 已开启" end-to-end on emulator). CP-12 is the ONE genuinely new canonical pressure. Phase D challenge required. |
| **Observation failure vs exhaustion** | CP-08 | **HIGH — U1 risk** | On a real emulator, ADB failures, vision timeouts, and scroll-end ambiguity will occur. Without distinguishing failure from exhaustion, the system will falsely report "end of list" on observation failures. DEF-02/DEF-03 are highest-priority S1 targets. |
| **Multi-branch false completion** | CP-04 | **MEDIUM — U2 blocker** | The unfixed E-07 bug (AllVisited with unvisited branch) will cause false completion in open-world Settings traversal (U2). Less urgent for U1 (single-target locate). |
| **Intent→Goal/Plan synthesis** | CP-14 | **FUTURE — U3 blocker** | The deferred capability is a product-frontier concern. Not blocking U1 or U2. |

**U1 path:** CP-12 challenge (Phase D) is the critical path item. CP-08 S1 replay is the highest-value evidence upgrade. Both are pre-U1.

---

## S1 Replay Portfolio (Prioritized)

Per roadmap S1 definition: "Recorded legacy/emulator/real-world evidence replaying the same reality pressure."

| Priority | CP | RM | Evidence Gap | Replay Target |
|---|---|---|---|---|
| **P0** | CP-08 | RM-07 | DEF-02, DEF-03: observation failure→exhaustion conflation (E0→E3+) | Real run where ADB/vision failure produced false `IsEnd=true` or false `AllVisited` |
| **P1** | CP-04 | RM-02 | E-07 unfixed bug (E1→E3) | Real multi-branch run replay proving hub false-completion |
| **P2** | CP-01 | RM-04 | DEF-01: entry fake success (E0→E3+) | Real run where EntryPolicy reported success but foreground app was wrong |
| **P3** | CP-02 | RM-05 | VE-09 stale-navigation false-success (E0→E3) | Real run with byte-change but no page-change |
| **P4** | CP-10 | RM-08 | Recovery trace (E1→E3) | Real run with recovery→verification cycle |

---

## Phase D / Phase 5/6 Boundaries

### Phase D: CP-12 Target Grounding Challenge

**Entry condition:** Phase C complete. CP-12 identified as CHALLENGE_REQUIRED.

**Scope:** Define semantic target identity verification as observable behavior. Produce positive evidence (E4/E3) of correct grounding. Admit a CP-12 Reality Model. If the challenge reveals a Runtime semantic gap, trigger the Semantic Gate for candidate generation.

**Explicitly NOT in Phase D:** Architecture design, implementation, S2/S3 execution, U1 execution.

### Phase 5/6: CP-14 Intent→Goal/Plan Synthesis

**Entry condition:** U1 complete. CP-14 identified as FUTURE_CAPABILITY. Intent semantics mature enough to justify unification.

**Scope:** Unify the two-mode system (closed-world concrete plan + open-world type-level spec) into a single Intent→Goal→Plan pipeline. This is a product-frontier capability, not an evidence-upgrade or challenge item.

---

## Next Task

**PHASE_D_CP12_TARGET_GROUNDING_CHALLENGE** — the one genuinely new canonical pressure, the critical path item for U1, and the only CHALLENGE_REQUIRED classification. Address CP-12 before any S1/S2/S3 work.

Alternatively: **S1_REPLAY_PORTFOLIO** — if the deferred evidence register (DEF-01..DEF-03) is prioritized first, execute P0 (CP-08 observation failure) as the highest-value S1 replay target.

## Repository Changes

`docs/decisions/phase-c-pressure-reality-matrix-result.md` — created. No other files modified.

STOP.
