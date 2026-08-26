## Why

Phase 2 provides a graduated, Run-bound Exploration Ledger, completion evidence path, and immutable depth semantics, but it intentionally provides no cross-Run historical knowledge retention or retrieval. A bounded UniAgent-local Memory capability is proposed so UniAgent can consult provenance-bearing historical knowledge before forming a future Exploration Plan without creating Runtime state, a second truth source, or execution authority.

This change is a draft only. Its buyer and owner are fixed for proposal purposes, while `UNIAGENT_PRIVATE_CROSS_SESSION` remains conditional on the next Human Gate. Apply and implementation are not authorized by creation or validation of these artifacts.

## What Changes

- Introduce a UniAgent-local exploration Memory boundary whose first buyer is **UniAgent pre-Run Exploration Plan advisory**.
- Define semantic distinctions between producer-owned `FactReference` records and Memory-owned, provenance-bearing `KnowledgeClaim` records.
- Define advisory-only admission and retrieval semantics, including explicit scope, version, freshness, contradiction, supersession, invalidation, and truthful unavailable outcomes.
- Permit `UNIAGENT_PRIVATE_CROSS_SESSION` retrieval only if the Implementation Human Gate explicitly approves that lifecycle scope.
- Allow retrieved knowledge to inform a UniAgent pre-Run supervisory decision while preserving the accepted StrategyDirective, Runtime fresh-observation requirement, and all Phase 2 authority boundaries.
- Require Memory unavailability, stale knowledge, contradictory claims, and invalid scope to fail closed as knowledge retrieval outcomes without affecting Runtime execution.
- Explicitly exclude RuntimeAgent Memory, mutable Runtime state, WorldBelief mutation, GoalEvidence, completion authority, action generation or blocking, policy enforcement, Dynamic Planner behavior, mid-Run Strategy mutation, Dynamic Depth, and Multi-Run orchestration.

## Capabilities

### New Capabilities

- `uniagent-local-exploration-memory`: Defines the owner-local Memory boundary, FactReference and KnowledgeClaim semantics, provenance/scope/version rules, freshness and invalidation lifecycle, advisory retrieval behavior, conditional private cross-session scope, and pre-Run Exploration Plan relationship.

### Modified Capabilities

- None.

## Impact

- Prospective affected area: a future UniAgent-local Memory model, retention/retrieval boundary, and pre-Run advisory consumer owned by UniAgent.
- Unchanged areas: RuntimeAgent, Agent/FSM/Traversal ownership, WorldBelief, ExplorationLedger, GoalEvidence, Run lifecycle, StrategyDirective immutability, Runtime wire/API surfaces, and Phase 2 graduation.
- No database, persistence technology, DTO, public API, Runtime hook, implementation code, or migration is selected by this proposal.
- Change classification: Large because it proposes a new capability, owner-local boundary, and conditional cross-session lifecycle. A separate Human Gate is required before apply.
