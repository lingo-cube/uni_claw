## ADDED Requirements

### Requirement: Read-only observability authority boundary
Read-only observability SHALL expose Kernel facts only and SHALL NOT create, transfer, or dilute authority. Projection SHALL NOT equal ownership, telemetry SHALL NOT equal truth creation, and persistence SHALL NOT equal runtime authority. DSH cognitive authority, Kernel execution/runtime-state authority, and External-World truth authority remain unchanged.

#### Scenario: Snapshot request cannot mutate runtime state
- **WHEN** a consumer requests the RunSnapshot for a run
- **THEN** the Kernel run state, Agent decision behavior, Container state, and world state SHALL remain unchanged, and the request SHALL be satisfiable with read-only access

#### Scenario: Telemetry exposes no authority verbs
- **WHEN** the observability surface is inspected
- **THEN** it SHALL expose no operation that authorizes an action, dispatches an Environment operation, mutates Container or WorldState, or generates GoalEvidence

### Requirement: RuntimeEvent logical envelope contract
The RuntimeEventStream SHALL be an append-only projection of semantic run events, each represented by a logical `RuntimeEventEnvelope` carrying `EventId`, `RunId`, `Sequence`, `EventKind`, optional `CorrelationId`, optional `CausationId`, optional `ObservationSequence`, optional `EvidenceRefs`, and a kind-specific `Payload`. Serialization and transport SHALL NOT be fixed by this capability.

#### Scenario: Projected sequence is ordering metadata only
- **WHEN** a consumer reads `Sequence` values of projected events
- **THEN** it SHALL treat them as monotonic ordering metadata within the projected run and SHALL NOT treat them as world truth or semantic identity

#### Scenario: Observation sequence anchors world evidence
- **WHEN** an event carries an `ObservationSequence`
- **THEN** it SHALL reference the Kernel-assigned `Observation.SequenceNumber` as the external-world evidence anchor, and the consumer SHALL NOT treat the projected `Sequence` as that anchor

#### Scenario: Sequence and ObservationSequence are independent domains
- **WHEN** a consumer compares `RuntimeEvent.Sequence` with an `ObservationSequence`
- **THEN** the two values SHALL be understood as belonging to independent semantic domains: the projected ordering metadata and the Kernel observation evidence anchor
- **AND** their numeric values MAY coincide by coincidence, and SHALL NOT be forced apart; no semantic meaning follows from equality or inequality

### Requirement: Audited event source classification
Every EventKind SHALL be classified against the repository audit as `A` (derivable from an existing span), `B` (derivable from the existing public read model), or `C` (requires new runtime semantic emission). The classification table SHALL be recorded in the change and SHALL be the contract for what the projection may emit.

#### Scenario: Span-derivable event is emitted
- **WHEN** `ContainerReconciled` or the span-portion of `ActionDispatched` is projected
- **THEN** the projection SHALL derive it from the existing `container.refresh` or `traversal.execution` span evidence rather than from invented state

#### Scenario: Public-read-model event is emitted
- **WHEN** a `B`-classified event such as `TrapRaised` or `RunFailed` is projected
- **THEN** the projection SHALL derive it from the existing public read model (`Agent.LastTrap`, `Agent.State`, `TraceEvent`) and SHALL NOT invent fields that the model does not expose

### Requirement: Truthful absence of C-class semantic events
`DecisionProposed`, `DecisionAccepted`, `ActionAuthorized`, and `RecoveryVerified` SHALL be classified `C` and SHALL be out of scope for this capability. The projection SHALL NOT synthesize them from Reason strings, infer them from dispatch, reconstruct them from eventual success, or guess them from trace ordering.

#### Scenario: C-class event is absent by default
- **WHEN** a run contains an `ActionDispatched` event or completes successfully
- **THEN** no `DecisionProposed`, `DecisionAccepted`, `ActionAuthorized`, or `RecoveryVerified` event SHALL be present, and the absence SHALL be explicit and truthful rather than inferred

#### Scenario: Consumer distinguishes absence from loss
- **WHEN** a consumer queries events for a run
- **THEN** it SHALL be able to distinguish a C-class event that did not occur (never emitted) from an event that occurred but was lost (telemetry gap diagnostic), and neither SHALL be manufactured as continuity

### Requirement: Span skeleton reuse and non-replacement
The RuntimeEventStream SHALL NOT replace the existing `RuntimeObservability` ActivitySource spans. Existing spans SHALL remain the structural/timing/causal skeleton; the RuntimeEvent projection SHALL be a semantic read model where evidence exists. `TraceId`, `SpanId`, and `ParentSpanId` SHALL be reused for correlation where appropriate, and no second competing trace framework SHALL be created.

#### Scenario: Span and semantic event coexist
- **WHEN** a run is both traced and projected
- **THEN** the frozen `TraceRun` spans and the projected RuntimeEvents SHALL coexist without either framework replacing the other, and Harness logic SHALL NOT be moved into Runtime

#### Scenario: Causation is only populated when known
- **WHEN** two projected events occur near each other in time
- **THEN** `CausationId` SHALL be populated only if a semantic causal relation is truthfully known, and proximity alone SHALL NOT populate it

### Requirement: Truthful RunSnapshot field classification
Every RunSnapshot field SHALL retain its repository-audited classification: `DIRECT_PUBLIC_PROJECTION` (from the Agent public read model), `DERIVED_READ_MODEL` (visibly identified as derived, never presented as Kernel-owned canonical state), or `NOT_CURRENTLY_AVAILABLE` (absent, never invented). The initial slice SHALL expose only fields that can be truthfully produced.

#### Scenario: Derived fields are visibly flagged
- **WHEN** a consumer reads `CurrentGoal`, `LastDecision`, `LastAction`, or `RecoveryState`
- **THEN** each SHALL be visibly identified as a derived read model with its truth source, and SHALL NOT be presented as canonical Kernel-owned state

#### Scenario: Unavailable fields stay absent
- **WHEN** a consumer requests `CurrentObservationSequence`, `CurrentContainerSummary`, `BindingsSummary`, `StateBeliefsSummary`, or the full `LatestGoalEvidence` record
- **THEN** the snapshot SHALL report them as not currently available and SHALL NOT fabricate them

#### Scenario: Partial goal evidence is not completed
- **WHEN** a run has completed and only `State=Completed` plus `Reason` are available
- **THEN** the snapshot SHALL NOT fabricate a full GoalEvidence record or a `SourceObservationSequence` when the underlying sequence is unavailable

### Requirement: No container expansion without a concrete buyer
The active Container and its internals (`ObjectBindings`, `ObjectStateBeliefs`, current observation) SHALL remain private to Runtime.Agent in this slice. No Agent public-surface expansion SHALL occur merely for UI convenience. If a later slice proves a concrete acceptance scenario requires them, the only permitted shape SHALL be a minimum narrow immutable read-only snapshot with snapshot semantics, no mutable Container reference, no command methods, and no back-reference allowing mutation.

#### Scenario: Slice-1 telemetry functions without container internals
- **WHEN** the minimum Trace UI renders the run header, timeline, available snapshot fields, and evidence inspector
- **THEN** it SHALL function without any of the not-currently-available container fields

#### Scenario: Future container snapshot is narrow and immutable
- **WHEN** a container projection is introduced by a later slice with a concrete buyer
- **THEN** it SHALL be immutable, read-only, snapshot-semantics, free of command methods and mutation back-references, and SHALL NOT expose the mutable `Container` itself or move authority

### Requirement: EvidenceRef logical contract
Evidence references SHALL be logical `EvidenceRef` values carrying `EvidenceId`, `EvidenceKind`, `RunId`, optional `ObservationSequence`, `ContentIdentity`/provenance, `AssetMaturity`, optional `SizeMetadata`, and a logical `Locator`. A filesystem path SHALL NOT be protocol identity. Resolution SHALL reuse existing Harness evidence assets and SHALL NOT build a second evidence store in this slice.

#### Scenario: Evidence resolves through existing assets
- **WHEN** a consumer resolves an `EvidenceRef`
- **THEN** resolution SHALL use the existing Harness evidence surface (`TraceCaptureSession`, `FileTraceCaptureStore`, scenario assets) and SHALL NOT require a new evidence store

#### Scenario: Locator is a logical key
- **WHEN** an `EvidenceRef` is serialized or compared
- **THEN** its `Locator` SHALL be a logical key independent of physical filesystem path, and the same logical evidence SHALL remain referenceable if its physical location changes

### Requirement: Zero-model read-only observability
Read-only observability SHALL consume zero LLM tokens and zero VLM tokens. Trace lookup, RunSnapshot, Evidence metadata, CLI, and structured storage SHALL be directly queryable without any model. The observability surface SHALL contain no cognitive call site and no implicit "ask the model" path.

#### Scenario: Telemetry works without a model provider
- **WHEN** no DSH, LLM, or VLM provider is installed and a consumer queries telemetry, snapshot, or evidence metadata
- **THEN** the queries SHALL succeed and return truthful results without any model invocation

#### Scenario: Unknown results are explicit, not escalated
- **WHEN** a query cannot produce a truthful answer
- **THEN** the surface SHALL return an explicit diagnostic (for example a telemetry gap or `NOT_CURRENTLY_AVAILABLE` classification) and SHALL NOT implicitly escalate to an LLM

### Requirement: Append-only cursor delivery
The RuntimeEventStream SHALL be append-only with stable `EventId` values, monotonic projected `Sequence` values, duplicate-safe consumption, reconnect from a cursor, and no mutable rewrite of emitted events. A consumer resuming from a cursor SHALL recognize re-delivered events by `EventId` and SHALL NOT double-apply them.

#### Scenario: Consumer reconnects with a cursor
- **WHEN** a consumer reconnects to a stream with a previously received cursor
- **THEN** re-delivered events SHALL be safely recognizable as duplicates by `EventId`, and the consumer SHALL apply each event at most once

#### Scenario: Projection records gaps without manufacturing continuity
- **WHEN** the projection loses observability evidence for part of a run
- **THEN** a telemetry gap/diagnostic SHALL be recorded and the projection SHALL NOT manufacture continuity by inventing or reordering events

### Requirement: Projection failure isolation
Projection, snapshot, and evidence-resolution failures SHALL be represented as diagnostics and SHALL NOT affect Runtime execution, semantic decisions, or authority.

#### Scenario: Telemetry projection fails during a run
- **WHEN** the telemetry projection fails while a run is executing
- **THEN** the Runtime execution and its results SHALL remain unaffected and the failure SHALL be recorded as a diagnostic

### Requirement: Architecture dependency guards
The capability SHALL NOT introduce any dependency from Runtime to DriverHost, DSH, or Platform, and SHALL NOT give DriverHost direct-action authority, Container mutation, telemetry authorization, or telemetry goal-completion. Runtime core dependency direction SHALL remain unchanged.

#### Scenario: Runtime dependency direction is preserved
- **WHEN** the change is applied
- **THEN** `src/UniClaw.Runtime` SHALL contain no reference to DriverHost, DSH, Platform, or any new model/provider dependency, and Runtime SHALL NOT be modified to emit C-class semantic events

#### Scenario: DriverHost cannot act on the world
- **WHEN** the DriverHost boundary is exercised
- **THEN** it SHALL be able to subscribe/project facts and expose read-only telemetry, snapshot, and EvidenceRef resolution, and SHALL NOT be able to authorize actions, dispatch Environment operations, mutate Container or WorldState, generate GoalEvidence, or synthesize missing semantic events

### Requirement: Runtime.Agent parallelism preservation
This capability SHALL be implementable in parallel with ongoing Runtime.Agent development and SHALL NOT introduce `IBrain`, `IDecisionProvider`, `ILLMDecisionEngine`, `AgentStrategy`, DSH-aware Agent code, token-budget code inside Runtime, an Advisory seam, or a Blocking seam. Any minimal immutable projection purchased by a later slice SHALL be observability surface only, not a cognitive refactor.

#### Scenario: Parallel development remains unblocked
- **WHEN** this change is implemented alongside Scroll, Popup, Recovery, Ambiguity, or other Runtime.Agent development
- **THEN** the two workstreams SHALL NOT require changes to each other's contracts, and the observability surface SHALL remain independent of Agent decision behavior
