## Why

**Priority: P0** — the visible locate run passes but takes **286s** (4:46), against a 120s scenario budget. Latency decomposition shows the "inter-step waiting" is not idle time but two redundant-work stacks on the traversal hot path: (1) **hook duplication** — `RunAssetHook` independently re-runs ADB screencap + uiautomator dump per step even though `PageAnalyzer.AnalyzeCurrentPageAsync()` captured the identical screen moments before, and `BoundaryHook` re-runs `dumpsys` every step; (2) **redundant AI vision calls** — a scroll step fires up to 6 `AnalyzeCurrentPageAsync` calls (~12s each) even when the UIAutomator hierarchy fingerprint proves the page structure did not change. All deterministic alternatives (`UiAutomatorPageAnalysis`, `HierarchyFingerprint`, `PageSnapshotManager.Fingerprint()`) already exist in the codebase but are wired with the wrong ordering or invalidation conditions.

This change is the **high-priority performance counterpart** to `harden-deterministic-verification-async-evidence` (P0 deterministic post-action verification / P1 async evidence pipeline). It targets the per-step hot path; harden targets the completion and persistence paths. Together they bring locate under the 120s budget.

## What Changes

- **P0a — reuse step captures:** `RunAssetHook` stops issuing its own ADB screencap/uiautomator-dump per step; it consumes the capture + hierarchy XML already produced by the step's page analysis, persisted on the step context. Step asset writes become non-blocking.
- **P0b — downsample boundary checks:** `BoundaryHook` checks the foreground package only when the page hierarchy fingerprint changes (or every N steps), not on every step.
- **P0c — fingerprint-driven AI elimination:** page-analysis cache invalidation moves from "every device action" to "hierarchy fingerprint changed"; post-scroll AI analysis is skipped when the pre/post swipe fingerprint is unchanged; ResultVerify retry loop exits early when the fingerprint shows no page change.
- **P0d — deterministic-first analysis:** the Host page analyzer runs UIAutomator analysis first and calls the remote vision model only as a fallback when the deterministic result is incomplete or unreliable.
- **P1 — lighter vision payloads:** screenshots are downscaled before encoding; a lightweight "change check" prompt variant is added for verify-only calls.
- No change to safety-before-action semantics, trace/storage contracts, or locked enum/interface counts.

## Capabilities

### New Capabilities

- `traversal-hot-path-io-efficiency`: Defines reuse of per-step captures between page analysis and evidence hooks, non-blocking step-asset writes, and fingerprint-gated boundary checking.
- `deterministic-first-page-analysis`: Defines hierarchy-fingerprint-driven cache retention, post-scroll analysis skipping, ResultVerify early exit, and UIAutomator-first with vision fallback.
- `vision-request-lite`: Defines downscaled screenshot encoding and a lightweight verify-only prompt variant.

### Modified Capabilities

<!-- No canonical Core requirement changes: Host composes existing page-analysis,
     ADB, screen-state, and hook seams; Core retry-loop changes are implementation
     refinements that keep the handler-result-verify contract (page-change verified
     before proceeding) intact. -->

## Impact

- `src/UniClaw.Host/Hooks/RunAssetHook.cs` — capture reuse, non-blocking writes
- `src/UniClaw.Host/Hooks/BoundaryHook.cs` — fingerprint-gated package checks
- `src/UniClaw.Host/Runner/InvalidatingPageAnalysisCache.cs` — fingerprint-based invalidation, UIAutomator-first ordering
- `src/UniClaw.Core/Traversal/InterceptionHandler.cs` — post-scroll fingerprint skip
- `src/UniClaw.Core/StateMachine/TraversalFSM.cs` — ResultVerify early exit
- `src/UniClaw.Core/Traversal/TraversalEngine.cs` — step-context capture stash, cache-valid analysis
- `src/UniClaw.Core/UniBrain/PageAnalyzer.cs` / `PromptTemplateRegistry.cs` — light verify prompt
- Vision provider encoding path (`OpenAiCompatibleVisionProvider` / `AnthropicModelProvider`) — downscale
- Tests: `InvalidatingPageAnalysisCacheTests`, `HooksTests`, `VerifyHookTests`, `TraversalHookTests`, `PageAnalyzerTests`, new fingerprint-skip tests
- Related: `harden-deterministic-verification-async-evidence` (post-action verification + async evidence pipeline) — this change does not overlap its tasks
