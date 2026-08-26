## Why

Independent graduation reverification revoked Runtime Exploration Roadmap Phase 2 graduation because the accepted Strategy Run does not actually derive or apply the closed exploration rules at admission, arbitrary-depth bounded-record semantics have no frozen source, real unresolved identities are double-counted, and structural-progress facts are absent from ledger compilation. Human selected Option A: preserve the frozen `StrategyDirective` wire/schema and remediate these gaps through a deterministic internal interpretation of its existing closed fields.

## What Changes

- Freeze a total internal interpretation table from the accepted `StrategyObjective` + `ExplorationIntent` + `StrategyCompletionKind` + `MaximumDepth` tuple to immutable per-Run exploration rules and depth-boundary disposition; RuntimeAgent does not choose or invent a mode.
- Derive the exploration semantics during `StrategyContractCompiler` admission, carry them in `RuntimeExecutionIntent`, and apply them on the real Agent classification/depth path.
- Preserve the existing depth-0 and depth-1 record-only semantics; for depth `N >= 2`, preserve exhaustive fail-closed behavior for `ExploreScope` and use bounded-record behavior for `InspectMatchesWithinScope`.
- Bind ledger metadata and evidence to the actual accepted Strategy Run instead of accepting caller-authored Run/intent/depth provenance at projection time.
- Make per-scope accounting identity-correlated and exhaustive: unresolved and unknown-frontier identities remain subsets of the discovered inventory, `Discovered` is never double-counted, and every discovered identity is exactly visited, pending, or unresolved while unknown frontier remains an overlapping annotation on record-only visited nodes.
- Admit existing `StrategyStructuralProgressFact` records as a fail-closed correlation input without using them as node-count or completion authority.
- Add real-path scenario tests and guards for admission derivation, rule application, depth `0/1/N` divergence, exact unresolved accounting, accepted-Run provenance, structural-fact correlation, and unchanged Agent/FSM/Traversal/GoalEvidence authority.
- Do not change `StrategyDirective`, `run.strategy.start`, existing wire DTOs, public protocol versions, ownership, lifecycle, completion authority, scenario knowledge, Phase 3 Memory, or Phase 4 dynamic depth.

## Capabilities

### New Capabilities

- `runtime-exploration-semantic-admission-remediation`: Defines the Human-selected Option A internal interpretation, accepted-Run evidence binding, identity-correct ledger accounting, and structural-progress correlation required to close the revoked Phase 2 graduation gaps without a Strategy Contract schema change.

### Modified Capabilities

- None. The predecessor `runtime-exploration-ledger-and-depth-control` change remains active and ungraduated; this successor adds a remediation contract and does not rewrite its frozen Spec after apply.

## Impact

- Production scope: `src/UniClaw.Runtime/Planning/StrategyContract.cs`, `Planning/IntentExecution.cs`, `Model/ExplorationLedger*.cs`, existing Agent open-world/admission evidence seams, and only the minimum existing immutable evidence records needed for identity correlation.
- Test scope: Strategy admission, exploration ledger/depth/unresolved real-path tests, Strategy execution tests, and architecture/authority guards under `tests/UniClaw.Runtime.Tests/`.
- Documentation scope: predecessor task reconciliation, graduation reverification decision, current gates, latest snapshot, and a later independent graduation decision only after all gates pass.
- Compatibility: no external wire/schema or protocol version change; legacy non-Strategy open-world entry remains behaviorally unchanged and cannot fabricate a Strategy-bound ledger.
- Classification: Large Change because it freezes internal Strategy interpretation and evidence correlation semantics. Proposal/design/spec/tasks preparation is authorized by Human Option A selection; production apply requires a separate explicit Human Gate.
