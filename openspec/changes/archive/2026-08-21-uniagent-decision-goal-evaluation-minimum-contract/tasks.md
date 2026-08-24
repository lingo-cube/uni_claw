## 0. Authorization gate

- [x] 0.1 Complete proposal, design, and minimum semantic specification from the ratified Agent Concept Model v1 scenarios.
- [x] 0.2 Select the first concrete producer/consumer, documentation-contract owner, dependency direction, and semantic lifetime; record explicit human documentation-only Apply authorization.

## 1. Contract realization

- [x] 1.1 Create the authorized minimum semantic contract for UniAgent Decision with identity, producer, Session/Primary-Goal correlation, disposition, basis references, and optional candidate-Directive/supersession references.
- [x] 1.2 Create the authorized minimum Goal Evaluation semantic contract with Completion, Satisfaction, Runtime-outcome/evidence references, producer, and optional supersession reference.
- [x] 1.3 Specify append-oriented recording and latest-projection behavior without mutating historical records or Runtime truth.
- [x] 1.4 Specify operator-authored supersession without editing UniAgent or RuntimeAgent producer records.

## 2. Authority and dependency guards

- [x] 2.1 Prove the semantic contract introduces no Runtime physical, belief, binding, GoalEvidence, or RunState mutation path.
- [x] 2.2 Prove Session remains correlation/navigation rather than a message bus, Runtime state store, or generic mutable JSON object.
- [x] 2.3 Prove no database, Data Plane, DSH UI, model provider, or Runtime Protocol method is added by the documentation-only scope.

## 3. Scenario validation

- [x] 3.1 Validate SC-A: Runtime Completed with Goal Completion Completed and Satisfaction Unsatisfied.
- [x] 3.2 Validate SC-B: preserved Runtime Failed outcome plus append-only Retry Decision/candidate Directive, with post-terminal dispatch explicitly reserved for a separately authorized new Run model.
- [x] 3.3 Validate SC-C: AssistanceRequired/operator Decision without fabricated terminal outcome or evaluation.
- [x] 3.4 Validate Indeterminate evaluation and operator supersession while preserving all prior records.

## 4. Documentation and graduation

- [x] 4.1 Publish the source-linked minimum contract documentation and update only the projections affected by the authorized semantic contract.
- [x] 4.2 Run strict OpenSpec validation, consistency checks, dependency/authority guards, and the task-relevant test suite.
- [x] 4.3 Obtain independent graduation review before spec sync or archive.

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|---|---|
| Agent concept authority | `docs/architecture/agent-concept-model-v1.md` |
| Semantic protocol authority | `docs/architecture/uniagent-protocol-v1-consolidation-design.md` |
| Proposed minimum contract | `docs/architecture/uniagent-decision-goal-evaluation-minimum-contract.md` |
