## Why

Architecture v1 and Protocol v1 already distinguish UniAgent supervisory authority from RuntimeAgent execution authority, but the repository does not yet freeze one coherent vocabulary for Agent loops, goal levels, plans, decisions, Runtime outcomes, Goal Evaluation, and their evidence/trace relationships. Without that alignment, DSH and UniClaw can use the same words for different owners and accidentally create a second Runtime truth or open reserved multi-run behavior.

## What Changes

- Freeze a subordinate Agent Concept Model v1 governed by the existing Architecture v1 and Protocol v1, not a parallel top-level baseline.
- Distinguish Session-level Primary Goal from the bounded Runtime Execution Goal carried by a Directive.
- Distinguish UniAgent Supervisory Plan, Runtime-local Plan, concrete Action, Decision, and Trace.
- Separate request acceptance, Runtime Outcome, and UniAgent Goal Evaluation; define Goal Evaluation as Completion plus Satisfaction.
- Preserve `Escalation != TerminalOutcome`: `AssistanceRequired` is non-terminal supervisory disposition, not a fabricated terminal Run result.
- Freeze the minimum Observation / Fact / Evidence / WorldBelief / GoalEvidence and Trace / Span / Event relationships without defining Data Plane schemas.
- Map the concepts to existing DSH / UniClaw objects and classify implemented, partial, missing-contract, and reserved surfaces.
- Validate the model with three scenarios: Runtime Completed but Goal Unsatisfied; Runtime Failed followed by a revised Directive; non-terminal human adjudication without fabricated completion.
- Amend only architecture/protocol documentation and current projections. No production code, wire DTO, storage model, DSH UI, or transport implementation is added.

## Capabilities

### New Capabilities

- `agent-concept-model-v1`: Minimum ownership, lifecycle, result/evaluation, evidence, and trace semantics for UniAgent and RuntimeAgent, aligned to the existing frozen baselines.

### Modified Capabilities

None.

## Impact

- `docs/architecture/agent-concept-model-v1.md`: new subordinate frozen concept model.
- `docs/architecture/uniagent-architecture-v1-core-development-guide.md`: bounded terminology/lifecycle amendment.
- `docs/architecture/uniagent-protocol-v1-consolidation-design.md`: bounded semantic alignment amendment with no wire change.
- `docs/architecture/README.md`, `docs/architecture/current-architecture-state.md`, `docs/architecture/dsh.md`, `docs/architecture/runtime.md`, `docs/architecture/evidence.md`: navigation and source-linked projections.
- `openspec/specs/agent-concept-model-v1/spec.md`: main capability spec after graduation.
- No impact to Runtime code, DriverHost protocol methods, DSH commands, dependencies, database schemas, or current reserved-extension gates.
