# Design: fast-semantic-container-identity-baseline

> BASELINE design (no code). Base: graduated Semantic Evidence Fusion
> (`docs/decisions/semantic-evidence-fusion-graduation-review.md`).
> Cross-references: `docs/decisions/fast-semantic-container-identity-baseline.md`.

## 1. Architecture foundation

Current failure:

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

Target:

```text
Vision Evidence
  + Existing Container Evidence
  + Fast Semantic Evidence
  ↓
Runtime Evidence Fusion
  ↓
Container Identity Validation
```

Semantic is an extra Evidence Provider, not a Resolver replacement.

## 2. A1 — FastSemanticContainerIdentityProvider

Location:

```text
Capabilities/Perception/Semantic/Fast
```

Input:

- `ObservationContext`
  - Current Observation
  - Visible Elements
  - Container History
  - Previous Verified Identity

Output:

- `SemanticEvidence`
  - Kind = `ContainerIdentity`
  - Candidate (e.g. `DeveloperOptions`)
  - Confidence (e.g. 0.87)
  - Reference = ObservationSequence

Forbidden input:

- Goal
- Action
- Expected State
- Planner Context

The provider is synchronous, bounded, and returns empty evidence on failure.

## 3. A2 — IVectorSemanticIndex

Interface:

```text
IVectorSemanticIndex
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

Boundaries:

- Vector Index returns semantic candidates, not Facts.
- Vector Index does not decide.

## 4. A3 — Fast Semantic flow

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

## 5. A4 — Vector Memory boundary

Current:

- Read-only Vector Index only.
- No Runtime write.
- No automatic learning.

Future:

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

## 6. A5 — Container Identity Validation

Runtime owns:

- Text Resolver (existing)
- Semantic Evidence Candidate (new input)

Example:

```text
Text Resolver: null
Semantic Evidence: DeveloperOptions confidence 0.86
Runtime Validation:
  - previous verified identity
  - container history
  - observation continuity
  - semantic evidence
```

Decision: whether to recover Container Identity.

Frozen: Semantic does NOT directly set `CurrentContainer`.

## 7. A6 — Fast / Slow boundary

- **Fast Semantic**: synchronous, bounded, vector retrieval. This baseline's focus.
- **Slow Semantic**: future async LLM checkpoint. Not implemented here.

## 8. A7 — Test matrix (APPLY)

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

## 9. Boundary / non-goals

- No Agent / Goal / Action / Planner / L1 / DSH modifications.
- No Vision Service modification.
- No CreateMultiPageResolver / ContainerIdentityResolver modification.
- No Belief Authority change.
- No Slow Semantic implementation.
- No Vector write path.

## 10. Validation

- `openspec validate fast-semantic-container-identity-baseline --type change --strict --no-interactive`
- `scripts/check-consistency.sh`
