# Design: dsh-assistance-provider-adapter

> BASELINE design (no code). Source-verified repository baseline: 2026-08-17.
> Contract frame: `runtime-external-contract-baseline` (Plane 3 — Assistance).
> Seam: `runtime-assistance-seam` (implemented). Mother-doc: §3 of
> `docs/decisions/outer-intelligence-integration-architecture.md`.

---

## 1. Verified source baseline

| Fact | Source |
|---|---|
| Runtime seam exists: `IAssistanceProvider.ConsultAsync(AssistanceContext, ct) → AssistanceAdvice?`; Agent consults at belief adjudication (Contradicted/Unresolved), bounded (3), advice consumed via existing deterministic actions (re-observe/rebind/dismiss-obstruction), stale advice discarded, fail-closed preserved; ZERO provider implementations in production | `src/UniClaw.Runtime/Capabilities/Brain/IAssistanceProvider.cs`; `Agent/Agent.SemanticRun.cs` (ConsultAssistanceAsync/TryApplyAssistanceAdviceAsync); `tests/.../Capabilities/FakeAssistanceProvider.cs` (test-only) |
| Advice Recommendation whitelist the Agent accepts: `re-observe` / `rebind` / `dismiss-obstruction` / null (unknown ⇒ not actionable ⇒ fail-closed) | `Agent/Agent.SemanticRun.cs` TryApplyAssistanceAdviceAsync |
| DSH plugin: `ctx.get('llm')` seam precedent — optional, read-only via ctx.get, activation never depends on an inference service (shadow cognition) | `dsh-plugin-uniclaw/src/plugin.js:140-143` |
| DSH adapter surface: `ping/listRuns/getRunSnapshot/getTrap/getRuntimeEvents/drainRunEvents/getEvidence/controlSupport/runStart` + disconnect/dispose; TCP loopback, plugin = client, DriverHost = server (one connection direction) | `dsh-plugin-uniclaw/src/adapter.js`; `src/UniClaw.Runtime.DriverHost/Transport/UniClawDriverHostServer.cs` |
| DriverHost wire table: 8 frozen read-only + `run.start` = 9 methods; additive convention established | `UniClawDriverHostServer.cs` `Invoke`; `dsh-runtime-agent-subagent-run-entry` precedent |
| Contract Plane 3: Runtime-initiated; capability-gap expression (not an LLM call); external output is advice, Kernel decides (I-3); correlation + world-version binding; wire format NOT frozen by the contract baseline | `openspec/changes/runtime-external-contract-baseline/design.md` §3.3 |
| Runtime is untouched by this gate: the seam already exists; Guard 2/10b token scans enforce zero external references in `UniClaw.Runtime` | `tests/.../Architecture/` |

---

## 2. Cross-process direction — the core design decision

### 2.1 The problem

`ConsultAsync` is **Runtime-initiated and synchronous** (the Agent awaits advice at
an adjudication point). The harness answer lives in the DSH plugin (Node). The
existing loopback TCP has ONE connection direction in practice: plugin (client)
→ DriverHost (server). Runtime→DSH synchronous RPC cannot ride that direction
directly.

### 2.2 Options

| Option | Shape | Verdict |
|---|---|---|
| A. Reverse listener | DriverHost connects to a second plugin-side listener | ❌ second connection direction + listener lifecycle on the plugin; violates the single-listener simplicity of the frozen transport |
| B. **Pending queue + poll + resolve** | DriverHost keeps a bounded pending-request registry; `ConsultAsync` enqueues and awaits (bounded timeout); plugin POLLS `assistance.pending` (DSH→DriverHost, existing direction), answers with harness intelligence, and submits via `assistance.resolve` | ✅ reuses the existing connection direction; synchronous semantics preserved via bounded await; LLM stays in the plugin |
| C. Event-stream push | reuse DSH session/event fanout for Runtime→DSH | ❌ session events are DSH-internal; not a cross-process Runtime→DSH channel |

**Decision: Option B** — pending registry + `assistance.pending` (poll) +
`assistance.resolve` (submit). Both new wire methods are DSH→DriverHost requests
over the existing connection and reuse the existing codec/error envelope; no
reverse connection, no listener on the plugin.

---

## 3. Wire design (additive methods)

### 3.1 `assistance.pending`

```
DSH → DriverHost: { method: "assistance.pending", params: { } }
→ result: { requests: [ AssistanceRequestDto ... ] }
```

`AssistanceRequestDto` (wire copy of `AssistanceContext`, observation digest only):

| Field | Source | Notes |
|---|---|---|
| `requestId` | `AssistanceContext.RequestId` | correlation identity |
| `runId` | `AssistanceContext.RunId` | |
| `semanticPage` | `AssistanceContext.SemanticPage` | |
| `beliefState` | `AssistanceContext.BeliefState` | Unresolved / Contradicted |
| `worldVersion` | `AssistanceContext.WorldVersion` | observation sequence anchor |
| `observation` | digest of `AssistanceContext.Observation` | element digests (text / perception type / switch state / bounds), NOT raw pixels or full hierarchy — capability-gap context, never a model prompt (F9) |

The registry is bounded (capacity, e.g. 8 pending; oldest first); poll returns the
pending set (read-only; does not dequeue — dequeue happens on resolve or timeout).

### 3.2 `assistance.resolve`

```
DSH → DriverHost: { method: "assistance.resolve", params: {
  requestId, worldVersion, recommendation?, additionalEvidence?, reason? } }
→ result: { resolved: true } | { resolved: false, diagnostic }
```

- `recommendation` is validated against the Agent's accepted whitelist
  (`re-observe` / `rebind` / `dismiss-obstruction`) or null/absent (abandon).
- The DriverHost-side consumer validates echo (requestId + worldVersion match the
  pending entry) before completing the awaited `ConsultAsync`; mismatches are
  rejected with `resolved: false` (the request stays pending until timeout).
- **Resolve writes ONLY the pending-reply slot** — never belief/binding/state/
  GoalEvidence (F3). The Agent applies the advice through its own deterministic
  mechanisms (already implemented in the seam).

### 3.3 Boundedness

- Pending registry capacity (e.g. 8) — overflow rejects the consult (null ⇒ Agent
  fail-closed).
- Per-request consult timeout (e.g. 30s — bounded harness latency) — timeout ⇒
  `ConsultAsync` returns null ⇒ Agent fail-closed (F5).
- Poll is bounded per connection; no unbounded retry.

---

## 4. DriverHost-side provider

```csharp
// src/UniClaw.Runtime.DriverHost/Assistance/ (NEW)
public sealed class AssistanceWireProvider : IAssistanceProvider
{
    // Owns: bounded pending registry (requestId → entry + TaskCompletionSource),
    //       per-request timeout, echo/world-version validation on resolve.
    // Implemented over the DriverHost observability/execution composition.
    public Task<AssistanceAdvice?> ConsultAsync(AssistanceContext context, CancellationToken ct);
}
```

- The registry lives in the DriverHost process (the same process that hosts the
  Agent) — no cross-process hop inside the consult.
- Injected at the composition root (`PhysicalHostComposition.BuildRuntimeGraph` /
  `RunExecutionCoordinator` graph construction) as the Agent's
  `IAssistanceProvider`; null when the harness is absent (fail-closed, zero
  regression — seam already handles null).
- Implements ONLY `ConsultAsync`; owns no Runtime truth.

---

## 5. DSH-side responsibility model — BOUNDARY REPAIR (three frozen roles)

> Repair decision (PROJECT_LEADER_REPAIR_DSH_ASSISTANCE_PROVIDER_ADAPTER_BOUNDARY):
> the transport adapter MUST NOT itself become the intelligence decision layer.
> Three responsibilities are frozen; the middle one (AssistanceBridge) is
> provider-agnostic by contract.

```
┌─ Runtime (DriverHost process) ─────────────────────────────────┐
│ A. AssistanceWireProvider : IAssistanceProvider                │
│    · register pending request · expose pending digest          │
│    · await correlated resolution · validate requestId/         │
│      worldVersion · timeout/cancel/fail closed                 │
│    · OWNS NO INTELLIGENCE                                      │
└──────────────────────────┬─────────────────────────────────────┘
                           │ loopback TCP (existing DSH→DriverHost direction)
┌──────────────────────────▼─────────────────────────────────────┐
│ B. AssistanceBridge (dsh-plugin-uniclaw / integration layer)   │
│    · poll assistance.pending                                   │
│    · translate Runtime AssistanceRequest → DSH-side            │
│      representation                                            │
│    · submit to an AVAILABLE Harness intelligence consumer      │
│    · receive structured result → translate to AssistanceAdvice │
│    · call assistance.resolve · correlation/error handling      │
│    · MUST NOT: own semantic decision policy; hard-code an LLM  │
│      as the ONLY intelligence mechanism; become an             │
│      intelligence router; implement Runtime recovery/planning  │
└──────────────────────────┬─────────────────────────────────────┘
                           │ injectable consumer port (provider-agnostic)
┌──────────────────────────▼─────────────────────────────────────┐
│ C. Harness Intelligence Consumer (DSH side)                    │
│    · actually SOLVES the AssistanceRequest                     │
│    · may use: General Agent / LLM / VLM / Skill / Tool /       │
│      Subagent / deterministic rule / human                     │
│    · selection/composition belongs to HARNESS only             │
└────────────────────────────────────────────────────────────────┘
```

### 5.1 AssistanceBridge — provider-agnostic by contract

- The bridge consumes a narrow **consumer port** (plugin-internal function or
  registered handler), e.g. `consumeAssistance(request) → structuredResult`. It
  does NOT reference `ctx.get('llm')`, any model package, or any single mechanism.
- The bridge owns protocol translation + correlation + error handling ONLY.
- The bridge is the **versioned Runtime ↔ DSH adapter**: it is where the harness
  version binding lives (per the target Integration Layer role).

### 5.2 Selected DSH native seam for the first consumer (repository evidence)

The narrowest existing DSH seam that can host the FIRST Assistance consumer is the
**LlmRuntime** (`@deepseek-ai/dsh-llm`, exposed to plugins as the optional
`ctx.llm` service):

- Evidence: `packages/llm/llm/src/index.ts` — `LlmRuntime extends Service` with a
  provider-neutral `registerAdapter(providers, adapter)` routing surface;
- Precedent: shadow cognition already consumes `ctx.get('llm')` as an optional,
  read-only, non-injected seam (`dsh-plugin-uniclaw/src/plugin.js:140-143`) —
  activation never depends on an inference service.

The first consumer implementation MAY therefore be a simple
`LlmAssistanceConsumer` behind the `ctx.llm` seam — but it is ONE OPTIONAL
HARNESS-SIDE IMPLEMENTATION, not the bridge contract. A `SubagentRuntime`
(`@deepseek-ai/dsh-subagent`, `registerProvider`, pluggable drivers — evidence:
graduation SourceEvidenceMatrix S26) is recorded as the future GENERAL consumer
host (General-Agent solving); it is NOT part of the first slice.

No new generic Harness intelligence framework is invented: the consumer port is a
bridge-internal seam, and the concrete consumers ride existing DSH seams.

**Seam conclusion: `DSH LLM_RUNTIME (ctx.llm) — first optional consumer host;
SubagentRuntime — future general host. No DSH_ASSISTANCE_CONSUMER_SEAM_GAP.**

### 5.3 Timeout / capacity — COMPOSITION_POLICY

- `timeout = 30s`, `pending capacity = 8` are retained for the first
  implementation slice, classified explicitly as **COMPOSITION_POLICY** (DriverHost
  composition defaults), NOT External Contract semantics.
- The contract requires only **bounded consultation** (the seam's fail-closed
  guarantee). A later composition may tune either value without a contract change.

---

## 6. Authority and isolation

- Agent keeps final decision (I-3); advice is candidate information only.
- **The adapter is NOT the intelligence decision layer**: the bridge is a
  provider-agnostic transport translator; intelligence selection/composition
  belongs to the Harness consumer (role C) — never Runtime, never the transport
  adapter.
- LLM/VLM intelligence is confined to the plugin (Node side) and only as an
  OPTIONAL consumer implementation (F2); the DriverHost wire methods are
  deterministic transport; `UniClaw.Runtime` untouched (F7).
- Frozen wire semantics preserved: 8 read-only + `run.start` unchanged; new methods
  additive (F6).
- No new RuntimeEvent kinds/emitters (F8).
- The wire request is capability-gap shaped, not a model prompt (F9).
- The first APPLY proves the full cross-process path with a
  FAKE/DETERMINISTIC consumer — no real model required; a real Harness consumer is
  attached independently afterwards (adapter quality is proven before conflation
  with intelligence quality).

---

## 7. Test plan (APPLY gate — design only here)

| # | Test | Proof |
|---|---|---|
| T1 | `assistance.pending` returns the enqueued request digest | wire test over real server + pending registry |
| T2 | `assistance.resolve` (valid echo + whitelist) completes the awaited ConsultAsync with the advice | wire + provider unit test |
| T3 | `assistance.resolve` (world-version mismatch) → `resolved:false`, consult stays pending → timeout → Agent fail-closed | stale/echo test |
| T4 | Consult timeout ⇒ null ⇒ Agent fail-closed (no hang) | provider unit test |
| T5 | registry capacity overflow ⇒ consult rejected ⇒ Agent fail-closed | provider unit test |
| T6 | bridge: poll + consumer-port submission + resolve; **fake/deterministic consumer** (no model); advice normalized to whitelist | node tests (fake consumer) |
| T7 | E2E: scripted environment + real DriverHost server + coordinator + AssistanceWireProvider + real plugin bridge + **fake consumer** → consult resolves → SAME goal continues (extend e2e-run-start pattern) | cross-process E2E, **model-free** |
| T8 | zero Runtime change; guards (F2/F6/F7/F8/F10); bridge source references no llm/model package | guard scans + git footprint + static scan |
| T9 | null provider (harness absent) → zero regression | existing seam tests |
| T10 | bridge provider-agnostic: a stub consumer replacing the fake resolves identically (consumer port swappable) | node test |

---

## 8. Deferred (explicitly NOT this change)

- Implementation (APPLY gate).
- Real Harness intelligence consumer attachment (LlmAssistanceConsumer behind
  `ctx.llm`, or SubagentRuntime general host) — attached independently AFTER the
  model-free path is proven (adapter is proven before being conflated with
  intelligence quality).
- Async correlation channel beyond the pending registry.
- Guidance (Plane 4) / Execution Handoff (Plane 5).
- TaskSpec / AgentProfile / intelligence settings.
- Changing the Runtime seam or any Runtime file.
