# Semantic Perception Contract Baseline

> Date: 2026-08-19
> Role: Project Leader / Runtime Contract Baseline
> Base: `PROJECT_LEADER_SEMANTIC_PERCEPTION_LAYER_BASELINE` (frozen)
> Scope: Architecture design + Decision doc + OpenSpec proposal/design/spec/tasks only
> Result: `PROJECT_LEADER_SEMANTIC_PERCEPTION_CONTRACT_BASELINE_RESULT`
> Decision: **BASELINE_FROZEN — DESIGN ONLY; APPLY NOT AUTHORIZED**

## 1. Purpose

On top of the frozen Semantic Perception **Layer** baseline, this document freezes
the **Runtime Contract** of Semantic Perception: what exactly Semantic produces,
how evidence becomes Fact, where Runtime may consume Semantic evidence, and what
must never happen.

This is a design-only baseline. It does **not** modify Runtime production code,
Vision Service, Agent, Assistance/L1, DSH, Vector Database, or LLM Consumer.

## 2. Architecture foundation

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

## 3. SemanticEvidence Contract

SemanticEvidence is the only output of Semantic. It is evidence, never Fact.

### Identity

- `evidenceId`
- `timestamp/version`
- `source`

### Semantic type

Phase 1:

```text
ContainerIdentity
```

Future reserved:

```text
ElementMeaning
Relation
```

### Candidate

Examples:

```text
DeveloperOptions
WifiSettings
NetworkAndInternet
```

### Confidence

```text
0-1
```

### Scope

MUST distinguish:

```text
CurrentObservation
CurrentContainer
HistoricalContext
```

### Freshness

MUST make explicit:

```text
observationSequence
createdAt
validUntil (optional)
```

### Evidence Reference

Must support future references:

```text
Observation refs
Trace refs
Fact refs
```

## 4. SemanticEvidence Lifecycle

Semantic does **not** directly produce Fact.

```text
Semantic Provider
        |
SemanticEvidence
        |
Runtime Validation
        |
Fact / Belief Update
```

Example:

```text
Semantic:
candidate=DeveloperOptions
confidence=0.91

(not a Fact)
```

Runtime integrates:

- Vision evidence
- Semantic evidence
- Container history

Then:

```text
Fact:
CurrentContainer=DeveloperOptions
```

## 5. Semantic Provider Interface

Design an abstract interface:

```text
ISemanticProvider

ResolveAsync(ObservationContext)
returns SemanticEvidence[]
```

Provider may only:

- query
- reason
- return evidence

Provider must never:

- Action
- Goal
- Plan
- World mutation

## 6. Fast Semantic / Slow Semantic Contract

### Fast Semantic

Position: fast evidence on the Runtime current decision path.

```text
Observation
  ↓
Vector Retrieval
  ↓
Candidate Evidence
  ↓
Runtime Validation
```

Requirements:

- bounded latency
- synchronous
- no reasoning loop
- failure returns null

Use: Container identity recovery.

### Slow Semantic

Position: complex semantic supplementation.

```text
Observation
  ↓
Runtime continue
  ↓
Async LLM semantic analysis
  ↓
Checkpoint Evidence
```

Requirements:

- asynchronous
- cannot block Runtime
- cannot override Runtime
- failure ignored

## 7. Vector Storage Boundary

Vector Store does not belong to:

- Runtime
- Agent
- Vision

Structure:

```text
Perception Layer
   |
   Vision Service
   |
Semantic Service
   |
Vector Store
```

Vector Store stores only:

```text
validated semantic patterns
```

Runtime automatic write is forbidden.

Future flow (NOT implemented in this task):

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

## 8. Runtime Consumption Boundary

Runtime uses Semantic only in this path:

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

## 9. Container Identity Recovery Phase 1

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

## 10. Relationship with Vision

Frozen:

- **Vision** answers: *What exists?*
  - e.g. text, button, toggle, bounds
- **Semantic** answers: *What might this mean?*
  - e.g. this container resembles DeveloperOptions
- **Runtime** answers: *Should we believe it?*

## 11. Relationship with Trace / Fact

Frozen:

- SemanticEvidence may reference `Observation` and `Trace`.
- Semantic does **not** produce Fact.
- Fact is produced by the Runtime belief system.

## 12. Falsifiers

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

## 13. Validation

- `openspec validate semantic-perception-contract-baseline --type change --strict --no-interactive`
- `scripts/check-consistency.sh`

## 14. Result

Baseline complete and ready for the next **APPLY** gate. No production
implementation is authorized by this document.
