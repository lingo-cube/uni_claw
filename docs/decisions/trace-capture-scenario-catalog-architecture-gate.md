# Trace Capture and Scenario Catalog Architecture Gate

> Date: 2026-08-12
> Role: Project Leader
> Lane: `SEMANTIC_DISCOVERY` / foundation architecture
> Result: `ARCHITECTURE_DESIGN_APPROVED`
> Implementation authority: **NOT GRANTED**
> Required next lifecycle: OpenSpec propose and Human approval before apply

## 1. Gate decision

The next foundation is approved as a **Harness capability**, outside the
graduated semantic Runtime:

```text
TRACE_CAPTURE_AND_SCENARIO_CATALOG_ARCHITECTURE_GATE
  = ARCHITECTURE_DESIGN_APPROVED

TRACECAPTURE_OWNER
  = HARNESS_CAPTURE_SESSION

TRACE_PERSISTENCE_BOUNDARY
  = NARROW_APPEND_ONLY_STORE

SCENARIOCATALOG_OWNER
  = HARNESS_REPOSITORY_INDEX

CORE_RUNTIME_DELTA
  = NONE

OPENSPEC
  = REQUIRED_BEFORE_IMPLEMENTATION
```

This approval freezes responsibility and dependency boundaries only. It does
not authorize production code, test migration, asset movement, live capture,
or modification of an existing golden.

## 2. Reconciled graduated baseline

The design starts from current repository truth, including work delivered after
the original Replay Harness graduation receipt:

- the semantic Runtime spine is graduated;
- Simulation and Observation Replay are graduated;
- `PhysicalEnvironment : IEnvironment` is the production observation/action
  composition boundary;
- physical perception and same-frame switch-state evidence are callable;
- `golden-run-v1` contains real emulator screenshots, perception outputs, and
  already-ON plus OFF-to-ON replay evidence;
- current Harness asset contracts, validation, and replay adapters live inside
  the test assembly;
- `PhysicalEnvironment` currently retains only in-memory Observation and action
  histories;
- golden replay currently constructs minimized `Observation` values in C# and
  uses separate ad-hoc trace JSON rather than one canonical Harness manifest;
- the existing `TraceAsset` persistent schema has no production capture path.

The last three facts are the concrete purchase pressure. They justify capture,
persistence, and catalog foundations. They do not justify a Provider framework,
Planner, Brain, or Runtime semantic change.

## 3. Ownership and authority

| Concern | Owner / authority | Explicitly does not own |
|---|---|---|
| Run semantic lifecycle and in-memory semantic Trace | Agent | File persistence, Scenario registration |
| Page-local mutable belief | Container | Capture state, asset catalog |
| Ground/lower/dispatch/verify protocol | Traversal | Trace storage policy, regression selection |
| Physical observation and dispatch | Environment / adapter | Scenario meaning, expected behavior |
| One capture attempt's transient recording state | `TraceCaptureSession` in Harness | Runtime belief, action authorization, completion |
| Durable capture bytes and atomic publication | `ITraceCaptureStore` implementation | Scenario meaning, replay expectations |
| Scenario ID uniqueness and asset-reference integrity | immutable `ScenarioCatalog` in Harness | Runtime selection, semantic planning, truth |
| Normative Scenario behavior | approved OpenSpec Scenario/spec | Captured-run outcome |
| Regression membership | explicit test/suite configuration | Agent or automatic catalog inference |

`TraceCaptureSession` is a mechanism-local mutable owner. It may own buffers,
capture order, artifact hashes, and its own lifecycle status. This is not a new
Runtime semantic-state owner and does not overlap Container.

## 4. Runtime versus Harness split

```text
Graduated Runtime
  Agent -> Container -> Traversal -> IEnvironment
       | read-only result/Trace snapshot
       v
Harness capture composition
  CapturingEnvironment -> PhysicalEnvironment
       |                         |
       | Observation/action     | optional rich physical-artifact tap
       +------------+------------+
                    v
            TraceCaptureSession
                    |
                    v
            ITraceCaptureStore

Harness validation path
  ScenarioCatalog -> ScenarioAsset -> ReplayAsset
       -> ReplayEnvironment -> unchanged Runtime -> behavior assertions
```

### Runtime remains responsible for

- semantic decisions and semantic Trace production;
- current World/Container belief;
- action authorization;
- grounding, lowering, dispatch protocol, and fresh verification;
- GoalEvidence and completion.

### Harness becomes responsible for

- observing public Runtime/environment outputs without changing them;
- correlating Frames, Observations, dispatches, results, and Runtime Trace
  snapshots into an immutable capture bundle;
- persistence, hashes, provenance, schema/version validation, and quarantine;
- immutable Scenario lookup and reference validation;
- replay construction and behavior-level regression execution.

### Forbidden coupling

- Runtime must not reference Harness.
- Agent, Container, and Traversal must not receive a capture/catalog/store
  dependency.
- `IEnvironment` and all graduated semantic contracts remain unchanged.
- ScenarioCatalog must not select a Scenario from business intent.
- Capture must not interpret evidence, decide whether a goal succeeded, or
  manufacture expected behavior.
- Harness must not parse `TraceEvent.Reason` or diagnostic strings into
  machine decisions.

## 5. TraceCapture lifecycle

### 5.1 Lifecycle states

```text
CREATED
  -> CAPTURING
  -> FINALIZING
  -> PERSISTED

CAPTURING / FINALIZING
  -> CAPTURE_FAILED
  -> QUARANTINED (optional diagnostic preservation; never catalog-visible)
```

This is a Harness object lifecycle, not a Runtime FSM.

### 5.2 Begin

The caller creates a capture request with:

- stable `CaptureSessionId` and `TraceId`;
- source/run label and optional approved `ScenarioId`;
- device/runtime/commit metadata when actually known;
- requested provenance (`LIVE_CAPTURE` while running);
- artifact policy: screenshots/perception/Observation/dispatch/runtime Trace;
- explicit local capture root.

Unknown metadata remains absent. IDs are caller-provided or generated once;
filenames never become semantic identity.

### 5.3 Record

`CapturingEnvironment : IEnvironment` wraps the concrete environment and
records, in actual external-call order:

- returned Observations;
- authorized DeviceActions received at the Environment boundary;
- returned ActionResults;
- exceptions/cancellation as capture history without rewriting Runtime outcome.

For physical runs, an optional adapter-specific artifact tap on
`PhysicalEnvironment` reports the same observation generation's:

- raw screenshot bytes and dimensions;
- perception output;
- final `Observation` and sequence number;
- content format and derived-from relation.

The tap is a narrow callback/delegate, not a new Runtime port or Provider
framework. It must not throw into `ObserveAsync` or `ExecuteAsync`. A capture
fault is latched by the Harness while the Runtime/environment call continues
with its original result.

### 5.4 Runtime Trace snapshot

After the run, the Harness reads the existing public Agent Trace and final
result/GoalEvidence. It stores an honest structural snapshot of available
fields. It does not infer `CAPABILITY_SELECTED`, `BELIEF_UPDATED`, or other
typed events by parsing `Reason`.

Environment-call order and Runtime Trace order remain explicitly distinct
streams unless a future semantic contract supplies a shared event sequence.
Correlation may use existing run ID, observation sequence, action ID, and
capture order. Missing correlation remains missing rather than fabricated.

### 5.5 Finalize

Finalization:

1. freezes all transient buffers into immutable asset records;
2. distinguishes **Runtime failure** from **capture failure**;
3. computes artifact hashes;
4. validates schema, IDs, ordering, references, and provenance;
5. requests one atomic persistence operation;
6. returns a separate `TraceCaptureResult`.

A failed Runtime run may be a complete and valuable capture. A capture with
missing/corrupt artifacts is `CAPTURE_FAILED` even if Runtime succeeded.

### 5.6 Failure isolation

Capture is observational. Therefore:

- capture failure never authorizes, suppresses, repeats, or changes an action;
- persistence failure never changes Runtime result or GoalEvidence;
- capture failure never causes Traversal retry;
- incomplete capture is not published to ScenarioCatalog;
- a capture-required command may report overall tooling failure after the
  Runtime run, but may not retroactively rewrite the Runtime result.

## 6. Trace persistence boundary

One specific external boundary is purchased:

```csharp
public interface ITraceCaptureStore
{
    ValueTask<TraceCapturePersistenceResult> SaveAsync(
        TraceCaptureBundle bundle,
        CancellationToken cancellationToken = default);
}
```

This is not `IProvider`, a registry, plugin system, or generic repository. It
exists because filesystem persistence is nondeterministic and needs an
in-memory falsifier.

### Minimum filesystem implementation

`FileTraceCaptureStore` must be:

- append-only by `CaptureSessionId`; existing IDs fail closed;
- atomic: write and validate in staging, then publish the directory;
- deterministic in JSON schema and artifact paths;
- content-hashed for binary/raw artifacts;
- local-only by default;
- non-version-controlled by default for unreviewed raw captures;
- cancellation-safe and cleanup-aware;
- unable to mutate a previously published capture.

Suggested physical layout (not persistent identity):

```text
artifacts/captures/<capture-session-id>/
  capture-manifest.json
  device-profile.json
  frames/<frame-id>/screenshot.png
  frames/<frame-id>/perception.json
  frames/<frame-id>/observation.json
  trace/runtime-trace.json
  trace/environment-timeline.json
  checksums.sha256
```

Raw capture storage and version-controlled regression assets are separate
lifecycles. Only reviewed/minimized assets are promoted into the repository.

## 7. ScenarioCatalog ownership

`ScenarioCatalog` is an immutable Harness value loaded from version-controlled
catalog/manifests. It owns only:

- unique Scenario ID lookup;
- schema-version compatibility;
- manifest/replay/frame/artifact reference integrity;
- hash/provenance consistency required to load a Scenario;
- deterministic enumeration for tooling.

It does not own:

- Scenario semantics or expected results;
- Runtime run state;
- mutable registration;
- intent routing or automatic Scenario selection;
- asset capture/promotion;
- regression pass/fail authority beyond returning validation errors.

Minimum public behavior:

```text
ScenarioCatalog.Load(catalog manifest) -> immutable catalog or validation errors
ScenarioCatalog.GetRequired(explicit scenarioId) -> ScenarioAsset
ScenarioCatalog.ResolveReplay(explicit scenarioId) -> validated ReplayAsset
```

No service locator, global singleton, watcher, database, auto-discovery plugin,
or runtime registry is purchased. Duplicate IDs, missing references, path
escape, unsupported schema, hash mismatch, and provenance inconsistency fail
closed before replay.

## 8. Scenario to regression flow

The canonical admission flow is:

```text
Approved Scenario
  -> Capture requirement / plan
  -> candidate TraceCapture bundle
  -> review, minimization, provenance and sensitivity audit
  -> immutable admitted Asset manifest
  -> ReplayAsset mapping
  -> ScenarioCatalog registration
  -> explicit Scenario replay through unchanged Runtime
  -> behavior-level regression
```

Rules:

1. Scenario comes from approved behavior/specification, not from the outcome of
   one captured run.
2. A capture may satisfy a Scenario's asset requirement but cannot define the
   expected outcome.
3. An unplanned discovery capture remains a candidate until a separate Scenario
   gate admits its pressure.
4. Asset promotion never changes historical provenance.
5. Replay must fail closed on action divergence, asset exhaustion, missing
   Frame, invalid hash, or unsupported schema.
6. Regression asserts public behavior: result, dispatch safety, allowed or
   forbidden action, fresh Observation, and GoalEvidence. It does not assert
   private method order or diagnostic prose.
7. Golden replacement creates a new asset version. Existing golden bytes are
   not overwritten in place.

## 9. Persistent contract disposition

The existing Harness schema is useful but currently test-private. The approved
direction is:

- extract reusable asset records, validator, JSON options, ReplayEnvironment,
  and manifest conversion into a dedicated `UniClaw.Runtime.Harness` project;
- Harness depends on `UniClaw.Runtime`; Runtime has no reverse reference;
- keep physical capture DTOs/callbacks adapter-specific;
- composition code may reference both Harness and Adapters;
- preserve schema-v1 read compatibility or provide an explicit versioned
  migration test;
- do not copy the same records into a second namespace/assembly.

The ad-hoc golden trace JSON is retained as source evidence during migration.
It must not be silently reclassified as canonical typed Trace events where the
current Runtime did not emit equivalent structured facts.

## 10. Required OpenSpec change

Implementation requires a new change:

```text
openspec/changes/trace-capture-scenario-catalog-foundation/
```

Required artifacts:

- `proposal.md` — problem, scope, exclusions, graduated baselines;
- `design.md` — lifecycle, dependency graph, persistence atomicity, capture
  failure isolation, catalog ownership, schema compatibility;
- `specs/trace-capture-lifecycle/spec.md`;
- `specs/trace-capture-persistence/spec.md`;
- `specs/scenario-catalog/spec.md`;
- `specs/catalog-driven-replay-regression/spec.md`;
- `tasks.md` with implementation, invariant, and test verification on every
  slice.

Minimum SHALL requirements:

1. TraceCapture SHALL remain outside Agent, Container, and Traversal.
2. Capture/persistence failure SHALL NOT change Runtime behavior or cause
   dispatch/retry.
3. Every captured observation/action/result SHALL retain deterministic order
   and explicit correlation when available.
4. Missing provenance/correlation SHALL remain missing, not inferred.
5. Persistence SHALL publish atomically and SHALL NOT overwrite an existing
   capture ID.
6. Unreviewed raw captures SHALL NOT be automatically added to the repository
   catalog.
7. ScenarioCatalog SHALL be immutable and reject duplicate/dangling/invalid
   references before replay.
8. Scenario selection SHALL be explicit; no intent-based automatic routing.
9. Captured outcome SHALL NOT define Scenario expected behavior.
10. Replay SHALL run through the existing `IEnvironment` boundary and fail
    closed on divergence/exhaustion/integrity failure.
11. Runtime, semantic Model, Agent Trace, GoalEvidence, and `IEnvironment`
    contracts SHALL remain unchanged.
12. Existing versioned replay manifests SHALL remain readable or have a tested,
    explicit migration.

Minimum falsifying Scenarios:

| ID | Pressure |
|---|---|
| `SC-TC-001` | already-ON run captures Frames/result/zero SetSwitch and persists atomically |
| `SC-TC-002` | OFF-to-ON captures action/result/fresh post-action Frame and replays to satisfied GoalEvidence |
| `SC-TC-003` | store/capture fault leaves Runtime outcome and dispatch count unchanged; capture is not catalog-visible |
| `SC-TC-004` | Runtime failure with complete artifacts persists as a valid failed-run capture |
| `SC-CAT-001` | duplicate ID, dangling replay/frame, bad hash, or unsupported version is rejected before execution |
| `SC-CAT-002` | explicit catalog Scenario drives ReplayEnvironment and behavior assertions without private call-order checks |
| `SC-REG-001` | action divergence or Observation exhaustion fails closed and cannot produce a false regression pass |

## 11. Approved implementation slices

These slices define future apply order; none is started by this gate.

### TC-00 — OpenSpec purchase

Create, review, validate, and receive approval for the change above. No code.

### TC-01 — Extract reusable Harness contracts

- add `UniClaw.Runtime.Harness`;
- move, do not duplicate, asset contracts/validator/JSON/replay adapter from the
  test assembly;
- preserve existing manifests and replay behavior;
- add dependency guards proving Runtime does not reference Harness or Adapters.

### TC-02 — TraceCapture lifecycle, in memory

- implement `TraceCaptureSession`, immutable bundle/result, and
  `CapturingEnvironment`;
- prove order, correlation, Runtime Trace snapshot honesty, failure isolation,
  and distinct Runtime-failure/capture-failure results;
- use an in-memory store only.

### TC-03 — Append-only filesystem persistence

- implement `ITraceCaptureStore` plus `FileTraceCaptureStore`;
- prove staging/atomic publish, hashes, collision refusal, cancellation, and no
  partially published catalog candidate.

### TC-04 — Physical artifact attachment

- add the narrow optional artifact tap to `PhysicalEnvironment`;
- attach raw screenshot, perception output, and final Observation to the same
  Frame/sequence;
- prove capture failure cannot escape into physical observe/dispatch behavior.

### TC-05 — Immutable ScenarioCatalog

- implement explicit catalog loading/lookup/reference validation;
- reject duplicate, dangling, traversal/path-escape, schema, hash, and
  provenance errors;
- no mutable registration, global singleton, watcher, provider, or automatic
  Scenario selection.

### TC-06 — Canonical golden migration and catalog replay

- represent already-ON and OFF-to-ON golden cases as canonical manifests,
  ScenarioAssets, ReplayAssets, and catalog entries;
- retain source screenshots/perception/ad-hoc traces without inventing typed
  Runtime facts;
- replace hard-coded replay assembly only after equivalence is proven;
- run permanent regressions through explicit catalog IDs.

### TC-07 — Closeout and graduation

- run targeted capture/catalog/replay tests;
- run full regression, Architecture Guards, consistency, and strict OpenSpec;
- audit repository assets for provenance and accidental sensitive/raw capture;
- record a separate implementation result and Human graduation receipt.

## 12. Explicit exclusions

This design does not authorize:

- Agent, Container, Traversal, Environment-port, semantic Trace, GoalEvidence,
  Observation, action, or other semantic-contract changes;
- Provider/Capability registries or plugin framework;
- Planner, Brain, VLM, automatic scenario generation, or intent routing;
- automatic conversion of traces into expected behavior;
- remote/cloud trace service, database, queue, daemon, UI, or background watcher;
- automatic upload or commit of screenshots;
- arbitrary Trace event inference from diagnostic strings;
- production replay orchestration inside Agent.

## 13. Architecture fit and current verification

| Check | Result |
|---|---|
| Agent authority changes | **NO** |
| Container ownership changes | **NO** |
| Traversal protocol changes | **NO** |
| Semantic contract changes | **NO** |
| New Runtime mutable owner | **NO** |
| Provider framework introduced | **NO** |
| Planner/Brain introduced | **NO** |
| New external boundary justified | **YES — narrow trace persistence only** |

Current baseline verification before this documentation-only gate:

- full solution regression: 794/794 passed;
- consistency: C1-C10 passed;
- OpenSpec strict validation: 14/14 passed;
- `git diff --check`: passed;
- compiler reported four pre-existing test warnings (`CS8794`) in live/image
  classifier tests; no failure was introduced by this gate.

## 14. Final return

```text
TRACE_CAPTURE_AND_SCENARIO_CATALOG_ARCHITECTURE_GATE_RESULT

TraceCaptureLifecycle:
  APPROVED_HARNESS_OWNED_FAIL_ISOLATED

TracePersistenceBoundary:
  APPROVED_NARROW_APPEND_ONLY_ATOMIC_STORE

ScenarioCatalogOwnership:
  APPROVED_IMMUTABLE_ID_AND_REFERENCE_INDEX_ONLY

ScenarioAssetReplayRegressionFlow:
  APPROVED_WITH_EXPLICIT_REVIEW_AND_NO_OUTCOME_INFERENCE

RuntimeHarnessSplit:
  APPROVED_ZERO_RUNTIME_SEMANTIC_DELTA

OpenSpecRequirement:
  REQUIRED_TRACE_CAPTURE_SCENARIO_CATALOG_FOUNDATION

ImplementationSlices:
  TC_00_THROUGH_TC_07_APPROVED_FOR_FUTURE_APPLY_ORDER

BehaviorDelta:
  NONE

OwnershipDelta:
  NONE_TO_GRADUATED_RUNTIME

AuthorityDelta:
  NONE

SemanticContractDelta:
  NONE

ArchitectureDesign:
  APPROVED

Implementation:
  NOT_AUTHORIZED_STOP

Next:
  HUMAN_APPROVE_OPENSPEC_PROPOSAL_CREATION
```

STOP. No implementation or OpenSpec change is started by this gate.
