# Semantic Perception Layer Baseline

> Date: 2026-08-19
> Role: Project Leader / Architecture Baseline
> Scope: Design only — NO production code implementation
> Result: `PROJECT_LEADER_SEMANTIC_PERCEPTION_LAYER_BASELINE_RESULT`
> Decision: **BASELINE_FROZEN — DESIGN ONLY; APPLY NOT AUTHORIZED**

## 1. Purpose

This document freezes the **Semantic Perception Layer** architecture baseline for
UniClaw. It defines:

- The boundary between **Semantic** perception and the **Agent**.
- The relationship between **Vision** and **Semantic** as parallel perception
  capabilities.
- The only permitted Semantic output: **SemanticEvidence**, never a Decision.
- The **Runtime** as the single authority for evidence fusion, container identity,
  binding, belief update, and action authority.
- The **first phase scope**: Container Identity Recovery only.
- The future **Fast Semantic / Slow Semantic** evolution path.
- The **memory boundary**: read-only semantic knowledge now; independent vector
  memory later.

This is an architecture baseline. It does **not** authorize production code,
Runtime changes, Vision service changes, Assistance system changes, or DSH
integration changes.

## 2. Architecture

Semantic belongs to the **Perception Layer**, not to the Agent. Vision and
Semantic are parallel perception capabilities:

```text
Perception Layer
├── Vision
│   └── pixel → element evidence
│
└── Semantic
    ├── Fast Semantic
    │   └── vector retrieval
    │
    └── Slow Semantic
        └── LLM semantic reasoning
```

Both produce evidence for the Runtime. Neither decides actions, completes goals,
mutates world state, plans, or behaves autonomously.

## 3. Semantic output contract

Semantic MUST output **SemanticEvidence**, not a Decision.

Allowed SemanticEvidence fields:

- `candidate`
- `confidence`
- `evidence source`
- `explanation`

Forbidden SemanticEvidence behavior:

- `action decision`
- `goal completion`
- `world state mutation`
- `planning`
- `autonomous behavior`

Semantic evidence is a perception result. It may support or contradict a
hypothesis, but the Runtime owns all belief and action authority.

## 4. Runtime remains the sole authority

Runtime is the only authority for:

- Evidence Fusion
- ContainerIdentity
- Binding
- Belief update
- Action authority

Semantic Perception MUST NOT bypass, weaken, or replace Runtime authority. It
feeds evidence into Runtime-owned fusion; it does not perform fusion or hold
belief.

## 5. Semantic input boundary

Allowed Semantic inputs:

- Current Observation
- Visible Elements
- Container History
- Previous Verified Identity

Forbidden Semantic inputs:

- Goal
- Action command
- Expected state
- Planning context

This input boundary is deliberately narrow so Semantic cannot evolve into an
Agent.

## 6. Phase 1 scope

The first phase supports **only**:

```text
Container Identity Recovery
```

Target problem:

- In a scrollable container, the page title may leave the viewport.
- After that happens, the Vision resolver may return `null`.
- That `null` can produce a false `SemanticContradiction`.

Phase 1 does **not** implement:

- ElementRelation
- Binding Semantic
- Memory Learning
- LLM reasoning
- Vector database

## 7. Fast / Slow Semantic evolution

### Fast Semantic

- Vector-based semantic retrieval.
- Suitable for cheap, bounded, local identity recovery.
- Must return `SemanticEvidence`.

### Slow Semantic

- LLM-based semantic reasoning.
- Future role: **async checkpoint evidence**.
- MUST NOT block the Runtime main control flow.

The baseline freezes the evolution path but does not purchase Fast/Slow
implementations in Phase 1.

## 8. Memory boundary

Current:

- Semantic knowledge is **read-only**.
- Runtime automatic learning / writing into Vector is **forbidden**.

Future evolution (independent capability):

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

This future memory pipeline is a separate capability, not part of Semantic
Perception or Runtime action authority.

## 9. Non-goals

This baseline does NOT create:

- Agent
- Planner
- Memory system
- LLM controller
- Action generator

## 10. Validation

- `openspec validate semantic-perception-layer-baseline --strict --no-interactive`
- `openspec validate --changes --strict --no-interactive`
- `scripts/check-consistency.sh`

## 11. Result

The baseline is complete and ready for the next **APPLY** gate. No production
implementation is authorized by this document.
