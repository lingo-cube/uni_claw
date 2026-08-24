# UniAgent Architecture — Canonical Index

> Status: ACTIVE | Authority: `PROJECT_LEADER_UNIAGENT_GLOBAL_ARCHITECTURE_ALIGNMENT_AND_CLEANUP` + `PROJECT_LEADER_UNIAGENT_PROTOCOL_V1_FINAL_FREEZE`
> Established: 2026-08-19 | Protocol v1 frozen: 2026-08-19
> Rule: **There is exactly ONE active top-level architecture baseline and ONE
> active protocol baseline for this repository.**

---

## 1. Active Baseline Hierarchy

```
Architecture v1 (sole active top-level architecture baseline)
    ↓ governs
Protocol v1 (sole active semantic protocol baseline, subordinate to Architecture v1)
    ↓ governs
Subordinate layer docs (charter, contract, layers, patterns, guards)
    ↓ governs
Implementation (RuntimeAgent, DriverHost, DSH plugin, tests)
```

| Document | Role | Status |
|---|---|---|
| [`uniagent-architecture-v1-core-development-guide.md`](uniagent-architecture-v1-core-development-guide.md) | **UniAgent Architecture v1 — frozen top-level baseline** | FROZEN |
| [`uniagent-protocol-v1-consolidation-design.md`](uniagent-protocol-v1-consolidation-design.md) | **UniAgent Protocol v1 — frozen semantic protocol baseline (subordinate to Architecture v1)** | FROZEN |
| [`agent-concept-model-v1.md`](agent-concept-model-v1.md) | **Agent terminology, ownership, lifecycle, result/evaluation, evidence, and trace model (subordinate to both governing baselines)** | FROZEN SUBORDINATE |
| [`uniagent-decision-goal-evaluation-minimum-contract.md`](uniagent-decision-goal-evaluation-minimum-contract.md) | **Minimum producer/reference/append/supersession contract for UniAgent Decision and Goal Evaluation** | FROZEN SUBORDINATE CONTRACT |

**Architecture v1 is the sole active top-level architecture baseline.**
**Protocol v1 is the sole active semantic protocol baseline, subordinate to
Architecture v1.** Together they form the active baseline hierarchy.

Every other architecture/decision/design/protocol/assistance document in this
repository is either:
- a **subordinate layer** (RuntimeAgent-internal detail governed by v1), or
- a **historical record** (superseded decision / graduated capability evidence), or
- a **deferred design** (not yet active architecture/protocol), or
- an **audit/inventory artifact** (alignment-inventory, protocol-debt-inventory).

**No older protocol, assistance, or runtime document may be interpreted as a
parallel top-level protocol authority.** Historical documents may remain as
evidence. If a conflict is found, Architecture v1 + Protocol v1 win; the
conflicting document must be aligned or superseded.

---

## 2. Architecture v1 Invariants (frozen, 19)

1. UniAgent is the supervisory/orchestration agent.
2. RuntimeAgent is the bounded specialist execution agent.
3. RuntimeAgent owns bounded autonomy; UniAgent owns supervisory autonomy.
4. RuntimeAgent retains world-state, execution, grounding, verification, local recovery, and physical-safety authority.
5. UniAgent may alter supervisory strategy but may not directly overwrite Runtime belief/state or bypass fresh observation/grounding/verification.
6. Session is the mandatory collaboration/correlation root.
7. Session is NOT a message bus, Runtime state store, Event Store, or Agent.
8. Session history is append-oriented; mutable latest refs/summary are projections only.
9. UniAgent ↔ RuntimeAgent realtime interaction uses a stable semantic Runtime Protocol independently of Session.
10. Composition Host owns composition and entry only.
11. AgentHost owns Agent lifecycle only.
12. Brain, Vision, Memory and future extensions are independent Capabilities.
13. RuntimeAgent owns perception contract, not Vision implementation.
14. Brain is enhanced intelligence, not orchestration authority.
15. DSH is the preferred v1 implementation framework/host, not an architecture concept.
16. Metadata / Control / Data planes are system architecture concerns, not Agent Core modules.
17. External capabilities may only affect Runtime through safe protocol/hook boundaries and cannot acquire Runtime authority.
18. v1 defaults to 1 Session / 1 Primary Goal / 1 Primary Run.
19. Multi-agent, sub-run, branch-run, multi-run, typed hooks, dynamic grants, complex long-lived scheduling and complex recovery remain Reserved Extensions.

---

## 3. Subordinate Layer Documents (governed by v1, not independent baselines)

These documents describe the **RuntimeAgent-internal** architecture and are
correct and frozen **within the RuntimeAgent boundary**. They use the legacy
term "Agent" where Architecture v1 uses "RuntimeAgent"; the concept is the same.
They do NOT define the UniAgent / Session / Composition-Host / AgentHost layers
(those are defined only by Architecture v1).

| Document | v1 concept it details | Terminology note |
|---|---|---|
| [`docs/system/greenfield-runtime-charter.md`](../system/greenfield-runtime-charter.md) | RuntimeAgent complete behavior guide (60 sections) | "Agent" = RuntimeAgent |
| [`docs/system/constitution/runtime-architecture-contract.md`](../system/constitution/runtime-architecture-contract.md) | RuntimeAgent invariants I-1..I-14 (mechanically guarded) | "Agent" = RuntimeAgent |
| [`docs/system/layers/`](../system/layers/) | RuntimeAgent internal layer docs (agent/container/traversal/environment/planning) | "agent-runtime" = RuntimeAgent |
| [`docs/system/patterns/`](../system/patterns/) | RuntimeAgent internal patterns (FSM/belief/trap-recovery/observability/action-safety/AI-capability) | RuntimeAgent-internal |
| [`guards/`](guards/) | Mechanical guard docs (Guard 5 Trap boundary, Guard 7 Recovery dependency) | RuntimeAgent-internal enforcement |

**Authority relationship:** Architecture v1 §3 (Authority) and §6 (Runtime
External Boundary) govern the outer boundary; the charter/contract govern the
RuntimeAgent-internal boundary. Where the two layers meet, v1 is authoritative.

---

## 4. DSH Relationship (implementation, not architecture)

Per Architecture v1 invariant 15: **DSH is the preferred v1 implementation
framework/host, not an architecture concept.**

DSH currently implements: Composition Host, AgentHost, UniAgent hosting,
Capability hosting, Control/Data integration, and Operations UI. These are
**implementations of v1 roles**, not architecture definitions.

Repository DSH implementation surfaces (all subordinate to v1):
- `dsh-plugin-uniclaw/` — DSH-side plugin (commands, adapter, shadow, assistance bridge)
- `src/UniClaw.Runtime.DriverHost/` — Runtime-side external boundary (Runtime Protocol wire surface)
- Pinned DSH checkout `47f943859bef60e4160492346772ded9b24f765a` (`0.1.0-rc.5`)

No DSH-specific concept (Cordis, ctx.llm, session events, command registry) is
an architecture concept. They are implementation details of the v1 host.

---

## 5. Reserved Extensions (NOT introduced in v1)

The following are **explicitly reserved** and MUST NOT be designed/implemented
until a real buyer exists and a fresh gate authorizes them (per Architecture v1
invariant 19 + Protocol v1 Deliverable 12):

- Multi-agent, Sub Run, Branch Run, Multi-Run orchestration
- Dynamic Capability Grant, Typed Hook hierarchy
- Long-lived Agent Scheduling, Complex Recovery Workflow, Complex Completion Orchestration
- TaskSpec / BusinessIntent autonomous entry (deferred design only)
- IntelligenceSeam / IIntelligenceProvider production consumer (deferred design only)
- Mid-Run supervisory strategy alteration messages (Reserved Extension of Surface A — UniAgent → RuntimeAgent)
- Non-terminal escalation transport (semantic distinction frozen in PI-9; transport realization reserved)
- Additional Hook invocation points beyond belief adjudication
- pause/resume/stop/abort mutating controls
- Push-based event delivery (vs current polling)
- Persistent EvidenceRef resolution
- ISwitchStateReader freeze (DEFERRED_CAPABILITY_INSTANCE; 8 preconditions; final-gate intact)

---

## 6. Audit Inventories & Protocol Debt

- Architecture Alignment Inventory: [`alignment-inventory.md`](alignment-inventory.md)
- Protocol Debt Inventory: [`protocol-debt-inventory.md`](protocol-debt-inventory.md)

These are **audit/inventory artifacts**, not architecture or protocol baselines.
They were inputs to Protocol v1 consolidation and are retained as evidence.
Protocol v1 supersedes the Protocol Debt Inventory's dispositions as the
authoritative frozen protocol baseline.

---

## 7. Document Lifecycle Rules

- New top-level architecture statements: only by amending Architecture v1 (frozen gate).
- New protocol statements: only by amending Protocol v1 (frozen gate).
- New subordinate layer docs: must reference Architecture v1 + Protocol v1 as governing baselines.
- Historical decisions: supersede + archive (do not delete when they explain history).
- Obsolete exploratory docs with no continuing authority: archive or delete.
- Duplicated architecture/protocol descriptions: consolidate into v1 baselines or a subordinate doc.
- Terminology: use UniAgent / RuntimeAgent / Session / Composition Host / AgentHost per Architecture v1.
- No older protocol/assistance/runtime document may be interpreted as a parallel top-level protocol authority.

---

## 8. Context Loading Architecture

This section is a loading map only. It does not create architecture authority or
change the baseline hierarchy defined above.

For the default retrieval sequence, see the [Context Loading Guide](../context-loading-guide.md).

| Level | Default context | Loading rule |
|---|---|---|
| **0 — Architecture Constitution + Index** | This canonical index and the already-approved authority/ownership/truth/safety/completion/amendment rules | Always load the index. `AGENTS.md` remains a repository map, not an additional architecture baseline. |
| **1 — Architecture v1** | [`uniagent-architecture-v1-core-development-guide.md`](uniagent-architecture-v1-core-development-guide.md) | Always load for architecture work. It is the sole active top-level architecture baseline. |
| **2 — Protocol / Domain Contract** | [`uniagent-protocol-v1-consolidation-design.md`](uniagent-protocol-v1-consolidation-design.md) for external protocol work; [`runtime-architecture-contract.md`](../system/constitution/runtime-architecture-contract.md) plus the relevant charter/layer/pattern for RuntimeAgent-internal work | Load only the contract documents required by the task domain. |
| **3 — Current State Projection** | `current-architecture-state.md` and the relevant topic projection (`runtime.md`, `evidence.md`, `vision.md`, `dsh.md`, `governance.md`) | Projections summarize current state and carry no independent authority. |
| **4 — Active Gates** | [latest snapshot](../snapshots/latest.md), [current gates](../work/active/current-gates.md), and the directly relevant active OpenSpec proposal/design/spec/tasks | Load only changes directly relevant to the current task. Directory presence does not establish a current buyer. |
| **5 — Historical Retrieval** | `../decisions/index.md`, `../failures/index.md`, and `../../openspec/changes/archive/` | Retrieve by Decision ID, capability, scenario, gate, failure/falsifier, successor, or current reference. Do not load all historical Decisions by default. |

Projection documents MUST identify their canonical sources and MUST NOT add
invariants, SHALL requirements, gates, lifecycle transitions, or authority.
