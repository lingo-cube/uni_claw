## Why

The Runtime can prove local completion inside one semantic Container, but it cannot preserve evidence-backed progress across several Containers or distinguish “one child completed” from “all approved siblings under the parent completed”. Legacy multi-branch failures show skipped siblings combined with false `AllVisited`, and SC-S0-CAPSTONE-001 requires honest bounded subtree completion. SC-P3-CAND-004 purchases the minimum missing cross-Container progress evidence without preselecting a navigation graph, stack, manager, or generic workflow model.

## What Changes

- Add the approved SC-P3-CAND-004 formal contract for one bounded parent scope with two approved child branches.
- Add exactly one immutable production `BranchProgressEvidence` value with three semantic fields: parent semantic identity, approved sibling-inventory evidence, and proven sibling-completion evidence.
- Add exactly one Agent-owned production state field containing immutable progress snapshots; Agent remains the existing cross-Container progress owner.
- Require fresh parent Observation evidence to establish the complete approved sibling inventory within the bounded Scenario boundary.
- Require existing Container-local completion evidence before a child may be recorded complete; returning to a parent or revisiting a child is not completion evidence.
- Derive bounded parent/subtree completion only when every approved sibling has valid completion evidence.
- Preserve progress through ordinary child → parent → sibling navigation without introducing a new Back action.
- Preserve Agent authority for cross-Container interpretation, GoalEvidence, and final RunState.

## Capabilities

### New Capabilities

- `sibling-branch-progress`: Defines Agent-owned, evidence-backed progress across a bounded semantic parent and its approved siblings, including honest subtree completion, identity conflict handling, parent return, and deterministic replay.

### Modified Capabilities

None. Existing Observation, WorldBelief, Container identity/local completion, Traversal journal, GoalEvidence, RunState, and Recovery ownership remain authoritative within their existing scopes.

## Impact

- Expected production surface: one new Model value plus existing Agent control flow, subject to later approved task planning.
- Expected verification surface: deterministic hierarchical Scenario Fake/Harness and SC-P3-CAND-004 positive, incomplete-sibling, revisit, stale-evidence, conflicting-identity, and replay proofs.
- Production delta budget: model types +1; production fields +4 total (three immutable value fields plus one Agent-owned state field); enums +0; interfaces +0; components +0; mutable-state owners +0.
- Ownership delta: none. Agent already owns cross-Container decisions and becomes the sole owner of the approved progress state. Authority delta: none.
- Backtracking remains execution mechanics through existing approved visible affordances and existing actions.
- Recovery-progress validity after external drift remains separate research.
- No NavigationGraph, PageGraph, TraversalGraph, stack/tree/hierarchy model, visited-set semantic type, TraversalContext, ResumeToken, manager, FSM, new Back action, new Recovery semantic, autonomous discovered-candidate safety policy, Capstone implementation, or Runtime refactor is purchased.
