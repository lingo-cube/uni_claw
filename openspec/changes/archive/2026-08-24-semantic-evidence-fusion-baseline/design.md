# Design: semantic-evidence-fusion-baseline

> BASELINE design (no code). Base: applied Semantic Perception Contract
> (`docs/decisions/semantic-perception-contract-baseline.md`).
> Cross-references: `docs/decisions/semantic-evidence-fusion-baseline.md`.

## 1. Architecture foundation

```text
Observation
  ↓
Perception Layer
  ↓
Vision Evidence + SemanticEvidence
  ↓
Runtime Evidence Fusion
  ↓
Runtime Belief
  ↓
Agent
```

Semantic is an Evidence Provider. Runtime is the only Belief Authority.

## 2. Evidence Fusion Boundary

Allowed:

```text
Observation
  ↓
Perception Evidence
  ↓
Evidence Fusion
  ↓
Runtime Belief
  ↓
Agent
```

Forbidden:

```text
SemanticEvidence
  ↓
Agent
  ↓
Action
```

Semantic must never bypass Runtime (F1).

## 3. Sole consumer

The only consumer of SemanticEvidence is **Runtime Evidence Fusion**.

Forbidden consumers:

- Agent directly consuming SemanticEvidence
- Planner consuming SemanticEvidence
- Action Executor consuming SemanticEvidence
- DSH consuming SemanticEvidence

## 4. SemanticEvidence → Fact conversion

SemanticEvidence is not a Fact.

```text
candidate = DeveloperOptions
confidence = 0.91
source = Vector   →  NOT a Fact
```

Conversion:

```text
SemanticEvidence
  + Vision Evidence
  + Container History
  + Current Observation
  ↓
Runtime Validation
  ↓
Fact / Belief Update
```

Example final Fact:

```text
CurrentContainer = DeveloperOptions
```

## 5. Confidence usage principle

Forbidden:

- confidence above a threshold treated directly as Truth.

Allowed:

- confidence is only an **Evidence Weight**.

Runtime integrates:

- source reliability
- freshness
- observation sequence
- spatial compatibility
- historical continuity

then decides whether to form a Belief (F4).

## 6. Container Identity Recovery

Phase 1 goal: solve **Scrolled Container Identity Drift**.

Current problem:

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
Text Evidence
  + Semantic Evidence
  ↓
Runtime Identity Validation
  ↓
Container Identity Fact
```

Frozen: Semantic does NOT become a Resolver; it is only an extra Evidence
Provider (F8).

## 7. Fast Semantic / Slow Semantic

### Fast Semantic (synchronous)

```text
Observation
  ↓
Vector Retrieval
  ↓
SemanticEvidence
  ↓
Runtime Fusion
```

Requirements: bounded latency, no reasoning loop, failure returns empty evidence
(F6).

### Slow Semantic (asynchronous)

```text
Observation
  ↓
Runtime Continue
  ↓
LLM Semantic Analysis
  ↓
Checkpoint Evidence
```

Requirements: does not block Runtime, does not override existing Fact, does not
change historical decisions (F7/F10).

## 8. Freshness admission

SemanticEvidence must include:

- ObservationSequence
- Timestamp
- Scope

Runtime must check:

- corresponds to current Observation?
- within valid range?
- allowed to participate in current Belief?

Forbidden: old SemanticEvidence auto-reuse (F5).

## 9. Trace / Fact relationship

SemanticEvidence may reference:

- Observation
- Trace

Flow:

```text
Trace
  ↓
Semantic Processing
  ↓
SemanticEvidence
  ↓
Runtime Validation
  ↓
Fact
```

SemanticEvidence cannot create a Fact (F2).

## 10. Vector / LLM isolation

Runtime does not know:

- Vector Database
- Embedding Model
- LLM Provider

Runtime depends only on:

```text
ISemanticProvider → SemanticEvidence
```

## 11. Falsifier mapping

| # | Falsifier |
|---|---|
| F1 | Semantic cannot bypass Runtime |
| F2 | Semantic cannot directly modify Belief |
| F3 | Semantic cannot execute Action |
| F4 | Confidence cannot equal Truth |
| F5 | Stale SemanticEvidence rejected |
| F6 | Vector failure returns empty evidence |
| F7 | LLM failure returns empty evidence |
| F8 | No Agent replacement |
| F9 | No Vision responsibility expansion |
| F10 | No L2 planning capability |

## 12. Deferred / non-goals

- Runtime Evidence Fusion production code
- Vector DB / Embedding / LLM integration
- Agent / Planner / Action Executor / DSH consumption
- Container Identity Recovery production resolver

## 13. Validation

- `openspec validate semantic-evidence-fusion-baseline --type change --strict --no-interactive`
- `scripts/check-consistency.sh`
