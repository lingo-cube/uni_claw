# Fast Semantic Container Identity Baseline

> Date: 2026-08-19
> Role: Project Leader / Architecture Baseline
> Base: `PROJECT_LEADER_SEMANTIC_EVIDENCE_FUSION_GRADUATED`
> Scope: Architecture design + Decision doc + OpenSpec proposal/design/spec/tasks only
> Result: `PROJECT_LEADER_FAST_SEMANTIC_CONTAINER_IDENTITY_BASELINE_RESULT`
> Decision: **FAST_SEMANTIC_CONTAINER_IDENTITY_BASELINE_FROZEN — DESIGN ONLY; APPLY NOT AUTHORIZED**

## 1. Purpose

After Semantic Evidence Fusion graduated, this baseline freezes the architecture
for **Fast Semantic Container Identity Recovery**. The goal is to solve
**Scrolled Container Identity Drift** by adding a bounded Fast Semantic Evidence
Provider — without replacing the Container Resolver and without changing
Agent/Vision/Belief authority.

This is a design-only baseline. It does **not** modify Agent, Goal, Action,
Planner, L1 Assistance, DSH, Vision Service, CreateMultiPageResolver,
ContainerIdentityResolver, or Belief Authority.

## 2. Current failure chain

```text
Observation
  ↓
CreateMultiPageResolver
  ↓
Visible Text Anchor Missing
  ↓
SemanticPage = null
  ↓
Container Continuity Failure
  ↓
SemanticContradiction
```

## 3. Target architecture

```text
Vision Evidence
  + Existing Container Evidence
  + Fast Semantic Evidence
  ↓
Runtime Evidence Fusion
  ↓
Container Identity Validation
```

Semantic is an **additional Evidence Provider**, not a Resolver replacement.
Semantic only provides `ContainerIdentity` evidence.

## 4. A1 — Fast Semantic Provider definition

Create:

```text
FastSemanticContainerIdentityProvider
Capabilities/Perception/Semantic/Fast
```

Input:

```text
ObservationContext
  - Current Observation
  - Visible Elements
  - Container History
  - Previous Verified Identity
```

Output:

```text
SemanticEvidence
  type: ContainerIdentity
  candidate: DeveloperOptions
  confidence: 0.87
  references: ObservationSequence
```

Forbidden input:

```text
Goal
Action
Expected State
Planner Context
```

## 5. A2 — Vector Retrieval abstraction

Create interface:

```text
IVectorSemanticIndex
```

Responsibility:

```text
semantic pattern retrieval
```

Input:

```text
ContainerSemanticQuery
  - Visible element summary
  - Element types
  - Text fragments
  - Structural features
```

Output:

```text
SemanticCandidate
  - Identity candidate
  - Similarity score
  - Pattern reference
```

Forbidden:

- Vector Index returns Fact.
- Vector Index decides.

## 6. A3 — Fast Semantic flow

Frozen synchronous bounded flow:

```text
Observation
  ↓
Feature Extraction
  ↓
Vector Retrieval
  ↓
SemanticEvidence
  ↓
SemanticEvidenceFusion
  ↓
Runtime Validation
```

Requirements:

- bounded latency
- no retry loop
- no reasoning
- failure = empty evidence

## 7. A4 — Vector Memory boundary

Current: do NOT create automatic write.

Vector data source (future):

```text
Trace
  ↓
Post Processing
  ↓
Semantic Pattern
  ↓
Validation
  ↓
Vector Memory
```

Current: only a **Read-only Vector Index**.

Forbidden: Runtime writes Vector.

## 8. A5 — Container Identity Validation

Design:

- Runtime original path: `Text Resolver`.
- Additional path: `Semantic Evidence Candidate`.

Example:

```text
Text Resolver: null
Semantic Evidence: DeveloperOptions confidence 0.86
Runtime Validation:
  - previous verified identity
  - container history
  - observation continuity
  - semantic evidence
  → decide whether to recover Container Identity
```

Frozen: Semantic does NOT directly set `CurrentContainer`.

## 9. A6 — Fast / Slow boundary

Clear:

- **Fast Semantic**: synchronous, bounded, vector retrieval.
- **Slow Semantic**: future async LLM checkpoint.

This change does **not** implement Slow Semantic.

## 10. A7 — Test design

| # | Test |
|---|---|
| T1 | Vector hit returns SemanticEvidence |
| T2 | Vector miss returns empty evidence |
| T3 | Fast semantic latency bounded |
| T4 | Semantic candidate does not become Fact |
| T5 | Old container identity requires Runtime validation |
| T6 | No Vector provider keeps Runtime unchanged |
| T7 | Agent unchanged |
| T8 | Resolver unchanged |
| T9 | Scrolled container can receive candidate evidence |
| T10 | Semantic confidence does not equal Truth |

## 11. Boundary / non-goals

- No Agent / Goal / Action / Planner / L1 / DSH modifications.
- No Vision Service modification.
- No CreateMultiPageResolver / ContainerIdentityResolver modification.
- No Belief Authority change.
- No Slow Semantic implementation.
- No Vector write path.

## 12. Validation

- `openspec validate fast-semantic-container-identity-baseline --type change --strict --no-interactive`
- `scripts/check-consistency.sh`

## 13. Result

```text
PROJECT_LEADER_FAST_SEMANTIC_CONTAINER_IDENTITY_BASELINE_RESULT
Decision: FAST_SEMANTIC_CONTAINER_IDENTITY_BASELINE_FROZEN
NEXT_GATE: PROJECT_LEADER_APPLY_FAST_SEMANTIC_CONTAINER_IDENTITY
```

Baseline complete and ready for the next APPLY gate. No production implementation
is authorized by this document.
