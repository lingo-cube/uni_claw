## 1. Runtime Activity Emission

- [x] 1.1 Add the bounded BCL `ActivitySource` seam with stable source/schema identity, closed layer/component vocabulary, explicit observability outcomes, and no-throw helpers.
  - Implementation: `src/UniClaw.Runtime/Observability/RuntimeObservability.cs` contains no per-run buffer and no Harness dependency.
  - Invariant Verification: Trace emission is observational only and cannot change semantic decisions, results, authority, ownership, or dependency direction.
  - Test Verification: No-listener equivalence, attribution, outcome, hierarchy, immutable schema, finalization, and fail-open tests pass.

- [x] 1.2 Instrument the five accepted active boundaries and preserve the four deferred receipts.
  - Implementation: Agent execution, Container refresh, Traversal execution, Environment `ObserveAsync`, and Environment `ExecuteAsync` have production call sites. Runtime invocation, Intent execution, Recovery attempt, and external capability invocation remain deferred for their recorded reasons.
  - Invariant Verification: No recorder/store injection, semantic branch, new mutable owner, or fabricated inactive span was introduced.
  - Test Verification: Production call-site audit and full regression pass.

## 2. Harness Trace Lifecycle

- [x] 2.1 Add immutable versioned `TraceRun`, `TraceSpan`, and `ObservabilityEvent` records plus per-run `RuntimeTraceRecorder` Activity projection.
  - Implementation: Harness owns recorder buffers, finalization, and immutable TraceRun lifecycle; Runtime owns none of them.
  - Invariant Verification: `TraceRun != TraceCaptureSession`, observability outcomes do not redefine semantic success, and Semantic Trace remains unchanged.
  - Test Verification: Recorder capture, parent-child context, stable attribution, immutable/versioned records, idempotent finalization, and disposal tests pass.

## 3. Persistence and Golden Integration

- [x] 3.1 Attach optional immutable `TraceRun` data to `TraceCaptureBundle` and persist it through the existing append-only `FileTraceCaptureStore` boundary.
  - Implementation: `observability-trace.json` is written only when a TraceRun is present; captures without it remain valid.
  - Invariant Verification: Persistence remains Harness-owned and cannot affect Runtime results or cause dispatch/retry.
  - Test Verification: Golden run recording composes RuntimeTraceRecorder, TraceCaptureBundle, and append-only persistence successfully.

## 4. Scenario Conformance and Closeout

- [x] 4.1 Add stable structural Scenario assertions and complete repository validation.
  - Implementation: Assertions cover span presence, approved layers, non-empty components, parent closure, unique span IDs, required events, and explicit outcomes.
  - Invariant Verification: No exact timing, private method order, CLR name, or diagnostic string is an acceptance contract; Trace never becomes semantic authority.
  - Test Verification: Full regression 819/819 PASS; Architecture Guards 16/16 PASS; consistency C1-C10 PASS; strict change validation PASS.

## Design Docs

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime/Observability/` and five active Runtime boundaries | [design.md](design.md), [Runtime Architecture Contract](../../../docs/system/constitution/runtime-architecture-contract.md), [Observability, Results and Completion Evidence](../../../docs/system/patterns/observability-and-results.md) |
| `src/UniClaw.Runtime.Harness/` | [design.md](design.md), [Trace Capture and Scenario Catalog Architecture Gate](../../../docs/decisions/trace-capture-scenario-catalog-architecture-gate.md) |
| `tests/UniClaw.Runtime.Tests/` | [design.md](design.md), [Runtime Architecture Contract](../../../docs/system/constitution/runtime-architecture-contract.md) |
