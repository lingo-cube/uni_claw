# Tasks: semantic-perception-contract-baseline

> System of record. BASELINE frozen; APPLY gate in progress
> (`PROJECT_LEADER_APPLY_SEMANTIC_PERCEPTION_CONTRACT`). A1/A2 implemented as
> type-level definitions only (no runtime wiring). A3–A6 remain FUTURE gates.

## Slices (this gate)

- [x] Slice 0 — OpenSpec change scaffolding (proposal/design/spec/tasks/README)
- [x] Slice 1 — Decision document: `docs/decisions/semantic-perception-contract-baseline.md`
- [x] Slice 2 — SemanticEvidence contract freeze (identity, semantic type,
      candidate, confidence, scope, freshness, evidence references)
- [x] Slice 3 — SemanticEvidence lifecycle freeze (Semantic → Runtime Validation →
      Fact / Belief Update; Semantic does not produce Fact)
- [x] Slice 4 — ISemanticProvider interface freeze (query / reason / evidence
      only; no Action / Goal / Plan / World mutation)
- [x] Slice 5 — Fast Semantic freeze (synchronous, bounded latency, vector
      retrieval, no reasoning loop, failure → null)
- [x] Slice 6 — Slow Semantic freeze (async, cannot block/override Runtime,
      failure ignored)
- [x] Slice 7 — Vector Storage boundary freeze (not Runtime/Agent/Vision; validated
      patterns only; no Runtime automatic write)
- [x] Slice 8 — Runtime Consumption boundary freeze (Observation → Perception
      Evidence → Evidence Fusion → Belief; no Semantic → Agent → Action)
- [x] Slice 9 — Container Identity Recovery Phase 1 freeze (Scrolled Container
      Identity Drift; Semantic is supplementary resolver)
- [x] Slice 10 — Vision / Semantic / Runtime question boundary freeze
- [x] Slice 11 — Trace / Fact relationship freeze
- [x] Slice 12 — Falsifiers F1–F10 defined and mapped
- [x] Slice 13 — Validation: `openspec validate semantic-perception-contract-baseline
      --strict` + `scripts/check-consistency.sh`

## Implementation plan (APPLY gate)

- [x] A1 — Implement SemanticEvidence DTO shape (type-level definitions only; no
      runtime wiring) — `src/UniClaw.Runtime/Capabilities/Perception/Semantic/SemanticEvidence.cs`
- [x] A2 — Implement ISemanticProvider interface (type-level definitions only; no
      runtime wiring) — `src/UniClaw.Runtime/Capabilities/Perception/Semantic/ISemanticProvider.cs`
- [ ] A3 — Fast Semantic vector retrieval adapter (future)
- [ ] A4 — Slow Semantic async LLM checkpoint (future)
- [ ] A5 — Runtime Evidence Fusion consumption seam (future)
- [ ] A6 — Container Identity Recovery Phase 1 tests (future)

## Falsifier mapping

- [x] F1 — Semantic cannot execute action (spec: Provider interface +
      falsifier requirement)
- [x] F2 — Semantic cannot complete goal (spec: Provider interface +
      falsifier requirement)
- [x] F3 — Semantic cannot mutate world (spec: Provider interface + falsifier
      requirement)
- [x] F4 — Semantic cannot bypass Runtime (spec: lifecycle + consumption boundary)
- [x] F5 — Vector retrieval failure => null (spec: Fast Semantic requirement)
- [x] F6 — LLM failure => null (spec: Slow Semantic requirement)
- [x] F7 — No automatic Runtime learning (spec: Vector Storage boundary)
- [x] F8 — No Agent replacement (spec: Phase 1 + falsifier requirement)
- [x] F9 — No Vision responsibility expansion (spec: Vision/Semantic/Runtime
      question boundary)
- [x] F10 — No L2 planning capability (spec: Slow Semantic + falsifier
      requirement)

## Validation record

- [x] `openspec validate semantic-perception-contract-baseline --type change --strict --no-interactive` — PASS
- [x] `scripts/check-consistency.sh` — ALL PASS
- [x] `dotnet build src/UniClaw.Runtime/UniClaw.Runtime.csproj` — 0 warnings, 0 errors (Runtime compiles; pre-existing unrelated test-file error in untracked `ExecutionHypothesisLedgerTests.cs` remains outside this change scope)
