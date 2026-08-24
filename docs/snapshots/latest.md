# Project Current State

DocumentType: `CURRENT_PROJECT_SNAPSHOT`
Authority: `NONE`
GeneratedProjection: `true`
GeneratedFrom: `docs/work/active/current-gates.md`
GeneratedAt: `2026-08-24`
ProjectionState: `CURRENT`
ActiveChangeCount: `17`
ArchivedChangeCount: `42`

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
The current lifecycle projection lists 17 Current Active changes and 41
Historical Archived changes. Task completion is not projected as graduation or
archive eligibility. [Current gates](../work/active/current-gates.md)

Blocked:
`semantic-perception-layer-baseline` remains `APPLY_NOT_AUTHORIZED`; this
snapshot does not state any additional blocked lifecycle conclusion. [Vision projection](../architecture/vision.md)

Next:
The `uniagent-runtimeagent-strategy-contract` change is graduated (evidence-verified,
human-authorized) and pending archive; its deferred scope (Planner, mid-Run strategy
replacement, Multi-Run, exploration Memory, dynamic depth) remains unauthorized. The
`runtime-exploration-ledger-and-depth-control` change (Runtime Exploration Roadmap
Phase 2 — Exploration Runtime) is graduated per its
[graduation decision](../decisions/runtime-exploration-ledger-and-depth-control-graduation-decision.md)
(evidence-derived exploration ledger projection, closed rule vocabulary with real-path
fail-closed classification, Visited-equals-rule-satisfied semantics, bounded semantic
depth control; no completion/FSM/action authority) and pending archive; its deferred
scope (per-scope structural-fact fusion, Phase 3 Memory, Phase 4 dynamic depth) remains
unauthorized. The `uniagent-decision-goal-evaluation-minimum-contract` change is graduated
and archived as a documentation-only semantic contract. DTO/store/UI/transport,
post-terminal retry dispatch, multi-Run execution, and non-terminal escalation
transport remain unimplemented and unauthorized. [Governance projection](../architecture/governance.md)
