# Design: DSH UniClaw Control-Plane Protocol Baseline

> Change: `dsh-uniclaw-control-plane-protocol-baseline`
> Mode: `SOURCE_FIRST_ARCHITECTURE_AUDIT_AND_OPENSPEC` — analysis and freezing only, zero implementation.
> THIS FILE IS A MAP, NOT A MANUAL: it records source-verified seams and frozen decisions; it does not specify a plugin.

---

## 0. Pinned Baseline

```
UNICLAW_DSH_COMPATIBILITY_BASELINE = 47f943859bef60e4160492346772ded9b24f765a
DSHVersion                          = 0.1.0-rc.5 (pre-release; no git tags on pinned checkout; release commit abe560f81e in history)
DSHRepository                       = https://github.com/deepseek-ai/deepseek-harness.git (branch master)
Audit checkout                      = /Users/fran/Documents/Code/dk-harness @ 47f943859b (READ-ONLY)
```

- **SOURCE IS AUTHORITATIVE.** Every mapping in this document traces to a row of
  [source-evidence-matrix.md](source-evidence-matrix.md) verified against the pinned checkout.
  Docs-vs-source discrepancies (D1–D7) are recorded; design never follows latest docs over pinned source.
- Pre-release stance: `SESSION_FORMAT_VERSION = 0`, no compatibility promise. Forward-compat of this baseline
  is explicitly out of scope; a future baseline re-pin is a separate change.

## 1. Authority Planes (frozen)

| Plane | Owner | Consequence |
|-------|-------|-------------|
| COGNITIVE / CONTROL | DeepSeek Harness | DSH proposes, interprets, plans, orchestrates; DSH commands/tools/services/events are control-plane surfaces |
| EXECUTION / STATE | UniClaw Kernel | Kernel owns run execution state, Container state, Traversal state, bindings, grounding, authorization, physical dispatch, post-action verification, recovery validity, GoalEvidence, completion |
| REALITY / TRUTH | External World | ADB/device truth is the world's, observed only through Kernel |

Identity rules (frozen, from the directive):
- DSH proposal ≠ Kernel authorization
- DSH plan ≠ physical execution
- DSH belief ≠ Container truth
- DSH tool result ≠ GoalEvidence
- DSH completion opinion ≠ Kernel completion

## 2. Architecture and Component Roles (frozen)

```
External World
   ▲  (Kernel observes/acts, reality is truth)
UniClaw Kernel (Agent → Container → Traversal → Environment; 14 invariants)
   ▲  read-only projections: RuntimeEvent / RunSnapshot / EvidenceRef (graduated, frozen)
DriverHost  = UNICLAW-SIDE INTEGRATION BOUNDARY (frozen role, see §3)
   ▲  (adapter translation; direction both ways at DSH boundary)
dsh-plugin-uniclaw  = DSH-SIDE INTEGRATION PLUGIN (frozen role, see §4)
   ▲  (DSH-native surfaces: plugin rows, commands, tools, services, events, slots)
DeepSeek Harness (Agent / Models / Workflows / Subagents / Sessions / Commands / Tools / Services / Events / Client / UI)
```

### 2.1 DriverHost role (frozen; UNICLAW-SIDE)

- **Current (graduated, unchanged):** hosts read-only projections of Kernel facts — RuntimeEvent stream,
  RunSnapshot read model, EvidenceRef logical resolution metadata. No cognition, no control verbs.
- **Future bounded (DriverHost-side work, NOT purchased here):** Kernel control operations
  (start/pause/resume/stop/abort/retry/recovery entry), DSH proposal admission, freshness/staleness
  validation entry, protocol adaptation.
- **MUST NOT become:** cognitive brain, agent replacement, Container/Traversal owner, generic workflow
  engine, generic AI-provider registry, second mutable WorldState, generic plugin platform.

### 2.2 dsh-plugin-uniclaw role (frozen; DSH-SIDE)

- Owns DSH-facing integration: plugin lifecycle (composition rows, S27/S28/S31), DSH command registration
  (S7), DSH tool registration (S9, buyer-gated), DSH service registration (S13 read-only RunSnapshot service),
  DSH event hooks (S6/S33 plugin-owned live events), DSH client-module registration (S11/S12 Control-Plane UI),
  DSH-side config (S35), translation to/from DriverHost (adapter).
- **MUST NOT own:** Kernel state, Container state, physical device state, GoalEvidence, Runtime completion.
- Placement decision: shared registries/services/events → HOST composition rows; per-run scoped
  tools/prompt/persona → agent preset rows under `$DSH_HOME/.agent-presets/` (S27/S28, two-plane rule).
  A plugin-provided `uniclaw` service with `declare module` augmentation (S32) is the host-plane anchor.

## 3. Extension-Point Audit (summary; full rows in source-evidence-matrix.md)

| DSH Extension Point | Verified Source | Freeze |
|---------------------|-----------------|--------|
| Durable session event vocabulary (merge-extensible `SessionEventMap`) | S1/S2/S3 | Native seam for plugin-declared Kernel-fact events; plugin owns the merge declaration |
| Live session/agent events (`session/event`, `agent/*`) | S5/S6 | Plugin hooks for live Kernel-fact delivery; Shadow insertion point |
| Commands registry (deterministic, model-free) | S7/S8 | Human control ops; durable `command/run`/`command/done` |
| Tools registry + guard waterfalls | S9/S10 | Model-facing tools (buyer-gated); permission gate |
| Typert Remote service | S13 | Read-only RunSnapshot service for browser/RPC |
| Session projection registry + `session/projection` push | S16/S17 | Live read-model push; derived-where-marked, never authoritative |
| Client slots + client-module registry | S11/S12 | Control-Plane UI panels |
| Storage domains | S18 | Plugin-owned persisted metadata (if buyer later) |
| Session persistence + checkpoint policy | S19/S20 | Durable session facts ride existing persistence |
| Goal machinery (service/tools/command/projection) | S23/S24 | Goal-lifecycle reuse decision deferred |
| User questions (human-in-the-loop) | S39 | Structured ask seam (trap inspection/recovery consent); answers not durable unless buyer-gated |
| Hook protocol (Claude Code/Codex bridges) | S40 | Inbound automation only; NOT a Kernel control carrier (D8) |
| Plan mode (logged-state precedent) | S41 | F13 precedent: UI reads committed `plan/mode` flips, no live mirror |
| Dynamic plugins (`@pluginId` seam) | S42 | Implementation-phase seam for runtime-defined plugins; composition rows + presets suffice for this baseline |
| FS permission seam | S43 | DSH file mutation rides its own policy gate (`fs/write-intent`); device dispatch stays Kernel-exclusive |
| Workflow engine + subagents | S25/S26 | Later orchestration |
| Composition/presets/boot/config | S27–S31, S35 | Plugin rows + preset rows + config layering |
| Sandbox/approval/permission | S34 | DSH→Kernel ops ride machine policy gate |

## 4. Observability → DSH Mapping (Kernel read-only truth → DSH surfaces)

Classification legend (from directive §8): **A** durable DSH session fact · **B** live DSH event ·
**C** plugin-owned transient event · **D** service/read-model only · **E** UI-only projection ·
**F** not exported · **G** deferred.

| RuntimeEventKind | Classification (frozen) | Buyer / Rationale | DSH Evidence |
|------------------|--------------------------|-------------------|--------------|
| ObservationProduced | **C/D** (live plugin event + service read-model; NO durable copy) | High-volume; no durability buyer; token economy (I-15) | S6, S13 |
| ContainerReconciled | **C/D** (live; read via RunSnapshot where available) | High-volume; `CurrentContainerSummary` is NOT_CURRENTLY_AVAILABLE — keep absent | S6, S13 |
| ActionDispatched | **C/D** (live; RunSnapshot `LastAction` DERIVED_READ_MODEL) | Dispatch log durable copy G (no concrete audit buyer yet) | S6, S13 |
| NavigationDecision | **C/D** (live; `LastDecision` read via service; NOT model-visible by default) | Reasoning artifact; model context deferred (I-15) | S6, S13 |
| ViewportExplorationDecision | **C/E** (live; UI projection of available progress data) | Exploration telemetry; UI-only | S6, S17 |
| TrapRaised | **A/B** (durable + live) | Buyer CONFIRMED: human inspection, pause/resume context, post-run audit; low-frequency high-value | S1–S3, S6, S7 |
| RecoveryStarted | **C** live; durable copy **G** | Recovery audit buyer not yet concrete | S6 |
| GoalEvidenceProduced (partial) | **C** live + **D** partial read (RunSnapshot `LatestGoalEvidence`); DSH NEVER creates GoalEvidence (F6) | Full source sequence absent → PROTOCOL_PRESSURE | S6, S13 |
| RunCompleted | **A/B** (durable + live) | Buyer CONFIRMED: run closeout, audit, model-context reconstruction, human inspection | S1–S3, S6 |
| RunFailed | **A/B** (durable + live) | Buyer CONFIRMED: same as RunCompleted + diagnosis entry | S1–S3, S6 |

Rules:
- Durability is purchased ONLY where a buyer is listed above (F14/F15 discipline: no silent claims, no
  weakening of graduated semantics). High-volume telemetry stays live-only.
- Durable copies are **projections of Kernel-reported facts**, appended by the plugin via `Session.append`
  (S3) with the RuntimeEvent envelope carried as data — never re-derived, never invented, never authoritative.
- The plugin-declared session event vocabulary (S1) is the ONLY new vocabulary; it extends the DSH-native
  merge-extensible union, so no parallel wire protocol exists (F4).

## 5. RunSnapshot → DSH Read Paths (read-only, frozen)

| Read Path | DSH Surface | Semantics | Authority | Status |
|-----------|-------------|-----------|-----------|--------|
| Canonical read | `UniClawRunService extends TypertRemoteService` read-only `remoteExport*` methods (S13) | Browser/RPC reads fresh snapshot on demand | Kernel truth; derived fields flagged | `NATIVE_DSH_SEAM_WITH_ADAPTER` |
| Live push | `uniclaw.runSnapshot` projection unit (S17) → `session/projection` frames (S16) | Fold of plugin-appended session facts → validated wire payload; higher-seq-wins in client | Projection, never authoritative | `NATIVE_DSH_SEAM_WITH_ADAPTER` |
| Human inspect | `/uniclaw inspect` command result (S7) | Deterministic structured text | Kernel truth | `NATIVE_DSH_SEAM_CONFIRMED` |
| UI data source | session-scoped slot (S12) consuming service/projection | Control-Plane UI panel | UI = projection (F13) | `NATIVE_DSH_SEAM_WITH_ADAPTER` |

- **No second mutable DSH-owned Runtime state** (F7): all copies are read-only projections or fold-derived
  caches (`session_projcache`), never a writable mirror the DSH owns as truth.

## 6. EvidenceRef → DSH Mapping

- **Logical identity preserved** (never filesystem path identity; F14 discipline): EvidenceRef rides as
  logical id in session event data (S1/S2) and in structured command/tool payloads (S7/S9).
- **Metadata first, structured evidence first, raw evidence lazy/on-demand**: `/uniclaw evidence <ref>`
  returns metadata; raw evidence (screenshots/trace bundles/observations) is never pushed into model context
  by default (I-15; token economy §9).
- **Persistent EvidenceRef resolution: DEFERRED** — the graduated observability contract does not expose it;
  this baseline records the gap and does not claim completeness (F14).

## 7. Human Control → Kernel (deterministic, model-free; F8)

| Op | DSH Surface | Model tokens | Kernel side | Status |
|----|-------------|--------------|-------------|--------|
| Start Run | `/uniclaw start` command (S7) | 0 | DriverHost future Kernel control op (deferred) | `NATIVE_DSH_SEAM_CONFIRMED` |
| Pause / Resume / Stop / Abort | `/uniclaw pause|resume|stop|abort` commands | 0 | deferred | `NATIVE_DSH_SEAM_CONFIRMED` |
| Inspect Run / Trap / Evidence | `/uniclaw inspect|trap|evidence` commands | 0 | read-only | `NATIVE_DSH_SEAM_CONFIRMED` |
| Retry / Request Recovery | `/uniclaw retry|recover` commands | 0 | Kernel validates against fresh state | `NATIVE_DSH_SEAM_CONFIRMED` |
| Submit Requirement / Goal | `/uniclaw requirement|goal` commands | 0 (mechanical submit) | Kernel admission is Kernel authority | `NATIVE_DSH_SEAM_CONFIRMED` |

- Command handlers execute against the receiving agent **without sending the command to the model**
  (S7 verified from source) — the exact F8-required seam: human deterministic control never needs an LLM turn.
- `command/run` + `command/done` durable events give a complete control audit trail (S8).
- The Kernel-side control surface (DriverHost bounded future work) is **not purchased here**; the DSH seam
  exists and is frozen, execution is deferred with the named buyer (the plugin-implementation change step 3
  will define the DriverHost admission contract; Kernel-side op remains Kernel authority).

## 8. Cognitive Ops → DSH (model involved; Kernel authority preserved)

| Op | DSH-native trigger/output | Proposal representation | Kernel buyer | Status |
|----|---------------------------|--------------------------|--------------|--------|
| Requirement interpretation | DSH agent loop (LLM) on user message | Plugin-declared session event (S1) | DriverHost admission → Kernel | `NATIVE_DSH_SEAM_CONFIRMED` |
| Goal proposal | `/uniclaw goal` command OR GoalService (S23) — decision deferred to step 3 with buyer | Durable command/goal events | Kernel goal admission | `NATIVE_DSH_SEAM_CONFIRMED` |
| Decision proposal | DSH session event (plugin-declared type) carrying proposal + basis refs | Session-event envelope (no custom DecisionRequest) | DriverHost admission → Kernel fresh-state revalidation → Kernel decision | `NATIVE_DSH_SEAM_WITH_ADAPTER`; Kernel `DecisionProposed` emitter = PROTOCOL_PRESSURE (C-class, not purchased) |
| Diagnosis | DSH agent analysis over bounded read-only context; optional subagent (S26) | Diagnosis artifact session event | Human inspection + recovery prep | `NATIVE_DSH_SEAM_CONFIRMED` |
| Healing candidate | DSH proposal + optional workflow orchestration (S25) | Session event → DriverHost admission → Kernel revalidation | Kernel validates healing | `NATIVE_DSH_SEAM_CONFIRMED` |
| Workflow orchestration | `ctx.workflowEngine` + `workflow` tool (S25) + subagents (S26) | native | never touches world directly | `NATIVE_DSH_SEAM_CONFIRMED` |

- **No custom envelopes frozen** (DecisionRequest/DecisionResponse/HealingRequest/DiagnosisRequest):
  the merge-extensible SessionEventMap IS the DSH-native vocabulary seam (F4 satisfied). A proposal is a
  session event of a plugin-declared type; the Kernel side decides via DriverHost admission.
- **Freshness**: every proposal's basis is revalidated against Kernel fresh state at admission
  (staleness entry is DriverHost's future bounded responsibility); DSH never assumes its snapshot is current.

## 9. Token Economy (I-15 preserved; frozen constraints)

- **Deterministic Information Acquisition Priority** (frozen): structured read model (S13 service) →
  deterministic service/command/tool (S7/S9/S10) → bounded evidence retrieval → model cognition ONLY with
  explicit buyer. Nothing in this baseline grants the model a default view of Runtime trace / RunSnapshot
  history / perception output / screenshots / EvidenceRefs / conversation.
- Human control ops: **0 model tokens** (commands, §7).
- DSH cognition receives **minimum sufficient context**; no entire-trace/snapshot/evidence dumps.
- No model-facing `uniclaw.*` tool is pre-approved (see §13 evaluation table); each needs Buyer/Purpose/
  Input/Output/Freshness/Kernel-validation/Authority/Token-evidence cost/Phase.

## 10. Protocol-Gap Policy (frozen)

1. A required semantic with a DSH-native seam → use the seam (no invention).
2. A required semantic with a seam needing translation → adapter inside dsh-plugin-uniclaw (never a new
   wire protocol; translation is shape conversion).
3. A semantic with no seam and no buyer → record as PROTOCOL_PRESSURE; never purchase speculatively.
4. A semantic with no seam and a concrete buyer → the buyer names the seam in a future change
   (sequence §13 of proposal.md); this baseline does not invent it.
5. DSH session events carry facts DSH is entitled to record (Kernel-reported projections); they never
   fabricate authority (F4/F5/F6/F7).

## 11. Shadow Insertion Point (frozen; NOT implemented)

- **Trigger**: dsh-plugin-uniclaw live events (S6/S33) fired when DriverHost delivers Kernel read-only
  facts (RuntimeEvent/RunSnapshot deltas).
- **Cognition**: (later `dsh-shadow-cognition` change) a DSH agent consumes read-only truth, performs
  cognition, records output as a plugin-declared session event and/or projection unit (S1/S17).
- **Isolation (frozen)**: Kernel execution is UNCHANGED by Shadow; Kernel consumption of DSH output is
  **ZERO**. Shadow output is a DSH-side artifact (recorded, reviewable, never fed back into the Kernel).
- Classification: session-event-append + projection; NOT a live control path.

## 12. Advisory Boundary (frozen; protocol design deferred to `dsh-advisory-cognition`)

- Future flow: DSH candidate → UniClaw Adapter → DriverHost admission → Kernel fresh-state revalidation →
  Kernel authority decision.
- This baseline confirms DSH native surfaces carry the required basis/provenance metadata: session events
  (S1/S2) carry data + refs; structured tool/command payloads (S7/S9) carry inputs/outputs. No Advisory
  protocol is designed here.

## 13. Model-Facing Tool Evaluation (evaluate; NOT pre-approve)

| Tool candidate | Buyer (current) | Purpose | Model required? | Freshness rule | Kernel validation | Authority | Token-evidence cost | Phase decision |
|----------------|------------------|---------|-----------------|----------------|--------------------|-----------|----------------------|----------------|
| `uniclaw.inspect_run` | UI/commands serve this deterministically (S7/S13); model buyer absent | Run inspection | No (tool exists, model use deferred) | read-on-demand | read-only | Kernel truth | low (snapshot) | `DEFERRED_NEEDS_BUYER` (model buyer = Shadow phase) |
| `uniclaw.get_snapshot` | same | RunSnapshot read | No | read-on-demand | read-only | Kernel truth | low | `DEFERRED_NEEDS_BUYER` |
| `uniclaw.get_evidence` | metadata-first retrieval | EvidenceRef metadata | No | lazy raw | read-only | logical identity | low metadata / high raw | `DEFERRED_NEEDS_BUYER` |
| `uniclaw.propose_goal` | command path covers human submit (S7) | Goal proposal | Yes (interpretation) | fresh-state revalidation at admission | Kernel goal admission | Kernel | bounded proposal | `DEFERRED_NEEDS_BUYER` |
| `uniclaw.propose_decision` | decision proposals ride session events (S1) | Decision proposal | Yes | fresh-state revalidation | Kernel decision | Kernel | bounded proposal + basis refs | `DEFERRED_NEEDS_BUYER` (Shadow/advisory phases) |
| `uniclaw.request_diagnosis` | diagnosis artifact via session event (S1) | Diagnosis | Yes | fresh-state revalidation | Kernel validates healing only | Kernel | bounded context | `DEFERRED_NEEDS_BUYER` |

**Hard-forbidden tool behaviors (frozen):** raw ADB execution, raw coordinate execution, Container
mutation, StateBelief mutation, GoalEvidence creation, direct RunCompleted mutation. Tools with no buyer
are rejected (none is approved in this baseline).

## 14. UI Mapping (DSH browser; frozen seams, no UI design)

- Client half of dsh-plugin-uniclaw registers session-scoped slots (S12) — Run Status, RunSnapshot,
  Evidence panels — following the `conversation.view` pattern (trajectory example).
- Data: Typert Remote service (S13) for reads; `session/projection` frames (S16/S17) for live push.
- **DSH UI state is a projection, NEVER Kernel truth** (F13). UI renders projection values with
  derived-where-marked labels; missing fields stay absent.

## 15. Transport Decision (frozen: `TRANSPORT_DEFERRED`)

Source-pressure analysis:
- DSH native seams are: in-process Cordis plugins (S27–S32), browser transport (`/api` fetch + WebSocket
  downlink, S15/S16), inbound automation (SDK JSON-RPC S21, ACP S22). **No DSH-native OUTBOUND IPC seam
  to an external .NET process exists.**
- The DSH↔DriverHost boundary is cross-process (.NET DriverHost ↔ Node DSH host). A carrier will be needed,
  but **no source pressure picks one in this baseline**; the graduated observability change deliberately
  made no transport choice; F9 forbids speculative custom transport.
- **Decision: `TRANSPORT_DEFERRED`.** The plugin-implementation change (step 3) selects the carrier at the
  adapter boundary against DriverHost's existing read surface, with options evaluated in this order:
  (1) DSH-native in-process (impossible cross-process — recorded as IN_PROCESS_NOT_AVAILABLE for this pair);
  (2) reuse of an existing DSH seam if DriverHost can act as a client of an inbound seam (SDK/ACP are
  DSH-as-server automation seams — direction mismatch for Kernel control, so not forced);
  (3) a minimal carrier defined by the adapter (only if 1–2 fail — a named decision with justification,
  still never a semantic protocol: payloads remain DSH session events / service methods).
- SDK/ACP are classified as automation-inbound seams (S21/S22), NOT control-plane carriers; no purchase.

## 16. Process Lifecycle (frozen: DriverHost owns its process)

- dsh-plugin-uniclaw **connects to** DriverHost; it does not launch, supervise, or persist the Kernel
  process (no architectural requirement makes DSH responsible for Kernel process durability).
- DriverHost process durability/lifecycle = UniClaw-side concern (Kernel + DriverHost deploy together).
- Plugin reconnect/freshness policy is config (S35) owned by the plugin, decided in step 3.

## 17. Falsifiers (this change FAILS if any holds)

| # | Falsifier | This change's posture |
|---|-----------|------------------------|
| F1 | DSH version not pinned | Pinned: §0 |
| F2 | Major mappings lack source evidence | Every mapping cites source-evidence-matrix rows |
| F3 | Runtime models reshaped to look like DSH schemas | Zero Runtime change; contracts untouched |
| F4 | Parallel protocol invented before exhausting native seams | No protocol invented; session-event vocabulary IS the seam |
| F5 | DSH direct physical dispatch | Hard-forbidden (§2.2, IntegrationMatrix) |
| F6 | DSH creates GoalEvidence | Hard-forbidden (§4, §13) |
| F7 | DSH/plugin becomes second mutable Runtime owner | All copies read-only projections (§5) |
| F8 | Human deterministic controls needlessly invoke models | Commands = 0 model tokens (§7, S7 source-verified) |
| F9 | Transport selected without source pressure | `TRANSPORT_DEFERRED` (§15) |
| F10 | Shadow implementation begins | Insertion point frozen only (§11) |
| F11 | C-class emitters purchased without concrete buyer | PROTOCOL_PRESSURE only |
| F12 | DriverHost becomes generic cognitive framework | Role frozen (§2.1) |
| F13 | DSH UI state treated as Kernel truth | UI = projection (§14) |
| F14 | Persistent EvidenceRef falsely claimed complete | DEFERRED, never claimed (§6) |
| F15 | Graduated observability semantics weakened | Contracts untouched; durability buyer-gated (§4) |

## 18. Graduation Criteria (this change GRADUATES when)

1. `UNICLAW_DSH_COMPATIBILITY_BASELINE` pinned and recorded (§0).
2. SourceEvidenceMatrix + IntegrationMatrix + DecisionTable complete, source-cited, READ-ONLY audit.
3. DriverHost role and dsh-plugin-uniclaw role frozen (§2).
4. Authority boundary frozen (§1) with hard-forbidden paths (§IntegrationMatrix).
5. Observability→DSH mapping frozen with buyer-gated durability (§4).
6. Human control mapping frozen on commands registry (§7); cognitive mapping frozen on session-event
   vocabulary (§8); Shadow insertion point frozen (§11); Advisory boundary frozen (§12).
7. Transport decision/defer explicit (§15); process lifecycle decision explicit (§16).
8. `openspec validate dsh-uniclaw-control-plane-protocol-baseline --strict --no-interactive` PASS and
   `scripts/check-consistency.sh` ALL PASS.
9. Target maturity `DSH_UNICLAW_CONTROL_PLANE_PROTOCOL_BASELINE_FROZEN` declared;
   next gate `PROJECT_LEADER_DSH_UNICLAW_CONTROL_PLANE_PROTOCOL_BASELINE_REVIEW`.
10. NOT required for graduation: plugin exists, control plane works, Shadow/Advisory/UI/transport exists.

## 19. Future Implementation Sequence (frozen)

1. ~~dsh-kernel-read-only-observability~~ (DONE, graduated)
2. **dsh-uniclaw-control-plane-protocol-baseline** (THIS change)
3. `dsh-uniclaw-control-plane-plugin-implementation` — plugin rows, commands, read-only service,
   projection unit, adapter, transport carrier decision, process-connect policy
4. `dsh-shadow-cognition` — Shadow agent consumes read-only truth, records DSH-side artifacts
5. `dsh-advisory-cognition` — advisory proposals via DriverHost admission + Kernel fresh-state revalidation
6. Bounded blocking seams ONLY IF later justified (never speculative)
