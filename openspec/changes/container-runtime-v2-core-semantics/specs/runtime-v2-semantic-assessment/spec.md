## Purpose

Define bounded Fast Container resolution and revision-bound Slow semantic advisory assessment as evidence producers whose outputs derive working trust but never acquire world, action, graph-mutation, recovery, or goal-completion authority.

## ADDED Requirements

### Requirement: Fast resolution combines action, fresh observation, and Graph prior
Fast Container resolution SHALL consume immutable action context, the correlated fresh accepted observation or Slice, and authority-free Graph candidates. It SHALL produce a revision-bound assessment of same Container, new Container, transient/intermediate, ambiguous, identity candidate, semantic support, and conflicts. Action and Graph inputs SHALL remain priors rather than truth.

#### Scenario: Trigger and destination semantics support candidate
- **WHEN** a Wallpaper trigger is followed by a destination containing mutually consistent Wallpaper semantics with no hard conflict
- **THEN** Fast MAY support a Wallpaper working identity while marking the evidence revision and SHALL NOT authorize an action or claim world truth

#### Scenario: Strong prior conflicts with fresh evidence
- **WHEN** a Graph or action prior predicts same Container but fresh evidence has a hard independent-destination conflict
- **THEN** Fast SHALL surface the conflict and SHALL NOT force the prior result

### Requirement: Fast Trust is a derived working interpretation
The Runtime SHALL derive Fast Trust from independent-Container support when relevant, semantic support, and absence of hard conflict. Fast Trust SHALL permit bounded continued interpretation and verification-prior use only; it SHALL NOT imply action authorization, completeness, Slow confirmation, or published memory.

#### Scenario: Fast-trusted node continues without Slow blocking
- **WHEN** a current evidence revision satisfies the Fast Trust contract
- **THEN** Runtime interpretation MAY continue without waiting for Slow while all existing action and completion gates remain required

### Requirement: Slow Advisor supports Disabled, Shadow, and Async Advisory modes
The Slow semantic capability SHALL be provider-neutral and support `Disabled`, `Shadow`, and `AsyncAdvisory` consumption modes before any stronger purchase. It MAY assess scene, Container semantics, trigger semantics, relation semantics, evidence usefulness, mismatch, and a bounded suggested disposition. It SHALL NOT directly mutate CurrentContainer or Graph, authorize an action, declare Goal completion, own traversal planning, or execute recovery.

#### Scenario: Shadow assessment has no behavior effect
- **WHEN** Slow runs in Shadow mode and returns a challenge
- **THEN** the assessment SHALL be recorded for evaluation without changing Runtime action, recovery, completion, CurrentContainer, or Graph behavior

#### Scenario: Slow identifies advertisement evidence
- **WHEN** Slow assesses a fresh destination as an advertisement whose content is not useful identity evidence
- **THEN** it MAY return that revision-bound assessment and a suggested disposition while leaving all decisions to Runtime/UniAgent owners

### Requirement: Valuable fresh evidence may start Fast and Slow in parallel
For configured useful evidence, the Runtime SHALL permit bounded Fast and Slow assessments to start in parallel rather than requiring Fast failure first. Fast MAY return first; Slow MAY later confirm, challenge, correct, or report insufficient evidence for the same revision.

#### Scenario: Fast returns before Slow
- **WHEN** Fast produces a working assessment and Slow remains pending for the same evidence revision
- **THEN** Runtime MAY use the bounded Fast interpretation and SHALL reject any later Slow result that is stale relative to newer fresh evidence

### Requirement: Assessment precedence is revision-scoped and derived
For the same evidence revision, fresh accepted evidence SHALL outrank all semantic assessment, Slow SHALL outrank Fast for semantic interpretation, and Fast SHALL outrank historical Graph prior. Slow SHALL never override a newer fresh evidence revision. Original Fast and Slow assessments SHALL remain immutable evidence while trust is derived.

#### Scenario: Slow challenges Fast for same revision
- **WHEN** Slow challenges a Fast identity interpretation for the same observation, node, trigger, and transition references
- **THEN** the derived semantic/trust view SHALL reflect the challenge without deleting the original assessments or authorizing an action

#### Scenario: Slow result is stale
- **WHEN** CurrentContainer has advanced to a newer evidence revision before Slow completes
- **THEN** the stale Slow result SHALL be retained as historical evidence at most and SHALL NOT mutate the current semantic/trust view

### Requirement: ContainerRuntimeV2 composes one stateless evidence lifecycle
Production composition of the immutable V2 reducer/Graph, Fast resolver, Slow acquisition/projection, correction projection, optional checkpoint projection, and unified read projection SHALL enter through one `ContainerRuntimeV2` lifecycle facade. The facade SHALL accept prior immutable state and exact evidence context and return immutable results. It SHALL NOT store current state, latest assessment, trust, correction, checkpoint, action, recovery, obligation, or Goal truth. Pure component tests and provider adapters MAY continue to call their narrow seams directly.

#### Scenario: Normal lifecycle shares one evidence context
- **WHEN** one accepted fresh occurrence is reduced, assessed by Fast, and submitted to Slow Shadow
- **THEN** occurrence, Fast, Slow, correction, and read projection SHALL be traceable to the same Observation, revision, Transition, Trigger, source/destination Node, and current Slice references

#### Scenario: Any component binding differs
- **WHEN** a supplied Fast or Slow request references a different revision, occurrence, trigger, node, observation, or Slice from the lifecycle context
- **THEN** composition SHALL fail closed without accepting a partial V2 state or producing a current correction

#### Scenario: Slow completes after a newer lifecycle revision
- **WHEN** Slow for O17 is projected after immutable V2 state has advanced to O23
- **THEN** the O17 result MAY remain readable as historical evidence but SHALL NOT replace O23 current semantic/trust or CurrentContainer
