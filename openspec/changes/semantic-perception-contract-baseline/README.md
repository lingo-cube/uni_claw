# semantic-perception-contract-baseline

**Status**: APPLIED (A1/A2 type-level definitions). **State**: contract shape
implemented and validated. A3–A6 remain future gates
(`PROJECT_LEADER_APPLY_SEMANTIC_PERCEPTION_CONTRACT`).

## One-line

Freeze the runtime contract of the Semantic Perception Layer: SemanticEvidence
shape, evidence → Fact lifecycle, ISemanticProvider boundary, Fast/Slow execution,
Vector Storage boundary, Runtime consumption boundary, and Container Identity
Recovery Phase 1.

## Why

The Layer Baseline froze *where* Semantic lives. This Contract Baseline freezes
*how* Semantic may be consumed by Runtime and how evidence becomes Fact, while
preventing Semantic from becoming an Agent.

## What changes

- **SemanticEvidence contract**: identity, type (ContainerIdentity Phase 1),
  candidate, confidence 0-1, scope, freshness, references.
- **Lifecycle**: Semantic → Runtime Validation → Fact / Belief Update. Semantic
  does NOT produce Fact.
- **ISemanticProvider**: query / reason / evidence only.
- **Fast Semantic**: synchronous, bounded-latency vector retrieval, failure → null.
- **Slow Semantic**: async LLM checkpoint, cannot block/override Runtime,
  failure ignored.
- **Vector Storage**: not Runtime/Agent/Vision; validated patterns only; no
  Runtime auto-write.
- **Runtime consumption**: Observation → Perception Evidence → Evidence Fusion →
  Belief; no Semantic → Agent → Action.
- **Phase 1**: Container Identity Recovery for Scrolled Container Identity Drift.
- **Falsifiers**: F1–F10 frozen.

## Artifacts

- `docs/decisions/semantic-perception-contract-baseline.md`
- `proposal.md`
- `design.md`
- `specs/semantic-perception-contract-baseline/spec.md`
- `tasks.md`

## Validation

- `openspec validate semantic-perception-contract-baseline --type change --strict --no-interactive`
- `bash scripts/check-consistency.sh`
