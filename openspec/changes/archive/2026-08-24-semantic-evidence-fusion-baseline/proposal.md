# Proposal: semantic-evidence-fusion-baseline

## Buyer

`PROJECT_LEADER_APPLY_SEMANTIC_PERCEPTION_CONTRACT_RESULT` is applied: the
Semantic Evidence DTO and `ISemanticProvider` type-level contract now exist. This
change builds on that to freeze **how SemanticEvidence enters the Runtime** —
the Evidence Fusion Boundary, the sole consumer, evidence → Fact conversion,
confidence usage, freshness admission, and Trace/Fact relationship.

This is a **BASELINE ONLY** change: architecture analysis + Decision doc +
OpenSpec proposal/design/spec/tasks. No production behavior is modified.

## Gap (fusion consumption boundary not yet frozen)

The contract baseline defined the evidence shape and provider port, but did not
freeze the Runtime-side consumption boundary: who may consume SemanticEvidence,
how confidence is used, how stale evidence is rejected, and how SemanticEvidence
becomes a Fact via Runtime validation.

## What this change does (BASELINE — design/spec only, APPLY later)

1. Freezes the **Evidence Fusion Boundary**: only
   Observation → Perception Evidence → Evidence Fusion → Runtime Belief → Agent.
   Semantic never bypasses Runtime.
2. Freezes the **sole consumer**: Runtime Evidence Fusion — not Agent, Planner,
   Action Executor, or DSH.
3. Freezes **SemanticEvidence → Fact conversion**: SemanticEvidence + Vision
   Evidence + Container History + Current Observation → Runtime Validation →
   Fact / Belief Update.
4. Freezes **confidence usage**: confidence is only an Evidence Weight, never
   direct Truth.
5. Freezes **Phase 1 Container Identity Recovery** for Scrolled Container
   Identity Drift; Semantic is an extra Evidence Provider, not a Resolver.
6. Freezes **Fast Semantic** (synchronous, bounded, failure → empty evidence) and
   **Slow Semantic** (async, no block, no Fact override, no historical change).
7. Freezes **freshness admission**: ObservationSequence / Timestamp / Scope must
   be validated; stale SemanticEvidence rejected.
8. Freezes **Trace/Fact relationship**: SemanticEvidence may reference
   Observation/Trace but cannot create Fact.
9. Freezes **Vector/LLM isolation**: Runtime knows only `ISemanticProvider`.
10. Defines falsifiers F1–F10.

## Scope

In scope:

- Decision doc: `docs/decisions/semantic-evidence-fusion-baseline.md`
- OpenSpec change: `openspec/changes/semantic-evidence-fusion-baseline/`
  - `proposal.md`
  - `design.md`
  - `specs/semantic-evidence-fusion-baseline/spec.md`
  - `tasks.md`
  - `README.md`
  - `.openspec.yaml`

Out of scope / forbidden:

- Runtime production behavior
- Vector DB
- Embedding
- LLM
- Agent
- Vision
- Assistance/L1
- DSH

## Non-goals

This change does NOT implement:

- Runtime Evidence Fusion production code
- Vector DB / Embedding / LLM integration
- Agent or Planner consumption
- Action Executor / DSH consumption
- Container Identity Recovery production resolver

## Required output

`PROJECT_LEADER_SEMANTIC_EVIDENCE_FUSION_BASELINE_RESULT` with Decision
`SEMANTIC_EVIDENCE_FUSION_BASELINE_FROZEN`, the OpenSpec change
(proposal/design/spec/tasks/README/.openspec.yaml) created and validated, and
`NEXT_GATE = PROJECT_LEADER_APPLY_SEMANTIC_EVIDENCE_FUSION`.

## Falsifiers

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

## Validation

- `openspec validate semantic-evidence-fusion-baseline --type change --strict --no-interactive`
- `scripts/check-consistency.sh`
