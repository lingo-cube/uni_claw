# Project Current State

DocumentType: `CURRENT_PROJECT_SNAPSHOT`
Authority: `NONE`
GeneratedProjection: `true`
GeneratedFrom: `docs/work/active/current-gates.md`
GeneratedAt: `2026-08-29`
ProjectionState: `CURRENT`
ActiveChangeCount: `24`
ArchivedChangeCount: `47`

This snapshot is a retrieval aid. It restates current projections without
creating a gate, selecting a capability buyer, or changing lifecycle state.

Runtime:
RuntimeAgent is the bounded execution authority and completes work through the
observe → reconcile → decide → execute → verify loop. [Runtime projection](../architecture/runtime.md)

Vision:
Vision and Semantic Perception remain subordinate capabilities within
Architecture v1; their capability output is subject to RuntimeAgent
reconciliation. [Vision projection](../architecture/vision.md)

DSH:
DSH is an implementation framework / composition host and does not own
execution truth. [DSH projection](../architecture/dsh.md)

Open Gates:
The current lifecycle projection lists 23 Current Active changes and 47
Historical Archived changes. Task completion is not projected as graduation or
archive eligibility. [Current gates](../work/active/current-gates.md)

Blocked:
`semantic-perception-layer-baseline` remains `APPLY_NOT_AUTHORIZED`; this
snapshot does not state any additional blocked lifecycle conclusion. [Vision projection](../architecture/vision.md)

Next:
Phase 2.5 is graduated AND archived (2026-08-26 unified archive):
`PHASE25_UNIAGENT_EMULATOR_RUNTIME_BUYER_VALIDATED` (Real-Emulator S1 8/8 / S2 bounded
fail-closed / S3 cross-run; Tier C Physical Device WAIVED_BY_HUMAN — no physical-device
claim). Phase 2 exploration-runtime change bundles are archived in the 2026-08-26
archive batch (ledger predecessor + semantic-admission remediation); the graduated
capabilities remain ACTIVE. Phase 3 Exploration Memory is
READY_FOR_SEPARATE_HUMAN_GATE (apply NOT authorized; the draft remains semantically
compatible and gains S3 insertion-point evidence). The
`uniagent-runtimeagent-strategy-contract` change is graduated (evidence-verified,
human-authorized) and pending archive; its deferred scope (Planner, mid-Run strategy
replacement, Multi-Run, exploration Memory, dynamic depth) remains unauthorized.
Runtime Exploration Roadmap Phase 2 is **GRADUATED / CHANGE SET ARCHIVED**. The
predecessor `runtime-exploration-ledger-and-depth-control` and Option A successor
`runtime-exploration-semantic-admission-remediation` are represented by their
dated archive bundles; the graduated capability remains ACTIVE. Their completed
implementation binds the admitted exploration interpretation and identity ledger
to one accepted Strategy Run without changing wire/schema or Runtime authority.
Phase 3 Memory, Phase 4 dynamic depth, new Evidence owner/state system, scenario
knowledge, and new completion authority remain unauthorized.
The active `perception-navigation-row-composition-repair` candidate removes
same-frame duplicate Settings rows and adds a four-anchor, frame-local row
relation verifier. A real three-anchor relaxation promoted a subtitle as a menu
item and was reverted under the authorized side-effect stop condition. The
remaining owner is primary visual row-role evidence; detector retraining or a
dedicated visual row-relation capability requires a Human Gate. Canonical Vision
deployment was not promoted.
The Phase 2 capability baseline is frozen, but the
[Roadmap consistency analysis](../decisions/runtime-exploration-roadmap-phase2-consistency-analysis.md)
found a depth-example mismatch (`Depth=1/2/N` versus approved D1 `depth=0/1/N`).
Phase 3 preparation is paused before Memory Ownership Analysis pending the
Human Roadmap disposition; Phase 3 implementation remains unauthorized.
The `uniagent-decision-goal-evaluation-minimum-contract` change is graduated
and archived as a documentation-only semantic contract. DTO/store/UI/transport,
post-terminal retry dispatch, multi-Run execution, and non-terminal escalation
transport remain unimplemented and unauthorized. [Governance projection](../architecture/governance.md)
