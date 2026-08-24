# Semantic Evidence Fusion — APPLY Result

> Date: 2026-08-19
> Role: Project Leader / Implementation Verifier
> Gate: `PROJECT_LEADER_APPLY_SEMANTIC_EVIDENCE_FUSION`
> Base: `PROJECT_LEADER_SEMANTIC_EVIDENCE_FUSION_BASELINE_RESULT` (frozen)
> Result: `PROJECT_LEADER_APPLY_SEMANTIC_EVIDENCE_FUSION_RESULT`
> Status: **APPLIED (Minimal Runtime Evidence Fusion integration)**

## 1. Implemented scope

Minimal Runtime Evidence Fusion interface skeleton + validation chain. Semantic
remains an Evidence Provider; Runtime remains the only Belief Authority. No
Vector DB, Embedding, LLM, Slow Semantic, Agent decision logic, Goal, Action,
Vision Service, Assistance/L1, DSH, or Memory write is implemented or modified.

## 2. Files added

| File | Purpose |
|---|---|
| `src/UniClaw.Runtime/Capabilities/Perception/Semantic/Fusion/SemanticEvidenceFusionInput.cs` | Fusion input: Current Observation + Vision Evidence + SemanticEvidence + Container History + Existing Belief Context + known Observation/Trace refs |
| `src/UniClaw.Runtime/Capabilities/Perception/Semantic/Fusion/SemanticEvidenceFusionResult.cs` | Output: AcceptedEvidence / RejectedEvidence / ValidationReasons / ConfidenceWeights (no Action/Goal/Plan/World mutation) |
| `src/UniClaw.Runtime/Capabilities/Perception/Semantic/Fusion/ISemanticEvidenceFusion.cs` | `ISemanticEvidenceFusion` port — sole SemanticEvidence consumer seam |
| `src/UniClaw.Runtime/Capabilities/Perception/Semantic/Fusion/SemanticEvidenceFusion.cs` | Validation pipeline: Freshness → Scope → Reference → Compatibility |
| `src/UniClaw.Runtime/Capabilities/Perception/Semantic/Fusion/ContainerIdentityEvidenceFusion.cs` | Reserved `IContainerIdentityEvidenceFusion` interface (Phase 1 reservation; no resolver replacement) |
| `src/UniClaw.Runtime/Capabilities/Perception/Semantic/Fusion/NoOpSemanticProvider.cs` | Default `ISemanticProvider` returning empty evidence |
| `src/UniClaw.Runtime/Capabilities/Perception/Semantic/Fusion/SemanticEvidenceFusionPipeline.cs` | Minimal wiring: provider → resolve → fuse (NoOp default) |
| `tests/UniClaw.Runtime.Tests/Perception/SemanticEvidenceFusionTests.cs` | T1–T10 |

## 3. Contract conformance

- **Evidence Fusion Boundary**: `Observation → Perception Evidence → Evidence Fusion → Runtime Belief → Agent`. The new seam is a fusion input/output only; there is no `Semantic → Agent → Action` path (F1).
- **Sole consumer**: only `ISemanticEvidenceFusion` consumes SemanticEvidence in the new code; no Agent / Planner / Action Executor / DSH consumer was added (F8).
- **Fact conversion**: fusion produces `ValidatedSemanticEvidenceResult` (evidence + weights), never a Fact. Fact remains Runtime Belief System-owned (F2).
- **Confidence**: only carried as `ConfidenceWeight` (Evidence Weight); no threshold→Truth and no belief/truth field is produced (F4).
- **Freshness**: rejects stale observation sequence / expired `ValidUntil` (F5); validates scope and Observation/Trace references.
- **Container Identity**: `IContainerIdentityEvidenceFusion` is a reserved interface; `CreateMultiPageResolver` / `ContainerIdentityResolver` are NOT changed or replaced (F8).
- **Fast/Slow isolation**: default `NoOpSemanticProvider` returns empty evidence; no Vector/Embedding/LLM in Runtime (F6/F7 isolation).
- **Trace**: Observation/Trace references are validated; Fact references are rejected as reserved (F2).

## 4. Change summary (required)

| Question | Answer |
|---|---|
| **修改文件** | 8 files (7 new production/types under `Capabilities/Perception/Semantic/Fusion/` + 1 new test file). No existing file modified. |
| **是否改变 Runtime 行为** | NO — additive seam only; no existing Runtime decision path is wired or changed. |
| **是否改变 Agent authority** | NO — Agent untouched. |
| **是否改变 Vision responsibility** | NO — Vision untouched; Vision-only path unchanged (T9). |
| **是否改变 L1 boundary** | NO — Assistance/L1 untouched. |
| **测试结果** | `SemanticEvidenceFusionTests` T1–T10: 10/10 PASS. |

## 5. Test matrix

| # | Test | Result |
|---|---|---|
| T1 | Empty SemanticEvidence → Runtime behavior unchanged | ✅ |
| T2 | Fresh SemanticEvidence accepted | ✅ |
| T3 | Stale SemanticEvidence rejected | ✅ |
| T4 | Wrong ObservationSequence rejected | ✅ |
| T5 | Confidence not converted to Truth | ✅ |
| T6 | SemanticEvidence cannot bypass Runtime | ✅ |
| T7 | No SemanticProvider still works | ✅ |
| T8 | ContainerIdentity interface exists, no behavior replacement | ✅ |
| T9 | Vision-only path unchanged | ✅ |
| T10 | Agent receives only Runtime Belief result | ✅ |

## 6. Validation evidence

```text
dotnet build src/UniClaw.Runtime/UniClaw.Runtime.csproj
→ 0 warnings, 0 errors

dotnet build tests/UniClaw.Runtime.Tests/UniClaw.Runtime.Tests.csproj
→ 0 warnings, 0 errors

dotnet test --filter SemanticEvidenceFusionTests
→ 10/10 PASS

openspec validate semantic-evidence-fusion-baseline --type change --strict --no-interactive
→ PASS

scripts/check-consistency.sh
→ ALL PASS
```

## 7. Non-goals respected

No Vector Database, Embedding, LLM, Slow Semantic implementation; no Agent / Goal
/ Action / Vision / Assistance/L1 / DSH / Memory changes.

## 8. Next step

Minimal fusion integration complete and validated. Ready for
`PROJECT_LEADER_SEMANTIC_EVIDENCE_FUSION_GRADUATION_REVIEW`.
