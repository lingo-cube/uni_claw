# ARCHITECTURE_CONSOLIDATION_DECISION

> ⚠️ **SUPERSEDED** — This document was a pre-v1 architecture consolidation
> attempt. It is **superseded as the top-level architecture baseline** by:
> [`docs/architecture/uniagent-architecture-v1-core-development-guide.md`](../../architecture/uniagent-architecture-v1-core-development-guide.md)
> (UniAgent Architecture v1, frozen).
>
> **SupersededBy:** UniAgent Architecture v1 (sole active top-level baseline)
> **Reason:** Architecture v1 was frozen as the sole top-level baseline by
> `PROJECT_LEADER_UNIAGENT_GLOBAL_ARCHITECTURE_ALIGNMENT_AND_CLEANUP`. This
> document's proposal to create `docs/architecture/baseline.md` as "the
> canonical architecture reference" conflicts with v1 being the sole baseline.
> **CurrentState:** Retained as a **historical inventory** of graduated
> decisions / active work / failure catalog. Its factual content (decision
> list, failure catalog) remains useful as reference, but it is NOT an
> architecture baseline and its TASK-DOC-* tasks are NOT authorized for
> execution (superseded by the v1 alignment cleanup).
> **Migration:** Architecture baseline → v1 doc + `docs/architecture/README.md`
> index. Decision/failure inventory → retained here as historical reference.

> Authority: GLM-5.2 Project Leader — ARCHITECTURE_CONSOLIDATION_REVIEW
> Mode: ANALYSIS / DECISION / TASK_DECOMPOSITION ONLY
> Date: 2026-08-19 (review snapshot)
> Head: `203cf83` (uni-agent branch)
> Constraints honored:
> - NO code modification; NO cleanup execution; NO architecture refactor.
> - Fact-first: derived only from graduated Gate Results, submitted Decisions, verified test results, and current code facts.
> - Decision First: every change must follow Observation → Decision → Implementation → Verification → Graduation. Executor must not originate architecture decisions.
> - Authority Boundary frozen; no Runtime/Fixture/GoalEvidence authority drift.
> - NO semantic cleanup at this stage — only documentation consolidation, Decision archiving, Index establishment, Architecture Baseline establishment, Governance consolidation.

---

## 1. Current Architecture Baseline

Frozen from historical graduated Decisions and current code facts.

### Runtime

- **Spine (frozen, I-1):** `Agent → Container → Traversal → Environment`. Reverse dependency forbidden.
- **Lifecycle core loop:** Observe → Reconcile → Decide → Execute → Observe → Verify → Update → Continue (charter §5; `Agent.SemanticRun.cs`).
- **Recovery path:** Trap → Determine Scope → Recovery → Observe → Verify → Reconcile → Resume (Phase 2 graduated).
- **Open-world path:** `IntentExecution.RunOpenWorldAsync` → `Agent.RunOpenWorldAsync` (bounded DFS, verified parent-return, sibling continuation).
- **Runtime maturity (current published state):**
  - `PHASE1_DETERMINISTIC_RUNTIME_BASELINE_GRADUATED` (Fake Environment; SC-P1-001..005)
  - `PHASE2_DETERMINISTIC_TRAP_RECOVERY_BASELINE_GRADUATED` (Trap + Agent-scope Recovery + Step-scope retry + Recovery verification; SC-P2-001..003)
  - `U2_BOUNDED_OPEN_WORLD_SETTINGS_TRAVERSAL_GRADUATED` (bounded runtime-discovered type-level traversal; SC-U2-MUS-001)
  - `OPEN_WORLD_TRAVERSAL_IDENTITY_SAFE` (cycle/duplicate fail-closed identity safety)
  - `OPEN_WORLD_CONTAINER_INVENTORY_COMPLETE` (bounded runtime inventory completeness discovery; COMPOSE-05 capstone)
  - `PHYSICAL_SCROLL_SEMANTIC_MECHANISM_DETERMINISTICALLY_VERIFIED` (F5 + DEFERRED_BOUNDED timing; deterministic only)
  - `SEMANTIC_RUN_POPUP_OBSTRUCTION_HANDLED` (SemanticRun local obstruction dispatch+verify+continue)
  - `PERCEPTION_ACTIONABLE_TOGGLE_REALITY_REPAIR_INTEGRATED` + `PERCEPTION_ACTIONABLE_TOGGLE_EVIDENCE_INTEGRATED` (raw-pixel toggle candidate recovery; Binding/StateBelief non-regression)
  - `SEMANTIC_RUN_UNEXPECTED_NAVIGATION_RECONCILED` (known-page transition reconciliation; fail-closed unknown)
  - `SETTINGS_NAVIGATION_CANDIDATE_EVIDENCE_BASELINE` (UiAutomator hierarchy source; NAVIGATION_CANDIDATE/LOCAL_CONTROL/UNKNOWN classification)
  - 10 Phase 3 capability SCs frozen + archived (uncertain-action, popup-local-recovery, scroll-identity, sibling-branch-progress, recovery-progress-resume, bounded-candidate-safety, viewport-exploration, bounded-cross-page-discovery, discovered-branch-effect-revalidation, s0-capstone)
  - Physical WiFi semantic loop: `EMULATOR_REALITY_END_TO_END_SEMANTIC_LOOP` + `EMULATOR_REALITY_MULTI_LEVEL_SEMANTIC_LOOP` (emulator-only §33; not real-device-proven)
- **Post-action state settle:** `post-action-state-settle` implemented (APPLY executed 2026-08-17; D.HYBRID, bounded re-observe, observation-scoped target identity).
- **Verified local continuity:** `verified-local-continuity` implemented (APPLY executed 2026-08-18; LOCAL_IDENTITY supports when Agent independently verified; eliminates FALSE_SEMANTIC_CONTRADICTION).
- **Architecture Contract invariants:** I-1..I-14 frozen and mechanically guarded (`ArchitectureGuardTests.cs`).
- **FSM boundary (I-7):** Phase 1/2 expressed as plain methods; no FSM introduced. Phase 3 viewport/candidate-safety etc. carry bounded mechanism state, never competing semantic authority.

### Agent

- **Sole semantic decision authority** (I-3): RunState lifecycle, capability selection, action authorization, goal-satisfaction adjudication, recovery orchestration.
- **Sole Run termination authority** (I-10): only satisfied `GoalEvidence` completes; Plan-exhaustion ≠ Completed; dispatch ≠ success.
- **GoalEvidence authority:** `KERNEL_ONLY` (frozen OBS-F9 language). Evaluator is caller-injected; reports evidence only; Agent consumes.
- **Escalation boundary (I-8):** low-level detects failure and returns `TraversalStepResult.Failed` / emits `Trap`; Container hands off read-only; Agent decides.
- **Open-world authority:** Agent derives inventory from fresh evidence, authorizes selection, derives `VerifiedBoundedTraversalCompletion`, consumes `GoalEvidence`.
- **Assistance seam (implemented, model-free):** `IAssistanceProvider?` optional ctor param; advice-mode consumption at Contradicted/Unresolved adjudication points; `MaxAssistanceConsults=3` budget; world-version staleness binding. Advice never writes belief/binding/state/truth/completion.
- **Agent code surface (current):** `Agent.cs`, `Agent.SemanticRun.cs`, `Agent.PlanRun.cs`, `Agent.Recovery.cs`, `Agent.OpenWorld.cs`, `ActionAuthorizer.cs`, `SourceGroundingValidator.cs`.

### Evidence

Evidence authority chain (frozen, cannot be Runtime-changed):
```
Raw Evidence → Structured Evidence → Semantic Evidence → Goal Evidence
```

- **Raw Evidence:** Observation (`IEnvironment.ObserveAsync`); Perception candidates (Python fusion → `switch`/`toggle` type + bounds); UiAutomator hierarchy (`StructuredElements`).
- **Structured Evidence:** `SemanticEvidence`, `ObjectBinding`, `BindingReconciler` proposals, `StateBeliefReducer` proposals, `PageAnalysis` evidence, `InteractionAffordanceEvidence`, `StructuredElementEvidence`.
- **Semantic Evidence:** `WorldBelief` (Reconcile.FromObservation; no scenario fields), `Container.ObjectStateBeliefs` (mutable, Container-owned), `BranchProgressEvidence`, `BranchInventoryEvidence`, `CandidateAuthorizationEvidence`, `ViewportExplorationEvidence`, `ContainerInventoryCompletenessEvidence`.
- **Goal Evidence:** `GoalEvidence(Satisfied, Reason, SourceObservationSequence)` — KERNEL_ONLY authority; evaluator reports evidence, Agent decides completion.
- **Source Identity model:** `NavigationSourceOccurrence` / `NavigationSourceOccurrenceReference` / `ProvenLogicalSource` / `BranchSourceGroundingEvidence` / `SourceEquivalenceEvidence` — `DISCOVERED != GROUNDED != CURRENTLY_VISIBLE != AUTHORIZED != VISITED != COMPLETED`; `TitleText != Identity`; `DispatchReceipt != WorldTruth`.
- **Container Completeness model:** `ContainerInventoryCompletenessEvidence` + `ProvenLogicalSources` (frozen discovery epoch); `PostCompletenessConsistencyValidator` for post-completion non-monotonic evidence; NOT universal Settings-tree enumeration.
- **Fresh Observation model:** every post-action step re-observes (`ObserveAsync`); `TraversalStepResult` requires `fresh.SequenceNumber > observation.SequenceNumber`; stale-frame fail-closed (`SwitchStateValidation`); `SourceObservationSequence` must point at fresh observation.
- **Trace authority (OBSERVATIONAL):** `TraceEvent` carries `TrapKind?`/`TrapScope?`/`RecoveryId?` as observational records; `RuntimeEvent.Sequence` ≠ `ObservationSequence` (independent domains, OBS-F9 frozen).

### Vision

- **Vision host lifecycle owner:** PhysicalHost application/composition root (`VisionRuntimeBootstrap` managed startup; `host.SocketPath` single endpoint source; `VisionReady` readiness contract).
- **Perception authority split (frozen):**
  - Candidate type/bounds: Python Perception (`heuristics.py` / `_run_pipeline`); canonical `switch` → Runtime `PerceptionType = "toggle"`.
  - Visual state (ON/OFF/UNKNOWN): C# `ImageSwitchStateProvider` (SOLE authority; `ClassifySwitchRegion`).
  - Association: `BindingAnalysis` + `BindingReconciler`.
  - Belief: `StateBeliefReducer`.
  - Decision: Agent.
  - GoalEvidence: Kernel (frozen OBS-F9).
- **Python `switch_state` = NON_AUTHORITATIVE** (bridge does not emit; test does not consume).
- **Deployment identity governance:** 5-axis (`schemaVersion`/`modelId`/`configId`/`pipelineRevision`/`deploymentId`); `build_active_identity.py` is mechanical writer; **admission is HUMAN/PROJECT_LEADER gate** (not script, not test, not bootstrap). Current candidate `pipelineRevision` drifted vs admitted receipt (committed source change) — **B1b BLOCKED (stale admission)**.
- **Vision identity governance boundary:** candidate ≠ admitted; runtime/bootstrap has ZERO admission authority; tests do not mutate governance to pass; verifier untouched.
- **`ISwitchStateReader`:** `UNPURCHASED_L2_CONTRACT_CANDIDATE` (safe result semantics, but contract lifetime/provider/composition/authority unresolved — final-gate NOT passed).

### DSH

- **Control-plane plugin integration (graduated):** real pinned DSH host (`47f943859bef60e4160492346772ded9b24f765a`, `0.1.0-rc.5`) → native plugin loader → UniClaw plugin → commands registry → deterministic read/inspect commands → UniClaw service → adapter → DriverHost. 6 registered commands (`uniclaw-evidence-open`, `uniclaw-inspect-run`, `uniclaw-inspect-trap`, `uniclaw-runs-list`, `uniclaw-shadow-analyze`, `uniclaw-events-after`); ZERO mutating commands.
- **Read-only Kernel observability (graduated):** `RuntimeEvent` + `RunSnapshot` + `EvidenceRef`; `OBS-F9` semantic domain separation frozen (`RuntimeEvent.Sequence` ≠ `ObservationSequence`).
- **Shadow cognition (graduated, EPHEMERAL):** human-request-only; ZERO custom session events; process-local bounded cache (max 20); F1–F16 all PASS; authority firewall = Kernel consumes ZERO Shadow output; `GoalEvidenceAuthority = KERNEL_ONLY`.
- **Control-plane event stream (graduated):** `run.events.after` (frozen wire method); cursor = EXCLUSIVE_SEQ_GREATER_THAN; 2000ms bounded polling; `eventId` dedupe; run isolation; zero model.
- **Assistance provider adapter (implemented, model-free):** `AssistancePendingRegistry` (capacity 8) + `AssistanceWireProvider` + `assistance.pending`/`assistance.resolve` wire (additive); plugin `AssistanceBridge` (provider-agnostic) + `DeterministicAssistanceConsumer` (replaceable); frozen 9-method wire table preserved (now 8 + run.start + assistance.pending/resolve).
- **Runtime run entry (implemented):** `run.start` wire method + `RunExecutionCoordinator` (DriverHost-owned runId, `ONE_ACTIVE_RUN_PER_DEVICE`); `uniclaw-run-goal` command; zero model calls in control path; no Agent→DSH/plugin dependency.
- **Transport:** ONE loopback TCP newline JSON-RPC (127.0.0.1:5177) between DSH plugin (client) and UniClaw DriverHost (listener).
- **External contract baseline:** 5-plane taxonomy (Goal/Data IMPLEMENTED; Assistance/Guidance/Execution Handoff DEFERRED); frozen 8 read-only methods + run.start; deferred planes have NO frozen wire format.
- **Outer Intelligence Integration (DESIGN ONLY, NOT IMPLEMENTED):** `IIntelligenceProvider` seam at Agent adjudication points; `TaskSpec`/`AgentProfile` contracts; `intelligence.consult`/`perception.ask`/`escalation.*` wire family proposed; pending OpenSpec propose (`dsh-outer-intelligence` / `kernel-intelligence-seam`).

### Governance

- **OpenSpec is system of record:** `openspec/changes/` for active, `openspec/changes/archive/` (30 archived) for graduated.
- **Active changes (11 directories):**
  1. `greenfield-agent-runtime` — LONG_LIVED_BASELINE_BY_DESIGN (9/9 tasks)
  2. `open-world-container-inventory-completeness` — PROPOSED_NO_CURRENT_BUYER (graduated+archived 2026-08-17 but directory entry remains per lifecycle matrix update 7; see §3 note)
  3. `trace-capture-scenario-catalog-foundation` — PROPOSED_NO_CURRENT_BUYER (proposal only, 0/0 tasks)
  4. `settings-full-tree-enumeration-integration` — ACTIVE_WORK (5 phases, 0 tasks checked; first-failure classification A–H)
  5. `post-action-state-settle` — IMPLEMENTED (APPLY executed 2026-08-17; 17/17 tests)
  6. `verified-local-continuity` — IMPLEMENTED (APPLY executed 2026-08-18; 15/15 tests)
  7. `runtime-assistance-seam` — IMPLEMENTED (APPLY executed 2026-08-17; 7/7 tests)
  8. `dsh-assistance-provider-adapter` — IMPLEMENTED (APPLY executed 2026-08-17; model-free)
  9. `runtime-external-contract-baseline` — DOCUMENTATION-ONLY (8/8 slices)
  10. `vision-runtime-bootstrap` — IMPLEMENTED (APPLY executed 2026-08-17; B1a RESOLVED, B1b BLOCKED)
  11. `dsh-runtime-agent-subagent-run-entry` — IMPLEMENTED (T1–T12 all pass; F1–F10 all pass)
- **Mechanical guards:** `ArchitectureGuardTests.cs` (Guard 1: zero ProjectReference; Guard 2: no legacy namespaces; Guard 3: contract docs exist; Guard 5/5b: Trap/RecoveryRequest boundaries; Guard 6: no coordinate/hierarchy; Guard 7: Recovery dependency direction; Guard 10a/10b/10c/10d: Kernel-DSH isolation).
- **Consistency checks:** `scripts/check-consistency.sh` (C1–C10: charter 60 sections, contract 14 invariants, navigation completeness).
- **Reality Model Admission Contract:** 26 sections; E4–E0 evidence-strength taxonomy; G1–G6 gates; 8 admission outcomes; 16-field canonical schema; HUMAN_ADOPTED 2026-08-09.
- **Agent Capability Architecture:** `THREE_LEVEL_CAPABILITY_MODEL_FROZEN` (L1 facade / L2 contract / L3 provider); `VISION = CONCEPT_ONLY`; `BRAIN = CONCEPT_ONLY`; `OPERATOR = NOT_JUSTIFIED`; `STATE_CLASSIFIER = DEFERRED_NOT_IMPLEMENTED`; `ISWITCHSTATEREADER = UNPURCHASED_L2_CONTRACT_CANDIDATE`.
- **Semantic Component Freeze:** 6 ownership rules + 1 dependency direction + 17 responsibility names + 7 naming principles + 3 forbidden coupling categories + 5 extension points. FREEZE_READY.

---

## 2. Frozen Decisions (GRADUATED_ARCHITECTURE)

Condition: verified, current architecture depends on it, no longer changes.

| ID | Decision | Status | Evidence |
|---|---|---|---|
| D-PHASE1 | `GRADUATE_PHASE1_DETERMINISTIC_RUNTIME` | GRADUATED (2026-08-16) | SC-P1-001..005 (8/6/10/4/3 tests); 47/47 targeted; 8/8 guards; Fake Environment; `phase1-deterministic-runtime-graduation-decision.md` |
| D-PHASE2 | `GRADUATE_PHASE2_TRAP_RECOVERY` | GRADUATED (2026-08-16) | SC-P2-001..003 (3/2/2 tests); 35/35 targeted; Guard 7; Trap=7 fields; `phase2-trap-recovery-graduation-decision.md` |
| D-U2 | `GRADUATE_U2_OPEN_WORLD_SETTINGS_TRAVERSAL` | GRADUATED (2026-08-16) | SC-U2-MUS-001; 14 tests; 65/65 targeted; `IntentExecution.RunOpenWorldAsync`; `u2-open-world-settings-traversal-graduation-decision.md` |
| D-OWIS | `OPEN_WORLD_TRAVERSAL_IDENTITY_SAFE` | GRADUATED (2026-08-16) | Cycle/duplicate fail-closed; run-local identity; `open-world-traversal-identity-safety-graduation-decision.md` |
| D-OWIC | `OPEN_WORLD_CONTAINER_INVENTORY_COMPLETE` | GRADUATED (2026-08-17) | 19-point claim; COMPOSE-05 capstone (8/8 children); 1164/1164 deterministic; `open-world-container-inventory-completeness-graduation.md` |
| D-SCROLL | `PHYSICAL_SCROLL_SEMANTIC_MECHANISM_DETERMINISTICALLY_VERIFIED` | GRADUATED (2026-08-15) | F5 + DEFERRED_BOUNDED; 25/25 targeted; 1004/1004; `physical-scroll-container-semantic-traversal-graduation-decision.md` |
| D-POPUP | `SEMANTIC_RUN_POPUP_OBSTRUCTION_HANDLED` | GRADUATED | 37/37 targeted; `TryHandleLocalObstructionAsync`; ArchitectureDelta=NONE; `semantic-run-popup-obstruction-graduation-decision.md` |
| D-PERCEPT-REPAIR | `PERCEPTION_ACTIONABLE_TOGGLE_REALITY_REPAIR_INTEGRATED` | GRADUATED (2026-08-16) | Raw-pixel toggle recovery; 13/13 PER-R; 55/55 Python; `perception-actionable-toggle-evidence-reality-repair-graduation-decision.md` |
| D-PERCEPT-PARENT | `PERCEPTION_ACTIONABLE_TOGGLE_EVIDENCE_INTEGRATED` | GRADUATED (2026-08-16) | Parent integration; 45/45 tasks; 26/26 targeted; `perception-actionable-toggle-evidence-graduation-decision.md` |
| D-NAV-RECON | `SEMANTIC_RUN_UNEXPECTED_NAVIGATION_RECONCILED` | GRADUATED (2026-08-16) | `ReconcileKnownPageTransition`; same-Goal; unknown fail-closed; `semantic-run-unexpected-navigation-reconciliation-graduation-decision.md` |
| D-SETTINGS-NAV | `SETTINGS_NAVIGATION_CANDIDATE_EVIDENCE_BASELINE` | GRADUATED (2026-08-16) | UiAutomator hierarchy; `StructuredElements`; `settings-navigation-candidate-evidence-graduation-decision.md` |
| D-PHASE3-AGG | `ARCHIVE_PHASE3_AGGREGATE` | CLOSED (2026-08-16) | 10/10 FULL_SCOPE_COVERED; EXPLICIT closeouts; `phase3-aggregate-lifecycle-archive-review.md` |
| D-DSH-OBS | `READ_ONLY_KERNEL_OBSERVABILITY_INTEGRATED` | GRADUATED (2026-08-15) | OBS-F9A/B/C/D; 53/53 targeted; 16/16 guards; `dsh-kernel-read-only-observability-graduation-decision.md` |
| D-DSH-SHADOW | `DSH_SHADOW_COGNITION_INTEGRATED` | GRADUATED (2026-08-15) | EPHEMERAL; F1–F16; 41/41+3/3+6/6; `dsh-shadow-cognition-graduation-decision.md` |
| D-DSH-PLUGIN | `DSH_UNICLAW_CONTROL_PLANE_PLUGIN_INTEGRATED` | GRADUATED (2026-08-15) | Real pinned host; 41/41 node; 8/8 real-host; `dsh-uniclaw-control-plane-plugin-graduation-decision.md` |
| D-DSH-EVENTS | `DSH_CONTROL_PLANE_REALTIME_EVENT_STREAM_INTEGRATED` | GRADUATED (2026-08-16) | `run.events.after`; 8/8 real-host; 23/23 cross-process; `dsh-control-plane-event-stream-graduation-decision.md` |
| D-PHYS-WIFI-S1 | `APPROVED_SLICE_1` (REALITY_COMPOSITION_FOUNDATION) | GRADUATED (2026-08-14) | PhysicalHost composition root; F1/F2; 915/915; `physical-wifi-slice1-graduation-decision.md` |
| D-PHYS-WIFI-S2 | `GRADUATED_PHYSICAL_WIFI_MINIMUM_SEMANTIC_LOOP` | GRADUATED (2026-08-14) | REAL end-to-end; F1–F6; 940/940; `physical-wifi-slice2-graduation-decision.md` |
| D-PHYS-ML | `EMULATOR_REALITY_MULTI_LEVEL_SEMANTIC_LOOP` | GRADUATED (2026-08-14) | A→B→same-Goal; 8 AUDIT PASS; `physical-settings-to-wifi-multi-level-graduation-decision.md` |
| D-COMP-FREEZE | `SEMANTIC_COMPONENT_FREEZE_READY` | FROZEN (2026-08-11) | 6 ownership rules; 17 names; 661/661; `semantic-component-freeze-gate.md` |
| D-CAP-MODEL | `THREE_LEVEL_CAPABILITY_MODEL_FROZEN` | FROZEN (2026-08-11) | L1/L2/L3; terminology guard; `agent-capability-architecture-consolidation-gate.md` |
| D-REALITY-CONTRACT | `HUMAN_ADOPT_REALITY_MODEL_ADMISSION_CONTRACT` | ADOPTED (2026-08-09) | 26 sections; E4–E0; G1–G6; `reality-model-admission-contract-gate.md` |
| D-PHASE2-GATE | `PHASE_2_HUMAN_GATE_APPROVED` | APPROVED (2026-08-08) | HG-1..HG-5; Guard 5/7; `phase2-human-gate-decision.md` |

---

## 3. Active Decisions (ACTIVE_WORK)

Condition: not complete, has explicit Next Gate.

| ID | Current State | Next Gate |
|---|---|---|
| A-SETTINGS-FULL-TREE | `settings-full-tree-enumeration-integration` — 5 phases, 0 tasks checked; first-failure classification A–H; real Android Settings root→child→grandchild→sibling→root capstone | Phase 1 SETTINGS_ROOT_REALITY_BASELINE (1.1–1.7: real foreground, root identity, structured evidence, scrollability, unknown inventory, authorization surface, ContainerComplete(Root) evidence) |
| A-POST-SETTLE | `post-action-state-settle` — APPLY executed 2026-08-17; 17/17 tests; observation-scoped target identity repaired; real emulator multilevel proof PASS | GRADUATION gate (pending Project Leader graduation review; tasks all `[x]`) |
| A-VERIFIED-CONT | `verified-local-continuity` — APPLY executed 2026-08-18; 15/15 tests; real-device corpus 0/24 false-contradiction (was 6/24) | GRADUATION gate (pending Project Leader graduation review; tasks all `[x]`) |
| A-RUNTIME-ASSIST | `runtime-assistance-seam` — APPLY executed 2026-08-17; 7/7 tests; F1–F10; null-provider zero regression | GRADUATION gate (pending Project Leader graduation review; tasks all `[x]`) |
| A-DSH-ASSIST-ADAPT | `dsh-assistance-provider-adapter` — APPLY executed 2026-08-17; model-free; 10/10 provider, 8/8 bridge, 1/1 E2E, 7/7 seam regression | GRADUATION gate (pending Project Leader graduation review; tasks all `[x]`) |
| A-RUNTIME-EXT-CONTRACT | `runtime-external-contract-baseline` — documentation-only; 8/8 slices; F1–F9 | GRADUATION gate (documentation baseline complete; pending archive) |
| A-VISION-BOOTSTRAP | `vision-runtime-bootstrap` — APPLY executed 2026-08-17; B1a RESOLVED (config/lifecycle), B1b BLOCKED (deployment identity drift) | `PROJECT_LEADER_APPLY_VISION_DEPLOYMENT_IDENTITY_ADMISSION` (§12 transaction: human admission decision + `build_active_identity.py` atomic regeneration + atomic-rename micro-fix + CORR_HOST truthful acceptance) |
| A-DSH-RUN-ENTRY | `dsh-runtime-agent-subagent-run-entry` — T1–T12 all pass; F1–F10; `run.start` + `RunExecutionCoordinator` + `ONE_ACTIVE_RUN_PER_DEVICE` | GRADUATION gate (pending Project Leader graduation review; tasks all `[x]`) |
| A-OUTER-INTEL | `outer-intelligence-integration-architecture` — DESIGN_DISCUSSION_CONVERGED; pending OpenSpec propose | OpenSpec propose `dsh-outer-intelligence` / `kernel-intelligence-seam` (IntelligenceSeam + TaskSpec + AgentProfile; must not bypass Phase 6 Intent Compilation) |
| A-ISWITCHSTATE | `ISwitchStateReader` — UNPURCHASED_L2_CONTRACT_CANDIDATE; final-gate NOT passed; 4 unresolved (authority, frame lifetime, production provider, build-zone map) | Re-open final gate requires: (1) OpenSpec purchase, (2) current-frame contract, (3) production-shaped adapter, (4) golden ON/OFF/UNKNOWN evidence, (5) deterministic fake, (6) replacement proof, (7) core-vs-adapter decision, (8) build-zone map reconciliation |
| A-OWIC-DIR | `open-world-container-inventory-completeness` directory — graduated+archived 2026-08-17 but active directory entry exists (lifecycle matrix vs archive discrepancy) | Reconcile directory state: confirm archive moved it to `archive/2026-08-17-open-world-container-inventory-completeness/`; if directory remains, archive cleanup needed |

---

## 4. Superseded Decisions

| ID | Superseded By | Reason | Migration |
|---|---|---|---|
| S-PERCEPT-FALSIFY | `perception-actionable-toggle-evidence-reality-repair-graduation-decision.md` | Original V1 graduation claimed `perception_type = empty` meant no toggles; live API35 run falsified (YOLO emits 0 control classes); raw-pixel repair established | Parent change `perception-actionable-toggle-evidence` graduated separately as parent; reality-repair is child; both archived 2026-08-16 |
| S-PERCEPT-FALSIFY-CORRECTION | `perception-actionable-toggle-evidence-reality-repair-graduation-decision.md` §15 | Intermediate record claimed "NO visible toggles" on API35 Developer Options; contradicted by repo-owned falsification frame (pixel-verified teal tracks + white knobs at 4 rows) | Retained frame + GT authoritative; old record referred to unnamed page-level inspection, not retained fixture |
| S-DEVOPTS-SCROLL | (no replacement; scenario invalidated) | Original AutomaticSystemUpdates scroll scenario: on Android 15/API 35 emulator, row visible at y≈0.77 in initial viewport — no scrolling required | `SUPERSEDED_AS_SCROLL_REALITY_SCENARIO`; recorded in `physical-scroll-container-semantic-traversal-graduation-decision.md` §3 |
| S-DSH-DURABLE-BASELINE | `dsh-shadow-cognition-graduation-decision.md` (EPHEMERAL rebaseline) | Original D5 baseline proposed `DSH_NATIVE_DURABLE` Shadow session events; falsified: `Session.append` cannot set `ignorable`; live-appended unknown event breaks cold reload; direct `sessionPersistence.append` bypasses fanout | Rebased to `EPHEMERAL_PROCESS_LOCAL`: zero custom session events; human-request-only triggers; bounded process-local cache |
| DSH-SHADOW-V1-REVIEW | Graduation V2/V3 | V1 `REPAIR_REQUIRED`: missing `inject: ['commands']` dependency declaration | Production repair: `dsh-plugin-uniclaw/src/plugin.js` gained `inject: ['commands']`; V2 added durable regression test; V3 final review |

---

## 5. Failure Catalog

| Failure ID | Symptom | Root Cause | Decision | Fix | Regression | Status |
|---|---|---|---|---|---|---|
| F-001-PERCEPT-GAP | `perception_type = empty` for all 31+ candidates on API35 Developer Options | YOLO weights do not detect control classes on API35; fusion required pre-existing icon/empty-text candidate to infer toggle | `GRADUATE` raw-pixel repair | `heuristics.py` +562 raw-pixel toggle detector; `engine.py` passes decoded image; `server.py` `image=proc_img` | Protected by repo-owned reality fixtures (developer-options-falsification.png + GT) | RESOLVED |
| F-002-OBS-F9 | `RuntimeEvent.Sequence == ObservationSequence` invariant too strict | Original V1 required `Assert.NotEqual(Sequence, ObservationSequence)` — forced numerical inequality even when coincidence valid | OBS-F9 semantic domain separation frozen | Test-only: removed `Assert.NotEqual`; replaced with semantic assertions (monotonic ordering, EventId unique, ObservationSequence ∈ Kernel anchors, terminal events null) | OBS-F9A/B/C/D tests | RESOLVED |
| F-003-DSH-INJECT | UniClaw commands not registering under real parallel loader | `ctx.get('commands')` read inside `apply` before commands service existed (parallel activation race) | V3 graduation | `dsh-plugin-uniclaw/src/plugin.js` `inject: ['commands']` on default export | `dsh-plugin-uniclaw/test/real-host.test.mjs` 8/8 durable | RESOLVED |
| F-004-DSH-DURABLE | Shadow session events break cold reload / silently dropped | `Session.append` cannot set `ignorable`; unknown event → `SessionFormatUnsupportedError`; direct `sessionPersistence.append` bypasses fanout | EPHEMERAL rebaseline | Zero custom session events; `shadow.autoTriggers = []`; bounded process-local cache | F7 restart, F15 zero-custom-event reload | RESOLVED |
| F-005-SEMANTIC-ID-GAP | Integration test falsely claimed DeveloperOptions control == WiFi semantic object | `TEST_SEMANTIC_IDENTITY_TRUTHFULNESS_GAP` — test associated real DeveloperOptions controls with `SemanticObject("WifiConnectivity")` | Test-only semantic identity repair | Repaired truthful mapping: ON→`DeveloperOptionsMaster.Enabled`, OFF→`AutomaticSystemUpdates.Enabled`; distinct objects; negative assertions forbid `WifiConnectivity`/`Bluetooth` | `AssertTruthfulSemanticModeling` guard | RESOLVED |
| F-006-FRAME-IDENTITY | Two `PerceptionFrame` instances for one screenshot | `PhysicalEnvironment.ObserveAsync` constructed second `PerceptionFrame` instead of deriving from `ImageSwitchStateProvider.Frame` | Frame identity fix (general ownership, not WiFi-specific) | `PhysicalEnvironment.ObserveAsync` derives frame from `ImageSwitchStateProvider.Frame`; one screenshot → one frame identity | Stale-frame fail-closed (`SwitchStateValidation`) | RESOLVED |
| F-007-VISION-IDENTITY-DRIFT | CORR_HOST03/04/09, DI16, IdentityMatch, H-series failures | `current-active-identity.json` receipt frozen at previous pipeline; `pipelineRevision` changed (committed source `41e322f`); receipt not regenerated | `STALE_DEPLOYMENT_RECEIPT_GOVERNANCE_FAILURE` (not a detection defect) | NOT performed in gate; recorded for deployment flow: `build_active_identity.py` atomic regeneration under human admission | F1–F10 falsifiers (candidate ready, receipt untouched) | BLOCKED (B1b; pending `PROJECT_LEADER_APPLY_VISION_DEPLOYMENT_IDENTITY_ADMISSION`) |
| F-008-POST-SETTLE-TARGET | Settle carried numeric `TargetElementIndex` across observations | `OBSERVATION_SCOPED_TARGET_IDENTITY` — index is observation-local (裁决 3); must not cross observations | Graduation repair | `IdentifyTargetToggle` re-identifies in EVERY fresh observation via spatial-relation evidence (bounds overlap + PerceptionType toggle) | T16 (index shifts → settle re-identifies), T17 (control gone → fail-closed) | RESOLVED |
| F-009-FALSE-CONTRADICTION | Real ASU OFF→ON/ON→OFF runs ended in false `SemanticContradiction` (6/24) | Title visible in initial viewport but absent after scroll/SetSwitch below-fold; `EvaluatePageBelief` treated title-absence as contradiction | `verified-local-continuity` | `TryAcceptVerifiedContinuity` + `EvaluatePageBeliefVerifiedContinuity` (LOCAL_IDENTITY Supports when Agent independently verified); `RefreshSemanticSnapshot(..., verifiedLocalContinuity)` | T1–T15 + T8b; real corpus 0/24 | RESOLVED |
| F-010-STATE-EVIDENCE-TRANSIENT | `STATE_EVIDENCE_REQUIRED_TRANSIENT_FAILURE` on real emulator Wi-Fi | Post-action animation window returned null SwitchState; no settle mechanism | `post-action-state-settle` | D.HYBRID settle: immediate observe → bounded delay → fresh ObserveAsync → re-evaluate; first-valid-frame stop; budget max 3 / ≈0.9s | T1–T17; real multilevel proof PASS | RESOLVED |
| F-011-ADB-FLAKE | `PF01_ProcessRunner_TimeoutKillsShortLivedChildWithoutShellInterpolation` occasional failure | 75ms timeout kills `/bin/sleep 5` + `<2s` wall-clock assertion; timing flake | `PRE_EXISTING_TIMING_FLAKE` (not a regression) | None (not attributed to any change; isolated re-run 5/5 pass) | N/A | PRE_EXISTING |

---

## 6. Documentation Plan

Current state: `docs/decisions/` has 182 files (flat). `docs/system/constitution/` has charter + contract. `docs/architecture/guards/` exists. NO `docs/architecture/`, `docs/decisions/` index, `docs/failures/`, or `docs/governance/` consolidated structure exists.

### Need to add:

```
docs/architecture/
  ├── baseline.md              # Current Architecture Baseline (§1 of this decision)
  ├── runtime-spine.md          # Agent → Container → Traversal → Environment + invariants
  ├── evidence-model.md         # Raw → Structured → Semantic → Goal Evidence chain
  ├── source-identity-model.md  # NavigationSourceOccurrence / ProvenLogicalSource / equivalence
  ├── container-completeness.md # ContainerInventoryCompletenessEvidence / frozen epoch
  ├── fresh-observation.md      # SequenceNumber advance / stale-frame fail-closed
  ├── vision-identity-governance.md  # 5-axis identity / admission flow / candidate≠admitted
  ├── dsh-assistance-boundary.md # IAssistanceProvider seam / advice-mode / world-version
  └── outer-intelligence-seam.md # IIntelligenceProvider design (DESIGN ONLY)
docs/decisions/
  ├── INDEX.md                  # Master index: graduated / active / superseded
  └── (existing flat files reorganized by category symlink or index reference)
docs/failures/
  ├── catalog.md                # Failure Catalog (§6 of this decision)
  └── (per-failure detail files if needed)
docs/governance/
  ├── openspec-lifecycle.md     # Active/archived matrix + lifecycle classifications
  ├── authority-boundary.md     # Agent/Container/Traversal/Environment/Evidence authority table
  ├── reality-model-contract.md # Pointer to system contract + admission rules
  └── architecture-gates.md     # Semantic Component Freeze + Capability Model + Final Gate
```

---

## 7. Executor Task List

> For Nova + DeepSeek-V4-Flash execution.
> Each task is DOCUMENTATION-ONLY unless explicitly stated.
> Executor rules: execute only approved tasks; NO autonomous design; NO Runtime semantic modification; NO new Decision creation.
> On any authority conflict, architecture boundary change, or need for new Decision → STOP → `ARCHITECTURE_DECISION_REQUIRED`.

---

### TASK-DOC-001

**Goal:** Establish `docs/architecture/baseline.md` consolidating the Current Architecture Baseline (§1 of this decision) as the canonical architecture reference.

**Files:**
- `docs/architecture/baseline.md` (NEW)

**Allowed Change:** Create new documentation file. Content = §1 of `architecture-consolidation-decision.md` reformatted as standalone reference. No production/test/spec code touched.

**Forbidden Change:** No code modification. No invariant re-interpretation. No new architectural claims beyond what is in graduated decisions. No merging of DESIGN_ONLY items (outer-intelligence, ISwitchStateReader) into "current" baseline.

**Acceptance Criteria:**
- File exists at `docs/architecture/baseline.md`.
- Content covers: Runtime, Agent, Evidence, Vision, DSH, Governance subsections.
- Every claim cites its source decision file (e.g., `[D-PHASE1]`).
- `scripts/check-consistency.sh` still ALL PASS.

**Verification:** `read` the file; confirm citations; run `scripts/check-consistency.sh`.

---

### TASK-DOC-002

**Goal:** Establish `docs/decisions/INDEX.md` as master index of all 182 decision files, classified GRADUATED_ARCHITECTURE / ACTIVE_WORK / SUPERSEDED.

**Files:**
- `docs/decisions/INDEX.md` (NEW)

**Allowed Change:** Create new documentation index file. Read-only enumeration of existing files. No file moves/renames.

**Forbidden Change:** No deletion or modification of existing decision files. No re-classification that contradicts the source decision's own Status line. No synthesizing new status for ambiguous files (mark `UNCLASSIFIED` and stop).

**Acceptance Criteria:**
- File lists all `docs/decisions/*.md` files.
- Each entry has: filename, classification (GRADUATED_ARCHITECTURE / ACTIVE_WORK / SUPERSEDED / GOVERNANCE_GATE / RESULT_RECEIPT / UNCLASSIFIED), one-line summary, source-status citation.
- Classification matches §2/§3/§4 of this decision for known items.

**Verification:** `read` the index; spot-check 10 random entries against source files.

---

### TASK-DOC-003

**Goal:** Establish `docs/failures/catalog.md` consolidating the Failure Catalog (§6 of this decision) as canonical failure reference.

**Files:**
- `docs/failures/catalog.md` (NEW)

**Allowed Change:** Create new documentation file. Content = §6 of `architecture-consolidation-decision.md` reformatted.

**Forbidden Change:** No code modification. No re-classification of RESOLVED failures as active. No minimizing of BLOCKED failures (F-007 must remain BLOCKED).

**Acceptance Criteria:**
- File lists all 11 failures with full 7-field schema.
- BLOCKED status clearly marked for F-007.
- Each failure cites its source decision/repair record.

**Verification:** `read` the file; confirm F-007 BLOCKED; confirm citations.

---

### TASK-DOC-004

**Goal:** Establish `docs/governance/openspec-lifecycle.md` consolidating active/archived OpenSpec change state.

**Files:**
- `docs/governance/openspec-lifecycle.md` (NEW)

**Allowed Change:** Create new documentation file. Content derived from `docs/decisions/active-openspec-lifecycle-matrix.md` + current `openspec/changes/` directory listing + `openspec/changes/archive/` listing.

**Forbidden Change:** No OpenSpec change creation/archival. No modification of `active-openspec-lifecycle-matrix.md` (it remains the historical receipt). No re-classification of LONG_LIVED_BASELINE as graduated.

**Acceptance Criteria:**
- File lists all 11 active changes with lifecycle classification + task count + next gate.
- File lists all 30 archived changes with maturity + archive date.
- Discrepancy note for `open-world-container-inventory-completeness` (graduated+archived but active directory entry — recorded for reconciliation, NOT fixed in this task).

**Verification:** `read` the file; cross-check against `ls openspec/changes/` and `ls openspec/changes/archive/`.

---

### TASK-DOC-005

**Goal:** Establish `docs/governance/authority-boundary.md` consolidating the frozen authority table from Semantic Component Freeze + Phase 1/2 graduation decisions.

**Files:**
- `docs/governance/authority-boundary.md` (NEW)

**Allowed Change:** Create new documentation file. Content = ownership/authority tables from `semantic-component-freeze-gate.md` §1 + `phase1-deterministic-runtime-graduation-decision.md` §4 + `phase2-trap-recovery-graduation-decision.md` §4.

**Forbidden Change:** No authority re-interpretation. No adding new owners. No merging Trap/Recovery authority into a single owner. No treating `ISwitchStateReader` as purchased.

**Acceptance Criteria:**
- Table covers: Agent (RunState, WorldBelief, Active Container Stack, TraceEvent, Run termination, Container local state, Traversal step state, Simulated world state, Element grounding, Physical effect, Trap emission, Recovery decision).
- Each row cites source charter section + invariant.
- `ISwitchStateReader` marked `UNPURCHASED_L2_CONTRACT_CANDIDATE`.

**Verification:** `read` the file; cross-check against `semantic-component-freeze-gate.md` §1.

---

### TASK-DOC-006

**Goal:** Establish `docs/architecture/evidence-model.md` documenting the Raw → Structured → Semantic → Goal Evidence chain and authority immutability.

**Files:**
- `docs/architecture/evidence-model.md` (NEW)

**Allowed Change:** Create new documentation file. Content derived from Phase 1 graduation §5 + OBS-F9 decision + perception graduation decisions.

**Forbidden Change:** No new evidence types. No treating Python `switch_state` as authoritative. No GoalEvidence authority shift. No Runtime-changable authority claims.

**Acceptance Criteria:**
- Chain documented: Raw (Observation/Perception/UiAutomator) → Structured (SemanticEvidence/ObjectBinding) → Semantic (WorldBelief/Container beliefs) → Goal (GoalEvidence KERNEL_ONLY).
- OBS-F9 domain separation documented (`RuntimeEvent.Sequence` ≠ `ObservationSequence`).
- Source Identity model documented (DISCOVERED != GROUNDED != ... != COMPLETED).

**Verification:** `read` the file; confirm OBS-F9 citation; confirm authority chain.

---

### TASK-DOC-007

**Goal:** Establish `docs/architecture/vision-identity-governance.md` documenting the 5-axis deployment identity model and admission boundary.

**Files:**
- `docs/architecture/vision-identity-governance.md` (NEW)

**Allowed Change:** Create new documentation file. Content from `vision-deployment-identity-admission-gate.md` + `vision-runtime-bootstrap` tasks + perception repair decision §16.

**Forbidden Change:** No receipt mutation. No auto-admission design. No weakening of identity verifier. No treating candidate as admitted.

**Acceptance Criteria:**
- 5-axis identity graph documented (schemaVersion/modelId/configId/pipelineRevision/deploymentId).
- Admission flow documented (candidate → validation → HUMAN/PL decision → `build_active_identity.py` → activation).
- Current state: candidate READY, admitted STALE (B1b BLOCKED).
- F1–F10 falsifiers documented.

**Verification:** `read` the file; confirm B1b BLOCKED; confirm candidate≠admitted.

---

### TASK-DOC-008

**Goal:** Establish `docs/architecture/dsh-assistance-boundary.md` documenting the Assistance seam authority boundary (implemented, model-free).

**Files:**
- `docs/architecture/dsh-assistance-boundary.md` (NEW)

**Allowed Change:** Create new documentation file. Content from `runtime-assistance-seam` + `dsh-assistance-provider-adapter` tasks + `runtime-external-contract-baseline` spec.

**Forbidden Change:** No Assistance-to-Truth path. No Advisory/Blocking cognition design (DEFERRED). No new wire methods beyond frozen 8 + run.start + assistance.pending/resolve. No IntelligenceSeam implementation (DESIGN ONLY).

**Acceptance Criteria:**
- Seam shape: `IAssistanceProvider?` optional ctor param; `AssistanceContext`/`AssistanceAdvice`.
- Call points: Contradicted + Unresolved only.
- Consumption: advice-mode (re-observe/rebind/dismiss/fail-closed); `MaxAssistanceConsults=3`.
- World-version binding/staleness.
- Wire table: frozen 8 + run.start + assistance.pending/resolve (additive).
- Deferred: Advisory/Blocking cognition, persistent EvidenceRef, TaskSpec/AgentProfile.

**Verification:** `read` the file; confirm advice≠truth; confirm model-free.

---

### TASK-DOC-009

**Goal:** Reconcile `open-world-container-inventory-completeness` directory state — confirm whether it was archived to `openspec/changes/archive/2026-08-17-open-world-container-inventory-completeness/` and the active directory is stale residue.

**Files:**
- `openspec/changes/open-world-container-inventory-completeness/` (investigate only)
- `openspec/changes/archive/2026-08-17-open-world-container-inventory-completeness/` (investigate only)

**Allowed Change:** Read-only investigation. If archived copy exists AND active directory is empty/stale, document the finding in `docs/governance/openspec-lifecycle.md` (TASK-DOC-004 output) as a resolved discrepancy. Do NOT delete or move any directory without explicit Project Leader approval.

**Forbidden Change:** No `git mv` or `rm` of any OpenSpec directory. No archival operation. If discrepancy cannot be resolved read-only → STOP → `ARCHITECTURE_DECISION_REQUIRED`.

**Acceptance Criteria:**
- Finding documented: either (a) active directory is stale residue and archive copy is canonical, or (b) active directory is canonical and archive is a copy, or (c) ARCHITECTURE_DECISION_REQUIRED.
- No file system mutation performed.

**Verification:** `ls` both paths; `diff` if needed; document finding.

---

### TASK-DOC-010

**Goal:** Establish `docs/governance/architecture-gates.md` consolidating the three architecture gates (Semantic Component Freeze, Capability Model, Capability Module Final Gate) as a single governance reference.

**Files:**
- `docs/governance/architecture-gates.md` (NEW)

**Allowed Change:** Create new documentation file. Content = summary of `semantic-component-freeze-gate.md` + `agent-capability-architecture-consolidation-gate.md` + `capability-module-architecture-final-gate.md`.

**Forbidden Change:** No re-opening of frozen gates. No re-interpretation of `FINAL_GATE_NOT_PASSED` as passed. No implementation authority granted for `ISwitchStateReader`/`StateClassifier`/`Vision`/`Brain`/`Operator`.

**Acceptance Criteria:**
- Three gates documented with their verdicts.
- `ISwitchStateReader` marked UNPURCHASED; 8 evidence items required before re-opening listed.
- `STATE_CLASSIFIER = DEFERRED_NOT_IMPLEMENTED`.
- Facade admission gate (8 conditions) documented.

**Verification:** `read` the file; confirm no implementation authority granted.

---

## Executor Rules (binding)

1. **Execute only approved tasks.** Each TASK-DOC-* above is approved. No other work is authorized.
2. **No autonomous design.** If a task reveals a need for new architecture, STOP → `ARCHITECTURE_DECISION_REQUIRED`.
3. **No Runtime semantic modification.** Tasks are documentation-only. Zero production/test/spec code changes.
4. **No new Decision creation.** Executors document and index; they do not graduate, supersede, or admit.
5. **Authority boundary preservation:** If any task reveals Agent/Container/Traversal/Environment/Evidence authority drift, Fixture-as-authority, test-manufactured completion, or Runtime changing GoalEvidence authority → STOP → `ARCHITECTURE_DECISION_REQUIRED`.
6. **Architecture boundary change:** If any task reveals the frozen spine (I-1), single-owner (I-2), single-authority (I-3), OBS-F9, or GoalEvidence=KERNEL_ONLY is violated or needs to change → STOP → `ARCHITECTURE_DECISION_REQUIRED`.
7. **Parallelization:** TASK-DOC-001 through TASK-DOC-008 and TASK-DOC-010 are independent and may run in parallel. TASK-DOC-009 is independent but its output feeds TASK-DOC-004's discrepancy note.
8. **Completion reporting:** Each task reports: files created, citations verified, `scripts/check-consistency.sh` result (if applicable), and any STOP triggers encountered.

---

## Compliance

- FORBIDDEN list respected: zero production change, zero test change, zero spec mutation, zero Runtime refactor, zero API modification, zero authority modification, zero historical implementation deletion.
- This document is the architecture consolidation decision; it creates NO new graduation, NO new authority, NO new capability.
- All facts derived from: graduated Gate Results, submitted Decisions, verified test results, current code facts (head `203cf83`, uni-agent branch).
- No speculation about future design; no best-practice replacement of existing architecture; no new capabilities introduced.

---

## State

```text
ARCHITECTURE_CONSOLIDATION_DECISION_ISSUED
```

Next Project Leader action: review this decision; if approved, dispatch TASK-DOC-001 through TASK-DOC-010 to Nova + DeepSeek-V4-Flash executors.
