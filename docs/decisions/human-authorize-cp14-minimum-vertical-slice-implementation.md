# HUMAN_AUTHORIZE_CP14_MINIMUM_VERTICAL_SLICE_IMPLEMENTATION

> Date: 2026-08-10
> Authority: Human
> Development lane: `CAPABILITY_DELIVERY_FAST`
> Capability: `CC-04 — Intent Semantic Envelope with Dual-Mode Projection`
> Scenario: `SC-CP14-MVS-001`
> Checkpoint: `CP14_MINIMUM_VERTICAL_SLICE_FAST_LOOP`

## Authorization

The Human authorizes exactly the revised parent production/test purchase in
`docs/decisions/cp14-minimum-falsifying-scenario.md`:

- exactly one new production file,
  `src/UniClaw.Runtime/Planning/IntentSemanticEnvelope.cs`;
- exactly six immutable record types;
- exactly seven public immutable values;
- exactly two pure deterministic `Project` overloads;
- zero enums, interfaces, components, engines, or mutable state;
- the bounded unit, Scenario, and architecture verification named by the
  Scenario receipt.

## Frozen Representation Union

```text
IntentSemanticEnvelope
  ├─ Resolved(Intent, Goal, IntentExecutionRepresentation)
  └─ Insufficient(Intent, Reason)

IntentExecutionRepresentation
  ├─ ClosedWorldConcrete(existing Plan)
  └─ OpenWorldTypeLevel(validated TypeLevelTraversalSpecification)
```

The closed variant preserves the exact supplied Plan. The open variant
preserves the exact validated specification without manufacturing pages,
targets, coordinates, route, or inventory. The insufficient variant exposes no
Goal or execution representation.

## Frozen Distinctions

```text
Intent != Goal
Goal != Execution Method
Plan != Reality
TypeLevelTraversalSpecification != ConcreteFutureRoute
TaskScope != ConcreteWorkInventory
```

The two projection overloads consume already-authoritative structured inputs.
They do not parse, infer, invent defaults or authority, generate routes, observe
the world, or invoke Agent.

## Explicitly Not Authorized

- Compiler component/engine, Planner, FSM/State Machine, Graph;
- LLM/VLM/provider integration;
- `Agent.RunAsync` or Agent/Container/Traversal control-flow changes;
- modifications to Goal, Plan, TypeLevelTraversalSpecification, GoalEvidence,
  Observation, inventory, progress, authorization, or grounding models;
- mutable state or ownership, authority, dependency-direction,
  safety-semantic, or architecture-invariant changes;
- semantic expansion beyond CC-04.

## Prior Purchase

The previous enum plus Plan-only parent purchase remains
`SUPERSEDED_BY_TYPE_LEVEL_SUBGAP_REVIEW` and is not restored.

## Continuation

Resume the same `CP14_MINIMUM_VERTICAL_SLICE_FAST_LOOP` checkpoint and
auto-continue through bounded implementation, unit/Scenario verification,
closed/open/insufficient branches, architecture guards, regression, full suite,
consistency, OpenSpec validation, diagnosis, repair, and revalidation. Stop only
at `VALIDATED` or a canonical Hard Gate.
