# Phase 3 Aggregate Lifecycle Archive Review — Governance Receipt

> Status: CLOSED | Decision: `ARCHIVE_PHASE3_AGGREGATE` | Date: 2026-08-16
> MODE: `AGGREGATE_GRADUATION_EVIDENCE_LIFECYCLE_REVIEW` (PROJECT_LEADER_PHASE3_AGGREGATE_LIFECYCLE_ARCHIVE_REVIEW)
> Scope: the 10 active `phase3-*` OpenSpec changes only. No production change, no test change, no spec edit, no new graduation record, and no new buyer selection was made in this review.
> Relation to lifecycle sequence: Phase3 聚合归档审查 → Phase1 reconcile → Phase1 毕业 → Phase2 毕业 → U2 毕业。本 gate 之后不选新 buyer。

## 1. Review Question

Do the 10 active Phase 3 changes each carry closeout evidence precise enough to archive them?

Archive rule applied per change (no all-or-nothing):

- **EXPLICIT directory naming** in the closeout authority line is required; vague "Phase 3 passed" references are not acceptable.
- Only **FULL_SCOPE_COVERED** changes are archived. PARTIAL_SCOPE_ONLY / INCONSISTENT / NOT_PROVEN changes stay active.
- Historical graduation is NOT fabricated: the 10 capability closeouts are the historical graduation evidence; no ten new graduation records are created.
- MASS_ARCHIVE is forbidden: each change is judged independently against its own spec requirement set.

## 2. Method

For each of the 10 active `phase3-*` changes:

1. Extract the change's own spec requirement set (`## ADDED Requirements` / `### Requirement` in `openspec/changes/<name>/specs/*/spec.md`).
2. Read the named capability closeout (`docs/decisions/phase3-sc-*-capability-closeout.md`): Capability title, Proven Behavior, Production Delta, Acceptance Receipt, State.
3. Verify the closeout authority line **explicitly names the change directory** (EXPLICIT).
4. Classify: FULL_SCOPE_COVERED / PARTIAL_SCOPE_ONLY / INCONSISTENT / NOT_PROVEN.
5. Special-case review: scroll vs physical-scroll, popup-local-recovery vs popup-obstruction integration, uncertain-action vs DSH cognition, s0-capstone external evidence.
6. `openspec validate --strict` per change.
7. Archive only FULL_SCOPE_COVERED changes; the CLI syncs main specs (`openspec/specs/`) during archive.

## 3. Introduced-SC vs Referenced-SC Distinction

Every Phase 3 spec references SCs that are **frozen pre-existing criteria of other changes** alongside the SC the change itself introduces. Referenced SCs are dependencies, not scope:

| Change (introduces) | Referenced frozen criteria in spec | Nature |
|---|---|---|
| `phase3-bounded-cross-page-discovery` (CAND-008) | CAND-006 "existing… criterion", CAND-007 "under SC-P3-CAND-007", CAND-004 "valid SC-P3-CAND-004 completed-sibling evidence" | reference |
| `phase3-discovered-branch-effect-revalidation` (CAND-009) | CAND-008 "accepted… inventory evidence", CAND-004 "historical… completion provenance" | reference |
| `phase3-s0-capstone-settings-traversal` (CAPSTONE-001) | SC-P3-002 / CAND-005 / CAND-009 / CAND-008 / CAND-004 / CAND-006 / SC-P3-003 / CAND-007 (all "frozen…" prefixes) | reference |
| `phase3-viewport-exploration-exhaustion` (CAND-007) | SC-P3-003 "after SC-P3-003 freshness and semantic continuity are proven" | reference |
| all others | only their own introduced SC | — |

The closeout proves the **introduced** SC's capability, which is the change's full scope.

## 4. Per-Change Coverage Matrix — 10/10 FULL_SCOPE_COVERED

| Change | Introduced SC | Closeout (EXPLICIT authority) | Spec reqs | Tasks | validate --strict | Production Delta | Classification |
|---|---|---|---|---|---|---|---|
| `phase3-uncertain-action` | SC-P3-001 | `phase3-sc-p3-001` → names `openspec/changes/phase3-uncertain-action/` | 4/4 matched | 4/4 | valid | +0 | **FULL_SCOPE_COVERED** |
| `phase3-popup-local-recovery` | SC-P3-002 | `phase3-sc-p3-002` → names `openspec/changes/phase3-popup-local-recovery/` | 7/7 matched | 4/4 | valid | +0 | **FULL_SCOPE_COVERED** |
| `phase3-scroll-identity-continuity` | SC-P3-003 | `phase3-sc-p3-003` → names `openspec/changes/phase3-scroll-identity-continuity/` | 7/7 matched | 4/4 | valid | +0 | **FULL_SCOPE_COVERED** |
| `phase3-sibling-branch-progress` | SC-P3-CAND-004 | `phase3-sc-p3-cand-004` → names `openspec/changes/phase3-sibling-branch-progress/` | 10/10 matched | 4/4 | valid | +1 immutable type | **FULL_SCOPE_COVERED** |
| `phase3-recovery-progress-resume` | SC-P3-CAND-005 | `phase3-sc-p3-cand-005` → names `openspec/changes/phase3-recovery-progress-resume/` | 10/10 matched | 4/4 | valid | +1 optional field | **FULL_SCOPE_COVERED** |
| `phase3-bounded-candidate-safety` | SC-P3-CAND-006 | `phase3-sc-p3-cand-006` → names `openspec/changes/phase3-bounded-candidate-safety/` | 8/8 matched | 4/4 | valid | +1 type +3 fields | **FULL_SCOPE_COVERED** |
| `phase3-viewport-exploration-exhaustion` | SC-P3-CAND-007 | `phase3-sc-p3-cand-007` → names `openspec/changes/phase3-viewport-exploration-exhaustion/` | 9/9 matched | 4/4 | valid | +1 type +4 fields | **FULL_SCOPE_COVERED** |
| `phase3-bounded-cross-page-discovery` | SC-P3-CAND-008 | `phase3-sc-p3-cand-008` → names `openspec/changes/phase3-bounded-cross-page-discovery/` | 9/9 matched | 4/4 | valid | +1 type +3 fields | **FULL_SCOPE_COVERED** |
| `phase3-discovered-branch-effect-revalidation` | SC-P3-CAND-009 | `phase3-sc-p3-cand-009` → names `openspec/changes/phase3-discovered-branch-effect-revalidation/` | 9/9 matched | 4/4 | valid | +1 type +3 fields | **FULL_SCOPE_COVERED** |
| `phase3-s0-capstone-settings-traversal` | SC-S0-CAPSTONE-001 | `phase3-sc-s0-capstone-001` → names `openspec/changes/phase3-s0-capstone-settings-traversal/` + HUMAN gates | 7/7 matched | 4/4 | valid | +0 (SHA-equal manifest) | **FULL_SCOPE_COVERED** |

Every closeout carries: OpenSpec strict validation passed; independent validation PASS; build 0 warnings / 0 errors; Architecture Guards 8/8 or 9/9; frozen State (SC_P3_XXX_FROZEN_CAPABILITY; capstone `SC_S0_CAPSTONE_001_READY_FOR_S0_RUN`).

## 5. Full-Scope Evidence Notes

- `uncertain-action` (SC-P3-001): spec is 4 requirements (fresh-Observation verification, completion authority, post-dispatch vs pre-dispatch retry separation, frozen model). Proven behavior covers all four; delta +0; not a DSH cognition concern (DSH shadow cognition is a separately archived DSH change).
- `popup-local-recovery` (SC-P3-002): 7 requirements; proven behavior covers bounded Container-scope handling, fresh continuity verification, progress preservation, escalation; delta +0.
- `scroll-identity-continuity` (SC-P3-003): 7 requirements; proven behavior covers one bounded forward movement, fresh evidence, identity preservation, escalation; delta +0.
- `sibling-branch-progress` (CAND-004): 10 requirements; proven behavior covers A/B inventory from fresh parent evidence, child-local completion proof, subtree derivation, no duplication; delta +1 `BranchProgressEvidence` value only.
- `recovery-progress-resume` (CAND-005): 10 requirements; proven behavior covers three-way criterion, historical retention, fresh revalidation, no blind replay; delta +1 optional `PlanStep` field only.
- `bounded-candidate-safety` (CAND-006): 8 requirements; proven behavior covers authorization separation, pre-dispatch Trace, at-most-one nomination; delta +1 `CandidateAuthorizationEvidence` +3 fields.
- `viewport-exploration-exhaustion` (CAND-007): 9 requirements; proven behavior covers three-valued exploration, at-most-one movement per true, positive-only exhaustion; delta +1 `ViewportExplorationEvidence` +4 fields.
- `bounded-cross-page-discovery` (CAND-008): 9 requirements; proven behavior covers required-branch inventory from accepted evidence (not Plan), route continuation, semantic depth independence; delta +1 `BranchInventoryEvidence` +3 fields.
- `discovered-branch-effect-revalidation` (CAND-009): 9 requirements; proven behavior covers singular immutable criterion, identity match under same parent, fresh post-Recovery evaluation only, no persisted lifecycle state; delta +1 `BranchEffectCriterion` +3 fields.
- `s0-capstone-settings-traversal` (CAPSTONE-001): 7 requirements; proven behavior covers external-world-only S0 world (reflection-guarded), exactly one Popup + one drift schedule, frozen-composition-only traversal, 7-conjunct GoalEvidence completion, stop-and-extract on Reality Distinction; delta 0 with SHA-equal pre/post manifest.

## 6. Special-Case Review

1. **scroll-identity-continuity vs `physical-scroll` (archived)**: archived `2026-08-16-physical-scroll-container-semantic-traversal` explicitly *consumes* the frozen SC-P3-003 / SC-P3-CAND-007 machinery ("冻结机制：SC-P3-003…SC-P3-CAND-007"). It is a consumer, not a replacement. No duplicate archive; scroll-identity-continuity is independently eligible.
2. **popup-local-recovery vs `popup-obstruction` (archived)**: archived `2026-08-16-semantic-run-popup-obstruction-integration` is a mechanism-integration change (Buyer POPUP_INTERRUPTION) that composes frozen handling; it does not replace SC-P3-002. No conflict.
3. **uncertain-action vs DSH cognition**: SC-P3-001 is a Runtime Agent dispatch-timeout semantics change; DSH shadow cognition is a separate archived DSH-side change. Independent.
4. **s0-capstone external evidence**: closeout `SC_S0_CAPSTONE_001_READY_FOR_S0_RUN` (2026-08-09) + HUMAN gates `ACCEPT_S0_BASELINE_READY_AUTHORIZE_CAPSTONE_OPENSPEC` / `AUTHORIZE_CAPSTONE_INTEGRATION` + `docs/decisions/s0-graduation.md` (HUMAN `S0_GRADUATED`). Full double-authorization chain exists.

## 7. Validation

- `openspec validate --strict` on all 10 changes: all valid (10/10).
- Archive collision check against `openspec/changes/archive/`: 0 collisions for all 10.
- Main-spec sync performed by `openspec archive` (creates `openspec/specs/<capability>/spec.md` from each change's ADDED Requirements).

## 8. Archive Action

All 10 changes archived on 2026-08-16 (CLI output per change):

```text
phase3-uncertain-action                        → 2026-08-16-phase3-uncertain-action
phase3-popup-local-recovery                    → 2026-08-16-phase3-popup-local-recovery
phase3-scroll-identity-continuity              → 2026-08-16-phase3-scroll-identity-continuity
phase3-sibling-branch-progress                 → 2026-08-16-phase3-sibling-branch-progress
phase3-recovery-progress-resume                → 2026-08-16-phase3-recovery-progress-resume
phase3-bounded-candidate-safety                → 2026-08-16-phase3-bounded-candidate-safety
phase3-viewport-exploration-exhaustion         → 2026-08-16-phase3-viewport-exploration-exhaustion
phase3-bounded-cross-page-discovery            → 2026-08-16-phase3-bounded-cross-page-discovery
phase3-discovered-branch-effect-revalidation   → 2026-08-16-phase3-discovered-branch-effect-revalidation
phase3-s0-capstone-settings-traversal          → 2026-08-16-phase3-s0-capstone-settings-traversal
```

Main specs created: `openspec/specs/uncertain-action-verification/`, `popup-local-recovery/`, `viewport-identity-continuity/`, `sibling-branch-progress/`, `recovery-progress-resume/`, `bounded-candidate-safety/`, `viewport-exploration-exhaustion/`, `bounded-cross-page-discovery/`, `discovered-branch-effect-revalidation/`, `s0-capstone-settings-traversal/`.

## 9. Lifecycle State After Archive

- Phase 3 changes: **0 active** (10 archived). Phase 3 OpenSpec lifecycle: **CLOSED** (all ten capability SCs have explicit archive-grade closeout receipts; no unarchived Phase 3 change remains).
- Active change count after archive: **7** — `greenfield-agent-runtime` (LONG_LIVED_BASELINE_BY_DESIGN), `open-world-container-inventory-completeness` (DEFERRED_NO_BUYER), `settings-navigation-candidate-evidence` (DEFERRED_NO_BUYER), `trace-capture-scenario-catalog-foundation` (DEFERRED_NO_BUYER), `phase1-deterministic-runtime` (PENDING_GRADUATION), `phase2-trap-recovery` (PENDING_GRADUATION), `u2-open-world-settings-traversal` (PENDING_GRADUATION).
- Next gates (fixed order, no new buyer after this gate): Phase1 reconcile → Phase1 毕业 → Phase2 毕业 → U2 毕业.

## 10. Compliance

- FORBIDDEN list respected: no production edit, no test edit, no spec edit, no new graduation record fabricated, no MASS_ARCHIVE (per-change independent judgment), no buyer selection.
- This document is the aggregate governance receipt; the ten capability closeouts remain the historical graduation evidence.
