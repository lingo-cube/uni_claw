# Runtime Simulation & Replay Harness — Architecture Decision

> 2026-08-11 | Status: GRADUATED
> Baseline: e7c587e (componentization) + 1bc0505 (closure proofs)
> Scope: Canonical Harness architecture. Graduation evidence: `runtime-simulation-replay-harness-graduation.md`.

---

## 1. Simulation Modes

### S0 — COMPONENT SIMULATION

Pure isolated stateless component tests. No Agent. No Environment.

```
SemanticEvidence → BindingReconciler → ObjectBinding
Observation + Binding → StateBeliefReducer → state beliefs
SemanticAction + Binding → SemanticActionLowerer → DeviceAction / no-dispatch
```

Evidence: **SYNTHETIC**

### S1 — DETERMINISTIC RUNTIME SIMULATION

Real graduated Runtime against a deterministic programmable world (`IEnvironment`).

```
BusinessIntent → Agent → Container → Traversal → SimulationEnvironment → Observation
```

Purpose: semantic closed loop, retries, recovery, budget, contradiction, idempotence, action/effect distinction, failure injection.

Evidence: **SYNTHETIC** (programmed world state)

### S2 — OBSERVATION REPLAY

Replay previously captured Observation/ActionResult sequences through the real Runtime. **Perception is skipped.**

```
Recorded Observation → IEnvironment (replay adapter) → Runtime → assertions
```

This is the PRIMARY fast replay mode for real Runtime behavior.

Evidence: **RECORDED_REALITY** or **REALITY_SEEDED**

### S3 — PERCEPTION REPLAY

Raw/normalized visual assets → perception pipeline → Observation → Runtime.

```
Screenshot → YOLO/OCR/fusion → Observation → Runtime → assertions
```

**DEFERRED_BY_PERCEPTION_ADAPTER** — current perception pipeline cannot yet be called from the uni-agent repo. Schema + fixture boundary designed; execution blocked until perception adapter is callable.

Evidence: **RECORDED_REALITY** (reprocessed from raw screenshots)

### S4 — LIVE CALIBRATION

Real emulator / physical device. NOT the ordinary CI loop. Feeds assets back into S2/S3.

Evidence: **LIVE_CAPTURE**

---

## 2. Asset / Evidence Maturity

| Classification | Definition | Can Promote To |
|---|---|---|
| **SYNTHETIC** | Artificially constructed by tests/simulator | — (terminal) |
| **REALITY_SEEDED** | Derived from real screenshots/traces/pages, but fields were manually added, normalized, reconstructed, or synthesized | — (terminal) |
| **RECORDED_REALITY** | Captured directly from actual emulator/device/runtime without semantic fabrication for the recorded field | — (terminal) |
| **LIVE_CAPTURE** | Current active physical/emulator execution | — (terminal) |

Provenance is **immutable historical evidence**. Never silently upgrade. `REALITY_SEEDED` fields remain `REALITY_SEEDED` even when scenarios pass.

---

## 3. Reality Asset Model

### DeviceProfile

```
DeviceProfileId: string (stable)
Platform: Android | iOS | Windows | macOS | Browser | Synthetic
DeviceKind: Synthetic | Emulator | Physical
Manufacturer?: string
Model?: string
Display: { Width: int, Height: int, Density?: float, Orientation?: string }
OS: { Family?: string, Version?: string }
Interaction: { NavigationMode?: string, AccessibilityAvailable?: bool }
Capture: { ScreenshotFormat?: string, ScreenshotDimensions?: { Width: int, Height: int } }
```

Unknown metadata remains absent. No fabrication.

### CaptureSession

```
CaptureSessionId: string (stable)
DeviceProfileId: string
StartedAt?: string (ISO 8601)
Source: string (descriptive: "EP-04 sim-replay", "B1 golden", etc.)
Provenance: AssetMaturity
Frames: string[] (FrameIds, ordered)
TraceId?: string
SchemaVersion: int
```

### Frame

```
FrameId: string (stable)
CaptureSessionId: string
SequenceIndex: int (0-based position within session)
Timestamp?: string
Provenance: AssetMaturity
ScreenshotId?: string
NormalizedScreenshotId?: string
Observation?: Observation (serialized Runtime Observation)
Evidence?: { SemanticEvidence[] } (snapshot)
Belief?: { PageBelief, ObjectBindings, ObjectStateBeliefs } (snapshot)
Relations: FrameRelation[]
```

A Frame WITHOUT a Screenshot is valid (Observation Replay).

### Artifact

```
ArtifactId: string (stable)
FrameId: string
ArtifactType: RawScreenshot | NormalizedScreenshot | AnnotatedScreenshot
              | OcrResult | DetectorResult | FusionResult
              | RuntimeObservation | SemanticDerivation
ContentHash?: string (SHA-256)
Format: string
Provenance: AssetMaturity
DerivedFrom?: string (ArtifactId of source)
TransformDescription?: string
```

One Screenshot may have multiple derived artifacts without duplication.

### FrameRelation

```
RelationType: PreviousFrame | NextFrame | SameSession
            | DerivedFrom | ObservedAfterAction | ObservedBeforeAction
            | CauseContext
SourceFrameId: string
TargetFrameId: string
```

Explicit. Not filename-derived.

### FrameSequence

```
FrameSequenceId: string
Frames: string[] (FrameIds, ordered)
Description?: string
```

Ordering/context only. NOT semantic page identity.

---

## 4. Trace Model

### Trace Asset

```
TraceId: string (stable)
SchemaVersion: int
RuntimeVersion?: string
Commit?: string
ScenarioId?: string
DeviceProfileId?: string
CaptureSessionId?: string
Provenance: AssetMaturity
StartedAt?: string
Source?: string
Events: TraceEvent[]
```

### TraceEvent

Discriminated union of typed events. Each event carries a `string EventType` discriminator for serialization. Free-text `Reason` is diagnostic-only — never machine-parsed for control flow.

| EventType | Required Fields | Optional References |
|---|---|---|
| `INTENT_RECEIVED` | Expression: string | — |
| `INTENT_COMPILED` | Goal: SemanticGoalInput, Intent: string | — |
| `OBSERVATION_RECEIVED` | SequenceNumber: long | FrameId |
| `SEMANTIC_EVIDENCE_PRODUCED` | Evidence: SemanticEvidence[] | FrameId |
| `BELIEF_UPDATED` | PageBelief?, ObjectBindings?, ObjectStateBeliefs? | FrameId |
| `CAPABILITY_SELECTED` | CapabilityName: string | ObjectIdentity |
| `SEMANTIC_ACTION_AUTHORIZED` | Action: SemanticAction | — |
| `EXECUTION_DISPATCHED` | DeviceAction | — |
| `ACTION_RESULT` | ActionResult | — |
| `FRESH_OBSERVATION_RECEIVED` | SequenceNumber: long | FrameId |
| `VERIFICATION_RESULT` | Success: bool, Detail?: string | — |
| `GOAL_EVIDENCE_PRODUCED` | Satisfied: bool, Reason: string | SequenceNumber |
| `GOAL_COMPLETED` | Reason: string | — |
| `RUN_TERMINATED` | Outcome: string, Reason: string | — |

Trace semantics:
- Decision ≠ Action
- Dispatch ≠ Effect
- Observation ≠ Belief
- Belief ≠ Truth
- Recovery attempt ≠ Recovery success

---

## 5. Scenario Model

A Scenario is a **test/validation asset**, not test code.

```
ScenarioId: string (stable)
SchemaVersion: int
Category: BehavioralCategory (see §6) | DomainTag (see §6)
Mode: SimulationMode (S0-S4)
Provenance: AssetMaturity

Input:
  Intent?: BusinessIntent
  GoalInput?: SemanticGoalInput

World:
  SimulationConfig?: SimulationConfig
  ReplaySource?: { Observations: Observation[], ActionResults: ActionResult[] }
  FrameSequenceId?: string

Expected:
  Outcome: Satisfied | StateEvidenceRequired | BindingUnresolved
          | SemanticContradiction | BudgetExhausted | ExecutionFailed
  MaxDispatchCount?: int
  AllowedActions?: DeviceAction[]
  ForbiddenActions?: DeviceAction[]
  RequiresFreshObservation?: bool
  RequiresGoalEvidence?: bool
  MustNotComplete?: bool
  MustNotDispatch?: bool
```

Behavior-level assertions, not implementation call-order.

---

## 6. Scenario Taxonomy

### Behavioral Categories (primary — Runtime pressure)

| Category | Definition |
|---|---|
| **HAPPY_PATH** | Expected semantic goal completes |
| **ALREADY_SATISFIED** | Goal already true → zero mutation |
| **UNKNOWN_WORLD** | Perception gap → truthful non-completion |
| **CONTRADICTED_WORLD** | Contradictory signals → safe refusal |
| **DYNAMIC_WORLD** | World changes between observations |
| **ACTION_FAILURE** | Dispatch rejected/timed out |
| **RECOVERY** | Recovery path exercised |
| **ADVERSARIAL** | Deliberate goal-contradicting world |
| **BUDGET_NON_CONVERGENCE** | World never converges → bounded termination |
| **BINDING_DRIFT** | Object binding shifts/lost between frames |
| **GROUNDING_AMBIGUITY** | Multiple candidates with equal support |
| **PERCEPTION_INSUFFICIENCY** | Missing/incomplete perception signals |

### Domain Tags (secondary — coexist with behavioral category)

NETWORK | SETTINGS | BROWSER | ACCOUNT | etc.

---

## 7. Replay Modes

### R1 — OBSERVATION REPLAY

Recorded (Observation, ActionResult) sequences feed the Runtime through an `IEnvironment`-compatible replay adapter. Fast deterministic semantic regression.

**Status**: **SUPPORTED** — versioned Observation assets execute through `ReplayEnvironment : IEnvironment` and the real graduated Runtime; action divergence and asset exhaustion fail closed.

### R2 — PERCEPTION REPLAY

Recorded images reprocessed into Observation. Only if perception pipeline is callable.

**Status**: Schema designed. Execution: **DEFERRED_BY_PERCEPTION_ADAPTER**.

### R3 — TRACE REPLAY

Complete recorded Trace/Frame sequence drives world responses AND validates Runtime behavior against invariant/scenario expectations. Semantic equivalence, not byte-for-byte reproduction.

**Status**: **PARTIAL** — stable versioned Trace schema and the replay consumption boundary exist. Production Trace capture and Trace-driven execution remain explicitly deferred.

---

## 8. Stateful Simulation

Deterministic world simulator: Runtime actions mutate a modeled external world.

**Minimum domain**: `ConnectivitySetting` with `WifiConnectivity.Enabled` + `BluetoothConnectivity.Enabled`.

**Behavior**:
- Current boolean/UNKNOWN state
- Action response (Dispatched / Rejected / TimedOut)
- Post-action Observation regeneration (SwitchState reflects simulated world)
- Index stability (configurable drift injection)

### Failure Injection

Bounded, deterministic. Minimum faults:

| Fault | Description |
|---|---|
| ACTION_REJECTED | ExecuteAsync returns Rejected |
| ACTION_TIMEOUT_WORLD_UNCHANGED | TimedOut, world stays same |
| ACTION_TIMEOUT_WORLD_CHANGED | TimedOut, world actually changed |
| DISPATCH_SUCCESS_WORLD_UNCHANGED | Dispatched but world state unchanged |
| ELEMENT_INDEX_CHANGED | Indices shift between observations |
| BINDING_DISAPPEARED | Object binding lost in fresh observation |
| AMBIGUOUS_TARGET_APPEARED | Multiple toggle candidates appear |
| PAGE_CONTRADICTION | Contradictory page signals |

---

## 9. Core Boundary Rules

1. **IEnvironment is the replay boundary** — `ReplayEnvironment` and `SimulationEnvironment` implement `IEnvironment`. No `IReplayEnvironment` / `ISimulationEnvironment` unless proven necessary.

2. **Replay/Simulation are adapters AROUND the Runtime**, not part of semantic authority.

3. **Asset schemas live outside graduated Runtime semantic core** — separate project/namespace. Runtime contracts remain unchanged.

4. **No mutable asset service** — manifests are immutable records/value objects.

5. **Schema versioning**: `SchemaVersion` int on all persistent manifests. Stable IDs. CLR type names not used as persistent schema identity.

6. **Content integrity**: SHA-256 hashes for immutable binary/raw artifacts. Hash ≠ semantic identity.

7. **Runtime contracts unchanged**: IEnvironment, Observation, DeviceAction, SemanticEvidence, etc. — all graduated.

---

## 10. Storage / Repository

Existing convention: `artifacts/` directory in uni-claw repo for raw replay/trace evidence. This uni-agent Harness keeps small, version-controlled executable manifests under `tests/UniClaw.Runtime.Tests/Replay/Assets/`; external raw assets remain in their source repository until truthfully migrated.

**Executable manifest root**: `tests/UniClaw.Runtime.Tests/Replay/Assets/`.

Large binary raw assets handled per existing repo policy. No Git LFS unless already used.

Metadata/manifests version-controlled as JSON.

---

## 11. Architecture Gate Triggers

Auto-continue unless:
1. Replay requires modifying Agent semantic authority
2. Simulation requires a second Container/state owner
3. IEnvironment cannot represent replay truthfully
4. Persistent asset schema requires embedding mutable Runtime state
5. Trace design would change Goal completion authority
6. Harness requires changing graduated Semantic contracts
7. A new Provider/Facade becomes mandatory
8. Existing invariant must change

---

## 12. Summary

```
SIMULATION_MODES:       S0 Component | S1 Runtime | S2 Observation Replay | S3 Perception Replay (DEFERRED) | S4 Live Calibration
ASSET_MATURITY:         SYNTHETIC | REALITY_SEEDED | RECORDED_REALITY | LIVE_CAPTURE
REPLAY_MODES:           R1 Observation Replay | R2 Perception Replay (DEFERRED) | R3 Trace Replay (PARTIAL)
IENVIRONMENT_BOUNDARY:  ReplayEnvironment implements IEnvironment — no new port
CORE_RUNTIME_DELTA:     NONE — agents are adapters around the graduated spine
GRADUATED_SPINE:        UNCHANGED
```

**Lifecycle**: `RUNTIME_SIMULATION_REPLAY_HARNESS = GRADUATED`. Deferred perception/provider work requires a separate capability gate.

STOP.
