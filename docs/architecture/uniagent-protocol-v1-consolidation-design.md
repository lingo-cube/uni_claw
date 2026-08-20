# UniAgent v1 Semantic Protocol Model — Consolidation Design (FROZEN)

> Status: **FROZEN** — `PROJECT_LEADER_UNIAGENT_PROTOCOL_V1_FINAL_FREEZE` (2026-08-19)
> Authority: `PROJECT_LEADER_UNIAGENT_PROTOCOL_CONSOLIDATION_DESIGN` → FINAL_FREEZE
> Mode: PROTOCOL DESIGN ONLY — NO production code changes, NO DTO implementation,
> NO JSON-RPC migration, NO L2 Planning, NO multi-agent/multi-run, NO typed hooks.
> Date: 2026-08-19 (PRE-FREEZE REPAIR + FINAL FREEZE applied)
> Head: `203cf83` (uni-agent branch)
> Authoritative inputs:
> - [`uniagent-architecture-v1-core-development-guide.md`](uniagent-architecture-v1-core-development-guide.md) (frozen Architecture v1)
> - [`protocol-debt-inventory.md`](protocol-debt-inventory.md) (audit input)
> - [`alignment-inventory.md`](alignment-inventory.md) (alignment input)
>
> **This document is the canonical frozen UniAgent v1 semantic protocol baseline,
> subordinate to Architecture v1.** It is the sole active protocol baseline.
> Older protocol/assistance/runtime documents are historical evidence, NOT
> parallel top-level protocol authorities.
>
> Principle: Architecture semantics define the boundary. Scenarios validate them.
> Protocols realize them. This document designs the MINIMUM semantic protocol
> that realizes v1 — it does NOT preserve historical API shape merely because
> it exists, and does NOT invent from imagination.
>
> **PRE-FREEZE REPAIR:** three bounded semantic repairs applied:
> (1) Capability-Hook / External-Hook overlap removed — one Capability Contract
> surface + one cross-cutting Runtime External Hook Boundary; (2) Escalation no
> longer equated with Terminal Outcome — non-terminal escalation is
> architecturally expressible but not yet transport-realized; (3) ISwitchStateReader
> reclassified as DEFERRED_CAPABILITY_INSTANCE, not a missing v1 contract.

---

## Deliverable 1 — Protocol Concept Map

The v1 protocol model has **three semantic contract surfaces**, **one
cross-cutting invocation/authority boundary**, and **one correlation root**.
Everything else is projection, internal model, or implementation detail.

```
┌─────────────────────────────────────────────────────────────────┐
│  Session (Surface S — correlation root)                          │
│  NOT transport · NOT state store · NOT agent                     │
│  append-oriented facts · projections/index only                  │
└──────────┬──────────────────────────────────────────────────────┘
           │ references (append-only)
           │
┌──────────▼──────────────────────────────────────────────────────┐
│  UniAgent                                                        │
│  supervisory autonomy                                            │
└──────────┬──────────────────────────────────────────────────────┘
           │
           │  ▶ Surface A: UniAgent → RuntimeAgent (Directive)
           │  ◀ Surface B: RuntimeAgent → UniAgent (Outcome)
           │
┌──────────▼──────────────────────────────────────────────────────┐
│  RuntimeAgent                                                    │
│  bounded autonomy · execution truth authority                   │
│                                                                  │
│  ┌────────────────────────────────────────────────────┐         │
│  │  Surface C: Capability Contract                    │         │
│  │  Brain / Vision / Memory / Operator / future       │         │
│  │  each capability defines its own request/response  │         │
│  └────────────────────────────────────────────────────┘         │
│                                                                  │
│  ══════════════════════════════════════════════════════          │
│  ═  Runtime External Hook Boundary (cross-cutting) ═            │
│  ═  safe invocation · advisory-only authority    ═            │
│  ═  failure semantics · staleness/correlation    ═            │
│  ═  RuntimeAgent accept/reject/reconcile         ═            │
│  ══════════════════════════════════════════════════════          │
│  (applies to ALL capability invocations — not a peer surface)   │
└──────────┬──────────────────────────────────────────────────────┘
           │
           │  Transport Boundary (transport-independent)
           │  current impl: loopback TCP newline JSON-RPC
           │
┌──────────▼──────────────────────────────────────────────────────┐
│  Composition Host / AgentHost / DSH (implementation framework)   │
└─────────────────────────────────────────────────────────────────┘
```

**Three semantic surfaces:**

| Surface | Direction | v1 concept | Purpose |
|---|---|---|---|
| A | UniAgent → RuntimeAgent | Directive | Request bounded execution |
| B | RuntimeAgent → UniAgent | Outcome | Communicate results, uncertainty, escalation, terminal state |
| C | RuntimeAgent ↔ Capability | Capability Contract | Per-capability request/response for evidence/advice |

**One cross-cutting boundary:**

| Boundary | Applies to | Rules |
|---|---|---|
| Runtime External Hook Boundary | ALL capability invocations (Surface C) | Safe invocation, advisory-only authority, failure semantics, staleness/correlation, RuntimeAgent accept/reject/reconcile ownership |

**One correlation root:** Session (Surface S) — references only, never transport.

**One transport boundary:** transport-independent; current impl is one option.

**Why Surface D was removed:** The former "Surface D — External Hook" expressed
the *same* authority pattern as Surface C (RuntimeAgent → safe external
invocation → advisory/evidence response → accept/reject/reconcile). The
distinction between "capability" and "external advisory" was a naming
difference, not a semantic one. Assistance is the transport realization of the
Brain Capability Contract (Surface C) invoked through the cross-cutting Hook
Boundary — not a separate peer surface. No repository evidence requires an
independent Surface D: the authority pattern, failure semantics, and
RuntimeAgent ownership are identical. Merging into one Capability Contract
surface + one cross-cutting Hook Boundary eliminates the duplication without
losing any semantic expressiveness.

---

## Deliverable 2 — Semantic Owner Matrix

| Semantic concept | v1 owner | Produces | Consumes | Second-truth risk |
|---|---|---|---|---|
| Directive (goal/directive) | UniAgent | UniAgent | RuntimeAgent | NO — RuntimeAgent accepts/rejects, does not re-originate |
| Run lifecycle (Idle→Running→Completed/Failed) | RuntimeAgent | RuntimeAgent | UniAgent (read-only) | NO — RunState sole owner = RuntimeAgent (I-2) |
| Observation | RuntimeAgent (via Environment) | Environment | RuntimeAgent | NO — evidence, not truth (I-4) |
| WorldBelief | RuntimeAgent | RuntimeAgent (Reconcile) | RuntimeAgent internal | NO — rebuilt from fresh observation |
| GoalEvidence | RuntimeAgent (kernel, OBS-F9 frozen) | EvidenceEvaluator | RuntimeAgent (Agent decides) | NO — KERNEL_ONLY authority |
| Trap | RuntimeAgent (Agent emits) | RuntimeAgent | RuntimeAgent / UniAgent (read) | NO — evidence, not decision (I-8) |
| RecoveryResult | RuntimeAgent (Recovery component) | Recovery | RuntimeAgent (Agent decides) | NO — mechanism, not authority |
| Terminal outcome | RuntimeAgent | RuntimeAgent | UniAgent | NO — RunCompleted/RunFailed = RuntimeAgent-owned |
| Non-terminal escalation | RuntimeAgent | RuntimeAgent | UniAgent (supervisory) | NO — RuntimeAgent signals; UniAgent adjudicates supervisory strategy without bypassing RuntimeAgent authority |
| Uncertainty (pre-terminal) | RuntimeAgent | RuntimeAgent | Capability (via Hook Boundary) | NO — RuntimeAgent requests info, retains decision |
| Capability advice/evidence | Capability (Brain/Vision/Memory) | Capability | RuntimeAgent | NO — advisory-only, never authority (v1 invariant 14/17) |
| Session facts | Producer of the fact | Each producer | All (read-only) | NO — append-only, projections are not truth |
| Snapshot / events | RuntimeAgent (projection) | RuntimeAgent projection | UniAgent / Data Plane | NO — classified projections, not canonical state |

**Authority firewall:** No consumer of any surface becomes a second truth
engine. The protocol carries producer-derived semantics; the consumer reads,
references, or advises — it never re-originate truth.

---

## Deliverable 3 — UniAgent → RuntimeAgent Minimal Contract (Surface A)

### A.1 Semantic concept: Directive

A Directive is the minimum UniAgent→RuntimeAgent semantic: a bounded request
for RuntimeAgent to execute within its bounded autonomy.

```
Directive (semantic contract)
  ├─ goal           : SemanticGoalInput          (what state to achieve)
  ├─ objects        : SemanticObject[]           (declared semantic targets)
  ├─ capabilities   : Capability[]               (declared business capabilities)
  └─ device         : DeviceSelector             (which physical world)
```

**Current realization:** `RunStartRequest` (4 fields) → `run.start` wire method
→ `RunExecutionCoordinator.StartRun` → `Agent.RunSemanticGoalAsync`.

**What the Directive IS:**
- A bounded goal-level declaration (WHAT to achieve, not HOW)
- RuntimeAgent retains execution, grounding, verification, completion authority
- DriverHost-owned runId (acceptance is asynchronous; run executes async)
- `ONE_ACTIVE_RUN_PER_DEVICE` enforcement is a control-layer policy

**What the Directive is NOT:**
- NOT TaskSpec (Reserved Extension — v1 invariant 19)
- NOT a Plan / precompiled steps (I-5: Plan is hypothesis)
- NOT coordinates, DeviceAction, element indexes, prompts
- NOT AgentProfile / consult settings / acceptance criteria (reserved)
- NOT supervisory strategy alteration (that is a future UniAgent → RuntimeAgent
  extension — see A.2; directionally Surface A, NOT Surface B; remains Reserved)

### A.2 Supervisory strategy alteration (Reserved — not in v1 minimum)

v1 invariant 5: "UniAgent may alter supervisory strategy but may not directly
overwrite Runtime belief/state or bypass fresh observation/grounding/verification."

**Direction:** Supervisory strategy alteration is **UniAgent → RuntimeAgent**
(directionally Surface A), NOT RuntimeAgent → UniAgent (Surface B). Surface B
carries RuntimeAgent-produced outcomes; it does not carry UniAgent-originated
strategy commands. A future supervisory-alteration message would extend
Surface A, not Surface B.

**Current runtime behavior does NOT require any explicit supervisory-alteration
message.** The RuntimeAgent's closed loop is self-contained; UniAgent's
supervisory role is realized through:
1. Directives (start a run with a goal — Surface A)
2. Reading outcomes (Surface B — RuntimeAgent-produced, read-only)
3. Session correlation (Surface S)

**If a future real buyer requires runtime strategy alteration** (e.g.,
UniAgent redirecting a run mid-execution), that would be a Reserved Extension
of Surface A and requires a fresh gate. **v1 minimum: NO supervisory-alteration
message.** Do NOT design that extension.

### A.3 Contract invariants (Surface A)

1. Directive carries only task-level declarations — never physical steps.
2. RuntimeAgent accepts or rejects; rejection is deterministic
   (`request_rejected`), never a fabricated run.
3. runId is RuntimeAgent-side-owned (DriverHost); UniAgent never creates runId.
4. Acceptance is asynchronous — `RunAccepted(runId, RunState)` returns immediately.
5. RuntimeAgent retains all execution authority; UniAgent gains zero physical
   /GoalEvidence/binding/belief authority (v1 invariant 17).

**Disposition of current `run.start`:** KEEP_AS_CONTRACT — the 4-field
`RunStartRequest` IS the minimal Directive contract. No field added, no field
removed, no rename. TaskSpec/acceptance/safety are Reserved Extensions.

---

## Deliverable 4 — RuntimeAgent → UniAgent Minimal Contract (Surface B)

### B.1 Semantic concept: Outcome

RuntimeAgent→UniAgent communication is **read-only projection of
RuntimeAgent-owned execution truth.** UniAgent never becomes a second truth
engine. Architecture v1 permits the following distinct RuntimeAgent→UniAgent
semantics:

```
Outcome (semantic contract — read-only projections)
  ├─ Result / Progress      : ordinary execution progress + intermediate state
  ├─ Uncertainty            : RuntimeAgent cannot adjudicate locally (pre-terminal)
  ├─ Escalation             : supervisory adjudication requested / decision-required
  ├─ Snapshot               : inspectable state reference (classified read model)
  └─ TerminalOutcome        : RunCompleted | RunFailed (with reason)
```

### B.2 Semantic distinctions (required by Architecture v1)

Architecture v1 explicitly permits Result, Uncertainty, Snapshot, Escalation,
and Terminal as distinct RuntimeAgent→UniAgent semantics. The protocol model
MUST be able to represent all five. The critical distinction repaired in this
revision: **Escalation ≠ TerminalOutcome.**

| Semantic distinction | Architecturally valid? | Currently transport-realized? | Current realization |
|---|---|---|---|
| Ordinary progress / result | YES | YES | `RuntimeEvent` stream (ActionDispatched, NavigationDecision, etc.) + `RunSnapshot` |
| Uncertainty (pre-terminal) | YES | YES (internally) | RuntimeAgent consults Capability via Hook Boundary; if unresolved → fail-closed |
| Snapshot / inspectable state | YES | YES | `run.snapshot.get` (classified: Direct/Derived/NotAvailable) |
| **Escalation (non-terminal, supervisory)** | **YES** | **NO** | `SEMANTICALLY_FROZEN_NOT_YET_REALIZED` — see B.3 |
| Terminal outcome | YES | YES | `RunCompleted` / `RunFailed` event + `RunState=Completed/Failed` |

### B.3 Escalation: semantically frozen, not yet transport-realized

**Architecture v1** permits RuntimeAgent to signal a non-terminal supervisory
escalation to UniAgent — a "decision-required" state where RuntimeAgent
preserves its execution authority but requests supervisory adjudication.
This is architecturally distinct from terminal failure.

**Current implementation** does NOT realize non-terminal escalation as a
transport surface. Current behavior:
1. RuntimeAgent encounters uncertainty (belief Contradicted / Unresolved)
2. RuntimeAgent consults Capability via Hook Boundary (Surface C)
3. If actionable advice → RuntimeAgent reconciles and continues (local closure)
4. If no actionable advice → RuntimeAgent fails closed → `RunFailed` (terminal)

Step 4 is the **only currently-realized escalation path**, and it is terminal.
A **non-terminal** escalation (RuntimeAgent signals UniAgent "I need supervisory
adjudication but I am not terminal") has **no transport realization** in v1.

**Classification:** `SEMANTICALLY_FROZEN_NOT_YET_REALIZED`

- The protocol model **freezes the semantic distinction** (Escalation ≠ Terminal)
  so that a future transport realization does not require re-opening protocol
  semantics.
- The protocol model does **NOT** invent:
  - pause/resume wire APIs
  - redirect DTOs
  - DecisionRequired JSON-RPC methods
  - mid-run mutation commands
- Those require a **real implementation buyer** and a fresh gate (Reserved
  Extension).

**Current implementation behavior remains unchanged.** RuntimeAgent may
continue to: resolve uncertainty locally, consult Capability/Assistance, and
fail closed when unresolved. The semantic freeze does not alter working behavior.

### B.4 What Outcome IS

- Producer-derived (RuntimeAgent produces; UniAgent reads)
- Classified (DirectPublicProjection / DerivedReadModel / NotCurrentlyAvailable —
  the consumer knows what is canonical vs derived vs absent)
- Append-oriented event stream (OBS-F9: RuntimeEvent.Sequence ≠ ObservationSequence)
- Terminal outcomes carry reason, never fabricated completion

### B.5 What Outcome is NOT

- NOT a command channel (UniAgent cannot mutate RuntimeAgent state through reads)
- NOT a second truth store (projections are not canonical; Diagnostics mark gaps)
- NOT a push channel in v1 (current: 2000ms polling; push is a future option)
- NOT reconstructed truth (UniAgent must not re-originate belief/state from events)

### B.6 Contract invariants (Surface B)

1. All outcomes are producer-derived (RuntimeAgent); consumer never re-originate.
2. RunState is sole-owner (RuntimeAgent); completion only from GoalEvidence (I-10).
3. Snapshot classification is truthful (Direct/Derived/NotAvailable — never invented).
4. Event stream is append-oriented; OBS-F9 domain separation frozen.
5. Terminal outcome carries explicit reason; no fabricated completion.
6. Consumer reads are idempotent and harmless (repeated polls = same data).
7. **Escalation is semantically distinct from TerminalOutcome** (architecturally
   valid; non-terminal escalation transport is `SEMANTICALLY_FROZEN_NOT_YET_REALIZED`).
8. Current implementation may continue to resolve uncertainty locally and fail
   closed; the semantic freeze does not mandate new transport.

**Disposition of current surfaces:**
- `run.list` / `run.snapshot.get` → KEEP_AS_PROJECTION (Snapshot)
- `run.events.after` / `run.events.drain` → KEEP_AS_PROJECTION (Progress/Result events)
- `run.trap.get` → KEEP_AS_PROJECTION (Trap evidence read)
- `evidence.get` → KEEP_AS_PROJECTION (evidence metadata; persistent resolution deferred)
- `ping` → KEEP_AS_TRANSPORT (health; not a semantic contract surface)
- Non-terminal escalation transport → `SEMANTICALLY_FROZEN_NOT_YET_REALIZED` (no wire method; Reserved Extension for transport realization)

---

## Deliverable 5 — Session Minimal Contract (Surface S)

### S.1 Semantic concept: Correlation Root

Session is the **mandatory collaboration/correlation root** for one UniAgent
activity. It is NOT transport, NOT state store, NOT event store, NOT agent.

```
Session (semantic contract)
  ├─ sessionId          : identity
  ├─ context            : activity context (who/what/why)
  ├─ runRefs            : append-only Run references
  ├─ decisionRefs       : append-only UniAgent Decision references
  ├─ capabilityRefs     : append-only Capability interaction references
  ├─ evidenceRefs       : append-only Evidence/Artifact references
  ├─ summary            : projection/index (mutable latest only)
  └─ navigation         : index over appended facts
```

### S.2 Append-oriented fact model

```
Producer → appends own facts ONLY:
  RuntimeAgent → Runtime facts (run started, run completed, trap raised, …)
  UniAgent     → Decisions (directive issued, strategy altered, …)
  Operator     → Operator decisions
  Memory       → Summary projection
```

**Already-happened facts are never rewritten.** `latestRunRef`,
`latestDecisionRef`, `summary` are **projections / indexes** — mutable latest
only, never the facts themselves.

### S.3 What Session MUST NOT be (v1 invariants 6-8)

| Forbidden | Reason |
|---|---|
| Message bus | UniAgent↔RuntimeAgent realtime uses Runtime Protocol (Surface A/B), NOT Session |
| Runtime state store | RuntimeAgent owns execution truth; Session only references |
| Event Store | RuntimeEvent stream is RuntimeAgent-owned projection (Surface B) |
| Agent | Session has no autonomy, no decision authority |
| Command queue | Directives flow through Surface A, not Session |
| Generic mutable JSON state | Only append-facts + projection indexes |

### S.4 Current state and gap

**Current:** DSH-side `Session` (pinned checkout) serves command invocation
context. This is **compatible** with v1 Session as correlation root, but the
v1 Session contract is **not yet formalized as a UniClaw-side contract**.

**Gap classification: MISSING_CONTRACT.** The UniClaw side currently has no
Session contract type — Session references flow through DSH implementation.
This is acceptable for v1 (DSH is the impl host, v1 invariant 15), but the
**semantic contract** (append-oriented, projection-only, correlation-root-only)
must be frozen so that:
1. A future non-DSH host can implement the same Session contract.
2. No consumer accidentally treats Session as transport/state.

### S.5 Contract invariants (Surface S)

1. Session is correlation root only — never transport, state, or agent.
2. Facts are append-only; latest refs/summary are projections.
3. Each producer appends only its own facts.
4. Session references Runs/Decisions/Capabilities/Evidence — it does not own them.
5. UniAgent↔RuntimeAgent realtime interaction does NOT flow through Session.
6. v1 default: 1 Session / 1 Primary Goal / 1 Primary Run (invariant 18).

**Disposition:** MISSING_CONTRACT → formalize as semantic contract (this design).
No DTO implementation in this phase.

---

## Deliverable 6 — Capability Contract Model (Surface C)

### C.1 Semantic concept: Capability Contract

A Capability is an independent external semantic ability (Brain, Vision, Memory,
Operator, future). RuntimeAgent owns the perception/execution contract;
Capabilities provide evidence/advice, never authority. Each capability defines
its own request/response semantics according to its nature — they are NOT
forced into one identical operational API.

```
CapabilityContract (generic semantic surface)
  ├─ Request  : per-capability request type   (RuntimeAgent → Capability)
  ├─ Response : per-capability response type   (Capability → RuntimeAgent)
  └─ Authority: ADVISORY_ONLY (never truth/authorization/completion)
```

All capability invocations are governed by the **Runtime External Hook
Boundary** (Deliverable 7), which defines the cross-cutting invocation/
authority/failure rules that apply to every capability.

### C.2 Per-capability semantic distinctions

| Capability | v1 role | Request semantics | Response semantics | Current instance | Status |
|---|---|---|---|---|---|
| Brain | Enhanced intelligence (v1 §5, invariant 14) | Adjudication context (belief Contradicted/Unresolved) | Candidate recommendation + evidence (advisory) | `IAssistanceProvider.ConsultAsync` | FROZEN (graduated) |
| Vision | Perception (v1 invariant 13) | Perception contract (bounds/frame) | Perception evidence (type/state/bounds) | `ISwitchStateReader.ReadAsync` | DEFERRED_CAPABILITY_INSTANCE (UNPURCHASED; 8 preconditions) |
| Memory | Knowledge (v1 §5) | (future) | (future) | Not implemented | NOT_PURCHASED |
| Operator | Human / supervisory / external advisory input (v1 §5) | Human or supervisory context / decision advisory | Advisory / decision input (advisory only) | Not implemented | NOT_PURCHASED |

### C.3 Contract invariants (Surface C)

1. Capability output is candidate information — never truth, authorization,
   goal completion, or physical action (v1 invariant 14/17).
2. RuntimeAgent retains all decision authority; Capability never acquires
   Runtime authority by being connected (v1 invariant 17).
3. Capability failure (null/timeout/error) → RuntimeAgent fail-closed; never
   progress on absent capability.
4. Capability does not receive mutable Runtime state — only immutable context
   snapshots (I-2/I-13).
5. v1: no typed hook taxonomy (invariant 19 — Reserved Extension).
6. Each capability may define its own request/response shape; the common
   boundary is the Hook Boundary (Deliverable 7), not an identical API.

**Operator authority clarification (frozen):** Operator represents human /
supervisory / external advisory / console-mediated human judgment input. It
MUST NOT own: grounding, physical execution, Runtime state, belief truth,
GoalEvidence, verification, or completion authority. RuntimeAgent remains the
sole execution / world-truth authority (PI-10/PI-12/PI-22). Operator is an
advisory/input capability under Surface C, governed by the Hook Boundary —
NOT an execution capability. No Operator API or implementation is introduced
in v1.

**Disposition of `IAssistanceProvider`:** KEEP_AS_CONTRACT — it IS the Brain
Capability Contract instance. `AssistanceContext`/`AssistanceAdvice` are the
request/response semantic shapes. The `worldVersion` staleness binding is a
correct correlation mechanism.

**Disposition of `ISwitchStateReader`:** DEFERRED_CAPABILITY_INSTANCE — it is
an implementation-level candidate instance of the Vision Capability Contract,
NOT a missing v1 semantic contract. The generic Vision/Perception capability
boundary is already frozen (Surface C). ISwitchStateReader is UNPURCHASED with
8 preconditions unresolved (final-gate). It must NOT become mandatory v1
implementation scope merely because protocol v1 is frozen. Its final-gate
requirements remain intact.

---

## Deliverable 7 — Runtime External Hook Boundary (Cross-Cutting)

### H.1 Semantic concept: Cross-Cutting Invocation/Authority Boundary

The Runtime External Hook Boundary is **NOT an independent peer semantic
surface** — it is a cross-cutting set of invocation, authority, failure, and
ownership rules that apply to **ALL capability invocations** (Surface C). It
replaces the former "Surface D — External Hook" which duplicated Surface C's
authority pattern.

```
Runtime External Hook Boundary (cross-cutting, applies to all Surface C invocations)
  ├─ safe invocation boundary   : RuntimeAgent requests at defined points only
  ├─ advisory/evidence-only     : capability output is never truth/authorization/completion
  ├─ failure semantics          : null/timeout/invalid → fail-closed (never progress)
  ├─ staleness/correlation      : worldVersion/requestId binding; stale advice discarded
  └─ Runtime ownership          : RuntimeAgent owns accept/reject/reconcile decision
```

### H.2 Safe invocation points (derived from existing behavior)

The existing runtime has exactly **one** safe invocation point: belief
adjudication (Contradicted / Unresolved). This is where RuntimeAgent may
request external capability information. No other invocation point exists in
current behavior.

**v1 minimum: one invocation point (belief adjudication).** Adding more
invocation points is a Reserved Extension.

### H.3 Authority boundary (frozen, applies to all capabilities)

| Boundary | Rule |
|---|---|
| Capability advice/evidence | Never writes belief/binding/state/truth/completion |
| Stale/uncorrelated advice | Discarded (worldVersion/requestId binding) |
| Invalid recommendation | Rejected (whitelist: re-observe / rebind / dismiss-obstruction for Brain) |
| Timeout / capacity overflow | Fail-closed; never hangs, never fabricates |
| Runtime decision | RuntimeAgent retains accept/reject/reconcile ownership |

### H.4 Contract invariants (Hook Boundary)

1. The Hook Boundary is cross-cutting — it applies to ALL Surface C invocations;
   it is not a separate peer surface.
2. No typed hook taxonomy in v1 (no SemanticHook/DiagnosisHook/RecoveryHook).
3. Invocation only at defined safe points (current: belief adjudication).
4. Advisory/evidence-only — never authority (v1 invariant 17).
5. Bounded (capacity + timeout as COMPOSITION_POLICY, not contract).
6. RuntimeAgent owns accept/reject/reconcile; hook never bypasses.
7. Staleness/correlation enforced (worldVersion/requestId).

---

## Deliverable 8 — Existing Surface → Target Contract Mapping

| Current surface | Semantic concept | Producer | Consumer | Classification | Target disposition |
|---|---|---|---|---|---|
| `run.start` | Directive (Surface A) | UniAgent (via DSH) | RuntimeAgent (DriverHost) | architecture contract | **KEEP_AS_CONTRACT** |
| `run.list` | Run enumeration (Surface B) | RuntimeAgent projection | UniAgent | projection/query API | **KEEP_AS_PROJECTION** |
| `run.snapshot.get` | Run snapshot (Surface B) | RuntimeAgent projection | UniAgent | projection/query API | **KEEP_AS_PROJECTION** |
| `run.events.after` | Event stream (Surface B) | RuntimeAgent projection | UniAgent | projection/query API | **KEEP_AS_PROJECTION** |
| `run.events.drain` | Event drain (Surface B) | RuntimeAgent projection | UniAgent | projection/query API | **KEEP_AS_PROJECTION** (merge conceptually with `events.after`; same surface) |
| `run.trap.get` | Trap evidence (Surface B) | RuntimeAgent | UniAgent | projection/query API | **KEEP_AS_PROJECTION** |
| `evidence.get` | Evidence metadata (Surface B) | RuntimeAgent | UniAgent | projection/query API | **KEEP_AS_PROJECTION** |
| `control.support` | Control audit (Control Plane) | RuntimeAgent | UniAgent | projection/query API | **KEEP_AS_PROJECTION** (read-only audit; mutating controls Reserved) |
| `assistance.pending` | Capability hook poll (Surface C + Hook Boundary) | RuntimeAgent (pending) | External host (Brain) | transport API | **KEEP_AS_TRANSPORT** (realizes Surface C Brain contract via Hook Boundary) |
| `assistance.resolve` | Capability hook resolve (Surface C + Hook Boundary) | External host (Brain) | RuntimeAgent | transport API | **KEEP_AS_TRANSPORT** (realizes Surface C Brain contract via Hook Boundary) |
| `ping` | Transport health | RuntimeAgent | UniAgent | implementation detail | **KEEP_AS_TRANSPORT** (not semantic contract) |
| `IAssistanceProvider` | Brain Capability Contract (Surface C) | RuntimeAgent requests | Brain capability | architecture contract | **KEEP_AS_CONTRACT** |
| `ISwitchStateReader` | Vision Capability Contract instance (Surface C) | RuntimeAgent requests | Vision capability | implementation-level candidate | **DEFERRED_CAPABILITY_INSTANCE** (UNPURCHASED; 8 preconditions; final-gate intact) |
| DSH `Session` | Session correlation root (Surface S) | DSH impl | all | architecture contract | **MISSING_CONTRACT** (not formalized UniClaw-side) |
| `RuntimeEvent` (18-family) | Observability events (Surface B) | RuntimeAgent projection | UniAgent/Data Plane | projection/query API | **KEEP_AS_PROJECTION** |
| `RunSnapshot` classification | Snapshot truth classification | RuntimeAgent | UniAgent | implementation detail | **INTERNALIZE** (correct; keep as projection metadata) |
| `Observation` / `WorldBelief` / `Trap` / `GoalEvidence` | RuntimeAgent-internal models | RuntimeAgent | RuntimeAgent | implementation detail | **INTERNALIZE** (not protocol; RuntimeAgent-internal) |
| Shadow cognition | Brain capability (DSH-side) | DSH | human (zero Kernel consumption) | implementation detail | **INTERNALIZE** (authority firewall; not protocol) |
| Non-terminal escalation transport | Surface B Escalation semantic | (not yet implemented) | (not yet implemented) | architecture-valid, not realized | **SEMANTICALLY_FROZEN_NOT_YET_REALIZED** (Reserved Extension for transport) |

**Key finding:** No surface needs RENAME, SPLIT, MERGE, or DEPRECATE based on
existing behavior. The current surface set is semantically minimal and correct
**for v1**. The gaps (Session contract, non-terminal escalation transport) are
semantic freezes, not existing surfaces to reshape. ISwitchStateReader is an
implementation-level deferred instance, not a protocol gap.

---

## Deliverable 9 — Assistance Semantic Decomposition Decision

### Audit: what does `assistance.*` represent?

| Concern | Present in assistance.*? | Evidence |
|---|---|---|
| RuntimeAgent → UniAgent escalation | NO — terminal `SemanticContradiction`/`RunFailed` is the terminal escalation (Surface B); assistance is pre-terminal capability consultation | Assistance fires at belief adjudication, before terminal |
| Generic capability request | YES — the Brain Capability Contract (Surface C) | `IAssistanceProvider` = Brain contract; invocation via Hook Boundary |
| Brain invocation | YES — `LlmAssistanceConsumer` uses `ctx.llm` | DSH-side `assistance/llm-consumer.js` |
| DSH transport mechanism | YES — `assistance.pending`/`resolve` are wire methods | `AssistanceWireContract.cs` |
| RuntimeAgent truth authority | NO — advice never writes belief/state/truth | Frozen: advisory-only |

### Decomposition decision (repaired)

`assistance.*` is the transport realization of **one** semantic path:

```
RuntimeAgent
  → encounters uncertainty (belief Contradicted/Unresolved)
  → invokes Brain Capability Contract (Surface C) via Hook Boundary
  → receives advisory candidate (AssistanceAdvice)
  → accepts/rejects/reconciles (RuntimeAgent retains authority)
  → if no actionable advice → fail-closed → RunFailed (Surface B terminal)
```

**There is exactly one semantic path**, not two surfaces. The former design
described assistance as "realizing both C and D" — that was the overlap.
Assistance realizes the Brain Capability Contract (Surface C) invoked through
the cross-cutting Hook Boundary (Deliverable 7). No independent Surface D
exists.

**The semantic contract and transport mechanism are already cleanly separated
in the codebase:**
- `IAssistanceProvider` + `AssistanceContext` + `AssistanceAdvice` (RuntimeAgent-
  internal, BCL+Model only) = Brain Capability Contract (Surface C)
- `AssistanceWireProvider` / `AssistanceWireContract` / DSH `AssistanceBridge`
  = transport realization via Hook Boundary

**No L1 behavior redesign is required.** The existing separation is
architecturally correct. The only action is to **formally name** the single
semantic path in the protocol model (done above):
- `IAssistanceProvider` = Brain Capability Contract instance (Surface C)
- `assistance.pending`/`resolve` = transport realization via Hook Boundary
- Trigger surface (Contradicted/Unresolved) = the one safe invocation point
- Advice whitelist (re-observe/rebind/dismiss-obstruction) = Brain advisory vocabulary

**No architecture contradiction found.** The L1 behavior is sound and conforms
to v1 invariants 14/17. Do NOT redesign.

---

## Deliverable 10 — Transport-vs-Semantic Separation Map

| Layer | What it contains | Current realization | v1 rule |
|---|---|---|---|
| **Semantic Protocol** | Directive (A), Outcome (B), Capability Contract (C), Session (S) | `RunStartRequest`, `RunSnapshot`/`RuntimeEvent`/`Trap`, `IAssistanceProvider`/`AssistanceContext`/`AssistanceAdvice`, (Session: MISSING_CONTRACT) | Transport-independent; valid if transport changes |
| **Runtime External Hook Boundary** | Cross-cutting invocation/authority/failure rules for all Surface C invocations | Realized in `AssistanceWireProvider` (staleness, capacity, timeout, accept/reject) | Applies to all capabilities; not a peer surface |
| **Transport API** | Wire methods that realize semantic protocol | `run.start`, `run.list`, `run.snapshot.get`, `run.events.*`, `run.trap.get`, `evidence.get`, `control.support`, `assistance.pending`/`resolve`, `ping` | Current: loopback TCP newline JSON-RPC |
| **DSH Plugin Adapter** | DSH-side binding of transport to DSH concepts | `dsh-plugin-uniclaw/src/` (adapter.js, protocol.js, commands.js, assistance/, shadow/) | DSH = impl framework (v1 invariant 15); not architecture |
| **Data/Query Projection** | Read-only projections of RuntimeAgent truth | `RunSnapshotProjector`, `RuntimeEventProjector`, `RuntimeEventStore`, `EvidenceCatalog` | Data Plane (v1 §8); not Agent Core |

**Separation rule:** The semantic protocol (Surfaces A/B/C + S) and the Hook
Boundary MUST remain valid if transport later becomes in-process, TCP,
JSON-RPC, remote service, or another Host. The current JSON-RPC method names
are transport realizations, not semantic contract names. **Do NOT implement
alternative transports now.**

**Contamination check (must be absent from semantic protocol):**
- DSH-specific names (Cordis, ctx.llm, session events) → ABSENT from
  `RunStartRequest`/`IAssistanceProvider`/`AssistanceContext`/`AssistanceAdvice`
  (verified: these types depend only on BCL + Model)
- JSON-RPC-specific assumptions → ABSENT from semantic contract types
- Polling-specific assumptions → ABSENT (polling is transport; semantic is
  "event stream with cursor")
- Plugin-specific concepts → ABSENT

**Result:** Semantic protocol is transport-clean. No contamination found.

---

## Deliverable 11 — Protocol Invariants

| # | Invariant | Source |
|---|---|---|
| PI-1 | Directive carries only task-level declarations; never physical steps | v1 §6, I-5 |
| PI-2 | RuntimeAgent owns runId creation; UniAgent never creates runId | v1 §3, existing behavior |
| PI-3 | Acceptance is asynchronous; run executes async | existing behavior |
| PI-4 | RunState sole owner = RuntimeAgent; completion only from GoalEvidence | I-2, I-10, OBS-F9 |
| PI-5 | All outcomes are producer-derived; consumer never re-originate truth | v1 §6, invariant 17 |
| PI-6 | Snapshot classification is truthful (Direct/Derived/NotAvailable) | existing behavior |
| PI-7 | Event stream is append-oriented; RuntimeEvent.Sequence ≠ ObservationSequence | OBS-F9 frozen |
| PI-8 | Terminal outcome carries explicit reason; no fabricated completion | I-10 |
| PI-9 | **Escalation is semantically distinct from TerminalOutcome** (architecturally valid; non-terminal transport `SEMANTICALLY_FROZEN_NOT_YET_REALIZED`) | v1 §3 (permits escalation), PRE-FREEZE REPAIR |
| PI-10 | Capability output is advisory/evidence-only; never authority | v1 invariant 14/17 |
| PI-11 | Hook failure (null/timeout/invalid) → fail-closed; never progress | existing behavior |
| PI-12 | RuntimeAgent retains accept/reject/reconcile ownership for all capability invocations | v1 §6 |
| PI-13 | The Runtime External Hook Boundary is cross-cutting — applies to ALL Surface C invocations; not a peer surface | PRE-FREEZE REPAIR |
| PI-14 | No typed hook taxonomy in v1 | v1 invariant 19 |
| PI-15 | Session is correlation root only; never transport/state/agent | v1 invariant 6-8 |
| PI-16 | Session facts are append-only; projections are not truth | v1 invariant 8 |
| PI-17 | UniAgent↔RuntimeAgent realtime does NOT flow through Session | v1 §4 |
| PI-18 | v1 default: 1 Session / 1 Primary Goal / 1 Primary Run | v1 invariant 18 |
| PI-19 | No multi-agent/multi-run/sub-run/branch-run in v1 | v1 invariant 19 |
| PI-20 | Transport is replaceable; semantic contract is transport-independent | v1 §6 |
| PI-21 | DSH is implementation framework; DSH concepts are not architecture | v1 invariant 15 |
| PI-22 | External capability never acquires Runtime authority by being connected | v1 invariant 17 |
| PI-23 | Each capability may define its own request/response shape; the common boundary is the Hook Boundary, not an identical API | v1 §5, PRE-FREEZE REPAIR |

---

## Deliverable 12 — Reserved Extensions (NOT in v1 protocol)

| Reserved extension | Condition to open | v1 invariant |
|---|---|---|
| TaskSpec / BusinessIntent autonomous entry | Real buyer who cannot provide resolved Goal/spec | 19 |
| IntelligenceSeam / IIntelligenceProvider production consumer | Real adjudication buyer beyond L1 advice | 19 |
| Supervisory strategy alteration messages | Real mid-run redirect buyer | 5 (alter strategy) |
| **Non-terminal escalation transport** (RuntimeAgent→UniAgent decision-required without terminal) | Real supervisory-adjudication buyer needing non-terminal transport realization | v1 §3 (permits escalation); PI-9 |
| Multi-agent / Sub Run / Branch Run / Multi-Run | Real multi-run orchestration buyer | 19 |
| Dynamic Capability Grant | Real dynamic capability-granting buyer | 19 |
| Typed Hook hierarchy (SemanticHook/DiagnosisHook/RecoveryHook) | Real typed-hook buyer | 19 |
| Long-lived Agent Scheduling | Real long-lived scheduling buyer | 19 |
| Complex Recovery Workflow | Real complex-recovery buyer | 19 |
| pause/resume/stop/abort mutating controls | Real control-plane mutation buyer | (control.support DEFERRED) |
| Push-based event delivery (vs polling) | Real push buyer | (transport optimization) |
| Persistent EvidenceRef resolution | Real persistent-evidence buyer | (evidence.get deferred) |
| Additional Hook invocation points (beyond belief adjudication) | Real buyer requiring invocation at other points | 19 |
| ISwitchStateReader freeze (Vision Capability Contract instance) | 8 preconditions resolved (final-gate) | 13 |

**Rule:** None of these may be designed/implemented until a real buyer
authorizes via a fresh gate. v1 protocol is the minimum that realizes current
architecture. **Non-terminal escalation transport** is now explicitly listed:
the semantic distinction is frozen (PI-9), but the transport realization is a
Reserved Extension requiring a real buyer.

---

## Deliverable 13 — Migration Impact Assessment

| Artifact | Impact | Action required |
|---|---|---|
| `run.start` wire + `RunStartRequest` | NONE — already the minimal Directive | No migration |
| `run.list/snapshot.get/events.*/trap.get/evidence.get` | NONE — already correct projections | No migration |
| `control.support` | NONE — read-only audit; mutating controls stay DEFERRED | No migration |
| `assistance.pending/resolve` wire | NONE — already cleanly realizes Brain Capability Contract (Surface C) via Hook Boundary | No migration |
| `IAssistanceProvider` + `AssistanceContext` + `AssistanceAdvice` | NONE — already the Brain Capability Contract instance | No migration |
| `ping` | NONE — transport health; not semantic | No migration |
| DSH `Session` | LOW — semantically compatible but UniClaw-side contract not formalized | Future: formalize Session contract (MISSING_CONTRACT) when a non-DSH host buyer or a contract-freeze gate authorizes |
| `ISwitchStateReader` | NONE — DEFERRED_CAPABILITY_INSTANCE; not v1 protocol scope; final-gate requirements intact | Future: resolve 8 preconditions (final-gate) when a real Vision buyer authorizes; NOT triggered by protocol v1 freeze |
| `RuntimeEvent` 18-family vocabulary | LOW — correct but not formally named as protocol vocabulary | Future: Protocol Consolidation implementation phase may formalize names |
| Transport (loopback TCP JSON-RPC) | NONE — transport is replaceable; semantic contract is transport-independent | No migration |
| Non-terminal escalation transport | NONE — `SEMANTICALLY_FROZEN_NOT_YET_REALIZED`; no transport exists and none is invented | Future: Reserved Extension when real supervisory-adjudication buyer authorizes |

**Overall migration impact: NONE for v1 protocol freeze.** The current
implementation already conforms to the minimal semantic protocol. The protocol
model in this document **names and freezes** what already exists; it does not
require reshaping any working surface.

**Implementation-phase work (future, NOT this phase):**
1. Formalize Session contract type (UniClaw-side) — when buyer authorizes
2. Formalize protocol vocabulary names — when Protocol Consolidation implements
3. Resolve ISwitchStateReader 8 preconditions — when final-gate re-opens (NOT triggered by protocol freeze)
4. Realize non-terminal escalation transport — when supervisory-adjudication buyer authorizes (Reserved Extension)

---

## Deliverable 14 — Explicit Unresolved Questions Requiring Implementation Evidence

| # | Question | Why unresolved | What evidence is needed |
|---|---|---|---|
| Q-1 | Should `run.events.after` and `run.events.drain` be one semantic surface or two? | They are two wire methods but one semantic concept (event stream with cursor). Current: both exist, both used. | Implementation evidence: is any consumer using them differently, or can they merge in a future transport revision? |
| Q-2 | Is the `control.support` audit table a protocol contract or an implementation detail? | It is read-only audit data exposed via wire. Semantically it is a projection of "what controls are supported." | Implementation evidence: does any non-DSH consumer need it as a contract, or is it DSH-specific? |
| Q-3 | Should the RuntimeEvent 18-family vocabulary be formally named as protocol vocabulary or remain projection metadata? | Currently it is projection metadata (A/B/C classified). v1 Data Plane allows it as projection. | Implementation evidence: does any consumer need to reason about event kinds as protocol contract, or is projection sufficient? |
| Q-4 | Does the Session contract need a UniClaw-side type before a non-DSH host exists? | Currently DSH implements Session; UniClaw-side has no Session type. v1 allows this (DSH = impl). | Implementation evidence: is there a real non-DSH host buyer, or is formalizing premature? |
| Q-5 | Should the advice whitelist (re-observe/rebind/dismiss-obstruction) be a protocol contract or COMPOSITION_POLICY? | Currently it is enforced in `AssistancePendingRegistry` (transport-side). Semantically it is Brain advisory vocabulary. | Implementation evidence: should it be in the semantic contract (`AssistanceAdvice.Recommendation` constraint) or remain transport policy? |
| Q-6 | Should `maxIterations` (currently composition-side default 5) ever become a Directive field? | Currently not a wire field; composition-side default. v1 Directive is 4 fields. | Implementation evidence: does any real buyer need per-run iteration bounds over wire? |
| Q-7 | What transport shape will non-terminal escalation take when a real buyer authorizes it? | The semantic distinction (Escalation ≠ Terminal) is frozen (PI-9), but no transport exists. | Implementation evidence: what does the supervisory-adjudication buyer actually need (pause? redirect? decision-required signal?); must NOT be invented from imagination. |

**These questions do NOT block protocol model freeze.** They are recorded for
the implementation phase to resolve with evidence, not imagination.

---

## Scenario Validation (§7)

The proposed minimum contracts are tested against representative existing
runtime cases. Scenarios VALIDATE; they do not invent architecture.

| Scenario | Surface(s) exercised | Contract sufficient? | Evidence |
|---|---|---|---|
| Normal deterministic completion (SC-P1-001) | A (Directive) + B (Outcome: RunCompleted, GoalEvidence) | ✅ YES | Directive → RuntimeAgent closed loop → GoalEvidence → RunCompleted. No additional surface needed. |
| Uncertainty that can safely continue (post-action settle) | B (Outcome: events) + internal RuntimeAgent | ✅ YES | RuntimeAgent settles internally; no UniAgent message needed. Surface B carries the event stream. |
| Uncertainty requiring escalation (SemanticContradiction) | B (Outcome: RunFailed terminal) + C (Brain Capability via Hook Boundary, consulted pre-terminal) | ✅ YES | RuntimeAgent consults Capability Contract (Surface C) via Hook Boundary; if no actionable advice → fail-closed → RunFailed (Surface B terminal). **Note:** non-terminal escalation is semantically frozen (PI-9) but not yet transport-realized; current behavior (terminal) is a valid subset. |
| Delayed intelligence (L1 assistance consult) | C (Brain Capability Contract via Hook Boundary) + B (Outcome events) | ✅ YES | RuntimeAgent requests at adjudication point; receives advice; accepts/rejects/reconciles. One semantic path (not two surfaces). |
| Local recovery (Phase 2 Trap→Recovery) | B (Outcome: TrapRaised, RecoveryStarted) + internal | ✅ YES | RuntimeAgent owns recovery; Surface B carries Trap + recovery events. UniAgent observes, does not control. |
| Terminal failure (SC-P1-002/003/004) | B (Outcome: RunFailed + reason) | ✅ YES | RuntimeAgent-owned terminal with explicit reason. No fabricated completion. |
| Successful GoalEvidence verification (SC-P1-003) | B (Outcome: RunCompleted + GoalEvidence) | ✅ YES | GoalEvidence KERNEL_ONLY; RunCompleted event. UniAgent reads, does not re-originate. |
| Current Assistance L1 path (real model) | C (Brain Capability Contract via Hook Boundary: assistance.pending/resolve) + B (events) | ✅ YES | Transport realizes Surface C Brain contract via Hook Boundary; semantic contract (`IAssistanceProvider`) is transport-independent. ConsultRate=0 on real failures outside trigger surface = correct trigger quality. One semantic path. |

**Result: ALL 8 representative scenarios are sufficient under the proposed
minimum contracts.** No scenario requires a new surface, a new field, or a
Reserved Extension. The protocol model is scenario-validated. Non-terminal
escalation is semantically expressible (PI-9) but not required by any current
scenario's transport — current terminal-only behavior is a valid subset.

---

## PRE-FREEZE REPAIR SUMMARY

### Repair 1: Removed Capability-Hook / External-Hook semantic overlap

- **Before:** Surface C (Capability Hook) + Surface D (External Hook) — both
  expressed the same authority pattern; assistance described as "realizing both
  C and D."
- **After:** One Surface C (Capability Contract) + one cross-cutting Runtime
  External Hook Boundary (Deliverable 7). Assistance realizes the Brain
  Capability Contract via the Hook Boundary — one semantic path, not two
  surfaces.
- **Repository evidence for independent Surface D:** NONE found. The authority
  pattern, failure semantics, and RuntimeAgent ownership are identical for all
  capability invocations. No semantic distinction requires an independent peer
  surface.

### Repair 2: Escalation no longer equated with Terminal Outcome

- **Before:** "v1 minimum: no separate escalation channel. Escalation = terminal
  outcome with classified reason."
- **After:** Surface B distinguishes five architecturally-valid semantics
  (Result/Progress, Uncertainty, Escalation, Snapshot, Terminal). Escalation ≠
  TerminalOutcome (PI-9). Non-terminal escalation is
  `SEMANTICALLY_FROZEN_NOT_YET_REALIZED` — no transport invented, no
  pause/resume/redirect DTOs. Current implementation behavior (terminal-only)
  remains a valid subset.
- **Architecture v1 alignment:** v1 §3 permits escalation as a distinct
  RuntimeAgent→UniAgent semantic. The protocol model now reflects this without
  requiring new v1 transport.

### Repair 3: ISwitchStateReader reclassified

- **Before:** MISSING_CONTRACT (framed as a missing v1 semantic contract).
- **After:** DEFERRED_CAPABILITY_INSTANCE — an implementation-level deferred
  instance of the Vision Capability Contract (Surface C), NOT a missing v1
  protocol contract. The generic Vision/Perception capability boundary is
  already frozen. ISwitchStateReader is UNPURCHASED with 8 preconditions
  unresolved (final-gate intact). It does NOT become mandatory v1 implementation
  scope merely because protocol v1 is frozen.

### Recomputed sections (all updated consistently):

- Protocol Concept Map: 3 surfaces + 1 cross-cutting boundary (was 4 surfaces)
- Semantic Owner Matrix: non-terminal escalation row added; no D references
- Surface B: 5 semantic distinctions; escalation separated from terminal
- Surface C: merged D; per-capability APIs; ISwitchStateReader = DEFERRED_CAPABILITY_INSTANCE
- Deliverable 7: Hook Boundary (cross-cutting, was Surface D)
- Surface mapping: D removed; ISwitchStateReader reclassified; non-terminal escalation added
- Assistance decomposition: one semantic path (was two surfaces)
- Transport separation: Hook Boundary layer added
- Invariants: PI-9 (escalation≠terminal), PI-13 (boundary is cross-cutting), PI-23 (per-capability APIs) added; 23 total (was 20)
- Reserved Extensions: non-terminal escalation transport + additional invocation points added; 14 total (was 12)
- Migration impact: ISwitchStateReader = NONE (was LOW); non-terminal escalation = NONE
- Unresolved questions: Q-7 added (escalation transport shape); 7 total (was 6)
- Final summary: corrected counts

---

## VALIDATION

| # | Criterion | Result |
|---|---|---|
| 1 | Capability semantics and Hook mechanism are not duplicated | ✅ PASS — one Surface C + one cross-cutting Hook Boundary; no Surface D |
| 2 | Brain/Assistance has exactly one understandable semantic path | ✅ PASS — RuntimeAgent → Brain Capability Contract (Surface C) via Hook Boundary → advisory → accept/reject/reconcile |
| 3 | RuntimeAgent remains sole execution/world-truth authority | ✅ PASS — PI-4/PI-5/PI-12; all outcomes producer-derived; consumer never re-originate |
| 4 | Non-terminal supervision remains architecturally expressible without adding new v1 transport | ✅ PASS — PI-9 (Escalation ≠ Terminal, SEMANTICALLY_FROZEN_NOT_YET_REALIZED); no transport invented |
| 5 | Current working L1 Assistance behavior remains unchanged | ✅ PASS — no redesign; one semantic path named; transport realization unchanged |
| 6 | ISwitchStateReader does not become accidental v1 implementation scope | ✅ PASS — DEFERRED_CAPABILITY_INSTANCE; not triggered by protocol freeze; final-gate intact |
| 7 | No Reserved Extension was introduced | ✅ PASS — 14 reserved extensions listed; none designed/implemented; non-terminal escalation transport explicitly reserved |
| 8 | Architecture v1 remains unchanged | ✅ PASS — v1 document untouched; this design realizes v1, does not modify it |

---

## FINAL STATUS

```
PROTOCOL_V1_FROZEN
```

**Frozen by:** `PROJECT_LEADER_UNIAGENT_PROTOCOL_V1_FINAL_FREEZE` (2026-08-19)

### Frozen protocol topology

```
Surface A — Directive (UniAgent → RuntimeAgent)
    └─ [FROZEN: RunStartRequest 4-field; supervisory-alteration = Reserved Extension of Surface A]
Surface B — Outcome (RuntimeAgent → UniAgent)
    ├─ Result / Progress        [REALIZED]
    ├─ Uncertainty              [REALIZED internally]
    ├─ Escalation               [SEMANTICALLY_FROZEN_NOT_YET_REALIZED — ≠ TerminalOutcome]
    ├─ Snapshot                 [REALIZED]
    └─ TerminalOutcome          [REALIZED]
Surface C — Capability Contract (RuntimeAgent ↔ Brain/Vision/Memory/Operator/future)
    ├─ Brain                    [FROZEN: IAssistanceProvider — advisory only]
    ├─ Vision                   [DEFERRED_CAPABILITY_INSTANCE: ISwitchStateReader UNPURCHASED]
    ├─ Memory                   [NOT_PURCHASED — advisory only]
    └─ Operator                 [NOT_PURCHASED — human/supervisory/advisory input; NO execution authority]
Surface S — Session (correlation root)
    └─ [MISSING_CONTRACT: not formalized UniClaw-side; DSH impl compatible]

Runtime External Hook Boundary (cross-cutting, applies to all Surface C)
    └─ safe invocation · advisory-only · failure fail-closed · staleness · RuntimeAgent accept/reject/reconcile

Transport Boundary (replaceable implementation boundary)
    └─ current: loopback TCP newline JSON-RPC (NOT architecture)
```

### Frozen: 23 protocol invariants (PI-1 .. PI-23)

All 23 protocol invariants are frozen and coherent. Key preserved distinctions:
- **PI-9:** Escalation ≠ TerminalOutcome (non-terminal = SEMANTICALLY_FROZEN_NOT_YET_REALIZED)
- **PI-10/PI-12/PI-22:** RuntimeAgent sole execution/world-truth authority; capabilities advisory-only
- **PI-13:** Hook Boundary is cross-cutting, not a peer surface
- **PI-23:** Each capability defines its own request/response shape

### Preserved classifications

- **Escalation ≠ TerminalOutcome** — non-terminal escalation = `SEMANTICALLY_FROZEN_NOT_YET_REALIZED`
- **ISwitchStateReader** = `DEFERRED_CAPABILITY_INSTANCE` (UNPURCHASED; 8 preconditions; final-gate intact)
- **Session** = `MISSING_CONTRACT` (not formalized UniClaw-side; DSH impl compatible)
- **Supervisory strategy alteration** = Reserved Extension of Surface A (UniAgent → RuntimeAgent direction)

### Exact remaining implementation gaps

1. **Session contract (Surface S):** MISSING_CONTRACT — not formalized as a
   UniClaw-side type. DSH implementation is compatible. Future: formalize when
   a non-DSH host buyer or contract-freeze gate authorizes. NOT auto-purchased.
2. **Non-terminal escalation transport (Surface B Escalation):**
   SEMANTICALLY_FROZEN_NOT_YET_REALIZED — the semantic distinction is frozen
   (PI-9) but no transport exists. Future: Reserved Extension when a real
   supervisory-adjudication buyer authorizes. Must NOT be invented from
   imagination. NOT auto-purchased.
3. **ISwitchStateReader (Surface C Vision instance):**
   DEFERRED_CAPABILITY_INSTANCE — UNPURCHASED, 8 preconditions unresolved
   (final-gate intact). Future: resolve preconditions when a real Vision buyer
   authorizes. NOT triggered by protocol v1 freeze. NOT auto-purchased.

**No production code changes. No DTO implementation. No JSON-RPC migration.
No L2 Planning. No multi-agent. No multi-run. No typed hooks.**

Implementation does NOT begin automatically. Protocol v1 is frozen. A separate
implementation phase — authorized by a future real buyer — addresses the gaps
with evidence.
