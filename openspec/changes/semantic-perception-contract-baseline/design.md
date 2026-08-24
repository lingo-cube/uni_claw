# Design: semantic-perception-contract-baseline

> BASELINE design (no code). Base: frozen Layer Baseline
> (`docs/decisions/semantic-perception-layer-baseline.md`).
> Cross-references: `docs/decisions/semantic-perception-contract-baseline.md`.

## 1. Architecture foundation

```text
Perception Layer
    Vision              Semantic
      |                    |
      +---- Evidence ------+
                |
          Runtime Belief
                |
              Agent
```

Semantic is a perception capability, not an Agent.
Semantic does not produce Decision.
Semantic only produces Evidence.

## 2. SemanticEvidence Contract

SemanticEvidence is the only output of Semantic. It is evidence, never Fact.

| Aspect | Value |
|---|---|
| Identity | `evidenceId`, `timestamp/version`, `source` |
| Semantic type (Phase 1) | `ContainerIdentity` |
| Semantic type (future) | `ElementMeaning`, `Relation` |
| Candidate | e.g. `DeveloperOptions`, `WifiSettings`, `NetworkAndInternet` |
| Confidence | `0-1` |
| Scope | `CurrentObservation`, `CurrentContainer`, `HistoricalContext` |
| Freshness | `observationSequence`, `createdAt`, `validUntil (optional)` |
| Evidence reference (future) | `Observation refs`, `Trace refs`, `Fact refs` |

## 3. SemanticEvidence Lifecycle

Semantic does NOT directly produce Fact.

```text
Semantic Provider
        |
SemanticEvidence
        |
Runtime Validation
        |
Fact / Belief Update
```

Semantic may state:

```text
candidate=DeveloperOptions
confidence=0.91
```

That is evidence, not Fact. Runtime integrates Vision evidence + Semantic
evidence + Container history and only then may produce:

```text
Fact:
CurrentContainer=DeveloperOptions
```

## 4. Semantic Provider Interface

Abstract interface design (no implementation):

```text
ISemanticProvider

ResolveAsync(ObservationContext)
returns SemanticEvidence[]
```

Provider capabilities (only):

- query
- reason
- return evidence

Provider prohibitions:

- Action
- Goal
- Plan
- World mutation

## 5. Fast Semantic / Slow Semantic Contract

### Fast Semantic

- Position: fast evidence on the Runtime current decision path.
- Flow:

```text
Observation
  ↓
Vector Retrieval
  ↓
Candidate Evidence
  ↓
Runtime Validation
```

- Requirements: bounded latency, synchronous, no reasoning loop, failure
  returns null.
- Use: Container identity recovery.

### Slow Semantic

- Position: complex semantic supplementation.
- Flow:

```text
Observation
  ↓
Runtime continue
  ↓
Async LLM semantic analysis
  ↓
Checkpoint Evidence
```

- Requirements: asynchronous, cannot block Runtime, cannot override Runtime,
  failure ignored.

## 6. Vector Storage Boundary

Vector Store does not belong to Runtime, Agent, or Vision.

```text
Perception Layer
   |
   Vision Service
   |
Semantic Service
   |
Vector Store
```

- Vector Store stores only `validated semantic patterns`.
- Runtime automatic write is forbidden.
- Future flow (not implemented in this task):

```text
Trace
  ↓
Post Processing
  ↓
Semantic Summary
  ↓
Validation
  ↓
Vector Memory
```

## 7. Runtime Consumption Boundary

Runtime may use Semantic only here:

```text
Observation
  ↓
Perception Evidence
  ↓
Evidence Fusion
  ↓
Belief
```

This path is forbidden:

```text
Semantic
  ↓
Agent
  ↓
Action
```

Semantic must never bypass Runtime.

## 8. Container Identity Recovery Phase 1

Phase 1 single goal: solve **Scrolled Container Identity Drift**.

Current:

```text
Observation
  ↓
Text Resolver
  ↓
null
  ↓
SemanticContradiction
```

Future:

```text
Observation
  ↓
Text Resolver
  ↓
Vector Semantic Resolver
  ↓
Candidate
  ↓
Runtime Validation
  ↓
Container Identity Fact
```

Semantic is only a supplementary resolver. It does not replace Runtime.

## 9. Relationship with Vision

- **Vision** answers: *What exists?*
  - e.g. text, button, toggle, bounds
- **Semantic** answers: *What might this mean?*
  - e.g. this container resembles DeveloperOptions
- **Runtime** answers: *Should we believe it?*

## 10. Relationship with Trace / Fact

- SemanticEvidence may reference `Observation` and `Trace`.
- Semantic does NOT produce Fact.
- Fact is produced by the Runtime belief system.

## 11. Falsifier mapping

| # | Falsifier |
|---|---|
| F1 | Semantic cannot execute action |
| F2 | Semantic cannot complete goal |
| F3 | Semantic cannot mutate world |
| F4 | Semantic cannot bypass Runtime |
| F5 | Vector retrieval failure => null |
| F6 | LLM failure => null |
| F7 | No automatic Runtime learning |
| F8 | No Agent replacement |
| F9 | No Vision responsibility expansion |
| F10 | No L2 planning capability |

## 12. Deferred / non-goals

- Vector Database implementation
- LLM Consumer implementation
- Fast Semantic implementation
- Slow Semantic implementation
- ISemanticProvider production implementation
- Runtime consumption code
- Agent / Planner / Memory system / Action generator

## 13. Validation

- `openspec validate semantic-perception-contract-baseline --type change --strict --no-interactive`
- `scripts/check-consistency.sh`
