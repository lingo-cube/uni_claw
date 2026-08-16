## 1. RuntimeEvent Projection

- [x] 1.1 Implement the consumer-side RuntimeEvent projection surface that derives `RuntimeEventEnvelope` values from the existing `TraceRun` spans (structural skeleton) and the Agent public read model (`State`, `Belief`, `Trace`, `LastTrap`, `RecoveryAnchor`, `NavigationEvidence`, `BranchProgress`).
  - Implementation: Projection lives in the DriverHost/integration direction; `src/UniClaw.Runtime/` is untouched. `Sequence` is projection-assigned monotonic ordering metadata; `ObservationSequence` references Kernel `Observation.SequenceNumber` only when attributable.
  - Invariant Verification: Projection != ownership; no second mutable owner; no new Runtime emitter, buffer, or dependency; `RuntimeObservability` unchanged; C-class events (`DecisionProposed`, `DecisionAccepted`, `ActionAuthorized`, `RecoveryVerified`) never synthesized from Reason strings, dispatch, success, or trace ordering.
  - Test Verification: A/B/C source-classification conformance tests pass; truthful-absence tests pass (ActionDispatched implies no ActionAuthorized; RunCompleted implies no fabricated full GoalEvidence).

- [x] 1.2 Implement the audited 18-family EventKind vocabulary with per-kind classification metadata (`A` / `B` / `C`) recorded in the projection output.
  - Implementation: `ContainerReconciled` derives from the `container.refresh` span; the span-portion of `ActionDispatched` derives from the `traversal.execution` span; `B`-family events derive from the public read model only.
  - Invariant Verification: Every emitted EventKind matches its audited classification; no event is emitted without a truthful source.
  - Test Verification: Classification-table conformance tests pass for all 18 families (A=1, A+B=1, B=12, C=4).

- [x] 1.3 Implement append-only cursor delivery with stable `EventId`, monotonic projected `Sequence`, duplicate-safe consumption, and reconnect-from-cursor.
  - Implementation: Logical operations `GetRunSnapshot(runId)`, `SubscribeRunEvents(runId)`, `GetRuntimeEvents(runId, cursor?)`, `GetEvidence(evidenceRef)` are transport-neutral; no HTTP/UDS/ACP/stdio/WebSocket/gRPC choice is made.
  - Invariant Verification: No mutable rewrite of emitted events; a telemetry gap produces a diagnostic, never manufactured continuity.
  - Test Verification: Cursor resume re-delivers recognizable duplicates (OBS-F7); gap recording test passes.

## 2. RunSnapshot Projection

- [x] 2.1 Implement the truthful initial RunSnapshot with audited field classification.
  - Implementation: `DIRECT_PUBLIC_PROJECTION`: `RunState`, `CurrentSemanticPage`, `ActiveTrap`. `DERIVED_READ_MODEL`: `CurrentGoal` (span tag), `LastDecision`, `LastAction`, `RecoveryState` — each visibly flagged with its truth source. `NOT_CURRENTLY_AVAILABLE`: `CurrentObservationSequence`, `CurrentContainerSummary`, `BindingsSummary`, `StateBeliefsSummary`, full `LatestGoalEvidence`.
  - Invariant Verification: RunSnapshot is a read-only projection of Kernel-owned state; derived fields are never presented as canonical Kernel state; snapshot requests cannot mutate runtime state (OBS-F5).
  - Test Verification: Field-classification tests pass; partial GoalEvidence (`State=Completed` + `Reason`) never fabricates `SourceObservationSequence` (OBS-F4).

- [x] 2.2 Verify the minimum Trace UI buyer (Run Header, Timeline, available Snapshot fields, Evidence Inspector) functions without Container internals; do not expand the Agent public surface.
  - Implementation: No accessor for active Container, current Observation, `ObjectBindings`, or `ObjectStateBeliefs` is added; `Container` is never exposed.
  - Invariant Verification: No Agent public-surface expansion for UI convenience (OBS-F10); no ContainerSnapshot is introduced without a concrete acceptance scenario buyer.
  - Test Verification: Buyer-scenario test passes using only currently-public fields.

## 3. EvidenceRef and DriverHost Boundary

- [x] 3.1 Implement logical `EvidenceRef` resolution reusing existing Harness evidence assets.
  - Implementation: `EvidenceRef { EvidenceId, EvidenceKind, RunId, ObservationSequence?, ContentIdentity, AssetMaturity, SizeMetadata?, Locator }`; `Locator` is a logical key; reuse `TraceCaptureSession`, `FileTraceCaptureStore`, `AssetMaturity`; no second evidence store.
  - Invariant Verification: Filesystem path is never protocol identity (OBS-F6); resolution requires no new storage.
  - Test Verification: Logical-locator stability test passes (same evidence referenceable across physical location changes).

- [x] 3.2 Define and enforce the DriverHost responsibility boundary in the projection surface.
  - Implementation: DriverHost subscribes/projects Runtime facts, exposes read-only telemetry, snapshot, and EvidenceRef resolution; it cannot authorize actions, dispatch Environment operations, mutate Container or WorldState, generate GoalEvidence, or synthesize missing semantic events.
  - Invariant Verification: Dependency direction unchanged (no Runtime → DriverHost/DSH/Platform reference); telemetry never authorizes and never completes goals.
  - Test Verification: Architecture-guard tests pass (Runtime zero new references; DriverHost boundary has no action authority).

## 4. Zero-Model Operation, Persistence Boundary, and Closeout

- [x] 4.1 Prove read-only observability consumes zero LLM/VLM tokens and works with no model/provider installed.
  - Implementation: No cognitive call site exists in telemetry/snapshot/evidence resolution; unknown/gap results are explicit diagnostics (never implicit LLM escalation).
  - Invariant Verification: I-15 (Deterministic Information Acquisition Priority) holds at this boundary.
  - Test Verification: OBS-F1 test passes — telemetry query succeeds while no model/provider exists.

- [x] 4.2 Confirm the platform persistence boundary: durable historical projection never becomes mutable runtime state.
  - Implementation: Persistence of RuntimeEvents/Trace metadata/Evidence metadata/Run results goes through the existing append-only capture/store boundary only.
  - Invariant Verification: Restarting the platform never rewrites current Kernel belief; persistence is additive, not authoritative.
  - Test Verification: Persistence-isolation test passes.

- [x] 4.3 Add scenario/regression coverage and run full repository validation.
  - Implementation: Tests cover projection truthfulness, source classification, read-only/no-mutation, zero-model operation, cursor/duplicate safety, failure isolation, and architecture guards; strict OpenSpec validation passes.
  - Invariant Verification: OBS-F2/F3/F4 truthful absence; OBS-F8 projection failure leaves Runtime execution unaffected; OBS-F9 projected Sequence never treated as Observation truth.
  - Test Verification: Full regression, architecture guards, consistency checks, and `openspec validate dsh-kernel-read-only-observability --strict` pass.

## Design Docs

| Module | Design Doc |
|--------|------------|
| Projection surface (DriverHost direction, new) | [design.md](design.md), [proposal.md](proposal.md) |
| `src/UniClaw.Runtime/Observability/` (unchanged reference) | [design.md](design.md), [Runtime Architecture Contract](../../../docs/system/constitution/runtime-architecture-contract.md) |
| `src/UniClaw.Runtime.Harness/` (reused assets) | [design.md](design.md), [Trace Capture and Scenario Catalog Architecture Gate](../../../docs/decisions/trace-capture-scenario-catalog-architecture-gate.md) |
| `tests/UniClaw.Runtime.Tests/` | [design.md](design.md), [Runtime Architecture Contract](../../../docs/system/constitution/runtime-architecture-contract.md) |
