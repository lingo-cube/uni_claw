# Design: dsh-shadow-cognition

> Design-only baseline for the minimum DSH-native **Shadow Cognition**
> capability. Pinned DSH baseline: `47f943859bef60e4160492346772ded9b24f765a`
> (`0.1.0-rc.5`), read-only checkout. Source is authoritative; every seam cited
> here was verified against the pinned checkout (see `source-evidence-matrix.md`,
> rows M1–M12, plus the falsification evidence rows M13–M19). Nothing in this
> change re-opens a frozen decision from the three graduated prerequisites
> (read-only observability, protocol baseline, plugin implementation).

> **REBASELINE 2026-08-15** — `PROJECT_LEADER_DSH_SHADOW_COGNITION_DURABILITY_REBASELINE_DECISION`
> (`BASELINE_ASSUMPTION_FALSIFIED`). The original frozen baseline assumed
> `Session.append('shadow/analysis', …) → session/event fanout →
> PersistenceCoordinator → write-behind → session/flush` could produce a
> reload-safe durable event carrying `ignorable: true`. Pinned-source audit and
> real cold-reload probes falsified that assumption (pinned `Session.append`
> cannot set the marker; a live-appended unknown event persists non-ignorable
> and makes the whole session log refuse to load; the only marker-capable route
> is a direct `sessionPersistence.append` that bypasses the authorized fanout
> semantics, couples two write paths by exact seq, and has demonstrated silent
> event loss). **V1 durability is rebaselined from `DSH_NATIVE_DURABLE` to
> `EPHEMERAL_PROCESS_LOCAL`**: Shadow V1 appends ZERO custom session events and
> keeps only a bounded process-local cache. See §9 (rebaselined D5), the
> rebaselined falsifiers F7/F15 in §16, and
> `source-evidence-matrix.md` §Falsification evidence. The original buyer
> (post-hoc / near-live run interpretation) is unaffected: a human request can
> deterministically reconstruct fresh context from the Kernel read surface and
> run Shadow analysis again after a DSH restart.

## 1. Scope and gates

This gate is `SOURCE_FIRST_BUYER_DRIVEN_OPENSPEC_BASELINE`: it produces
OpenSpec artifacts only. Hard constraints honored:

| Constraint | Disposition |
|---|---|
| `Implementation` | FORBIDDEN — zero production code written |
| `ProductionMutation` | NONE — zero files changed outside `openspec/changes/dsh-shadow-cognition/` |
| `RuntimeChanged` | NO — zero files under `src/UniClaw.Runtime/` |
| `RuntimeAgentChanged` | NO |
| `DriverHostSemanticChanged` | NO |
| `NewRuntimeSemanticEmitters` | NO — the frozen `RuntimeEvent` surface is consumed as-is |
| `Advisory` / `BlockingCognition` / `PhysicalAuthority` | NO — Shadow only |
| `Archive` | FORBIDDEN — the change stays active, not implemented |

## 2. Shadow Definition (mission, direction, zero authority)

**Shadow Cognition** = DSH observes Kernel-produced evidence and run state →
optionally invokes cognitive/model reasoning → produces DSH-side
interpretation / hypothesis / recommendation artifacts → humans may inspect
them. Kernel consumes ZERO shadow outputs.

- Required information direction: Kernel → DriverHost observability →
  `dsh-plugin-uniclaw` → DSH Shadow Cognition.
- Forbidden direction: Shadow Cognition → Kernel decision/execution.
- `KernelInputFromShadow = ZERO`, enforced architecturally (not merely
  documented):
  1. The frozen wire contract (`dsh-uniclaw-control-plane-protocol-baseline`)
     contains **zero mutation methods** — only `ping`, `run.list`,
     `run.snapshot.get`, `run.trap.get`, `run.events.after`, `run.events.drain`,
     `evidence.get`, `control.support`. `start`/`pause`/`resume`/`stop`/`abort`
     were frozen `DEFERRED_NO_KERNEL_CONTROL_BUYER`. The plugin physically
     cannot dispatch a Kernel mutation.
  2. `dsh-plugin-uniclaw` owns no physical capability: dependencies are only
     Node builtins + the pinned `@deepseek-ai/cordis` peer. No ADB, no device
     libs, no Container/Traversal/GoalEvidence code exists or is added.
  3. `src/UniClaw.Runtime` is untouched (mechanical guard in future tests).
  4. DSH is a client; DriverHost is the server. Nothing DSH-side feeds Kernel
     state; Kernel has no consumer of shadow outputs.

Shadow has NO execution authority, NO authorization authority, NO GoalEvidence
authority, NO Container authority, NO StateBelief authority, NO Binding
authority, NO Runtime state-transition authority.

## 3. Buyer (D1)

**First buyer: POST-HOC / NEAR-LIVE RUN INTERPRETATION** (exact). The human
operator/developer inspecting a UniClaw run through DSH asks "what appears to
be happening, why, what blocks progress, what should I investigate next?"
Shadow answers with hypotheses, diagnostic summaries, candidate explanations,
and human-facing recommendations — never executable authority.

Example buyer questions Shadow may address: What appears to be happening in
this run? Why did the Agent choose the latest visible action? What evidence may
explain a Trap? What seems likely to be blocking progress? Is the observed run
behavior consistent with the current Goal? What should a human investigate
next?

## 4. Trigger model (D2)

V1 trigger set — exact, chosen from the actual DSH architecture (session-scoped
commands run on the DSH-native command registry; the plugin drains
`RuntimeEvent`s from DriverHost read surfaces only when a human requests it):

| Trigger | Kind | V1 status | Rationale |
|---|---|---|---|
| `human.request` — `uniclaw-shadow-analyze <runId> [--focus …] [--reason …]` | mandatory, always available | ON | The primary buyer; zero model cost until invoked |
| `run.failed` — terminal `RunFailed` RuntimeEvent observed on the drain | reserved vocabulary, NOT built | DEFERRED (`AutoTriggersDeferredUntilConsumerExists = YES`) | Without a durable or currently consumed native live Shadow surface, automatically generating ephemeral analyses has no clear human buyer |
| `run.completed` — terminal `RunCompleted` RuntimeEvent observed on the drain | reserved vocabulary, NOT built | DEFERRED | Same as `run.failed` |

- **V1 builds NO auto-trigger machinery** — `Do NOT build unused cognition.`
  `shadow.autoTriggers` remains in the config schema as a RESERVED key: V1
  validation requires it to be the empty list `[]`; a non-empty value is a
  configuration error naming the deferral. The `run.failed`/`run.completed`
  vocabulary stays recorded only so a future buyer can re-introduce it
  deliberately.
- NO auto invocation on `TrapRaised`, `RecoveryStarted`, or any other
  `RuntimeEvent` in this baseline: there is no buyer for automatic model
  invocation on non-terminal transitions, and trap interpretation is served by
  the human request (`--focus trap`) plus the deterministic
  `uniclaw-inspect-trap` command.
- Human requests always produce a new analysis (no dedupe for human.request).
  The `(runId, trigger)` dedupe rule applies only to auto triggers, which do
  not exist in V1; when auto triggers are re-introduced by a future buyer, at
  most one analysis per `(runId, trigger)` pair, with no per-event invocation.

## 5. Source inputs (read-only, graduated only)

Consumed surfaces (all frozen in the graduated baseline; zero new emitters):

- `RunSnapshot` (classification-preserving, `directPublicProjection` from
  Agent.State), via `run.snapshot.get`.
- `RuntimeEvent` pages (run-scoped cursors, stable `EventId`, `Sequence`),
  via `run.events.after` / `run.events.drain`. Available classes include
  `ObservationProduced`, `ContainerReconciled`, `ActionDispatched`,
  `NavigationDecision`, `ViewportExplorationDecision`, `TrapRaised`,
  `RecoveryStarted`, `GoalEvidenceProduced`, `RunCompleted`, `RunFailed`.
- `EvidenceRef` (logical locator only), via `evidence.get` — resolved lazily.
- `run.trap.get` (trap detail) when focus is trap.
- `control.support` — read-only audit table only (frozen), never control.

**Unavailable data is a real contract.** C-class decision/authorization events
and other partial data (e.g. full GoalEvidence freshness source,
`CurrentObservationSequence`, `CurrentContainerSummary`, `BindingsSummary`,
`StateBeliefsSummary`) are NOT available. Rule: unavailable evidence →
explicit uncertainty in the artifact; never fabricated inference presented as
Kernel fact.

## 6. Evidence hierarchy (five categories, never collapse)

| Category | Meaning | Where it appears |
|---|---|---|
| KERNEL FACT | Verbatim from a `RuntimeEvent` / `RunSnapshot` field | `ObservedFacts[].classification: 'kernel-fact'` with ref |
| DERIVED READ MODEL | Computed by DriverHost projection (e.g. snapshot `classification: 'directPublicProjection'`) | `ObservedFacts[].classification: 'derived-read-model'` with ref |
| EVIDENCE REF | Logical locator (`RuntimeEvent` id, snapshot field, `EvidenceRef` locator) | `EvidenceRefs[]`, `ObservedFacts[].ref`, `Hypotheses[].supportingRefs` |
| SHADOW INFERENCE | Model/DSH interpretation — explicitly NOT Kernel fact | `Hypotheses[]` (each flagged, never presented as fact) |
| SHADOW RECOMMENDATION | Human-facing suggestion; zero executable authority | `Recommendations[]` (target: human investigation) |

Example (from the directive): KERNEL FACT `ActionDispatched(Tap)` vs SHADOW
INFERENCE "the action may have targeted the Wi-Fi toggle" — never equivalent;
the inference must carry its supporting evidence refs and may be flagged
uncertain.

The whole `ShadowAnalysis` artifact is classified `COGNITIVE_INFERENCE`.
Never `WORLD_TRUTH`, never `KERNEL_FACT`, never `ACTION_AUTHORIZATION`, never
`GOAL_EVIDENCE`. The classification is a mandatory constant field of the
artifact and survives in-process caching and UI presentation (V1 has no
durable session event, so there is no persistence to survive).

## 7. ShadowAnalysis output schema (D4)

Minimum DSH-side artifact, bounded to current-buyer fields only:

```
ShadowAnalysis {
  analysisId: string              // "shadow-<runId>-<n>" (unique per session)
  runId: string                   // inspected Kernel RunId (never derived from SessionId)
  sessionId: string               // DSH session that produced it (explicit, separate)
  trigger: 'human.request'   // V1 ONLY; 'run.failed'|'run.completed' reserved/deferred (not producible in V1, §4)
  focus: 'general' | 'trap' | 'failure' | 'completion' | 'progress' | 'blocked'
  requestedAt: number             // epoch ms
  completedAt: number             // epoch ms
  classification: 'COGNITIVE_INFERENCE'   // constant, survives caching/presentation (no persistence in V1, §9)
  evidenceRefs: EvidenceRef[]     // bounded locators, refs preferred over copied content
  observedFacts: ObservedFact[]   // { claim, classification: 'kernel-fact'|'derived-read-model', ref }
  hypotheses: Hypothesis[]        // { claim, classification: 'shadow-inference', supportingRefs: ref[], flaggedUncertain?: boolean }
  uncertainties: Uncertainty[]    // { topic, reason: 'missing-data'|'stale-data'|'unresolved-evidence-ref'|'model-unavailable'|'model-timeout'|'model-error'|'context-assembly-failed' }
  recommendations: Recommendation[] // { text, target: 'human-investigation' }
  humanSummary: string            // primary human-facing text
  model: { provider: string, model: string }   // DSH-config-selected, recorded
  modelCall: ModelCallRecord      // accounting (§26): status, startedAt/finishedAt, inputEventCount, contextChars, error?
}
```

Explicitly NOT introduced (no buyer): confidence scoring framework, severity
ontology, memory system, planner state, execution proposal, approval status,
action authorization. Only fields with a current buyer exist. The schema has
no durability field and implies none: the artifact is ephemeral
(`EPHEMERAL_PROCESS_LOCAL`, §9), may live in a bounded process-local cache,
and is recomputable on demand.

## 8. Context assembly policy (D3) — bounded causal window

Deterministic retrieval FIRST (zero-model), then one assembled context:

1. `run.snapshot.get` → latest `RunSnapshot` (always; exactly one snapshot in
   context — no history dumps).
2. `run.events.after` → bounded recent `RuntimeEvent`s: most recent
   `shadow.maxEvents` events (default 200) within a bounded age window.
3. `run.trap.get` → trap detail, only when `focus: 'trap'`.
4. Events causally related to the trigger: for a human request on a run that
   already reached a terminal state (failure/completion — the post-hoc buyer),
   the terminal event plus the immediately preceding bounded window (already
   inside the `maxEvents` cap); for `focus: 'trap'` the `TrapRaised` event and
   its causal window. (`run.failed`/`run.completed` semantics are consumed as
   analysis focus in V1, NOT as auto triggers — §4.)
5. `EvidenceRef` resolution — LAZY: only when the analysis buyer requires
   content (e.g. `focus` needs it), capped at `shadow.maxEvidenceRefs`
   (default 8) and `shadow.evidenceBytesPerRef` (default 8192); otherwise the
   logical locator is referenced, not copied.

Hard caps (BoundedContext = PASS): `maxEvents` (default 200),
`maxContextChars` (default 80 000), snapshot count = 1, `maxEvidenceRefs`
(default 8), evidence bytes per ref (default 8192). No unbounded transcript
accumulation: the plugin keeps only a bounded in-memory ring of recent events
per run and re-fetches on demand; it never accumulates full run history.

Visual evidence: lazy by design — `shadow.visual.enabled` (default `false`);
image/screenshot refs are fetched only when the buyer requires them AND the
flag is on. The first baseline does not require visual input to the model.

Model input: ONE user message containing the assembled context (trigger +
focus, latest snapshot facts, bounded events, causal window, locator-only
EvidenceRefs) with a system prompt enforcing the Shadow analyst role
(distinguish facts from inference; mark uncertainty; never claim authority).
The model is NOT asked to reconstruct facts already retrieved
deterministically.

## 9. Durability decision (D5) — EPHEMERAL_PROCESS_LOCAL (rebaselined)

> **REBASELINED** by `PROJECT_LEADER_DSH_SHADOW_COGNITION_DURABILITY_REBASELINE_DECISION`
> (2026-08-15). The original D5 (`DSH_NATIVE_DURABLE` via a `shadow/analysis`
> session event with `ignorable: true`) is **BASELINE_ASSUMPTION_FALSIFIED**.
> This is NOT an implementation failure, a Kernel defect, a DriverHost defect,
> or a plugin defect — it is a falsified baseline assumption about the pinned
> DSH API. Full evidence: `source-evidence-matrix.md` §Falsification evidence
> (M13–M20), labeled SOURCE FACT / PROBE RESULT / PROJECT LEADER DECISION.

### 9.1 Falsification record (summary)

Verified against the pinned checkout (`47f943859bef60e4160492346772ded9b24f765a`,
`0.1.0-rc.5`) and by real cold-reload probes (real pinned `boot()` with
`@deepseek-ai/dsh-session` + `@deepseek-ai/dsh-session-persistence-jsonl`,
cold re-read from the same storage root in a second process):

1. **SOURCE FACT** — `Session.append` (`packages/core/session/src/index.ts`)
   constructs the envelope as
   `deepFreeze({type, seq, time, data: dataSnapshot, ...(surfaceMetadata)})`
   where `surfaceMetadata` comes ONLY from `opts[0]`'s
   `sourceEventSeqs`/`surfaceOp` (`SurfaceIntent`). There is no supported way
   for an out-of-repo caller to set `ignorable: true` on a live append.
2. **SOURCE FACT** — a live-appended unknown `shadow/analysis` therefore
   persists as an unknown NON-IGNORABLE event.
3. **PROBE RESULT (Path A)** — after `session.append('shadow/analysis', …)` +
   `session/flush`, a true cold reload refuses the WHOLE producing session log:
   `SessionFormatUnsupportedError: … unknown to this harness and not marked
   ignorable; refusing to interpret the log`. The frozen baseline's "required
   chain" (`Session append → session/event fanout → PersistenceCoordinator →
   native write-behind → session/flush`) cannot produce a reload-safe durable
   Shadow event.
4. **SOURCE FACT** — a direct `PersistenceCoordinator.append` can inject an
   ignorable raw envelope, but that bypasses the authorized live append/fanout
   semantics and creates unsafe sequence/write-path coupling; there is ZERO
   in-repo precedent (the only callers are coordinator-contract tests, which
   exercise the DETACHED-session pattern only). The generated
   `KNOWN_SESSION_EVENT_TYPES` registration surface for out-of-repo plugin
   event types "is deferred until such a consumer exists".
5. **PROBE RESULT (Path C)** — the unsafe direct-persistence approach has
   demonstrated SILENT event loss: without exact live/persistent seq
   alignment, the next live event (`command/done`) is dropped from storage by
   the write-behind's `batch.filter(e => e.seq >= cursor)`.
6. **SOURCE FACT** — a detached sibling-session persistence pattern does not
   satisfy "in the producing DSH session's log" and would materially change
   the frozen design (parallel persistence convention without a current
   buyer).

### 9.2 Decision: Shadow V1 durability = EPHEMERAL_PROCESS_LOCAL

- **Shadow V1 appends ZERO custom session events.** No `shadow/analysis`
  session event exists anywhere: `DurableEventType = NONE`,
  `UnknownCustomSessionEventsWritten = NONE`,
  `UnknownNonIgnorableSessionEventsWritten = NONE`. No `ignorable` marker is
  required because no custom event is appended at all. The pinned session log
  and its reload behavior are completely untouched by Shadow.
- A completed `ShadowAnalysis` MAY live in a **bounded process-local cache**:
  `Map<runId, bounded recent ShadowAnalysis>`, bounded (size cap), 
  process-local, non-authoritative, disposable. It is convenience only — the
  command response is the authoritative human inspection surface (§11). This
  cache is explicitly NOT a "Memory", "Knowledge Store", or "History
  Database"; it is not persisted and has no load path.
- **Restart semantics (truthful):** DSH restart → Shadow cache empty → Kernel
  unaffected → no Shadow history is reconstructed or fabricated → a human may
  request a fresh analysis and context is deterministically reconstructed from
  the graduated Kernel read surfaces. Lost ephemeral analyses are NEVER
  replayed as though they were persisted.
- **Why this is honest for the first buyer:** Shadow is explicitly
  `COGNITIVE_INFERENCE`, non-authoritative, human-facing, and Kernel consumes
  zero Shadow output. The first buyer (post-hoc / near-live run
  interpretation) does not require durability: a human request can
  deterministically reconstruct fresh context from the Kernel read surface and
  run Shadow analysis again after a DSH restart. `BuyerStillSatisfied = YES`.
- **Kernel truth independence (unchanged):** Kernel truth never depends on
  Shadow persistence — now trivially, because no Shadow persistence exists.

### 9.3 Forbidden workarounds

- **DUAL-WRITE WORKAROUND: FORBIDDEN.** Do NOT implement
  `Session.append('shadow/analysis', …)` + `sessionPersistence.append(…)`
  sequence-aligned dual writing. Do NOT depend on `appendLiveBatch` filtering,
  cursor side effects, private coordinator behavior, or manual seq allocation.
  No production logic may reproduce probe Path B.
- **DETACHED SHADOW SESSION: FORBIDDEN.** Do NOT create
  `shadow-{sessionId}`, `analysis-{runId}`, or any detached/sibling DSH
  session solely to persist Shadow output — that would create a parallel
  persistence convention without a current buyer.
- Do NOT modify pinned DSH to solve this (`Session.append`,
  `known-event-types`, `PersistenceCoordinator`, session reader,
  `SESSION_FORMAT_VERSION`). Pinned DSH remains authoritative and untouched;
  no fork/patch is bought by this change.

### 9.4 Deferred durability buyer

Recorded deferred pressure item: **`DSH_SHADOW_DURABILITY_EXTENSION`**. It may
be reconsidered only when at least one of the following exists:

- **A.** Pinned DSH exposes a supported runtime registration surface for
  external session event types; **or**
- **B.** `Session.append` officially supports ignorable external events; **or**
- **C.** DSH exposes another sanctioned native artifact persistence surface
  suitable for plugin-owned cognition; **or**
- **D.** A concrete human/product buyer requires durable Shadow history AND
  Project Leader explicitly authorizes a new persistence design.

Do not predict which solution wins.

## 10. Model invocation seam (D6) — ctx.llm, DSH-native

- Seam: `ctx.llm` — `LlmRuntime` service
  (`packages/llm/llm/src/index.ts`), `stream(options: GenerateOptions)`
  (line 913), the same service the agent loop uses
  (`packages/core/agent-loop/src/agent.ts:486` builds a `GenerateOptions` and
  calls through it). Shadow builds its OWN one-shot `GenerateOptions`:
  `{ provider, model, system: <analyst prompt>, messages: [user(context)],
  signal, maxTokens? }`.
- NO loop semantics: no derived history, no agent loop, no tool loop, no
  `markAgentLoopRequest` (loop-only marker), `purpose` left unset (the closed
  `purpose` enum is `'compaction' | 'session-title'` — Shadow is neither;
  ordinary conversation requests leave it unset).
- Model selection belongs to DSH configuration: `shadow.model.provider` and
  `shadow.model.model` (plugin config row). No generic provider framework is
  introduced. Capability needs for the first baseline: text-in/text-out,
  bounded output — no image input, no tool calling.
- If no model is configured: analysis degrades to a deterministic
  read-only digest (facts + cited events + `uncertainty:
  'model-unavailable'`), classification still `COGNITIVE_INFERENCE`, status
  `not-configured`. Deterministic collection is zero-model.
- Model-facing tools: NONE in the first baseline (§19 — "Prefer NO
  model-facing Kernel mutation tools"; there are no tools at all, so no
  mutation surface exists). Retrieval is deterministic and precedes the call.
  The tool-registration seam (`ctx.tools`,
  `packages/core/tools/src/index.ts:827`) was audited and is recorded in the
  matrix as NOT selected. If a later iteration adds read-only retrieval tools,
  they must expose only bounded read-only information (get run snapshot, get
  bounded runtime events, resolve logical EvidenceRef) — no mutation methods.
- Model-call accounting (§26): Shadow MAY invoke models; per analysis record a
  `ModelCallRecord` — trigger, input `EvidenceRefs`, `inputEventCount`,
  `contextChars` (boundedness, cheaply available), status
  (`success | error | timeout | aborted | not-configured`), provider/model,
  timestamps. Deterministic read-only collection before the model remains
  zero-model (and all existing `uniclaw-*` commands remain zero-model).

## 11. Human inspection surface (D7)

- Primary: **`uniclaw-shadow-analyze`** command on the existing DSH-native
  command registry. Naming audited against the graduated command set
  (`uniclaw-inspect-run`, `uniclaw-inspect-trap`, `uniclaw-evidence-open`,
  `uniclaw-runs-list`): same `uniclaw-` kebab-case prefix, same zero-model
  dispatch (the command handler runs against the receiving agent WITHOUT
  sending the command to the model), session-scoped, returns the
  `ShadowAnalysis` as structured text (HumanSummary first, then
  observedFacts / hypotheses / uncertainties / recommendations).
- Secondary: NONE in V1 (no durable `shadow/analysis` session event — §9).
  The command response is the authoritative human inspection surface; the
  optional bounded process-local cache is convenience only, with no frontend,
  no custom WebSocket, no new event bus.
- NO new frontend, no new slots, no new UI module, no new transport.

## 12. Session ↔ Kernel Run relationship (D9)

- Mapping: **one DSH session ↔ zero/one/many inspected Kernel RunIds**,
  established explicitly per analysis request (the command takes an explicit
  `runId`; the `ShadowAnalysis` artifact carries both `runId` and `sessionId`
  as separate fields — no durable event exists in V1, §9).
- `SessionId == RunId` is NEVER asserted; there is no code path deriving one
  from the other.
- No Kernel run is created from session creation, and no DSH session is
  created from a Kernel run in this baseline.
- The plugin keeps only a bounded in-process per-run cache (recent events /
  last snapshot) keyed by `runId`; it is process-local, never an identity
  mapping, never persisted as identity.

## 13. Failure isolation (D8) — fail-open relative to Kernel

Every Shadow failure path is fail-open and contained:

| Failure | Behavior |
|---|---|
| Model not configured | deterministic digest + `uncertainty: model-unavailable` |
| Model timeout (`shadow.timeoutMs`, default 60 000, AbortSignal) | artifact status `timeout`, `uncertainty: model-timeout` |
| Model error | artifact status `error`, `uncertainty: model-error` |
| Context assembly failure | artifact with `uncertainty: context-assembly-failed` (partial facts still reported) |
| EvidenceRef resolution failure | `uncertainty: unresolved-evidence-ref`; claims never assert unresolved content |
| Bounded cache write failure | contained and logged; the analysis is still returned to the command caller (cache is convenience only) |
| DSH restart | ephemeral ShadowAnalysis lost truthfully (cache empty); Kernel run unaffected; pinned session log and its reload completely untouched (zero custom session events); fresh analysis recomputable on demand from the Kernel read surfaces; lost analyses are never reconstructed or fabricated |

MUST NOT (any of the above): stop a Kernel Run, change a Goal, create a Trap
in Kernel, change Agent state, change completion. Architectural basis: there
is no wire mutation path and no Kernel consumer (§2).

## 14. Authority guard boundary (D10)

Architecture guards proving Shadow cannot import/use: ADB dispatch,
PhysicalEnvironment mutation, Runtime Agent mutation APIs, Container mutation
APIs, Traversal mutation APIs, GoalEvidence creation APIs.

1. **Wire contract**: zero mutation methods (frozen) — the only DriverHost
   surface the plugin can reach is the read-only method set (§2).
2. **Narrowed facade**: the Shadow module (future `shadow.js`) receives only a
   read-only retrieval facade (snapshot / events / trap / evidence / runs /
   support) — no adapter internals, no control surface.
3. **Zero physical capability**: `dsh-plugin-uniclaw` deps stay Node builtins
   + pinned cordis peer; no ADB/device/container/traversal/goal code exists.
4. **Zero Runtime modification**: mechanical guard — no files under
   `src/UniClaw.Runtime`; no new RuntimeEvent classes; no new emitters.
5. **Mechanical guards (future tests)**: grep-guard the Shadow source tree for
   forbidden mutation/dispatch vocabulary; assert the wire client surface is
   exactly the frozen read-only method set; assert ZERO custom session events
   are appended (no `shadow/analysis` session event type exists anywhere in
   the plugin source — §9.3); assert no `shadow/analysis`-to-Kernel path.

## 15. Shadow vs Advisory freeze (D12)

Frozen distinction:

- **SHADOW**: Kernel ignores the cognitive result entirely
  (`KernelInputFromShadow = ZERO`).
- **ADVISORY** (future, different change): Kernel/Agent may consume a cognitive
  proposal while remaining final authority.

This change is SHADOW ONLY. Explicitly forbidden: `DecisionProposed`,
`DecisionAccepted`, `ActionAuthorized` RuntimeEvents; proposal ingestion into
Agent; semantic action suggestions consumed by Kernel. Those belong to a later
Advisory change.

## 16. Falsifier matrix (F1–F16)

Each falsifier is a verification contract for the implementation gate.
SHADOW-F7 and SHADOW-F15 were REBASELINED by
`PROJECT_LEADER_DSH_SHADOW_COGNITION_DURABILITY_REBASELINE_DECISION`
(2026-08-15): the original durability wording was falsified against the
pinned DSH API (§9.1); the rebaselined wording reflects V1
`EPHEMERAL_PROCESS_LOCAL` semantics with zero custom session events.

| # | Falsifier | Defined behavior |
|---|---|---|
| SHADOW-F1 | TrapRaised + bounded evidence → analysis produced | Given a run that raised a Trap with bounded evidence, when a human requests `uniclaw-shadow-analyze <runId> --focus trap`, then an analysis is produced within the bounded window citing the TrapRaised RuntimeEvent id |
| SHADOW-F2 | RunFailed → analysis cites relevant RuntimeEvents | Given a failed run, when analysis is requested, then the artifact's observedFacts/hypotheses cite RuntimeEvent ids from the bounded window including the failure event |
| SHADOW-F3 | missing snapshot field → uncertainty, not fabrication | Given the snapshot lacks field X, when analysis is produced, then `uncertainties` contains `missing-data: X` and no observedFact asserts X |
| SHADOW-F4 | EvidenceRef unavailable → analysis stays truthful | Given `evidence.get` fails/unavailable for a ref, when analysis is produced, then `uncertainties` contains `unresolved-evidence-ref` and no claim asserts the unresolved content |
| SHADOW-F5 | model timeout → Kernel unaffected | Given the model call exceeds `timeoutMs`, when the artifact completes, then status is `timeout` with `model-timeout` uncertainty, and the Kernel run is unaffected (no wire mutation exists) |
| SHADOW-F6 | model failure → Kernel unaffected | Same as F5 with status `error` / `model-error` |
| SHADOW-F7 | DSH restart → Kernel unaffected; ephemeral loss truthful | Given DSH restarts during/after analysis, then the Kernel run continues; ephemeral ShadowAnalysis is lost truthfully (cache empty); no fake Shadow history is reconstructed; fresh analysis can be recomputed on demand from current legitimate read surfaces. **PASS criterion: truthful loss is allowed** (rebaselined — original "durable events resume from the session log" is falsified, §9) |
| SHADOW-F8 | "tap X" suggestion → Kernel performs NOTHING | Given an artifact recommends "tap X", then no Kernel action occurs — architecturally: zero wire mutation methods, zero Kernel consumer |
| SHADOW-F9 | Shadow output cannot create GoalEvidence | Given a `ShadowAnalysis` artifact, then there is no code path to GoalEvidence creation (no wire method; no Kernel change; grep guard) |
| SHADOW-F10 | Shadow output cannot mutate Container/Binding/StateBelief | Same as F9 for Container/Binding/StateBelief mutation |
| SHADOW-F11 | bounded context window enforced | Given a run with more than `maxEvents` events, when analysis is produced, then `inputEventCount ≤ maxEvents` and `contextChars ≤ maxContextChars` |
| SHADOW-F12 | visual evidence lazy-loaded only on buyer need | Given a focus without visual need (or `shadow.visual.enabled=false`), then zero image fetch; only a buyer-requiring focus + enabled flag fetches |
| SHADOW-F13 | Kernel facts vs Shadow hypotheses distinguishable | Given any artifact, then every observedFact carries `kernel-fact`/`derived-read-model` and every hypothesis carries `shadow-inference`; no collapse |
| SHADOW-F14 | session/run identity not conflated | Given the `ShadowAnalysis` artifact and the command flow, then `runId` and `sessionId` are separate explicit fields; no code asserts `SessionId == RunId`; no Kernel run is created from session creation |
| SHADOW-F15 | V1 writes ZERO custom session events; session reload stays safe | **REBASELINED** (original "persist `shadow/analysis` with `ignorable: true` → reload succeeds" is not implementable against the pinned DSH API — §9.1). Given Shadow analysis completes and DSH restarts, then: the Kernel run remains unaffected; pinned session reload remains unaffected; NO `shadow/analysis` event exists in the log; the lost ephemeral analysis is NOT reconstructed or fabricated; a new human request may recompute analysis from current legitimate read surfaces. This directly protects session reload safety (zero custom session events, zero `ignorable` requirement, zero marker) |
| SHADOW-F16 | zero new Runtime semantic emitters | Given the whole change, then zero files under `src/UniClaw.Runtime` are modified and zero new RuntimeEvent classes/emitters exist |

## 17. Validation (this gate)

- `openspec validate dsh-shadow-cognition --strict --no-interactive` → PASS.
- Architecture/docs consistency (`scripts/check-consistency.sh`) — cheap,
  run.
- Pinned DSH checkout clean (`git status --porcelain` empty at
  `47f943859bef60e4160492346772ded9b24f765a`) → PASS.
- No production mutation: only `openspec/changes/dsh-shadow-cognition/` was
  written.
- No full Runtime regression needed (production unchanged); the implementation
  gate will run node + falsifier tests + zero-delta scans. Under
  `EPHEMERAL_PROCESS_LOCAL`, the implementation gate's falsifier suite replaces
  the persistence reload test with the zero-custom-session-event guard and the
  restart-loss truthfulness test (F7/F15 rebaselined).

## 18. Reference

- `source-evidence-matrix.md` — M1–M12, every seam verified against the pinned
  checkout (claim, source file, symbol, behavior, why selected, compatibility
  constraints); M13–M20 falsification evidence for the durability rebaseline
  (SOURCE FACT / PROBE RESULT / PROJECT LEADER DECISION).
- Frozen prerequisites: `openspec/changes/archive/2026-08-15-dsh-uniclaw-control-plane-protocol-baseline/`,
  `openspec/changes/archive/2026-08-15-dsh-uniclaw-control-plane-plugin-implementation/`,
  graduation records under `docs/decisions/`.
