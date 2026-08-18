# DSH Assistance Consumer Selection — Decision

> Status: BUYER_SELECTION_COMPLETE (no production code)
> Date: 2026-08-17
> Prerequisites: RUNTIME_EXTERNAL_CONTRACT_BASELINE accepted ·
> RUNTIME_ASSISTANCE_SEAM graduated · DSH_ASSISTANCE_PROVIDER_ADAPTER graduated
> Frozen path: Runtime.Agent → IAssistanceProvider → AssistanceWireProvider →
> `assistance.pending/resolve` → AssistanceBridge → Harness Assistance Consumer
> `REAL_HARNESS_INTELLIGENCE_CONSUMER = NOT_YET_PURCHASED` (this gate selects it)
> Source truth: pinned DSH checkout `47f943859b` (packages inspected at source)

---

## 1. Current L1 buyer (reconstructed from repository truth)

The graduated Runtime L1 CONSULT seam produces requests ONLY at:

- belief `Contradicted` (and the defensive `Unresolved` path) — `Agent.SemanticRun.cs`
  DECIDE block.

Advice vocabulary is bounded (Agent whitelist): `re-observe` / `rebind` /
`dismiss-obstruction` / null (abandon).

The consumer owns NO semantic authority, Runtime belief, binding state,
GoalEvidence, physical execution, or route execution.

**What the Runtime is actually asking:**

> "I lack enough information to choose among a bounded set of
> recovery/evidence-improvement actions at a belief conflict."

**What it is NOT yet asking:** general planning, arbitrary GUI navigation,
long-horizon reasoning, execution takeover, L2 DELEGATE_PLANNING, L3 YIELD.

---

## 2. DSH repository seam inventory (source-verified)

| Seam | Package | Verified source behavior |
|---|---|---|
| **A. LlmRuntime** | `@deepseek-ai/dsh-llm` (`ctx.llm`) | `LlmRuntime extends Service`; provider-neutral `registerAdapter(providers, adapter)`; call surface `stream(options: GenerateOptions): AsyncIterable<StreamChunk>`; `GenerateOptions { provider, model, messages, system, tools?, maxTokens?, stop?, signal?, sessionId?, purpose? }`; cancellation via `options.signal`; **NO native response-format / jsonSchema structured-output field**; one-shot messages (no session required); `listProviders` / `resolveModel` metadata |
| **B. General Agent** | `@deepseek-ai/core-agent-loop` (`ReactLoopAgent implements Agent`) | Full reasoning loop over queued turns: `send/followup/steer/inject/cancel/whenIdle`; step-boundary input; `RuntimeContextProjection` context; tool assembly; session/turn machinery; a persistent agent instance, not a one-shot call |
| **C. SubagentRuntime** | `@deepseek-ai/dsh-subagent` (`ctx.subagents`) | `SubagentRuntime extends Service`; `registerProvider(provider)` pluggable drivers (in-process/fork/acp/codex/claude-code/dsh-sdk); one-shot runs + `startContinuable` durable children; `observeActivation`; child-agent lifecycle overhead (provider, childId, parent, session) |
| **D. Skill/Tool** | `packages/interaction/*` (`tool-ask-user`, `user-approval`, `user-questions`), `packages/tools` | Tool-registry / user-question / approval seams; tool calls are mediated by an agent loop (not a standalone bounded service) |
| **E. Human/approval** | `user-approval` / `user-questions` | Comparison point only: L1 consult never authorizes a physical action — human-in-the-loop is not the default consumer |
| **F. Structured output** | LlmRuntime types | `GenerateOptions` has NO schema/response-format constraint; `tools?: ToolSchema[]` is the only schema channel (tool-call params) — not a plain JSON-output guarantee |

---

## 3. Candidate comparison

### Candidate A — Direct LLM consumer (LlmRuntime)

- Advantages: smallest latency/overhead; lowest token cost; one bounded
  `stream()` call fits L1; provider-neutral model adapter already exists;
  cancellation (`signal`); no session dependence.
- Risks: no native structured-output guarantee (needs a minimal consumer-side
  validation/normalization layer); temptation to grow prompts into a second agent
  (guarded by the bounded vocabulary + whitelist); reasoning models may exceed
  the 30s composition budget (mitigation: bounded prompt + non-reasoning model +
  consumer-side timeout; budget is COMPOSITION_POLICY).

### Candidate B — General Agent consumer (ReactLoopAgent)

- Advantages: rich intelligence; session context; tool/skill access; natural L2 path.
- Risks: heavy machinery (loop, session, context projection); high token/latency;
  hard bounded semantics; risk that the Runtime starts delegating ordinary local
  decisions; **excessive for a three-choice conflict diagnosis**.

### Candidate C — Subagent consumer (SubagentRuntime)

- Risks: child-agent startup overhead (provider/childId/session/continuation);
  isolation not needed for one bounded consult; **selection must NOT follow from
  "Runtime.Agent is also a subagent" — the concepts are independent**.
- L1 fit: overkill; L2+ specialized assistance may justify it later.

### Candidate D — Deterministic/rule consumer

- Already exists (graduated, OPT-IN `assistance.consumer: 'deterministic'`).
- Useful as cheap pre-filter/fallback; MUST NOT recreate Runtime semantic
  authority in the adapter. Not the primary real consumer.

### Candidate E — Human/approval

- Comparison only: L1 consults do not authorize physical actions; REJECT as the
  default consumer (future high-stakes assistance may revisit).

---

## 4. L1 vs future L2 separation (mandatory)

| Level | Runtime asks | Consumer |
|---|---|---|
| **L1 CONSULT (now)** | "choose among bounded recovery/evidence actions" | **LlmRuntime** (smallest bounded intelligence) |
| **L2 DELEGATE_PLANNING (future)** | "I know the Goal and world but not where to go" | **General Agent** (ReactLoopAgent) — separate gate, NOT purchased now |

These MUST NOT share an architecture by default. The L1 buyer does not justify
agent-loop complexity; L2 will purchase it independently with its own buyer.

---

## 5. Selected first real consumer

**`A. DSH_LLM_ASSISTANCE_CONSUMER_READY`** — the first REAL Harness intelligence
consumer is a **LlmAssistanceConsumer** riding the exact graduated consumer port
(`{ resolve(request) → structured result }`), implemented behind the optional
`ctx.llm` seam (shadow-cognition precedent: non-injected, activation never depends
on an inference service).

Exact DSH seam: **`@deepseek-ai/dsh-llm` `LlmRuntime.stream(GenerateOptions)`**
via `ctx.get('llm')` — one bounded call per assistance request; provider/model
selected by DSH model routing (`resolveModel` / adapter registration); cancellation
via `signal`.

No architecture change is required: the bridge consumes a replaceable consumer
port; `LlmAssistanceConsumer` is a drop-in implementation of that port.
**Not `ARCHITECTURE_GATE_REQUIRED`.**

---

## 6. Consumer contract (Harness-side port — unchanged shape)

```ts
HarnessAssistanceRequest = {
  requestId: string; runId: string; assistanceKind: 'belief-conflict';
  semanticPage: string; beliefState: 'Contradicted' | 'Unresolved';
  worldVersion: number;
  observation: { sequence: number; foregroundApplication: string | null;
                 elementCount: number; elementTexts: string[] }; // bounded summary
  allowedRecommendations: ['re-observe', 'rebind', 'dismiss-obstruction'];
  evidenceRefs?: string[]; // optional artifact references
}

HarnessAssistanceResult = {
  requestId: string; worldVersion: number;
  recommendation: 're-observe' | 'rebind' | 'dismiss-obstruction' | null;
  reason: string;
}
```

NOT exposed: Runtime private objects, Container mutable state, DeviceAction,
coordinates, ElementIndex as authority, goal-completion command, executable plan.

---

## 7. Structured-output strategy (§6)

DSH `LlmRuntime` has NO native JSON-schema constraint (verified:
`GenerateOptions` lacks response-format/jsonSchema). Therefore:

1. **Prompt-level constraint**: a minimal system prompt instructs the model to
   return ONLY a single JSON object matching the whitelist vocabulary + a short
   reason; `maxTokens` bounded (e.g. 200); stop sequences delimit the JSON block.
2. **Consumer-side minimal validation/normalization layer** (the only required
   layer — lives INSIDE `LlmAssistanceConsumer`, NOT the bridge): parse the
   returned text as JSON; validate `recommendation ∈ allowedRecommendations`
   (else null); require a non-empty `reason`; any failure ⇒ null (fail-closed).

The bridge remains transport/protocol translation only.

---

## 8. Failure behavior (§8) — all → no advice

| Failure | Behavior |
|---|---|
| consumer unavailable (`ctx.llm` absent) | no advice (bounded timeout → Agent fail-closed) |
| model failure / stream error | caught → null (never fabricates) |
| timeout | consumer-side bounded timeout (within 30s composition budget) → null |
| invalid structured result | validation layer → null |
| consumer cancellation | `signal` aborts → null |
| worldVersion stale | rejected by the GRADUATED wire/provider path (unchanged) |

No consumer failure fabricates belief, progress, action authorization, or Goal
completion.

---

## 9. Token / latency analysis (§7)

| Candidate | Model calls/request | Prompt/context | Session dep | Tool overhead | Child startup | Expected latency | 30s budget truthful |
|---|---|---|---|---|---|---|---|
| **LlmRuntime** | **1** | small (system + bounded digest) | none | none | none | <5s (non-reasoning model) | ✅ (COMPOSITION_POLICY) |
| General Agent | ≥1 loop turns | full session/context projection | yes | yes | n/a | seconds–minutes | ⚠️ |
| Subagent | ≥1 child run | child context | yes | yes | yes | seconds+ | ⚠️ |

Prefer the smallest mechanism: **LlmRuntime**. Budget note: a reasoning-effort
model may exceed 30s — the consumer selects a bounded/non-reasoning model for L1
(DSH model routing); the 30s value remains COMPOSITION_POLICY.

---

## 10. Observability requirements (§9)

Minimum DSH-side (Harness-owned) recording per consultation — **not injected into
the Runtime Data Plane**:

- assistance consumer selected
- model/provider used (LlmRuntime providerInfo/resolveModel)
- latency (start→result)
- token usage (stream chunk usage when available)
- result classification (resolved recommendation / abandoned / timeout / error)
- RequestId / RunId correlation

Harness owns its own intelligence observability (logger/events on the DSH side).

---

## 11. Decision matrix

| Candidate | Current L1 Fit | Latency | Token Cost | Context/Tools | Complexity | Future L2 Fit | Decision |
|---|---|---|---|---|---|---|---|
| **LlmRuntime** | **exact** | low | lowest | none needed | low | partial (upgrades to agent) | **BUY_NOW** |
| General Agent | overkill | high | high | rich (unused) | high | exact | **SUPPORTED_FUTURE** (L2) |
| SubagentRuntime | overkill | medium | medium | child context | medium | specialized assistance | **SUPPORTED_FUTURE** |
| deterministic/rule | fallback only | lowest | zero | n/a | lowest | n/a | **SUPPORTED** (pre-filter/fallback, already OPT-IN) |
| human/approval | not needed (no physical authority) | n/a | n/a | n/a | n/a | high-stakes only | **REJECT** (current) |

**Primary current-L1 decision: `A. DSH_LLM_ASSISTANCE_CONSUMER_READY`**

---

## 12. Bounded implementation scope (next Apply gate)

`PROJECT_LEADER_APPLY_DSH_LLM_ASSISTANCE_CONSUMER` — implement ONLY:

1. `dsh-plugin-uniclaw/src/assistance/llm-consumer.js` — `LlmAssistanceConsumer`
   implementing the graduated consumer port via optional `ctx.llm` (`stream` +
   `signal` + bounded prompt + consumer-side JSON validation/normalization).
2. Composition: extend the OPT-IN consumer selection
   (`resolveAssistanceBridge`): `assistance.consumer: 'llm'` builds the bridge with
   `LlmAssistanceConsumer`; no configured consumer still ⇒ no bridge ⇒ bounded
   fail-closed. **Deterministic profile unchanged** (`'deterministic'`).
3. Tests: model-free fake-llm adapter through the consumer port; validation layer
   (valid/invalid/malformed JSON → whitelist/null); failure semantics
   (unavailable/error/timeout/cancel → null); bridge unchanged; guard scans.
4. NO Runtime changes, NO AssistanceBridge semantic expansion, NO General Agent /
   Subagent purchase, NO structured-output framework invention.

## 13. Falsifiers

| # | Falsifier | Fails if |
|---|---|---|
| F1 | Runtime-private exposure | the consumer reads Runtime/Container private state or binding |
| F2 | authority grant | the consumer authorizes DeviceAction/coordinates/ElementIndex/Goal completion |
| F3 | bridge semantic expansion | `AssistanceBridge` changes beyond transport/protocol translation |
| F4 | free-form inference | the consumer infers re-observe/rebind/dismiss from unvalidated prose (no validation layer) |
| F5 | L2 premature purchase | General Agent / Subagent is used for current L1 |
| F6 | failure-as-advice | model failure/timeout/invalid result yields advice instead of null |
| F7 | budget untruthful | the consumer cannot stay within the 30s composition budget (model/timeout selection) |
| F8 | observability gap | latency/token/result/error/correlation are not recorded Harness-side |
| F9 | router role | the consumer becomes an intelligence router or semantic decision layer |
| F10 | session dependence | L1 consult requires a DSH session/agent loop |

## 14. Recommended next gate

`PROJECT_LEADER_APPLY_DSH_LLM_ASSISTANCE_CONSUMER` — implement the bounded scope
above (model-free tests first, then the `ctx.llm` consumer behind OPT-IN config),
then `PROJECT_LEADER_DSH_LLM_ASSISTANCE_CONSUMER_GRADUATION_REVIEW`.

---
