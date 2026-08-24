# Proposal: semantic-perception-layer-baseline

## Buyer

Scrollable-container identity recovery is a real Runtime failure: when a page
title leaves the viewport inside a scrollable container, the Vision resolver can
return `null`, causing a false `SemanticContradiction`. The world has not left
the container; the visible identity anchor has simply scrolled out of view. This
change establishes the **Semantic Perception Layer** baseline so that a bounded
Semantic capability can later recover Container Identity from evidence beyond the
currently visible pixel/text title.

This is a **BASELINE ONLY** change: architecture decision + OpenSpec
proposal/design/spec/tasks. No production code is implemented in this change.

## Gap (verified repository truth)

- `CreateMultiPageResolver` / Vision-based page resolution depends on which text
  anchors are currently visible in the viewport.
- In a scrollable container, the page title can leave the viewport after
  scrolling or after a state-changing action.
- When the title is not visible, the resolver returns `null`.
- The Runtime currently interprets `null` identity as unresolved semantic page
  and can raise `SemanticContradiction`.
- The defect is a perception-layer identity-evidence gap, not an Agent semantic
  decision gap.

## What this change does (BASELINE — design/spec only, APPLY later)

1. Freezes the architecture: **Semantic belongs to Perception Layer**, not Agent.
2. Freezes **Vision and Semantic as parallel perception capabilities**.
3. Defines Semantic output as **SemanticEvidence only**, never a Decision.
4. Freezes Runtime as the sole authority for Evidence Fusion, ContainerIdentity,
   Binding, Belief update, and Action authority.
5. Freezes Semantic input boundaries (allowed: Current Observation, Visible
   Elements, Container History, Previous Verified Identity; forbidden: Goal,
   Action command, Expected state, Planning context).
6. Freezes Phase 1 scope as **Container Identity Recovery only**.
7. Defines Fast Semantic (vector retrieval) and Slow Semantic (LLM semantic
   reasoning) evolution; Slow MUST be async checkpoint evidence and MUST NOT
   block Runtime main control flow.
8. Freezes memory boundary: Semantic knowledge is read-only; Runtime automatic
   vector writes are forbidden. Future memory pipeline is an independent
   capability.

## Scope

In scope:

- Decision document: `docs/decisions/semantic-perception-layer-baseline.md`
- OpenSpec change: `openspec/changes/semantic-perception-layer-baseline/`
  - `proposal.md`
  - `design.md`
  - `specs/semantic-perception-layer-baseline/spec.md`
  - `tasks.md`

Out of scope:

- Runtime production code
- Vision service
- Assistance system
- DSH integration
- Any implementation of Fast/Slow Semantic, Vector database, Memory system,
  LLM controller, Agent, Planner, or Action generator

## Non-goals

This change does NOT create:

- Agent
- Planner
- Memory system
- LLM controller
- Action generator
- ElementRelation
- Binding Semantic
- Memory Learning
- LLM reasoning implementation
- Vector database implementation

## Required output

`PROJECT_LEADER_SEMANTIC_PERCEPTION_LAYER_BASELINE_RESULT` with Decision
`SEMANTIC_PERCEPTION_LAYER_BASELINE_FROZEN`, the OpenSpec change
(proposal/design/spec/tasks) created and validated, and `NEXT_GATE =
PROJECT_LEADER_APPLY_SEMANTIC_PERCEPTION_LAYER_BASELINE`.

## Falsifiers

| # | Falsifier | Fails if |
|---|---|---|
| F1 | Semantic-as-Agent | Semantic owns Decision, Goal, Planning, or Action authority |
| F2 | Semantic-not-perception | Semantic is placed inside Agent rather than Perception Layer |
| F3 | Vision-subsumes-Semantic | Vision and Semantic are not modeled as parallel perception capabilities |
| F4 | Decision output | Semantic produces a Decision instead of SemanticEvidence |
| F5 | Runtime authority diluted | Runtime loses sole authority over fusion, identity, binding, belief, or action |
| F6 | input-boundary violation | Semantic receives Goal, Action command, Expected state, or Planning context |
| F7 | phase-1 expansion | Phase 1 implements anything beyond Container Identity Recovery |
| F8 | slow blocking | Slow Semantic can block the Runtime main control flow |
| F9 | memory learning | Runtime auto-learns or writes into Vector in this change |
| F10 | production mutation | this baseline change modifies Runtime/Vision/Assistance/DSH production code |

## Validation

- `openspec validate semantic-perception-layer-baseline --type change --strict --no-interactive`
- `openspec validate --changes --strict --no-interactive`
- `scripts/check-consistency.sh`
