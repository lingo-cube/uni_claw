# Graduation Decision: dsh-shadow-cognition

> Gate: `PROJECT_LEADER_DSH_SHADOW_COGNITION_GRADUATION_REVIEW`
> Mode: `INDEPENDENT_EPHEMERAL_SHADOW_GRADUATION_REVIEW`
> Date: 2026-08-15
> Decision: **GRADUATED** — Maturity `DSH_SHADOW_COGNITION_INTEGRATED`
> Pinned DSH baseline: `47f943859bef60e4160492346772ded9b24f765a` (`0.1.0-rc.5`), checkout clean.

## 1. Original durable baseline and its falsification

The original design baseline (D5) proposed `DSH_NATIVE_DURABLE` Shadow output: a
durable `shadow/analysis` session event carrying `ignorable: true`, written
through live `Session.append` and observed by session persistence. The baseline
was falsified by pinned-source audit and live probes (source-evidence-matrix.md
M13–M20):

- `Session.append` in the pinned core cannot set the `ignorable` marker
  (envelope type accepts it, but there is no supported live-writer path for an
  out-of-repo plugin).
- A live-appended unknown event makes the session log refuse to load on cold
  reload (`SessionFormatUnsupportedError`).
- A direct `sessionPersistence.append` bypasses the authorized fanout, breaks
  sequence coupling, and a live probe (Path C) silently dropped the
  `command/done` event.

## 2. EPHEMERAL rebaseline

`PROJECT_LEADER_DSH_SHADOW_COGNITION_DURABILITY_REBASELINE_DECISION`
(`BASELINE_ASSUMPTION_FALSIFIED`, 2026-08-15) froze V1 to:

- **Durability:** `EPHEMERAL_PROCESS_LOCAL` — Shadow appends ZERO custom
  session events; no `shadow/analysis` event exists; no `ignorable` marker is
  needed because nothing is appended.
- **Triggers:** human request ONLY; `run.failed` / `run.completed` auto
  triggers DEFERRED (`shadow.autoTriggers` reserved and validated `[]`).
- **Cache:** bounded process-local `Map<runId, ShadowAnalysis>` (max 20,
  insertion-order eviction), convenience only, non-authoritative, never a
  Memory/Knowledge Store/History Database; restart loses analyses truthfully.

## 3. Real pinned DSH integration

`dsh-plugin-uniclaw/test/real-host-shadow.test.mjs` (included in `npm test`,
6/6) boots the REAL pinned DSH host — `@deepseek-ai/dsh-app-boot` `boot()`,
real vendored Cordis 4.0.1 loader, real `@deepseek-ai/dsh-commands` registry,
real `@deepseek-ai/dsh-llm` (LlmRuntime), real `Session` identity — with the
real `dsh-plugin-uniclaw` plugin configured for `shadow.model.provider`. Only
the model backend is substituted (a real `LlmAdapter` subclass registered via
`llm.registerAdapter(['test-shadow'], fake)` behind the real `ctx.llm` seam).
The test refuses to run unless the pinned checkout is at the exact HEAD with
empty porcelain. A wire-conformant loopback DriverHost fixture records every
RPC method requested (F8 proof).

## 4. Command and session identity semantics

- `uniclaw-shadow-analyze` is registered on the REAL DSH command registry;
  registry view carries exactly five commands
  (`uniclaw-evidence-open`, `uniclaw-inspect-run`, `uniclaw-inspect-trap`,
  `uniclaw-runs-list`, `uniclaw-shadow-analyze`). No mutating commands exist.
- `recordInput: true` means the pinned `commands.execute` includes
  `args: <rawInput>` in the `command/run` lifecycle event data — normal DSH
  command semantics; it creates NO custom Shadow event.
- Session identity is read truthfully from `invocation.agent.session.id`
  (a real detached `Session` object used as the command invocation session —
  the same shape `commands.execute` accepts; it implies no detached Shadow
  persistence convention). Missing identity → command refuses to invent one.
- `sessionId != runId` always (F14); no Kernel run is ever created from a
  session.

## 5. Context limits

Frozen and enforced in production assembly (`src/shadow/context.js`):
`MaxEvents = 200` (recent-window slice), `MaxContextChars = 80000`
(deterministic trimming: drop oldest non-priority events, then hard-slice;
final text NEVER exceeds the cap), `MaxEvidenceRefs = 8`
(locator-deduped), `evidenceBytesPerRef = 8192` (lazy resolution, metadata
only). Overflow is attacked by F11 (300-event window) — bounded end-to-end
and deterministic across runs.

## 6. Model seam

`ctx.llm` (`LlmRuntime.stream(GenerateOptions)`) only; provider/model are
DSH-config-owned (`shadow.model.provider` / `shadow.model.model`). One-shot:
exactly 0-or-1 calls per analysis, no retry/agent loop/tool loop/reflection/
planner cycle, no loop marker, `purpose` unset, ZERO model-facing tools
(GenerateOptions carries no `tools`). No provider SDK is imported anywhere in
the plugin. Model-call accounting records trigger, evidence refs, input event
count, context chars, status, timestamps. Failures map truthfully against the
pinned seam: `NO_ADAPTER` finish chunk → `not-configured` +
`model-unavailable`; other finish errors → `model-error`; timeout →
`model-timeout`; caller abort → `aborted`; malformed/empty output fails closed
at the interpretation layer (never promoted to structured facts).

## 7. Zero custom session events

One invocation generates exactly `command/run` + `command/done` (asserted on
the real session's `events` view after real execution). No
`shadow/analysis` / `shadow/*` event exists. `CustomSessionEventsWritten = 0`;
`SessionPersistenceUsed = NO`. Static guards: zero matches for
`session.append` / `sessionPersistence` / `PersistenceCoordinator` /
`Session.create` / `.flush(` / `.append(` in plugin source.

## 8. Restart semantics

Cache is process-local; full plugin/ctx dispose + fresh boot → cache empty,
old analysis not reconstructed, Kernel unchanged, and a fresh human request
recomputes a fresh analysis (rebased SHADOW-F7; zero-custom-event reload
safety = rebased SHADOW-F15).

## 9. Falsifier suite F1–F16

All PASS (shadow.test.mjs 41/41 + architecture-guards.test.mjs 3/3 +
real-host-shadow.test.mjs 6/6):

- F1 trap focus → TrapRaised cited, trap detail bounded, lazy evidence
- F2 RunFailed → failure in bounded evidence, hypotheses stay `shadow-inference`
- F3 missing data → `missing-data` uncertainty
- F4 unresolvable EvidenceRef → `unresolved-evidence-ref` uncertainty
- F5 timeout → `model-timeout`, calls ≤ 1, no retry
- F6 model error → `model-error`, deterministic digest, no fabricated hypotheses
- F7 restart → ephemeral result lost truthfully, fresh reanalysis succeeds
- F8 authority firewall → action-like model text stays text; zero DriverHost
  mutation RPCs, zero DeviceAction/ADB/Kernel mutation
- F9 no GoalEvidence creation path (static + behavioral)
- F10 no Container/Binding/StateBelief mutation (static)
- F11 bounds enforced under adversarial large inputs
- F12 visual disabled → zero image input (text blocks only)
- F13 fact/hypothesis classification survives to human command output
- F14 `sessionId != runId`, both retained correctly
- F15 zero custom session events + reload unaffected
- F16 zero new Runtime semantic emitters / zero Runtime modifications
  attributable to Shadow / zero new wire methods

## 10. Authority firewall

Kernel consumes ZERO Shadow output; there is no Shadow→Kernel mutation path.
The adapter's wire table is EXACTLY the frozen 8 read-only methods; the Shadow
facade narrows to 4 of them (snapshot, events, trap, evidence metadata).
`src/UniClaw.Runtime` and `src/UniClaw.Runtime.DriverHost` carry zero
shadow-related content (guarded; only the pre-existing Phase 0-3 modified set
is present). GoalEvidenceAuthority remains KERNEL_ONLY (frozen epistemic
language only). Direct DSH physical authority (ADB/PhysicalEnvironment/
DeviceAction) is absent.

## 11. Remaining deferred buyers

- **A.** `dsh-shadow-durability-extension` (deferred pressure; needs a real
  human/product buyer + a new persistence design)
- **B.** `dsh-advisory-cognition` (explicitly out of scope; Kernel would keep
  final authority)
- **C.** human/UI Shadow consumption (a new frontend is out of scope)
- **D.** no immediate cognition expansion

Project Leader chooses the next buyer.
