## Context

Repository observability truth (fresh audit, `uni-agent` branch):

- Runtime emits structural observability through one BCL `ActivitySource` `"UniClaw.Runtime"` v1.0.0 (`src/UniClaw.Runtime/Observability/RuntimeObservability.cs`). Only **three** semantic-ish span emitters exist today:
  1. `RunSemanticGoal` (layer `AGENT`, component `agent.execution`; tags `goal` / `runId`)
  2. `RefreshSnapshot` (layer `CONTAINER`, component `container.refresh`)
  3. `LoweredAction` (layer `TRAVERSAL`, component `traversal.execution`)
  - Declared-but-never-emitted component constants (`environment.observe`, `environment.execute`, `recovery.attempt`, `capability.invocation`) are **aspirational, not emission points**.
- Harness already subscribes to `ActivitySource` and freezes immutable `TraceRun { TraceRunId, TraceId, RunId, Spans[], Diagnostics[] }` via `RuntimeTraceRecorder`; Harness also owns `TraceCaptureSession`, `FileTraceCaptureStore` (append-only), `AssetMaturity`, and the Scenario Catalog.
- Runtime.Agent public surface exposes only: `State`, `Belief`, `Trace`, `Reason`, `RecoveryAnchor`, `LastTrap`, `BranchProgress`, `NavigationEvidence`. It does **not** publicly expose: active Container, current Observation, current Goal, `ObjectBindings`, `ObjectStateBeliefs`.
- `Observation.SequenceNumber` is a Kernel-assigned **deterministic monotonic** counter — not a timestamp (裁决 6). `TraceEvent` is an append-only flat Agent-owned semantic trace with no sequence number and no `ObservationSequence` field.
- `Capabilities/Brain/` is empty; Runtime contains no `IBrain`, `IDecisionProvider`, `ILLMDecisionEngine`, DSH dependency, or token-budget logic (audit: zero matches).

There is currently **no canonical semantic `RuntimeEventStream`**. Four semantic event families cannot be truthfully reconstructed from existing evidence: `DecisionProposed`, `DecisionAccepted`, `ActionAuthorized`, `RecoveryVerified`. This slice must not fabricate them.

## Goals / Non-Goals

**Goals:**

- Provide a truthful, append-only, cursor-safe `RuntimeEventStream` projection with a logical `RuntimeEventEnvelope`, where every emitted EventKind is classified against the repository audit (A / B / C).
- Provide a truthful initial `RunSnapshot` read model whose fields are classified `DIRECT_PUBLIC_PROJECTION`, `DERIVED_READ_MODEL`, or `NOT_CURRENTLY_AVAILABLE`, with derived fields visibly marked.
- Provide a logical `EvidenceRef` contract reusing existing Harness assets (no second evidence store, no filesystem path as protocol identity).
- Define the future DriverHost responsibility boundary and transport-neutral logical read operations.
- Freeze I-15 at this boundary: read-only observability consumes **zero LLM/VLM tokens** and works with no model/provider installed.
- Keep Runtime execution, decision behavior, authority, and dependency direction byte-for-byte unchanged (OBS-F5, OBS-F8).

**Non-Goals:**

- No `C`-class semantic event emission. `DecisionProposed`, `DecisionAccepted`, `ActionAuthorized`, `RecoveryVerified` are explicitly out of scope and MUST NOT be synthesized from Reason strings, inferred from dispatch, reconstructed from eventual success, or guessed from Trace ordering.
- No cognition: no Shadow LLM calls, no Advisory, no Blocking seam, no control-plane physical actions.
- No second competing trace framework; `RuntimeObservability` spans remain the structural/timing/causal skeleton. Harness logic is not moved into Runtime.
- No new mutable state owner: projection != ownership; telemetry != truth creation; persistence != runtime authority.
- No transport/serialization selection (HTTP/UDS/ACP/stdio/WebSocket/gRPC deferred).
- No Agent public-surface expansion for UI convenience; active Container internals stay private unless a concrete acceptance scenario buys the minimum projection (see Decision 6).
- No `IBrain` / `IDecisionProvider` / `ILLMDecisionEngine` / `AgentStrategy` / DSH-aware Agent code / token-budget code inside Runtime.

## Decisions

### 1. RuntimeEventStream is a projection, not a new Runtime subsystem

The canonical semantic `RuntimeEventStream` is produced by the consumer-side projection (DriverHost direction / integration layer) from: existing spans (structural skeleton + correlation), the Agent public read model (`State`, `Belief`, `Trace`, `LastTrap`, `RecoveryAnchor`, `NavigationEvidence`, `BranchProgress`), the Harness frozen `TraceRun`, and — where present — journal/observation evidence reachable through the public read model. Runtime gains **no** new buffer, emitter, or dependency.

Rationale over "Runtime emits semantic events": emitting semantic events inside Runtime would couple Runtime to a protocol it must not know about and would create a second truth stream. The projection can be fail-open and purely additive. Alternative rejected: a full in-Runtime event pipeline — violates the zero-dependency-direction-change guard and this slice's "no Runtime.Agent modification" constraint.

### 2. Event source truth table (A / B / C) — repository-audited

Every EventKind MUST carry one of three classifications; the OpenSpec records them as the contract, not as an aspiration:

| # | EventKind | Classification | Truthful source today |
|---|---|---|---|
| 1 | `ObservationProduced` | **B** | New `Observation` (SequenceNumber advances) reachable via `Container.CurrentObservation` evidence in journal `PostActionObservation` / `NavigationEvidence`; no "who requested" attribution; `environment.observe` span is declared but not emitted |
| 2 | `ContainerReconciled` | **A** | `RefreshSnapshot` span (`container.refresh`) exists; content fields projected from public read model only |
| 3 | `BindingUpdated` | **B** | `ObjectBindings` delta across refreshes, correlated via `container.refresh` span |
| 4 | `StateBeliefUpdated` | **B** | `ObjectStateBeliefs` delta across refreshes, same correlation |
| 5 | `DecisionProposed` | **C — OUT OF SCOPE** | No pre-dispatch decision record exists; `TraceEvent.Action` is a dispatched-step record, not a proposal |
| 6 | `DecisionAccepted` | **C — OUT OF SCOPE** | No decision stream exists to accept into |
| 7 | `ActionAuthorized` | **C — OUT OF SCOPE** | `AuthorizeAction` is a static validator; no authorization event is recorded |
| 8 | `ActionDispatched` | **A+B** | `LoweredAction` span (`traversal.execution`) + `TraceEvent(ActionId/Action)` + journal `DispatchedAction` |
| 9 | `PostActionObserved` | **B** | Journal `PostActionObservation` non-empty + observation sequence advance |
| 10 | `VerificationCompleted` | **B (step-level)** | Journal `Result` — step-level result, NOT verification-specific; granularity mixed, labeled as such |
| 11 | `NavigationDecision` | **B (evidence)** | `NavigationEvidence` (accepted transitions) + Container `TraceEvent`; intent is not recorded — only accepted evidence |
| 12 | `ViewportExplorationDecision` | **B** | `TraceEvent.Reason` "viewport exploration …" |
| 13 | `TrapRaised` | **B** | `Agent.LastTrap` + `TraceEvent(TrapKind/TrapScope)` |
| 14 | `RecoveryStarted` | **B** | `TraceEvent.RecoveryId` (+ `Reason`) |
| 15 | `RecoveryVerified` | **C — OUT OF SCOPE** | No recovery-verification result is recorded |
| 16 | `GoalEvidenceProduced` | **B (partial)** | `State=Completed` + `Reason` only; full `GoalEvidence(Satisfied, SourceObservationSequence)` is not on the Agent public surface |
| 17 | `RunCompleted` | **B** | `State=Completed` + `Reason` |
| 18 | `RunFailed` | **B** | `State=Failed` + `Reason` |

Totals: **A=1, A+B=1, B=12, C=4.** The four `C` families are the decision/authorization spine that later Advisory/Blocking slices need; buying their emitters is explicitly deferred to a follow-up change with a concrete buyer (see Follow-Up Boundary in design.md). Truthful absence is a first-class property: a consumer MUST be able to distinguish "event did not happen / not yet emitted" from "event happened but was not recorded".

### 3. RuntimeEventEnvelope logical contract

```
RuntimeEventEnvelope {
  EventId                // stable, unique within projection
  RunId                  // Kernel run identifier
  Sequence               // monotonic within projected Run — ordering metadata ONLY
  EventKind              // from the audited 18-family vocabulary
  CorrelationId?         // protocol/run operation correlation
  CausationId?           // semantic causal relation ONLY where truthfully known
  ObservationSequence?   // Kernel-assigned Observation.SequenceNumber anchor when attributable
  EvidenceRefs[]         // logical references, not embedded content
  Payload                // kind-specific minimal facts from the classified source
}
```

- `Sequence` is projection-assigned, monotonic per projected Run, and explicitly **not** world truth and **not** semantic identity (OBS-F9). `Observation.SequenceNumber` remains the external-world evidence anchor. The two are **independent semantic domains**: their numeric values MAY coincide by coincidence, and no semantic meaning follows from equality or inequality (OBS-F9A/B/C/D).
- Timestamp (if surfaced) is operational only.
- Serialization/transport are explicitly deferred; this is a logical contract.
- Causality reuse: `TraceId`/`SpanId`/`ParentSpanId` from existing spans provide the structural/causal skeleton where truthful; `CorrelationId` ties protocol operations; `CausationId` is populated **only** when a semantic causal relation is truthfully known — never merely because two events occurred nearby.

### 4. RunSnapshot truthful field classification

| Field | Classification | Truthful source |
|---|---|---|
| `RunState` | DIRECT_PUBLIC_PROJECTION | `Agent.State` |
| `CurrentSemanticPage` | DIRECT_PUBLIC_PROJECTION | `Agent.Belief.SemanticPage` |
| `ActiveTrap` | DIRECT_PUBLIC_PROJECTION | `Agent.LastTrap` |
| `CurrentGoal` | DERIVED_READ_MODEL | `RunSemanticGoal` span tag `goal` / caller context — **not** an Agent property |
| `LastDecision` | DERIVED_READ_MODEL | latest `TraceEvent(Reason/Action)` |
| `LastAction` | DERIVED_READ_MODEL | `TraceEvent.Action` + `ActionId` |
| `RecoveryState` | DERIVED_READ_MODEL | `TraceEvent.RecoveryId` + `Reason` |
| `LatestGoalEvidence` | NOT_CURRENTLY_AVAILABLE (partial) | only `State=Completed` + `Reason`; full record + `SourceObservationSequence` unavailable — MUST NOT be fabricated (OBS-F4) |
| `CurrentObservationSequence` | NOT_CURRENTLY_AVAILABLE | active Container private; Agent does not expose it |
| `CurrentContainerSummary` | NOT_CURRENTLY_AVAILABLE | `Trace` has only `ContainerId` string |
| `BindingsSummary` | NOT_CURRENTLY_AVAILABLE | `Container.ObjectBindings` unreachable |
| `StateBeliefsSummary` | NOT_CURRENTLY_AVAILABLE | `Container.ObjectStateBeliefs` unreachable |

Rules: RunSnapshot is a **read-only projection of Kernel-owned state**; the consumer (DSH direction) never becomes a second mutable owner; no mutable references are exposed; every `DERIVED_READ_MODEL` field is visibly flagged as derived (e.g. `source: DERIVED_READ_MODEL(span:agent.execution)`), never presented as canonical Kernel-owned state.

### 5. Event stream delivery: append-only + cursor

Logical operations (transport-neutral, no protocol choice):

```
GetRunSnapshot(runId)                  // current truthful snapshot
SubscribeRunEvents(runId)              // push of projected events
GetRuntimeEvents(runId, cursor?)       // pull from a cursor
GetEvidence(evidenceRef)               // resolve logical evidence reference
```

Delivery contract: stable `EventId`; monotonic projected `Sequence`; duplicate-safe consumer (cursor resume re-delivers are recognizable by `EventId`, never double-apply); reconnect from cursor; no mutable event rewrite. Projection may be fail-open operationally, but it MUST NOT fabricate missing events: if observability loses an event, a **telemetry gap / diagnostic** is recorded and continuity is not manufactured.

### 6. Active Container / Observation decision — DEFER (no Slice-1 buyer)

Slice-1 acceptance (Run Header, Timeline, available Snapshot fields, Evidence Inspector) can function without `CurrentObservationSequence`, `CurrentContainerSummary`, `BindingsSummary`, `StateBeliefsSummary`. The read-only telemetry surface plus `DERIVED_READ_MODEL` fields satisfy the minimum Trace UI. Therefore the initial RunSnapshot exposes only truthfully available fields and these four remain `NOT_CURRENTLY_AVAILABLE` — **no Agent public-surface expansion for UI convenience** (OBS-F10).

Future shape (only if a concrete acceptance scenario in a later slice buys it): a minimum narrow immutable `ContainerSnapshot` — immutable, read-only, snapshot semantics, no mutable `Container` reference, no command methods, no back-reference allowing mutation, no authority movement. `Container` itself is never exposed. This is observability surface only, not a cognitive refactor.

### 7. EvidenceRef contract — logical key, reuse existing assets

```
EvidenceRef {
  EvidenceId
  EvidenceKind          // Screenshot | PerceptionOutput | BindingEvidence | ActionJournal | ReplayAsset | TraceFragment
  RunId
  ObservationSequence?
  ContentIdentity       // provenance / content identity
  AssetMaturity         // reuse existing Harness AssetMaturity vocabulary
  SizeMetadata?
  Locator               // LOGICAL key — never a filesystem path as protocol identity
}
```

Resolution reuses the existing Harness evidence surface (`TraceCaptureSession`, `FileTraceCaptureStore`, Scenario assets). No second evidence store is built in this slice. P0 retrieval ordering: structured semantics → targeted text → cropped image → full screen → history (I-15).

### 8. DriverHost boundary

DriverHost (future independent sibling project, protocol/integration boundary) responsibilities: subscribe/project Runtime facts; expose read-only telemetry; expose snapshot; resolve `EvidenceRef`; later host ContextCompiler. DriverHost MUST NOT: authorize actions, dispatch Environment operations, mutate Container, mutate WorldState, generate GoalEvidence, or synthesize missing semantic events. PhysicalHost remains the reality composition. This slice defines the boundary logically; implementation waits for OpenSpec Apply authorization.

### 9. P0 / zero-model requirement

Freeze I-15 at this integration boundary: trace lookup, RunSnapshot, Evidence metadata, CLI, structured storage are directly queryable **without LLM**. Read-only observability consumes zero LLM tokens and zero VLM tokens. Required falsifier: telemetry query succeeds while no model/provider exists (OBS-F1). The projection, snapshot, and evidence resolution contain no cognitive call site and no "ask the model" path — `UNKNOWN`/gap results are explicit diagnostics, never implicit LLM escalation.

### 10. Platform persistence boundary

Ownership: Kernel = active runtime truth; DriverHost = live projection; Platform Core = durable historical projection. Platform MAY persist RuntimeEvents, Trace metadata, Evidence metadata, and Run results through the existing append-only capture/store boundary — but durable storage MUST NOT become mutable runtime state; restarting Platform MUST NOT rewrite current Kernel belief. Persistence is additive, never authoritative over live state.

## Risks / Trade-offs

- **C-family semantic spine stays absent** (Decision/Authorization/RecoveryVerification events) → later Advisory/Blocking slices must buy minimal Kernel-side emission in a separate change with a concrete buyer; this slice never pretends the spine exists. Mitigation: explicit `C — OUT OF SCOPE` classification and truthful-absence semantics in the spec.
- **Projection ordering under concurrency** (interleaved binding/belief updates within one observation) cannot be proven ordered → `Sequence` is explicitly ordering metadata, not truth; a telemetry gap/diagnostic is recorded rather than inventing order. Mitigation: causality is derived from span `ParentSpanId`/ids, never from callback order.
- **RunSnapshot could be mistaken for canonical state** → every field carries its classification; `DERIVED_READ_MODEL` is visibly flagged; snapshot is documented as a projection of Kernel-owned state.
- **Projection logic could drift from repository truth** → the A/B/C table is audited and frozen into the spec; architecture guards enforce no Runtime dependency change and no new emitters.
- **Observability failure could be assumed to affect execution** → OBS-F8: projection failure MUST NOT affect Runtime execution; recorded as diagnostics only.

## Migration Plan

1. Implement consumer-side projection surface (DriverHost direction) reading existing spans + Agent public read model + Harness `TraceRun`; no Runtime changes.
2. Add RunSnapshot projection with classified fields; derived fields flagged.
3. Add EvidenceRef resolution reusing existing Harness assets.
4. Add zero-model verification (run with no provider present) and architecture guards.
5. Full regression + strict OpenSpec validation. Rollback: remove the projection surface only; Runtime and Harness contracts remain untouched.

## Maturity Target

After implementation, this change targets exactly **`READ_ONLY_KERNEL_OBSERVABILITY_INTEGRATED`** and nothing more. Explicitly NOT targeted: `DSH_COGNITION_INTEGRATED`, `SHADOW_MODE_COMPLETE`, `SEMANTIC_DECISION_STREAM_COMPLETE`. The slice succeeds even though C-class semantic events remain absent, provided their absence is explicit and truthful.

## Follow-Up Boundary

If this change graduates, the next intended change is **`dsh-shadow-cognition`**: a future slice that consumes `RuntimeEvent`, `RunSnapshot`, and `EvidenceRef` and produces `DecisionProposal` records that the Kernel ignores. Only after Shadow evidence exists should the program consider Advisory, which is the point where a minimal Kernel decision/authorization emission seam may be bought. This slice MUST NOT buy C-class Runtime emitters merely because later Advisory will need them.

## Open Questions

1. Where does the projection cursor / last-Sequence state live (DriverHost live projection vs Platform durable projection) without becoming a second owner?
2. Whether `SubscribeRunEvents` push is needed in Slice 1 or cursor-pull (`GetRuntimeEvents`) suffices for the minimum Trace UI buyer.
3. Whether `LatestGoalEvidence` should surface the partial `State+Reason` form now, or remain fully absent until the full record becomes available (current plan: partial, flagged).
4. Granularity of `VerificationCompleted` (step-level Journal Result) — whether to rename/split in a future slice once verification-specific evidence exists.
