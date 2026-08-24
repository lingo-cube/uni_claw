# Proposal: fast-semantic-container-identity-baseline

## Buyer

`PROJECT_LEADER_SEMANTIC_EVIDENCE_FUSION_GRADUATED` is the base. The next target
is **Scrolled Container Identity Drift**: when a page title leaves the viewport,
`CreateMultiPageResolver` returns null, the container continuity check fails, and
Runtime raises a false `SemanticContradiction`.

This change freezes the **Fast Semantic Container Identity Recovery** architecture:
a bounded Fast Semantic Evidence Provider that supplies ContainerIdentity evidence
into the graduated `SemanticEvidenceFusion` seam — without replacing the resolver
or changing Agent/Vision/Belief authority.

This is a **BASELINE ONLY** change: architecture design + Decision doc + OpenSpec
proposal/design/spec/tasks. No production implementation.

## Gap (current architecture lacks Fast Semantic evidence)

The graduated Semantic Evidence Fusion seam exists, but there is no Fast Semantic
Container Identity provider/interface design. The Runtime still depends only on
the Text Resolver for container identity. When visible text anchors scroll out of
view, no SemanticCandidate evidence is available to help Runtime Identity
Validation.

## What this change does (BASELINE — design/spec only, APPLY later)

1. Defines `FastSemanticContainerIdentityProvider` under
   `Capabilities/Perception/Semantic/Fast`.
2. Defines `IVectorSemanticIndex` + `ContainerSemanticQuery` + `SemanticCandidate`.
3. Freezes the Fast Semantic flow:
   Observation → Feature Extraction → Vector Retrieval → SemanticEvidence →
   SemanticEvidenceFusion → Runtime Validation.
4. Freezes Vector Memory boundary: read-only Vector Index only; no Runtime write.
5. Freezes Container Identity Validation: Text Resolver remains; Semantic is an
   extra evidence source; Runtime decides whether to recover Container Identity.
6. Freezes Fast/Slow boundary: Fast is sync bounded vector retrieval; Slow is
   future async LLM checkpoint; this change does not implement Slow.
7. Defines tests T1–T10 for the APPLY gate.

## Scope

In scope:

- Decision doc: `docs/decisions/fast-semantic-container-identity-baseline.md`
- OpenSpec change: `openspec/changes/fast-semantic-container-identity-baseline/`
  - `proposal.md`
  - `design.md`
  - `specs/fast-semantic-container-identity-baseline/spec.md`
  - `tasks.md`
  - `README.md`
  - `.openspec.yaml`

Out of scope / forbidden:

- Agent / Goal / Action / Planner / L1 / DSH modifications
- Vision Service modification
- CreateMultiPageResolver / ContainerIdentityResolver modification
- Belief Authority modification
- Slow Semantic implementation
- Vector write path

## Non-goals

This change does NOT implement:

- Vector Database
- Embedding
- LLM Semantic
- Real Semantic Provider
- Fast Semantic Provider production code
- Container Resolver replacement
- Runtime Belief modification

## Required output

`PROJECT_LEADER_FAST_SEMANTIC_CONTAINER_IDENTITY_BASELINE_RESULT` with Decision
`FAST_SEMANTIC_CONTAINER_IDENTITY_BASELINE_FROZEN`, the OpenSpec change
(proposal/design/spec/tasks/README/.openspec.yaml) created and validated, and
`NEXT_GATE = PROJECT_LEADER_APPLY_FAST_SEMANTIC_CONTAINER_IDENTITY`.

## Validation

- `openspec validate fast-semantic-container-identity-baseline --type change --strict --no-interactive`
- `scripts/check-consistency.sh`
