# DSH UniClaw Control-Plane Protocol Baseline — Graduation Review Record

> Status: GRADUATED (INDEPENDENT REVIEW) | Decision: `PROJECT_LEADER_DSH_UNICLAW_CONTROL_PLANE_PROTOCOL_BASELINE_REVIEW` | Date: 2026-08-15
> Maturity: `DSH_UNICLAW_CONTROL_PLANE_PROTOCOL_BASELINE_FROZEN`
> Scope: protocol/plugin integration architecture freeze only — NOT an authorization for plugin implementation, Shadow, Advisory, transport, or any Runtime/DSH production change.
> Change artifacts: `openspec/changes/dsh-uniclaw-control-plane-protocol-baseline/` (archived same day).

## Decision

`GRADUATED` — the DSH-native integration architecture for UniClaw is frozen enough to implement.
The next change (`dsh-uniclaw-control-plane-plugin-implementation`) can proceed without making new
architecture decisions.

## Independent Verification (this review re-checked source truth, not the reported claim)

- **DSH pin (PASS)**: re-verified from the READ-ONLY checkout — commit
  `47f943859bef60e4160492346772ded9b24f765a`, branch `master`, root version `0.1.0-rc.5`,
  `git describe` fails (no tags), remote `https://github.com/deepseek-ai/deepseek-harness.git`.
  `UNICLAW_DSH_COMPATIBILITY_BASELINE = 47f943859b…` matches exactly; design depends on pinned source, not latest.
- **SourceEvidenceMatrix (PASS, 43 rows S1–S43)**: independently re-verified this session against the
  pinned checkout: S1 (44 known event types, read-path refusal, `SESSION_FORMAT_VERSION=0`),
  S3 (append validation/deep-freeze/reentry, no known-set check at append), S4 (fork lineage),
  S5 (`session/event` at index.ts:76, `session/flush` declaration at :85 / impl `flush` at :1022),
  S7 (command handler "without sending the command to the model", index.ts:53–54/278),
  S13 (`TypertRemoteService` abstract, typert/protocol:147), S17 (`SessionProjectionRegistry`, session-projection:171),
  S19 (`SessionPersistence` abstract API + coordinator refusal at coordinator.ts:1063),
  S20 (checkpoint flush before first chunk), S21 (SDK maps, 3 requests / 4 notifications),
  S22 (ACP `agentInfo {name:'deepseek-harness-acp'}`), S23 (`GoalService extends TypertRemoteService`, goal:183),
  S25 (`WorkflowEngine` abstract, workflow:157), S26 (`SubagentRuntime` + `registerProvider`, subagent:171/369),
  S34 (sandbox/approval rows in cordis.patch.yml), S39 (`UserQuestionService`, user-questions:51),
  S40 (hooks `hook/invoked`+`hook/result` log-only; `updatedInput` parsed but NOT honored, types.ts:132;
  Codex blocks-only), S41 (plan mode "there is no live mirror", plan-mode:182),
  S42 (`CordisDynamicPluginId/PackageId/RunId`, cordis-host-runner:26/49–59),
  S43 (`fs/write-intent` waterfall, fs:58). Remaining rows were verified by the four settled audit
  subagent reports (fd265913, 2df1054d, 171db725, 16e619f2) with exact line numbers. Semantics,
  durability, lifecycle, and model-facing classifications match source.
- **DiscrepancyRegister (PASS, 11 rows D1–D11)** — disposition:

  | # | Classification | Disposition |
  |---|----------------|-------------|
  | D1 | DOCUMENTATION_DRIFT | verified: 5 dispatch modes (`vendor/cordis/src/events.ts:32`) |
  | D2 | DOCUMENTATION_DRIFT | Plugin.Function vs Plugin.Object shape |
  | D3 | NAMING_DRIFT | doc labels ≠ source event names; design does not use doc labels |
  | D4 | SOURCE_LOCATION_DRIFT | declaration :85 vs impl :1022; doc anchor is the declaration (cosmetic) |
  | D5 | DOCUMENTATION_DRIFT | root `docs/persistence.md` does not exist |
  | D6 | DOCUMENTATION_DRIFT | `host.call` = dynamic-runner seam only; static modules use Typert + `connection.rpc.call` |
  | D7 | DOCUMENTATION_DRIFT | pre-release stance (`SESSION_FORMAT_VERSION=0`, no tags) — status note, not a defect |
  | D8 | SEMANTIC_DIFFERENCE | hooks asymmetry is real; **zero protocol-risk** because the design classifies hooks as inbound automation (S40) and builds no control path on them |
  | D9 | SOURCE_LOCATION_DRIFT | `timeoutMs` carried in registry; enforcement in separate guard plugin — field semantics match |
  | D10 | DOCUMENTATION_DRIFT | config-catalog.md is generated; source types authoritative |
  | D11 | NAMING_DRIFT | vendor/README version table intentionally diverges from vendored package.json |

  **UNRESOLVED_PROTOCOL_RISK = 0.**

- **Session-event architecture (SessionEventsAreCordisEvents = MUST_BE_NO, verified)**:
  the session log is a plain event-sourced class; delivery is a single Cordis emit `session/event`
  (declared in `SessionStore`, index.ts:76) that fans out to three paths —
  (A) in-process listeners (persistence/telemetry subscribe),
  (B) SDK server `ctx.on('session/event')` → `transport.notify('session.event')` (server.ts:71–73),
  (C) apiproxy mux `ctx.on('session/event')` → `session/event` WS frames (api-proxy.ts:1350, 3475–3493).
  Implication verified: a plugin-appended valid session event reaches durable persistence, live
  listeners, and the browser through existing seams — **no new UniClaw event emitter needed**.
  → `NewRuntimeSemanticEmittersRequired = NO` confirmed.
- **Known-set refusal constraint (new disposition, non-blocking)**: `append` does NOT check
  `KNOWN_SESSION_EVENT_TYPES` (verified in `Session.append`), but the persistence read path refuses
  unknown non-`ignorable` types on reload (`coordinator.ts:1063` `assertEventsSupported` →
  `SessionFormatUnsupportedError`). Out-of-repo plugin-declared types are outside the 44 by
  construction (generated from in-repo `SessionEventMap` members; a registration surface is deferred
  until such a consumer exists). Resolution (frozen policy for step 3): durable plugin-declared
  Kernel-fact records MUST be `ignorable: true` (control-plane records, never surface-interpreted,
  never model-visible — semantically consistent with the authority model) OR wait for the deferred
  registration surface. This is a durability-mechanism decision, not an architecture decision; the
  constraint is recorded in matrix S1/S2.
- **Stale cross-references (disposition, non-blocking documentation drift)**: proposal.md:34
  ("7 条差异" — register grew to 11), tasks.md:52 and design.md:20 ("D1–D7" — now D1–D11),
  design.md:198 ("sequence §13 of proposal.md" — sequence lives in design §19). Cosmetic only; the
  authoritative register and sequence are correct. Frozen as-is per this gate's REPAIR-FORBIDDEN mode.

## Frozen Decisions (confirmed by this review)

- **Authority planes**: COGNITIVE/CONTROL = DSH · EXECUTION/STATE = UniClaw Kernel ·
  REALITY/TRUTH = External World. DSH proposal ≠ Kernel authorization; DSH session fact ≠ Container
  truth; DSH UI state ≠ world truth; DSH tool result ≠ GoalEvidence. No mapping contradicts.
- **DriverHost boundary**: UNICLAW-SIDE integration boundary — read RuntimeEvent/RunSnapshot,
  resolve EvidenceRef, approved Kernel controls, future proposal admission, protocol adaptation.
  MUST NOT become cognitive brain / Container-Traversal owner / generic workflow engine /
  generic provider framework / second mutable Runtime state.
- **dsh-plugin-uniclaw boundary**: DSH-facing only — plugin lifecycle, command/tool/service
  registration, session/live events, client-module hooks, config, translation to DriverHost.
  MUST NOT own Kernel/Container/device state, GoalEvidence, completion.
- **Observability mapping**: §4 classification A/B/C/D/E/G; RunCompleted/RunFailed/TrapRaised = A
  (durable, buyer-confirmed); high-volume families C/D/E live-only; GoalEvidenceProduced partial =
  C/D, DSH never creates GoalEvidence (F6); no "mirror every RuntimeEvent" design; durable copies
  are provenance-marked projections. Graduated RuntimeEvent/RunSnapshot/EvidenceRef contracts are
  consumed, not reshaped (F15 safe).
- **Human control**: commands registry, handler-without-model (S7) — 0 model tokens for
  Start/Pause/Resume/Stop/Abort/Inspect/Open Evidence/Retry/Recovery/Submit Goal. Token economy I-15
  preserved: structured read model → deterministic command → bounded evidence retrieval → model only
  with buyer.
- **Command vs tool boundary**: human/UI → command/service seam; model-facing cognitive capability →
  tool/agent-native seam, buyer-gated; no UI button invokes an LLM tool unnecessarily; no tool
  exposes ADB / raw coordinates / Environment.Execute / Container or StateBelief mutation /
  GoalEvidence creation / completion mutation.
- **Transport**: `TRANSPORT_DEFERRED` — DSH native seams are in-process (Cordis), browser
  (S15/S16), or inbound automation (S21/S22); no source pressure for a custom carrier; adapter
  boundary decides in the implementation phase. F9 safe.
- **Process lifecycle**: DriverHost owns its own process; plugin CONNECTS, never launches/supervises;
  no DSH-plugin-crash ⇔ Kernel-state-loss and no DSH-restart ⇔ Kernel-truth-reset coupling.
- **Shadow insertion point**: dsh-plugin-uniclaw live event hook (S6/S33) on Kernel-fact delivery;
  DSH reads truth → cognizes → records DSH-side artifact (session event / projection unit);
  Kernel consumption of DSH output = ZERO. Not implemented.
- **Parallel protocol**: MUST_BE_NO — every required semantic maps to a DSH-native surface;
  DecisionRequest/DecisionResponse/HealingRequest/DiagnosisRequest appear only as rejected
  candidates. Protocol-gap candidates are PROTOCOL_PRESSURE only (B-class/C-class emitters,
  full GoalEvidence sequence, Container/Observation read models, persistent EvidenceRef, transport),
  each with buyer-absent + authority analysis + deferred implementation.
- **Runtime deltas**: RuntimeSemanticModelChangeRequired = NO, RuntimeAgentRefactorRequired = NO,
  DirectDSHPhysicalAuthority = NO, GoalEvidenceAuthority = KERNEL_ONLY.

## Validation Evidence (final)

- `openspec validate dsh-uniclaw-control-plane-protocol-baseline --strict --no-interactive` → valid (exit 0), run fresh this review.
- `scripts/check-consistency.sh` → ALL PASS (C1–C10), run fresh this review.
- OpenSpec truthfulness: proposal/design/spec/tasks/matrices contain no overclaim — no plugin, Shadow,
  Advisory, emitter, transport, or persistent-Kernel-mirror is claimed; MODIFIED/REMOVED = None.

## Next Change

`dsh-uniclaw-control-plane-plugin-implementation` — minimal slice: DSH plugin lifecycle +
DriverHost connection/integration + read-only RuntimeEvent/RunSnapshot/EvidenceRef consumption +
deterministic human control seam. No Shadow cognition in the first slice without a separate
approved OpenSpec. Durability policy for plugin-declared facts: `ignorable: true` records
(per the known-set refusal disposition above).
