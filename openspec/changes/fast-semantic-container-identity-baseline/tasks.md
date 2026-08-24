# Tasks: fast-semantic-container-identity-baseline

> System of record. BASELINE frozen; APPLY gate executed minimal Fast Semantic
> Container Identity integration (`PROJECT_LEADER_APPLY_FAST_SEMANTIC_CONTAINER_IDENTITY`).

## Slices (this gate)

- [x] Slice 0 — OpenSpec change scaffolding (proposal/design/spec/tasks/README/.openspec.yaml)
- [x] Slice 1 — Decision document: `docs/decisions/fast-semantic-container-identity-baseline.md`
- [x] Slice 2 — FastSemanticContainerIdentityProvider definition (input/output boundary, forbidden inputs)
- [x] Slice 3 — IVectorSemanticIndex / ContainerSemanticQuery / SemanticCandidate abstraction
- [x] Slice 4 — Fast Semantic flow freeze (Observation → Feature Extraction → Vector Retrieval → SemanticEvidence → Fusion → Runtime Validation)
- [x] Slice 5 — Vector Memory boundary freeze (read-only; no Runtime write; no auto-learning)
- [x] Slice 6 — Container Identity Validation freeze (Runtime-owned; Semantic is extra evidence)
- [x] Slice 7 — Fast / Slow boundary freeze (Fast sync bounded; Slow future async LLM)
- [x] Slice 8 — Test matrix T1–T10 defined
- [x] Slice 9 — Strict boundary freeze (no Agent / Goal / Action / Planner / L1 / DSH / Vision / Resolver / Belief Authority change)
- [x] Slice 10 — Validation: `openspec validate fast-semantic-container-identity-baseline
      --strict` + `scripts/check-consistency.sh`

## Implementation plan (APPLY gate)

- [x] A1 — Implement `FastSemanticContainerIdentityProvider`
- [x] A2 — Implement `IVectorSemanticIndex` + `ContainerSemanticQuery` + `SemanticCandidate` + `InMemoryVectorSemanticIndex`
- [x] A3 — Fast Semantic pipeline wiring into `SemanticEvidenceFusion`
- [x] A4 — Implement T1–T12 tests (`FastSemanticContainerIdentityTests`)
- [x] A5 — Verify no Agent / Resolver / Belief Authority change

## Falsifier mapping (design-level)

- [x] F1 — Fast Semantic does not bypass Runtime
- [x] F2 — Fast Semantic does not directly modify Belief
- [x] F3 — Fast Semantic does not execute Action
- [x] F4 — Confidence does not equal Truth
- [x] F5 — Vector miss returns empty evidence
- [x] F6 — No Vector provider keeps Runtime unchanged
- [x] F7 — No Agent replacement
- [x] F8 — No Resolver replacement
- [x] F9 — No Vision responsibility expansion
- [x] F10 — No L2 planning capability

## Validation record

- [x] `openspec validate fast-semantic-container-identity-baseline --type change --strict --no-interactive` — PASS
- [x] `scripts/check-consistency.sh` — ALL PASS
- [x] `dotnet build src/UniClaw.Runtime/UniClaw.Runtime.csproj` — 0 warnings, 0 errors
- [x] `dotnet build tests/UniClaw.Runtime.Tests/UniClaw.Runtime.Tests.csproj` — 0 warnings, 0 errors
- [x] `dotnet test --filter FastSemanticContainerIdentity` — 12/12 PASS
