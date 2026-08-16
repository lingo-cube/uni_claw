# IntegrationMatrix — UniClaw ↔ DSH Control-Plane Mapping

> Change: `dsh-uniclaw-control-plane-protocol-baseline`
> Pinned DSH baseline: `UNICLAW_DSH_COMPATIBILITY_BASELINE = 47f943859bef60e4160492346772ded9b24f765a` (0.1.0-rc.5, pre-release, no tags)
> Authority planes (frozen): **COGNITIVE/CONTROL = DeepSeek Harness** · **EXECUTION/STATE = UniClaw Kernel** · **REALITY/TRUTH = External World**
> DSH proposal ≠ Kernel authorization · DSH plan ≠ physical execution · DSH belief ≠ Container truth · DSH tool result ≠ GoalEvidence · DSH completion opinion ≠ Kernel completion

## Decision Codes

| Code | Meaning |
|------|---------|
| `NATIVE_DSH_SEAM_CONFIRMED` | DSH exposes a native surface (event/registry/service/command/tool/slot/plugin) that carries the semantic directly, without inventing a parallel protocol |
| `NATIVE_DSH_SEAM_WITH_ADAPTER` | DSH native surface exists, but a thin adapter inside dsh-plugin-uniclaw is required to translate DriverHost-shaped data into the DSH surface contract |
| `NO_NATIVE_SEAM_FOUND` | No DSH-native surface carries the semantic; a new DSH-side mechanism would be required (NOT purchased in this baseline) |
| `DEFERRED_NEEDS_BUYER` | A native seam exists and is recorded, but no concrete consumer/buyer exists in this baseline; purchase is deferred to the named future change |
| `PROTOCOL_PRESSURE` | Recorded as pressure only — the semantic is real but deliberately NOT purchased (no buyer, or Kernel-side emitter missing); becomes a candidate for a later change with a concrete buyer |

## Required Rows

### A. Observability → DSH (Kernel read-only truth → DSH surfaces)

| UniClaw Concern | UniClaw Source | DSH Native Surface | Direction | Durable / Live / Read-only | Model Involved? | Authority | Freshness | Adapter Required? | Status | DSH Source Evidence |
|---|---|---|---|---|---|---|---|---|---|---|
| RuntimeEvent (Kernel canonical read model, graduated) | RuntimeEventProjector.cs (Kernel observability seam, read-only; frozen contract) | dsh-plugin-uniclaw host plugin subscribes via DriverHost read surface, then: live plugin event (`emit`), optionally appended as plugin-declared session event (S1/S2/S3), optionally projected (S17), pushed to UI (S15/S16) | UniClaw → DSH | Live by default; durable copy ONLY with buyer (audit/replay/resume) | No (read path is deterministic) | Kernel owns emission; DSH copy is a projection, never authoritative | Event-driven push; freshness = Kernel emission time | Yes — DriverHost payload → DSH event shape (adapter) | `NATIVE_DSH_SEAM_WITH_ADAPTER` for the live path; **durable copy of high-volume kinds `DEFERRED_NEEDS_BUYER`** | S1, S2, S3, S6, S17, S16 |
| RunSnapshot (Kernel read-only read model, graduated) | RunSnapshotProjector.cs (DIRECT_PUBLIC_PROJECTION / DERIVED_READ_MODEL / NOT_CURRENTLY_AVAILABLE field classification) | Read-only `UniClawRunService extends TypertRemoteService` with `remoteExport*` methods (S13) as canonical read path; projection unit `uniclaw.runSnapshot` (S17) for live UI push; command result for human inspect (S7); client slot data source (S12) | UniClaw → DSH (read) | Read-only; live service read + live projection push; persisted cache only as fold checkpoint (S17), never a second mutable Runtime state | No | Kernel is truth source; DSH copy is derived-where-marked, non-authoritative | Read-on-demand + `session/projection` push on change | Yes — DriverHost snapshot → service/projection contract | `NATIVE_DSH_SEAM_WITH_ADAPTER` | S13, S17, S15, S16, S12 |
| EvidenceRef (logical evidence identity, graduated; persistent resolution NOT available) | EvidenceRef logical contract (not filesystem path; resolve via Harness assets) | DSH session event data carries EvidenceRef as logical id (S1/S2); `uniclaw.get_evidence`-style tool (S9) or command (S7) returns metadata-first, raw evidence lazy/on-demand; UI shows logical ref (S12) | UniClaw → DSH | Metadata durable (refs in session events); raw evidence on-demand only | Deterministic retrieval: no | Evidence identity stays logical; physical resolution stays in UniClaw/Harness | Ref freshness = session event time | Yes — adapter renders DriverHost evidence shape into DSH payloads | `NATIVE_DSH_SEAM_WITH_ADAPTER`; **persistent EvidenceRef resolution `DEFERRED_NEEDS_BUYER`** (F14 — never claim complete) | S1, S2, S7, S9, S12 |
| ObservationProduced (B/A event family) | RuntimeEvent source truth table (span-derivable) | Live plugin event; service read-model (RunSnapshot carries current Observation summary where available); **NOT durable by default** (high-volume, no buyer) | UniClaw → DSH | Live; not durable | No | Kernel truth | Push | Yes | `NATIVE_DSH_SEAM_WITH_ADAPTER` (live); durable `DEFERRED_NEEDS_BUYER` | S6, S13, S17 |
| ContainerReconciled (B/A event family) | span-derivable | Live plugin event; RunSnapshot `CurrentContainerSummary` (NOT_CURRENTLY_AVAILABLE today — keep absent); UI projection of what IS available | UniClaw → DSH | Live; not durable | No | Kernel truth | Push | Yes | `NATIVE_DSH_SEAM_WITH_ADAPTER` (live); durable `DEFERRED_NEEDS_BUYER` | S6, S13, S17 |
| ActionDispatched (A+B event family) | span-portion derivable + read model | Live plugin event; RunSnapshot `LastAction` (DERIVED_READ_MODEL) read via service; durable copy `DEFERRED_NEEDS_BUYER` (audit buyer not yet concrete) | UniClaw → DSH | Live; read-only; not durable in baseline | No | Kernel truth | Push + read | Yes | `NATIVE_DSH_SEAM_WITH_ADAPTER` | S6, S13 |
| NavigationDecision (B event family) | public read model | Live plugin event; RunSnapshot `LastDecision` (DERIVED_READ_MODEL) read via service; **not model-visible by default** (I-15) | UniClaw → DSH | Live; read-only | No | Kernel truth | Push + read | Yes | `NATIVE_DSH_SEAM_WITH_ADAPTER` | S6, S13 |
| ViewportExplorationDecision (B event family) | public read model | Live plugin event; UI projection (exploration progress) via projection unit where data available | UniClaw → DSH | Live; read-only | No | Kernel truth | Push | Yes | `NATIVE_DSH_SEAM_WITH_ADAPTER` | S6, S17 |
| TrapRaised (B event family) | public read model (`LastTrap`) | Live plugin event + durable session event (buyer: human inspection, pause/resume, audit) — trap is low-frequency, high-value | UniClaw → DSH | Durable (buyer-confirmed: human inspection + post-run audit) + live | No | Kernel truth | Push | Yes | `NATIVE_DSH_SEAM_WITH_ADAPTER` | S1, S2, S3, S6, S7 |
| RecoveryStarted (B event family) | public read model (`RecoveryAnchor`) | Live plugin event; durable copy `DEFERRED_NEEDS_BUYER` (recovery audit buyer not yet concrete) | UniClaw → DSH | Live; durable deferred | No | Kernel truth | Push | Yes | `NATIVE_DSH_SEAM_WITH_ADAPTER` (live); durable `DEFERRED_NEEDS_BUYER` | S6 |
| GoalEvidenceProduced (partial; full source sequence NOT available) | partial GoalEvidence only (`State=Completed` + `Reason`, no `SourceObservationSequence`) | Live plugin event; RunSnapshot `LatestGoalEvidence` partial read (NOT_CURRENTLY_AVAILABLE for full); **DSH never creates GoalEvidence** (F6) — DSH records only what Kernel exposes | UniClaw → DSH | Live; read-only | No | Kernel owns GoalEvidence; DSH records Kernel-reported facts only | Push + read | Yes | `NATIVE_DSH_SEAM_WITH_ADAPTER`; full sequence `PROTOCOL_PRESSURE` | S6, S13, S1 |
| RunCompleted / RunFailed | Kernel completion | Live plugin event + durable session event (buyer: run closeout, audit, model-context reconstruction, human inspection) | UniClaw → DSH | Durable (buyer-confirmed) | No (the record); a future Shadow turn may read it with its own buyer | Kernel declares completion; DSH records the Kernel-reported fact, never its own completion opinion | Push | Yes | `NATIVE_DSH_SEAM_WITH_ADAPTER` | S1, S2, S3, S6 |

### B. Human Control → Kernel (DSH → UniClaw; deterministic, model-free by default)

| UniClaw Concern | UniClaw Source | DSH Native Surface | Direction | Durable / Live / Read-only | Model Involved? | Authority | Freshness | Adapter Required? | Status | DSH Source Evidence |
|---|---|---|---|---|---|---|---|---|---|---|
| Start Run | Kernel entry (approved control op; DriverHost future responsibility) | DSH command `/uniclaw start` (S7) — handler executes against receiving agent WITHOUT model turn | DSH → UniClaw | `command/run`+`command/done` durable; control op live | **No — 0 model tokens (F8)** | Kernel authorizes execution; DSH only forwards human intent | Immediate | Yes — command handler → DriverHost admission → Kernel control op | `NATIVE_DSH_SEAM_CONFIRMED` (command seam); **Kernel control op itself `DEFERRED_NEEDS_BUYER`** (DriverHost future bounded work) | S7, S8 |
| Pause Run | Kernel control op (future) | DSH command `/uniclaw pause` | DSH → UniClaw | Durable lifecycle events; live op | No | Kernel | Immediate | Yes | `NATIVE_DSH_SEAM_CONFIRMED` (seam); Kernel op deferred | S7 |
| Resume Run | Kernel control op (future) | DSH command `/uniclaw resume` | DSH → UniClaw | Durable; live | No | Kernel | Immediate | Yes | `NATIVE_DSH_SEAM_CONFIRMED` (seam); Kernel op deferred | S7 |
| Stop Run | Kernel control op (future) | DSH command `/uniclaw stop` | DSH → UniClaw | Durable; live | No | Kernel | Immediate | Yes | `NATIVE_DSH_SEAM_CONFIRMED` (seam); Kernel op deferred | S7 |
| Abort Run | Kernel control op (future) | DSH command `/uniclaw abort` | DSH → UniClaw | Durable; live | No | Kernel | Immediate | Yes | `NATIVE_DSH_SEAM_CONFIRMED` (seam); Kernel op deferred | S7 |
| Inspect Run | RunSnapshot read model | DSH command `/uniclaw inspect` returning structured command result (S7) + read-only service (S13) + UI panel (S12) | UniClaw → DSH (read) | Read-only; durable command events | No | Kernel truth | Read-on-demand | Yes | `NATIVE_DSH_SEAM_CONFIRMED` | S7, S13, S12 |
| Inspect Trap | Kernel `LastTrap` | DSH command `/uniclaw trap` + UI panel | UniClaw → DSH (read) | Read-only | No | Kernel truth | Read-on-demand | Yes | `NATIVE_DSH_SEAM_CONFIRMED` | S7, S12 |
| Inspect Evidence | EvidenceRef resolution (metadata) | DSH command `/uniclaw evidence <ref>` (metadata first) | UniClaw → DSH (read) | Read-only; metadata via logical ref | No | Logical identity; physical resolution in UniClaw/Harness | On-demand; lazy raw | Yes | `NATIVE_DSH_SEAM_CONFIRMED` | S7, S9 |
| Retry | Kernel recovery op (future) | DSH command `/uniclaw retry` | DSH → UniClaw | Durable; live | No | Kernel validates retry against fresh state | Immediate | Yes | `NATIVE_DSH_SEAM_CONFIRMED` (seam); Kernel op deferred | S7 |
| Request Recovery | Kernel recovery op (future) | DSH command `/uniclaw recover` | DSH → UniClaw | Durable; live | No | Kernel validates recovery validity | Immediate | Yes | `NATIVE_DSH_SEAM_CONFIRMED` (seam); Kernel op deferred | S7 |
| Submit Requirement | Kernel requirement intake | DSH command `/uniclaw requirement <text>` (handler forwards proposal; Kernel admission is Kernel authority) | DSH → UniClaw | Durable | No | Kernel admits/validates | Immediate | Yes | `NATIVE_DSH_SEAM_CONFIRMED` (command seam); admission deferred | S7 |
| Submit Goal | Kernel goal intake | DSH command `/uniclaw goal <objective>` OR GoalService machinery (S23) if goal lifecycle should live in DSH — baseline: proposal via command, Kernel owns goal authority | DSH → UniClaw | Durable | No | Kernel owns goal acceptance | Immediate | Yes | `NATIVE_DSH_SEAM_CONFIRMED` (command seam); Kernel goal admission deferred; GoalService reuse decision in design.md §12 | S7, S23 |

### C. Cognitive Ops (DSH cognition; model involved; Kernel authority preserved)

| UniClaw Concern | UniClaw Source | DSH Native Surface | Direction | Durable / Live / Read-only | Model Involved? | Authority | Freshness | Adapter Required? | Status | DSH Source Evidence |
|---|---|---|---|---|---|---|---|---|---|---|
| Requirement interpretation | Requirement text (human/DSH input) | DSH agent loop (LLM) reads requirement; output = proposal artifact; trigger = user message / session prompt (S21/S3) | External/DSH → proposal | Proposal durable as plugin-declared session event | **Yes — model cognition with explicit buyer** (I-15: only after deterministic acquisition) | Proposal ≠ authorization; Kernel admits | Turn-based | Yes | `NATIVE_DSH_SEAM_CONFIRMED` (agent loop + session event); proposal-envelope `DEFERRED_NEEDS_BUYER` | S3, S21, S1 |
| Goal proposal | Interpreted requirement | GoalService (S23) `create` OR command `/uniclaw goal`; baseline freezes command-proposal path; GoalService reuse is a design decision, not a baseline purchase | DSH → proposal | Durable (`goal/change` or command events) | Yes (interpretation) / No (mechanical submit) | Kernel owns goal acceptance | Turn-based | Yes | `NATIVE_DSH_SEAM_CONFIRMED`; Kernel admission deferred | S23, S7 |
| Decision proposal | Kernel decision loop (C-class `DecisionProposed` — Kernel emitter DOES NOT EXIST) | DSH session event of plugin-declared type (S1) carrying proposal + basis refs; delivered to DriverHost admission → Kernel fresh-state revalidation → Kernel authority decision | DSH → proposal | Durable proposal (buyer: Kernel admission) | Yes — model proposes; Kernel disposes | Kernel decides; DSH proposes only | Freshness validated at Kernel admission (staleness entry) | Yes | `NATIVE_DSH_SEAM_WITH_ADAPTER` (session-event vocabulary IS the native seam — F4 satisfied, no parallel protocol); **Kernel-side `DecisionProposed` emitter `PROTOCOL_PRESSURE`** (C-class, not purchased) | S1, S2, S3 |
| Diagnosis | Trap/recovery context | DSH agent analysis (LLM) over bounded read-only context; output = diagnosis artifact session event; may delegate to subagent (S26) | DSH → artifact | Durable diagnosis (buyer: human inspection + recovery prep) | Yes | Diagnosis = candidate, not Kernel truth | Turn-based; fresh-state revalidation at use | Yes | `NATIVE_DSH_SEAM_CONFIRMED`; Kernel consumption deferred | S3, S26, S6 |
| Healing candidate | Diagnosis output | DSH proposes healing candidate as session event → DriverHost admission → Kernel revalidation → Kernel authority; workflow orchestration (S25) if multi-agent | DSH → proposal | Durable proposal | Yes | Kernel validates healing | Fresh-state revalidation | Yes | `NATIVE_DSH_SEAM_CONFIRMED`; Kernel op deferred | S1, S25 |
| Workflow orchestration | Multi-step cognition | `ctx.workflowEngine` + `workflow` tool (S25) + subagents (S26) | DSH-internal | Script per-call; orchestration live | Yes | Orchestration never touches world directly | Per-phase | No (native) | `NATIVE_DSH_SEAM_CONFIRMED` | S25, S26 |
| Shadow cognition | Kernel read-only truth | dsh-plugin-uniclaw live events → (later dsh-shadow-cognition change) DSH agent consumes read-only truth, performs cognition, records output as plugin-declared session event/projection; Kernel execution unchanged; **Kernel consumption of DSH output = ZERO** | UniClaw → DSH → (recorded DSH side) | Recorded DSH-side artifact | Yes | Kernel unaffected | Push-triggered | Yes | `NATIVE_DSH_SEAM_WITH_ADAPTER`; **insertion point frozen here, implementation in `dsh-shadow-cognition`** | S6, S1, S17, S25 |

### D. UI / Client (DSH browser)

| UniClaw Concern | UniClaw Source | DSH Native Surface | Direction | Durable / Live / Read-only | Model Involved? | Authority | Freshness | Adapter Required? | Status | DSH Source Evidence |
|---|---|---|---|---|---|---|---|---|---|---|
| Control Plane UI | DriverHost projections | dsh-plugin-uniclaw client half (S11) registering session-scoped slots (S12) for Run Status / RunSnapshot / Evidence panels; data via Typert Remote service (S13) + `session/projection` push (S16/S17) | UniClaw → browser (via DSH) | Live; projection-derived, never Kernel truth (F13) | No | Kernel truth; UI is a projection | Live push + read-on-demand | Yes | `NATIVE_DSH_SEAM_WITH_ADAPTER`; actual UI design is a later phase | S11, S12, S13, S16, S17 |
| Trace / timeline display | RuntimeEvent stream (bounded) | Client slot rendering plugin-appended session events (trajectory-view pattern S12) | UniClaw → browser | Live; bounded | No | Projection | Push | Yes | `NATIVE_DSH_SEAM_WITH_ADAPTER` | S12, S16 |

## DecisionTable

| Semantic | Decision | Rationale (source pressure) |
|----------|----------|-----------------------------|
| Kernel fact → DSH live event | `NATIVE_DSH_SEAM_WITH_ADAPTER` | DSH `emit` events (S6/S33) carry any payload; adapter translates DriverHost shape — no parallel protocol needed (F4 satisfied) |
| Kernel fact → DSH durable session event | `DEFERRED_NEEDS_BUYER` (all but TrapRaised/RunCompleted/RunFailed/GoalEvidenceProduced-partial) | Durability requires real buyer (F14/F15 discipline); high-volume kinds stay live-only to protect token/durability economy |
| Kernel fact → DSH model context | `DEFERRED_NEEDS_BUYER` | I-15: structured read → deterministic service/command/tool → bounded retrieval → model cognition only with explicit buyer; no model-facing uniclaw tool pre-approved (F-tool table in design.md §13) |
| RunSnapshot read | `NATIVE_DSH_SEAM_WITH_ADAPTER` | TypertRemoteService (S13) + projection unit (S17) + command (S7) — three native read paths, all read-only |
| EvidenceRef | `NATIVE_DSH_SEAM_WITH_ADAPTER` (logical identity, metadata-first) | Session events carry logical refs (S1); persistent resolution absent → `DEFERRED_NEEDS_BUYER`, never claimed (F14) |
| Human control (start/pause/resume/stop/abort/inspect/retry/recover/requirement/goal) | `NATIVE_DSH_SEAM_CONFIRMED` | Commands registry (S7) is deterministic, model-free, durable-lifecycle-logged — the exact F8-required seam |
| Kernel control op execution | `DEFERRED_NEEDS_BUYER` | Kernel control surface is future bounded DriverHost work (graduated observability change deliberately built read-only ops only); DSH seam exists now, execution deferred |
| Decision/Diagnosis/Healing proposal envelopes | `NATIVE_DSH_SEAM_WITH_ADAPTER` (no new envelope) | SessionEventMap merge-extension (S1) IS the native vocabulary seam — proposal artifacts ride plugin-declared session event types; NO custom DecisionRequest/Response/HealingRequest protocol frozen (F4) |
| Goal lifecycle | `DEFERRED_NEEDS_BUYER` (GoalService reuse decision) | GoalService (S23) is DSH-native; whether UniClaw run goals ride it is a later design decision with a concrete buyer; baseline freezes command-proposal path |
| Shadow insertion point | Frozen now (S6/S1/S17), implemented in `dsh-shadow-cognition` | Plugin live events → cognition → recorded DSH-side artifact; Kernel execution unchanged; Kernel consumption zero |
| Advisory boundary | Frozen now (S1 basis/provenance metadata), protocol design in `dsh-advisory-cognition` | Session events carry basis/provenance; DriverHost admission + Kernel fresh-state revalidation are Kernel-side |
| Transport DSH↔DriverHost | `TRANSPORT_DEFERRED` (see design.md §18) | DSH native seams are in-process (Cordis), browser (S15/S16), or inbound-automation (S21/S22); no source pressure for a custom carrier in this baseline; adapter boundary decides in implementation phase |
| Process lifecycle | DriverHost owns its own process (design.md §19) | No architectural requirement makes DSH responsible for Kernel process durability; plugin CONNECTS, never launches/supervises |
| Plugin placement | Host plane rows + optional per-session preset rows (design.md §6) | Two-plane rule: registries/shared → host composition; one-session tools/persona → agent preset (S27/S28) |
| Parallel protocol | **NOT INVENTED** | Every required semantic maps onto a DSH-native surface (S1–S43); no custom UniClaw↔DSH wire protocol is proposed (F4 MUST_BE_NO) |

## Hard Forbidden Paths (frozen)

| Path | Why |
|------|-----|
| DSH → Environment/ADB directly | Direct physical dispatch (F5) |
| DSH → Container mutation | Container ownership is Kernel-only |
| DSH → Traversal state mutation outside approved Kernel entry | Traversal state is Kernel-only |
| DSH → GoalEvidence creation | F6 |
| DSH → completion declaration | Kernel declares completion |
| DSH/plugin as second mutable Runtime state owner | F7 |
| DSH UI state treated as Kernel truth | F13 |
| Persistent EvidenceRef claimed complete | F14 |
| Graduated observability semantics weakened | F15 |
