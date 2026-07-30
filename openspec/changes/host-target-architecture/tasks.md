## 1. M1 — Core: `IObservableScreenStateProvider` + `ScreenStateResult` (resolves C1)

- [ ] 1.1 Add Core record `ScreenStateResult` to `src/UniClaw.Core/Traversal/` with fields `Succeeded`, `Status`, `HierarchyXml`, `HierarchyFingerprint`, `HasScroll`, `IsEndOfList`, `Failure` (sealed record, validated)
- [ ] 1.2 Add Core interface `IObservableScreenStateProvider` in `UniClaw.Core.Traversal` that inherits `IScreenStateProvider` and adds exactly `Task<ScreenStateResult> RefreshAsync(string? previousHierarchyXml, bool afterScroll, CancellationToken ct)`
- [ ] 1.3 Make `AdbScreenStateProvider` implement `IObservableScreenStateProvider`; implement `RefreshAsync` returning `ScreenStateResult`; leave the 4 locked methods (`HasScroll`/`GetScrollProgress`/`IsEndOfList`/`GetScrollSwipeConfig`) signatures and behavior unchanged
- [ ] 1.4 Add interface contract test: `IObservableScreenStateProvider` inherits the 4 locked methods and declares only `RefreshAsync`
- [ ] 1.5 Verify `ArchitectureGuardTests` 4-method lock on `IScreenStateProvider` stays green (run guard test)

## 2. M2 — Core/UniBrain: `MockModelProvider` vision replay + `UniBrainFactory` (resolves C2)

- [ ] 2.1 Extend `MockModelFixture` so its capability→`MockModelEntry` map satisfies `CompleteVisionAsync`/`CompleteMultimodalAsync` (mode-agnostic entries; consuming method sets `Mode`)
- [ ] 2.2 Change `MockModelProvider.CompleteVisionAsync`/`CompleteMultimodalAsync` from `NotImplementedException` to symmetric replay-or-fail-fast: look up `fixture.Resolve(request.Capability ?? "")`, return `ModelResponse` with `Mode="vision"`/`"multimodal"` on hit, throw `DomainValidationException` on miss
- [ ] 2.3 Add fixture-driven vision test (zero API cost): a vision capability preset is returned by `CompleteVisionAsync` with `Mode="vision"`; missing vision preset fails fast
- [ ] 2.4 Add Core `UniBrainFactory`/builder in `src/UniClaw.Core/UniBrain/` that turns `UniBrainConfig` + a separate credentials object into an assembled `UniBrainService` (resolves sub-interfaces per `DefaultProvider`/`CapabilityRouting`; selects real or `MockModelProvider`)
- [ ] 2.5 Add factory test: builds a `UniBrainService` from `UniBrainConfig` with default provider; honors `CapabilityRouting`; mock `DefaultProvider` produces a replay facade; credentials supplied via the separate channel, not `UniBrainConfig`
- [ ] 2.6 Verify `UniBrainConfig` remains credential-free (no credential/API-key fields added)

## 3. M3 — Host: delete `DeterministicSettingsModelProvider`, assemble via factory, drop the cast (resolves C1, C2)

- [ ] 3.1 Port the deterministic Settings analysis into a `MockModelFixture` preset (capability entries) consumed by `MockModelProvider` inside `UniBrainFactory`
- [ ] 3.2 Delete `DeterministicSettingsModelProvider` from `src/UniClaw.Host/` (`HostCommands.cs:668`)
- [ ] 3.3 Change `CreateProvider` mock branch to assemble `IUniBrain` via `UniBrainFactory` with `MockModelProvider` (config-driven, no Host-owned provider)
- [ ] 3.4 Delete the `(AdbScreenStateProvider)` cast in `HostCommands.cs:504`; program against `IObservableScreenStateProvider`
- [ ] 3.5 Type `HostRunServices.ScreenState` as `IObservableScreenStateProvider`; type `ScenarioObservation` constructor param to the interface
- [ ] 3.6 Add Host composition tests: no cast to concrete provider; no Host-owned `IModelProvider`; replay link shape produced from config; existing Host tests stay green

## 4. M4 — Core: `PageAnalysis` shape contract (resolves C4; supports D4)

- [ ] 4.1 Define the `PageAnalysis` shape contract over fields `Level1Menus`/`Level2Menus`/`Items`/`CurrentPath`/`HasScroll`/`IsEndOfList` (the fields the runner and safety gate consume)
- [ ] 4.2 Add a dedicated `PageAnalysisShapeContractTests` class running both AI and UIAutomator observation paths over the same fixture and asserting structural equivalence
- [ ] 4.3 Make the UIAutomator observation path fill `Level1Menus`/`Level2Menus` (no longer empty) — `ScenarioObservation.cs:181-186`
- [ ] 4.4 Make the UIAutomator observation path derive `Direction` from layout instead of hardcoding `Direction.Left`
- [ ] 4.5 Verify dual-path same-fixture equivalence test passes; "mock green" implies "real-path-shape green"; a path omitting a contracted field fails the contract test

## 5. M5 — Host: inject `IEntryPolicyExecutor`; record D6 (resolves C3, D6)

- [ ] 5.1 Replace `new EntryPolicyExecutor(...)` in `IncrementalScenarioRunner.cs:521` with constructor-injected `IEntryPolicyExecutor`; move construction into the composition factory
- [ ] 5.2 Add entry injection test: runner receives an injected `IEntryPolicyExecutor`, no `new EntryPolicyExecutor` in runner code
- [ ] 5.3 Add `// Does not use TraversalEngine; see host-target-architecture-design §3` note on `HostRunServices.CreateTraversalEngine`; mark it unused
- [ ] 5.4 Record the D6 decision in the spec (V1 runner self-contained, no `TraversalEngine`/`TraversalFSM` dependency) — confirm the `host-composition-root` spec requirement matches implementation

## 6. M6 — Host: probes on `ITraceRecorder`

- [ ] 6.1 Route `doctor` probe diagnostics through `ITraceRecorder` and the run-asset pipeline; remove any parallel diagnostic output format
- [ ] 6.2 Route `analyze` probe to record a single observation via `ITraceRecorder`
- [ ] 6.3 Add test: `doctor` output is trace-correlated (records via `ITraceRecorder`); `analyze` records one observation on trace
- [ ] 6.4 Ensure new probes can be added on the same trace path (no parallel diagnostic system)

## 7. M7 — Architecture guard (structural; depends on M2/M3)

- [ ] 7.1 Add guard test: Host must assemble `IUniBrain`, not directly `new` `IPageAnalyzer`/`IModelProvider` — test fails on regression to the bypass pattern
- [ ] 7.2 Add guard test: Host holds exactly one `IActionExecutor` (the `SafeActionExecutor`-decorated one); guard rejects a second un-decorated `IActionExecutor` so recovery/popup paths cannot bypass the safety gate
- [ ] 7.3 Verify guard passes for the config-driven assembly pattern and the single-decorated-executor pattern; verify it fails when a bypass is reintroduced

## 8. Acceptance criteria (explicit, code-anchored, per migration step)

> Each criterion states the **objective verdict** — a grep/test/compile check that passes or fails, not prose. Anchor lines are current as of branch `feature/refactor`; re-resolve by symbol (MCP first per `MCP-QUERY.md`) if lines shift during implementation.

### M1 — `IObservableScreenStateProvider` + `ScreenStateResult`

- [ ] 8.1 `IScreenStateProvider` still declares exactly 4 methods — `ArchitectureGuardTests.IScreenStateProvider_Has4Methods` (line 818) stays green; `typeof(IScreenStateProvider).GetMethods().Where(DeclaringType==IScreenStateProvider)` count == 4 and names == `{GetScrollProgress, GetScrollSwipeConfig, HasScroll, IsEndOfList}`
- [ ] 8.2 `IObservableScreenStateProvider` inherits `IScreenStateProvider` and declares exactly one **new** method `RefreshAsync` — reflection: `typeof(IObservableScreenStateProvider).GetMethods().Where(DeclaringType==IObservableScreenStateProvider).Count() == 1` and that method is `RefreshAsync`
- [ ] 8.3 `ScreenStateResult` is a `sealed record` in `UniClaw.Core.Traversal` with fields `Succeeded`/`Status`/`HierarchyXml`/`HierarchyFingerprint`/`HasScroll`/`IsEndOfList`/`Failure` — verified by a contract test asserting field set and record-ness
- [ ] 8.4 `AdbScreenStateProvider` implements `IObservableScreenStateProvider`; its 4 locked methods unchanged (signatures byte-identical to pre-M1)
- [ ] 8.5 No `grep -rn 'AdbScreenStateResult' src/UniClaw.Core/` hit — the result type is lifted to Core, not duplicated

### M2 — `MockModelProvider` vision replay + `UniBrainFactory`

- [ ] 8.6 `MockModelProvider.CompleteVisionAsync`/`CompleteMultimodalAsync` no longer throw `NotImplementedException` — `grep -n 'NotImplementedException'` in the mock provider file returns 0 hits in those methods
- [ ] 8.7 Fixture-driven vision test passes at zero API cost: a `MockModelFixture` with a vision-capability preset → `CompleteVisionAsync` returns `ModelResponse` with `Mode="vision"` and the preset content; missing preset → `DomainValidationException`
- [ ] 8.8 `UniBrainFactory` builds a `UniBrainService` from `UniBrainConfig` + a **separate** credentials object — factory test asserts the returned `IUniBrain` composes the config-selected providers
- [ ] 8.9 `UniBrainConfig` has no credential/API-key fields — `grep -iE 'ApiKey|Secret|Token|Password|Credential'` in `UniBrainConfig.cs` returns 0 hits (invariant preserved)

### M3 — Host: delete duplicate provider, assemble via factory, drop the cast

- [ ] 8.10 `grep -rn 'DeterministicSettingsModelProvider' src/UniClaw.Host/` returns 0 hits (the anti-pattern provider is deleted)
- [ ] 8.11 `grep -n '(AdbScreenStateProvider)' src/UniClaw.Host/Commands/HostCommands.cs` returns 0 hits (the C1 cast at line 504 is gone)
- [ ] 8.12 `HostRunServices.ScreenState` is typed `IObservableScreenStateProvider`, not `AdbScreenStateProvider` — verified by reading the property declaration
- [ ] 8.13 `ScenarioObservation` constructor screen-state param is typed `IObservableScreenStateProvider` — verified by reading the constructor signature
- [ ] 8.14 Host composition test: the mock/replay link shape is produced by `UniBrainFactory` with `DefaultProvider="mock"`; Host holds no `IModelProvider` field and constructs no `PageAnalyzer` directly

### M4 — `PageAnalysis` shape contract

- [ ] 8.15 `grep -n 'Direction.Left' src/UniClaw.Host/Runner/ScenarioObservation.cs` returns 0 hits (the hardcoded `Direction.Left` at lines 181-182 is removed)
- [ ] 8.16 `ScenarioObservation` UIAutomator path fills `Level1Menus` and `Level2Menus` (non-empty where the page has them) — verified by reading the `PageAnalysis` construction in that path
- [ ] 8.17 `PageAnalysisShapeContractTests` runs both AI and UIAutomator paths over the **same fixture** and asserts structural equivalence on `Level1Menus`/`Level2Menus`/`Items`/`CurrentPath`/`HasScroll`/`IsEndOfList` — test passes
- [ ] 8.18 "Mock green ⇒ real-path-shape green": the contract test passes for the UIAutomator path on every fixture where it passes for the AI/mock path
- [ ] 8.19 `LocateScenarioStepPlanner.Plan` (ScenarioPlanning.cs) consumes only contracted fields (`Items`, `HasScroll`, `IsEndOfList`, `CurrentPath`) — no reliance on path-specific extras; verified by reading the method

### M5 — Entry injection + D6

- [ ] 8.20 `grep -n 'new EntryPolicyExecutor' src/UniClaw.Host/Runner/IncrementalScenarioRunner.cs` returns 0 hits (the C3 `new` at line 521 is gone)
- [ ] 8.21 `IncrementalScenarioRunner` receives `IEntryPolicyExecutor` via constructor injection — verified by reading the constructor signature
- [ ] 8.22 `HostRunServices.CreateTraversalEngine` carries the note `// Does not use TraversalEngine; see host-target-architecture-design §3` and is marked unused — `grep -n` for the note succeeds
- [ ] 8.23 `IncrementalScenarioRunner` has no reference to `TraversalEngine`/`TraversalFSM` — `grep -nE 'TraversalEngine|TraversalFSM'` in the runner file returns 0 hits

### M6 — Probes on trace

- [ ] 8.24 `doctor` probe records diagnostics via `ITraceRecorder` — test asserts an `ITraceRecorder` call correlated with the probe; no separate diagnostic output file/format is written
- [ ] 8.25 `analyze` probe records a single observation via `ITraceRecorder` — test asserts one observation record on trace

### M7 — Architecture guard

- [ ] 8.26 Guard test asserts Host assembly contains no direct `new PageAnalyzer(` or `new ...ModelProvider(` (real-provider concrete construction) — test fails when such a `new` is reintroduced
- [ ] 8.27 Guard test asserts `HostRunServices` exposes exactly one `IActionExecutor`, and it is the `SafeActionExecutor`-decorated instance — test fails when a second un-decorated `IActionExecutor` is added

### Whole-change gates

- [ ] 8.28 Full test suite green after each of M1–M7 (existing 930+ tests preserved); locked 4-method guard green at every step
- [ ] 8.29 No locked enum values changed (`DecisionResult` still 3; `SafetyTag` still 4; `ElementTypeMapper` vocabulary unchanged) — `grep`/reflection guard tests green
- [ ] 8.30 No reverse-dependency violations introduced — `ArchitectureGuardTests` cross-layer refs test green; Core gains extension interfaces only, no new Core→Host/Device refs
- [ ] 8.31 The active `deliver-safe-android-settings-test-loop` change's 24 spec requirements are unchanged by this seams redesign — verified by diffing the active change's spec deltas (no edits to its `specs/`)