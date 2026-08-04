# Test tiers: Legacy Harness, Simulation, and Explicit Integration

UniClaw has three testing roles. Core Simulation is the normal deterministic
algorithm baseline. External provider/ADB/emulator integration is explicit and
scope-gated. The queued Host runner Harness is a migration aid while
`runner-through-engine` deletes the old parallel runner loop; new product-path
coverage must target the Host-assembled `TraversalEngine` path.

## At a glance

| | Legacy Harness | Simulation / engine path | Explicit Integration |
|---|---|---|---|
| **Role** | Preserve fault cases until old runner tests are migrated/deleted | Daily Core/FSM correctness baseline | Real provider, ADB and emulator evidence |
| **Driver** | old Host runners | real `TraversalEngine`/`TraversalFSM` | production Host composition → Core engine/FSM |
| **Fakes** | stateless queued observations | stateful fixture or focused device fakes | none at selected external boundary |
| **Default** | temporary unit coverage | runs normally | skipped unless scope enabled |
| **Location** | `tests/UniClaw.Host.Tests/Runner/` | Core `Simulation/` and Host `EnginePathTests` | `*IntegrationTests` + `docs/testing/integration-tests.md` |

## Legacy Harness — stateless queued script (migration-only)

`tests/UniClaw.Host.Tests/Runner/RunnerTestHarness.cs` is retained only while
OpenSpec `runner-through-engine` migrates/deletes `ScenarioRunnerBase`,
`IncrementalScenarioRunner`, and `EnumerateScenarioRunner`. Do not add new
product behavior to this loop. New orchestration coverage belongs in
`EnginePathTests` or the explicit emulator scopes.

The runner loop is observe → analyze → plan → gate → execute → re-observe →
verify. The Harness replaces the external dependencies (device + model) with
**stateless fakes** so the test controls exactly what the runner sees at each
step:

- **`FakeObservationSource`** — holds a `Queue<ScenarioObservation>` the test
  author pre-populates. The runner dequeues one per `ObserveAsync` call. The
  fake does NOT track device state — what the runner "did" has no effect on the
  next observation. Also holds a `Queue<string>` for `GetCurrentFingerprintAsync`
  (drives the stale-plan check). This is the primary fake: the runner's behavior
  is driven entirely by the queued sequence.
- **`FakeActionExecutor`** — records `TapAsync` / `SwipeAsync` /
  `PressBackAsync` calls to a `Calls` list and returns `true`. Does NOT touch a
  device and does NOT change subsequent observations. Tests assert "the
  dangerous entry was never clicked" by checking `harness.Actions.Calls`.
- **`FakeEntryDriver`** — launch/wait return `true` for reset. Does not start
  an app.
- **`FakeAdbRunner`** — implements `IAdbSession` but **throws if used**.
  Guarantees the test never reaches ADB (observations come from the queue, not
  from `AdbScenarioObservationSource`).
- **`FakeScreenState`** — implements `IObservableScreenStateProvider`;
  `HasScroll` / `IsEndOfList` return fixed values; `RefreshAsync` throws
  (observations come from the queue).
- **`UnusedPageAnalyzer`** / **`UnusedBrain`** — throw if used. `PageAnalysis`
  is placed directly into `ScenarioObservation.Analysis` by the test builder,
  so the real model-analysis path must never fire. `UnusedBrain` exists
  because M3 added an `IUniBrain Brain` field to `HostRunServices`; the harness
  fills it with this placeholder.

Builder helpers:

- `Observation(fingerprint, page, item?, hasScroll, isEnd, level1Menus?)` —
  builds a `ScenarioObservation` with synthetic screenshot bytes, synthetic UI
  XML (`<hierarchy fingerprint="..."/>`), and a `PageAnalysis` populated with
  `CurrentPath` / `Items` / `Level1Menus` / `HasScroll` / `IsEndOfList`. The
  `level1Menus` param was added for the enumerate planner (which consumes
  `Level1Menus`); the locate planner consumes `Items`.
- `Item(name)` — builds a `MenuItem`.
- `Menu(name, y)` — builds a `MenuInfo`.
- `Manifest(runId)` — builds a `RunManifestInput`.

### Why stateless

Host tests verify **policy and orchestration over arbitrary observation
sequences**, including fault sequences: stale plan, return-verification failure
(back lands on the wrong page), dangerous-skip, scroll-stuck-without-end.
A stateful virtual device cannot easily produce "back landed on the wrong
page"; a hand-queued sequence can. The runner's reaction to a scripted
sequence is exactly what the assertions check.

### Example (dangerous skip)

1. Queue: `[Settings home with A + "Reset options", click A → About page, back
   → Settings home, Settings home with A sampled + "Reset options" unprocessed
   + isEnd]`.
2. Build the **real** safety gate (`SettingsSafetyEvaluator` +
   `SafeActionExecutor` — NOT faked; the gate must really run to deny).
3. Runner reaches click "Reset options" → real gate `deny.dangerous.text`
   denies → `FakeActionExecutor.Calls` has no click for it → assert
   `SafetyDenied == 1`, `Calls` excludes that click, `outcome.Status == success`
   (skipped and continued).

No device, no model call — verifies the dangerous-skip policy end-to-end.

## Simulation — stateful virtual device (Core tier)

`tests/UniClaw.Core.Tests/Simulation/` tests Core-layer engine/algorithm
correctness using a **stateful** virtual device built from `StateFixture`.

The world is declared with `StateFixtureBuilder`:

```csharp
new StateFixtureBuilder()
    .Page("home", p => p
        .Name("HomeScreen")
        .Button("btn_settings", "Settings", 0.5, 0.9))
    .Page("settings", p => p
        .Name("SettingsScreen")
        .BackButton("btn_back", 0.05, 0.05))
    .Transition(t => t.Id("go").Click("btn_settings").From("home").To("settings"))
    .Transition(t => t.Id("back").Click("btn_back").From("settings").To("home"))
    .Build();
```

Stateful fakes implement the Core seams but compute from the fixture's
**current** page:

- **`StatefulMockVisionService(fixture)`** — implements `IPageAnalyzer`.
  `AnalyzeCurrentPageAsync` returns the `PageAnalysis` for the fixture's
  current page. A `TapAsync` hitting `btn_settings` flips the fixture to the
  "settings" page, so the next `AnalyzeCurrentPageAsync` returns settings-page
  content. State is self-consistent: correct action → page changes.
- **`StatefulMockActionExecutor(vision)`** — implements `IActionExecutor`;
  `TapAsync(x, y)` finds the hit button and triggers the fixture's state
  transition (it does not just return `true`).

`SimulationE2ETests` assembles a real `TraversalEngine` with
`UniBrainService(vision, mockAdvisor, mockText)` + `StatefulMockActionExecutor`
and asserts the engine navigates the fixture's state graph correctly.

### Why stateful

Core's engine assumes a coherent device: a correct action changes the page.
A stateful virtual device exercises the traversal algorithm (state advance,
sub-graph traversal, backtrack) naturally — the test does not have to
hand-script every observation, the fixture advances itself.

Files: `SimulationE2ETests.cs`, `StatefulMockVisionTests.cs`,
`StatefulMockActionTests.cs`, `StateFixtureTests.cs`,
`Scroll/PagedContentAndScreenTests.cs`, `ExpectedBehavior/...`.

## When to use which

- Preserving a legacy runner fault case until its engine-path replacement lands
  → **Harness**. Migrate the assertion; do not extend the old loop.
- Verifying **Core engine/algorithm correctness** (traversal over a coherent
  device graph, state advancement, backtracking) → **Simulation**, with a
  `StateFixture` state graph and the stateful mock vision/action pair.
- Verifying provider, ADB, production Host composition, or either agreed Android
  Settings scenario → the explicit scope ladder in
  `docs/testing/integration-tests.md`.

Do not try to inject "back landed on the wrong page" into the stateful
Simulation (it cannot produce an incoherent state), and do not try to verify
engine traversal via the stateless Harness (it has no state graph to
traverse).
