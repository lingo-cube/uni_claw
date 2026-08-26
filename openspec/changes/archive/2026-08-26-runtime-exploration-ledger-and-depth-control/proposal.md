## Why

The Runtime Exploration Roadmap Phase 2 (Exploration Runtime) is partially delivered: strategy admission, run-scoped reasoning, depth-as-numeric-bound (fail-closed cutoff, "bounded cutoff is not exhaustion"), branch progress evidence, and bounded revisit coverage all exist, but they are **fragmented across independent models** (`BranchProgressEvidence`, `_revisitCoverage` maps, `StrategyExecutionEvidenceView` structural-progress facts). There is no single evidence-derived projection answering, per exploration scope: how many nodes were discovered, how many were processed per exploration rule, how many remain pending, and how large the unknown frontier is. The roadmap's Completion Evidence requirement (`discovered/processed/pending/unknown frontier → completion: true`) therefore cannot be stated against one coherent ledger. Depth is enforced only as a numeric cutoff whose violation fails the Run; there is no first-class semantic distinction between "Depth = 1 root only", "Depth = 2 root + children", and bounded deeper exploration, and no rule vocabulary for container-vs-leaf handling (expand vs record-only).

## What Changes

- Add a read-only, evidence-derived **Exploration Ledger projection**: a per-Run immutable view unifying discovered, visited (rule-satisfied, not clicked), pending, and unresolved-frontier counts compiled from existing branch-progress, coverage, and evidence-view records — adding **no new state system, no new owner**, and never mutating existing evidence.
- Add a closed **Exploration Rule vocabulary** (per discovered node classification: `ExpandContainer` / `RecordOnly`) interpreted from the accepted `StrategyDirective`'s exploration intent; RuntimeAgent classifies nodes using existing semantic capability output and applies the declared rule. `Visited` is defined as "rule-satisfied with evidence", never as "clicked".
- Add **semantic Depth Control**: map declared `MaximumDepth` to bounded exploration semantics (0 = root record-only, 1 = root + direct children, N = bounded recursive with fail-closed cutoff preserved). Depth semantics are admission-validated and enforced at the existing traversal cutoff; no dynamic depth, no mid-Run depth mutation.
- Expose the ledger as an Agent-readable evidence projection on the existing snapshot/evidence surfaces; completion remains Agent-owned GoalEvidence — the ledger is an **input to proof, never a completion fact**.

## Capabilities

### New Capabilities

- `runtime-exploration-ledger-and-depth-control`: Defines the evidence-derived exploration ledger projection, the closed exploration-rule vocabulary, `Visited ≠ Clicked` evidence semantics, and bounded semantic depth control without new authority, state systems, or scenario knowledge.

### Modified Capabilities

- None. The frozen `run.start` contract, the eight read-only methods, `StrategyDirective` schema, `StrategyExecutionEvidenceView`, and all existing evidence models remain unchanged; the ledger is an additive internal projection compiled from them.

## Impact

- Design scope: `src/UniClaw.Runtime/` (Planning/Model projection compilation, node classification at the existing semantic seam, depth-semantics validation in admission) and tests. No DriverHost wire changes in this change; read-model exposure follows existing snapshot surfaces only.
- Unchanged authority: Agent (RunState, GoalEvidence, authorization, terminal), FSM, Traversal execution, WorldBelief ownership, Memory (Phase 3 remains out of scope), and the UniAgent Planner boundary. RuntimeAgent gains no planning, completion, or strategy-generation responsibility.
- Boundary risks explicitly excluded: no scenario-specific classification rules, no fixed page paths, no UI-text/coordinate grounding, no new Memory owner (Phase 3), no dynamic depth (Phase 4), no `Visited = Clicked` equivalence.
- This is a **Large change** (new internal projection boundary + new rule vocabulary): production apply requires an explicit Human Gate approval. This proposal contains design/specification only.
