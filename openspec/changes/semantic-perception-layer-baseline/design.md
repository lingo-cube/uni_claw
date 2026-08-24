# Design: semantic-perception-layer-baseline

> BASELINE design (no code). Source-verified baseline: 2026-08-19.
> Cross-references: `docs/decisions/semantic-perception-layer-baseline.md`.

## 1. Architecture

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

Semantic is a first-class perception capability, parallel to Vision. It is NOT a
subsystem of Agent.

## 2. Ownership

| Component | Owns | Does NOT own |
|---|---|---|
| Perception Layer / Semantic | Produce `SemanticEvidence` about perception-level claims | Decisions, belief, goals, actions |
| Vision | Produce element evidence from pixels | Semantic identity reasoning, belief |
| Runtime | Evidence Fusion, ContainerIdentity, Binding, Belief update, Action authority | Raw perception internals |
| Agent | Semantic decisions/goals based on evidence | Raw perception production |

The ownership boundary is frozen. Semantic MUST remain evidence-producing and
stateless with respect to Runtime belief.

## 3. SemanticEvidence contract

Semantic output MUST be `SemanticEvidence`, not `Decision`.

Allowed fields:

| Field | Meaning |
|---|---|
| `candidate` | A semantic hypothesis, e.g. container identity candidate |
| `confidence` | Qualitative or bounded confidence in the candidate |
| `evidence source` | Which Semantic channel produced it (e.g. FAST/SLOW) |
| `explanation` | Human/audit-readable reasoning for the evidence |

Forbidden fields/behaviors:

- `action decision`
- `goal completion`
- `world state mutation`
- `planning`
- `autonomous behavior`

`SemanticEvidence` may support or contradict a claim. It is not a command and not
a belief.

## 4. Runtime authority

The Runtime is the single authority for:

- Evidence Fusion
- ContainerIdentity
- Binding
- Belief update
- Action authority

Semantic Perception supplies evidence into the Runtime. It MUST NOT perform
fusion itself, hold ContainerIdentity state, mutate bindings, update beliefs, or
authorize actions.

## 5. Input boundary

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

The forbidden list prevents Semantic from evolving into an Agent. A Semantic
component must not know what the goal is or what action the Agent plans to take.

## 6. Phase 1 scope: Container Identity Recovery only

The only supported semantic capability in Phase 1 is **Container Identity
Recovery**.

Target problem:

```
scrollable container:
  page title leaves viewport
    → Vision resolver returns null
      → false SemanticContradiction
```

Phase 1 design boundary:

- Semantic may produce container identity candidates from allowed inputs.
- Runtime remains the sole authority that accepts/rejects identity and fuses the
  evidence.
- Semantic does NOT create containers, mutate bindings, or decide actions.

Phase 1 does NOT implement:

- ElementRelation
- Binding Semantic
- Memory Learning
- LLM reasoning
- Vector database

## 7. Fast / Slow evolution

### Fast Semantic

- Vector-based semantic retrieval.
- Intended for cheap identity recovery.
- Output: `SemanticEvidence` with source `FAST`.

### Slow Semantic

- LLM-based semantic reasoning.
- Future role: async checkpoint evidence.
- MUST NOT block the Runtime main control flow.
- Output: `SemanticEvidence` with source `SLOW`.

Neither Fast nor Slow is implemented by this baseline.

## 8. Memory boundary

Current:

- Semantic knowledge is **read-only**.
- Runtime automatic learning / writing into Vector is **forbidden**.

Future independent pipeline:

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

The future pipeline is a separate capability and is not part of this change.

## 9. Non-goals

This change does not create:

- Agent
- Planner
- Memory system
- LLM controller
- Action generator

## 10. Deferred

- Fast Semantic vector database
- Slow Semantic LLM reasoning
- ElementRelation
- Binding Semantic
- Memory Learning
- Vector Memory pipeline
- Any production implementation

## 11. Validation

- `openspec validate semantic-perception-layer-baseline --type change --strict --no-interactive`
- `openspec validate --changes --strict --no-interactive`
- `scripts/check-consistency.sh`
