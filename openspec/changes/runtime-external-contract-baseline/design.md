# Design: runtime-external-contract-baseline

> Pure-documentation contract baseline. No code, no DTO, no Runtime modification.
> Every "current reality" statement is verified repository evidence (2026-08-17).
> Cross-reference: `docs/decisions/runtime-dsh-architecture-gap-analysis.md`.

---

## 1. Purpose and scope of the contract

The **Runtime External Contract** fixes the boundary between the UniClaw Runtime
(execution authority) and any external Intelligence Harness (DSH today). It has
three functions:

1. **Freeze what exists**: the implemented Goal and Data plane surfaces keep their
   exact semantics; the contract makes them the versioned base.
2. **Define the frame for what comes next**: deferred planes (Assistance, Guidance,
   Execution Handoff) get boundary semantics + authority constraints now, so later
   gates implement inside a defined contract instead of growing ad-hoc protocols.
3. **Anchor the isolation**: the contract is the single source for what may cross
   the boundary and what may never cross it (authority clauses).

The contract is documentation. It does not implement anything.

---

## 2. Target architecture model (contract context)

```
┌─ Runtime.Agent ─────────────────────────────┐
│ independent autonomous execution agent;     │
│ semantic decision / authorization /         │
│ execution / verification authority          │
└──────────────┬──────────────────────────────┘
               │ Runtime External Contract (5 planes)
┌──────────────▼──────────────────────────────┐
│ Integration Layer / Adapter                 │
│ protocol conversion; binding per harness    │
│ version; isolation of Runtime from          │
│ DSH/Cordis/concrete model implementations   │
└──────────────┬──────────────────────────────┘
┌──────────────▼──────────────────────────────┐
│ External Intelligence Harness (DSH)         │
│ general intelligence host: LLM/VLM/subagent │
│ /tool/UI/observation; never controls        │
│ Runtime internal state; never bypasses      │
│ Runtime execution authority                 │
└─────────────────────────────────────────────┘
```

Evolution route (planning context; each phase gated separately):

```
Phase A Runtime as Standalone Agent        ✅ implemented (semantic loop, fail-closed)
Phase B Runtime as Executable Subagent     ✅ implemented (run.start vertical slice)
Phase C Runtime Collaborative Agent        ⬜ L1 CONSULT / L2 DELEGATE_PLANNING
Phase D Runtime + Harness Hybrid           ⬜ L3 YIELD / deep collaboration
```

---

## 3. The five-plane contract

### 3.1 Plane 1 — Goal (External → Runtime)

| Aspect | Contract |
|---|---|
| Direction | External → Runtime |
| Target message | `RunGoal` — Semantic Goal + Object Identity + Desired State |
| Semantics | intent-level only; never physical steps (no coordinates, no `DeviceAction`, no element index, no precompiled sequence) |
| Implementation (current reality) | `run.start` wire method; request = `{ goal: SemanticGoalInput, objects: SemanticObject[], capabilities: Capability[], device: DeviceSelector }` (`RunStartRequest`); response = `RunAccepted { accepted, runId, runState }` — DriverHost-owned runId, asynchronous, `request_rejected` deterministic rejection |
| Status | **IMPLEMENTED — frozen as contract clause** |
| Extension (deferred) | execution constraints (e.g. maxIterations), acceptance criteria, TaskSpec-shaped intent — NOT part of this baseline; added by a later gate only with a real consumer |

### 3.2 Plane 2 — Data (Runtime → External)

| Aspect | Contract |
|---|---|
| Direction | Runtime → External |
| Target messages | `RuntimeSnapshot` (Goal / World state / Belief / Progress / Execution status / Blocker / Artifact references); `RuntimeEvent` (Lifecycle / Observation changes / Execution events / Assistance signals) |
| Semantics | evidence, not truth; classified fields (DirectPublicProjection / DerivedReadModel / NotCurrentlyAvailable); no internal state leakage (no Container/Binding/StateBelief internals) |
| Implementation (current reality) | frozen 8 read-only methods: `ping`, `run.list`, `run.snapshot.get`, `run.trap.get`, `run.events.after`, `run.events.drain`, `evidence.get`, `control.support`; `RunSnapshot` (13 classified fields); 18-family `RuntimeEvent` vocabulary (A/B/C classified; C-class never emitted); `EvidenceRef` logical locator + `evidence.get` |
| Status | **IMPLEMENTED — frozen as contract clause** |
| Extension (deferred) | explicit "Blocker" field (currently composed from ActiveTrap/Reason/Diagnostics — PARTIAL); "Assistance signals" event family (depends on the Assistance seam existing first) — NOT part of this baseline |

### 3.3 Plane 3 — Assistance (Runtime → External) — DEFERRED

| Aspect | Contract |
|---|---|
| Direction | Runtime → External |
| Target message | `AssistanceRequest` — semantic interpretation / perception enrichment / candidate ranking / recovery planning / route planning |
| Core semantics | **NOT an LLM call** — the Runtime expresses which capability it lacks. The external host answers with information; the Runtime keeps final decision authority (L1 CONSULT) |
| Implementation | **ZERO implementation (verified)** — no `AssistanceRequest` token anywhere in `src/` or `dsh-plugin-uniclaw/src/`; the mother-doc `IntelligenceSeam` (adjudication-point consult, advice mode) is design-only (`docs/decisions/outer-intelligence-integration-architecture.md` §3) |
| Boundary declaration (this gate) | ① Runtime-initiated; ② capability-gap expression, never a model invocation; ③ external output is advice (candidate information), Kernel decides (I-3); ④ response must carry correlation + bound world version (see §5) |
| Wire format | **NOT frozen** (conceptual shape may be illustrated in design only) |
| Owner gate | `RUNTIME_ASSISTANCE_SEAM_GATE` (L1 CONSULT) — after this baseline |

### 3.4 Plane 4 — Guidance (External → Runtime) — DEFERRED

| Aspect | Contract |
|---|---|
| Direction | External → Runtime |
| Target message | `GuidanceProposal` — Hypothesis / Recommendation / Next waypoint / Expected effect / Additional evidence |
| Core semantics | **Guidance ≠ Truth ≠ Authorization ≠ Goal completion**; the Runtime executes and verifies; completion still requires Kernel GoalEvidence (I-10) |
| Implementation | **ZERO implementation (verified)** — no `GuidanceProposal` token anywhere; Phase 6 intent compilation is design-only (target-architecture review) |
| Boundary declaration (this gate) | ① external suggestion only; ② never authorizes a physical action; ③ never completes a run; ④ Runtime retains execution + verification; ⑤ closest existing shape is the caller-injected `viewportExplorationEvaluator` (runtime knowledge, Agent decides) — an in-process analogue, NOT a cross-process Guidance protocol |
| Wire format | **NOT frozen** |
| Owner gate | L2 `DELEGATE_PLANNING` (after Assistance; far-term) |

### 3.5 Plane 5 — Execution Handoff (Runtime ↔ External) — DEFERRED

| Aspect | Contract |
|---|---|
| Direction | Runtime ↔ External |
| Target messages | `ExecutionYield` (Runtime cannot safely handle the current interaction) / `ExecutionReturn` (after external handling, Runtime re-observes and reconciles) |
| Core semantics | temporary release of the execution lease; requires lease/reconcile semantics beyond the current `RunState` model (`Idle/Initializing/Running/Completed/Failed`; `Terminated` reserved) |
| Implementation | **ZERO implementation (verified)** — `Agent.RunSemanticGoalAsync` blocks to a terminal state (single-Run per instance); the run.start path passes `CancellationToken.None` (no cancellation surface in the slice) |
| Boundary declaration (this gate) | ① lease is temporary and explicit; ② Runtime re-observes fresh evidence after `ExecutionReturn` (world wins — I-4); ③ never a bypass of execution authority |
| Wire format | **NOT frozen** |
| Owner gate | L3 `YIELD` (Phase C/D; far-term) |

---

## 4. Versioning policy (contract level)

| Rule | Contract |
|---|---|
| Wire protocol version (current) | `UniClawWireContract.ProtocolVersion = 1` (returned by `ping`) — frozen |
| Frozen method set | the 8 read-only methods + `run.start` = 9 methods whose semantics are frozen (additive evolution only) |
| Additive-first | a new plane adds NEW methods/messages; it never modifies an existing method's semantics (R10 precedent of dsh-runtime-agent-subagent-run-entry) |
| Backward compatibility | any evolution must keep existing consumers working (existing DTO shapes, error codes, cursor semantics unchanged) |
| Deprecation | deprecation requires an explicit contract amendment (a new OpenSpec change); removal is not allowed without a real consumer migration |
| Contract-level version | the baseline is versioned by the change archive (`runtime-external-contract-baseline`); the wire keeps its own integer version — the two are distinct (contract document version vs wire protocol version) |

---

## 5. Correlation and world-version primitives (pre-defined, not implemented)

These primitives are defined NOW so the future Assistance plane has a frame. They
reuse existing raw fields; no new code is added by this gate.

| Primitive | Definition | Existing raw basis (verified) |
|---|---|---|
| **Correlation** | every asynchronous Runtime-initiated request/response pair carries a request correlation identity; responses must echo it; uncorrelated responses are discarded | `RuntimeEvent.CorrelationId` (reuses `TraceRun.TraceId`); `RuntimeEvent.EventId` stable per event |
| **World version** | the monotonic `Observation.SequenceNumber` is the world-version value; a request records the world version it was issued against; a response is stale and MUST be rejected if the world has advanced beyond it | `Observation.SequenceNumber` (monotonic, kernel-assigned, `IEnvironment.ObserveAsync`); `RuntimeEvent.ObservationSequence` anchors events to a world version |
| **Staleness rule** | advice/guidance bound to an old world version never mutates current belief; the Runtime re-observes (fresh evidence) before applying anything | existing re-observe semantics in `Agent.SemanticRun.cs` (bounded re-observation, fail-closed) |

---

## 6. Collaboration levels (contract definition)

| Level | Contract | Current reality |
|---|---|---|
| **L0 LOCAL** | Runtime completes autonomously; no external interaction | ✅ implemented (semantic loop) |
| **L1 CONSULT** | Runtime requests external information; Runtime keeps final decision | ⬜ seam MISSING (Plane 3) |
| **L2 DELEGATE_PLANNING** | Runtime cannot plan next step; external provides route/guidance; Runtime executes and verifies | ⬜ MISSING (Plane 4) |
| **L3 YIELD_EXECUTION** | Runtime temporarily releases the execution lease | ⬜ MISSING (Plane 5) |

Levels are additive: a higher level never removes a lower level's authority.

---

## 7. Authority clauses (contract, unchanged from current reality)

1. `DirectDSHPhysicalAuthority = MUST_BE_NO` — DSH requests intent; never coordinates/actions.
2. `DirectDSHGoalEvidenceAuthority = MUST_BE_NO` — completion only from Kernel (`RunCompleted`/`RunFailed`, GoalEvidence; I-10).
3. `DirectDSHBindingAuthority / DirectDSHStateBeliefAuthority = MUST_BE_NO` — Container state private (OBS-F10, Guard 10c).
4. `AgentDependsOnDSH = MUST_BE_NO` — Runtime zero ProjectReference (Guard 1), zero DSH tokens (Guard 2/10b), plugin guard A/B.
5. `DriverHostProcessOwnedByPlugin = MUST_BE_NO` — plugin connects, never supervises (frozen process-lifecycle decision).
6. `GuidanceIsNotTruth = MUST_HOLD` — Guidance ≠ Truth ≠ Authorization ≠ Goal completion.
7. `AssistanceIsCapabilityGapExpression = MUST_HOLD` — an Assistance request expresses a missing capability; it is not an LLM invocation.
8. `ModelCallsForControlPath = MUST_BE_0` — all control-path operations are deterministic (F16/F17 node guards + zero-model run.start).
9. `KernelKeepsExecutionAuthority = MUST_HOLD` — no plane may bypass execution/verification (I-2/I-3).

Mechanical enforcement currently in place (verified): Guard 1/2/10a/10b/10c/10d,
`PluginIntegrationGuardTests` (A/B/C/D/F/F2), node F16/F17, `RunStartExecutionSeam_NotInAgentSemantics_AndSurfaceStaysReadOnly`.

---

## 8. Implemented-surface mapping (contract appendix)

| Target message | Current surface | Verified source |
|---|---|---|
| `RunGoal` (intent) | `run.start` + `RunStartRequest` + `RunAccepted` | `DriverHost/Execution/RunStartRequest.cs`, `RunStartWireContract.cs`; `Transport/UniClawDriverHostServer.cs` case "run.start" |
| `RuntimeSnapshot` | `run.snapshot.get` → `RunSnapshot` (13 classified fields) | `Projection/RunSnapshotProjector.cs`, `Transport/UniClawWireContract.cs` |
| `RuntimeEvent` | `run.events.after/drain` → 18-family vocabulary | `Projection/RuntimeEventProjector.cs`, `Model/RuntimeEventKind.cs` |
| Artifact references | `EvidenceRef` + `evidence.get` | `Model/EvidenceRef.cs`, `Projection/EvidenceCatalog.cs` |
| Execution status / trap | `run.trap.get` → classified `InspectTrapResult` | `Control/UniClawControlSurface.cs` |
| Control audit | `control.support` (start = AUTHORIZED_RUN_START_ENTRY; pause/resume/stop/abort = DEFERRED) | `Control/ControlSupportAudit.cs` |
| Run registry | `run.list` → DriverHost-owned runIds | `DriverHostObservability.RegisteredRunIds` |

---

## 9. Open questions (for later gates, not decided here)

1. Whether an explicit `Blocker` snapshot field is needed (currently composed from
   ActiveTrap/Reason/Diagnostics) — decide with a real consumer.
2. Whether the future Assistance request needs its own correlation channel or
   reuses the event-stream correlation fields — decide in the Assistance seam gate.
3. TaskSpec-shaped intent fields — only with a real Phase-6-style consumer.

## 10. Out of scope / deferred summary

- **Deferred planes**: Assistance (Plane 3), Guidance (Plane 4), Execution Handoff
  (Plane 5) — boundaries + authority only, no wire freeze (F9).
- **Future concepts NOT assumed to exist**: TaskSpec, AgentProfile, intelligence
  settings, intent compilation, escalation protocol (F7).
- **Code**: zero changes (F1).
