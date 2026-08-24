# Spec: runtime-agent-directive-decomposition

> Spec-driven definition of the bounded exploration directive → runtime-local plan
> hypothesis decomposition capability. Additive; reuses the existing open-world DFS
> execution. Source baseline verified 2026-08-21 (uni-agent branch, build clean).

## Purpose

Lets a caller express an abstract, bounded exploration directive and have it
deterministically decomposed into the existing runtime-local open-world execution
inputs, so the RuntimeAgent can run an evidence-driven exploration without the caller
manually constructing the traversal specification and goal evaluators each time.

## ADDED Requirements

### Requirement: Bounded exploration directive representation

The Runtime MUST provide an immutable `Directive` model that expresses a bounded
exploration intent as: a declared task scope (application identity + semantic root),
a declared entry boundary, a maximum semantic depth, a safety boundary of allowed
interaction categories, a completion requirement, and a caller-injected strategy rule
set (candidate authorization, branch inventory, viewport exploration, and element
category classification). The `Directive` MUST NOT carry a `Plan`, element coordinates,
a `DeviceAction`, a `TraversalStep`, an element index, or any precompiled physical step.

#### Scenario: directive carries only task-level declarations
- **WHEN** a caller constructs a `Directive` with scope, entry, depth, safety,
  completion, and strategy rules
- **THEN** the `Directive` exposes exactly those task-level declarations
- **AND** it exposes no `Plan`, no coordinates, no `DeviceAction`, and no element index

#### Scenario: directive rejects an unsafe boundary
- **WHEN** a caller constructs a `Directive` whose safety boundary is empty
- **THEN** construction fails with an explicit validation error
- **AND** no `Directive` instance is created

#### Scenario: directive rejects a negative or missing depth
- **WHEN** a caller constructs a `Directive` with a negative maximum depth
- **THEN** construction fails with an explicit validation error

### Requirement: Stateless directive decomposition

The Runtime MUST provide a stateless `DirectiveDecomposer` that deterministically
projects a `Directive` into exactly one `TypeLevelTraversalSpecification` and one
type-directed `Goal` evaluator assembly suitable for the existing
`IntentExecution.RunOpenWorldAsync` seam. The decomposer MUST NOT observe the world,
MUST NOT select a UI target, MUST NOT construct a concrete route, and MUST NOT invent
strategy rules beyond those the caller injected on the `Directive`.

#### Scenario: directive decomposes into open-world execution inputs
- **WHEN** `DirectiveDecomposer.Decompose` is invoked with a valid `Directive`
- **THEN** it returns a `TypeLevelTraversalSpecification` whose scope, entry, depth,
  safety, completion, and dispatch policy are derived from the `Directive`
- **AND** it returns a `Goal` whose candidate-authorization, branch-inventory,
  viewport-exploration, and category-classifier evaluators are the caller-injected rules

#### Scenario: decomposition is deterministic and world-free
- **WHEN** the same `Directive` is decomposed twice
- **THEN** both decompositions produce structurally identical execution inputs
- **AND** neither decomposition performs an observation or selects a UI target

#### Scenario: incomplete directive is rejected, not guessed
- **WHEN** `DirectiveDecomposer.Decompose` is invoked with a `Directive` missing a
  required strategy rule for the declared completion requirement
- **THEN** it returns an explicit insufficiency result
- **AND** it produces no execution inputs and no fabricated rule

### Requirement: Authorization-boundary preservation

The decomposition MUST preserve the caller's authorization boundary exactly. The
decomposed `Goal` candidate-authorization evaluator MUST be the rule the caller
injected on the `Directive`; the decomposer MUST NOT widen, relax, or synthesize
authorization. A candidate the caller's rule rejects MUST remain rejected after
decomposition.

#### Scenario: rejected candidate stays rejected
- **WHEN** a `Directive`'s candidate-authorization rule rejects a candidate
- **THEN** the decomposed `Goal` rejects the same candidate
- **AND** the decomposer does not introduce any additional authorization

#### Scenario: decomposer grants no authority
- **WHEN** decomposition completes
- **THEN** no decomposition output authorizes an interaction the caller's safety
  boundary forbids

### Requirement: No authority escalation

The decomposition MUST NOT create a new decision authority or a new state owner. The
RuntimeAgent MUST remain the sole run-level semantic and execution authority; the
decomposer MUST be a stateless projection only. The downstream `Agent.RunOpenWorldAsync`
DFS engine, `Container`, `Traversal`, `Recovery`, and the frozen invariants
(Contract I-1..I-14) MUST be unchanged by this capability.

#### Scenario: runtime agent keeps sole execution authority
- **WHEN** a directive is decomposed and run via the existing open-world seam
- **THEN** the RuntimeAgent owns the run lifecycle, grounding, action authorization,
  verification, and terminal outcome exactly as before
- **AND** the decomposer holds no mutable state and participates in no decision

#### Scenario: no new architecture component
- **WHEN** the capability is implemented
- **THEN** it adds an immutable model and a stateless transform only
- **AND** it introduces no new state owner, no global planner, no static navigation
  graph, and no LLM inside the traversal loop

### Requirement: Existing capability regression

The capability MUST NOT change the behavior of the existing open-world execution,
bounded candidate safety, cross-page discovery, or the SETTINGS-TREE-01 capstone. The
existing deterministic suites for those capabilities MUST remain green after the
capability is added.

#### Scenario: settings-tree capstone remains green
- **WHEN** the capability is implemented and the full Runtime test suite is run
- **THEN** the SETTINGS-TREE-01 capstone proofs (TREE-1..TREE-20) pass unchanged

#### Scenario: existing open-world suites remain green
- **WHEN** the capability is implemented and the full Runtime test suite is run
- **THEN** the SC-U2-MUS-001 and SC-OW-TD-001 open-world suites pass unchanged
