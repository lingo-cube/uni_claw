# Spec: semantic-perception-layer-baseline

> BASELINE spec for the Semantic Perception Layer architecture. No code in this
> change. Cross-reference:
> `docs/decisions/semantic-perception-layer-baseline.md`.

## ADDED Requirements

### Requirement: Semantic is a Perception Layer capability parallel to Vision

Semantic MUST belong to the Perception Layer, not the Agent. Vision and Semantic
MUST be modeled as parallel perception capabilities. Semantic MUST NOT be
implemented as an Agent subsystem or as a replacement for Vision.

#### Scenario: semantic lives in Perception Layer

Given the UniClaw architecture,
When the placement of Semantic is inspected,
Then Semantic is part of the Perception Layer, alongside Vision, and is not an
Agent component (falsifier F2).

#### Scenario: vision and semantic are parallel

Given a perception request,
When the architecture is evaluated,
Then Vision (`pixel → element evidence`) and Semantic (Fast/Slow evidence
production) are distinct parallel capabilities; neither subsumes the other
(falsifier F3).

### Requirement: Semantic output is SemanticEvidence only

Semantic MUST output `SemanticEvidence`, not a Decision. Allowed SemanticEvidence
fields are: candidate, confidence, evidence source, and explanation. Forbidden
Semantic outputs are: action decision, goal completion, world state mutation,
planning, and autonomous behavior.

#### Scenario: evidence not decision

Given a Semantic component,
When it produces output,
Then the output is `SemanticEvidence` with perception-level fields only — it does
not contain an action decision, goal completion, world state mutation, planning
result, or autonomous behavior (falsifiers F1/F4).

#### Scenario: evidence may support or contradict

Given a semantic hypothesis about container identity,
When Semantic produces evidence,
Then the evidence may support or contradict the hypothesis; it never acts as the
final authority for the claim.

### Requirement: Runtime remains the sole authority

Runtime MUST remain the single authority for Evidence Fusion, ContainerIdentity,
Binding, Belief update, and Action authority. Semantic MUST NOT perform these
authoritative functions.

#### Scenario: runtime owns fusion and identity

Given SemanticEvidence produced by the Semantic Perception Layer,
When the Runtime consumes it,
Then Runtime owns Evidence Fusion, ContainerIdentity, Binding, Belief update, and
Action authority; Semantic does not replace or override them (falsifier F5).

#### Scenario: semantic does not mutate state

Given a Semantic evidence result,
When the result is processed,
Then it does not mutate world state, container identity, bindings, beliefs, or
action authority.

### Requirement: Semantic input boundary is narrow

Semantic MUST accept only Current Observation, Visible Elements, Container
History, and Previous Verified Identity. Semantic MUST NOT receive Goal, Action
command, Expected state, or Planning context.

#### Scenario: allowed inputs only

Given a Semantic component,
When its inputs are inspected,
Then they are limited to Current Observation, Visible Elements, Container History,
and Previous Verified Identity (falsifier F6).

#### Scenario: no goal or planning context

Given a Runtime that holds Goal and Planning context,
When Semantic is invoked,
Then Goal, Action command, Expected state, and Planning context are NOT passed to
Semantic; the boundary prevents Semantic from evolving into an Agent.

### Requirement: Phase 1 scope is Container Identity Recovery only

Phase 1 Semantic capability MUST support only Container Identity Recovery. Phase 1
MUST NOT implement ElementRelation, Binding Semantic, Memory Learning, LLM
reasoning, or Vector database.

#### Scenario: scrollable container identity recovery

Given a scrollable container whose page title leaves the viewport and Vision
returns `null`,
When Semantic is available,
Then Semantic MAY produce container identity candidate evidence to help Runtime
avoid a false `SemanticContradiction`; Runtime remains the authority to accept or
reject the identity (falsifier F7).

#### Scenario: phase 1 excludes other semantic capabilities

Given the Phase 1 scope,
When the implemented capabilities are inspected,
Then only Container Identity Recovery is included; ElementRelation, Binding
Semantic, Memory Learning, LLM reasoning, and Vector database are not implemented
(falsifier F7).

### Requirement: Slow Semantic is non-blocking async checkpoint evidence

Fast Semantic is vector-based semantic retrieval. Slow Semantic is LLM-based
semantic reasoning. Slow Semantic MUST be an async checkpoint evidence source and
MUST NOT block the Runtime main control flow.

#### Scenario: slow semantic does not block runtime

Given a Slow Semantic request,
When the Runtime main control flow is running,
Then Slow Semantic evidence is treated as asynchronous checkpoint evidence and
never blocks the Runtime main control flow (falsifier F8).

#### Scenario: fast semantic vector retrieval

Given a Fast Semantic capability,
When it is used for semantic retrieval,
Then it uses vector-based retrieval and returns `SemanticEvidence`.

### Requirement: Memory remains read-only in this change

Semantic knowledge MUST be read-only in this baseline. Runtime automatic learning
or writing into Vector is forbidden. Future memory pipeline (Trace → Post
Processing → Semantic Pattern → Validation → Vector Memory) is an independent
capability.

#### Scenario: no runtime vector writes

Given the Semantic Perception Layer baseline,
When memory behavior is inspected,
Then Semantic knowledge is read-only and Runtime does not automatically learn or
write into Vector (falsifier F9).

#### Scenario: future memory pipeline is separate

Given the future memory evolution path,
When it is described,
Then it is an independent capability outside Semantic Perception and outside
Runtime action authority.

### Requirement: This change creates no Agent-like capability

This change MUST NOT create an Agent, Planner, Memory system, LLM controller, or
Action generator. It is an architecture baseline for Semantic Perception only.

#### Scenario: no new agent-like components

Given this OpenSpec change,
When its deliverables are inspected,
Then it creates no Agent, Planner, Memory system, LLM controller, or Action
generator (falsifiers F1/F10).

#### Scenario: baseline only, no production mutation

Given this change,
When the repository diff is inspected,
Then it does not modify Runtime production code, Vision service, Assistance
system, or DSH integration (falsifier F10).

## MODIFIED Requirements

None. This change modifies no existing spec or implementation.
