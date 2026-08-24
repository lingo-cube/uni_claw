# Spec: runtime-assistance-seam

## Purpose

Define the Runtime-side Assistance seam for L1 CONSULT at the external intelligence boundary.

## Requirements

### Requirement: Runtime-side assistance provider abstraction

The design MUST define an abstract, zero-dependency provider interface in
`UniClaw.Runtime` (construction-injected, optional) that lets the Agent request
external information at adjudication points while keeping final decision authority.

#### Scenario: interface lives in the runtime with zero external types

Given the seam definition,
When the interface, context, and advice types are inspected,
Then they live under `UniClaw.Runtime` (Capabilities/Brain domain) and reference
only BCL + `UniClaw.Runtime.Model` — no DSH/Cordis/LLM/VLM/model/harness types
(Guard 2).

#### Scenario: provider is optional and null-safe

Given the Agent construction contract,
When the provider parameter is absent (null),
Then the Agent behaves exactly as today (fail-closed, zero regression).

### Requirement: Adjudication call points (belief surface only)

The seam MUST be invoked ONLY at the belief adjudication surface:
`LocalPageBeliefState ∈ {Unresolved, Contradicted}`.

#### Scenario: contradicted belief consults before failing closed

Given a container whose fused belief state is `Contradicted`,
When the Agent reaches the adjudication point,
Then it MAY consult the provider (advice-mode) before falling back to the existing
`SemanticContradiction` fail-closed outcome.

#### Scenario: unresolved belief gains an explicit consult point

Given a container whose fused belief state is `Unresolved`,
When the Agent reaches the adjudication point,
Then it MAY request external interpretation of the unresolved evidence,
And with no provider or no actionable advice the current fail-closed semantics are
preserved.

#### Scenario: non-adjudication points are out of scope

Given the seam definition,
When it is inspected,
Then it does NOT extend to `BindingUnresolved`, `StateEvidenceRequired`,
`BudgetExhausted`, or viewport-exploration unresolved outcomes (L2+ scope).

### Requirement: Advice-mode consumption

The advice MUST be candidate information only: it never writes belief, binding,
Container, or state; it is never truth, authorization, or goal completion; applying
advice is always an Agent decision (I-3).

#### Scenario: advice cannot mutate runtime state

Given an `AssistanceAdvice`,
When the Agent consumes it,
Then the advice itself never mutates belief/binding/Container/state — any resulting
action is an Agent-authorized deterministic action (e.g. re-observe, rebind,
dismiss) followed by fresh evidence and re-evaluation of the SAME goal.

#### Scenario: bounded consult discipline

Given a consult at an adjudication point,
When the advice yields no deterministic resolution,
Then the Agent fails closed exactly as today, with a bounded number of consult
attempts per adjudication (no unbounded loop).

### Requirement: World-version binding and staleness

The context MUST carry the world version (`Observation.SequenceNumber` /
`WorldBelief.SourceObservationSequence`); advice bound to an advanced world MUST be
discarded; the Agent re-observes fresh evidence before applying anything.

#### Scenario: stale advice is discarded

Given advice whose `WorldVersion` differs from the context world version,
When the Agent receives it,
Then it is discarded and never mutates current belief.

#### Scenario: fresh evidence before application

Given an advice-recommended deterministic action,
When the Agent performs it,
Then it re-observes (sequence advanced) and re-evaluates the SAME goal — the world
is authoritative (I-4).

### Requirement: Correlation

The context MUST carry a correlation identity; the advice MUST echo it; mismatched
responses MUST be discarded.

#### Scenario: correlation echo

Given an `AssistanceContext` with `RequestId`,
When advice is returned,
Then the advice `RequestId` equals the context `RequestId`, otherwise it is
discarded.

### Requirement: DSH-side provider is deferred

This change MUST NOT implement the DSH-side provider, wire transport, or
`intelligence.consult` / `perception.ask` / escalation protocol.

#### Scenario: no harness implementation

Given the change's scope,
When its artifact set is inspected,
Then no DSH plugin, wire method, or provider adapter is defined (deferred to
`dsh-intelligence-provider-integration`).

### Requirement: Behavior-preserving guarantees

The seam MUST NOT change fail-closed semantics, Trap/Recovery, GoalEvidence,
completion, drift/popup/deferred-reconciliation handling, or introduce new
RuntimeEvent kinds/emitters.

#### Scenario: consult failure fails closed

Given a provider that throws or times out,
When the Agent consults,
Then it fails closed (never fabricates progress), and the failure is an Agent-side
decision input, not a process fault.

#### Scenario: no new emitters

Given the seam's runtime surface,
When the event vocabulary is inspected,
Then no new RuntimeEvent kinds or emitters are introduced.
