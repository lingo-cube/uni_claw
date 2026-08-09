# S0_BASELINE_READY & Capstone Authorization Review

> Status: Review | Date: 2026-08-09
> Scope: repository-truth review, findings, and recommendation only. This document exercises no authority: it does not declare `S0_BASELINE_READY`, does not authorize Capstone implementation or execution, and creates no OpenSpec change.
> Origin: human-invoked workflow `S0_BASELINE_READY_CAPSTONE_AUTHORIZATION_REVIEW`.

## Prior Run of This Workflow

This workflow has run once before: its result was consumed as `S0_BASELINE_READY_CAPSTONE_AUTHORIZATION_REVIEW_RESULT → EXTRACT_BOUNDED_CANDIDATE`, producing SC-P3-CAND-009 (registered → `PROJECT_LEADER_SEMANTIC_GATE_SC_P3_CAND_009` → HUMAN `ACCEPT_OPTION_C_BOUNDARY` → implemented → frozen 2026-08-09). That was the last capability gap the Capstone mapping exposed; it is now closed.

## Repository Truth Inventory (verified 2026-08-09)

1. **Frozen scenario capabilities — 13**: SC-P1-001, SC-P1-005, SC-P2-001, SC-P2-003, SC-P3-001, SC-P3-002, SC-P3-003, SC-P3-CAND-004, CAND-005, CAND-006, CAND-007, CAND-008, CAND-009. All `PASS` at S0. Evidence per capability: the roadmap §5 matrix carries **11** `FROZEN` rows (SC-P3-CAND-008/009 are frozen by their closeouts but have no matrix rows yet — see F4); every Phase 3 row has a capability closeout; SC-P1-005 is frozen via the matrix/Phase-1 slice (no standalone closeout file exists); the Phase 2 freeze covers SC-P1-001/SC-P2-001/002/003 with `PHASE_2_FROZEN`.
2. **Legacy simulation baseline classification: FROZEN today** — 47 cases, 0 `UNKNOWN`; `S0_LEGACY_CLASSIFICATION_FROZEN`; independently validated (PASS round 2).
3. **Capstone registration: `CANDIDATE` / readiness `PREREQUISITES_MAPPED`** — `docs/system/scenarios/06-s0-capstone-settings-traversal.md`; the 12-row Capability Prerequisites table lists 11 `COVERED` at S0 plus the safe-parent-return/backtracking mechanics note within SC-P3-CAND-004; the document records "a separate Capstone authorization is still required; this document does not start that workflow."
4. **OpenSpec: NO Capstone change exists** — `openspec/changes/` contains 12 changes (greenfield + phase1 + phase2 + 9 phase-3 changes); "Capstone" appears in the tree only as forbidden/deferred scope inside other changes, never as its own change. Every frozen capability was purchased through an OpenSpec change (proposal → design → specs → scenarios → tasks → Semantic Gate); the Capstone has none.
5. **Roadmap §8 S0_BASELINE_READY** (items 1–5) and §11 Next Authority Boundary: `STOP_AT_SC_P3_CAND_007_FROZEN` — **stale** (predates CAND-008/009 closeouts and today's classification freeze).
6. **Working tree**: the CAND-009 production delta is uncommitted (`src/UniClaw.Runtime/Agent/Agent.cs`, `src/UniClaw.Runtime/Model/Goal.cs` modified; `src/UniClaw.Runtime/Model/BranchEffectCriterion.cs` + CAND-009 test files untracked; `tests/UniClaw.Runtime.Tests/Unit/ModelImmutabilityTests.cs` modified); docs/decisions + openspec additions also uncommitted. Capability closeouts were written against exactly these files.

## Findings

### F1 — Capstone integration mapping is complete; no new candidate extraction required at pre-authorization

Every Capstone Required Integration Behavior maps to a frozen capability:

| Capstone requirement (06 doc) | Frozen coverage |
|---|---|
| Normal traversal through approved pages and sibling branches | SC-P1-001; SC-P3-CAND-004 |
| Discover branch candidates from fresh evidence; route not pre-encoded up front | SC-P3-CAND-008 |
| Preserve evidence-backed progress; no double-count; no completion while a branch is unresolved | SC-P3-CAND-004; GoalEvidence |
| Dangerous candidate → never dispatched | SC-P3-CAND-006; static preauthorized safety |
| Exactly one Popup obstruction | SC-P3-002 |
| Exactly one external Launcher drift; re-enter; restore; reconcile | SC-P2-001 + SC-P3-CAND-005 + SC-P3-CAND-009 |
| One bounded forward viewport movement; repeated exploration with honest exhaustion | SC-P3-003 + SC-P3-CAND-007 |
| Completion evidence 1–7 incl. deterministic replay | GoalEvidence + all frozen replay contracts |
| Safe parent return/backtracking | Mechanics within SC-P3-CAND-004; no generic Back/graph/stack purchase |
| S0 world boundary (Fake may not encode Container identity/Recovery authority/progress/Goal success) | Capstone Fake-world construction rule; no production purchase |
| Legacy evidence boundary | Legacy classification (frozen) |

The prior run's last gap (CAND-009: discovered-branch post-Recovery revalidation) is frozen. The Capstone is purely integrative: `CAPSTONE integrates frozen capabilities and does not directly purchase new production semantics` (06 doc).

### F2 — S0_BASELINE_READY items 1–5 are collectively satisfied

1. high-value legacy corpus classified — DONE (2026-08-09, 47 cases);
2. no high-value evidence remains `UNKNOWN` — DONE (0 `UNKNOWN`);
3. core Runtime boundaries have deterministic Scenario pressure — DONE (classification mapping: every pressure maps to a frozen Scenario or the registered Capstone; 13 frozen capabilities — 11 roadmap matrix `FROZEN` rows plus CAND-008/009 by closeout);
4. S0 simulation remains external-world-only — DONE (classification Non-Normative Mechanisms; Capstone S0 World Boundary);
5. key positive, negative/disturbance, and replay evidence exists — DONE (classification evidence inventory).

The classification closeout deferred the determination ("the remaining items are the Capstone registration/authorization boundary and any gate decisions"). This review now finds items 1–5 met: **the only remaining authority is the human gate decision that declares the status and authorizes the Capstone path.** Declaring `S0_BASELINE_READY` is a roadmap status change, not a finding — it requires the gate below.

### F3 — The missing authorization surface: the Capstone has no OpenSpec purchase

Per the shared protocol Scenario-First principle ("任何 production capability 必须由 Active Scenario 购买"), the Capstone's integration run must be purchased by an Active Scenario. Currently:

- the 06 doc is a registration, explicitly "not an OpenSpec purchase";
- no `openspec/changes/*` exists for the Capstone → no proposal/design/specs/scenarios/tasks → no Semantic Gate → no implementation or execution authority;
- the Capstone readiness sequence stops at `PREREQUISITES_MAPPED` with no path to `READY_FOR_S0_RUN` until that purchase exists.

The purchase should be an integration Scenario with **zero production model delta** (budget 0 types/fields/enums/interfaces/components/mutable state), carrying: the Fake-world construction contract (S0 World Boundary), the disturbance schedule (popup + drift), the completion-evidence-7 contract, the deterministic replay contract, and the self-gating clause already in the 06 doc ("if execution exposes a new Reality Distinction → stop → extract one bounded Candidate → Semantic Gate → prove/freeze → return").

### F4 — Doc rot (stale repository truth)

- Roadmap §11 `STOP_AT_SC_P3_CAND_007_FROZEN` — predates CAND-008/009 and today's classification freeze; the next-authority boundary line is obsolete.
- Roadmap §5 matrix has no rows for SC-P3-CAND-008/009 (frozen by closeout only) — the §11 boundary refresh should carry both rows.
- AGENTS.md "当前阶段: Phase 0 完成 … → Phase 1 Deterministic Runtime（…待审批实施）" — stale; the branch is at Phase 3 with 13 frozen capabilities.
- Roadmap matrix Capstone row: "Dependency / prerequisite: Independent legacy-baseline classification" — now satisfied; may cite the frozen closeout.

### F5 — Working-tree hygiene (non-blocking)

CAND-009's production delta and the closeout docs are uncommitted. Capability status is unaffected (closeouts and validation were produced against these exact files); the commit is a repository-hygiene decision, not an authorization.

## Recommendation

**R1. Declare `S0_BASELINE_READY`** — items 1–5 are met; the declaration is a Human Gate decision (roadmap status change), to be recorded in the roadmap §8 status line.
**R2. Authorize the Capstone OpenSpec change** — create `openspec/changes/phase3-s0-capstone-settings-traversal/` (proposal/design/specs/scenarios/tasks) with the zero-production-purchase integration contract above, then run the HUMAN Semantic Gate (`PROJECT_LEADER_SEMANTIC_GATE_SC_S0_CAPSTONE_001`), decision `AUTHORIZE_CAPSTONE_INTEGRATION`.
**R3. After gate approval** — dispatch implementation (integration run + deterministic replay) → independent runtime-validator acceptance → `READY_FOR_S0_RUN` → execution → `S0_GRADUATED` (per 06 doc readiness sequence `REGISTERED → DECOMPOSED → PREREQUISITES_MAPPED → READY_FOR_S0_RUN → S0_GRADUATED`).
**R4. Doc-rot cleanup** — refresh roadmap §11 boundary, AGENTS.md phase statements, and the matrix dependency column as part of the gate decision (no authority needed for factual syncs).

## Next Authority

```text
PROJECT_LEADER_HUMAN_GATE_S0_BASELINE_READY_AND_CAPSTONE
```

Proposed decision options for the gate:

- `ACCEPT_S0_BASELINE_READY_AUTHORIZE_CAPSTONE_OPENSPEC` — declare readiness + authorize the Capstone OpenSpec change (then its Semantic Gate);
- `ACCEPT_S0_BASELINE_READY_HOLD_CAPSTONE` — declare readiness only; Capstone purchase deferred;
- `HOLD_BOTH` — keep status `PREREQUISITES_MAPPED`; review findings only.

## STOP

Review complete. This document claims no status and authorizes nothing. STOP after review; the gate decision is the human's.
