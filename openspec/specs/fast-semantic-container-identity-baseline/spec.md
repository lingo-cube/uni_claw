# fast-semantic-container-identity-baseline Specification

## Purpose

TBD - created by archiving change fast-semantic-container-identity-baseline. Update Purpose after archive.

## Requirements

### Requirement: FastSemanticContainerIdentityProvider is a bounded Fast Semantic provider

A `FastSemanticContainerIdentityProvider` MUST be defined under
`Capabilities/Perception/Semantic/Fast`. It MUST consume only `ObservationContext`
(Current Observation, Visible Elements, Container History, Previous Verified
Identity) and MUST return `SemanticEvidence` of kind `ContainerIdentity`. It MUST
NOT accept Goal, Action, Expected State, or Planner Context. It MUST be
synchronous, bounded, and return empty evidence on failure.

#### Scenario: provider returns container identity evidence

Given an ObservationContext with visible elements and previous verified identity,
When the Fast provider runs,
Then it returns `SemanticEvidence` with kind `ContainerIdentity`, a candidate,
confidence, and observation reference (T1).

#### Scenario: provider fails to empty evidence

Given a Fast provider whose vector retrieval misses,
When it cannot produce a candidate,
Then it returns empty evidence (T2).

#### Scenario: forbidden inputs are not accepted

Given the Fast provider interface,
When its input boundary is inspected,
Then Goal, Action, Expected State, and Planner Context are not accepted.

### Requirement: IVectorSemanticIndex is read-only semantic pattern retrieval

An `IVectorSemanticIndex` interface MUST be defined for semantic pattern
retrieval. It MUST accept `ContainerSemanticQuery` (visible element summary,
element types, text fragments, structural features) and MUST return
`SemanticCandidate` (identity candidate, similarity score, pattern reference).
Vector Index MUST NOT return Fact and MUST NOT decide.

#### Scenario: query returns semantic candidate

Given a `ContainerSemanticQuery`,
When the vector index is queried,
Then it returns `SemanticCandidate` with identity candidate, similarity score, and
pattern reference.

#### Scenario: vector index does not decide

Given the vector index output,
When inspected,
Then it returns a candidate, not a Fact and not a decision (T4).

### Requirement: Fast Semantic flow is synchronous and bounded

Fast Semantic MUST follow: Observation → Feature Extraction → Vector Retrieval →
SemanticEvidence → SemanticEvidenceFusion → Runtime Validation. It MUST have
bounded latency, no retry loop, no reasoning, and failure = empty evidence.

#### Scenario: latency bounded

Given a Fast Semantic call,
When it executes,
Then it completes within the bounded latency contract (T3).

#### Scenario: no retry loop or reasoning

Given the Fast Semantic provider,
When its behavior is inspected,
Then it has no retry loop and no reasoning loop.

### Requirement: Vector Memory is read-only in this baseline

Vector data source MUST be read-only in this change. Runtime MUST NOT write
Vector. Auto-learning MUST NOT be created. Future Vector Memory pipeline (Trace →
Post Processing → Semantic Pattern → Validation → Vector Memory) is not
implemented.

#### Scenario: no runtime vector write

Given the Fast Semantic architecture,
When Vector write behavior is inspected,
Then Runtime does not write to Vector and no auto-learning is created.

#### Scenario: future vector memory deferred

Given the future pipeline,
When it is described,
Then it is deferred and not implemented in this change.

### Requirement: Container Identity Validation stays Runtime-owned

The existing Text Resolver MUST remain. Semantic Evidence Candidate MUST be an
additional input. Runtime Validation MUST combine previous verified identity,
container history, observation continuity, and semantic evidence before deciding
whether to recover Container Identity. Semantic MUST NOT directly set
CurrentContainer.

#### Scenario: scrolled container receives candidate evidence

Given a scrolled container where Text Resolver returns null,
When Fast Semantic produces a candidate,
Then Runtime Identity Validation may use it to consider Container Identity
recovery (T9); Semantic does not set CurrentContainer.

#### Scenario: old container identity requires runtime validation

Given a previous container identity candidate,
When Runtime decides,
Then it requires Runtime Validation, not direct Semantic setting (T5).

### Requirement: Fast / Slow boundary is explicit

Fast Semantic MUST be synchronous, bounded, vector retrieval. Slow Semantic MUST
remain a future async LLM checkpoint. This change MUST NOT implement Slow
Semantic.

#### Scenario: slow semantic not implemented

Given this change,
When Slow Semantic is inspected,
Then it is not implemented; only the Fast boundary is defined (T6-adjacent).

### Requirement: No Agent, Resolver, or Belief Authority changes

This change MUST NOT modify Agent, Goal, Action, Planner, L1 Assistance, DSH,
Vision Service, CreateMultiPageResolver, ContainerIdentityResolver, or Belief
Authority.

#### Scenario: agent unchanged

Given this change,
When Agent is inspected,
Then Agent behavior is unchanged (T7).

#### Scenario: resolver unchanged

Given this change,
When CreateMultiPageResolver / ContainerIdentityResolver are inspected,
Then their behavior is unchanged (T8).

### Requirement: Semantic confidence remains an evidence weight

Fast Semantic confidence MUST be an evidence weight, not Truth. It MUST NOT make
confidence > threshold equal Truth. Runtime decides Belief.

#### Scenario: confidence not truth

Given SemanticEvidence with high confidence,
When Runtime consumes it,
Then confidence is only an Evidence Weight and does not equal Truth (T10).