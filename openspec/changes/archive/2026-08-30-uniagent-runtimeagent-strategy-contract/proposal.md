## Why

The current UniAgent-to-RuntimeAgent Surface A can submit only a bounded four-field Directive, while the completed RuntimeAgent Directive, hypothesis, reconciliation, and adaptation capabilities still lack a typed way to receive a UniAgent-authored abstract execution strategy. A bounded Strategy Contract is needed so RuntimeAgent can interpret and adapt an already-authorized strategy without acquiring user-level planning, action, lifecycle, or completion authority.

## What Changes

- Add a generic, start-time `StrategyDirective` contract whose objective, scope, exploration intent, constraints, completion criteria, and adaptation boundary are authored by UniAgent.
- Add an additive Goal-plane start operation, provisionally named `run.strategy.start`; keep the frozen `run.start` request and all existing methods unchanged.
- Require RuntimeAgent to accept or reject the typed strategy before execution, then interpret an accepted strategy into a runtime-local execution intent and initial execution hypothesis.
- Permit only bounded runtime-local reconciliation and hypothesis adaptation declared by the strategy. Objective, scope, safety constraints, and completion criteria remain immutable for the accepted Run.
- Require unsupported semantic criteria or unavailable generic capability bindings to be rejected deterministically rather than guessed or implemented with scenario-specific knowledge.
- Preserve Agent ownership of action authorization, RunState, GoalEvidence, and terminal outcome; preserve FSM transition ownership and Traversal execution ownership.
- Exclude user-language interpretation, RuntimeAgent-authored supervisory planning, concrete action/route plans on the wire, mid-Run strategy replacement, Multi-Run continuation, and scenario-specific logic.

This is an additive protocol capability but a material Surface A semantic expansion. It therefore requires a new OpenSpec protocol gate; it is not treated as an ordinary internal Runtime model addition. It does not change the UniAgent Architecture v1 authority model.

## Capabilities

### New Capabilities

- `uniagent-runtimeagent-strategy-contract`: Defines the bounded StrategyDirective, RuntimeAgent interpretation/adaptation boundary, start-time acceptance behavior, and authority-preserving handoff to Agent execution.

### Modified Capabilities

- `runtime-external-contract-baseline`: Extends the implemented Goal plane with a new additive strategy-start operation while preserving the frozen `run.start` contract and deferred status of mid-Run Guidance.

## Impact

- Design scope: UniAgent protocol models, DriverHost Goal-plane transport, RuntimeAgent strategy validation/interpretation, composition-provided semantic capability bindings, and scenario/authority tests.
- Unchanged authority: Agent, FSM, Traversal, WorldBelief, GoalEvidence, Run terminal state, and verification ownership.
- Compatibility: no existing wire method or payload changes; existing clients continue to use `run.start`.
- Delivery boundary: this change contains design/specification only until a separate human approval authorizes apply. Production code is explicitly out of scope for the present task.
