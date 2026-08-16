## Why

The UniClaw Kernel already emits structural observability (BCL `ActivitySource` "UniClaw.Runtime" with three semantic-ish span emitters) and the Harness already freezes immutable `TraceRun` projections, but there is no canonical semantic `RuntimeEventStream` and no read-only Kernel state projection. Before any DSH cognition (Shadow/Advisory/Blocking) can be designed against real facts, an external consumer (future DriverHost / DSH Control Plane) must be able to truthfully answer "what happened during this run?" and boundedly "what state does the Kernel currently expose?" — without becoming a second state owner, fabricating nonexistent facts, or changing Runtime decision behavior. This slice establishes that read-only observability boundary.

## What Changes

- Add a canonical, append-only, logically defined `RuntimeEventStream` projection with a `RuntimeEventEnvelope` (EventId / RunId / Sequence / EventKind / CorrelationId? / CausationId? / ObservationSequence? / EvidenceRefs[] / Payload). No transport or serialization is chosen.
- Establish a repository-audited **event source truth table**: every EventKind classified `A` (derivable from existing span), `B` (derivable from existing public read model), or `C` (requires new runtime semantic emission). `C` events (`DecisionProposed`, `DecisionAccepted`, `ActionAuthorized`, `RecoveryVerified`) are **explicitly out of scope** and MUST NOT be synthesized, inferred, or reconstructed from dispatch/success/trace ordering.
- Add a truthful initial `RunSnapshot` read model. Every field is classified `DIRECT_PUBLIC_PROJECTION`, `DERIVED_READ_MODEL`, or `NOT_CURRENTLY_AVAILABLE`; derived fields are visibly identified as derived; unavailable fields stay absent.
- Add a logical `EvidenceRef` contract (logical locator, not filesystem path identity) reusing existing Harness assets (`TraceCaptureSession`, `FileTraceCaptureStore`, `AssetMaturity`); no second evidence store.
- Define the future independent DriverHost boundary (subscribe/project facts, expose read-only telemetry, snapshot, resolve EvidenceRef, later ContextCompiler) and transport-neutral logical read operations (`GetRunSnapshot`, `SubscribeRunEvents`, `GetRuntimeEvents`, `GetEvidence`).
- Freeze I-15 (Deterministic Information Acquisition Priority) at this boundary: read-only observability consumes **zero LLM/VLM tokens** and works with no model/provider installed.
- Persist nothing beyond the existing Harness capture/store boundary; durable historical projection must never become mutable runtime state.

## Capabilities

### New Capabilities

- `dsh-kernel-read-only-observability`: Truthful read-only projection of Kernel runtime events and exposed state for external consumers, without second state ownership, fact fabrication, decision-behavior change, or AI abstractions.

### Modified Capabilities

None.

## Impact

- `src/UniClaw.Runtime/`: **no semantic behavior change**. Runtime observability seam (`RuntimeObservability`) stays exactly as-is; no new span emitters are added by this slice; no `IBrain`/`IDecisionProvider`/`ILLMDecisionEngine`/DSH dependency/token-budget code enters Runtime.
- `src/UniClaw.Runtime.Harness/`: existing `TraceRun`/`TraceCaptureSession`/`FileTraceCaptureStore`/`AssetMaturity` are reused as projection sources and evidence assets. Harness logic is not moved into Runtime.
- New consumer-side projection surface (DriverHost direction): RuntimeEvent projection, RunSnapshot projection, EvidenceRef resolution, read-only logical API. Not implemented in this slice beyond what OpenSpec Apply later authorizes.
- `tests/UniClaw.Runtime.Tests/`: projection truthfulness, source-classification conformance, read-only/no-mutation, zero-model operation, cursor/duplicate safety, failure isolation, and architecture-guard coverage.
- No new external package, service, transport, or model dependency.
