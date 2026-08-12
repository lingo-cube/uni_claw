## 1. Runtime Activity Emission Foundation

- [ ] 1.1 Add the bounded Runtime `ActivitySource` seam, stable source/schema identity, closed layer taxonomy, stable component/event identifiers, explicit observability outcomes, and no-throw emission helpers.
  - Implementation: Add only BCL diagnostics infrastructure under `src/UniClaw.Runtime/Observability/`; hold no per-run buffers and reference no Harness types.
  - Invariant Verification: Prove Agent authority, Container mutable ownership, Traversal execution authority, Environment boundary, public semantic contracts, and dependency direction are unchanged; prove stable identifiers do not derive from CLR/private names.
  - Test Verification: Add focused tests for no-listener equivalence, stable attribution, explicit success/failure/cancellation/unknown outcomes, async parent context, and hostile-listener isolation.

- [ ] 1.2 Instrument Runtime invocation, Agent execution, Intent execution, Container refresh, Traversal execution, Environment `ObserveAsync`, Environment `ExecuteAsync`, Recovery attempt, and external capability invocation at their existing operation boundaries.
  - Implementation: Wrap existing lifetimes only; preserve original arguments, return values, exceptions, cancellation, action count, retry behavior, fresh verification, recovery behavior, GoalEvidence, and completion.
  - Invariant Verification: Confirm no signature injection of recorder/store dependencies, no new semantic branch, no new mutable state owner, no semantic parsing of observability data, and no unapproved private-method spans.
  - Test Verification: Add boundary-coverage tests for exercised spans and absence tests proving recovery/capability spans are not fabricated when those paths are not exercised.

## 2. Harness Trace Model and Projection

- [ ] 2.1 Add immutable, schema-versioned Harness `TraceRun`, `TraceSpan`, and `ObservabilityEvent` values plus the narrow structural outcome and validation vocabulary required by the specs.
  - Implementation: Keep all models in `UniClaw.Runtime.Harness`; use immutable collections and distinguish timed spans from point events and from existing Agent `TraceEvent` values.
  - Invariant Verification: Prove `TraceRun != TraceCaptureSession`, Runtime owns none of these values, observability outcome does not redefine semantic success/completion, and no Provider/framework/registry is introduced.
  - Test Verification: Add immutability, required-field, schema-version, span/event-separation, and unsupported-version tests.

- [ ] 2.2 Implement one per-run Harness `RuntimeTraceRecorder` that filters the approved source/run, isolates callbacks, owns only its local mutable buffers, and freezes once into an immutable projection.
  - Implementation: Subscribe before Runtime invocation, preserve asynchronous activity lifecycle data by identifiers, latch recorder faults as Harness diagnostics, and dispose the listener deterministically.
  - Invariant Verification: Prove recorder/listener failures cannot escape into Runtime or initiate dispatch/retry/recovery, and prove no Runtime-to-Harness reference or global recorder owner exists.
  - Test Verification: Add concurrent/out-of-order callback, irrelevant-source/run filtering, double-finalization, disposal, malformed metadata, and hostile-listener/recorder tests.

- [ ] 2.3 Implement `Activity` to `TraceRun` projection with identifier-based hierarchy, monotonic nanosecond offsets/durations, explicit outcomes, validation diagnostics, and deterministic structural normalization.
  - Implementation: Validate one Runtime-invocation root, unique identities, parent existence, acyclicity, closure, child containment, event containment, non-negative timing, and overflow-safe conversion; do not silently repair invalid records.
  - Invariant Verification: Confirm wall time, callback order, exact duration, diagnostic text, CLR names, and generated IDs cannot become semantic or replay equality inputs.
  - Test Verification: Add parent-before/after-child completion, orphan/cycle/duplicate/unclosed span, wall-clock drift, invalid timing, failed-child/handled-parent, and deterministic normalized replay tests.

## 3. Append-Only Persistence Composition

- [ ] 3.1 Compose an optional immutable `TraceRun` attachment into the existing Harness capture bundle and `ITraceCaptureStore` serialization/publication lifecycle.
  - Implementation: Persist the versioned hierarchical trace alongside existing capture artifacts through the existing append-only operation; retain the existing capture session ID publication key and separate capture/runtime/trace outcomes.
  - Invariant Verification: Prove Runtime has no persistence dependency, `TraceCaptureSession` retains capture lifecycle ownership, TraceRun retains no mutable capture lifecycle, and no second store/generic repository is added.
  - Test Verification: Add round-trip schema/hierarchy/outcome/timing/event tests, append-only overwrite rejection, atomic-failure preservation, and separation of Runtime, trace, and capture failures.

- [ ] 3.2 Preserve backward readability for captures without hierarchical trace data and fail closed for unsupported TraceRun schemas.
  - Implementation: Treat missing TraceRun as honest absence; do not synthesize hierarchy from environment records, semantic `TraceEvent` values, or diagnostic strings.
  - Invariant Verification: Confirm existing capture assets and ScenarioCatalog behavior remain unchanged unless a caller explicitly requests trace conformance.
  - Test Verification: Load pre-foundation capture fixtures, unsupported-version fixtures, and persistence-failure fixtures and verify no mutation or silent upgrade.

## 4. Scenario Observability Conformance

- [ ] 4.1 Add Harness assertion utilities for stable span existence/closure, layer/component closure, required stable events/fields, ancestry, and failure/cancellation boundaries.
  - Implementation: Expose only stable public observability contracts; explicitly omit assertion APIs for exact durations, callback/private-method order, CLR names, and diagnostic strings.
  - Invariant Verification: Prove conformance is post-run diagnostic validation only and cannot authorize action, select retry, interpret Observation, decide Recovery, or supply Goal completion.
  - Test Verification: Add positive and negative assertion tests for missing/unclosed spans, invalid attribution, missing events, ancestry breaks, and failure-boundary mismatch; add compile/API-shape guards excluding forbidden assertions.

- [ ] 4.2 Add an end-to-end Harness Scenario that proves required hierarchical boundaries, event/outcome evidence, monotonic closure, listener-failure isolation, and deterministic structural replay.
  - Implementation: Exercise real production call paths and the Harness recorder/projection/persistence composition without prebuilding a passing TraceRun fixture.
  - Invariant Verification: Confirm the Scenario does not parse diagnostic text, assert exact timing/private order, or use observability evidence to change Runtime semantic behavior.
  - Test Verification: Run the traced success/failure cases twice and prove normalized structural equivalence, correct failure boundary, no duplicated dispatch, and unchanged Runtime result/GoalEvidence.

## 5. Architecture and Regression Validation

- [ ] 5.1 Extend architecture guards for the observability boundary and verify all frozen exclusions.
  - Implementation: Add mechanical checks that Runtime does not reference Harness/trace persistence, stable attribution is explicit, and no forbidden Provider framework, capability registry, Brain, Planner, or semantic-contract dependency is introduced.
  - Invariant Verification: Re-audit all Runtime Architecture Contract invariants, especially I-1 dependency direction, I-2 mutable ownership, I-3 authority, I-10 Goal completion, and I-12 YAGNI.
  - Test Verification: Run Architecture Guard tests and include deliberate-failure fixtures for the new dependency/contract guards where the existing guard style supports them.

- [ ] 5.2 Run repository-wide validation and independently audit the completed change against this OpenSpec.
  - Implementation: Record implementation delta and mark tasks complete only from repository evidence; do not broaden the trace taxonomy or instrumentation boundaries during closeout.
  - Invariant Verification: Confirm Agent authority, Container state ownership, Traversal authority, Environment boundary, semantic contracts, and existing `TraceEvent`/`TraceCaptureSession` responsibilities remain unchanged.
  - Test Verification: Run `dotnet build src/UniClaw.Runtime.sln`, `dotnet test src/UniClaw.Runtime.sln`, architecture guards, `scripts/check-consistency.sh`, and strict OpenSpec validation; require all regressions and observability scenarios to pass.

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Runtime/Observability/` and instrumented Runtime boundaries | [design.md](design.md), [Runtime Architecture Contract](../../../docs/system/constitution/runtime-architecture-contract.md), [Observability, Results and Completion Evidence](../../../docs/system/patterns/observability-and-results.md) |
| `src/UniClaw.Runtime.Harness/` | [design.md](design.md), [Trace Capture and Scenario Catalog Architecture Gate](../../../docs/decisions/trace-capture-scenario-catalog-architecture-gate.md) |
| `tests/UniClaw.Runtime.Tests/` | [design.md](design.md), [Runtime Architecture Contract](../../../docs/system/constitution/runtime-architecture-contract.md) |
