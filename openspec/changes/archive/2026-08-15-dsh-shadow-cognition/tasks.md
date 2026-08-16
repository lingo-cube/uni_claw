# Tasks: dsh-shadow-cognition

> System of record for implementation progress. Check each box the moment the
> task is complete; final counts are reported in the leader result.
> Baseline gate (`PROJECT_LEADER_CREATE_DSH_SHADOW_COGNITION_BASELINE`) =
> design-only: Slice 0 complete, everything else pending implementation.
> **REBASELINE 2026-08-15** (`PROJECT_LEADER_DSH_SHADOW_COGNITION_DURABILITY_REBASELINE_DECISION`):
> V1 durability rebaselined to `EPHEMERAL_PROCESS_LOCAL` (zero custom session
> events), triggers frozen to `human.request` ONLY. Slices 6–7 below reflect
> the rebaseline; implementation is NOT complete.
> (next gate: `PROJECT_LEADER_APPLY_DSH_SHADOW_COGNITION_V2`).

## Slices

- [x] Slice 0 — OpenSpec baseline scaffolding (proposal, design, spec,
      source-evidence-matrix, README, .openspec.yaml, tasks) — DESIGN ONLY,
      zero production code; rebaselined 2026-08-15 (durability → ephemeral,
      triggers → human.request only)
- [x] Slice 1 — Shadow module skeleton (`dsh-plugin-uniclaw/src/shadow/` — the
      design's "or equivalent minimal organization" directive; implemented as a
      `src/shadow/` directory: `analysis.js`, `context.js`, `model.js`,
      `cache.js`, `index.js`): `shadow` service provide, config validation
      (`shadow.enabled`, `shadow.model.{provider,model}`,
      `shadow.autoTriggers` (reserved, MUST be `[]` in V1 — auto triggers
      deferred), `shadow.maxEvents`, `shadow.maxContextChars`,
      `shadow.maxEvidenceRefs`, `shadow.evidenceBytesPerRef`,
      `shadow.timeoutMs`, `shadow.visual.enabled`), read-only retrieval facade
      wiring
- [x] Slice 2 — Deterministic context assembler: latest RunSnapshot + bounded
      recent RuntimeEvents (causal window) + trap detail on trap focus + lazy
      EvidenceRef resolution; hard caps enforced (F11, F12)
- [x] Slice 3 — Model invocation via `ctx.llm.stream` (one-shot `GenerateOptions`,
      DSH-config provider/model, AbortSignal timeout, no loop marker, no tools);
      deterministic digest path when model not configured
- [x] Slice 4 — `ShadowAnalysis` output builder: bounded schema, evidence
      hierarchy classifications, `classification: 'COGNITIVE_INFERENCE'`,
      uncertainties, recommendations (human-facing), ModelCallRecord accounting
- [x] Slice 5 — `uniclaw-shadow-analyze` command (naming audited, session-scoped,
      zero-model dispatch, structured text output, `command/run`+`command/done`
      lifecycle)
- [x] Slice 6 — Ephemeral durability (rebaselined): ZERO custom session events
      (no `shadow/analysis` session event; no dual-write workaround, no
      detached session); bounded process-local cache
      (`Map<runId, bounded recent ShadowAnalysis>`, non-authoritative,
      disposable — NOT a Memory/Knowledge Store/History Database);
      restart-loss truthfulness (F15 rebaselined) + zero-custom-session-event
      guard
- [x] Slice 7 — Trigger model (rebaselined): human request ONLY (V1);
      `run.failed`/`run.completed` auto triggers DEFERRED
      (`AutoTriggersDeferredUntilConsumerExists = YES`), `shadow.autoTriggers`
      reserved and validated empty; no auto-trigger machinery built, no
      per-event invocation
- [x] Slice 8 — Failure isolation (F5/F6/F7 + cache/context failures) and
      model-call accounting (trigger, EvidenceRefs, inputEventCount, contextChars,
      status, timestamps)
- [x] Slice 9 — Architecture guards + falsifier tests F1–F16 (authority grep
      guards, zero Runtime delta scan, zero-custom-session-event guard,
      restart-loss truthfulness test, identity separation test, boundedness
      assertions)
- [x] Validation — node test suite (101/101), `openspec validate
      dsh-shadow-cognition --strict --no-interactive`, `scripts/check-consistency.sh`,
      pinned DSH checkout clean, zero-delta production scan, zero custom session
      events written
