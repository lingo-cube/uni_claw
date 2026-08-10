# CP14_TYPE_LEVEL_REPRESENTATION_MINIMUM_FALSIFYING_SCENARIO

> Scenario: `SC-CP14-TL-MVS-001`
> Capability: truthful `OPEN_WORLD_TYPE_LEVEL` task representation
> Development lane: `CAPABILITY_DELIVERY_FAST`
> Status: `VALIDATED`
> Checkpoint: `CP14_TYPE_LEVEL_REPRESENTATION_MINIMUM_VERTICAL_SLICE_FAST_LOOP`
> Parent checkpoint: `CP14_MINIMUM_VERTICAL_SLICE_FAST_LOOP` (suspended)

## Accepted Semantic Receipt

The following semantics are already approved and are not reopened here:

- CP-14 and admitted RM-11;
- CC-04 Intent Semantic Envelope with Dual-Mode Projection;
- `CLOSED_WORLD_CONCRETE` remains represented by the existing `Plan`;
- `CP14_OPEN_WORLD_TYPE_LEVEL_REPRESENTATION_SEMANTIC_GATE_RESULT` authorizes
  exactly six open-world dimensions: task scope, target element categories,
  traversal depth bound, safety/forbidden-interaction boundary, caller
  completion requirement, and entry/starting boundary;
- `TypeLevelTraversalSpecification != ConcreteFutureRoute`;
- `TaskScope != ConcreteWorkInventory`;
- architecture impact is `NONE_AT_SEMANTIC_LEVEL`;
- state-machine pressure is `NO_STATE_PRESSURE`.

This sub-slice does not reopen Intent compilation, ambiguity semantics,
closed-world execution, Goal completion authority, constraint enforcement, or
the CP-14 Architecture Challenge.

## Missing Production Capability

Repository production truth has no immutable artifact that can preserve all six
approved dimensions while leaving concrete pages, targets, coordinates, route,
and work inventory unknown. The existing artifacts remain intentionally
different:

- `Plan` / `PlanStep` are concrete execution hypotheses;
- `Goal` and `GoalEvidence` retain completion evaluation and authority;
- `Observation` supplies runtime evidence;
- `BranchInventoryEvidence` and `BranchProgressEvidence` describe discovered
  inventory and progress;
- candidate-authorization and target-grounding receipts apply to concrete
  observed instances;
- `RecoveryAnchor` establishes a verified Runtime entry but is not declared
  task scope.

No one of these may be overloaded as the missing specification.

## Minimum Falsifying Scenario

### Common authoritative specification

Before execution, the caller declares:

- task scope rooted in the Settings application and one semantic root;
- target category `NavigableContainer`;
- maximum traversal depth `N`;
- a safety boundary allowing interaction only with the declared safe
  categories;
- completion requirement `ExhaustiveWithinScope`;
- an application and semantic entry boundary.

The specification contains no concrete page name beyond the declared semantic
root/entry, no observed target, no coordinate, no future action sequence, no
route, and no concrete work inventory.

### FS-A — bounded safe traversal without a route

Given the authoritative specification above, construction succeeds without a
`Plan`, placeholder `PlanStep`, concrete page inventory, target list, or route.
All six semantic dimensions remain structurally inspectable and immutable.

The Scenario fails if any approved dimension exists only as an opaque aggregate
string, or if construction requires concrete future work.

### FS-B — same specification, different worlds

The same specification is paired with two deterministic world observations.
The observations produce different concrete candidate and inventory evidence,
while the specification remains structurally equal and unchanged.

The Scenario fails if observed inventory mutates or redefines task scope.

### FS-C — depth is authoritative input

Two specifications differ only in maximum depth. They are structurally unequal
before execution. Current depth remains runtime-derived evidence and is not
stored as specification progress.

The Scenario fails if the depth difference is hidden in evaluator code or
requires a concrete route.

### FS-D — safety is authoritative input

Two specifications have the same scope, target categories, depth, completion,
and entry but different allowed interaction-category boundaries. They are
structurally unequal before concrete inventory exists.

The Scenario fails if safety defaults to unrestricted interaction, is replaced
by a concrete candidate receipt, or is inferred from current Observation.

### FS-E — required dimensions cannot disappear

Construction rejects missing/empty scope, empty target categories, invalid
depth, absent safety boundary, absent completion requirement, or missing entry
identity. It never fills a missing value from a default route, fixture, Goal
delegate, or current world.

## Completion Boundary

`ExhaustiveWithinScope` preserves only the caller's completion requirement. It
does not report progress or completion.

- CP-04 retains discovered-work inventory/progress semantics;
- CP-06 and Agent retain final GoalEvidence completion authority;
- CP-07 retains enforcement of declared depth/safety constraints;
- CP-08 retains observation-failure versus exhaustion semantics.

Specification construction, equality, or exhaustion of any representation is
never Goal completion evidence.

## Architecture Fit Check

Result: `ARCHITECTURE_FIT_CONFIRMED`.

The specification is immutable caller-side planning data. It creates no mutable
state and no decision authority. It is produced before Agent execution and does
not change the runtime spine:

```text
Authoritative caller input
→ immutable type-level specification
→ later bounded upstream projection
→ existing Agent boundary
→ Container
→ Traversal
→ Environment
```

This sub-slice does not perform the later projection or change
`Agent.RunAsync(Goal, Plan, ...)`.

| Audit | Result |
|---|---|
| Mutable-state ownership | `UNCHANGED` |
| Decision authority | `UNCHANGED` |
| Dependency direction | `UNCHANGED` |
| Architecture invariants | `UNCHANGED` |
| Safety authority | `UNCHANGED` |
| External-world authority | `UNCHANGED` |
| State-machine pressure | `NO_STATE_PRESSURE` |

## Proven-Minimal Production Purchase

### Production artifact

One new production file:

- `src/UniClaw.Runtime/Planning/TypeLevelTraversalSpecification.cs`

Exactly six immutable semantic types:

1. `TypeLevelTraversalSpecification` — immutable aggregate;
2. `TypeLevelTaskScope` — validated application identity plus semantic root;
3. `TypeLevelElementCategory` — bounded category vocabulary for this Scenario:
   `NavigableContainer` and `StateChangingControl`;
4. `TypeLevelSafetyBoundary` — immutable allowed-interaction category set;
5. `TypeLevelCompletionRequirement` — this Scenario purchases only
   `ExhaustiveWithinScope`;
6. `TypeLevelEntryBoundary` — validated application identity plus expected
   semantic entry.

`TypeLevelTraversalSpecification` has exactly six immutable values:

1. `Scope: TypeLevelTaskScope`;
2. `TargetCategories: ImmutableHashSet<TypeLevelElementCategory>`;
3. `MaximumDepth: int` (`>= 0`);
4. `Safety: TypeLevelSafetyBoundary`;
5. `Completion: TypeLevelCompletionRequirement`;
6. `Entry: TypeLevelEntryBoundary`.

Supporting immutable values:

- `TypeLevelTaskScope`: `ApplicationIdentity`, `SemanticRoot`;
- `TypeLevelSafetyBoundary`: `AllowedInteractionCategories`;
- `TypeLevelEntryBoundary`: `ApplicationIdentity`, `ExpectedSemanticEntry`.

All strings are validated non-empty identity values inside a named semantic
value; scope, safety, completion, category, depth, and entry are not represented
as one opaque string or undifferentiated alias. All category sets are immutable,
non-default, non-empty, and use exact value equality.

### Explicit non-purchases

- no `IntentSemanticEnvelope` implementation or parent CP-14 projection change;
- no `Plan`, `PlanStep`, `Goal`, `GoalEvidence`, `Observation`, inventory,
  progress, authorization, or grounding field;
- no Agent/Container/Traversal/Environment/Recovery behavior or signature
  change;
- no type-level matching, discovery, enforcement, completion, compilation, or
  planning algorithm;
- no parser, Planner, Compiler engine, Task IR hierarchy, Graph, FSM,
  LLM/VLM/provider, prompt, or clarification UX;
- no hard-coded WiFi or concrete Settings route;
- no new mutable state, interface, component, owner, or authority;
- no safety-semantic expansion: the value preserves a caller boundary but does
  not authorize or execute an interaction;
- no target-only completion, timeout/max-step completion, unbounded traversal,
  aliases, or category vocabulary not required by FS-A..E.

## Test Purchase

Expected test files:

- `tests/UniClaw.Runtime.Tests/Unit/TypeLevelTraversalSpecificationTests.cs`;
- `tests/UniClaw.Runtime.Tests/Scenario/Cp14TypeLevelRepresentationScenarioTests.cs`;
- minimal additions to
  `tests/UniClaw.Runtime.Tests/Architecture/ArchitectureGuardTests.cs`.

Required proof:

- FS-A..FS-E pass deterministically;
- equal input produces equal immutable specification;
- the specification has no `Plan`, `PlanStep`, `Observation`, concrete target,
  route, inventory, progress, GoalEvidence, authorization, or mutable-state
  surface;
- existing `Agent.RunAsync` signature and Runtime control flow remain unchanged;
- build, full tests, architecture guards, consistency checks, and strict
  repository validation pass.

## Delta Budget

```text
Production files added: 1
Production files modified: 0
New immutable record types: 4
New enum types: 2
New immutable aggregate values: 11
New methods beyond validating constructors: 0
New interfaces/components/engines: 0
New mutable state: 0
Agent/Container/Traversal/Environment/Recovery delta: 0
Ownership delta: NONE
Authority delta: NONE
Dependency-direction delta: NONE
Architecture-invariant delta: NONE
Safety-semantic delta: NONE
```

## Historical Human Implementation Gate

At checkpoint creation, repository truth contained no Human receipt authorizing
the production file, six types, eleven immutable values, or test purchase above.
The earlier parent CP-14 Human Gate was withdrawn because its `Plan`-only
open-world payload was semantically incomplete.

The Fast Loop therefore stopped exactly once at:

```text
HUMAN_IMPLEMENTATION_GATE_REQUIRED
```

Human authorization then resumed this same
`CP14_TYPE_LEVEL_REPRESENTATION_MINIMUM_VERTICAL_SLICE_FAST_LOOP` checkpoint
through implementation and full validation. The next action is to rebase the
suspended parent `CP14_MINIMUM_VERTICAL_SLICE_FAST_LOOP`; the validated sub-slice
does not automatically authorize the parent envelope purchase.

## Validation Receipt

Human authorization is recorded in
`docs/decisions/human-authorize-cp14-type-level-representation-minimum-vertical-slice-implementation.md`.
The authorized implementation is validated:

- production delta: one file, four immutable records, two enums, eleven public
  immutable values, and three private static readonly immutable canonical set
  instances used only for deterministic collection value equality;
- FS-A..FS-E: 5/5 PASS;
- targeted CP-14 type-level tests: 11/11 PASS;
- Architecture Guards: 8/8 PASS;
- full suite: 455/455 PASS;
- build: 0 warnings, 0 errors;
- consistency: 9/9 PASS;
- OpenSpec repository strict validation: 13/13 PASS;
- ownership, authority, dependency direction, architecture invariants, safety
  authority, external-world authority: unchanged;
- state-machine pressure: `NO_STATE_PRESSURE`.

The canonical result is recorded in
`docs/decisions/cp14-type-level-representation-minimum-vertical-slice-fast-loop-result.md`.

STOP.
