## Context

See `proposal.md` for motivation. The relevant authority baselines are UniAgent Architecture v1, Agent Concept Model v1, the consolidated UniAgent Protocol v1 design, and Runtime Architecture Contract I-1 through I-14.

Current execution has two disconnected layers:

1. Surface A admits only `RunStartRequest { goal, objects, capabilities, device }`.
2. RuntimeAgent's completed Phase 1–4 capabilities can represent Directive, ExecutionHypothesis, RuntimeDecision/Reconciliation, and HypothesisAdaptation, but they are not driven by a UniAgent-authored abstract strategy contract.

The current Agent execution spine remains:

```text
Agent.RunOpenWorldAsync
  -> Agent authorizes bounded execution
  -> FSM owns lifecycle transitions
  -> Traversal performs concrete execution
  -> Agent verifies GoalEvidence
  -> FSM reaches Completed or Failed
```

The Strategy Contract must enter before this spine and must not wrap it in a RuntimeAgent-owned Multi-Run loop. The separate `runtime-agent-pre-terminal-cycle-contract` change may later provide an Agent-owned seam for same-Run reasoning, but this change neither depends on its implementation nor absorbs its authority.

## Goals / Non-Goals

**Goals:**

- Carry one already-resolved, bounded UniAgent strategy into RuntimeAgent at Run admission.
- Let RuntimeAgent validate and interpret that strategy into world-local execution intent and hypotheses.
- Bound reconciliation and adaptation without weakening Agent, FSM, Traversal, GoalEvidence, or terminal ownership.
- Support generic exhaustive-scope and criterion-directed exploration without application-specific knowledge.
- Preserve the existing `run.start` wire contract.

**Non-Goals:**

- Parse a user request or generate user-level strategy in RuntimeAgent.
- Transfer UniAgent's Supervisory Plan, route, action sequence, or executable predicate.
- Add mid-Run strategy replacement, a Guidance-plane transport, or RuntimeAgent-to-UniAgent invocation.
- Implement the Agent-owned pre-terminal cycle seam.
- Add Android Settings logic, selectors, labels, routes, or knowledge.
- Modify production code in the design phase.

## Decisions

### 1. Treat this as a new protocol-gated OpenSpec change, not a top-level authority change

The change is additive at the transport level but not merely an internal model addition. Protocol v1 freezes current Surface A to the four-field Directive and reserves strategy messages for a fresh gate. The user request supplies the buyer for investigation and design, so this change prepares that gate.

No new top-level Architecture Decision is required because the existing owner matrix already assigns Primary Goal interpretation, Supervisory Plan, and Directive authorship to UniAgent while allowing RuntimeAgent-local planning and reconciliation. A graduation decision may be created only after approved implementation and verification.

Alternative considered: add only an in-process Runtime model. Rejected because that would bypass the frozen external contract and leave UniAgent with no authoritative way to supply it.

### 2. Extend the Goal plane at Run start; do not open the Guidance plane

Add a distinct operation, provisionally frozen as:

```text
run.strategy.start(StrategyRunStartRequest)
  -> StrategyRunAdmission
```

The request carries one `StrategyDirective` plus the same device-selection concern required to start a Run. Admission either rejects before execution or accepts exactly one Agent-owned Run. The existing `run.start` payload and semantics remain unchanged.

A start-time bounded strategy enriches the execution-goal declaration. It is not mid-Run Guidance and cannot redirect an active Run. This keeps the deferred Guidance plane, non-terminal escalation transport, and strategy-alteration messages out of scope.

Alternative considered: add optional strategy fields to `RunStartRequest`. Rejected because it changes a frozen message and makes version compatibility ambiguous.

### 3. StrategyDirective is a bounded Directive variant, not a Plan

The semantic distinction is:

| Artifact | Owner | Meaning | Forbidden content |
|---|---|---|---|
| Supervisory Plan | UniAgent | User-level decomposition and choice of future bounded work | Runtime ownership or transport in this change |
| Existing Directive | UniAgent | One four-field bounded execution request: WHAT, not HOW | Runtime re-origination, physical steps |
| StrategyDirective | UniAgent | One bounded execution request plus abstract approach, constraints, completion semantics, and adaptation permissions | User-language interpretation, routes, actions, callbacks |
| RuntimeExecutionIntent | RuntimeAgent, internal | World-local interpretation of the accepted strategy | DeviceAction, authorization, FSM transition, completion fact |
| ExecutionHypothesis | RuntimeAgent, internal | Revisable belief about how accepted intent maps to current reality | External strategy mutation or terminal authority |

RuntimeAgent does not generate or revise the StrategyDirective. It only admits it, interprets it, and revises internal hypotheses within its explicit boundary.

### 4. Use typed declarative model objects

External contract objects:

- `StrategyRunStartRequest`
  - `StrategyDirective Strategy`
  - `DeviceSelector Device`
- `StrategyDirective`
  - `StrategyId` and `ContractVersion`
  - `StrategyObjective Objective`
  - `StrategyScope Scope`
  - `ExplorationIntent Exploration`
  - `StrategyConstraintSet Constraints`
  - `StrategyCompletionCriteria Completion`
  - `StrategyAdaptationBoundary Adaptation`
- `StrategyObjective`
  - typed intent such as `ExploreScope` or `InspectMatchesWithinScope`
  - optional typed `SemanticCriterionRef`; never unresolved user prose
- `StrategyScope`
  - semantic entry boundary, allowed descendants/relations, and finite structural limits
- `StrategyConstraintSet`
  - allowed interaction categories, prohibited effects, safety invariants, and finite resource limits
- `StrategyCompletionCriteria`
  - Runtime-verifiable evidence semantics such as exhaustive coverage within scope or inspection of all discovered matches
  - criteria describe required evidence; they do not assert completion
- `StrategyAdaptationBoundary`
  - allowed classes such as `ReconcileBelief`, `RegroundSemanticTarget`, `ReorderPendingWork`, and `ReviseExecutionHypothesis`
  - immutable exclusions for objective, scope, safety, and completion mutation
- `SemanticCriterionRef`
  - stable identifier, version, and required capability identity
  - no code, delegate, selector, or scenario label
- `StrategyRunAdmission`
  - accepted/rejected status, deterministic rejection code, and Run identity/state only when accepted

Internal-only objects:

- `ValidatedStrategy`: normalized immutable admission result.
- `RuntimeExecutionIntent`: a bounded semantic work description suitable for the existing Agent handoff; it is not an action.
- Existing `ExecutionHypothesis`, `RuntimeDecision`, and `HypothesisAdaptation`: reused rather than duplicated.
- `StrategyBoundaryViolation`: a bounded internal reason indicating that revision/escalation is required.

The concrete C# shape should use closed discriminated types or sealed value objects for authority-bearing fields. Arbitrary dictionaries and executable evaluator delegates are not valid wire representations.

### 5. Resolve semantics through generic capability bindings

Criterion references resolve against generic semantic capabilities installed by composition. Runtime core knows only the capability contract and typed result; it does not know Android Settings, security labels, or scenario routes.

Admission fails when a criterion is unknown, version-incompatible, not deterministic enough for the declared completion rule, or not available in the selected runtime composition. RuntimeAgent never converts unsupported prose into a guessed criterion.

Alternative considered: accept a free-form strategy prompt. Rejected because it makes RuntimeAgent the user-level planner and prevents deterministic boundary validation.

Alternative considered: serialize predicate delegates or rule callbacks. Rejected because executable input crosses the trust and authority boundary and cannot form a stable wire contract.

### 6. Execute through one Agent-owned Run

```text
UniAgent
  creates Supervisory Plan privately
  derives one bounded StrategyDirective
        |
        v
RuntimeAgent admission
  validate types, bounds, completion verifiability, capability support
  reject OR freeze ValidatedStrategy
        |
        v
RuntimeAgent interpretation
  create RuntimeExecutionIntent
  seed/reconcile ExecutionHypothesis against WorldBelief
        |
        v
Agent-owned Run
  decide whether continuation/action is allowed
  authorize candidate execution
        |
        v
FSM -> Traversal -> observation/verification -> GoalEvidence -> FSM terminal
```

At no point does RuntimeAgent call Traversal or FSM. If later wired to the Agent-owned pre-terminal cycle seam, RuntimeAgent returns only a bounded reasoning result. Agent alone decides whether that result permits same-Run continuation.

### 7. Protect immutable boundaries and terminal state mechanically

The accepted StrategyDirective is immutable for the Run. RuntimeAgent adaptation may alter only runtime-local hypothesis state and pending semantic intent explicitly listed in `StrategyAdaptationBoundary`.

The following type/API separations are required:

- `RuntimeExecutionIntent` cannot contain or inherit `DeviceAction`.
- Strategy interpretation and adaptation modules receive no Traversal or FSM dependency.
- No RuntimeAgent strategy method returns `RunState.Completed`, `RunState.Failed`, or a completion boolean.
- Completion criteria compile to GoalEvidence requirements; only Agent-owned verification can satisfy them.
- RuntimeAgent has no client dependency capable of calling `run.start` or `run.strategy.start`.
- An accepted admission creates at most one Run identity; terminal state closes the contract.

### 8. Authority proof

| Forbidden edge | Why it is impossible in this design | Required guard/proof |
|---|---|---|
| RuntimeAgent -> Action | Runtime output is `RuntimeExecutionIntent`, never `DeviceAction`; Agent performs independent authorization | Type/dependency guard plus negative authority test |
| RuntimeAgent -> FSM | Strategy modules receive no FSM reference and return no transition command | Dependency guard plus FSM transition tests |
| RuntimeAgent -> Completion | Completion criteria are evidence requirements, not facts; Agent owns GoalEvidence and terminal transition | Tests proving apparent satisfaction cannot complete without GoalEvidence |
| RuntimeAgent -> MultiRun | Strategy admission is start-time, accepts at most one Run, and RuntimeAgent has no start-client dependency | Single-Run correlation test and forbidden dependency guard |
| RuntimeAgent -> user planning | Input is typed and already resolved; unsupported prose/criteria are rejected | Admission tests for unresolved and unsupported intent |
| RuntimeAgent -> scenario knowledge | Runtime core binds opaque generic semantic capabilities only | Source/assembly guard against scenario-specific constants and dependencies |

### 9. Stop-condition evaluation

The design does not require RuntimeAgent planning authority, Agent lifecycle changes, FSM ownership changes, Goal ownership changes, or scenario-specific knowledge. Therefore the architecture investigation may proceed to OpenSpec design.

Implementation remains blocked on explicit human approval because this opens a new Surface A operation. Any future requirement for mid-Run strategy mutation, RuntimeAgent-generated StrategyDirective, direct action encoding, or non-GoalEvidence completion triggers the stop condition and requires a new decision.

## Risks / Trade-offs

- [The name "strategy" may be mistaken for RuntimeAgent planning authority] -> Use the owner-qualified `StrategyDirective` term, reject free-form input, and document that RuntimeAgent only interprets an immutable UniAgent artifact.
- [Generic semantic references can hide scenario logic] -> Require stable capability identity/version, composition ownership, scenario-free Runtime core, and source/dependency guards.
- [Completion criteria can become a shadow completion authority] -> Model them only as GoalEvidence requirements and prohibit completion flags from strategy modules.
- [A new method increases protocol surface] -> Preserve all frozen messages, use additive versioning, and add compatibility tests for old clients.
- [Interaction with pre-terminal adaptation may create an accidental outer loop] -> Permit integration only through an Agent-owned callback/result seam and prove one Run identity from admission to terminal state.
- [Over-generalized model creates speculative framework complexity] -> Initially support only the two bought generic intents: exhaustive exploration within scope and inspection of typed matches within scope; add further intent kinds through later OpenSpec gates.
- [Rejected strategy may provide poor diagnostics] -> Use bounded, stable rejection codes without exposing internal belief or authorizing fallback behavior.

## Migration Plan

1. Obtain explicit human approval to apply this change.
2. Have Luna implement only the approved unchecked tasks, preserving existing `run.start` and Phase 1–4 contracts.
3. Add the new operation behind explicit capability discovery so existing clients remain unaffected.
4. Prove admission, generic interpretation, authority boundaries, and one-Run lifecycle with deterministic scenarios.
5. Have Sol independently review architecture compliance, run relevant guards/tests, and decide whether graduation evidence is sufficient.
6. Roll back by disabling/removing only the new operation and its additive models; no existing wire payload or lifecycle needs migration.
