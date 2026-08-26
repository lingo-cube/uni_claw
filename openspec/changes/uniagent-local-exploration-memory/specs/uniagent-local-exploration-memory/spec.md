## Purpose

Defines a bounded UniAgent-local historical knowledge capability that supplies provenance-bearing, advisory-only retrieval results to pre-Run Exploration Plan decisions without creating Runtime truth, action, lifecycle, or completion authority.

## ADDED Requirements

### Requirement: UniAgent-local ownership and first buyer

The system SHALL assign the exploration Memory boundary to UniAgent-local ownership, and its first buyer SHALL be UniAgent pre-Run Exploration Plan advisory. Memory MUST NOT be owned by RuntimeAgent, Session, Agent, FSM, Traversal, or ExplorationLedger, and storage placement MUST NOT transfer semantic ownership.

#### Scenario: UniAgent requests pre-Run advisory knowledge

- **WHEN** UniAgent is considering a future Exploration Plan before a Run is admitted
- **THEN** UniAgent-local Memory may return bounded advisory knowledge while UniAgent remains the sole owner of whether and how that knowledge influences its supervisory decision

#### Scenario: Runtime has no Memory dependency

- **WHEN** the Runtime execution boundary and Memory ownership graph are inspected
- **THEN** RuntimeAgent, Agent, FSM, Traversal, WorldBelief, GoalEvidence, and ExplorationLedger have no dependency on UniAgent-local Memory

### Requirement: Producer-owned FactReference semantics

Memory SHALL admit historical facts only through immutable `FactReference` semantics that preserve producer identity, source reference identity, Session/Run correlation when available, observation or event time, environment scope, evidence kind, and integrity provenance. A FactReference MUST NOT copy, rewrite, or re-originate the referenced producer-owned fact.

#### Scenario: Runtime-produced fact is referenced

- **WHEN** an immutable reference to a Runtime-produced historical fact is admitted with valid provenance and scope
- **THEN** Memory retains the reference while the referenced fact remains owned by its original producer

#### Scenario: Unprovenanced assertion is submitted

- **WHEN** an assertion has no verifiable producer, source reference, or integrity provenance
- **THEN** Memory rejects it as a FactReference and creates no fallback historical fact

### Requirement: KnowledgeClaim is derived knowledge, never fact or policy

Memory SHALL represent derived historical knowledge as a versioned `KnowledgeClaim` whose provenance cites one or more valid FactReferences and whose semantics include an explicit scope, derivation time, freshness disposition, and contradiction or supersession state. A KnowledgeClaim MUST remain contestable advisory knowledge and MUST NOT become a Runtime fact, current-world truth, executable policy, action authorization, or completion fact.

#### Scenario: Claim is derived from historical references

- **WHEN** Memory derives a scoped claim from one or more valid FactReferences
- **THEN** the KnowledgeClaim preserves those references and remains distinguishable from every source fact

#### Scenario: Claim proposes an executable prohibition

- **WHEN** candidate content would directly block or authorize an action as policy
- **THEN** Memory rejects that content from the KnowledgeClaim boundary or represents only a non-enforcing descriptive risk claim

### Requirement: Conditional private cross-session scope

The system MUST NOT enable `UNIAGENT_PRIVATE_CROSS_SESSION` retention or retrieval unless the Implementation Human Gate explicitly approves that scope. When approved, cross-session results SHALL remain private to the owning UniAgent identity and SHALL require an explicit compatible environment and consumer scope; the system MUST NOT fall back to global or shared retrieval.

#### Scenario: Cross-session scope is not approved

- **WHEN** UniAgent requests knowledge from another Session without a recorded approval for `UNIAGENT_PRIVATE_CROSS_SESSION`
- **THEN** retrieval returns an unavailable or invalid-scope disposition and does not search a broader scope

#### Scenario: Approved private cross-session retrieval

- **WHEN** the Human Gate has approved `UNIAGENT_PRIVATE_CROSS_SESSION` and a request matches the owning UniAgent and environment scope
- **THEN** Memory may return matching historical KnowledgeClaims without exposing them to another Agent or consumer

### Requirement: Bounded and truthful retrieval semantics

Every retrieval SHALL declare its consumer, pre-Run advisory purpose, requested scope, as-of time, freshness policy, allowed knowledge category, and finite result budget. Memory SHALL validate scope before content matching, preserve contradictory candidates, and return an explicit `FOUND`, `NOT_FOUND`, `STALE_ONLY`, `CONTRADICTED`, `INVALID_SCOPE`, `SOURCE_UNAVAILABLE`, or `MEMORY_UNAVAILABLE` disposition. Retrieval MUST NOT manufacture fallback knowledge, silently widen scope, or mutate any source or consumer state.

#### Scenario: No matching claim exists

- **WHEN** a valid bounded request has no matching KnowledgeClaim
- **THEN** Memory returns `NOT_FOUND` with no fabricated default claim

#### Scenario: Contradictory claims match

- **WHEN** multiple in-scope KnowledgeClaims conflict and no authoritative supersession resolves them
- **THEN** Memory returns the contradiction explicitly instead of selecting one as truth

#### Scenario: Result budget is absent or unbounded

- **WHEN** a retrieval request has no finite result budget
- **THEN** Memory rejects the request rather than loading an unbounded history

### Requirement: Freshness and invalidation remain distinct from observation freshness

Memory SHALL track KnowledgeClaim validity using version, applicable scope, derivation time, expiration, contradiction, supersession, source availability, and explicit invalidation. It SHALL distinguish knowledge freshness from Runtime observation freshness. An active or recently derived KnowledgeClaim MUST NOT satisfy grounding, verification, Visited semantics, GoalEvidence, or any current-world freshness requirement.

#### Scenario: Knowledge has expired

- **WHEN** a matching KnowledgeClaim is past its expiration or incompatible with the current environment version
- **THEN** Memory returns it only under an explicitly allowed stale disposition and UniAgent cannot treat it as current evidence

#### Scenario: New facts contradict a claim

- **WHEN** valid new FactReferences contradict an active KnowledgeClaim
- **THEN** Memory marks the conflict or invalidates/supersedes the claim without silently rewriting its historical provenance

### Requirement: Pre-Run Exploration Plan relationship is advisory only

Retrieved KnowledgeClaims MAY be considered by UniAgent before it authors or selects a bounded Exploration Plan or start-time StrategyDirective. Memory MUST NOT automatically generate a Plan, create a Directive, select or mutate depth, inject routes or actions, start a Run, modify an accepted StrategyDirective, or influence an active Run. Any accepted StrategyDirective SHALL remain Run-immutable, and Runtime completion SHALL continue to require fresh Agent-owned GoalEvidence through the existing FSM path.

#### Scenario: UniAgent uses a historical path hypothesis

- **WHEN** UniAgent considers a retrieved historical environment claim while preparing a future bounded strategy
- **THEN** the claim may affect supervisory prioritization but cannot mark a node Visited, prove coverage, or bypass fresh Runtime grounding and verification

#### Scenario: Run is already active

- **WHEN** a Strategy Run has been admitted
- **THEN** Memory retrieval cannot replace its StrategyDirective, change its depth, redirect execution, or create a successor Run

### Requirement: Memory failure is isolated from Runtime execution

Memory unavailability, timeout, invalid output, stale-only output, or retrieval rejection SHALL be represented truthfully to UniAgent and MUST NOT alter, pause, fail, authorize, continue, or complete Runtime execution. No Runtime fallback dependency on Memory SHALL exist.

#### Scenario: Memory is unavailable before planning

- **WHEN** UniAgent requests pre-Run advisory knowledge and Memory is unavailable
- **THEN** UniAgent receives `MEMORY_UNAVAILABLE`, no historical claim is fabricated, and the Runtime execution system remains unchanged and operable under its existing contracts

#### Scenario: Memory becomes unavailable during a Run

- **WHEN** Memory availability changes after a Run has started
- **THEN** the active Run continues or terminates solely under existing Agent, FSM, Traversal, fresh-observation, and GoalEvidence semantics

### Requirement: Phase 2 and Runtime authority boundaries remain unchanged

Memory and retrieval SHALL NOT mutate WorldBelief, produce GoalEvidence, own completion, replace ExplorationLedger, authorize or generate Action, enforce policy, invoke Agent/FSM/Traversal, implement a Dynamic Planner, mutate Strategy mid-Run, alter dynamic depth, or orchestrate Multi-Run continuation. Retrieval output MUST remain advisory to UniAgent and MUST NOT become Runtime truth through transport, storage, ranking, or consumer interpretation.

#### Scenario: KnowledgeClaim is presented as Runtime truth

- **WHEN** a consumer attempts to use a KnowledgeClaim as current WorldBelief, GoalEvidence, completion evidence, or action authorization
- **THEN** the boundary rejects that use and requires the existing fresh Runtime evidence and authority path

#### Scenario: Retrieval boundary is inspected for authority changes

- **WHEN** ownership, dependencies, outputs, and failure behavior are validated
- **THEN** Agent authorization, FSM lifecycle, Traversal execution, Runtime fresh observation, ExplorationLedger projection, GoalEvidence, and terminal authority remain exactly unchanged
