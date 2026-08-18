# Active OpenSpec Change — Lifecycle Truth Matrix

> Governance reconciliation (2026-08-16), `PROJECT_LEADER_RECONCILE_ACTIVE_OPENSPEC_LIFECYCLE`
> Mode: GOVERNANCE_ONLY (no production/test/OpenSpec mutation except verified archives)
> Principle: active directory ≠ current buyer. Every entry carries one lifecycle
> classification; the semantic view is the planning source, not the directory list.
> Update 2 (2026-08-16): Phase3 aggregate archive review closed — 10 phase3-* changes
> archived (see `docs/decisions/phase3-aggregate-lifecycle-archive-review.md`);
> `runtime-observability-trace-foundation` archived.
> Update 3 (2026-08-16): Phase1 task truth reconciled (canonical 21 task lines;
> 22 was a counting error) — C1 satisfied by existing durable evidence, C2
> POST_GRADUATION_ARCHIVE_PENDING.
> Update 4 (2026-08-16): Phase1 GRADUATED (`phase1-deterministic-runtime-graduation-decision.md`,
> `PHASE1_DETERMINISTIC_RUNTIME_BASELINE_GRADUATED`) + archived
> (`2026-08-16-phase1-deterministic-runtime`). Phase2 GRADUATED
> (`phase2-trap-recovery-graduation-decision.md`, `PHASE2_DETERMINISTIC_TRAP_RECOVERY_BASELINE_GRADUATED`)
> + archived (`2026-08-16-phase2-trap-recovery`).
> Update 5 (2026-08-16): U2 GRADUATED (`u2-open-world-settings-traversal-graduation-decision.md`,
> `U2_BOUNDED_OPEN_WORLD_SETTINGS_TRAVERSAL_GRADUATED`) + archived
> (`2026-08-16-u2-open-world-settings-traversal`). PENDING_GRADUATION queue is now EMPTY.
> Update 6 (2026-08-16, observed state — NOT this flow's action):
> `settings-navigation-candidate-evidence` was graduated and archived by a parallel
> governance flow (`docs/decisions/settings-navigation-candidate-evidence-graduation-decision.md`,
> `SETTINGS_NAVIGATION_CANDIDATE_EVIDENCE_BASELINE`, archive
> `2026-08-16-settings-navigation-candidate-evidence`), with buyer
> SETTINGS_FULL_TREE_INVENTORY_COMPLETENESS. Recorded here for matrix truth.

## Inventory (3 active directories, post-U2-graduation recount)

| # | ChangeName | Tasks | Proposal | Design | Spec | OriginalBuyer | LifecycleClassification | AggregateCoverage | RecommendedAction | MutateThisGate |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | greenfield-agent-runtime | 9/9 | Y | Y | Y | Phase 0 地基 | **LONG_LIVED_BASELINE_BY_DESIGN** | N/A | KEEP_ACTIVE | NO |
| 2 | open-world-container-inventory-completeness | 0/17 | Y | Y | Y | SETTINGS_FULL_TREE_INVENTORY | **PROPOSED_NO_CURRENT_BUYER** | N/A | KEEP_ACTIVE_DEFERRED | NO |
| 3 | trace-capture-scenario-catalog-foundation | 0/0 | Y | N | N | Scenario catalog | **PROPOSED_NO_CURRENT_BUYER** | N/A | KEEP_ACTIVE_DEFERRED | NO |

## Corrected Arithmetic (from the recounted inventory)

- **ActualActiveChangeCount**: 3 (18 − 11 Phase3/runtime-observability − 2 Phase1/Phase2 − 1 U2 − 1 settings-navigation)
- **ArchivedThisCycle**: 15 (10×phase3-* + runtime-observability-trace-foundation +
  phase1-deterministic-runtime + phase2-trap-recovery + u2-open-world-settings-traversal +
  settings-navigation-candidate-evidence [parallel flow])
- **GraduatedThisCycle**: 4 (phase1, phase2, u2 [this flow] + settings-navigation-candidate-evidence [parallel flow])
- **CompletedButActiveChanges**: 0
- **GraduatedButActiveChanges**: 0
- **StaleActiveChanges**: 0
- **LongLivedBaselines**: 1 (greenfield-agent-runtime)
- **ProposedWithoutBuyer**: 2 (open-world-inventory, trace-capture)
- **PendingGraduation**: 0 (EMPTY — lifecycle cleanup complete)
- **AggregateCovered (Phase3)**: 0 remaining active (10 archived with EXPLICIT closeouts)

## Graduated & Archived 2026-08-16

| Change | Maturity | Record | Archive |
|---|---|---|---|
| phase1-deterministic-runtime | `PHASE1_DETERMINISTIC_RUNTIME_BASELINE_GRADUATED` | `docs/decisions/phase1-deterministic-runtime-graduation-decision.md` | `openspec/changes/archive/2026-08-16-phase1-deterministic-runtime/` |
| phase2-trap-recovery | `PHASE2_DETERMINISTIC_TRAP_RECOVERY_BASELINE_GRADUATED` | `docs/decisions/phase2-trap-recovery-graduation-decision.md` | `openspec/changes/archive/2026-08-16-phase2-trap-recovery/` |
| u2-open-world-settings-traversal | `U2_BOUNDED_OPEN_WORLD_SETTINGS_TRAVERSAL_GRADUATED` | `docs/decisions/u2-open-world-settings-traversal-graduation-decision.md` | `openspec/changes/archive/2026-08-16-u2-open-world-settings-traversal/` |
| settings-navigation-candidate-evidence | `SETTINGS_NAVIGATION_CANDIDATE_EVIDENCE_BASELINE` (parallel flow) | `docs/decisions/settings-navigation-candidate-evidence-graduation-decision.md` | `openspec/changes/archive/2026-08-16-settings-navigation-candidate-evidence/` |

Phase3 aggregate archive (closed): all 10 Phase3 changes FULL_SCOPE_COVERED with
EXPLICIT directory-named closeouts, archived to `2026-08-16-phase3-*`; receipt
`docs/decisions/phase3-aggregate-lifecycle-archive-review.md`.

## Semantic Lifecycle View (planning source)

- **CURRENT_BUYER**: (none — no active change has a live consumer/failure)
- **DEFERRED_NO_BUYER**: open-world-inventory, trace-capture
- **LONG_LIVED_BASELINE**: greenfield-agent-runtime
- **PENDING_GRADUATION**: (none — queue empty)
- **GRADUATED_ARCHIVED_2026-08-16**: phase1, phase2, u2, settings-navigation-candidate-evidence,
  phase3-* (10), runtime-observability-trace-foundation
