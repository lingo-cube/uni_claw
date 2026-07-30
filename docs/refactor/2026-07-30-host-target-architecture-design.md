# UniClaw.Host — Target Architecture and Migration Plan

> Date: 2026-07-30
> Scope: redesign of `src/UniClaw.Host/` as a composition root, plus the Core seams it must lift.
> Status: design draft (docs-first, per user). Not yet an OpenSpec change — to be promoted after review.
> Companion docs:
> - `2026-07-30-current-internal-gaps-calibrated.md` — calibrated gap inventory
> - `2026-07-30-host-implementation-map.md` — current Host control gaps (§6.1-6.6)
> - `2026-07-30-deliver-safe-settings-spec-defect-analysis.md` — spec defects D1-D6
> - Core conflicts C1-C4 identified in conversation (§1 below restates them)

## 1. Problem Statement

The current Host was built as a single `locate_one_item` vertical slice. To ship fast it bypassed Core's locked seams and re-implemented capabilities that belong in Core. Four concrete conflicts were found:

- **C1 — Host bypasses locked `IScreenStateProvider`**: `HostCommands.cs:504` casts `IScreenStateProvider` back to the concrete `AdbScreenStateProvider` to reach `RefreshAsync` (`AdbScreenStateProvider.cs:67`), which returns `AdbScreenStateResult` — a record defined only in Device, not in the locked interface. Host depends on concrete classes, not the seam.
- **C2 — Duplicate mock `IModelProvider`**: Host defines `DeterministicSettingsModelProvider` (`HostCommands.cs:668`) instead of using Core's `MockModelProvider` (`MockModelProvider.cs:11`). The Core one lacks vision replay; the Host one is Settings-specific and not reusable. Two mocks, neither right.
- **C3 — `EntryPolicyExecutor` is `new`-ed, not injected**: `IncrementalScenarioRunner.cs:521` constructs the Core class directly with a Host-wrapped driver instead of injecting `IEntryPolicyExecutor`.
- **C4 — Two observation paths produce structurally different `PageAnalysis`**: the AI path fills `Level1Menus`/`Level2Menus`/`Items` (`PageAnalyzer.cs:160-163`); the UIAutomator path fills only `Items`/`CurrentPath`/`HasScroll`/`IsEndOfList` and hardcodes `Direction.Left` (`ScenarioObservation.cs:181-186`). Same record, different shape.

Root cause: Host assembles AI capabilities by grabbing low-level components (`IModelProvider`, raw `IPageAnalyzer`) instead of the UniBrain facade, and re-implements missing capabilities locally rather than lifting them into Core. This is why the layer resists control.

## 2. Design Principles

1. **Host is a composition root, not an implementer.** Host configures + assembles Core components. It does not implement providers. `DeterministicSettingsModelProvider` is an anti-pattern to remove.
2. **AI capabilities aggregate into UniBrain.** Visual perception (`IPageAnalyzer`), traversal decision (`ITraversalAdvisor`), text understanding (`ITextUnderstanding`), and the transport layer that backs them (`IModelProvider`/`IPromptLibrary`) are accessed through the `IUniBrain` facade. Host assembles `IUniBrain`, not its sub-interfaces directly. `IUniBrain` already aggregates these three sub-interfaces (`IUniBrain.cs`); the design uses it as intended rather than bypassing it.
3. **Configuration-driven assembly.** Host supplies config; Core/UniBrain assembles the facade internally. `UniBrainConfig` (`UniBrainConfig.cs`, already present: `DefaultProvider` + `CapabilityRouting`) is the existing vehicle — extend it, don't reinvent it. Host hands config to a UniBrain factory/builder; it does not hand-assemble `new PageAnalyzer(...)` with raw providers.
4. **Missing capabilities are added in Core, not Host.** When assembly reveals a gap (e.g., `MockModelProvider` vision replay, the observable screen-state seam, `PageAnalysis` shape contract), the fix lands in Core. Host never hides a substitute.
5. **Non-AI capabilities stay in the Host assembly layer.** Screen state (`IObservableScreenStateProvider` — PRD: scroll is device state, not AI), action execution, the safety gate, entry policy, run assets, and trace are platform/observability concerns, not AI. They are assembled by Host; they do not enter UniBrain.
6. **Probes are Host conveniences built on existing trace.** `doctor`/`analyze` and future probes may be added by Host, but their diagnostics flow through `ITraceRecorder` and the existing asset pipeline, not a parallel diagnostic system.
7. **Same Host, different link shapes by config.** Mock/replay vs real vs real-device are not separate runners — they are the same composition root producing different link shapes from different config (provider selection, device serial, mode). One Host assembly capability, configured apart.
8. **Locks are extended, never broken.** `IScreenStateProvider`'s 4-method lock (guarded by `ArchitectureGuardTests.cs:818`) is untouched. New seams are added as extension interfaces, not by mutating locked ones.

## 3. Target Architecture

```
[Config] scenario JSON + UniBrainConfig (provider/model/mode/routing) + device serial
   │
   ├─[Host] load ScenarioCatalog → ScenarioSnapshot (hash + freeze)
   ├─[Host] load ScenarioPlanCompiler → TraversalPlan
   │
   ├─[Host] assemble IUniBrain  ← single AI injection point (config-driven)
   │      UniBrainService (built by Core from UniBrainConfig, NOT hand-new-ed by Host):
   │      ├─ IModelProvider    ← config selects: real (Anthropic/Sensenova) or MockModelProvider
   │      ├─ IPageAnalyzer     ← UniBrain sub-interface (visual perception)
   │      ├─ ITraversalAdvisor ← UniBrain sub-interface (decision)
   │      └─ ITextUnderstanding ← UniBrain sub-interface (text)
   │
   ├─[Host] assemble non-AI capabilities (NOT UniBrain)
   │      ├─ IObservableScreenStateProvider = AdbScreenStateProvider  [new Core seam]
   │      ├─ IActionExecutor = SafeActionExecutor(AdbActionExecutor, safety gate)
   │      ├─ IEntryPolicyExecutor = EntryPolicyExecutor(SafeEntryActionDriver)  [injected, not new]
   │      └─ RunAssets / ITraceRecorder
   │
   ├─[Host] assemble IScenarioObservationSource (unified PageAnalysis shape contract)
   │
   └─[Host] run IScenarioRunner (Locate/Enumerate strategies)
           observe → analyze → plan → gate → execute → re-observe → verify
           └─ RunResult + trace + issues.jsonl (unified observability assets)

[Probes — Host conveniences on ITraceRecorder]
   ├─ doctor  → records probe results via ITraceRecorder, no parallel diagnostic output
   └─ analyze → records single observation via ITraceRecorder
```

Decision recorded (resolves spec defect D6): in V1 the scenario runner owns the observe→plan→gate→execute→verify loop and does not depend on `TraversalEngine`/`TraversalFSM`. `HostRunServices.CreateTraversalEngine` is retained but marked unused; if a future change routes the runner through `TraversalEngine`, it updates both this spec and the `traversal-engine` canonical spec.

## 4. Component Design

### 4.1 `IObservableScreenStateProvider` (resolves C1)
New Core interface, inherits the locked `IScreenStateProvider` (4 methods untouched):
```csharp
// Core/Traversal/IObservableScreenStateProvider.cs
public interface IObservableScreenStateProvider : IScreenStateProvider
{
    Task<ScreenStateResult> RefreshAsync(string? previousHierarchyXml, bool afterScroll, CancellationToken ct);
}
```
- `ScreenStateResult` lifted into Core (replaces Device-only `AdbScreenStateResult`): `Succeeded` / `Status` / `HierarchyXml` / `HierarchyFingerprint` / `HasScroll` / `IsEndOfList` / `Failure`.
- `AdbScreenStateProvider` implements the new interface; its 4 locked methods unchanged.
- Host programs against `IObservableScreenStateProvider`; `HostCommands.cs:504` cast is deleted; `ScenarioObservation` constructor param typed to the interface.
- `HostRunServices.ScreenState` typed `IObservableScreenStateProvider`.

### 4.2 UniBrain config-driven assembly (resolves C2; uses existing `UniBrainConfig`)
- Extend `UniBrainConfig` so it is the single config the Host hands to Core. Core provides a `UniBrainFactory`/builder (to be added in Core) that turns config + credentials into a `UniBrainService` — Host no longer hand-`new`s `PageAnalyzer`/`IModelProvider`.
- `MockModelProvider` gains vision replay in Core (extend `MockModelFixture` to support `CompleteVisionAsync`/`CompleteMultimodalAsync` preset entries). This makes the replay link shape a config selection inside UniBrain, not a Host-owned provider.
- Delete `DeterministicSettingsModelProvider` from Host. Its job (a deterministic Settings analysis for replay) returns to Core as a `MockModelFixture` preset consumed by `MockModelProvider`.
- Real visual providers (`AnthropicModelProvider`, `OpenAiCompatibleVisionProvider`) are selected by `UniBrainConfig.DefaultProvider`/routing inside the factory; Host supplies credentials via config, never hardcodes a provider.

### 4.3 `PageAnalysis` shape contract (resolves C4; Core)
- A Core-defined contract (tests, not just prose) that both observation paths satisfy: `Level1Menus`/`Level2Menus`/`Items`/`CurrentPath`/`HasScroll`/`IsEndOfList` filled to a common rule.
- UIAutomator path fills `Level1Menus`/`Level2Menus` (no longer leaves them empty) and derives `Direction` from layout instead of hardcoding `Direction.Left`.
- A contract test runs both paths over the same fixture and asserts structural equivalence on the fields the runner and safety gate consume. "Mock green" then implies "real-path-shape green."

### 4.4 Entry injection + runner/engine relationship (resolves C3 + D6)
- Host injects `IEntryPolicyExecutor` (no `new EntryPolicyExecutor`); construction lives in the composition factory.
- D6 recorded: V1 runner is self-contained, does not use `TraversalEngine`. A `// Does not use TraversalEngine; see host-target-architecture-design §3` note marks `HostRunServices.CreateTraversalEngine`.

### 4.5 Probes on trace (Host)
- `doctor`/`analyze` route diagnostics through `ITraceRecorder` and run assets. New probes added the same way; no parallel diagnostic output format.

## 5. Migration Steps

Each step independently verifiable, preserves the existing 930+ tests, and keeps the locked 4-method guard green. Order respects dependencies (seam before use; Core gap before Host use of it).

| Step | What | Resolves | Verify |
|------|------|----------|--------|
| **M1** | Core: add `IObservableScreenStateProvider` + `ScreenStateResult`; `AdbScreenStateProvider` implements it; locked 4 methods untouched | C1 | guard test green (4 methods); new interface contract test |
| **M2** | Core/UniBrain: add `MockModelProvider` vision replay (extend `MockModelFixture`); add `UniBrainFactory`/builder consuming `UniBrainConfig` | C2, G1 | fixture-driven vision test (zero API cost); factory builds facade from config |
| **M3** | Host: delete `DeterministicSettingsModelProvider`; `CreateProvider` mock branch → assemble via `UniBrainFactory` with `MockModelProvider`; delete `(AdbScreenStateProvider)` cast → program `IObservableScreenStateProvider` | C1, C2 | Host composition tests green; no cast; no Host-owned provider |
| **M4** | Core: define + test `PageAnalysis` shape contract; UIAutomator path fills `Level1Menus`/`Level2Menus`, drops `Direction.Left` hardcode | C4 | dual-path same-fixture equivalence test |
| **M5** | Host: inject `IEntryPolicyExecutor` (no `new`); record D6 decision; mark `CreateTraversalEngine` unused | C3, D6 | entry injection test; doc note |
| **M6** | Host: route `doctor`/`analyze` diagnostics through `ITraceRecorder`; add missing probes on same path | probes | doctor output trace-correlated |
| **M7** | Add architecture guard: Host must assemble `IUniBrain`, not directly `new` `IPageAnalyzer`/`IModelProvider` | structural | guard test prevents regression to the bypass pattern |

Dependencies: M1→M3 (interface before use); M2→M3 (Core mock before Host drops its own). M4, M5 independent and parallelizable. M6 last (convenience layer). M7 after M2/M3 (guard the new pattern once it exists).

## 6. Out of Scope (by chosen boundary "seams + D6")

- `enumerate_first_level` runner (task 8) — addressed by a separate OpenSpec change; this design only fixes seams so it can plug in cleanly.
- Spec defects D1/D2/D3 — addressed by a spec amendment under the active change; this design's §4.3 (shape contract) supports D4 directly.
- Phase 3 behavior (G4), advanced scroll metrics (G5) — deferred per calibrated gaps.

## 7. Relationship to Active Change

This is a target architecture, not a task under `deliver-safe-android-settings-test-loop`. Its seams (M1-M5) are prerequisites for that change's tasks 8/9 being honest (C1/C2/C4 are what make task 8's E2E tests and task 9's stability gates trustworthy). Recommended sequencing: promote this design to a spec amendment / new OpenSpec change, land M1-M2 (Core) first, then resume tasks 8/9 against the fixed seams. The spec defects D1/D2/D3 should be amended in parallel so tasks 8/9 have a closed definition of "done."

## Verification Note

Static design only; no `dotnet` execution. Interface/guard claims verified by reading `ArchitectureGuardTests.cs:818` (4-method lock) and `IScreenStateProvider.cs` / `IUniBrain.cs` / `UniBrainConfig.cs` source. Migration steps are designed to keep that guard green by adding extension interfaces and Core capabilities rather than mutating locked contracts.