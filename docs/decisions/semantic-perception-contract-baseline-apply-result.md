# Semantic Perception Contract Baseline — APPLY Result

> Date: 2026-08-19
> Role: Project Leader / Implementation Verifier
> Gate: `PROJECT_LEADER_APPLY_SEMANTIC_PERCEPTION_CONTRACT`
> Base: `PROJECT_LEADER_SEMANTIC_PERCEPTION_CONTRACT_BASELINE_RESULT` (frozen)
> Result: `PROJECT_LEADER_APPLY_SEMANTIC_PERCEPTION_CONTRACT_RESULT`
> Status: **APPLIED (A1/A2) — A3–A6 DEFERRED TO FUTURE GATES**

## 1. Scope of this APPLY

This APPLY gate implements the **type-level contract shapes** defined by the frozen
Semantic Perception Contract Baseline. It delivers **no runtime wiring, no fusion,
no resolver, and no functional behavior**. A3–A6 are explicitly future gates.

Implemented:

- **A1 — SemanticEvidence DTO shape**
- **A2 — ISemanticProvider interface**
- `ObservationContext` input context (allowed Semantic inputs only)

Deferred to future gates:

- A3 — Fast Semantic vector retrieval adapter (future)
- A4 — Slow Semantic async LLM checkpoint (future)
- A5 — Runtime Evidence Fusion consumption seam (future)
- A6 — Container Identity Recovery Phase 1 tests (future)

## 2. Files added

| File | Concept |
|---|---|
| `src/UniClaw.Runtime/Capabilities/Perception/Semantic/SemanticEvidence.cs` | `SemanticEvidence` DTO + `SemanticEvidenceKind` (ContainerIdentity), `SemanticEvidenceScope`, `SemanticEvidenceReference` |
| `src/UniClaw.Runtime/Capabilities/Perception/Semantic/ISemanticProvider.cs` | `ISemanticProvider` port + `ObservationContext` |

These live under `Capabilities/Perception/Semantic`, mirroring the Vision
capability placement (`Capabilities/Perception/Vision`) and reinforcing that
Semantic is a **Perception Layer** capability, not an Agent component.

## 3. Contract conformance

- `SemanticEvidence` contains identity (`EvidenceId` / `Version` / `Source`),
  kind (`ContainerIdentity` Phase 1), candidate, confidence [0,1], scope
  (`CurrentObservation` / `CurrentContainer` / `HistoricalContext`), freshness
  (`ObservationSequence` / `CreatedAt` / `ValidUntil`), and optional references
  (`Observation` / `Trace` / `Fact`).
- `SemanticEvidence` is evidence only — it carries no Action, Goal, Plan, World
  mutation, or Decision (F1–F3).
- `ISemanticProvider.ResolveAsync(ObservationContext)` returns
  `ImmutableArray<SemanticEvidence>` and is query/reason/evidence only (F1–F3).
- `ObservationContext` accepts only allowed inputs and never Goal / Action
  command / Expected state / Planning context (F6).
- No Runtime fusion, no Agent wiring, no Vector writing (F4/F7), no Agent
  replacement (F8), no Vision responsibility expansion (F9), no L2 planning (F10).

## 4. Falsifier compliance

| # | Falsifier | Status |
|---|---|---|
| F1 | Semantic cannot execute action | ✓ (interface/DTO only) |
| F2 | Semantic cannot complete goal | ✓ |
| F3 | Semantic cannot mutate world | ✓ |
| F4 | Semantic cannot bypass Runtime | ✓ — no wiring to Agent/Action |
| F5 | Vector retrieval failure => null | Deferred to A3 (empty result shape supports it) |
| F6 | LLM failure => null | Deferred to A4 (empty result shape supports it) |
| F7 | No automatic Runtime learning | ✓ — no Vector write path |
| F8 | No Agent replacement | ✓ |
| F9 | No Vision responsibility expansion | ✓ |
| F10 | No L2 planning capability | ✓ |

## 5. Validation evidence

```text
openspec validate semantic-perception-contract-baseline --type change --strict --no-interactive
→ PASS

scripts/check-consistency.sh
→ ALL PASS (C1–C10)

dotnet build src/UniClaw.Runtime/UniClaw.Runtime.csproj
→ 0 warnings, 0 errors
```

Note: full-solution `dotnet build src/UniClaw.Runtime.sln` is blocked by a
**pre-existing, unrelated** compile error in the untracked test file
`tests/UniClaw.Runtime.Tests/Unit/Directive/ExecutionHypothesisLedgerTests.cs`
(`IEnumerable<ExecutionHypothesis>` has no `Length`). That file belongs to a
different in-progress change and is outside this APPLY scope.

## 6. Non-goals respected

No modification to Runtime production behavior, Vision Service, Agent,
Assistance/L1, DSH. No Vector Database, no LLM Consumer, no Fast/Slow resolver,
no fusion seam, no tests (all deferred).

## 7. Next step

Contract shape (A1/A2) is complete and validated. A3–A6 remain future gates.
This change is ready for graduation review or a subsequent
Fast/Slow/Container-Identity-Recovery implementation gate.
