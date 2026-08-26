# Design — Exploration Ledger and Depth Control

> Status: PROPOSAL (not approved; production apply requires Human Gate)
> Roadmap: `docs/decisions/runtime-exploration-roadmap.md` Phase 2 remainder
> Authority: subordinate to Runtime Architecture Contract (I-1..I-14) and Architecture v1

## 1. Current-state evidence (read-only investigation, 2026-08-24)

| Roadmap Phase 2 element | Existing implementation | Gap |
|---|---|---|
| Exploration Ledger (unified discovered/visited/pending/frontier) | `BranchProgressEvidence` (approved/completed/authorized sibling maps per parent), `_revisitCoverage` (per-page exposed/resolved) in `Agent.OpenWorld.cs`, `StrategyExecutionEvidenceView` structural-progress facts | Three fragmented sources; no unified per-Run projection; no `unknown frontier` accounting |
| Exploration Loop | Observe → Reconcile → Decide → Execute → Verify closed loop with pre-terminal strategy reasoning | Delivered by `runtime-agent-strategy-execution-loop` |
| Depth Control (1 = root, 2 = root + children, N = full) | `TypeLevelTraversalSpecification.MaximumDepth` numeric bound; `Agent.OpenWorld.cs:298` fail-closed cutoff ("bounded cutoff is not exhaustion"); `MaximumSupportedDepth = 64` admission guard | Numeric only; no per-level semantic rule (root-only vs expand); cutoff violation fails the Run rather than reflecting declared depth semantics |
| Node Exploration Model (Unknown → Discovered → classify → rule-satisfied) | Discovery/authorization/return evidence per branch | No closed classification vocabulary (`ExpandContainer` / `RecordOnly`); `Visited` never formally defined as rule-satisfaction |
| Completion Evidence (discovered/processed/pending/frontier counts) | Coverage summaries in failure messages (`discovered=…, resolved=…`) — strings, not typed evidence | No typed ledger the Agent can consume as GoalEvidence input |

## 2. Design decisions

### D1 — Ledger is a projection, not a state system (Gate 3 avoidance)

`ExplorationLedgerView` is a **read-only immutable record compiled on demand** from existing evidence records (branch progress, revisit coverage, structural-progress facts, observation sequence correlations). It introduces no mutable state, no new owner, and no lifecycle. Compilation is deterministic and pure: same evidence → same ledger. This deliberately avoids creating a new state system (roadmap Gate 3); the underlying evidence owners are unchanged.

### D2 — Closed rule vocabulary, interpreted not planned

A closed `ExplorationRule` vocabulary (`ExpandContainer`, `RecordOnly`) is **derived at admission** from the accepted `StrategyDirective`'s exploration intent (already part of the graduated contract). RuntimeAgent applies the rule during classification using existing semantic capability output (container affordance). RuntimeAgent never authors rules, never invents rules for unclassifiable nodes (fail-closed: unclassifiable → unresolved, recorded in ledger, never guessed) — preserving "RuntimeAgent executes, never plans".

### D3 — `Visited ≠ Clicked`

`Visited` is recorded only when the applied rule is satisfied **with evidence**: `RecordOnly` nodes are visited by fresh-observation record (evidence: observation sequence); `ExpandContainer` nodes are visited by verified subtree return (evidence: `BranchProgressEvidence` subtree-complete / verified boundary disposition). A dispatch/click event alone never marks visited. This aligns with existing SC-P3 evidence semantics; the ledger only aggregates them.

### D4 — Depth semantics at admission + existing cutoff at execution

Admission maps `MaximumDepth` to declared semantics: `0` = root record-only; `1` = root + direct children (children record-only); `N ≥ 2` = bounded recursive expansion to N with children at depth N record-only. Execution enforces via the **existing** fail-closed cutoff in `Agent.OpenWorld.cs` — unchanged. Nodes beyond declared depth are **not** failures by themselves: they are `RecordOnly` at the boundary (this is the behavioral delta: today the cutoff fails the Run when in-scope inventory requires deeper traversal; under the new semantics, reaching depth N with pending containers classifies them record-only and marks the frontier unknown-beyond-depth rather than failing, **only when** the declared strategy semantics chose bounded-record; exhaustive strategies keep today's fail-closed behavior). No mid-Run depth mutation, no dynamic depth.

### D5 — Ledger consumption boundary

The ledger is exposed as an Agent-readable evidence projection on existing snapshot/evidence surfaces. Completion remains exclusively Agent-owned GoalEvidence + FSM transition; the ledger is an input, never a completion fact, and never triggers transitions. No DriverHost wire method changes in this change.

## 3. Scope boundaries

- **In scope**: ledger projection types + deterministic compiler; closed rule vocabulary + classification at the existing semantic seam; admission-time depth-semantics mapping; typed ledger exposure on existing surfaces; deterministic tests; authority guards (ledger cannot act, cannot complete, cannot mutate evidence).
- **Out of scope**: exploration Memory (Phase 3), dynamic depth / unknown handling strategies (Phase 4), UniAgent Planner, wire-method additions, scenario knowledge, mid-Run strategy/depth mutation, multi-Run.

## 4. Risks

| Risk | Mitigation |
|---|---|
| Ledger drifts from evidence truth (second truth source) | Projection compiled on demand, no persistence, digest like evidence view; guard test: ledger derives only from existing evidence records |
| Depth-boundary record-only weakens exhaustion guarantees | Only when strategy declares bounded-record semantics; exhaustive intent preserves fail-closed cutoff; explicit tests for both |
| Classification becomes scenario-specific | Closed vocabulary + generic semantic capability contract only; scenario-neutrality guard extended to classification code |
| Ledger becomes a completion fact | Authority guard: ledger type carries no transition/complete/authorize members; behavioral test: satisfied ledger alone never completes a Run |
