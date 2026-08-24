# Tasks: semantic-evidence-fusion-baseline

> System of record. BASELINE frozen; APPLY gate executed minimal Runtime
> Evidence Fusion integration (`PROJECT_LEADER_APPLY_SEMANTIC_EVIDENCE_FUSION`).

## Slices (this gate)

- [x] Slice 0 — OpenSpec change scaffolding (proposal/design/spec/tasks/README/.openspec.yaml)
- [x] Slice 1 — Decision document: `docs/decisions/semantic-evidence-fusion-baseline.md`
- [x] Slice 2 — Evidence Fusion Boundary freeze (Observation → Perception Evidence →
      Evidence Fusion → Runtime Belief → Agent; no Semantic → Agent → Action)
- [x] Slice 3 — Sole consumer freeze (Runtime Evidence Fusion only; no Agent /
      Planner / Action Executor / DSH direct consumption)
- [x] Slice 4 — SemanticEvidence → Fact conversion freeze (Semantic + Vision +
      Container History + Current Observation → Runtime Validation → Fact/Belief)
- [x] Slice 5 — Confidence usage freeze (Evidence Weight only, never Truth)
- [x] Slice 6 — Container Identity Recovery Phase 1 freeze (Semantic is extra
      evidence provider, not Resolver)
- [x] Slice 7 — Fast Semantic freeze (synchronous, bounded, empty failure) and
      Slow Semantic freeze (async, no block/no override/no historical change)
- [x] Slice 8 — Freshness admission freeze (ObservationSequence / Timestamp /
      Scope; stale evidence rejected)
- [x] Slice 9 — Trace / Fact relationship freeze
- [x] Slice 10 — Vector / LLM isolation freeze (Runtime depends only on
      ISemanticProvider)
- [x] Slice 11 — Falsifiers F1–F10 defined and mapped
- [x] Slice 12 — Validation: `openspec validate semantic-evidence-fusion-baseline
      --strict` + `scripts/check-consistency.sh`

## Implementation plan (APPLY gate)

- [x] A1 — Runtime Evidence Fusion consuming seam (`ISemanticEvidenceFusion`,
      `SemanticEvidenceFusionInput`, `ValidatedSemanticEvidenceResult`)
- [x] A2 — Freshness/Scope/Reference/Compatibility validation pipeline
      (`SemanticEvidenceFusion`)
- [x] A3 — Container Identity interface reservation (`IContainerIdentityEvidenceFusion`;
      no resolver replacement)
- [x] A4 — Fast/Slow provider wiring seam (default `NoOpSemanticProvider`;
      `SemanticEvidenceFusionPipeline`)
- [x] A5 — Tests T1–T10 (`SemanticEvidenceFusionTests`)

## Falsifier mapping

- [x] F1 — Semantic cannot bypass Runtime (spec: Evidence Fusion Boundary)
- [x] F2 — Semantic cannot directly modify Belief (spec: Fact conversion +
      Trace/Fact relationship)
- [x] F3 — Semantic cannot execute Action (spec: Evidence Fusion Boundary +
      Falsifier requirement)
- [x] F4 — Confidence cannot equal Truth (spec: Confidence is an Evidence Weight)
- [x] F5 — Stale SemanticEvidence rejected (spec: Freshness admission)
- [x] F6 — Vector failure returns empty evidence (spec: Fast Semantic)
- [x] F7 — LLM failure returns empty evidence (spec: Slow Semantic)
- [x] F8 — No Agent replacement (spec: Sole consumer + Phase 1)
- [x] F9 — No Vision responsibility expansion (spec: Vector/LLM isolation +
      Falsifier requirement)
- [x] F10 — No L2 planning capability (spec: Slow Semantic + Falsifier requirement)

## Validation record

- [x] `openspec validate semantic-evidence-fusion-baseline --type change --strict --no-interactive` — PASS
- [x] `scripts/check-consistency.sh` — ALL PASS
- [x] `dotnet build src/UniClaw.Runtime/UniClaw.Runtime.csproj` — 0 warnings, 0 errors
- [x] `dotnet build tests/UniClaw.Runtime.Tests/UniClaw.Runtime.Tests.csproj` — 0 warnings, 0 errors
- [x] `dotnet test --filter SemanticEvidenceFusionTests` — 10/10 PASS
