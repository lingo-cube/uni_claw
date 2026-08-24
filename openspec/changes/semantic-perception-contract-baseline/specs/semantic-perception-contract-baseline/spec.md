# Spec: semantic-perception-contract-baseline

> BASELINE spec for the Semantic Perception runtime contract. No code in this
> change. Base: frozen Layer Baseline
> (`docs/decisions/semantic-perception-layer-baseline.md`).
> Cross-reference: `docs/decisions/semantic-perception-contract-baseline.md`.

## ADDED Requirements

### Requirement: SemanticEvidence carries a frozen contract shape

SemanticEvidence MUST carry identity (`evidenceId`, `timestamp/version`,
`source`), a Phase 1 semantic type of `ContainerIdentity`, candidate, confidence
(`0-1`), scope (CurrentObservation / CurrentContainer / HistoricalContext),
freshness (`observationSequence`, `createdAt`, `validUntil` optional), and
support for future evidence references (Observation / Trace / Fact). Future
semantic types `ElementMeaning` and `Relation` are reserved but not implemented.

#### Scenario: identity and freshness present

Given a SemanticEvidence produced for Container Identity,
When its shape is inspected,
Then it includes `evidenceId`, `timestamp/version`, `source`, `observationSequence`,
`createdAt`, and (optionally) `validUntil`.

#### Scenario: scope distinguishes context

Given a SemanticEvidence,
When its scope is evaluated,
Then it is one of `CurrentObservation`, `CurrentContainer`, or
`HistoricalContext`.

#### Scenario: future types reserved

Given the SemanticEvidence contract,
When future semantic types are considered,
Then `ElementMeaning` and `Relation` remain reserved and are not implemented in
this change.

### Requirement: SemanticEvidence lifecycle keeps Fact out of Semantic

Semantic MUST NOT directly produce Fact. The lifecycle MUST be: Semantic Provider →
SemanticEvidence → Runtime Validation → Fact / Belief Update. Runtime MUST
integrate Vision evidence, Semantic evidence, and Container history before it may
produce a Fact such as `CurrentContainer`.

#### Scenario: evidence is not fact

Given Semantic returning `candidate=DeveloperOptions, confidence=0.91`,
When the result is consumed,
Then it is treated as evidence, not as a Fact; the Runtime remains the only
producer of Fact / Belief Update.

#### Scenario: runtime validates before fact

Given the evidence lifecycle,
When Fact / Belief Update occurs,
Then it happens only after Runtime Validation, never directly from Semantic
(falsifier F4).

### Requirement: Semantic Provider is query/reason/evidence only

The abstract `ISemanticProvider` interface MUST support
`ResolveAsync(ObservationContext) → SemanticEvidence[]`. A Provider MUST be
limited to querying, reasoning, and returning evidence. It MUST NOT execute
Action, complete Goal, Plan, or mutate World.

#### Scenario: provider returns evidence only

Given an ISemanticProvider,
When it is invoked with an ObservationContext,
Then it returns `SemanticEvidence[]` and performs no Action, Goal, Plan, or World
mutation (falsifiers F1/F2/F3).

#### Scenario: provider boundary frozen

Given the Provider interface design,
When its capabilities are inspected,
Then query / reason / evidence-return are allowed and everything else is excluded.

### Requirement: Fast Semantic is bounded synchronous vector retrieval

Fast Semantic MUST be synchronous with bounded latency, use Vector Retrieval to
produce Candidate Evidence for Runtime Validation, have no reasoning loop, and
return null on failure. Its use is Container identity recovery.

#### Scenario: fast failure returns null

Given a Fast Semantic vector retrieval that fails,
When it returns,
Then it returns null (falsifier F5), and the Runtime proceeds with the existing
fail-closed path.

#### Scenario: fast is synchronous and bounded

Given a Fast Semantic call on the Runtime decision path,
When it executes,
Then it is synchronous with bounded latency and has no reasoning loop.

### Requirement: Slow Semantic is asynchronous checkpoint evidence

Slow Semantic MUST be asynchronous, MUST NOT block the Runtime main control flow,
MUST NOT override Runtime, and MUST ignore failure. It produces checkpoint
evidence after Runtime continues.

#### Scenario: slow does not block runtime

Given a Slow Semantic LLM analysis,
When the Runtime main control flow is running,
Then Slow Semantic runs asynchronously and never blocks the Runtime (falsifier F10
adjacent).

#### Scenario: slow failure ignored

Given a Slow Semantic LLM analysis that fails,
When its result is unused,
Then the failure is ignored and produces null; it does not degrade the Runtime
decision (falsifier F6).

### Requirement: Vector Storage stays outside Runtime/Agent/Vision

Vector Store MUST NOT belong to Runtime, Agent, or Vision. It belongs under the
Perception Layer / Semantic Service. It stores only `validated semantic patterns`.
Runtime automatic write is forbidden.

#### Scenario: vector store ownership

Given the architecture,
When Vector Store ownership is inspected,
Then it belongs to Perception Layer / Semantic Service, not Runtime, Agent, or
Vision.

#### Scenario: no automatic runtime learning

Given the Vector Store boundary,
When Runtime behavior is inspected,
Then Runtime does not automatically write into Vector (falsifier F7).

### Requirement: Runtime consumption boundary is frozen

Runtime MUST consume Semantic only via Observation → Perception Evidence →
Evidence Fusion → Belief. The path Semantic → Agent → Action MUST be forbidden.
Semantic MUST NOT bypass Runtime.

#### Scenario: semantic feeds evidence fusion

Given Semantic evidence,
When Runtime processes it,
Then it enters through Perception Evidence → Evidence Fusion → Belief, never
directly to Agent → Action (falsifier F4).

#### Scenario: no semantic-to-action bypass

Given an Agent action,
When its dependency path is inspected,
Then Semantic cannot bypass Runtime to drive Agent → Action.

### Requirement: Phase 1 is Container Identity Recovery only

Phase 1 Semantic capability MUST be limited to Container Identity Recovery for
Scrolled Container Identity Drift. Semantic is a supplementary resolver, not a
Runtime replacement. It produces a Candidate; Runtime Validation produces the
Container Identity Fact.

#### Scenario: scrolled container identity recovery

Given a scrollable container whose page title leaves the viewport and the Text
Resolver returns null,
When Semantic is available,
Then it MAY supply a Candidate via a Vector Semantic Resolver, and Runtime
Validation decides whether to produce the Container Identity Fact.

#### Scenario: semantic supplements, does not replace runtime

Given Semantic candidate evidence,
When identity is decided,
Then Runtime remains the sole authority and Semantic does not replace it.

### Requirement: Vision / Semantic / Runtime question boundary

Vision MUST answer "What exists?" (text, button, toggle, bounds). Semantic MUST
answer "What might this mean?" (e.g. this container resembles DeveloperOptions).
Runtime MUST answer "Should we believe it?".

#### Scenario: responsibility separation

Given a perception request,
When responsibilities are evaluated,
Then Vision reports existence, Semantic proposes meaning, and Runtime decides
belief (falsifier F9).

#### Scenario: no vision responsibility expansion

Given the Semantic change,
When Vision scope is evaluated,
Then Vision's responsibility is not expanded; it still answers "What exists?"
(falsifier F9).

### Requirement: Trace / Fact relationship is frozen

SemanticEvidence MAY reference Observation and Trace. Semantic MUST NOT produce
Fact. Fact MUST be produced by the Runtime belief system.

#### Scenario: evidence references observation and trace

Given SemanticEvidence,
When its references are inspected,
Then it may reference Observation and Trace.

#### Scenario: runtime produces facts

Given the full pipeline,
When a Fact such as `CurrentContainer` is produced,
Then it is produced by the Runtime belief system, not by Semantic.

### Requirement: Falsifiers are frozen

The contract MUST satisfy falsifiers F1–F10: no Action execution, no Goal
completion, no World mutation, no Runtime bypass, Vector retrieval failure → null,
LLM failure → null, no automatic Runtime learning, no Agent replacement, no Vision
responsibility expansion, and no L2 planning capability.

#### Scenario: all falsifiers enforced

Given the Semantic Perception contract,
When a falsifier scenario is evaluated,
Then none of F1–F10 may be violated (e.g. Semantic cannot execute action, cannot
complete goal, cannot mutate world, cannot bypass Runtime, vector/LLM failure is
null, no auto learning, no Agent replacement, no Vision expansion, no L2 planning).

## MODIFIED Requirements

None. This change modifies no existing spec or implementation.
