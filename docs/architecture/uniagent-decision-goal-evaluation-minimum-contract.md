# UniAgent Decision + Goal Evaluation Minimum Contract

> Status: `FROZEN_SUBORDINATE_SEMANTIC_CONTRACT`
> DocumentType: `ARCHITECTURE_SEMANTIC_CONTRACT`
> Authority: `PROJECT_LEADER_UNIAGENT_DECISION_GOAL_EVALUATION_MINIMUM_CONTRACT`
> Applied: 2026-08-21 via `uniagent-decision-goal-evaluation-minimum-contract`
> Governing sources: [Agent Concept Model v1](agent-concept-model-v1.md),
> [UniAgent Architecture v1](uniagent-architecture-v1-core-development-guide.md),
> and [UniAgent Protocol v1](uniagent-protocol-v1-consolidation-design.md)

## 1. Position and scope

This contract freezes the minimum semantics required to record a UniAgent
supervisory Decision and a Goal Evaluation without creating a second Runtime
truth. It defines ownership, semantic roles, correlation, references,
append-oriented history, and supersession.

This is a documentation-only contract. It does not define or authorize:

- a C# or TypeScript DTO/interface;
- a database table, Event Store, Data Plane, or persistence lifetime;
- a Runtime Protocol method, RuntimeEvent kind, or DSH command/UI;
- a model provider or reasoning implementation;
- RuntimeAgent Decision unification;
- post-terminal retry dispatch, Multi-Run, SubRun, or BranchRun;
- non-terminal escalation transport.

## 2. Producer, consumer, and dependency direction

| Concern | Minimum contract |
|---|---|
| UniAgent Decision producer | UniAgent only |
| Operator judgment producer | Operator, as a distinct producer-authored record |
| Goal Evaluation producer | UniAgent or Operator, with producer identity preserved |
| Consumers | User/Application, Operator, Session projection/navigation readers |
| Semantic owner | UniAgent supervisory domain, governed by the Agent Concept Model v1 |
| Runtime dependency | Reference-only: Decision/Evaluation may reference Runtime-produced Outcome/Evidence; Runtime does not depend on this contract |
| History | Append-oriented; latest values are projections, not mutable records |
| Physical lifetime | Unspecified; future representation/persistence gate required |

DSH may later host an implementation, but no DSH-specific concept becomes part
of this contract and hosting does not transfer Runtime or evaluation authority.

## 3. UniAgent Decision record semantics

Every UniAgent Decision representation must preserve these semantic roles:

- stable Decision identity;
- producer identity and producer role;
- Session correlation;
- Primary Goal correlation;
- one bounded supervisory disposition;
- basis references to producer-owned Facts, Runtime Outcomes, Evidence, or prior
  Decisions;
- optional candidate Directive reference;
- optional superseded-Decision reference.

The minimum supervisory disposition vocabulary is:

- `Continue`;
- `ReviseDirective`;
- `Retry`;
- `RequestOperator`;
- `AcceptRuntimeOutcome`;
- `Terminate`.

A Decision is not an Action, Runtime Outcome, RunState, GoalEvidence, or mutable
Runtime command. `Retry` or `ReviseDirective` may reference a candidate bounded
Directive but does not authorize physical actions or post-terminal dispatch. If
the prior Run is `Completed` or `Failed`, executing another Directive requires a
separately authorized new Run model.

## 4. Goal Evaluation record semantics

Every Goal Evaluation representation must preserve these semantic roles:

- stable Evaluation identity;
- producer identity and producer role;
- Session and Primary Goal correlation;
- relevant Runtime Outcome and Evidence references;
- independent Completion and Satisfaction values;
- optional supporting Decision references;
- optional superseded-Evaluation reference.

Completion vocabulary:

```text
Completed | Incomplete | Indeterminate
```

Satisfaction vocabulary:

```text
Satisfied | Unsatisfied | Indeterminate
```

The two dimensions are independent. They must not be collapsed into one boolean
or copied into Runtime RunState. Insufficient evidence produces `Indeterminate`,
not a fabricated positive or negative result.

## 5. Append-oriented history and supersession

Decisions and Goal Evaluations are producer-authored append records. Already
recorded content is never edited or deleted to express a later judgment.

An Operator or later UniAgent evaluation may supersede the current evaluation by:

1. appending a new record under its own producer identity;
2. referencing the prior evaluation through `superseded-Evaluation` semantics;
3. advancing a `latestEvaluationRef`-style projection.

The projection is mutable navigation; the records and their supporting Runtime
Outcome/Evidence remain unchanged.

## 6. Runtime authority firewall

Decision and Goal Evaluation may reference, but never re-originate or mutate:

- RunState or terminal Runtime Outcome;
- GoalEvidence;
- Observation or WorldBelief;
- binding, grounding, verification, or recovery state;
- physical action or device state.

Session remains a correlation/navigation root. It is not the message bus,
Runtime state store, Event Store, command queue, or generic mutable JSON object.

## 7. Scenario receipts

### SC-A — Runtime Completed, Goal Unsatisfied

RuntimeAgent truthfully produces `Completed`. UniAgent evaluates the broader
Primary Goal as:

```text
Completion   = Completed
Satisfaction = Unsatisfied
```

The Goal Evaluation references the Runtime outcome/evidence and does not rewrite
RunState or GoalEvidence.

### SC-B — Runtime Failed, Retry Decision recorded

RuntimeAgent truthfully produces `Failed`. UniAgent appends a `Retry` or
`ReviseDirective` Decision and may reference a candidate Directive serving the
same Primary Goal. The failed outcome remains history. Current v1 does not
dispatch the candidate after terminal failure; a new Run gate is required.

### SC-C — AssistanceRequired with operator judgment

`AssistanceRequired` remains a non-terminal Escalation disposition. Operator
input is appended as its own Decision/Evaluation basis. No `Completed`, `Failed`,
Goal Completion, or Satisfaction value is fabricated, and RuntimeAgent keeps
final terminal-outcome authority.

### SC-D — Indeterminate and operator supersession

When evidence is insufficient, the affected Goal Evaluation dimension is
`Indeterminate`. If an Operator later obtains sufficient evidence, it appends a
new evaluation referencing the earlier one; the latest projection advances and
both records remain available.

## 8. Current realization status

| Surface | Status |
|---|---|
| Semantic contract | `FROZEN` |
| UniAgent Decision DTO/record implementation | `NOT_IMPLEMENTED` |
| Goal Evaluation DTO/record implementation | `NOT_IMPLEMENTED` |
| Append store / latest projection | `NOT_IMPLEMENTED` |
| Operator supersession transport/UI | `NOT_IMPLEMENTED` |
| Non-terminal escalation transport | `SEMANTICALLY_FROZEN_NOT_YET_REALIZED` |
| Post-terminal retry dispatch / new Run model | `RESERVED_EXTENSION` |

Future implementation requires a concrete host/consumer representation gate.
That gate must preserve this contract without expanding Runtime authority.
