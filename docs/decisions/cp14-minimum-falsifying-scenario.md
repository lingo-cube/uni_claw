# CP14_MINIMUM_FALSIFYING_SCENARIO_CONTRACT

> Scenario: `SC-CP14-MVS-001`
> Capability: `CC-04 — Intent Semantic Envelope with Dual-Mode Projection`
> Development lane: `CAPABILITY_DELIVERY_FAST`
> Status: `VALIDATED`
> Checkpoint: `CP14_MINIMUM_VERTICAL_SLICE_FAST_LOOP`
> Rebased on:
> `CP14_TYPE_LEVEL_REPRESENTATION_MINIMUM_VERTICAL_SLICE_FAST_LOOP_RESULT`
> = `VALIDATED`

## Reconciliation of the Superseded Purchase

The former parent purchase is withdrawn and remains
`SUPERSEDED_BY_TYPE_LEVEL_SUBGAP_REVIEW`. It represented both modes with a mode
enum plus one concrete `Plan`, and carried scope, constraints, and completion as
opaque strings. That shape could encode an impossible state:

```text
Representation = OPEN_WORLD_TYPE_LEVEL
+ only concrete Plan payload
```

The rebased purchase does not restore that shape. The validated
`TypeLevelTraversalSpecification` is now the only production representation for
`OPEN_WORLD_TYPE_LEVEL`; the existing `Plan` remains the production
representation for `CLOSED_WORLD_CONCRETE`.

## Frozen Semantic and Architecture Receipts

- CP-14 remains covered by admitted RM-11.
- CC-04 remains the selected capability semantic envelope.
- `CP14_ARCHITECTURE_CHALLENGE_RESULT` remains
  `ARCHITECTURE_FIT_CONFIRMED`.
- Authoritative Intent, desired state, permissions, and any explicit method
  constraint originate with the caller/task source.
- The bounded projection occurs before `Agent.RunAsync` and does not invoke or
  become part of Agent.
- Agent retains Goal, GoalEvidence, world interpretation, and final completion
  authority.
- `StateMachinePressure` remains `NO_STATE_PRESSURE`.

The following distinctions remain normative:

```text
Intent != Goal
Goal != Execution Method
Plan != Reality
TaskScope != ConcreteWorkInventory
TypeLevelTraversalSpecification != ConcreteFutureRoute
Specification != Observation / Progress / GoalEvidence / AuthorizationReceipt
```

## Rebased Production-Shaped Boundary

The parent slice requires one immutable caller-side semantic envelope in the
existing `Planning/` area:

```text
IntentSemanticEnvelope
  - Resolved
      Intent: string
      Goal: existing Goal
      Representation: IntentExecutionRepresentation

  - Insufficient
      Intent: string
      Reason: string

IntentExecutionRepresentation
  - ClosedWorldConcrete
      Plan: existing Plan

  - OpenWorldTypeLevel
      Specification: validated TypeLevelTraversalSpecification
```

There is no representation enum and no nullable Plan/specification pair. The
variant type is the discriminator and owns exactly one truthful payload.

The bounded projection exposes exactly two pure deterministic overloads:

```text
Project(Intent, Goal, IntentExecutionRepresentation)
  → Resolved

Project(Intent, InsufficientReason)
  → Insufficient
```

Both consume already-authoritative structured input. They validate only null,
empty, and structural completeness. They do not parse Intent, infer a desired
state, select a provider, choose an execution mode, generate a Plan, generate a
route, or invoke Agent.

The two overloads avoid a nullable input matrix in which missing Goal,
representation, or reason could be silently defaulted. The caller must
explicitly supply either one complete resolved projection or one explicit
insufficiency receipt.

## Scenario A — Same Intent, Different Current World

### Given

- Intent: `确保 WiFi 已开启`.
- The caller supplies the same authoritative `Goal` and one
  `ClosedWorldConcrete` representation carrying the existing bounded WiFi
  `Plan`.
- World A initially observes WiFi ON.
- World B initially observes WiFi OFF.

### Oracle

```text
Project resolved closed-world input
→ Resolved(Intent, Goal, ClosedWorldConcrete(exact Plan))

World A:
  existing Agent initial GoalEvidence is satisfied
  → Completed
  → zero unnecessary action

World B:
  initial GoalEvidence is unsatisfied
  → caller extracts the exact Plan from the closed variant
  → existing Agent executes and obtains fresh WiFi ON evidence
  → Completed
```

The envelope does not create mandatory work, and neither Intent nor Plan is
completion evidence.

## Scenario B — Open-World Type-Level Projection

### Given

- Intent: traverse safe Settings entries within the declared depth.
- The caller supplies one authoritative `Goal` and the already-validated
  `TypeLevelTraversalSpecification`.
- Scope, categories, depth, safety, completion requirement, and entry are
  explicit in that specification.
- Concrete pages, targets, coordinates, route, and work inventory remain
  unknown.

### Oracle

```text
Project resolved open-world input
→ Resolved(Intent, Goal, OpenWorldTypeLevel(exact Specification))
→ no Plan exists in the open-world variant
→ no concrete work is fabricated
→ different Observations may later produce different inventories
  without changing the envelope or specification
```

This parent slice proves production-shaped semantic composition only. It does
not add the downstream matching, discovery, enforcement, or execution adapter
that a later usable open-world traversal slice may require.

## Scenario C — Explicit Closed-World Route

### Given

- The caller explicitly supplies a bounded concrete route and method-specific
  constraints as an existing `Plan`.
- Goal meaning remains distinct from that route.

### Oracle

```text
Project explicit method-constrained input
→ Resolved(ClosedWorldConcrete(exact caller Plan))
→ exact Plan identity/value is preserved
→ no silent conversion to open-world discovery
→ route mismatch remains execution/world-correspondence failure
```

## Scenario D — Unresolved / Insufficient Intent

### Given

- Intent: `处理一下 WiFi`.
- Desired state, authoritative Goal, and execution representation are not
  supplied.
- The caller supplies an explicit non-empty insufficiency reason.

### Oracle

```text
Project(Intent, InsufficientReason)
→ Insufficient(Intent, Reason)
→ no Goal
→ no Plan
→ no TypeLevelTraversalSpecification
→ no executable projection
→ zero Agent invocation / Observation / dispatch / mutation
```

The projection never invents WiFi ON/OFF, scope, permission, completion,
execution mode, Plan, or clarification behavior.

## Determinism and Structural Assertions

- Equal authoritative input references/values produce equal projection values.
- `Resolved` always has exactly one `IntentExecutionRepresentation` variant.
- `ClosedWorldConcrete` has exactly one existing `Plan` and no type-level
  specification.
- `OpenWorldTypeLevel` has exactly one validated specification and no `Plan`.
- `Insufficient` structurally exposes only Intent and Reason.
- The exact caller Plan and exact caller specification are preserved; neither is
  copied into another representation.
- `Agent.RunAsync(Goal, Plan, runId, CancellationToken)` remains unchanged.
- Agent source does not reference or invoke `IntentSemanticEnvelope`.
- No projection or unresolved-intent state is added to `RunState`.

## Architecture Fit Check

Result: `ARCHITECTURE_FIT_CONFIRMED`.

```text
Authoritative caller/task source
→ bounded pure Intent semantic projection
→ IntentSemanticEnvelope
   ├─ ClosedWorldConcrete(existing Plan)
   └─ OpenWorldTypeLevel(validated TypeLevelTraversalSpecification)
→ caller consumes the resolved variant
→ existing Agent boundary where applicable
```

| Responsibility | Frozen authority |
|---|---|
| Authoritative Intent, desired state, permissions, explicit method | Caller / task source |
| Structural preservation and resolved/insufficient projection | Bounded upstream semantic boundary |
| Closed concrete execution hypothesis | Existing Plan |
| Open type-level task specification | Validated TypeLevelTraversalSpecification |
| Run Goal, world interpretation, GoalEvidence, final completion | Agent |
| Page-local state | Container |
| Local select/check/execute/fresh verify | Traversal |
| Observation and dispatch reporting | Environment |

No mutable-state owner, decision authority, dependency direction, safety
authority, external-world authority, or architecture invariant changes.

## Revised Proven-Minimal Parent Production Purchase

### Production API

Add exactly one production file:

- `src/UniClaw.Runtime/Planning/IntentSemanticEnvelope.cs`

Add exactly six immutable record types:

1. `IntentSemanticEnvelope` — abstract union root;
2. `IntentSemanticEnvelope.Resolved` — sealed variant;
3. `IntentSemanticEnvelope.Insufficient` — sealed variant;
4. `IntentExecutionRepresentation` — abstract union root;
5. `IntentExecutionRepresentation.ClosedWorldConcrete` — sealed variant;
6. `IntentExecutionRepresentation.OpenWorldTypeLevel` — sealed variant.

Add exactly seven public immutable values:

- `Resolved`: `Intent`, `Goal`, `Representation`;
- `Insufficient`: `Intent`, `Reason`;
- `ClosedWorldConcrete`: `Plan`;
- `OpenWorldTypeLevel`: `Specification`.

Add exactly two pure deterministic production overloads:

- `IntentSemanticEnvelope.Project(string intent, Goal goal,
  IntentExecutionRepresentation representation)`;
- `IntentSemanticEnvelope.Project(string intent, string insufficientReason)`.

No new enum is needed. Constructors/factories validate non-null and non-empty
inputs. They contain no parsing, inference, mode selection, planning, routing,
world access, Agent invocation, or mutable state.

Existing production files modified: 0.
Existing Goal/Plan/TypeLevelTraversalSpecification fields modified: 0.
Agent/Container/Traversal/Environment/Recovery control-flow delta: 0.

### Test Purchase

Add:

- `tests/UniClaw.Runtime.Tests/Unit/IntentSemanticEnvelopeTests.cs`;
- `tests/UniClaw.Runtime.Tests/Scenario/Cp14IntentSemanticEnvelopeScenarioTests.cs`.

Allow only minimal assertions in
`tests/UniClaw.Runtime.Tests/Architecture/ArchitectureGuardTests.cs` if needed to
prove that Agent does not depend on the upstream envelope and its RunAsync
signature remains unchanged.

Required proof:

- Scenario A WiFi ON/OFF closed-world behavior remains unchanged;
- Scenario B exact validated open-world specification is composed without Plan,
  concrete route, or inventory;
- Scenario C exact caller Plan is preserved;
- Scenario D insufficient input exposes no executable projection;
- projection is deterministic and variants are immutable;
- build, targeted tests, architecture guards, full regression, consistency, and
  OpenSpec repository strict validation pass.

## Delta Budget

```text
Production files added: 1
Production files modified: 0
New immutable record types: 6
New public immutable values: 7
New pure production overloads: 2
New enums/interfaces/components/engines: 0
New mutable state: 0
Goal/Plan/TypeLevelTraversalSpecification fields: 0
Agent/Container/Traversal/Environment/Recovery delta: 0
Ownership delta: NONE
Authority delta: NONE
Dependency-direction delta: NONE
Architecture-invariant delta: NONE
Safety-semantic delta: NONE
StateMachinePressure: NO_STATE_PRESSURE
```

## Forbidden / Deferred Boundary

- no NL parser, Intent understanding algorithm, prompt, LLM/VLM/provider;
- no Planner, Compiler component, Task IR hierarchy, Graph, FSM, or new
  RunState;
- no route generation, default execution-mode selection, or clarification UX;
- no hard-coded WiFi or Settings production mapping;
- no type-level matching, candidate discovery, constraint enforcement,
  completion, progress, inventory, or execution adapter;
- no GoalEvidence, Agent, Container, Traversal, Environment, Recovery, Plan, or
  TypeLevelTraversalSpecification modification;
- no ownership, authority, dependency, safety, or invariant change;
- no U2 implementation.

## Human Implementation Gate

Repository truth contains Human authorization for the validated type-level
representation sub-slice only. It contains no Human receipt authorizing this
rebased parent file, six record types, seven values, two projection overloads,
or parent tests.

The parent Fast Loop therefore stops exactly once at:

```text
HUMAN_IMPLEMENTATION_GATE_REQUIRED
```

After Human authorization of exactly this revised purchase, resume the same
`CP14_MINIMUM_VERTICAL_SLICE_FAST_LOOP` checkpoint and auto-continue through
implementation, closed/open/insufficient Scenario tests, architecture guards,
regressions, diagnosis, repair, and full validation.

STOP.
