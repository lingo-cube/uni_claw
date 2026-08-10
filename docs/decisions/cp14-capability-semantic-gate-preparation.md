# CP14_CAPABILITY_SEMANTIC_GATE_PREPARATION_RESULT

> Date: 2026-08-10
> Development lane: `SEMANTIC_DISCOVERY`
> Project Leader: `PROJECT_LEADER_MODEL`
> Workers: `EXECUTION_WORKER_MODEL`
> Authority: `B4_REALITY_MODEL_ADMISSION_RM11_RESULT`
> Scope: Semantic Gate preparation only; no candidate generation or implementation

## Decision Boundary

RM-11 is formally admitted as `ACCEPT_NEW_MODEL` for CP-14. Its required
distinction is:

```text
User Intent != Goal != Execution Method != Concrete Work
```

The current Runtime boundary is:

```text
Agent.RunAsync(Goal, Plan, runId, cancellationToken)
```

This task determines the minimum missing behavior semantics between a high-level
task intent and that existing structured boundary. It does not define classes,
components, ownership, parser algorithms, prompts, providers, or architecture.

## Classification

```text
Classification: CAPABILITY_GAP
SemanticGateDecision: AUTHORIZE_CP14_CAPABILITY_EXPLORATION
HumanGateRequired: YES
HumanGate: HUMAN_AUTHORIZE_CP14_CAPABILITY_CANDIDATE_GENERATION
```

The recommendation authorizes only bounded behavioral candidate exploration. It
does not authorize a Compiler, Planner, FSM, Task IR, Runtime modification,
model/provider selection, or implementation.

## Authoritative Runtime Baseline

### ExistingCapability

The current Runtime already provides the following relevant semantics:

| Area | Existing capability | Boundary |
|---|---|---|
| Run control | `Agent` owns RunState, completion authority, Trace, and final failure authority. | It receives already-structured inputs; it does not understand high-level Intent. |
| Goal | Immutable `Goal` carries an injected `EvidenceEvaluator`; initial and post-action GoalEvidence can prove zero-work completion or completion after work. | Goal meaning is caller-constructed; no Goal construction from Intent exists. |
| Plan | Immutable `Plan` carries caller-supplied `PlanStep` data. Plan is a hypothesis, not world truth. | It represents execution structure, not a preserved Intent or generic representation-selection decision. |
| Closed-world execution | U1 validates concrete structured Goal + Plan execution, including an already-satisfied world that dispatches zero actions and an OFF world that requires bounded work. | This is a caller-preconstructed closed-world input, not a capability that derives it from high-level Intent. |
| Open-world evidence | Optional bounded evaluators can express selected inventory, viewport, grounding, and branch evidence. | These are scenario-specific bounded extensions; they do not form a general open-world type-level task boundary. |
| Ambiguity safety | U1 preserves ambiguous grounding as no dispatch/no completion; GoalEvidence remains authoritative. | There is no high-level ambiguous Intent representation that remains unresolved before Goal/Plan construction. |
| Lifecycle | `Idle → Initializing → Running → Completed | Failed`; completion requires GoalEvidence. | RM-11 does not require a new lifecycle state or transition. |

## RM11ERCoverage

| RM-11 ER | Status | Current evidence | Gap interpretation |
|---|---|---|---|
| `ER-CP14-01` Intent != ExecutionMethod | `GAP` | Goal and Plan are separate, but no Intent boundary exists. | The Runtime cannot preserve high-level meaning separately from the method selected by the caller. |
| `ER-CP14-02` Goal != ExecutionMethod | `PARTIAL` | Agent evaluates GoalEvidence independently and can dispatch zero work when the Goal is already satisfied. | Goal is not derived from Intent and the boundary does not prevent a caller from collapsing Goal and method before invocation. |
| `ER-CP14-03` TaskScope != ConcreteWorkInventory | `PARTIAL` | Bounded branch/viewport evidence can represent discovered work in selected scenarios. | No general Intent-level scope is preserved separately from the concrete inventory or Plan. |
| `ER-CP14-04` TypeLevelTraversalSpecification != ConcreteFutureRoute | `PARTIAL` | Existing Plan is a hypothesis and selected bounded evaluators consume fresh evidence. | No general type-level representation can be selected or passed from a high-level Intent without prebuilding the route. |
| `ER-CP14-05` Preserve OpenWorldMode and ClosedWorldMode | `PARTIAL` | Closed-world concrete Plan is validated; bounded open-world behavior exists in selected capabilities. | The Runtime has no semantic boundary that preserves both modes as intentional representations. |
| `ER-CP14-06` Same Intent may yield different or zero work | `PARTIAL` | U1 proves zero-work and non-zero-work outcomes for related structured Goal + Plan inputs; TE-07 proves route variation. | The variation is supplied by callers/scenarios, not derived or preserved from a high-level Intent. |
| `ER-CP14-07` Ambiguous/incomplete Intent remains unresolved | `GAP` | Ambiguous target grounding is preserved after structured input reaches Runtime. | Missing target, scope, authority, completion, desired state, or method cannot currently be represented as unresolved Intent before Goal/Plan construction. |
| `ER-CP14-08` Completion uses goal/world evidence, not representation exhaustion | `SATISFIED` | Agent alone converts satisfied GoalEvidence to Completed; Plan exhaustion is not completion. | This is existing cross-cutting completion authority and must be preserved by any future boundary. |

Overall: `CAPABILITY_GAP`. The gap is at the semantic input boundary, not in
the existing Agent completion authority or current closed-world execution loop.

## Required Capability Areas

### 1. Intent meaning preservation — GAP

The Runtime has no input semantic that retains the user's high-level task
meaning independently of the eventual Goal, constraints, and execution method.
The missing behavior is preservation, not a required text parser.

### 2. Goal construction — GAP

The current `Goal` is a caller-injected predicate. A future capability must be
able to derive or receive a desired-world outcome from Intent without treating
the actions needed to reach it as the outcome itself.

### 3. Scope extraction / preservation — GAP

Current Plan steps and bounded evaluators do not provide a general Intent-level
scope boundary. The minimum semantics must preserve scope independently from
the items discovered or selected for execution.

### 4. Constraint extraction / preservation — PARTIAL

Current structured inputs can carry concrete action details and selected bounded
constraints through scenario-specific evaluators. The missing piece is a
general semantic distinction between constraints (safety, category, depth,
allowed work, entry, and completion boundaries) and the concrete route.

### 5. Completion meaning construction — PARTIAL

The Agent already owns completion and requires GoalEvidence. What is missing is
the ability for a high-level Intent boundary to supply completion meaning
without deriving it from Plan length or action exhaustion.

### 6. Execution-representation selection — GAP

The caller currently selects the structured input shape. No semantic rule
preserves whether the task intentionally requests a concrete method or leaves
concrete work open for observation.

### 7. `CLOSED_WORLD_CONCRETE` construction — SATISFIED at the existing input boundary

An explicit concrete Plan can be supplied and executed. This satisfies the
existing closed-world execution input, not Intent-to-Plan construction.

### 8. `OPEN_WORLD_TYPE_LEVEL` construction — PARTIAL

The Runtime can consume selected bounded discovery evidence, but it cannot
accept a general type-level task representation produced from high-level Intent
while preserving scope, constraints, completion, and observation-populated
work inventory.

### 9. Ambiguous / incomplete Intent preservation — GAP

The current structured boundary has no unresolved Intent value. The minimum
semantics must allow `UNRESOLVED / INSUFFICIENT` before any missing desired
state, scope, authority, completion criterion, or method is invented.

## MinimumSemanticDelta

The minimum behavioral delta is a semantic boundary that can represent, preserve,
and validate the following distinct meanings before execution input is formed:

```text
High-Level Intent
  → desired Goal meaning
  + Scope
  + Constraints
  + Completion meaning
  + Execution Representation selection
  → existing Goal + Plan boundary only when sufficiently resolved
```

Required behavior:

1. Preserve Intent meaning separately from Goal and Execution Method.
2. Preserve Goal meaning separately from constraints and concrete work.
3. Preserve scope and constraints separately from the observed inventory.
4. Select or retain `CLOSED_WORLD_CONCRETE` when the caller explicitly supplies
   method constraints or concrete targets/actions.
5. Select or retain `OPEN_WORLD_TYPE_LEVEL` when Intent supplies categories,
   boundaries, safety, depth, or completion semantics while concrete instances
   remain unknown.
6. Never discard an explicit caller method constraint in favor of open-world
   discovery.
7. Never require a concrete route when the accepted Intent intentionally leaves
   instances open.
8. Preserve `UNRESOLVED / INSUFFICIENT` for ambiguous or incomplete Intent and
   prevent conversion into executable Goal + Plan semantics until resolved by an
   already-authorized policy.
9. Keep completion tied to Goal/world evidence; representation construction or
   exhaustion cannot prove completion.

This is a behavioral contract only. It deliberately does not specify a type,
class, component, parser, model call, or ownership assignment.

## IntentCompilationHypothesis

```text
SUPPORTED_CAPABILITY_HYPOTHESIS
```

A capability conceptually equivalent to:

```text
Intent
→ Goal + Scope + Constraints + Completion + ExecutionRepresentation
```

would cover the identified gaps if, and only if, it preserves the distinctions
above and can return unresolved semantics without inventing missing meaning.
“Intent compilation” is therefore a useful capability hypothesis, not a
decision to create a Compiler or a compilation architecture. The hypothesis
must remain valid if later implemented by structured input, deterministic
mapping, human-authored data, or another mechanism.

## Execution Representation Semantics

### ClosedWorldMode

`PRESERVED`.

Evidence required for selection:

- Intent or caller input explicitly constrains concrete method, target, action,
  route, expected page, or method-specific limits;
- the method constraint must remain observable as part of the task boundary;
- route mismatch remains an execution/world-correspondence failure, not silent
  permission to reinterpret the task as open-world.

### OpenWorldMode

`PRESERVED`.

Evidence required for selection:

- Intent supplies categories/types, scope, safety constraints, depth/boundary,
  entry, and completion semantics;
- concrete pages, elements, coordinates, route, and work inventory are not
  known before observation;
- fresh observations can populate concrete work within the declared scope;
- discovered work remains constrained by the declared boundary.

### Selection Rule

No universal default is authorized. The future capability must select or retain
the representation from explicit semantic evidence:

```text
explicit method constraint → CLOSED_WORLD_CONCRETE
open category/boundary semantics + unknown instances → OPEN_WORLD_TYPE_LEVEL
ambiguous or incomplete evidence → UNRESOLVED / INSUFFICIENT
```

This is a semantic classification rule, not an implementation algorithm.

## AmbiguitySemantics

For incomplete Intent such as “处理一下 WiFi”, the minimum requirement is:

```text
IntentMeaning: UNRESOLVED / INSUFFICIENT
ExecutableGoal: NOT_CREATED
ExecutionRepresentation: NOT_SELECTED
Authority / desired state / scope / completion / method: NOT_INVENTED
```

The capability must not silently infer whether the user means enable, inspect,
navigate to, enumerate, or otherwise mutate Wi-Fi. It must not create mutation
authority, target scope, completion meaning, or a concrete method from the
incomplete phrase. This task does not design clarification UX or choose the
eventual unresolved-response policy.

## StateMachinePressure

```text
NO_STATE_PRESSURE
```

This follows the admitted RM-11 B4 result. The distinction between resolved,
unresolved, open-world, and closed-world semantics is an input/capability
boundary, not evidence that a new Agent lifecycle state or transition is
required. Existing `RunState` remains unchanged. No `Compiling`, `Planning`,
`Replanning`, or other state is purchased or designed.

If independent capability exploration produces evidence that the current
RunState semantics cannot represent a required boundary without a new lifecycle
state, it must stop and return:

```text
NEW_STATE_PRESSURE_DETECTED
```

with exact evidence. It must not silently introduce state architecture.

## ArchitectureImpact

```text
ArchitectureImpact: NONE_AT_SEMANTIC_LEVEL
```

At this preparation stage, the capability can conceptually coexist with I-1
through I-14 by preserving existing boundaries:

- Agent remains the authority for RunState and completion.
- GoalEvidence remains the completion proof; Plan remains a hypothesis.
- Observation remains evidence, not semantic truth.
- No new mutable owner, decision authority, dependency direction, or runtime
  layer is selected.
- Existing closed-world Plan execution remains valid.
- Open-world semantics are explored as an input/behavior contract only.

Ownership and authority are intentionally not assigned here. If candidate
exploration proves that any invariant, ownership, authority, or dependency
direction must change, return `ARCHITECTURE_PRESSURE_DETECTED` and stop.

## Bounded Capability Exploration Envelope

If the Human Gate authorizes exploration, candidate generation is limited to:

### Allowed question

What is the smallest behavior contract that transforms a resolved high-level
Intent into existing Runtime-consumable Goal/Plan semantics while preserving
RM-11's open/closed representation and ambiguity boundaries?

### Required candidate evidence

- FS-CP14-001: same Intent, different current worlds, different or zero work;
- FS-CP14-002: open-world discovery without pre-enumeration;
- FS-CP14-003: explicit closed-world method constraint;
- FS-CP14-004: ambiguous/incomplete Intent remains unresolved;
- existing GoalEvidence and completion authority remain intact;
- deterministic replay of equal resolved inputs remains possible.

### Candidate acceptance constraints

- Must preserve all RM-11 core ERs.
- Must preserve both `CLOSED_WORLD_CONCRETE` and `OPEN_WORLD_TYPE_LEVEL`.
- Must not invent missing meaning under ambiguity.
- Must not change I-1..I-14, ownership, authority, dependency direction, or
  safety authority.
- Must not add a universal representation default.
- Must not prescribe a Compiler, Planner, Task IR, FSM, parser, prompt,
  LLM/VLM/provider, clarification UX, or Runtime file change.

### Explicitly outside the envelope

Candidate exploration may not generate implementation candidates, class names,
component diagrams, APIs, schemas, parsing algorithms, provider routes, or
OpenSpec Runtime tasks. Those require later authorization after the behavioral
candidate is accepted.

## SemanticGateDecision

```text
AUTHORIZE_CP14_CAPABILITY_EXPLORATION
```

The current Runtime has a bounded semantic capability gap. The gap is real and
is not explained by missing reality evidence; RM-11 is admitted and its core
representation distinction is validated. No architecture or state-machine
pressure is present.

## HumanGateRequired

```text
YES
HUMAN_AUTHORIZE_CP14_CAPABILITY_CANDIDATE_GENERATION
```

The Human Gate authorizes only behavioral candidate exploration inside the
bounded envelope above. It does not authorize:

- Compiler architecture;
- Planner architecture;
- FSM / State Machine;
- Runtime modification;
- model/provider selection;
- implementation;
- a new CP or RM.

## RecommendedNextTask

```text
HUMAN_AUTHORIZE_CP14_CAPABILITY_CANDIDATE_GENERATION
```

After that authorization, the next execution task may be a bounded capability
candidate-generation task. This preparation itself generates no candidates.

## Output

```text
CP14_CAPABILITY_SEMANTIC_GATE_PREPARATION_RESULT

Classification: CAPABILITY_GAP
RM11ERCoverage: ER-CP14-01 GAP; ER-CP14-02 PARTIAL; ER-CP14-03 PARTIAL; ER-CP14-04 PARTIAL; ER-CP14-05 PARTIAL; ER-CP14-06 PARTIAL; ER-CP14-07 GAP; ER-CP14-08 SATISFIED
ExistingCapability: caller-preconstructed Goal + Plan; Agent-owned GoalEvidence completion; closed-world execution; bounded scenario-specific open-world evidence
MissingCapability: Intent meaning, Goal/Scope/Constraint/Completion preservation, representation selection, and unresolved ambiguous-Intent boundary
MinimumSemanticDelta: preserve Intent → Goal + Scope + Constraints + Completion + ExecutionRepresentation without collapsing method into goal or inventing ambiguity
IntentCompilationHypothesis: SUPPORTED_CAPABILITY_HYPOTHESIS
ClosedWorldMode: PRESERVED
OpenWorldMode: PRESERVED
AmbiguitySemantics: UNRESOLVED / INSUFFICIENT; no desired state, scope, authority, completion, or method invented
StateMachinePressure: NO_STATE_PRESSURE
ArchitectureImpact: NONE_AT_SEMANTIC_LEVEL
SemanticGateDecision: AUTHORIZE_CP14_CAPABILITY_EXPLORATION
HumanGateRequired: YES — HUMAN_AUTHORIZE_CP14_CAPABILITY_CANDIDATE_GENERATION
RecommendedNextTask: HUMAN_AUTHORIZE_CP14_CAPABILITY_CANDIDATE_GENERATION
STOP.
```
