## Why

Agent Concept Model v1 now freezes UniAgent Decision and Goal Evaluation ownership, but both remain `MISSING_CONTRACT`: current DSH / UniClaw surfaces cannot express a durable, producer-authored supervisory judgment or a completion-plus-satisfaction evaluation without inventing ad-hoc records. The three ratified scenarios provide a concrete buyer for a minimal semantic contract, while storage, UI, transport, and Session database models remain deliberately out of scope.

## What Changes

- Define the minimum transport- and storage-independent semantic contract for an append-oriented `UniAgentDecision` record.
- Define the minimum semantic contract for `GoalEvaluation`, with independent Completion and Satisfaction dimensions.
- Require explicit references to the Primary Goal, relevant Runtime Outcome, and supporting Evidence/Fact/Decision records without copying or rewriting producer-owned truth.
- Define operator participation as a new producer-authored evaluation/decision that may supersede the latest evaluation projection while preserving prior records.
- Validate the contract against SC-A (Runtime Completed / Goal Unsatisfied), SC-B (Runtime Failed / revised Directive), and SC-C (operator adjudication without fabricated terminal outcome).
- Keep RuntimeAgent Decision, Runtime Outcome, GoalEvidence, RunState, and Runtime Protocol ownership unchanged.
- Apply the documentation-only semantic contract under the user's explicit direct-completion authorization. Code/DTO/storage/UI/transport representation remains `NOT_AUTHORIZED` until a later buyer selects an implementation owner.

## Capabilities

### New Capabilities

- `uniagent-decision-goal-evaluation-minimum-contract`: Minimum producer, reference, append-only, supersession, and authority semantics for UniAgent Decision and Goal Evaluation.

### Modified Capabilities

None.

## Impact

- Documentation contract: `docs/architecture/uniagent-decision-goal-evaluation-minimum-contract.md`.
- Source alignment: `docs/architecture/agent-concept-model-v1.md` and `docs/architecture/uniagent-protocol-v1-consolidation-design.md`.
- Future host selection may affect DSH Session integration, but this proposal makes no DSH-specific concept architectural and changes no plugin/runtime code.
- No new Runtime Protocol method, RunState, RuntimeEvent kind, physical authority, database schema, Data Plane, Session DTO, persistence, UI, or model call.
