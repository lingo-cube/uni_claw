# Current Internal Gaps

> Date: 2026-07-29
> Scope: internal repository gaps only. This excludes external selection of the first target APK/package unless it is needed to explain an internal integration boundary.
> OpenSpec status: no active change exists under `openspec/changes/`; this document is a gap inventory, not an OpenSpec proposal.

## Summary

The project has no active OpenSpec change. The Core traversal loop, UniBrain vertical slices, trace pipeline, prompt registry, Claude vision transport, and Android Emulator readiness tooling are already present. The remaining internal gaps are mostly capability completion, real-device composition boundaries, Phase 3 behavior hardening, and documentation/spec synchronization.

Recommended next internal work:

1. Finish the remaining UniBrain capability slices.
2. Define the Mode A `PageAnalysis` to `IScreenStateProvider` scroll-state bridge.
3. Clean up doc/spec drift created by completed refactors.
4. Defer deeper Phase 3 behavior work until the above seams are stable.

## G1: UniBrain Capability Completion

Priority: P0 internal

Current state:

- `PageAnalyzer` implements `AnalyzeCurrentPageAsync`, but `FindAppEntryAsync` and `VerifyPageTypeAsync` still throw `NotImplementedException`.
- `TraversalAdvisor` implements `DecideNextActionAsync`, but `InferContainerTypeAsync`, `HandleExceptionAsync`, and `ScreenSafetyAsync` still throw `NotImplementedException`.
- `DeepSeekModelProvider` is text-only; `CompleteVisionAsync` and `CompleteMultimodalAsync` are intentionally not implemented.
- `MockModelProvider` is also text-only, which limits fixture-driven testing for future vision/multimodal slices.

Evidence:

- `src/UniClaw.Core/UniBrain/PageAnalyzer.cs`
- `src/UniClaw.Core/UniBrain/TraversalAdvisor.cs`
- `src/UniClaw.DeepSeekProvider/DeepSeekModelProvider.cs`
- `src/UniClaw.Core/Simulation/MockModelProvider.cs`
- `openspec/specs/page-analyzer/spec.md`
- `openspec/specs/traversal-advisor/spec.md`
- `openspec/specs/model-provider/spec.md`

Suggested OpenSpec changes:

- `unibrain-verify-page-type`
- `unibrain-find-app-entry`
- `unibrain-screen-safety`
- `unibrain-exception-advisor`
- `mockmodelprovider-vision-fixtures` if vision fixture replay is needed before real SDK calls

## G2: Mode A Scroll-State Bridge

Priority: P0 internal, blocks robust real-device traversal

Current state:

- `TraversalEngine` consumes both `IUniBrain` and `IScreenStateProvider`.
- In Mode A, `PageAnalyzer.AnalyzeCurrentPageAsync` produces `PageAnalysis.HasScroll` and `PageAnalysis.IsEndOfList`.
- `IScreenStateProvider` is a separate dependency. There is no internal bridge that lets real Mode A traversal reuse the latest `PageAnalysis` scroll fields.
- `AdbScreenStateProvider` exists, but UIAutomator scroll metadata may not be sufficient for all real apps. The PRD records this as an unresolved bridge choice.

Likely direction:

- Add a `PageAnalysisAwareScreenStateProvider` or equivalent cache bridge that is updated after each `AnalyzeCurrentPageAsync` call.
- Keep `IScreenStateProvider` out of `IUniBrain`; scrolling remains platform state, not AI service responsibility.

Evidence:

- `src/UniClaw.Core/Traversal/TraversalEngine.cs`
- `src/UniClaw.Device/AdbScreenStateProvider.cs`
- `docs/prd/2026-07-22-unibrain-prd.md` section 2.8
- `docs/system/decisions/log.md` D-126

Suggested OpenSpec change:

- `pageanalysis-screenstate-bridge`

## G3: First Device Composition Root

Priority: P1 internal, depends on target app choice for full end-to-end value

Current state:

- `scripts/android-emulator.sh` can validate Emulator, ADB, screenshot, and UIAutomator readiness.
- `UniClaw.Device` contains `AdbScreenCapture`, `AdbActionExecutor`, and `AdbScreenStateProvider`.
- There is no executable host or integration composition root that wires:
  - selected provider via `ModelRouter`
  - `PageAnalyzer`
  - `UniBrainService`
  - ADB device implementations
  - `TraversalEngine`
  - opt-in `DeviceIntegration` test category

Boundary:

- The repository intentionally has no hard-coded APK/package.
- The internal gap is the composition seam and test harness; the external input is the first selected target app.

Evidence:

- `docs/testing/android-emulator.md`
- `openspec/changes/archive/2026-07-28-add-android-emulator-integration/design.md`
- `src/UniClaw.Device/`
- `tests/UniClaw.Core.Tests/UniBrain/RealVisionIntegrationTests.cs`

Suggested OpenSpec change:

- `first-device-traversal-integration`

## G4: Traversal Phase 3 Behavior

Priority: P2 internal

Current state:

- `HandlePreconditionCheckAsync` still assumes pass and records `precondition_assume_pass`.
- Real precondition behavior requires an `ITraversalNode.Precondition` or equivalent extension.
- `restore_ops` verification is deferred because the engine does not yet have toggle-after-restore operation logic.
- `skip_dangerous` verification is deferred because dangerous button detection is not implemented.

Evidence:

- `src/UniClaw.Core/StateMachine/TraversalFSM.cs`
- `openspec/specs/traversal-fsm/spec.md`
- `docs/refactor/2026-07-13-execution-plan-digest-design.md`
- `docs/system/decisions/log.md` D-23 / D-68 references

Suggested OpenSpec changes:

- `traversal-node-preconditions`
- `toggle-restore-operations`
- `dangerous-action-screening`

## G5: Scroll Metrics And Advanced Baselines

Priority: P2 internal

Current state:

- `JumpDetected`, `JumpRecovered`, and `AdaptiveStepIncreases` are intentionally placeholders returning zero.
- Some archived advanced-baseline tasks remain unchecked because they were blocked by earlier scroll integration work.
- These are not current active tasks, but they represent still-deferred verification depth.

Evidence:

- `openspec/specs/baseline-scroll-metrics/spec.md`
- `openspec/specs/scroll-metrics-extraction/spec.md`
- `openspec/changes/archive/2026-07-13-advanced-simulation-baseline/tasks.md`
- `openspec/changes/archive/2026-07-13-baseline-scroll-metrics-fix/tasks.md`

Suggested OpenSpec change:

- `advanced-scroll-metrics`

## G6: Documentation And Spec Drift

Priority: P1 internal cleanup

Current state:

- Some docs still describe old states that appear to have been superseded by later code and archived changes.
- `docs/system/README.md` still says `TraversalFSM` has 6/8 stub handlers, but current `TraversalFSM` has real StepContext paths for Execute, ResultVerify, ErrorHandling, and PopupHandling, with no-StepContext fallback retained for compatibility.
- `openspec/specs/traversal-advisor/spec.md` says `TraversalAdvisor` consumes `IModelRouter`, while current code uses the newer `IModelProvider` injection pattern.
- `tests/UniClaw.Core.Tests/Baseline/MultiBranchNavigationTests.cs` comments still describe TDD failures and current BUG behavior, while the related navigation-subpage-frames OpenSpec change is archived as complete. This needs verification on a host with `dotnet` available before editing assertions/comments.

Evidence:

- `docs/system/README.md`
- `openspec/specs/traversal-advisor/spec.md`
- `tests/UniClaw.Core.Tests/Baseline/MultiBranchNavigationTests.cs`
- `openspec/changes/archive/2026-07-14-navigation-subpage-frames/tasks.md`

Suggested OpenSpec change:

- `docs-spec-sync-current-state`

## Not Counted As Active Gaps

These were found during search but should not be treated as current work without re-triage:

- Archived change task checkboxes marked deferred when later changes already covered the behavior.
- Old `docs/refactor/` design checklist items whose OpenSpec changes are archived and implemented.
- Test-only fake providers or spy recorders throwing `NotImplementedException` for methods outside the test scenario.

## Recommended Order

1. `docs-spec-sync-current-state`: remove misleading stale statements before new work depends on them.
2. `pageanalysis-screenstate-bridge`: unblock Mode A traversal behavior.
3. `first-device-traversal-integration`: wire the real device boundary once target app input is available.
4. Remaining UniBrain capability slices, ordered by traversal need: `screen_safety`, `verify_page_type`, `find_app_entry`, `exception_advisor`, `container_inference`.
5. Phase 3 behavior and metric depth: preconditions, dangerous action screening, toggle restore, advanced scroll metrics.

## Verification Note

An attempt to run:

```bash
dotnet test src/UniClaw.Core.sln --filter "FullyQualifiedName~MultiBranchNavigationTests"
```

failed on this host because `dotnet` is not available in `PATH`. Multi-branch test comments should be treated as suspicious stale documentation until verified in a .NET-enabled environment.
