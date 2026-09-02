# P26-V2 Slow Shadow Wiring — Bounded Experiment Stage 1 (Shadow-only)

STATUS: `SLOW_SEMANTIC_SHADOW_WIRED_SHADOW_ONLY` (authorized:
`SLOW_SEMANTIC_SHADOW_EXPERIMENT_APPROVED_BOUNDED`; NOT Slow production
graduation; no Slow action/recovery authority purchased)

Date: 2026-09-02

## What was purchased by the prior R5/R7 stages (unchanged)

- `ISlowContainerSemanticAdvisor` (Disabled/Shadow/AsyncAdvisory modes,
  Confirm/Challenge/Correct/Insufficient kinds, exact evidence correlation,
  `HasRuntimeEffect = false` for Shadow) — frozen contract, no modification.

## IMPLEMENTED (this stage — harness-side only, zero production Runtime change)

- **`QwenVlSlowAdvisor`** (`src/UniClaw.Runtime.ValidationHarness/
  SettingsCampaign/SlowShadow/QwenVlSlowAdvisor.cs`): the first concrete
  `ISlowContainerSemanticAdvisor` — local Qwen2.5-VL-3B-UI-R1 (GGUF Q4_K_M +
  mmproj) served by `llama-server` (OpenAI-compatible
  `/v1/chat/completions`, base URL default `http://127.0.0.1:8765`).
  - Input per invocation: the frame's PNG screenshot (base64 image), the
    structured perception candidates (text/kind/bounds, capped 40), the
    observable Fast current-container identity and the prior container —
    revision-bound via the request (ObservationRef / EvidenceRevision /
    NodeRef / SourceNodeRef echo enforced by the frozen contract).
  - Output is constrained to the allowed bounded vocabulary ONLY: kind
    (Confirm/Challenge/Correct/Insufficient), scene (SceneAssessment:
    unknown/normal/advertisement/transient/loading/overlay/unrelated/
    off_path/wrong_child), container_semantic, trigger_semantic,
    candidate_interpretation (→ `Details`), corrected_identity
    (→ `CorrectedIdentityCandidate`), evidence_usefulness,
    suggested_disposition. No action, recovery, or goal surface exists on
    the advisor.
  - Fail-closed: unparseable/absent/faulty provider output → `Insufficient`
    with the issue recorded; the raw model output is ALWAYS retained in the
    metric record for FalseCorrection review. A provider fault never escapes
    into the harness run.
- **`SlowShadowEvaluator`** (same dir): per-observation Shadow-mode
  acquisition through the REAL purchased seam —
  `SlowContainerSemanticConsumer.AcquireAsync(Shadow, …)` + `Project(…)` —
  queued off the run's critical path (sequential, bounded drain at campaign
  end). Requests mirror the production Agent conventions
  (`observation:{seq}`, revision = seq, `agent-node:{seq}:{page}`); the Fast
  assessment is constructed from the observable Fast live page resolution
  bound to the same revision/nodes. Produces the ledger
  (`p26-slow-shadow-ledger.v1`) with per-entry metrics + summary:
  SlowInvocations / Confirm / Challenge / Correct / Insufficient /
  ConflictsWithFast / ParseFailures / avg+max latency / prompt+completion
  tokens.
- **Campaign wiring** (`SettingsCampaignProgram.cs`): opt-in
  `P26_SLOW_SHADOW=1` (absent/false = byte-identical frozen Fast-only
  baseline; same opt-in pattern as `P26_CAPTURE_STAGE_VIEWS`). The artifact
  tap (screenshot + candidates per frame) and the observation tap (stabilized
  observation + `ResolveSemanticPage`) feed the evaluator. Production Agent
  path untouched: Slow stays `Disabled` in the Agent (spec-mandated,
  guard-enforced by `ContainerRuntimeV2LiveStateReplacementArchitectureGuardTests`).
- Knobs: `P26_SLOW_SHADOW_URL`, `P26_SLOW_SHADOW_TIMEOUT_SEC` (default 90),
  `P26_SLOW_SHADOW_LEDGER` (default `/tmp/p26-slow-shadow-ledger.json`).

## Honest harness-side input gaps (recorded per experiment, by design)

TriggerOccurrence / TransitionOccurrence / Graph candidates are
Agent-internal and not exposed on any read surface the harness taps can see
— requests carry them null, and the gap is itself experimental evidence: a
real ASYNC_ADVISORY consumption point must acquire them Runtime-side
(exactly the R5 deferred scope). The shadow Fast assessment is the OBSERVED
Fast live outcome (page resolution), not the Agent-internal
FastContainerAssessment.

## VALIDATED

- Provider smoke (live): llama-server + Qwen2.5-VL vision chat completion on
  a synthetic settings-page image — 2.5s latency, 521 prompt tokens, token
  usage reported (preview doc: `docs/experiments/qwen2.5-vl-local-preview.md`).
- Focused tests (`tests/UniClaw.Runtime.Tests/ValidationHarness/
  SlowShadowAdvisorTests.cs`, 7/7 green): bounded-output mapping + binding
  echo; unparseable output → Insufficient fail-closed with raw retained;
  missing frame context → fail-closed; provider fault never escapes;
  artifact-pixel/container-context merge; evaluator Shadow-mode
  acquisition/projection through the real seam (revision-bound entries,
  ConflictsWithFast projection, ledger write, trigger/transition gap
  recorded).
- `dotnet build src/UniClaw.Runtime.sln`: 0 errors.
- Full `dotnet test src/UniClaw.Runtime.sln`: failure set EXACTLY the
  pre-existing baseline (4 Runtime.Tests: ScrollStability TitleOff
  [documented R8-B], ScenarioKnowledge guard token [pre-existing untracked
  prior-session file], Capstone/ExternalBoundary [require live emulator];
  5 Semantic V2/V3 qualification [prior session in-flight]) — 2629 passed,
  zero new failures from this stage.
- `scripts/check-consistency.sh`: ALL PASS; strict OpenSpec change
  validation: passed.
- Perception suites unaffected by this stage (engine unchanged since the
  residual-repair green run: pytest 384 + 95 subtests, unittest 337).

## NEXT_WORKITEM

Fresh Phase 2.6 campaign rounds (Fast-only frozen baseline + Shadow ledger):
fresh emulator boot per round, `P26_SLOW_SHADOW=1 settingscampaign 1`,
record per-run evidence + ledger, classify each blocker
A (SEMANTIC_SIDE_BUYER) / B (PERCEPTION_SIDE_BUYER) / C (SLOW_RISK), then
produce `PHASE_2_6_FAST_PLUS_SLOW_SHADOW_RESULT`.
