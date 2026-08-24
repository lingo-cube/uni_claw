# Proposal: semantic-perception-contract-baseline

## Buyer

`PROJECT_LEADER_SEMANTIC_PERCEPTION_LAYER_BASELINE` is frozen. This change builds
on it to freeze the **Runtime Contract** of Semantic Perception: what
SemanticEvidence looks like, how evidence becomes Fact, where Runtime may consume
Semantic evidence, and the Fast/Slow execution models.

This is a **BASELINE ONLY** change: architecture decision + OpenSpec
proposal/design/spec/tasks. No production code is implemented.

## Gap (current contract is not yet frozen)

The layer baseline froze *where* Semantic lives, but did not yet freeze the
runtime-level contract: the SemanticEvidence shape, evidence lifecycle
(Semantic → Runtime Validation → Fact), the Semantic Provider interface, the
Fast/Slow execution models, the Vector Storage boundary, and the falsifiers that
prevent Semantic from becoming an Agent.

## What this change does (BASELINE — design/spec only, APPLY later)

1. Freezes the **SemanticEvidence Contract**: identity, semantic type, candidate,
   confidence, scope, freshness, and evidence references.
2. Freezes the **SemanticEvidence lifecycle**: Semantic does NOT produce Fact;
   Runtime Validation produces Fact / Belief Update.
3. Defines the **ISemanticProvider** interface: query / reason / return evidence
   only; never Action / Goal / Plan / World mutation.
4. Freezes **Fast Semantic** (synchronous, bounded-latency Vector Retrieval,
   failure → null) and **Slow Semantic** (async LLM, cannot block or override
   Runtime, failure ignored).
5. Freezes the **Vector Storage Boundary**: Vector Store belongs to Perception
   Layer / Semantic Service, not Runtime/Agent/Vision; Runtime automatic write
   is forbidden.
6. Freezes the **Runtime Consumption Boundary**: Observation → Perception
   Evidence → Evidence Fusion → Belief; never Semantic → Agent → Action.
7. Freezes Phase 1 as **Container Identity Recovery** for Scrolled Container
   Identity Drift; Semantic is a supplementary resolver, not a Runtime replacement.
8. Freezes the **Vision / Semantic / Runtime** question boundary
   (What exists / What might this mean / Should we believe it).
9. Freezes the **Trace / Fact** relationship: SemanticEvidence may reference
   Observation and Trace, but Fact comes from the Runtime belief system.
10. Defines falsifiers F1–F10.

## Scope

In scope:

- Decision doc: `docs/decisions/semantic-perception-contract-baseline.md`
- OpenSpec change: `openspec/changes/semantic-perception-contract-baseline/`
  - `proposal.md`
  - `design.md`
  - `specs/semantic-perception-contract-baseline/spec.md`
  - `tasks.md`
  - `README.md`

Out of scope / forbidden:

- Runtime production code
- Vision Service
- Agent
- Assistance/L1
- DSH
- Vector Database implementation
- LLM Consumer implementation

## Non-goals

This change does NOT implement:

- Vector Database
- LLM Consumer
- Fast Semantic
- Slow Semantic
- ISemanticProvider production implementation
- Runtime consumption code
- Agent / Planner / Memory system / Action generator

## Required output

`PROJECT_LEADER_SEMANTIC_PERCEPTION_CONTRACT_BASELINE_RESULT` with Decision
`SEMANTIC_PERCEPTION_CONTRACT_BASELINE_FROZEN`, the OpenSpec change
(proposal/design/spec/tasks/README) created and validated, and `NEXT_GATE =
PROJECT_LEADER_APPLY_SEMANTIC_PERCEPTION_CONTRACT`.

## Falsifiers

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

## Validation

- `openspec validate semantic-perception-contract-baseline --type change --strict --no-interactive`
- `scripts/check-consistency.sh`
