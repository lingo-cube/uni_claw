## Why

The current `src/UniClaw.Host/` was built as a single `locate_one_item` vertical slice that bypassed Core's locked seams and re-implemented capabilities that belong in Core (casting `IScreenStateProvider` back to a concrete type, defining a duplicate `DeterministicSettingsModelProvider`, `new`-ing `EntryPolicyExecutor`, and producing structurally different `PageAnalysis` from two observation paths). This makes Host an implementer rather than a composition root, which is why the layer resists control and why the active `deliver-safe-android-settings-test-loop` change's tasks 8/9 cannot be honest until the seams are fixed. The redesign lifts the missing capabilities into Core, assembles AI capabilities through the `IUniBrain` facade config-driven, and restores layer boundaries so the active change can resume against fixed seams.

## What Changes

- **New Core seam `IObservableScreenStateProvider`** that inherits the locked `IScreenStateProvider` (4 methods untouched) and adds `RefreshAsync` returning a Core-lifted `ScreenStateResult`. Host programs against this interface instead of casting to the concrete `AdbScreenStateProvider`. Resolves C1.
- **Core gains `UniBrainFactory`/builder** consuming the existing `UniBrainConfig`, so Host hands config + credentials to Core and receives an assembled `UniBrainService` rather than hand-`new`-ing `PageAnalyzer`/`IModelProvider`. Resolves C2 (assembly seam) and the structural bypass.
- **`MockModelProvider` gains vision replay in Core** (extend `MockModelFixture` to support `CompleteVisionAsync`/`CompleteMultimodalAsync` presets). The replay link shape becomes a config selection inside UniBrain, not a Host-owned provider. Resolves C2 (capability gap).
- **BREAKING (Host-only): Delete `DeterministicSettingsModelProvider`** from Host. Its deterministic Settings analysis job returns to Core as a `MockModelFixture` preset consumed by `MockModelProvider`. Resolves C2 (duplicate mock).
- **Core defines a `PageAnalysis` shape contract** (tests, not prose) that both the AI and UIAutomator observation paths satisfy: `Level1Menus`/`Level2Menus`/`Items`/`CurrentPath`/`HasScroll`/`IsEndOfList` filled to a common rule; UIAutomator path fills `Level1Menus`/`Level2Menus` and derives `Direction` from layout instead of hardcoding `Direction.Left`. Resolves C4.
- **Host injects `IEntryPolicyExecutor`** (no `new EntryPolicyExecutor`); construction lives in the composition factory. Records the D6 decision that the V1 scenario runner is self-contained and does not use `TraversalEngine`/`TraversalFSM`. Resolves C3 + D6.
- **Host probes (`doctor`/`analyze`) route diagnostics through `ITraceRecorder`** and run assets; no parallel diagnostic output format.
- **New architecture guard**: Host must assemble `IUniBrain`, not directly `new` `IPageAnalyzer`/`IModelProvider`; Host holds exactly one (decorated) `IActionExecutor`. Prevents regression to the bypass pattern.
- The 24 spec requirements of `deliver-safe-android-settings-test-loop` are not changed; this is a structural redesign of assembly seams that the active change's requirements depend on. Spec defects D1/D2/D3 are out of scope (parallel amendment); D4 is supported by the shape contract; D6 is recorded here.

## Capabilities

### New Capabilities

- `host-composition-root`: Defines Host as a composition root that configures + assembles Core components (config-driven `IUniBrain` assembly, non-AI capability assembly, `--repeat` aggregation, probes on trace) and the structural guard preventing the bypass pattern. Host does not implement providers.

### Modified Capabilities

- `screen-state-provider`: Adds the `IObservableScreenStateProvider` extension interface (inherits the locked 4-method `IScreenStateProvider`, adds `RefreshAsync` + Core-lifted `ScreenStateResult`); the 4-method lock is untouched and extended, not broken.
- `model-provider`: Extends `MockModelProvider`/`MockModelFixture` with vision replay (`CompleteVisionAsync`/`CompleteMultimodalAsync` preset support) so the replay link shape is a Core config selection, not a Host-owned provider.
- `unibrain-facade`: Adds `UniBrainFactory`/builder that turns `UniBrainConfig` + credentials into an assembled `UniBrainService`, making config-driven assembly the single AI injection point Host uses.
- `page-analyzer`: Adds a `PageAnalysis` shape contract (test-enforced) that both AI and UIAutomator observation paths satisfy, and requires the UIAutomator path to fill `Level1Menus`/`Level2Menus` and derive `Direction` from layout instead of hardcoding `Direction.Left`.

## Impact

- **Core** (`src/UniClaw.Core/`): new `IObservableScreenStateProvider` + `ScreenStateResult` in `Traversal/`; new `UniBrainFactory`/builder in `UniBrain/`; `MockModelProvider`/`MockModelFixture` vision replay extension; new `PageAnalysis` shape contract tests. The locked `IScreenStateProvider` 4-method guard (`ArchitectureGuardTests.cs:818`) stays green — extension interfaces only.
- **Host** (`src/UniClaw.Host/`): `HostCommands.cs` loses the `DeterministicSettingsModelProvider` and the `(AdbScreenStateProvider)` cast; `CreateProvider` mock branch assembles via `UniBrainFactory`; `IncrementalScenarioRunner.cs` injects `IEntryPolicyExecutor` instead of `new`; `HostRunServices` typed against the new seams; `doctor`/`analyze` routed through `ITraceRecorder`; new architecture guard test.
- **Device** (`src/UniClaw.Device/`): `AdbScreenStateProvider` implements `IObservableScreenStateProvider`; its 4 locked methods unchanged; `AdbScreenStateResult` superseded by the Core-lifted `ScreenStateResult`.
- **No locked enum changes, no reverse-dependency violations** (Core gains extension interfaces; spec forbids only reverse refs, Core additions are allowed).
- Existing 930+ tests preserved; migration steps M1–M7 each independently verifiable.
- Relationship to active change: this is a prerequisite, not a task under `deliver-safe-android-settings-test-loop`. Recommended sequencing — land M1–M2 (Core) first, then resume the active change's tasks 8/9 against the fixed seams.