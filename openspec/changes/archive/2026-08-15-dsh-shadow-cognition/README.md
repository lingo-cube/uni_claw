# README: dsh-shadow-cognition

Design-only baseline (`SOURCE_FIRST_BUYER_DRIVEN_OPENSPEC_BASELINE`) for the
minimum DSH-native **Shadow Cognition** capability: DSH observes
Kernel-produced evidence and run state, optionally invokes cognitive/model
reasoning, produces DSH-side interpretation artifacts, and lets humans inspect
them — while Kernel consumes ZERO shadow outputs.

- **Status:** active change, NOT implemented, NOT archived.
- **Gate:** `PROJECT_LEADER_CREATE_DSH_SHADOW_COGNITION_BASELINE` →
  `BASELINE_READY_FOR_IMPLEMENTATION` (design frozen); **REBASELINED 2026-08-15**
  by `PROJECT_LEADER_DSH_SHADOW_COGNITION_DURABILITY_REBASELINE_DECISION`
  (`BASELINE_ASSUMPTION_FALSIFIED`) — durability → `EPHEMERAL_PROCESS_LOCAL`
  (zero custom session events), triggers → human.request only.
- **Next gate:** `PROJECT_LEADER_APPLY_DSH_SHADOW_COGNITION_V2`.
- **Pinned DSH:** commit `47f943859bef60e4160492346772ded9b24f765a`
  (`0.1.0-rc.5`), read-only checkout.
- **Frozen prerequisites:** `dsh-uniclaw-control-plane-protocol-baseline` and
  `dsh-uniclaw-control-plane-plugin-implementation` (archived, graduated).

## Documents

- [proposal.md](proposal.md) — problem, scope, non-goals, authority
- [design.md](design.md) — D1–D12 frozen decisions, context policy, durability
  (§9, rebaselined), model seam, failure isolation, falsifier matrix F1–F16
- [specs/dsh-shadow-cognition/spec.md](specs/dsh-shadow-cognition/spec.md) —
  ADDED requirements with scenarios
- [source-evidence-matrix.md](source-evidence-matrix.md) — M1–M12 seams plus
  M13–M20 falsification evidence, all verified against the pinned DSH source
- [tasks.md](tasks.md) — implementation checklist (system of record; Slice 0
  done, rest pending)

## Key frozen decisions

- **Buyer:** post-hoc / near-live run interpretation (human-requested).
- **Triggers:** `uniclaw-shadow-analyze` (mandatory) — human.request ONLY in
  V1; `run.failed` / `run.completed` auto triggers DEFERRED
  (`AutoTriggersDeferredUntilConsumerExists = YES`); never per-event.
- **Model seam:** `ctx.llm` (`LlmRuntime.stream(GenerateOptions)`), one-shot,
  DSH-config-selected provider/model, no tools, no new provider framework.
- **Durability:** `EPHEMERAL_PROCESS_LOCAL` — Shadow V1 appends ZERO custom
  session events (no `shadow/analysis` event exists; no `ignorable` marker
  needed); bounded process-local cache only; restart loses analyses truthfully
  and fresh analysis is recomputable on demand; dual-write workaround and
  detached Shadow sessions forbidden.
- **Human surface:** `uniclaw-shadow-analyze` command (response = authoritative
  inspection surface); no new frontend.
- **Authority:** Kernel consumes zero shadow outputs; zero wire mutation
  methods; `src/UniClaw.Runtime` untouched; Shadow only — no Advisory.
