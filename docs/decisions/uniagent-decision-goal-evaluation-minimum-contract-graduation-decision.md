# UniAgent Decision + Goal Evaluation Minimum Contract — Graduation Decision

> Status: GRADUATED (documentation-only semantic contract) | Decision: `GRADUATE_UNIAGENT_DECISION_GOAL_EVALUATION_MINIMUM_CONTRACT` | Date: 2026-08-21
> Change: `openspec/changes/archive/2026-08-21-uniagent-decision-goal-evaluation-minimum-contract/`
> Authority: Agent Concept Model v1, Architecture v1, and Protocol v1 remain governing baselines.

## 1. Buyer and exact claim boundary

**Buyer:** UniAgent supervisory semantics and its future User/Application, Operator, and Session projection consumers.

This receipt claims only that a transport- and storage-independent semantic contract for producer-authored UniAgent Decisions and Goal Evaluations is frozen and source-linked. It covers producer identity, Session/Primary Goal correlation, bounded supervisory dispositions, Runtime evidence references, independent Completion and Satisfaction dimensions, append-oriented history, and operator-authored supersession.

This is documentation-only. DTOs, stores, persistence, UI, transport, model integration, and Runtime Protocol changes are not implemented or authorized by this receipt. RuntimeAgent remains the owner of Runtime truth and terminal outcomes.

## 2. Validation evidence

- Canonical contract: `docs/architecture/uniagent-decision-goal-evaluation-minimum-contract.md`.
- Main capability spec synchronized at `openspec/specs/uniagent-decision-goal-evaluation-minimum-contract/spec.md`.
- Proposal, design, tasks, and delta specification record the authorization boundary and deferred representation gate.
- Governing sources reviewed: `docs/architecture/agent-concept-model-v1.md` and `docs/architecture/uniagent-protocol-v1-consolidation-design.md`.
- Required validation: strict OpenSpec change/spec validation, documentation consistency checks, dependency/authority guards, and task-relevant regression validation are required before archive; this receipt records semantic graduation, not a claim of implementation tests.

## 3. Scenario falsifiers

| Scenario | Expected contract behavior | Falsifier result |
|---|---|---|
| SC-A — Runtime `Completed`, Goal `Unsatisfied` | Goal Evaluation records independent `Completion = Completed` and `Satisfaction = Unsatisfied`, referencing Runtime evidence without rewriting Runtime truth. | **Not falsified** by the source contract; no DTO or mutable Runtime projection is claimed. |
| SC-B — Runtime `Failed`, retry/revised Directive | UniAgent appends a Retry or ReviseDirective Decision and may reference a candidate Directive; the failed outcome remains history. | **Not falsified**: post-terminal retry dispatch, Multi-Run, and a second Run are explicitly outside current realization. |
| SC-C — `AssistanceRequired` and operator judgment | Escalation remains non-terminal; operator input is a distinct producer-authored Decision/Evaluation input. | **Not falsified**: no fabricated terminal outcome or Goal Evaluation is introduced, and no escalation transport is claimed. |
| SC-D — Indeterminate and operator supersession | Insufficient evidence yields `Indeterminate`; later operator evaluation appends with explicit supersession while preserving history. | **Not falsified** at semantic level; physical persistence and projection implementation remain deferred. |

## 4. Deferred scope

The following require a later buyer and representation/host gate: Decision and Goal Evaluation DTOs or records; append store and latest projection; database/Event Store/Data Plane persistence; DSH UI or transport; model provider integration; non-terminal escalation transport; restart/persistence behavior; post-terminal retry dispatch; Multi-Run, SubRun, or BranchRun models; and generic RuntimeAgent Decision unification.

## 5. Final conclusion

The change is **GRADUATED as a documentation-only semantic contract**. Its minimum semantics are frozen and aligned with Agent Concept Model v1 and Protocol v1, while authority remains separated: UniAgent evaluates and decides, RuntimeAgent owns execution truth, and operators contribute producer-authored judgments. No implementation surface is implied. The change may proceed to the normal archive step after required validation and task completion; archiving does not authorize deferred scope.
