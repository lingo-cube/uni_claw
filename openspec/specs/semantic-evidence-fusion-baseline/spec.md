# Spec: semantic-evidence-fusion-baseline

> BASELINE spec for the Semantic Evidence Fusion runtime consumption boundary.
> No code in this change. Base: applied Semantic Perception Contract
> (`docs/decisions/semantic-perception-contract-baseline.md`).
> Cross-reference: `docs/decisions/semantic-evidence-fusion-baseline.md`.

## Purpose

Define the frozen consumption boundary for Semantic Evidence entering the
Runtime: SemanticEvidence MUST enter only via Observation → Perception Evidence
→ Evidence Fusion → Runtime Belief → Agent. The path SemanticEvidence → Agent →
Action is forbidden. Fast Semantic is synchronous with bounded latency; Slow
Semantic is asynchronous checkpoint evidence. Confidence is an evidence weight,
not truth. This is the sole consumer seam for SemanticEvidence.

## Requirements

### Requirement: Evidence Fusion Boundary is frozen

SemanticEvidence MUST enter Runtime only via Observation → Perception Evidence →
Evidence Fusion → Runtime Belief → Agent. The path SemanticEvidence → Agent →
Action MUST be forbidden. Semantic MUST never bypass Runtime.

#### Scenario: semantic evidence flows through runtime fusion

Given SemanticEvidence produced by the Semantic Perception Layer,
When Runtime processes it,
Then it enters through Evidence Fusion → Runtime Belief, never directly to
Agent → Action (falsifier F1).

#### Scenario: no semantic to action bypass

Given an Agent action,
When its dependency path is inspected,
Then SemanticEvidence cannot bypass Runtime to drive Agent → Action (falsifier F1).

### Requirement: Runtime Evidence Fusion is the sole consumer

The only consumer of SemanticEvidence MUST be Runtime Evidence Fusion. Agent,
Planner, Action Executor, and DSH MUST NOT consume SemanticEvidence directly.

#### Scenario: exclusive runtime fusion consumer

Given the SemanticEvidence output,
When consumers are enumerated,
Then Runtime Evidence Fusion is the sole consumer; Agent, Planner, Action
Executor, and DSH do not consume it directly.

#### Scenario: agent consumes belief not raw evidence

Given the Agent boundary,
When Agent consumes semantic information,
Then it consumes Runtime Belief, not raw SemanticEvidence (falsifier F8).

### Requirement: SemanticEvidence is converted to Fact only by Runtime Validation

SemanticEvidence (e.g. candidate=DeveloperOptions, confidence=0.91, source=Vector)
is NOT a Fact. A Fact such as CurrentContainer MUST be produced only after
SemanticEvidence + Vision Evidence + Container History + Current Observation pass
through Runtime Validation → Fact / Belief Update.

#### Scenario: evidence does not become fact directly

Given SemanticEvidence with a candidate and confidence,
When it is consumed,
Then it is not a Fact until Runtime Validation integrates other evidence and
produces a Belief (falsifier F2).

#### Scenario: runtime validation produces the container fact

Given the full conversion pipeline,
When a Container Identity Fact is produced,
Then it is produced by Runtime Validation from Semantic + Vision + Container
History + Current Observation, not by Semantic alone.

### Requirement: Confidence is an Evidence Weight, not Truth

Confidence MUST NOT be treated as direct Truth even above a threshold. Confidence
MUST be used only as an Evidence Weight. Runtime MUST integrate source
reliability, freshness, observation sequence, spatial compatibility, and
historical continuity before deciding whether to form a Belief.

#### Scenario: confidence alone never decides belief

Given a high-confidence SemanticEvidence,
When Runtime evaluates it,
Then confidence is treated only as an Evidence Weight, and Runtime still
integrates reliability, freshness, sequence, spatial compatibility, and
historical continuity before forming Belief (falsifier F4).

#### Scenario: no threshold equals truth

Given the confidence usage principle,
When a threshold is considered,
Then no confidence threshold by itself produces Truth; it only weights evidence.

### Requirement: Container Identity Recovery is Phase 1 and Semantic is auxiliary

Phase 1 MUST address Scrolled Container Identity Drift. Semantic MUST be an extra
Evidence Provider, not a Resolver. Runtime Identity Validation MUST produce the
Container Identity Fact from Text Evidence + Semantic Evidence.

#### Scenario: semantic is an extra evidence provider

Given a scrollable container where the Text Resolver returns null,
When the Runtime identity path runs,
Then Runtime integrates Text Evidence + Semantic Evidence and performs Runtime
Identity Validation before producing the Container Identity Fact (falsifier F8).

#### Scenario: semantic does not become a resolver

Given the Phase 1 scope,
When Semantic's role is inspected,
Then Semantic is an evidence provider, not a Resolver replacement (falsifier F8).

### Requirement: Fast Semantic is synchronous with empty failure

Fast Semantic MUST be synchronous with bounded latency, MUST have no reasoning
loop, and MUST return empty evidence on failure. Its flow is Observation → Vector
Retrieval → SemanticEvidence → Runtime Fusion.

#### Scenario: fast failure returns empty evidence

Given a Fast Semantic vector retrieval that fails,
When it returns,
Then it returns empty evidence (falsifier F6) and Runtime proceeds via its normal
fusion path.

#### Scenario: fast is synchronous and bounded

Given a Fast Semantic call on the Runtime decision path,
When it executes,
Then it is synchronous with bounded latency and has no reasoning loop.

### Requirement: Slow Semantic is asynchronous checkpoint evidence

Slow Semantic MUST run asynchronously, MUST NOT block Runtime, MUST NOT override
existing Fact, and MUST NOT change historical decisions. Its flow is Observation →
Runtime Continue → LLM Semantic Analysis → Checkpoint Evidence.

#### Scenario: slow does not block or override

Given a Slow Semantic LLM analysis,
When Runtime is running,
Then Slow Semantic is asynchronous, does not block Runtime, and does not override
existing Fact or change historical decisions (falsifiers F7/F10).

#### Scenario: slow failure is empty check

Given a Slow Semantic LLM analysis that fails,
When its result is considered,
Then it returns empty checkpoint evidence and does not alter Runtime belief.

### Requirement: Freshness admission rejects stale evidence

SemanticEvidence MUST carry ObservationSequence, Timestamp, and Scope. Runtime
MUST check whether evidence corresponds to the current Observation, is within
valid range, and is allowed to participate in the current Belief. Old
SemanticEvidence MUST NOT be auto-reused.

#### Scenario: stale evidence rejected

Given a SemanticEvidence whose ObservationSequence no longer matches the current
Observation,
When Runtime admission is evaluated,
Then it is rejected and not allowed to participate in the current Belief
(falsifier F5).

#### Scenario: freshness fields present

Given a SemanticEvidence,
When its freshness is inspected,
Then it includes ObservationSequence, Timestamp, and Scope.

### Requirement: Trace / Fact relationship is frozen

SemanticEvidence MAY reference Observation and Trace. Semantic MUST NOT create
Fact. Fact MUST be produced by Runtime Validation after Semantic Processing.

#### Scenario: evidence references trace

Given SemanticEvidence,
When its references are inspected,
Then it may reference Observation and Trace, but it cannot create a Fact.

#### Scenario: runtime validation creates facts

Given the full pipeline,
When a Fact is produced,
Then it is produced by Runtime Validation, not by Semantic (falsifier F2).

### Requirement: Vector / LLM isolation is frozen

Runtime MUST NOT know Vector Database, Embedding Model, or LLM Provider. Runtime
MUST depend only on `ISemanticProvider` which returns `SemanticEvidence`.

#### Scenario: runtime depends only on provider

Given the Runtime dependency surface,
When Semantic dependencies are inspected,
Then Runtime depends only on `ISemanticProvider → SemanticEvidence`, not on any
Vector/Embedding/LLM internals (falsifiers F6/F7 isolation).

#### Scenario: no vector or llm knowledge in runtime

Given the architecture,
When Runtime internals are inspected,
Then Runtime does not know Vector Database, Embedding Model, or LLM Provider.

### Requirement: Falsifiers are frozen

The contract MUST satisfy falsifiers F1–F10: no Runtime bypass, no direct Belief
modification, no Action execution, confidence != Truth, stale evidence rejected,
Vector/LLM failure → empty evidence, no Agent replacement, no Vision expansion,
no L2 planning.

#### Scenario: all falsifiers enforced

Given the Semantic Evidence Fusion contract,
When a falsifier scenario is evaluated,
Then none of F1–F10 may be violated.
