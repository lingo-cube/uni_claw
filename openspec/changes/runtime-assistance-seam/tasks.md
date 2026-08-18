# Tasks: runtime-assistance-seam

> System of record. THIS GATE IS BASELINE ONLY (proposal/design/spec/tasks +
> validation). Implementation tasks are pending the APPLY gate.

## Slices (this gate)

- [x] Slice 0 — OpenSpec change scaffolding (proposal/design/spec/README/.openspec.yaml)
- [x] Slice 1 — Verified source baseline (belief three-value model, adjudication
      points, world-version anchor, injection precedent, mother-doc seam, charter
      Brain domain, Guard 2 evidence)
- [x] Slice 2 — Seam shape (IAssistanceProvider / AssistanceContext /
      AssistanceAdvice; placement Capabilities/Brain; naming decision + mother-doc
      mapping)
- [x] Slice 3 — Adjudication call points (Contradicted consult-before-fail;
      Unresolved explicit consult point; non-adjudication points out of scope)
- [x] Slice 4 — Advice-mode consumption + bounded consult discipline
- [x] Slice 5 — World-version binding/staleness + correlation (contract primitives)
- [x] Slice 6 — Guard 2 compliance + injection/backward-compat + behavior-preserving
      guarantees
- [x] Validation — openspec validate --strict, check-consistency.sh, contract +
      mother-doc cross-check

## Implementation plan (APPLY gate — EXECUTED 2026-08-17)

- [x] A1 — Add `Capabilities/Brain/IAssistanceProvider.cs` (+ `AssistanceContext`,
      `AssistanceAdvice`) — BCL + Model only
      (`src/UniClaw.Runtime/Capabilities/Brain/IAssistanceProvider.cs`)
- [x] A2 — Agent: optional `IAssistanceProvider?` constructor param (null = today)
      (`Agent/Agent.cs`: field + ctor param + MaxAssistanceConsults=3 budget)
- [x] A3 — Adjudication call points (Contradicted + Unresolved) with bounded
      consult discipline, advice consumption (re-observe/rebind/dismiss or fail
      closed), world-version staleness check
      (`Agent/Agent.SemanticRun.cs`: DECIDE block + ConsultAssistanceAsync +
      TryApplyAssistanceAdviceAsync)
- [x] A4 — Tests: fake provider (test-side); contradicted-consult → resolve or
      fail-closed; stale advice discarded; null provider zero regression; consult
      failure fails closed; bounded attempts; guard scans (no external types, no
      new emitters)
      (`tests/.../Capabilities/FakeAssistanceProvider.cs` +
      `tests/.../Scenario/AssistanceSeamTests.cs` — 7 tests)

## Implementation evidence (A4)

- [x] Contradicted → consult "re-observe" → external world transition (seq3) →
      continuity verified → SAME goal → SetSwitch → SATISFIED (provider.Consults ≥ 1)
- [x] Null provider → SemanticContradiction, zero SetSwitch (zero regression)
- [x] Stale advice (WorldVersion+1) → discarded → SemanticContradiction, Consults == 1
- [x] Consult throws → caught → SemanticContradiction, Consults == 1 (no process fault)
- [x] Actionable "re-observe" loop on an unchanging world → bounded: Consults == 3
      (MaxAssistanceConsults) → SemanticContradiction (no unbounded loop)
- [x] "rebind" advice → existing rebind mechanism applied → bounded fail-closed
- [x] Context snapshot: RunId / SemanticPage / BeliefState=Contradicted /
      WorldVersion=2 (Agent initial observation seq) / correlation RequestId
- [x] Regression: AgentSemanticClosedLoop + Scenario + DriverHost + Architecture
      suites 726/726 PASS (zero regression; guards clean — no external types, no
      new emitters, Guard 2/10b token scans pass)

## Falsifier mapping (gate §Proposal)

- [x] F1 — interface depends only on BCL + Model (no DSH/Cordis/model types)
- [x] F2 — advice never writes belief/binding/state
- [x] F3 — call points limited to belief adjudication surface
- [x] F4 — advice never truth/authorization/goal-completion
- [x] F5 — stale advice discarded (world-version binding)
- [x] F6 — null provider = today's fail-closed (zero regression)
- [x] F7 — no DSH-side implementation in this change
- [x] F8 — no new RuntimeEvent kinds/emitters
- [x] F9 — consult failure fails closed (never progress)
- [x] F10 — all repository-reality claims verified at source
