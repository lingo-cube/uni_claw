# Proposal: dsh-assistance-provider-adapter

## Buyer

The Runtime-side Assistance seam is implemented (`runtime-assistance-seam`,
`IAssistanceProvider.ConsultAsync` in `UniClaw.Runtime/Capabilities/Brain/`). The
Runtime can now ASK for external information at belief adjudication points — but
**nothing implements that interface yet**: with no provider injected, the seam is
inert and every adjudication still fails closed.

This gate delivers the **DSH-side provider adapter**: the external intelligence
harness (DSH) becomes the concrete `IAssistanceProvider` implementation, answering
Runtime assistance requests with harness intelligence (e.g. LLM/VLM/记忆 via the
DSH `ctx.llm` seam), while preserving every authority boundary (advice is candidate
information; the Agent keeps final decision — I-3).

This is the BASELINE gate for the mother-doc `dsh-intelligence-provider-integration`
intent (`docs/decisions/outer-intelligence-integration-architecture.md` §3), scoped
and named against the contract's Assistance terminology.

## Gap

- `IAssistanceProvider` has **zero implementations** (verified: only the interface
  + tests' fake exist).
- **No Runtime→DSH request channel exists**: the loopback TCP transport is
  one-directional in practice — the DSH plugin CONNECTS (client) to the DriverHost
  (server) and issues requests DSH→Runtime. Assistance is Runtime→DSH and
  synchronous (`ConsultAsync` awaits). A channel in the reverse semantic direction
  must be designed.
- The DSH plugin has an **LLM seam precedent** (`ctx.get('llm')`, used by shadow
  cognition, not injected — activation never depends on an inference service) but
  no assistance consumer.
- DriverHost has 9 wire methods (8 frozen read-only + `run.start`); no assistance
  method family exists.

**Earliest missing system link: `HARNESS_SIDE_ASSISTANCE_PROVIDER_ADAPTER`.**

## What this change does (BASELINE gate)

**Design + spec only (no code in this gate; APPLY later).**

1. Resolves the **cross-process direction** with a pending-queue poll/resolve
   design that reuses the existing DSH→DriverHost connection direction:
   - `assistance.pending` — DSH→DriverHost read poll: returns the bounded set of
     pending assistance requests (requestId / runId / semantic page / belief state
     / world version / observation digest).
   - `assistance.resolve` — DSH→DriverHost submit: attaches an `AssistanceAdvice`
     (echoing requestId + world version) or abandons the request (null → the Agent
     fails closed).
   - The DriverHost-side `ConsultAsync` enqueues the request and awaits a bounded
     response (timeout ⇒ null ⇒ Agent fail-closed).
2. Defines the **DriverHost-side provider**: `AssistanceWireProvider : IAssistanceProvider`
   (constructed over the pending-request registry) — the seam's first real
   implementation, injected at the composition root into the Agent.
3. Defines the **DSH-side responsibility model as three frozen roles** (boundary
   repair): the DriverHost-side `AssistanceWireProvider` (transport only, owns NO
   intelligence), the plugin-side **`AssistanceBridge`** (versioned protocol
   translator — polls pending, translates the Runtime AssistanceRequest into a
   DSH-side representation, submits to an AVAILABLE Harness intelligence consumer,
   translates the structured result to `AssistanceAdvice`, resolves; MUST NOT own
   semantic decision policy, MUST NOT hard-code an LLM as the only mechanism, MUST
   NOT become an intelligence router), and the **Harness Intelligence Consumer**
   (DSH side, behind existing DSH capability seams — the role that actually solves
   the request; may use General Agent / LLM / VLM / Skill / Tool / Subagent /
   deterministic rule / human; selection belongs to the Harness).
4. Removes any design requirement that the plugin MUST call `ctx.get('llm')`
   directly: the bridge contract is **model/provider agnostic**. A simple LLM
   resolver MAY exist later as one optional Harness-side implementation. The
   narrowest existing DSH seam hosting the first consumer is the **LlmRuntime**
   (`@deepseek-ai/dsh-llm` via the optional `ctx.llm` service — repository
   evidence: shadow cognition's non-injected `ctx.get('llm')` precedent); the
   `SubagentRuntime` (`@deepseek-ai/dsh-subagent`, pluggable drivers) is recorded
   as the future GENERAL consumer host. No new generic Harness intelligence
   framework is invented.
5. Fixes **boundedness and timeout**: pending registry capacity (8) and per-request
   timeout (30s) are retained for the first slice but classified explicitly as
   **COMPOSITION_POLICY** (not External Contract semantics — the contract only
   requires bounded consultation); timeout/abandon ⇒ `ConsultAsync` returns null ⇒
   the Agent fails closed (never hangs, never fabricated progress).
6. Fixes **authority**: the resolve path writes ONLY the pending-request reply; it
   never writes Kernel state; the Agent keeps final decision (I-3); advice ≠ truth
   / authorization / goal completion; world-version binding enforced on both sides.
7. Fixes **isolation**: LLM/VLM intelligence lives only in the plugin (Node side)
   and only as an OPTIONAL consumer implementation; the DriverHost wire methods are
   additive; `UniClaw.Runtime` is untouched by this gate (the seam already exists);
   no new RuntimeEvent kinds/emitters.
8. **First APPLY proves the model-free path**: Runtime → AssistanceWireProvider →
   pending wire → AssistanceBridge → fake/deterministic consumer → resolve →
   Runtime → fresh world verification — WITHOUT a real model; a real Harness
   consumer is attached independently afterwards.

## Non-goals (explicitly out of scope)

- Implementing the adapter (APPLY gate, after this baseline validates).
- **The adapter becoming the intelligence decision layer**: semantic decision
  policy, intelligence routing, and recovery/planning semantics belong to the
  Harness consumer, never to the transport bridge/adapter.
- **Hard-coding an LLM as the only intelligence mechanism**: the bridge contract is
  provider-agnostic; an LLM resolver is one optional Harness-side consumer
  implementation, attached independently after the model-free path is proven.
- Changing the Runtime seam or Agent (already implemented; untouched).
- Any new RuntimeEvent kind/emitter; any Runtime semantics change.
- Async correlation channel beyond the pending registry (reserved).
- Guidance (Plane 4) / Execution Handoff (Plane 5) — far-term gates.
- TaskSpec / AgentProfile / intelligence settings — not assumed to exist.
- Changing the frozen 8 read-only methods or `run.start` semantics.

## Required output

`PROJECT_LEADER_DSH_ASSISTANCE_PROVIDER_ADAPTER_BASELINE_RESULT` with Decision
`BUYER_CONFIRMED` (verified gap) or `BUYER_ALREADY_SATISFIED`, the OpenSpec change
(proposal/design/spec/tasks) created and validated, and `NEXT_GATE =
PROJECT_LEADER_APPLY_DSH_ASSISTANCE_PROVIDER_ADAPTER` (after buyer confirmation).

## Authority (unchanged)

- `AgentKeepsSemanticDecisionAuthority = MUST_HOLD` (I-3): advice is candidate
  information only; the Agent authorizes every resulting action.
- `AdviceDoesNotWriteRuntimeState = MUST_HOLD`; `AdviceIsNotTruthOrAuthorizationOrCompletion = MUST_HOLD`.
- `AssistanceIsCapabilityGapExpression = MUST_HOLD`: the wire request expresses a
  missing capability; the DSH-side answer MAY use harness intelligence, but the
  wire vocabulary is capability-gap shaped (not a model prompt).
- `DirectDSHPhysicalAuthority / DirectDSHGoalEvidenceAuthority / DirectDSHBindingAuthority / DirectDSHStateBeliefAuthority = MUST_BE_NO`.
- `FrozenWireSemantics = PRESERVED` (8 read-only + `run.start` untouched; new
  methods additive).
- `LLMConfinedToPlugin = MUST_HOLD` (harness intelligence only in the Node plugin,
  and only as an OPTIONAL consumer implementation; DriverHost/Runtime never
  reference a model).
- `AdapterIsNotIntelligenceDecisionLayer = MUST_HOLD` (the bridge is a
  provider-agnostic transport translator; intelligence selection/composition
  belongs to the Harness consumer).
- `BridgeProviderAgnostic = MUST_HOLD` (no direct `ctx.get('llm')` requirement; no
  single mechanism hard-coded in the bridge).
- `BoundedConsult = MUST_HOLD` (capacity + timeout; overflow/timeout ⇒ fail-closed;
  concrete values are COMPOSITION_POLICY, not contract semantics).
- `FirstApplyModelFree = MUST_HOLD` (the cross-process path is proven with a
  fake/deterministic consumer; no real model required).

## Falsifiers

| # | Falsifier | Fails if |
|---|---|---|
| F1 | reverse connection | the design requires the DriverHost to connect to a plugin listener (second connection direction) |
| F2 | LLM in runtime/driverhost | the DriverHost or Runtime references a model/LLM; intelligence is not confined to the plugin |
| F3 | resolve writes kernel state | `assistance.resolve` writes belief/binding/state/GoalEvidence instead of only the pending reply |
| F4 | advice-as-authority | the adapter treats advice as truth/authorization/goal-completion |
| F5 | unbounded consult | the pending registry or consult wait is unbounded (no capacity/timeout) |
| F6 | frozen semantics change | the 8 read-only methods or `run.start` semantics change |
| F7 | runtime seam change | this gate modifies `UniClaw.Runtime` (the seam already exists; untouched) |
| F8 | new emitters | new RuntimeEvent kinds/emitters are introduced |
| F9 | wire prompt leak | the wire request carries a model prompt instead of the capability-gap context |
| F10 | fabricated claims | any repository-reality statement contradicts verified source |
| F11 | adapter as decision layer | the bridge owns semantic decision policy, intelligence routing, or Runtime recovery/planning semantics (those belong to the Harness consumer) |
| F12 | model-gated first apply | the first APPLY cross-process path requires a real model instead of a fake/deterministic consumer |

## Validation

- `openspec validate dsh-assistance-provider-adapter --strict --no-interactive`
- `scripts/check-consistency.sh`
- Cross-check against `runtime-assistance-seam` (seam contract) and
  `runtime-external-contract-baseline` (Plane 3) and the mother-doc §3.
