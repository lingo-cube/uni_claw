# Architecture Alignment Inventory

> Produced by: `PROJECT_LEADER_UNIAGENT_GLOBAL_ARCHITECTURE_ALIGNMENT_AND_CLEANUP`
> Baseline: [UniAgent Architecture v1](uniagent-architecture-v1-core-development-guide.md) (sole active top-level baseline)
> Date: 2026-08-19
> Mode: AUDIT + CLASSIFICATION (no protocol redesign; no behavioral refactor)

This inventory maps every materially relevant architectural artifact in the
repository against Architecture v1 and classifies it. It does NOT refactor.

---

## Classification legend

| Class | Meaning |
|---|---|
| ALIGNED | Consistent with v1; no action needed |
| PARTIALLY_ALIGNED | Consistent in substance; terminology or framing needs normalization |
| LEGACY_BUT_USED | Uses pre-v1 terms but actively consumed; align terminology, do not remove |
| SUPERSEDED | Replaced by v1 or a later decision; mark successor + archive |
| CONFLICTING | Contradicts v1; must be aligned or superseded |
| ORPHANED | No active consumer; candidate for archive/delete |
| UNKNOWN | Insufficient evidence; record, do not act |

---

## A. Architecture documentation

| Artifact | v1 concept | Classification | Action | Note |
|---|---|---|---|---|
| `docs/architecture/uniagent-architecture-v1-core-development-guide.md` | THE baseline | ALIGNED | KEEP | Sole top-level baseline |
| `docs/architecture/README.md` (new) | canonical index | ALIGNED | KEEP | Establishes v1 as sole baseline |
| `docs/architecture/guards/guard-5-trap-boundary.md` | RuntimeAgent-internal enforcement | ALIGNED | KEEP | Subordinate layer |
| `docs/architecture/guards/guard-7-recovery-dependency-boundary.md` | RuntimeAgent-internal enforcement | ALIGNED | KEEP | Subordinate layer |

## B. docs/system (RuntimeAgent-internal layer docs)

| Artifact | v1 concept | Classification | Action | Note |
|---|---|---|---|---|
| `docs/system/greenfield-runtime-charter.md` | RuntimeAgent complete behavior guide | PARTIALLY_ALIGNED | ALIGN (terminology) | Uses "Agent" = RuntimeAgent; add v1 cross-ref, no content change |
| `docs/system/constitution/runtime-architecture-contract.md` | RuntimeAgent invariants I-1..I-14 | PARTIALLY_ALIGNED | ALIGN (terminology) | Uses "Agent" = RuntimeAgent; add v1 cross-ref |
| `docs/system/layers/agent-runtime.md` | RuntimeAgent internal layer | PARTIALLY_ALIGNED | ALIGN (terminology) | "agent-runtime" = RuntimeAgent |
| `docs/system/layers/container-runtime.md` | RuntimeAgent internal layer | ALIGNED | KEEP | |
| `docs/system/layers/traversal-runtime.md` | RuntimeAgent internal layer | ALIGNED | KEEP | |
| `docs/system/layers/environment-runtime.md` | RuntimeAgent internal layer | ALIGNED | KEEP | |
| `docs/system/layers/planning.md` | RuntimeAgent internal layer | ALIGNED | KEEP | |
| `docs/system/patterns/*` (6 files) | RuntimeAgent internal patterns | ALIGNED | KEEP | |
| `docs/system/engineering/*` (4 files) | Engineering guides | ALIGNED | KEEP | |
| `docs/system/reality-model-admission-contract.md` | Evidence admission governance | ALIGNED | KEEP | Subordinate governance |
| `docs/system/scenarios/` | Scenario specs | ALIGNED | KEEP | |
| `docs/system/README.md` | System docs index | PARTIALLY_ALIGNED | ALIGN (add v1 ref) | Should point to v1 as top-level |

## C. Decisions / ADRs (docs/decisions/)

| Artifact class | Count | v1 concept | Classification | Action | Note |
|---|---|---|---|---|---|
| Graduated capability decisions (phase1/phase2/u2/scroll/popup/perception/nav/dsh-*) | ~18 | Historical graduation evidence | SUPERSEDED (as architecture authority) / KEEP (as evidence) | KEEP + mark historical | They graduate RuntimeAgent-internal capabilities; not top-level architecture |
| `architecture-consolidation-decision.md` | 1 | Pre-v1 consolidation attempt | CONFLICTING (proposed parallel baseline) | SUPERSEDE + mark successor | Done: header now points to v1 |
| `outer-intelligence-integration-architecture.md` | 1 | Deferred design (IntelligenceSeam) | PARTIALLY_ALIGNED (framing) | ALIGN (add v1 note) | Done: marked DEFERRED / Reserved Extension |
| `runtime-dsh-architecture-gap-analysis.md` | 1 | Runtime↔DSH gap analysis | PARTIALLY_ALIGNED | ALIGN (terminology) | Frames DSH as "host"; add v1 cross-ref (DSH = impl, not architecture) |
| `post-lifecycle-cleanup-system-state-review.md` | 1 | System baseline review | LEGACY_BUT_USED | KEEP | Pre-v1 but still useful system map; add v1 cross-ref |
| `active-openspec-lifecycle-matrix.md` | 1 | OpenSpec lifecycle | ALIGNED | KEEP | |
| `semantic-agent-runtime-target-architecture-review.md` / `-current-state-review.md` | 2 | Pre-v1 architecture reviews | LEGACY_BUT_USED | KEEP | Historical context; not active baselines |
| `reality-model-admission-contract-gate.md` | 1 | Evidence admission gate | ALIGNED | KEEP | |
| `semantic-component-freeze-gate.md` | 1 | RuntimeAgent ownership freeze | ALIGNED | KEEP | Subordinate to v1 §3/§6 |
| `agent-capability-architecture-consolidation-gate.md` | 1 | L1/L2/L3 capability model | ALIGNED | KEEP | Compatible with v1 §5/§12 |
| `capability-module-architecture-final-gate.md` | 1 | Final gate (ISwitchStateReader) | ALIGNED | KEEP | v1 invariant 13 (perception contract, not impl) |
| `vision-deployment-identity-*` (admission gate + promotion) | 2 | Vision governance | ALIGNED | KEEP | v1 invariant 13/15 |
| `dsh-l1-runtime-environment-readiness.md` | 1 | DSH L1 readiness | ALIGNED | KEEP | v1 invariant 15 (DSH = impl host) |
| `l1-assistance-real-world-validation.md` | 1 | L1 operational validation | ALIGNED | KEEP | |
| Other gate/result/buyer/falsification records | ~140+ | Historical evidence | LEGACY_BUT_USED | KEEP | Explain history; not active architecture |

## D. OpenSpec active changes

| Change | v1 concept | Classification | Action | Note |
|---|---|---|---|---|
| `greenfield-agent-runtime` | RuntimeAgent foundation | ALIGNED | KEEP | LONG_LIVED_BASELINE |
| `runtime-external-contract-baseline` | Runtime Protocol surface (5-plane) | ALIGNED | KEEP | Maps to v1 §6/§9 |
| `runtime-assistance-seam` | RuntimeAgent assistance hook | ALIGNED | KEEP | v1 §6 (safe protocol/hook) |
| `dsh-assistance-provider-adapter` | DSH-side assistance impl | ALIGNED | KEEP | v1 invariant 15/17 |
| `dsh-runtime-agent-subagent-run-entry` | Runtime Protocol run.start | ALIGNED | KEEP | v1 §6/§9 |
| `vision-runtime-bootstrap` | Vision capability hosting | ALIGNED | KEEP | v1 invariant 12/13 |
| `post-action-state-settle` | RuntimeAgent verification mechanics | ALIGNED | KEEP | RuntimeAgent-internal |
| `verified-local-continuity` | RuntimeAgent verification mechanics | ALIGNED | KEEP | RuntimeAgent-internal |
| `settings-full-tree-enumeration-integration` | RuntimeAgent traversal | ALIGNED | KEEP | RuntimeAgent-internal |
| `trace-capture-scenario-catalog-foundation` | Scenario catalog infrastructure | ALIGNED | KEEP | DEFERRED_NO_BUYER |

## E. OpenSpec specs (synced main specs, openspec/specs/)

| Spec | v1 concept | Classification | Action |
|---|---|---|---|
| `run-lifecycle`, `normal-wifi-scenario`, `container-traversal`, `environment` | RuntimeAgent core | ALIGNED | KEEP |
| `agent-recovery`, `recovery-verification`, `trap-recovery` (patterns) | RuntimeAgent recovery | ALIGNED | KEEP |
| `bounded-candidate-safety`, `bounded-cross-page-discovery`, `discovered-branch-effect-revalidation` | RuntimeAgent open-world | ALIGNED | KEEP |
| `popup-local-recovery`, `recovery-progress-resume`, `viewport-*` | RuntimeAgent Phase3 | ALIGNED | KEEP |
| `dsh-control-plane`, `dsh-shadow-cognition`, `dsh-uniclaw-control-plane-plugin-implementation` | DSH integration | ALIGNED | KEEP (DSH = impl) |
| `hierarchical-trace-projection`, `runtime-activity-emission` | Observability | ALIGNED | KEEP |
| `perception-actionable-toggle-evidence*`, `physical-scroll-*` | Perception/scroll | ALIGNED | KEEP |
| `open-world-traversal-identity-safety` | RuntimeAgent identity | ALIGNED | KEEP |

## F. RuntimeAgent / Runtime implementation (src/UniClaw.Runtime/)

| Artifact | v1 concept | Classification | Action | Note |
|---|---|---|---|---|
| `Agent/` (Agent.cs, SemanticRun, PlanRun, Recovery, OpenWorld, ActionAuthorizer) | RuntimeAgent core | PARTIALLY_ALIGNED | ALIGN (terminology only, no rename) | Class name `Agent` = RuntimeAgent; do NOT rename code (behavioral risk) |
| `Container/`, `Traversal/`, `Environment/`, `Recovery/`, `World/`, `Planning/`, `Model/`, `Observability/`, `Startup/`, `Memory/` | RuntimeAgent internal layers | ALIGNED | KEEP | Per charter/contract |
| `Capabilities/Brain/IAssistanceProvider.cs` | Brain capability hook | ALIGNED | KEEP | v1 §5/§6 (safe hook, advice-only) |
| `Capabilities/Perception/Vision/ISwitchStateReader.cs` | Perception contract | PARTIALLY_ALIGNED | KEEP (UNPURCHASED candidate) | v1 invariant 13; final-gate not passed |
| `Capabilities/Operator/` (empty) | — | ORPHANED | DEFER (no active consumer; do not delete without evidence) | Empty dir; no code |
| `src/UniClaw.Runtime.csproj` zero ProjectReference | RuntimeAgent isolation | ALIGNED | KEEP | Guard 1 |

## G. DriverHost / external boundary

| Artifact | v1 concept | Classification | Action |
|---|---|---|---|
| `Transport/` (wire codec, contract, server) | Runtime Protocol wire surface | ALIGNED | KEEP |
| `Control/` (control support audit) | Control plane | ALIGNED | KEEP |
| `Assistance/` (pending registry, wire provider, wire contract) | Assistance protocol | ALIGNED | KEEP |
| `Execution/` (run coordinator, run start) | Runtime Protocol run.start | ALIGNED | KEEP |
| `Projection/`, `Store/` | Observability/Data plane | ALIGNED | KEEP |
| `Model/` | DTOs | ALIGNED | KEEP |
| Guard 10a/10b/10c/10d (no Runtime→DSH reverse dep) | v1 invariant 17 | ALIGNED | KEEP |

## H. DSH / dsh-plugin-uniclaw integration

| Artifact | v1 concept | Classification | Action | Note |
|---|---|---|---|---|
| `dsh-plugin-uniclaw/src/plugin.js` | DSH plugin entry | ALIGNED | KEEP | v1 invariant 15 (impl) |
| `dsh-plugin-uniclaw/src/adapter.js` | Runtime Protocol client | ALIGNED | KEEP | |
| `dsh-plugin-uniclaw/src/commands.js` | DSH commands | ALIGNED | KEEP | Read-only + run.start |
| `dsh-plugin-uniclaw/src/protocol.js` | Wire protocol | ALIGNED | KEEP | |
| `dsh-plugin-uniclaw/src/assistance/` (bridge, consumer, llm-consumer) | Assistance bridge | ALIGNED | KEEP | v1 §6 (safe hook) |
| `dsh-plugin-uniclaw/src/shadow/` | Shadow cognition | ALIGNED | KEEP | v1 invariant 14 (Brain = enhanced intelligence, not authority) |
| `src/UniClaw.Runtime.PhysicalHost/` | Composition Host | ALIGNED | KEEP | v1 §7 |
| `src/UniClaw.Runtime.Harness/` | Test/diagnostic harness | ALIGNED | KEEP | |
| `src/UniClaw.Runtime.Adapters/` | Environment adapters | ALIGNED | KEEP | v1 invariant 13 |
| `src/UniClaw.Runtime.Vision.Host/` | Vision implementation | ALIGNED | KEEP | v1 invariant 12/13 (impl, not contract) |

## I. Session-related code/docs

| Artifact | v1 concept | Classification | Action | Note |
|---|---|---|---|---|
| `src/UniClaw.Runtime.Harness/TraceCaptureSession.cs` | Trace capture session (diagnostic) | ALIGNED | KEEP | NOT the v1 Session (correlation root); naming overlap only — no conflict |
| DSH-side `Session` (pinned checkout) | v1 Session correlation root | ALIGNED | KEEP | Implemented in DSH (v1 invariant 15) |
| No `class Session` in RuntimeAgent | Correct — Session is not RuntimeAgent-internal | ALIGNED | KEEP | v1 invariant 6/7 |

## J. Protocol surfaces (detailed in protocol-debt-inventory.md)

| Surface | v1 concept | Classification | Action |
|---|---|---|---|
| `run.*` (list/snapshot/trap/events/lastart) | Runtime Protocol | ALIGNED → DEFER_TO_PROTOCOL_CONSOLIDATION | Record debt |
| `assistance.*` (pending/resolve) | Runtime Protocol assistance hook | ALIGNED → DEFER_TO_PROTOCOL_CONSOLIDATION | Record debt |
| `evidence.get` | Runtime Protocol data | ALIGNED → DEFER_TO_PROTOCOL_CONSOLIDATION | Record debt |
| `control.support` | Control plane | ALIGNED → DEFER_TO_PROTOCOL_CONSOLIDATION | Record debt |
| observation/evidence (RuntimeAgent-internal) | RuntimeAgent-internal models | ALIGNED | KEEP |
| Runtime ↔ DSH/Host (loopback TCP JSON-RPC) | Runtime Protocol transport | ALIGNED | KEEP |
| Capability integration paths (IAssistanceProvider, ISwitchStateReader) | Safe hooks | ALIGNED → DEFER (ISwitchStateReader UNPURCHASED) | Record debt |

## K. Tests and test hosts

| Artifact | v1 concept | Classification | Action |
|---|---|---|---|
| `tests/UniClaw.Runtime.Tests/Architecture/` | Guard tests | ALIGNED | KEEP |
| `tests/UniClaw.Runtime.Tests/Scenario/` | Scenario tests | ALIGNED | KEEP |
| `tests/UniClaw.Runtime.Tests/DriverHost/` | Protocol tests | ALIGNED | KEEP |
| `tests/UniClaw.Runtime.Tests/Composition/`, `PhysicalHost/` | Composition tests | ALIGNED | KEEP |
| `tests/UniClaw.Runtime.Tests/Perception/`, `Vision/` | Perception tests | ALIGNED | KEEP |
| `tests/UniClaw.Runtime.Tests/Capabilities/` | Capability tests | ALIGNED | KEEP |
| `tests/UniClaw.Runtime.Tests/Replay/`, `Integration/`, `Observability/`, `Unit/` | Other tests | ALIGNED | KEEP |

## L. AGENTS / README / indexes

| Artifact | v1 concept | Classification | Action | Note |
|---|---|---|---|---|
| `AGENTS.md` | Project entry map | ALIGNED (updated) | KEEP | Now references v1 as top-level baseline |
| `CLAUDE.md` | Claude adapter | ALIGNED | KEEP | References AGENTS.md |
| `src/UniClaw.Runtime/AGENTS.md` | Build-zone map | PARTIALLY_ALIGNED | ALIGN (add v1 cross-ref) | RuntimeAgent-internal map; add v1 pointer |
| `docs/system/README.md` | System docs index | PARTIALLY_ALIGNED | ALIGN (add v1 ref) | Should point to v1 as top-level |

---

## Summary counts

| Classification | Count (material artifacts) |
|---|---|
| ALIGNED | ~95% of artifacts |
| PARTIALLY_ALIGNED | ~12 (terminology: "Agent"→RuntimeAgent framing; DSH framing) |
| LEGACY_BUT_USED | ~145 historical decision records |
| SUPERSEDED | 1 (`architecture-consolidation-decision.md` as baseline) |
| CONFLICTING | 1 (resolved: `architecture-consolidation-decision.md` superseded) |
| ORPHANED | 1 (`Capabilities/Operator/` empty dir — deferred, not deleted) |
| UNKNOWN | 0 |

## Conflicts repaired (this cleanup)

1. `architecture-consolidation-decision.md` — superseded as top-level baseline; header now points to v1.
2. `outer-intelligence-integration-architecture.md` — aligned: marked DEFERRED / Reserved Extension; DSH framing clarified as implementation.
3. `AGENTS.md` — aligned: now references v1 as sole top-level baseline.
4. `docs/architecture/README.md` — created: canonical index establishing v1 as sole baseline.

## Conflicts NOT repaired (require terminology normalization only, no behavioral change)

These are PARTIALLY_ALIGNED terminology items where the legacy term "Agent" is
used where v1 says "RuntimeAgent". **No code rename is performed** (behavioral
risk; not authorized in this bounded cleanup). These are recorded as alignment
debt for a future documentation pass:

- `greenfield-runtime-charter.md` uses "Agent" = RuntimeAgent
- `runtime-architecture-contract.md` uses "Agent" = RuntimeAgent
- `src/UniClaw.Runtime/Agent/` class names use `Agent` = RuntimeAgent
- `docs/system/layers/agent-runtime.md` uses "agent-runtime" = RuntimeAgent

**Rule:** These are correct within the RuntimeAgent-internal boundary. v1 uses
"RuntimeAgent" at the top level to distinguish from "UniAgent". The internal
docs may continue using "Agent" as long as v1 is referenced as the governing
top-level baseline. Cross-references to v1 should be added but are not blocking.
