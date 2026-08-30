# fast-semantic-container-identity-baseline

**Status**: BASELINE (design only). **State**: decision + OpenSpec
proposal/design/spec/tasks/README defined. Pending APPLY gate
(`PROJECT_LEADER_APPLY_FAST_SEMANTIC_CONTAINER_IDENTITY`).

## One-line

Freeze the Fast Semantic Container Identity Recovery architecture: a bounded
Fast Semantic Evidence Provider that supplies `ContainerIdentity` evidence into
the graduated `SemanticEvidenceFusion` seam, without replacing the Container
Resolver or changing Agent/Vision/Belief authority.

## Why

Scrolled Container Identity Drift occurs when the page title leaves the viewport
and `CreateMultiPageResolver` returns null, causing a false `SemanticContradiction`.
The Fast Semantic provider adds semantic candidate evidence for Runtime Identity
Validation.

## What changes

- **FastSemanticContainerIdentityProvider** under `Capabilities/Perception/Semantic/Fast`
- **IVectorSemanticIndex** + `ContainerSemanticQuery` + `SemanticCandidate`
- **Fast flow**: Observation → Feature Extraction → Vector Retrieval →
  SemanticEvidence → SemanticEvidenceFusion → Runtime Validation
- **Read-only Vector Index**; no Runtime write; no auto-learning
- **Runtime-owned Container Identity Validation**; Semantic is extra evidence
- **Fast/Slow boundary**: Fast sync bounded; Slow future async LLM
- **Tests**: T1–T10 defined for APPLY

## Boundaries

No Agent / Goal / Action / Planner / L1 / DSH / Vision / Resolver / Belief
Authority modifications. No Vector DB / Embedding / LLM / Slow Semantic
implementation in this gate.

## Artifacts

- `docs/decisions/fast-semantic-container-identity-baseline.md`
- `proposal.md`
- `design.md`
- `specs/fast-semantic-container-identity-baseline/spec.md`
- `tasks.md`

## Validation

- `openspec validate fast-semantic-container-identity-baseline --type change --strict --no-interactive`
- `bash scripts/check-consistency.sh`
