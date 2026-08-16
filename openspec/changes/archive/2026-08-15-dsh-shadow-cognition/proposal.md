# Proposal: dsh-shadow-cognition

## Problem

The DSH↔UniClaw chain is complete up to the control plane: the frozen protocol
baseline (`dsh-uniclaw-control-plane-protocol-baseline`, archived) and the
graduated plugin slice (`dsh-uniclaw-control-plane-plugin-implementation`,
archived, maturity `DSH_UNICLAW_CONTROL_PLANE_PLUGIN_INTEGRATED`) give DSH a
deterministic, read-only, zero-model window onto Kernel-produced evidence
(`RuntimeEvent`, `RunSnapshot`, `EvidenceRef`) and run state. Humans can inspect
a run deterministically (`uniclaw-inspect-run`, `uniclaw-inspect-trap`,
`uniclaw-evidence-open`, `uniclaw-runs-list`), but nothing in DSH interprets
what the evidence appears to mean. The chain ends at "here are the facts".

There is no DSH-side interpretation layer, and — critically — there is no
frozen design for one. Any future cognition capability must be bounded:
Kernel truth must never depend on it, it must never gain execution or
authorization authority, and it must be built on DSH-native seams rather than
invented machinery.

## What this change does

This change is a **design-only baseline** (`SOURCE_FIRST_BUYER_DRIVEN_OPENSPEC_BASELINE`).
It freezes the minimum DSH-native **Shadow Cognition** capability: DSH observes
Kernel-produced evidence and run state, optionally invokes cognitive/model
reasoning, produces DSH-side interpretation artifacts (hypotheses, diagnostic
summaries, candidate explanations, human-facing recommendations), and lets
humans inspect them. Kernel consumes ZERO shadow outputs.

> **REBASELINE 2026-08-15** — `PROJECT_LEADER_DSH_SHADOW_COGNITION_DURABILITY_REBASELINE_DECISION`
> (`BASELINE_ASSUMPTION_FALSIFIED`): items 2 and 8 below were amended. V1
> durability is `EPHEMERAL_PROCESS_LOCAL` — Shadow V1 appends ZERO custom
> session events (the pinned `Session.append` cannot carry the `ignorable`
> marker; a live-appended unknown event makes the session log refuse to load —
> see design.md §9 and source-evidence-matrix.md M13–M19). V1 triggers are
> frozen to human request ONLY; `run.failed`/`run.completed` auto triggers are
> DEFERRED. The buyer (post-hoc / near-live run interpretation) is unchanged
> and still satisfied: a human request can deterministically reconstruct fresh
> context from the Kernel read surface and recompute analysis after a restart.

The baseline freezes:

1. **First buyer** — post-hoc / near-live run interpretation.
2. **Trigger model** — human-requested analysis (mandatory, always available)
   ONLY in V1. Terminal run-state triggers (`run.failed`, `run.completed`) are
   DEFERRED (`AutoTriggersDeferredUntilConsumerExists = YES`); the reserved
   `shadow.autoTriggers` key must be empty. No model invocation on every
   `RuntimeEvent`.
3. **Read-only source inputs** — only the already-graduated read surfaces
   (`RuntimeEvent`, `RunSnapshot`, `EvidenceRef`); zero new Runtime emitters;
   unavailable data is expressed as explicit uncertainty, never fabricated
   Kernel fact.
4. **Evidence hierarchy** — KERNEL FACT / DERIVED READ MODEL / EVIDENCE REF /
   SHADOW INFERENCE / SHADOW RECOMMENDATION never collapse; every artifact is
   classified `COGNITIVE_INFERENCE`, never `WORLD_TRUTH`, `KERNEL_FACT`,
   `ACTION_AUTHORIZATION`, or `GOAL_EVIDENCE`.
5. **Output model** — one bounded `ShadowAnalysis` artifact (RunId, AnalysisId,
   Trigger, EvidenceRefs, ObservedFacts, Hypotheses, Uncertainties,
   HumanSummary, …). No confidence framework, severity ontology, memory,
   planner state, execution proposal, or approval status.
6. **Bounded context assembly** — deterministic retrieval first (snapshot →
   bounded events → trap → lazy EvidenceRef), bounded causal window, no
   unbounded transcript accumulation, lazy visual evidence.
7. **DSH-native model invocation** — `ctx.llm` (`LlmRuntime` /
   `GenerateOptions`) with DSH-config-selected provider/model; one-shot calls;
   no new provider framework; no model-facing Kernel tools; model-call
   accounting recorded.
8. **Durability** — `EPHEMERAL_PROCESS_LOCAL`: Shadow V1 appends ZERO custom
   session events (no `shadow/analysis` event exists; no `ignorable` marker is
   needed because nothing is appended). Completed analyses MAY live in a
   bounded process-local cache (non-authoritative, disposable); DSH restart
   loses them truthfully and a fresh analysis is recomputable on demand.
   Dual-write workaround and detached Shadow sessions are FORBIDDEN; deferred
   pressure item `DSH_SHADOW_DURABILITY_EXTENSION` recorded (design.md §9.4).
9. **Human surface** — `uniclaw-shadow-analyze` command on the existing
   DSH-native command registry (naming audited against existing `uniclaw-*`
   commands); command response is the authoritative inspection surface; no new
   frontend.
10. **Failure isolation** — fail-open relative to Kernel: model unavailable,
    timeout, context-assembly failure, bounded-cache failure, DSH restart never
    stop a Kernel run, never change a Goal, never create a Kernel Trap, never
    change Agent state or completion.
11. **Authority guard boundary** — architecturally enforced: the frozen wire
    contract has ZERO mutation methods, the plugin owns no physical capability,
    and `src/UniClaw.Runtime` is untouched. Shadow cannot dispatch ADB, mutate
    Container/Binding/StateBelief, or create GoalEvidence.
12. **Session ↔ Run semantics** — one DSH session ↔ zero/one/many inspected
    Kernel RunIds; `SessionId == RunId` is never asserted; no Kernel run is
    created from session creation.

No implementation. No production mutation. No Runtime change. No runtime agent
change. No DriverHost semantic change. No new Runtime emitters. No Advisory,
no Blocking cognition, no physical authority, no archive.

## Non-goals (explicitly out of scope)

- Production implementation of any Shadow code (this is a baseline gate only).
- Advisory cognition (Kernel/Agent may consume a cognitive proposal but keeps
  final authority) — belongs to a later change; explicitly forbidden here are
  `DecisionProposed` / `DecisionAccepted` / `ActionAuthorized` RuntimeEvents,
  proposal ingestion into Agent, and any semantic action suggestion consumed by
  Kernel.
- Blocking cognition, physical authority, C-class decision/authorization events.
- New Runtime semantic emitters, Runtime semantic model changes, Runtime
  Agent changes, DriverHost semantic changes.
- New model/provider framework, custom WebSocket, parallel session model,
  custom agent runtime, custom `ShadowEventBus`, UniClaw cognition protocol.
- Confidence scoring framework, severity ontology, memory system, planner
  state, execution proposals, approval status, action authorization.
- Auto-invoking a model on any `RuntimeEvent`: terminal `run.failed` /
  `run.completed` auto triggers are DEFERRED (not built) in V1
  (`AutoTriggersDeferredUntilConsumerExists = YES`); trap interpretation is
  human-requested.
- Any durable Shadow persistence in V1: no `shadow/analysis` session event,
  no dual-write workaround, no detached Shadow session
  (`EPHEMERAL_PROCESS_LOCAL`, design.md §9).
- A new frontend for Shadow output.
- Re-opening any graduated change (the three prerequisites in §0 of the
  directive stay frozen).
