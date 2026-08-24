# Semantic Evidence Fusion Baseline

> Date: 2026-08-19
> Role: Project Leader / Architecture Baseline
> Base: `PROJECT_LEADER_APPLY_SEMANTIC_PERCEPTION_CONTRACT_RESULT` (applied)
> Scope: Architecture analysis + Decision doc + OpenSpec proposal/design/spec/tasks only
> Result: `PROJECT_LEADER_SEMANTIC_EVIDENCE_FUSION_BASELINE_RESULT`
> Decision: **SEMANTIC_EVIDENCE_FUSION_BASELINE_FROZEN — DESIGN ONLY; APPLY NOT AUTHORIZED**

## 1. Purpose

On top of the applied Semantic Perception Layer + SemanticEvidence Contract, this
baseline freezes **how SemanticEvidence enters the Runtime**: the consumption
boundary, the sole consumer, the evidence → Fact transition, confidence usage,
freshness admission, and the Trace/Fact relationship.

This is a design-only baseline. It does **not** modify Runtime production
behavior, Vector DB, Embedding, LLM, Agent, Vision, Assistance/L1, or DSH.

## 2. Architecture foundation

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

## 3. Evidence Fusion Boundary

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

Semantic must never bypass Runtime.

## 4. SemanticEvidence Consumer

The **only consumer** of SemanticEvidence is **Runtime Evidence Fusion**.

Forbidden consumers:

- Agent directly consuming SemanticEvidence
- Planner consuming SemanticEvidence
- Action Executor consuming SemanticEvidence
- DSH consuming SemanticEvidence

## 5. SemanticEvidence → Fact conversion

SemanticEvidence example:

```text
candidate = DeveloperOptions
confidence = 0.91
source = Vector
```

This is **NOT** a Fact.

It must pass through:

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

Example final result:

```text
CurrentContainer = DeveloperOptions
```

## 6. Confidence usage principle

Forbidden:

- Confidence above some threshold is treated directly as Truth.

Allowed:

- Confidence is only an **Evidence Weight**.

Runtime integrates:

- source reliability
- freshness
- observation sequence
- spatial compatibility
- historical continuity

and then decides whether to form a Belief.

## 7. Container Identity Recovery

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

Frozen: Semantic does NOT become a Resolver. Semantic is only an extra Evidence
Provider.

## 8. Fast Semantic / Slow Semantic

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

Requirements:

- bounded latency
- no reasoning loop
- failure returns empty evidence

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

Requirements:

- does not block Runtime
- does not override existing Fact
- does not change historical decisions

## 9. SemanticEvidence Freshness

SemanticEvidence must include:

- ObservationSequence
- Timestamp
- Scope

Runtime must check:

- does it correspond to the current Observation?
- is it within the valid range?
- is it allowed to participate in the current Belief?

Forbidden: old SemanticEvidence auto-reuse.

## 10. Trace / Fact relationship

SemanticEvidence may reference:

- Observation
- Trace

But it cannot create a Fact.

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

## 11. Vector / LLM isolation

Runtime does not know:

- Vector Database
- Embedding Model
- LLM Provider

Runtime depends only on:

```text
ISemanticProvider → SemanticEvidence
```

## 12. Falsifiers

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

## 13. Validation

- `openspec validate semantic-evidence-fusion-baseline --type change --strict --no-interactive`
- `scripts/check-consistency.sh`

## 14. Result

```text
PROJECT_LEADER_SEMANTIC_EVIDENCE_FUSION_BASELINE_RESULT
Decision: SEMANTIC_EVIDENCE_FUSION_BASELINE_FROZEN
```

Baseline complete and ready for the next APPLY gate. No production implementation
is authorized by this document.
