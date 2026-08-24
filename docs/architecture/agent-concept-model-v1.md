# Agent Concept Model v1

> Status: `FROZEN_SUBORDINATE_BASELINE`
> DocumentType: `SUBORDINATE_ARCHITECTURE_SEMANTIC_MODEL`
> Authority: `PROJECT_LEADER_AGENT_CONCEPT_MODEL_V1_ALIGNMENT`
> Ratified: 2026-08-21 via `agent-concept-model-v1-alignment`
> Governing baselines:
> [UniAgent Architecture v1](uniagent-architecture-v1-core-development-guide.md) and
> [UniAgent Protocol v1](uniagent-protocol-v1-consolidation-design.md)
> Scope: Agent terminology, ownership, lifecycle, and mapping to current
> DSH / UniClaw objects. Architecture v1 and Protocol v1 remain the governing
> top-level baselines.

## 1. Purpose

This model consolidates the minimum Agent concepts needed to describe:

- UniAgent supervisory intelligence;
- RuntimeAgent bounded execution intelligence;
- Directive, Plan, Decision, Result, and Goal Evaluation;
- Session continuity and disposable Agent loops;
- Observation, Fact, Evidence, Trace, Span, and Event;
- the mapping from these concepts to current DSH and UniClaw objects.

The result is a stable v1 vocabulary, not a new framework or Data Plane design.
The collision resolutions in §12 are ratified interpretations of the governing
baselines; they add no wire field, Runtime behavior, or reserved extension.

## 2. v1 model

```text
Session (continuity and correlation)
  │
  ├── Primary Goal + Session Context
  │          │
  │          ▼
  │     UniAgent Loop
  │     - interpret intent
  │     - maintain supervisory plan
  │     - issue directives
  │     - evaluate outcomes
  │          │
  │          ▼
  │     Directive
  │          │
  │          ▼
  │     RuntimeAgent Run / Loop
  │     - observe and reconcile
  │     - locally decompose
  │     - decide and act
  │     - verify and recover
  │          │
  │          ▼
  │     Runtime Outcome
  │          │
  └──────────┴──► UniAgent Decision / Goal Evaluation
```

The Agent object is not the continuity root. Agent loops may end and their
in-memory instances may be destroyed. Continuity is provided by Session,
Context, Memory, and append-oriented references to decisions, runs, facts,
evidence, and traces.

## 3. Agent roles

### 3.1 UniAgent

**Position:** supervisory, orchestration, and goal-evaluation intelligence.

UniAgent owns:

- interpretation of the Primary Goal in Session context;
- an abstract supervisory plan;
- issuance of bounded Directives to RuntimeAgent;
- supervisory Decisions such as continue, retry, redirect, recover, stop, or
  request operator judgment;
- acceptance and interpretation of RuntimeAgent-produced outcomes;
- final Goal Evaluation across completion and satisfaction dimensions.

UniAgent does not own:

- physical device actions;
- RuntimeAgent WorldBelief, grounding, or UI execution state;
- fresh verification or Runtime-local recovery details;
- a permanently resident stateful Agent instance.

This is aligned with Architecture v1's `supervisory autonomy`. A UniAgent
Decision may produce a new Directive, but it may not directly mutate Runtime
belief or physical state.

### 3.2 RuntimeAgent

**Position:** intelligent execution inside a concrete Runtime boundary.

RuntimeAgent owns:

- accepting or rejecting a bounded Directive;
- local decomposition of the execution goal;
- Runtime-local planning as a revisable hypothesis;
- reconciliation, semantic Decision, grounding, action authorization,
  execution, verification, and bounded recovery;
- Run-local state, WorldBelief, GoalEvidence, and terminal Run outcome;
- escalation when the problem cannot be closed within its authority.

RuntimeAgent may know the execution goal, but is primarily driven by a bounded
Directive. It may dynamically expand an abstract intent into concrete actions;
it is not limited to replaying a precompiled step list.

RuntimeAgent must not:

- redefine the Primary Goal;
- claim final user satisfaction;
- treat Plan as truth;
- let an external capability bypass grounding, verification, or execution
  authority.

## 4. Directive, Plan, Action, and Trace

| Concept | v1 meaning | Ownership and boundary |
|---|---|---|
| Directive | One intentional, bounded request describing what RuntimeAgent should do next | Produced by UniAgent; accepted or rejected by RuntimeAgent |
| StrategyDirective | One already-resolved bounded request adding typed scope, abstract exploration intent, constraints, evidence criteria, and adaptation permissions | Produced by UniAgent; admitted once at Run start and interpreted, never originated, by RuntimeAgent |
| Supervisory Plan | UniAgent's expected route, rules, or sequencing across supervisory Decisions | UniAgent-internal; not a Surface A wire field |
| Runtime Plan | RuntimeAgent's local execution hypothesis | RuntimeAgent-internal; revisable after fresh observation |
| Action | A concrete operation actually authorized and dispatched in the Runtime | RuntimeAgent / Kernel execution boundary |
| Trace | The correlated record of what reasoning and execution actually occurred | Producer-scoped records linked through Session and correlation references |

The normative separation is:

```text
Primary Goal
  → UniAgent Decision
  → Directive / StrategyDirective
  → Runtime-local Plan
  → Concrete Action
  → Trace / Evidence
  → Runtime Outcome
  → Goal Evaluation
```

An abstract Supervisory Plan may exist without being serialized across the
Runtime Protocol. Protocol v1 Surface A preserves the bounded Directive and
additively admits a typed StrategyDirective at Run start. Neither carries a
`plan`, `planRef`, route, action sequence, or mid-run redirection; those remain
separately gated protocol changes.

## 5. Decision

A Decision is an Agent's evidence-grounded judgment in a particular context.
It is neither an Action nor a Result.

UniAgent Decisions include:

- issue or revise a Directive;
- continue, retry, or terminate supervisory work;
- accept a Runtime Outcome;
- request operator judgment;
- produce or supersede a Goal Evaluation.

RuntimeAgent Decisions include:

- choose the next locally valid action;
- retry observation or grounding;
- enter bounded recovery;
- continue or complete a Run;
- fail closed or raise an escalation need.

A Decision record should reference its basis where available, but this model
does not freeze a Decision DTO or persistence schema. A UniAgent Decision never
becomes Runtime truth merely because it is recorded in Session.

## 6. Result and evaluation layers

One boolean must not represent transport acceptance, Runtime execution, and
user-goal satisfaction at the same time.

### 6.1 Request acceptance

`Accepted` or `Rejected` reports whether RuntimeAgent accepted a Directive and
created a Run. Rejection is not a failed Run because no Run exists.

### 6.2 Runtime Outcome

Runtime Outcome is a RuntimeAgent-produced fact about the Run lifecycle.

Current v1 realization:

- `Completed`;
- `Failed`.

Conceptually valid but not currently realized as Run terminal states:

- `Cancelled`;
- `Interrupted`.

`AssistanceRequired` is not frozen as a terminal Run state. Protocol v1 already
requires non-terminal Escalation to remain distinct from TerminalOutcome. The
recommended v1 interpretation is:

```text
AssistanceRequired = non-terminal escalation / supervisory disposition
                   ≠ completed Runtime Outcome
```

If a future implementation ends one disposable RuntimeAgent loop while keeping
the Run logically open for adjudication, that loop-level outcome must be named
separately from the Run terminal outcome.

Failure is still a complete and truthful lifecycle outcome. `Failed` does not
mean that the lifecycle record is incomplete.

### 6.3 Goal Evaluation

Goal Evaluation is produced by UniAgent and does not rewrite the Runtime Outcome.

```text
GoalEvaluation
  ├─ Completion  : Completed | Incomplete | Indeterminate
  └─ Satisfaction: Satisfied | Unsatisfied | Indeterminate
```

Example:

```text
Runtime Outcome: Completed
Goal Evaluation:
  Completion   = Completed
  Satisfaction = Unsatisfied
```

An operator may append an evaluation or supersede the latest Goal Evaluation
projection. The operator must not alter the original Runtime Outcome, evidence,
or prior evaluation record.

## 7. Observation, Fact, Evidence, and belief

| Concept | v1 meaning | Existing boundary clarification |
|---|---|---|
| Observation | Raw information produced by a perception or environment interaction | Environment produces it; RuntimeAgent consumes and reconciles it |
| Fact | An append-oriented assertion available to an Agent | Facts are records, not a mutable current-state object |
| Evidence | Information supporting a Fact, Decision, or Evaluation | Evidence does not automatically become truth or authority |
| WorldBelief | RuntimeAgent's current best, revisable judgment about the world | A projection/reconciliation result, not an append-only Fact log |
| GoalEvidence | RuntimeAgent-kernel evidence used to decide Run completion | A specialized Runtime concept, not a synonym for all Evidence |

Facts are appended rather than mutated:

```yaml
- id: F1
  assertion: wifi_enabled = false
- id: F2
  assertion: wifi_enabled = true
```

The current fact projection may select F2 as the latest supported assertion,
but F1 remains part of history. Fact schema, validity intervals, supersession,
indexing, storage, and event sourcing remain Data Plane concerns and are not
defined here.

## 8. Trace, Span, and Event

| Concept | Minimum v1 definition |
|---|---|
| Trace | One correlated reasoning or execution process |
| Span | One bounded local activity within a Trace |
| Event | One instantaneous occurrence within a Trace or Span |

RuntimeAgent traces primarily cover Action, Verification, Recovery, and Runtime
state changes. UniAgent traces primarily cover Decision, Directive, Plan
adjustment, and Goal Evaluation.

The two trace domains may be correlated by Session, Directive, Run, Decision,
and Evidence references without becoming one shared mutable trace object. This
model does not freeze trace storage, reference DTOs, sampling, replay format, or
event-sourcing behavior. The active trace-capture OpenSpec remains a separate,
not-yet-implemented gate.

## 9. Lifecycle and cardinality

The v1 lifecycle rules are:

1. Session may span multiple conversational turns and Agent loop activations.
2. A conversational turn is not necessarily a Session.
3. Agent lifecycle is independent from Session lifecycle.
4. A RuntimeAgent is a bounded specialist Agent and may be described as the
   UniAgent's execution SubAgent without introducing a generic multi-agent graph.
5. A RuntimeAgent Run has its own complete lifecycle and authoritative outcome.
6. Agent instances may be destroyed after their loop or Run lifecycle ends.
7. Continuity comes from Session, Context, Memory, and correlated records, not a
   permanently alive Agent object.
8. Architecture v1 still defaults to `1 Session / 1 Primary Goal / 1 Primary Run`.

The phrase “one UniAgent loop may start one or more RuntimeAgent loops” is safe
in current v1 only when `loop` means repeated activations or bounded internal
cycles before the same Primary Run reaches a terminal outcome. Once a Run is
`Completed` or `Failed`, executing another Directive requires a new Run model;
Multi-Run, SubRun, BranchRun, and concurrent RuntimeAgent instances remain
Reserved Extensions. UniAgent may record a Retry Decision or candidate revised
Directive after failure, but current v1 does not dispatch it as a second Run.

## 10. Mapping to current DSH / UniClaw objects

| Concept | Current object or surface | Classification | Notes |
|---|---|---|---|
| Session | DSH `Session` invocation context | `PARTIALLY_ALIGNED` | Compatible host implementation; UniClaw-side Session contract is not formalized |
| Primary Goal | No single current repository DTO | `MISSING_CONTRACT` | Must not be equated automatically with Runtime `SemanticGoalInput` |
| UniAgent | DSH-hosted outer Agent role | `PARTIALLY_ALIGNED` | Architectural role exists; no UniClaw-owned UniAgent domain model |
| Directive | `RunStartRequest` / `run.start` / `uniclaw-run-goal` | `ALIGNED` | Current four-field Surface A realization |
| Directive execution target | `SemanticGoalInput` | `ALIGNED_WITH_NAMING_SPLIT` | This is a bounded Runtime execution goal, not necessarily the full Primary Goal |
| Supervisory Plan | DSH outer-Agent reasoning/context | `NOT_FORMALIZED` | No protocol field or durable plan record is frozen |
| Runtime Plan | Runtime-local planning and traversal hypotheses | `ALIGNED` | Must remain revisable and evidence-bound |
| RuntimeAgent | `UniClaw.Runtime.Agent` plus Runtime graph | `ALIGNED` | Legacy class name `Agent` means RuntimeAgent inside the Runtime boundary |
| Run lifecycle entry | DriverHost `RunExecutionCoordinator` and `run.start` | `ALIGNED` | DriverHost owns run identity and execution-side lifecycle coordination |
| Runtime Outcome | `RunState`, `RunCompleted` / `RunFailed`, `GoalEvidence` | `PARTIALLY_ALIGNED` | Completed/Failed realized; Cancelled/Interrupted not realized |
| Runtime progress/result | `RuntimeEvent` and `RunSnapshot` projections | `ALIGNED` | Producer-derived read models, not a second truth store |
| UniAgent Decision | [minimum semantic contract](uniagent-decision-goal-evaluation-minimum-contract.md) | `FROZEN_CONTRACT_NOT_IMPLEMENTED` | Producer/reference/append semantics frozen; no DTO/store exists |
| RuntimeAgent Decision | Runtime semantic decisions and projected decision events | `PARTIALLY_ALIGNED` | Multiple concrete forms; no generic Decision base type is required |
| Goal Evaluation | [minimum semantic contract](uniagent-decision-goal-evaluation-minimum-contract.md) | `FROZEN_CONTRACT_NOT_IMPLEMENTED` | Completion/satisfaction and supersession semantics frozen; no DTO/store exists |
| Observation | Runtime `Observation`, produced through `IEnvironment` | `ALIGNED` | Evidence, not semantic truth |
| Fact | Session append-fact model and producer records | `PARTIALLY_ALIGNED` | Minimum semantics exist; general schema intentionally absent |
| Evidence | `GoalEvidence`, `EvidenceRef`, traps, observations, artifacts | `ALIGNED_WITH_SPECIALIZATIONS` | Preserve each producer's authority and type-specific meaning |
| Runtime Trace | `RuntimeTraceRecorder`, `RuntimeEvent`, observability spans | `PARTIALLY_ALIGNED` | No unified persistence/replay contract |
| UniAgent Trace | DSH/session-side activity records | `MISSING_CONTRACT` | Decision/Directive/Evaluation trace vocabulary is not formalized |
| Human judgment | Operator capability / assistance path | `PARTIALLY_ALIGNED` | Non-terminal supervisory escalation transport remains reserved |

## 11. Scenario validation

### SC-A — Runtime Completed, Goal Unsatisfied

Given one Session-level Primary Goal and a bounded Runtime Directive, when
RuntimeAgent truthfully completes its Run from GoalEvidence but UniAgent finds
that the broader user constraints or expected quality are not satisfied, then:

- Runtime Outcome remains `Completed`;
- Goal Evaluation records `Completion = Completed` and
  `Satisfaction = Unsatisfied`;
- neither evaluation nor operator judgment rewrites RunState or GoalEvidence.

**Result:** `SEMANTICALLY_VALIDATED`; Goal Evaluation has a frozen semantic
contract but remains `NOT_IMPLEMENTED` at representation level.

### SC-B — Runtime Failed, revised Directive

Given a RuntimeAgent Run that fails after bounded local recovery, when UniAgent
uses the failure evidence to revise its next Directive, then:

- the original `Failed` outcome remains append-oriented history;
- the Primary Goal remains unchanged;
- the candidate Directive contains no physical actions or precompiled plan;
- UniAgent may record the Retry Decision and candidate Directive;
- current v1 does not dispatch that Directive after terminal failure because an
  independent second Run, SubRun, or BranchRun is not authorized.

**Result:** `DECISION_SEMANTICS_VALIDATED_EXECUTION_RESERVED`.

### SC-C — AssistanceRequired and operator adjudication

Given RuntimeAgent cannot close an uncertainty locally, when it requires
supervisory or operator judgment, then:

- `AssistanceRequired` is a non-terminal Escalation disposition;
- no `Completed` or `Failed` outcome is fabricated;
- RuntimeAgent retains grounding, action authorization, verification, and final
  terminal-outcome authority;
- operator input appends its own Decision/Evaluation record.

**Result:** `SEMANTICALLY_VALIDATED_NOT_TRANSPORT_REALIZED`; the current
capability-assistance poll/resolve path is not claimed as UniAgent non-terminal
escalation transport.

## 12. Collision and supplement inventory

### C-1 — Primary Goal versus Runtime execution goal

The current wire field named `goal` maps to `SemanticGoalInput`. The v1 model
model introduces a richer Session-level Primary Goal. These must be treated as
two levels:

- `Primary Goal`: interpreted and evaluated by UniAgent;
- `Directive Goal` or `Execution Goal`: bounded target delegated to RuntimeAgent.

No code rename is required to freeze the conceptual distinction.

### C-2 — Abstract Plan versus Directive contract

Architecture permits UniAgent to plan, but Protocol v1 Surface A carries
bounded start requests, not a Plan. The four-field Directive remains frozen;
the additive StrategyDirective carries typed abstract approach and boundaries
for one Run without serializing the Supervisory Plan. A Plan wire field or
mid-Run redirection requires a later buyer and protocol gate.

### C-3 — Runtime completion versus Goal completion

RuntimeAgent owns Run completion from GoalEvidence. UniAgent owns evaluation of
the Session-level Primary Goal. Goal Evaluation may disagree with a successful
Runtime Outcome without becoming a second Runtime truth engine.

### C-4 — `AssistanceRequired` versus terminal outcome

Treating `AssistanceRequired` as an ordinary terminal result would collapse the
Protocol v1 distinction `Escalation != TerminalOutcome`. Keep it non-terminal in
v1, or introduce a separately named loop-level disposition in a future gate.

### C-5 — Observation producer

RuntimeAgent produces Runtime facts and outcomes, but Environment/perception
produces Observation evidence. RuntimeAgent owns reconciliation and belief, not
the raw observation's authorship.

### C-6 — Append-only Fact versus mutable belief

Fact records are append-oriented. WorldBelief and latest-fact summaries are
revisable projections. They must not be collapsed into one mutable Fact object.

### C-7 — Agent Loop versus Run

An Agent Loop is a behavioral/lifecycle activity; a Run is the authoritative
Runtime execution lifecycle and correlation identity. They are related but not
synonyms. Multiple pre-terminal cycles inside one Run do not authorize
Multi-Run; a post-terminal retry requires a separately authorized new Run model.

### C-8 — RuntimeAgent as SubAgent

RuntimeAgent may be called a SubAgent only in the fixed UniAgent-to-RuntimeAgent
specialist relationship. This does not open generic agent spawning, agent graphs,
SubRun, BranchRun, or multi-agent scheduling.

### C-9 — Operator override

Operator judgment may supersede the current Goal Evaluation projection, but it
must append its own decision/evaluation record and preserve Runtime Outcome and
earlier evaluations.

## 13. v1 freeze boundary

The following semantics are frozen by the approved alignment:

- UniAgent and RuntimeAgent role division;
- disposable Agent lifecycle with Session-owned continuity;
- Primary Goal versus bounded Runtime execution goal;
- Directive, Supervisory Plan, Runtime Plan, Action, Decision, and Trace
  separation;
- Runtime Outcome versus Goal Evaluation;
- Goal Evaluation as completion plus satisfaction;
- Observation, append-oriented Fact, Evidence, WorldBelief, and GoalEvidence
  separation;
- RuntimeAgent-as-specialist-SubAgent without opening generic multi-agent scope.

The following remain outside this freeze:

- user-level Task semantics;
- Session, Run, SubRun, and Decision database schemas;
- Data Plane and event sourcing;
- Fact, Evidence, and Trace reference schemas;
- Memory storage and compression;
- DSH API and UI realization;
- Agent Hook implementation;
- multi-run, sub-run, branch-run, and generic multi-agent orchestration;
- terminal cancellation/interruption semantics;
- non-terminal escalation transport.

## 14. Downstream implementation routing

The semantic model is complete. Downstream work must remain buyer-driven:

1. `UniAgentDecision` and `GoalEvaluation` now have a frozen
   [minimum semantic contract](uniagent-decision-goal-evaluation-minimum-contract.md);
   any DTO/store/host representation requires a later gate;
2. Session representation, persistence, Data Plane, DSH UI, and operator transport remain
   separate buyers;
3. cancellation/interruption, non-terminal escalation transport, Multi-Run,
   SubRun, BranchRun, and generic multi-agent orchestration require fresh gates;
4. any implementation must preserve the authority and lifecycle distinctions in
   this model and its governing baselines.
