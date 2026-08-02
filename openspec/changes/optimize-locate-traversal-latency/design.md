## Context

The visible locate run passes but takes 286s against a 120s budget. Latency decomposition: per-step ADB duplicate work (RunAssetHook re-runs screencap + uiautomator dump per step; BoundaryHook re-runs dumpsys per step) and redundant AI vision calls (a scroll step fires up to 6 `AnalyzeCurrentPageAsync` calls at ~12s each). All deterministic alternatives exist (`UiAutomatorPageAnalysis`, `HierarchyFingerprint` from `AdbScreenStateProvider`, `PageSnapshotManager.Fingerprint()`) but are wired with the wrong ordering or invalidation conditions.

Related change `harden-deterministic-verification-async-evidence` (P0) owns deterministic post-action verification and (P1) the full async evidence pipeline; this change stays within the per-step hot path and deliberately leaves those scopes alone.

## Goals / Non-Goals

**Goals:**
- Eliminate duplicate per-step ADB calls (uiautomator dump ×2, dumpsys ×1 per step today)
- Eliminate remote vision calls that a deterministic fingerprint proves redundant
- Make step asset writes non-blocking without losing durability at finalization
- Bring locate end-to-end from 286s to under the 120s scenario budget

**Non-Goals:**
- No Core public contract changes (`IPageAnalyzer`, `ITraceRecorder`/`ITraceStorage`, locked enums) — all changes land in Host composition or Core internal logic
- No ADB transport speed work (screencap bandwidth is a physical limit; we reduce call count, not call cost)
- No fixed-sleep micro-tuning (exploration confirmed all fixed delays are <1s and are not the bottleneck)
- Not implementing harden's full async evidence pipeline — only a step-scoped asset sink here

## Decisions

### D1: Hierarchy XML/fingerprint is shared via step context; hook keeps one screencap

`UiAutomatorAugmentingPageAnalyzer` already calls `_screenState.RefreshAsync()`. That result (`ScreenStateResult` with `HierarchyFingerprint` + XML) is written to the step context. `RunAssetHook` consumes the freshest step-context XML instead of issuing its own two ADB dump calls.

The screenshot cannot be shared back out of `IPageAnalyzer` (Core interface constraint, no change), so `RunAssetHook` keeps its single `CaptureAsync` per observation point for evidence integrity — the analysis screenshot serves the model, the hook screenshot serves the evidence. This removes 2 of 3 ADB calls per step (uiautomator dump ×2), keeping 1 screencap.

*Alternative rejected: share the analysis screenshot by extending `IPageAnalyzer` — violates the no-Core-contract-change constraint.*

### D2: Step-scoped asset sink with finalization drain

`StepAssetWriter` calls are replaced by submission to a bounded `Channel<WriteTask>` consumed by one background writer; run finalization drains and flushes the channel before the result is recorded. This is a deliberately small, step-scoped precursor: when harden's P1 `asynchronous-run-evidence` pipeline lands, this sink can be swapped out without changing hook interfaces.

*Alternative rejected: fully async all assets now — overlaps harden P1 scope.*

### D3: Boundary checks are fingerprint-gated

`BoundaryHook` records the last-checked fingerprint and a step counter; it issues `dumpsys` only when the fingerprint changed since the last check or every N steps (N=5). First check always runs.

### D4: Page-analysis cache retention keyed on hierarchy fingerprint

`InvalidatingPageAnalysisCache` stops invalidating on every action. Cache entries carry the `HierarchyFingerprint` of the moment they were produced. On `AnalyzeCurrentPageAsync`, the decorator first runs `RefreshAsync` (1-2s, XML lands in step context for D1), compares fingerprints: equal → return cached analysis (saves ~12s AI call); different → full analysis, store with new fingerprint. The action executor only marks the cache dirty; actual validity is decided by fingerprint at read time.

Dynamic-content risk (fingerprint equal but pixels changed, e.g. spinner animation) is covered by the spec's fallback rule and by safety-before-action always evaluating on freshest state.

### D5: Post-scroll analysis skipped on fingerprint equality

`InterceptionHandler.TryHandleScrollAsync` already has the pre-swipe `ScreenStateResult` in the step context. After `SwipeAsync`, it runs `RefreshAsync` once; equal fingerprint → treat as end-of-list, no vision call; different → analyze the new state.

### D6: ResultVerify early exit on unchanged fingerprint

`TraversalFSM.HandleResultVerifyAsync` records the pre-action fingerprint (from step context) and compares after its first verification attempt; equal → exit the retry loop immediately (no further vision calls), treating verification as pending. Changed → existing retry behavior.

### D7: UIAutomator-first analysis with vision fallback

`UiAutomatorAugmentingPageAnalyzer` reorders: `RefreshAsync` → `UiAutomatorPageAnalysis.Parse(xml, screenState)` → completeness check (non-empty clickable items AND (page identity OR scroll state resolved)) → return deterministic result, or fall back to `_visual.AnalyzeCurrentPageAsync()`. The merge path is kept for the fallback case.

### D8: Vision payload lite

- **Downscale (deferred dependency check):** providers downscale to max 720px width before base64 if an image library is available without adding a heavy dependency; otherwise keep full-size and ship only the prompt side. Evidence assets always keep the original capture.
- **Light verify prompt (in scope):** `PromptTemplateRegistry` gains an `AnalyzeVisualLite` template returning only `{changed, page_identity, item_count}` with `MaxTokens` reduced to 1024; verify-only call sites use it.

## Risks / Trade-offs

- **Fingerprint equality may mask pixel-level change (animations, progress bars)** → Mitigation: cache read still precedes any action with the freshest `RefreshAsync` state; spec requires vision fallback when deterministic state is ambiguous; locate scenarios use static Settings pages where XML structure is the source of truth.
- **Fire-and-forget asset writes could lose evidence on hard crash** → Mitigation: bounded channel with backpressure, finalization drain before success/failure result, writer failure recorded in run diagnostics (same semantics harden P1 mandates).
- **UIAutomator-first completeness judgment may be wrong on complex pages** → Mitigation: conservative completeness rule (items non-empty + identity or scroll known); any parse failure falls back to vision; existing `UiAutomatorPageAnalysis` already proven in scenario observation.
- **Downscale may degrade model accuracy** → Mitigation: original resolution preserved for evidence; downscale gated behind a capability probe; light-prompt path is independently valuable.
- **Per-step `RefreshAsync` cost (1-2s) replaces AI cost (12s)** → Worth it at ~85% saving per cache hit; XML also feeds D1 evidence so it is not pure overhead.

## Migration Plan

Layers are independently shippable and verifiable: D1/D3 (hook I/O) → D4/D5/D6 (fingerprint AI elimination) → D7 (deterministic-first) → D2/D8 (sink + lite payload). Each layer runs the locate scenario and records provider-call count + total duration before moving on. No config/state migration required; cache and sinks are run-scoped.

## Open Questions

- Image downscale dependency: acceptable to add (e.g., SkiaSharp/ImageSharp) or keep payload full-size with light prompt only? Decided provisionally: light prompt is in scope; downscale ships only if an existing image dependency is available in `UniClaw.Device`.
- Whether `UiAutomatorPageAnalysis.Parse` completeness rule needs a Settings-page-specific adjustment beyond the generic rule.
