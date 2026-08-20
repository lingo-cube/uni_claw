# Protocol Debt Inventory

> Produced by: `PROJECT_LEADER_UNIAGENT_GLOBAL_ARCHITECTURE_ALIGNMENT_AND_CLEANUP`
> Baseline: [UniAgent Architecture v1](uniagent-architecture-v1-core-development-guide.md)
> Date: 2026-08-19
> Mode: INVENTORY ONLY — **Do NOT solve protocol debt in this change.**
> Purpose: This inventory becomes the input to the next Protocol Consolidation change.

Per Architecture v1 §10, Protocol Consolidation happens AFTER architecture
cleanup. This document records current protocol surfaces and their drift from
v1 target concepts, but does NOT redesign them.

---

## Protocol surface registry

### P-01 — `run.start`

| Field | Value |
|---|---|
| Current semantic purpose | Start a RuntimeAgent run with a goal + objects + capabilities + device selector |
| Current producer | DSH plugin (`adapter.runStart`) / `RunStartWireContract` |
| Current consumer | `DriverHost/Execution/RunExecutionCoordinator` → `Agent.RunSemanticGoalAsync` |
| Architecture v1 target concept | Runtime Protocol — UniAgent→RuntimeAgent goal entry (v1 §6/§9) |
| Mismatch/drift | Carries `SemanticGoalInput` + `objects[]` + `capabilities[]` (resolved type-level inputs), NOT v1's deferred `TaskSpec` (Reserved Extension). No acceptance/safety fields. `maxIterations` is composition-side default, not a wire field. |
| Disposition | **KEEP** (current contract is the graduated v1 entry; TaskSpec is reserved) |
| Debt notes | TaskSpec / acceptance / safety fields → Reserved Extension (v1 invariant 19); do NOT add now |

### P-02 — `run.list` / `run.snapshot.get`

| Field | Value |
|---|---|
| Current semantic purpose | Enumerate runs / get a run's read-only snapshot |
| Current producer | `DriverHost/Projection/RunSnapshotProjector` |
| Current consumer | DSH plugin commands (`uniclaw-runs-list`, `uniclaw-inspect-run`) |
| Architecture v1 target concept | Runtime Protocol — data plane read (v1 §6/§8) |
| Mismatch/drift | Snapshot classification (DirectPublicProjection / DerivedReadModel / NotCurrentlyAvailable) is RuntimeAgent-internal vocabulary; wire exposes it verbatim. No v1 mismatch. |
| Disposition | **KEEP** |
| Debt notes | Snapshot read-model taxonomy may be formalized in Protocol Consolidation |

### P-03 — `run.events.after` / `run.events.drain`

| Field | Value |
|---|---|
| Current semantic purpose | Cursor-based RuntimeEvent stream (exclusive seq > cursor) / drain all |
| Current producer | `DriverHost/Store/RuntimeEventStore` + `RuntimeEventProjector` |
| Current consumer | DSH plugin (`uniclaw-events-after`) + control-plane client polling |
| Architecture v1 target concept | Runtime Protocol — data plane event stream (v1 §8 Data Plane) |
| Mismatch/drift | 18-family RuntimeEvent vocabulary (A/B/C classification). Polling (2000ms), not push. OBS-F9: `RuntimeEvent.Sequence` ≠ `ObservationSequence` (frozen, correct). |
| Disposition | **KEEP** |
| Debt notes | Push vs polling; event vocabulary formalization → Protocol Consolidation |

### P-04 — `run.trap.get`

| Field | Value |
|---|---|
| Current semantic purpose | Read a run's active Trap (7-field immutable evidence) |
| Current producer | RuntimeAgent `Agent.LastTrap` (projected) |
| Current consumer | DSH plugin (`uniclaw-inspect-trap`) |
| Architecture v1 target concept | Runtime Protocol — data plane evidence read (v1 §6) |
| Mismatch/drift | None (Trap is evidence, not decision — v1 invariant 17 respected) |
| Disposition | **KEEP** |

### P-05 — `evidence.get`

| Field | Value |
|---|---|
| Current semantic purpose | Resolve a logical EvidenceRef to evidence metadata |
| Current producer | `DriverHost/Store/EvidenceCatalog` |
| Current consumer | DSH plugin (`uniclaw-evidence-open`) |
| Architecture v1 target concept | Runtime Protocol — data plane evidence (v1 §6) |
| Mismatch/drift | Logical locators only; persistent resolution NOT implemented (deferred). |
| Disposition | **KEEP** |
| Debt notes | Persistent EvidenceRef resolution → future buyer |

### P-06 — `control.support`

| Field | Value |
|---|---|
| Current semantic purpose | Audit which control operations are supported (read-only) |
| Current producer | `DriverHost/Control/ControlSupportAudit` |
| Current consumer | DSH plugin |
| Architecture v1 target concept | Control Plane (v1 §8) |
| Mismatch/drift | `start` is authorized (`AUTHORIZED_RUN_START_ENTRY`); pause/resume/stop/abort DEFERRED. No mutating control registered. |
| Disposition | **KEEP** |
| Debt notes | pause/resume/stop/abort → future control-plane buyer (Reserved Extension) |

### P-07 — `assistance.pending` / `assistance.resolve`

| Field | Value |
|---|---|
| Current semantic purpose | RuntimeAgent posts a pending assistance request; DSH resolves with advice |
| Current producer | `DriverHost/Assistance/AssistanceWireProvider` (pending) / DSH bridge (resolve) |
| Current consumer | DSH plugin `AssistanceBridge` → `LlmAssistanceConsumer` → `assistance.resolve` |
| Architecture v1 target concept | Runtime Protocol — safe hook boundary (v1 §6, invariant 17) |
| Mismatch/drift | Capacity 8, timeout 30s (COMPOSITION_POLICY). `worldVersion` staleness binding. Advice is advisory-only (never writes belief/state/truth/completion). |
| Disposition | **KEEP** |
| Debt notes | Advice vocabulary formalization; trigger surface (Contradicted/Unresolved only) → Protocol Consolidation |

### P-08 — `ping`

| Field | Value |
|---|---|
| Current semantic purpose | Identity/health probe |
| Current producer | `DriverHost/Transport/UniClawDriverHostServer` |
| Current consumer | DSH plugin |
| Architecture v1 target concept | Runtime Protocol — transport health |
| Disposition | **KEEP** |

---

## RuntimeAgent-internal model surfaces (NOT wire protocol, recorded for completeness)

### M-01 — Observation / ObservedElement

| Field | Value |
|---|---|
| Current semantic purpose | RuntimeAgent observation evidence (elements, foreground, seq) |
| Architecture v1 target concept | RuntimeAgent-internal evidence (v1 §3 RuntimeAgent) |
| Disposition | **KEEP** (RuntimeAgent-internal; not protocol debt) |

### M-02 — WorldBelief / Container beliefs / GoalEvidence

| Field | Value |
|---|---|
| Current semantic purpose | RuntimeAgent belief + completion evidence |
| Architecture v1 target concept | RuntimeAgent-internal (v1 §3; GoalEvidence = KERNEL_ONLY, OBS-F9 frozen) |
| Disposition | **KEEP** |

### M-03 — Trap / RecoveryResult / RecoveryAnchor

| Field | Value |
|---|---|
| Current semantic purpose | RuntimeAgent failure + recovery evidence |
| Architecture v1 target concept | RuntimeAgent-internal (v1 §3) |
| Disposition | **KEEP** |

### M-04 — RuntimeEvent (18 families)

| Field | Value |
|---|---|
| Current semantic purpose | Observability event projection (A/B/C classification) |
| Architecture v1 target concept | Data Plane (v1 §8) |
| Disposition | **KEEP** |
| Debt notes | Event vocabulary formalization → Protocol Consolidation |

---

## Session references

### S-01 — DSH Session (pinned checkout)

| Field | Value |
|---|---|
| Current semantic purpose | DSH-side session (command invocation, history, identity) |
| Architecture v1 target concept | v1 Session = correlation root (v1 §4); implemented in DSH (v1 invariant 15) |
| Mismatch/drift | DSH `Session` currently serves command invocation context. v1 Session is the UniAgent collaboration correlation root. These are compatible but the v1 Session contract (append-oriented, projection-only latest refs, NOT message bus/state/event-store) is not yet formalized as a UniClaw-side contract. |
| Disposition | **MISSING_CONTRACT** (v1 Session contract not yet formalized on UniClaw side) |
| Debt notes | Session Contract → Protocol Consolidation (v1 §10). Do NOT invent Session DTOs now. |

### S-02 — TraceCaptureSession (Runtime.Harness)

| Field | Value |
|---|---|
| Current semantic purpose | Diagnostic trace capture session (test/harness only) |
| Architecture v1 target concept | NOT v1 Session — naming overlap only |
| Disposition | **KEEP** (no conflict; different concept, different layer) |

---

## Capability integration paths

### C-01 — IAssistanceProvider (Brain hook)

| Field | Value |
|---|---|
| Current semantic purpose | RuntimeAgent optional assistance (advice at Contradicted/Unresolved) |
| Architecture v1 target concept | Brain = enhanced intelligence Capability (v1 §5, invariant 14); safe hook (v1 §6, invariant 17) |
| Mismatch/drift | None — advice-only, never authority. Bounded consult (MaxAssistanceConsults=3). |
| Disposition | **KEEP** |
| Debt notes | Hook taxonomy (v1: no typed hooks in v1) → Reserved Extension |

### C-02 — ISwitchStateReader (Perception contract)

| Field | Value |
|---|---|
| Current semantic purpose | Provider-neutral switch-state read contract |
| Architecture v1 target concept | v1 invariant 13 (RuntimeAgent owns perception contract, not Vision impl) |
| Mismatch/drift | UNPURCHASED_L2_CONTRACT_CANDIDATE (final-gate not passed; 8 evidence items required) |
| Disposition | **MISSING_CONTRACT** (not yet frozen; 8 preconditions unresolved) |
| Debt notes | Frame lifetime, production provider, composition, build-zone map → future gate |

### C-03 — Shadow cognition (DSH-side)

| Field | Value |
|---|---|
| Current semantic purpose | DSH-side ephemeral analysis (human-request-only, zero Kernel consumption) |
| Architecture v1 target concept | Brain = enhanced intelligence (v1 §5); DSH = impl host (v1 invariant 15) |
| Mismatch/drift | None — Kernel consumes ZERO Shadow output (authority firewall) |
| Disposition | **KEEP** |

---

## Transport

### T-01 — Loopback TCP newline JSON-RPC (127.0.0.1:5177)

| Field | Value |
|---|---|
| Current semantic purpose | Runtime Protocol transport between DSH plugin (client) and DriverHost (listener) |
| Architecture v1 target concept | Runtime Protocol transport (v1 §6) |
| Disposition | **KEEP** |

---

## Reserved Extensions (NOT to be introduced in Protocol Consolidation v1)

Per v1 invariant 19, the following are Reserved Extensions and must NOT be
designed until a real buyer authorizes them:

- TaskSpec / BusinessIntent autonomous entry
- IntelligenceSeam / IIntelligenceProvider production consumer
- Multi-agent, Sub Run, Branch Run, Multi-Run
- Dynamic Capability Grant, Typed Hook hierarchy
- Long-lived Agent Scheduling, Complex Recovery Workflow
- pause/resume/stop/abort mutating controls

---

## Disposition summary

| Disposition | Count |
|---|---|
| KEEP | 12 |
| MISSING_CONTRACT | 2 (Session contract, ISwitchStateReader) |
| RENAME | 0 |
| SPLIT | 0 |
| MERGE | 0 |
| DEPRECATE | 0 |

## Next step

This inventory is the input to the next **Protocol Consolidation** change, which
will formalize: RuntimeAgent Protocol, Session Contract, Capability Contract,
Hook Contract (per v1 §10). Protocol Consolidation MUST be architecture-
constrained, scenario-validated, and existing-evidence-driven. It MUST NOT
invent protocols from imagination or open Reserved Extensions.
