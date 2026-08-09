# LEGACY_NORMALIZED_EVIDENCE_RESULT — Step 3

> Generated: 2026-08-09
> Primary input: `docs/decisions/legacy-high-value-evidence-set-step2.md`
> Legacy truth source: `feature/refactor` (read-only Git objects)
> Behavioral facts verified via `git show feature/refactor:<path>`

---

## Normalization Summary

**Primary Evidence Reviewed:** 18
**Successfully Normalized:** 18
**Insufficient Evidence:** 0
**Contradictory Evidence:** 2 (E-07, E-13)

---

## NORMALIZED EVIDENCE RECORDS

---

### E-01 — EmulatorScenarioIntegrationTests

**Title:** Full-stack Settings scenario execution on Android emulator with real AI vision and ADB actions

**Source Evidence:** `tests/UniClaw.Host.Tests/Integration/EmulatorScenarioIntegrationTests.cs`, `scenarios/android-settings/locate-one-item.v1.json`, `scenarios/android-settings/enumerate-settings-safely.v1.json`

---

**1. USER / TASK INTENT**

Scenario A (locate): Find "About phone" (or alias: "About device", "About emulated device", "Device information", "Phone information") from the Android Settings home screen. Verify the destination page identity matches one of the expected identities. Entry strategy: cold launch the Settings app (`com.android.settings`). Boundaries: maxDepth=2, maxSteps=12, maxScrolls=6, maxDuration=120s. Allowed actions: back, click, launch, scroll, wait.

Scenario B (enumerate): Enumerate all unique first-level entries on the Android Settings home screen. Sample safe read-only sub-pages. Skip dangerous entries (exclude anything matching pattern "search"). Prove end-of-list was reached and the app returned to the Settings home page. Boundaries: maxDepth=2, maxSteps=120, maxScrolls=12, maxDuration=3600s. Entry strategy: cold launch.

---

**2. INITIAL EXTERNAL WORLD**

Android emulator (AVD `uniclaw-lite-api35`) running. ADB server available. AI provider (sensenova or local vision server) accessible with valid credentials. `com.android.settings` package installed but not necessarily in foreground. Device serial resolved from `UNICLAW_ADB_SERIAL` environment variable or auto-detected via single online device.

---

**3. AVAILABLE OBSERVATION**

- Real AI vision analysis of emulator screenshots (provider: sensenova DeepSeek flash, or local vision server with YOLO+OCR)
- ADB screencap → PNG bytes → vision provider → structured element list with types, coordinates, text
- Action execution results (success/failure) from ADB
- FSM step trace records (step number, from state, to state, action dispatched, action success)
- Output: `result.json` with fields: `status`, `stepsConsumed`, `actionsAttempted`, `successCriteriaSatisfied`, `successEvidence`, `completionReason`, `discoveredEntries`, `visitedEntries`, `skippedEntries`

---

**4. ACTION / DECISION TAKEN**

For locate scenario: tap action on the identified target row (matching "About phone" or alias), followed by back/launch/wait for reset.
For enumerate scenario: iteratively tap each first-level Settings entry, observe sub-page, press back, repeat until all entries handled or boundaries exhausted.

Actions are gated through a safety policy (`settings-read-only-v1`) — deny-by-default for dangerous operations. Only allowed action types: back, click, launch, scroll, wait.

---

**5. WORLD TRANSITION**

Locate scenario: emulator navigates from Settings home to a sub-page. After verification, reset procedure (back → launch → wait) attempts to return to Settings home.

Enumerate scenario: emulator cycles through first-level entries: Settings home → tap entry → observe sub-page → press back → Settings home → tap next entry → ... until all first-level entries visited or boundaries exhausted.

---

**6. EVENT / DISTURBANCE**

Provider preflight may fail (missing credentials / model files) — caught before execution starts. No mid-run disturbance evidence from the test itself.

---

**7. OBSERVED OUTCOME**

Locate scenario: Run status = `"pending_verification"`. Steps consumed > 0. Actions attempted > 0. `result.json` contains exactly one file. `successCriteriaSatisfied = true`. Success evidence contains entries starting with `"target_action_executed:"` and `"target_page_identity:"`. Post-hoc TraceTool VerifyEngine returns status `"success"`.

Enumerate scenario: Run status = `"success"`. Steps consumed > 0. Actions attempted > 0. `completionReason = "enumerated_all_first_level"`. `successCriteriaSatisfied = true`. Sum of discoveredEntries + visitedEntries + skippedEntries > 0. Success evidence contains entries for: `"first_level_discovered:"`, `"first_level_visited:"`, `"first_level_skipped:"`, `"end_of_list:"`, `"return_page_identity:"`.

---

**8. FAILURE OR SUCCESS CLAIM**

Legacy system classifies both scenarios as SUCCESS (locate: `"pending_verification"` with TraceTool verify confirming `"success"`; enumerate: `"success"`).

---

**9. CONTRADICTORY EVIDENCE**

UNKNOWN — the test only exercises the success path. The `"pending_verification"` status for locate mode means the Host defers final verdict to an offline TraceTool verify step, implying the Host alone cannot confirm target-page identity.

---

**10. EXPECTED EXTERNAL BEHAVIOR**

Locate scenario: target page must match one of the expected page identities (aliases list). Target action must have been executed.

Enumerate scenario: all first-level entries must be discovered, visited, or explicitly skipped. End-of-list must be proven (no more entries remain). App must return to Settings home page after enumeration.

---

**11. EVIDENCE PROVENANCE**

REAL_DEVICE (emulator AVD) + INTEGRATION (real AI provider + real ADB)

- Executable: YES (scope-gated via `UNICLAW_INTEGRATION_SCOPES`)
- Historical Failure: NO (no specific failure recorded; verify step implies prior false-success risk)
- Reproducible: YES (requires emulator + provider credentials)

---

**12. LEGACY MECHANISM CONTEXT**

Non-normative: `HostCompositionFactory.RunScenarioAsync`, `TraversalEngine`, `TraversalFSM`, `IntegrationConfigLoader`, `ProviderPreflight`, `AdbTestContext`, `TraceTool VerifyEngine`, `result.json`, `successCriteria`, `CompletionReason`, `ScenarioLocate`, `ScenarioEnumerate`.

---

**13. NORMALIZED BEHAVIORAL STATEMENT**

Given an Android emulator running the Settings app and an AI vision provider, when asked to locate "About phone" from the Settings home list within 12 steps and 2 levels of depth, the system navigated to a sub-page matching the expected identity and reported success after post-hoc verification. When asked to enumerate all first-level Settings entries within 120 steps and 2 levels of depth, the system visited each entry's sub-page, returned to Settings home between entries, reported `enumerated_all_first_level`, and asserted end-of-list was reached with the app back on the Settings home page.

---

### E-02 — AdbSessionIntegrationTests

**Title:** ADB session self-healing after external server kill

**Source Evidence:** `tests/UniClaw.Host.Tests/Device/AdbSessionIntegrationTests.cs`

---

**1. USER / TASK INTENT**

Verify that the ADB connection to the Android device/emulator can recover after the ADB server process is killed externally. Also verify that two different ADB session implementations produce identical shell command output.

---

**2. INITIAL EXTERNAL WORLD**

Android emulator running with ADB server active. Device serial resolved. ADB shell commands functional (pre-check: `echo hello` succeeds).

---

**3. AVAILABLE OBSERVATION**

- Shell command stdout (`echo hello`, `echo pre-check`, `echo post-recovery`, `echo compare-test`)
- Shell command success/failure boolean
- PNG screenshot bytes (magic bytes 0x89,'P','N','G' verified)
- External process: `adb kill-server` subprocess exit

---

**4. ACTION / DECISION TAKEN**

Test sequence:
1. Execute shell command (`echo pre-check`) — verify server functional
2. Kill ADB server externally: `adb kill-server`
3. Wait 500ms
4. Execute shell command (`echo post-recovery`) through the same session object

Also: screenshot capture, and comparison of `ProcessAdbSession` vs `AdvancedSharpAdbSession` stdout for identical shell command.

---

**5. WORLD TRANSITION**

ADB server process is terminated by external kill command. After 500ms wait, the session object internally reconnects (self-healing). No explicit reconnect call in the test — recovery is transparent to the caller.

---

**6. EVENT / DISTURBANCE**

External ADB server kill (`adb kill-server`). This is a deliberate infrastructure-level disturbance.

---

**7. OBSERVED OUTCOME**

After server kill + 500ms wait: `ExecuteShellAsync("echo post-recovery")` returns `result.Success = true`. PNG screenshot captured successfully (non-empty bytes, valid PNG header). Both session implementations produce identical trimmed stdout for the same command.

---

**8. FAILURE OR SUCCESS CLAIM**

SUCCESS — self-healing worked; behavioral equivalence confirmed.

---

**9. CONTRADICTORY EVIDENCE**

NO

---

**10. EXPECTED EXTERNAL BEHAVIOR**

External infrastructure failure (ADB server crash/kill) must be transparently recovered without caller intervention. Different session implementations must produce identical results for the same device operations.

---

**11. EVIDENCE PROVENANCE**

EMULATOR + INTEGRATION

- Executable: YES (scope-gated: `adb-session`)
- Historical Failure: NO
- Reproducible: YES (requires emulator)

---

**12. LEGACY MECHANISM CONTEXT**

Non-normative: `AdvancedSharpAdbSession`, `ProcessAdbSession`, `AdbTestContext.ResolveSerialAsync`, `adb kill-server`, `ExecuteShellAsync`, `CaptureScreenshotAsync`.

---

**13. NORMALIZED BEHAVIORAL STATEMENT**

Given an Android emulator with an active ADB connection, when the ADB server process is killed externally, the session transparently recovers and the next shell command succeeds without caller intervention. Two different transport implementations produce identical output for the same device operation.

---

### E-03 — SimulationBaselineTests

**Title:** Deterministic 7-page Settings app traversal and target-search stop (synthetic fixture)

**Source Evidence:** `tests/UniClaw.Core.Tests/Baseline/SimulationBaselineTests.cs`, contract JSONs `settings-full-traversal.json`, `settings-target-search.json`

---

**1. USER / TASK INTENT**

Scenario A: Exhaustively traverse the Android Settings app — visit every reachable page, interact with every visible element (buttons, switches), and prove nothing was missed.

Scenario B: Find "Dark mode" in the Settings app and stop as soon as it is found. Do not visit pages beyond the target (specifically: must NOT visit Storage, Internal Storage, or SD Card).

---

**2. INITIAL EXTERNAL WORLD**

Simulated Android Settings app with 7 pages connected by 12 transitions:
- **Settings home** — 6 button-type elements: Wi‑Fi, Bluetooth, Display, Storage, Battery, Apps
- **Wi‑Fi page** — 1 switch + 3 network buttons + back button
- **Bluetooth page** — 1 switch + 2 device buttons + back button
- **Display page** — 2 switches (brightness, dark_mode) + 1 wallpaper button + back button
- **Storage page** — 2 buttons (Internal Storage, External Storage) + back button
- **Internal Storage page** — 3 read-only info items (Apps: 25GB, Media: 15GB, System: 5GB) + back button
- **External Storage page** — 2 read-only info items (Photos: 1.5GB, Videos: 500MB) + back button

Elements have types: `button`, `switch`, `readonly`. Transitions are click-triggered: clicking a button navigates to its target page; pressing back returns.

SIMULATION_ASSUMPTION: The simulation directly supplies element type classification (`button`, `switch`, `readonly`), element identity (name, coordinates), page identity, and the transition graph. These are not derived from observation.

---

**3. AVAILABLE OBSERVATION**

Synthetic vision produces a structured element list from the fixture: for each page, returns all elements with their declared types, names, and coordinates. No real screenshot or AI inference. Page identity is known from the fixture.

SIMULATION_ASSUMPTION: Observation perfectly matches fixture — no OCR errors, no misclassification, no missing elements, no coordinate drift.

---

**4. ACTION / DECISION TAKEN**

Scenario A (full traversal): The system generates child tasks for each matched element (button → navigable child; switch → toggle leaf). It visits each page, interacts with its elements, uses back to return. Claimed strategy: depth-first visitation, parent before child, back after forward navigation.

Scenario B (target search): The system searches for an element named "Dark mode" (exact match). When found, it executes the tap action and stops. Pages Storage, Internal Storage, SD Card are forbidden — the system must not visit them.

---

**5. WORLD TRANSITION**

Scenario A: 19 pages visited, 24 actions dispatched (element interactions + back navigations), 99 total FSM steps. 0 scroll actions (no scrollable content in this fixture). All 18 declared elements across all pages visited.

Scenario B: Stops after finding "Dark mode" on the Display page. 14 pages visited, 14 actions dispatched, 66 FSM steps. Forbidden pages (Storage, Internal Storage, SD Card) not visited.

---

**6. EVENT / DISTURBANCE**

NONE — deterministic fixture, no injected faults.

---

**7. OBSERVED OUTCOME**

Scenario A: Completion reason = `all_visited`. All 18 elements visited (coverage = 18/18). Contract assertions all passed.

Scenario B: Completion reason = `target_found`. Target "Dark mode" found on Display page. Forbidden pages not visited.

---

**8. FAILURE OR SUCCESS CLAIM**

SUCCESS — both scenarios report completion matching intent.

---

**9. CONTRADICTORY EVIDENCE**

NO — fixture behavior is self-consistent.

---

**10. EXPECTED EXTERNAL BEHAVIOR**

Full traversal: all pages reachable through declared transitions must be visited. All interactable elements must be visited. Depth-first order. No page visited more than 5 consecutive times without progressing.

Target search: must stop at target. Must not visit forbidden pages. Numeric anchors: totalSteps=66 for target search, totalSteps=99 for full traversal (these are calibration anchors, not universal truths).

---

**11. EVIDENCE PROVENANCE**

DETERMINISTIC_SIMULATION

- Executable: YES (plain `[Fact]`, always runs)
- Historical Failure: NO
- Reproducible: YES (deterministic)

---

**12. LEGACY MECHANISM CONTEXT**

Non-normative: `StateFixture`, `StateFixtureBuilder`, `StatefulMockVisionService`, `StatefulMockActionExecutor`, `TraversalEngine`, `TraversalFSM`, `DynamicMatch`, `ChildrenStrategy`, `ExpectedBehavior`, `CompletionPolicy`, `PlanCompiler`, `IntentSlots`, `menu_container`, `switch_leaf`, `button_leaf`.

---

**13. NORMALIZED BEHAVIORAL STATEMENT**

Given a 7-page application with 12 transitions and 18 declared interactive elements organized as buttons and switches, when asked to exhaustively traverse all pages, the system visited all 19 pages and all 18 elements over 24 actions, reporting AllVisited. When asked to find "Dark mode" and stop, the system navigated to the Display page and stopped without visiting the forbidden Storage subtree, reporting TargetFound.

---

### E-04 — FsmSimulationRegressionTests

**Title:** FSM-level error-handling behavior under fault injection (synthetic, no external world)

**Source Evidence:** `tests/UniClaw.Core.Tests/StateMachine/FsmSimulationRegressionTests.cs`

---

**1. USER / TASK INTENT**

Verify error-handling invariants of the execution state machine: what happens when actions repeatedly fail, when recovery backtracks, when popups appear, when preconditions are not met, and when AI returns empty responses.

---

**2. INITIAL EXTERNAL WORLD**

None — this is a pure FSM-level test. No external device, no page fixture, no vision. The "world" is a synthetic execution context with a configurable action executor (returns success or failure as directed) and a configurable page analyzer (returns specific page identities or faults as directed).

SIMULATION_ASSUMPTION: The FSM harness directly controls action outcomes, error strategies, page analysis results, and precondition check results. It also directly sets the "current error" and "consecutive error count" in the execution context.

---

**3. AVAILABLE OBSERVATION**

- Action execution result (success/failure as configured by the harness)
- Page analysis result (specific page identity or fault as configured)
- Precondition check result (true/false as configured)
- FSM state (current state name)
- Consecutive error counter value
- Action history (sequence of dispatched action types and success/failure)

---

**4. ACTION / DECISION TAKEN**

Seven fault scenarios tested:

1. **5 failures on sub-page** → after 5 distinct failed items (each generating a new frame), the system dispatches PressBack and transitions to FrameComplete. The item-level gate (≥5 failed items) fires before the consecutive-error gate (≥3).

2. **3 backtracks with consecutive errors** → after 2 backtracks, consecutive error count = 2 (backtrack does NOT reset it). After 3rd error, consecutive-error gate fires → Pop-only (because the frame never dispatched any operation to the physical world, so physical page is still the parent page — PressBack would be wrong).

3. **Popup detected during result verification** → system retries once, then transitions to PopupHandling state.

4. **No page change after action, no popup** → system transitions directly to Branch state (no infinite retry).

5. **Successful action execution** → system transitions to ResultVerify state. Action recorded as successful.

6. **Precondition check returns false** → system transitions to ErrorHandling state (does not attempt action).

7. **AI returns empty response** → system classifies this as non-transient (will not retry).

---

**5. WORLD TRANSITION**

No external world transitions — purely FSM internal state changes. Actions are recorded in history but have no external effect.

---

**6. EVENT / DISTURBANCE**

Injected faults:
- `InvalidOperationException("safety deny")` — simulates safety gate rejection
- `InvalidOperationException("deny")` — simulates general action denial
- `DomainValidationException` for empty AI response
- Failing precondition checker returning false
- Callback page analyzer returning popup page identity

---

**7. OBSERVED OUTCOME**

1. 5-failure scenario: FSM reaches FrameComplete, action history contains `error_recovery_page_item_limit_5`.
2. 3-backtrack scenario: consecutive error count = 2 after 2 backtracks, then gate fires at 3 → Pop-only (`error_recovery_press_back_pop_only`).
3. Popup scenario: FSM reaches PopupHandling after exactly 2 page analysis calls.
4. No-change scenario: FSM reaches Branch.
5. Success scenario: FSM reaches ResultVerify, exactly 1 action recorded as successful.
6. Precondition fail: FSM reaches ErrorHandling.
7. Empty AI response: `IsTransient` returns false.

---

**8. FAILURE OR SUCCESS CLAIM**

All tests assert expected FSM state transitions — all classified as expected behavior (the tests verify the error-handling design, not report failures).

---

**9. CONTRADICTORY EVIDENCE**

NO — these are synthetic FSM-level tests; no external world to contradict.

---

**10. EXPECTED EXTERNAL BEHAVIOR**

- After 5 distinct failed interactions on a sub-page, the system should PressBack (not retry indefinitely).
- Consecutive errors accumulate across backtracks; recovery must not reset the counter.
- When the system has never successfully dispatched an operation on the current page, recovery should Pop (remove the current context) rather than PressBack (which would navigate away from a page the system never left).
- A popup should be detected within 2 page analysis attempts.
- Empty AI responses are structural failures, not transient — no retry.
- Failed preconditions should prevent action execution.

---

**11. EVIDENCE PROVENANCE**

DETERMINISTIC_SIMULATION (FSM-level, no external world)

- Executable: YES (plain `[Fact]`, always runs)
- Historical Failure: YES (Bug #2: consecutive-error counting was broken — Execute throws → catch → ErrorHandling without incrementing)
- Reproducible: YES (deterministic, <1ms per test)

---

**12. LEGACY MECHANISM CONTEXT**

Non-normative: `TraversalFSM`, `FsmSimulationHarness`, `FakeActionExecutor`, `FakeBrain`, `StrategyForcingHandler`, `CallbackPageAnalyzer`, `ErrorHandling`, `PreconditionCheck`, `Execute`, `ResultVerify`, `Branch`, `FrameComplete`, `PopupHandling`, `ErrorStrategy.Backtrack`, `ErrorStrategy.PressBack`, `ConsecutiveErrors`, `NodeFailedItems`.

---

**13. NORMALIZED BEHAVIORAL STATEMENT**

Given a execution loop with configurable action outcomes and page observations, when 5 distinct interactions fail on a sub-page, the system navigates back rather than retrying indefinitely. When consecutive errors accumulate across recovery attempts, the system preserves the error count and applies a Pop-only recovery when no physical navigation has occurred. When a popup appears, the system detects it within 2 observation attempts and transitions to popup handling. When an action succeeds, the system verifies the result. When preconditions fail, the system enters error handling without dispatching the action. An empty AI response is classified as non-transient and not retried.

---

### E-05 — AIIntentSimulationTests

**Title:** Natural-language intent extraction through AI → structured plan construction → deterministic engine execution

**Source Evidence:** `tests/UniClaw.Core.Tests/UniBrain/AIIntentSimulationTests.cs`

---

**1. USER / TASK INTENT**

Natural-language descriptions:
- "Locate About phone from the Android Settings home list and verify the destination page." (target "About phone", depth 2)
- "Enumerate unique first-level Android Settings entries, sample safe read-only pages, and skip dangerous entries." (target null, depth 2)
- "Navigate to Internal Storage in Android Settings and verify the storage breakdown page." (target "Internal Storage", depth 3)
- "Enumerate all Settings pages including sub-pages like Wi‑Fi networks, Storage breakdown, and verify every reachable read-only page up to three levels deep." (depth 3)

---

**2. INITIAL EXTERNAL WORLD**

Simulated Android Settings app with 6 pages (SettingsAppFixture) or 12 pages (DeepSettingsFixture, 3 levels) or 13 pages (BaselineCompatibleFixture). Elements typed as `menu_item` to align with plan construction vocabulary.

SIMULATION_ASSUMPTION: The simulation provides perfect element classification (`menu_item`, `switch`). The AI intent extraction is stubbed with canned JSON responses — real AI uncertainty is not exercised (except in opt-in live tests).

---

**3. AVAILABLE OBSERVATION**

- Stub AI response (JSON): `{"scope":"target_only","element_handling":"menu_only","navigation":"bounded_settings","restore":true,"completion":null}`
- Synthetic vision providing element lists from fixture
- Plan construction output: `TraversalPlan` with `CompletionPolicy`, `RootNode`, `ChildrenStrategy`
- Engine execution: visited pages, completion reason

---

**4. ACTION / DECISION TAKEN**

Two-stage pipeline:
1. **Intent extraction** (stubbed): NL description → AI model → `ExtractedIntentSlots` JSON containing: scope (`target_only` / `full`), element_handling (`menu_only`), navigation, restore, completion.
2. **Plan construction** (deterministic): `IntentSlots` (merging AI-inferred fields with caller-supplied factuals: target app, target name, depth, entry page) → `PlanCompiler` → `TraversalPlan` with `DynamicMatch` children strategy, `CompletionPolicy` (TargetFound for locate, Exhaustive for enumerate), and template registry (`menu_only` → only `menu_container` rules).

Caller-supplied factuals (NOT AI-inferred): TargetApp, Target, Depth, Entry.

---

**5. WORLD TRANSITION**

Locate (depth 2): Engine navigates to "About phone" page, stops. Visited pages < 10 (does not wastefully explore unrelated pages).
Enumerate (depth 2): Engine visits Wi‑Fi, Bluetooth, Display, About phone, Battery.
Deep locate (depth 3): Engine navigates Storage → Internal Storage, stops. Visited pages < 20.
Deep enumerate (depth 3): Engine visits 10+ pages including level-2 (Internal Storage, SD Card) and level-3 (HomeNetwork, OfficeWiFi).

---

**6. EVENT / DISTURBANCE**

NONE in stub-driven tests. AI extraction is deterministic (canned response). Live Sensenova tests (opt-in only) would exercise real AI uncertainty.

---

**7. OBSERVED OUTCOME**

Locate: Completion reason = `TargetFound`. "About phone" in visited pages. "Battery" NOT in visited pages.
Enumerate: Completion reason = `AllVisited`. All expected level-1 pages visited.
Deep locate (depth 3): `TargetFound`, "Storage" and "Internal Storage" in visited pages.
Deep enumerate (depth 3): `AllVisited`, all expected pages across 3 levels visited.

---

**8. FAILURE OR SUCCESS CLAIM**

SUCCESS — all stub-driven tests pass with correct completion reasons.

---

**9. CONTRADICTORY EVIDENCE**

NO within stub-driven tests. The opt-in live tests (`LiveSensenova_*`) may produce different results with real AI — not tested in default baseline.

---

**10. EXPECTED EXTERNAL BEHAVIOR**

- Scope "target_only" → find target and stop (CompletionPolicy: TargetFound, ExecuteThenStop)
- Scope "full" → visit all reachable pages (CompletionPolicy: Exhaustive)
- Depth constraint must be propagated into page exploration (depth=2 → never visit level-3 pages)
- AI-inferred fields (scope, element handling, navigation, restore, completion) are probabilistic — caller must supply factual fields (target app, target, depth, entry)

---

**11. EVIDENCE PROVENANCE**

DETERMINISTIC_SIMULATION (stub AI) + MIXED (opt-in live Sensenova tests)

- Executable: YES (stub: plain `[Fact]`; live: opt-in gated)
- Historical Failure: NO
- Reproducible: YES (stub); UNKNOWN (live)

---

**12. LEGACY MECHANISM CONTEXT**

Non-normative: `IntentExtractor`, `ExtractedIntentSlots`, `PlanCompiler`, `TraversalPlan`, `CompletionPolicy`, `DynamicMatch`, `StubHttpHandler`, `OpenAiCompatibleVisionProvider`, `StatefulMockVisionService`, `StatefulMockActionExecutor`, `TraversalEngine`.

---

**13. NORMALIZED BEHAVIORAL STATEMENT**

Given a natural-language description of a Settings exploration task, when an AI model extracts structured intent fields (scope, element handling, navigation, restore, completion) and the caller supplies factual constraints (target app, target name, depth limit, entry page), the combined intent slots are deterministically compiled into an execution plan. The plan drives a traversal engine through a simulated page graph: for "locate About phone", the engine finds and stops at the target; for "enumerate all Settings", the engine visits all reachable pages within the depth constraint. AI-inferred fields determine exploration mode; caller-supplied factuals constrain the boundary.

---

### E-06 — ScrollableBaselineTests

**Title:** Scrollable list traversal with varied content density profiles (synthetic fixture)

**Source Evidence:** `tests/UniClaw.Core.Tests/Baseline/ScrollableBaselineTests.cs`, contract JSONs at `Baseline/Fixtures/expected/scroll/*.json`

---

**1. USER / TASK INTENT**

Exhaustively scroll through a list of Wi‑Fi networks to visit every visible network entry. Handle edge cases: return to top, deduplicate overlapping items, verify boundary conditions (top=progress 0.0, bottom=IsEndOfList), recover from sparse segments with jump, adapt step size for overlapping content.

---

**2. INITIAL EXTERNAL WORLD**

Simulated Wi‑Fi Settings page with a scrollable list containing 24 network items (pageSize 4, fillRatio 1.0 — dense, each scroll reveals 4 new items). Sparse variant: 8 items, pageSize 2, fillRatio 0.5 (gaps between items). Overlapping variant: 17 items, pageSize 5, fillRatio 1.0 (items overlap across scroll boundaries). Back button at top-left (coordinate 0.05,0.05).

SIMULATION_ASSUMPTION: The simulation directly controls which items are "visible" after each scroll action based on a mathematical model of viewport progress (cumulative vs windowed visibility). Scroll actions always succeed. Item identity is known perfectly (no OCR errors). Scroll progress is known exactly (0.0 to 1.0).

---

**3. AVAILABLE OBSERVATION**

Synthetic scroll-aware vision: after each scroll action, returns the set of items currently visible in the viewport based on scroll progress and paging model. Items have known names ("Network_0" through "Network_23"), coordinates, and types.

SIMULATION_ASSUMPTION: The vision system knows exactly which items are visible after each scroll. Real-world scroll uncertainty (partial visibility, animation lag, coordinate drift) is absent.

---

**4. ACTION / DECISION TAKEN**

Seven scenarios executed (6 test methods, some with sub-variants):
1. Scroll through all 7 screens (24 networks, 6 scroll actions needed)
2. Scroll back to top — progress reverts to 0.0
3. Overlapping content deduplication — items appearing in multiple scroll frames counted once
4. Boundary conditions — initial progress 0.0, final IsEndOfList true
5. Sparse list jump recovery — gaps in content distribution handled without losing items
6. Overlapping list adaptive step — step size increases when overlap is high

---

**5. WORLD TRANSITION**

Dense WiFi list: 6 scroll actions, scroll progress advances from 0.0 to 1.0, all 24 items become visible across 7 screens (including partial overlap).

Sparse list: 3 scroll actions with half-step recovery jumps. All 8 items visited despite sparse distribution (only 2 items per page, 50% fill).

Overlapping list: 3 scroll actions with increased step size. All 17 unique items visited across 18 page observations.

---

**6. EVENT / DISTURBANCE**

SIMULATION_ASSUMPTION: The simulation models controlled disturbances:
- Sparse fill → jump detection → half-step recovery (legacy: "跳跃检测管线已删" — jump detection pipeline deleted; now uses seen-set differential termination)
- Overlapping content → adaptive step size increase (legacy: "自适应步长管线已删" — adaptive step pipeline deleted)
- Element deduplication → same-named items appearing in multiple frames counted once

---

**7. OBSERVED OUTCOME**

Dense WiFi: 26 pages visited, 30 actions (scrolls + element taps), 104 FSM steps, 5 scrolls, scroll distance 1, final progress 1.0. All 25 network elements visited (including Network3/6 overlap dedup).

Sparse: 5 pages, 7 actions, 20 steps, 3 scrolls. All 8 items visited.

Overlapping: 18 pages, 20 actions, 72 steps, 3 scrolls. High overlap reduced scroll count while visiting all unique items.

All contracts report `all_visited`. All assertions pass.

---

**8. FAILURE OR SUCCESS CLAIM**

SUCCESS — all scroll scenarios complete with `all_visited`.

---

**9. CONTRADICTORY EVIDENCE**

NO — fixture behavior is self-consistent. However, notes indicate jump-detection and adaptive-step pipelines were deleted (now using seen-set differential termination instead), meaning these disturbance-handling mechanisms are no longer active in the codebase.

---

**10. EXPECTED EXTERNAL BEHAVIOR**

- Scroll through all content → all items visited, no duplicates counted twice
- Scroll back to top → progress reverts correctly
- Boundary detection: top = progress 0.0, bottom = IsEndOfList true
- Sparse content: detection of content gaps → recovery with reduced step
- Overlapping content: detection of high overlap → step size increased to reduce unnecessary scrolls
- Scroll termination must be based on content stability (seen-set differential), not scroll failure

---

**11. EVIDENCE PROVENANCE**

DETERMINISTIC_SIMULATION

- Executable: YES (plain `[Fact]`, always runs)
- Historical Failure: NO
- Reproducible: YES (deterministic)

---

**12. LEGACY MECHANISM CONTEXT**

Non-normative: `SimulatedScreen`, `PagedItemGenerator`, `ScrollableMockVisionService`, `ScrollableMockActionExecutor`, `WiFiListFixture7Screens`, `ExpectedBehavior`, `ScrollBehaviorProfile`, `cumulative visibility`, `windowed visibility`, `fillRatio`, `pageSize`, `seen-set differential termination`.

---

**13. NORMALIZED BEHAVIORAL STATEMENT**

Given a scrollable list of 24 items with 4 items visible per screen, when asked to exhaustively scroll through all content, the system performed 6 scroll actions, visited all 24 items (with overlap deduplication), detected the end of the list, and reported completion. When content is sparse (gaps between items), the system detected gaps and recovered with half-step scrolling. When content overlaps heavily across scroll boundaries, the system increased step size to reduce unnecessary scrolling.

---

### E-07 — MultiBranchNavigationTests

**Title:** UNFIXED BUG: hub with two navigation branches — only first branch traversed, false AllVisited

**Source Evidence:** `tests/UniClaw.Core.Tests/Baseline/MultiBranchNavigationTests.cs`

---

**1. USER / TASK INTENT**

Exhaustively enumerate all reachable content from a hub page that has two navigation buttons: "Go to List A" (leads to a scrollable list of 16 items) and "Go to List B" (leads to another scrollable list of 16 items). All 32 items across both branches should be visited.

Also: verify the bug is not scroll-related — test with non-scrollable branches (3 static items each, 6 total).

---

**2. INITIAL EXTERNAL WORLD**

Simulated hub page with two buttons:
- Button A: "Go to List A" at coordinate (0.50, 0.30) → navigates to "List A" page with 16 scrollable items (A_0 through A_15, pageSize 4)
- Button B: "Go to List B" at coordinate (0.50, 0.50) → navigates to "List B" page with 16 scrollable items (B_0 through B_15, pageSize 4)

Neither List A nor List B has a back button (back navigation must be inferred).

Non-scrollable variant: same hub structure, but List A2 has 3 static read-only items (A2_item_0/1/2), List B2 has 3 static read-only items (B2_item_0/1/2). No scroll, no back buttons.

SIMULATION_ASSUMPTION: Both navigation buttons are equally visible to the observation system. Both are correctly classified as navigable elements. The page graph is known to the fixture but the system must discover it through observation.

---

**3. AVAILABLE OBSERVATION**

Synthetic vision returns both buttons on the hub page. On List A, returns scrollable content (items A_0 through A_3 visible initially, more revealed by scrolling). On List B, returns scrollable content (items B_0 through B_3 visible initially). No back buttons visible on either list page.

---

**4. ACTION / DECISION TAKEN**

Observed behavior (BUG): The system taps "Go to List A", scrolls through all 16 items on List A, then... stops. It never taps "Go to List B". List B remains at 0/16 items visited.

The system then reports completion.

Expected behavior (per test assertion): The system should tap "Go to List A", visit all 16 items, navigate back to hub, tap "Go to List B", visit all 16 items. Back actions should appear in the action history.

---

**5. WORLD TRANSITION**

Observed: Hub → List A (scroll through 16 items) → (stops). List B never visited.

Expected: Hub → List A (scroll through 16 items) → back to Hub → List B (scroll through 16 items) → completion.

---

**6. EVENT / DISTURBANCE**

The bug IS the disturbance: the second navigation branch is silently skipped. There is no error, no timeout, no crash. The system simply does not attempt the second button.

Non-scrollable variant confirms the bug is not scroll-related — even with 3 static items per branch (no scrolling needed), the second branch is not visited.

---

**7. OBSERVED OUTCOME**

**BUG BEHAVIOR (verbatim from source comments at lines ~151, ~202, ~249):**

Test 1 (scrollable): "当前行为 (BUG): listA 16/16, listB 0/16, CompletionReason=AllVisited (谎言)。期望行为: listA 16/16, listB 16/16, CompletionReason=AllVisited (真实)。TDD: 此测试当前 FAIL"

Test 2 (deep nav): "当前行为 (BUG): 只走第一个分支, 深层页可能丢失。TDD: 此测试当前 FAIL"

Test 3 (non-scrollable): "验证 bug 与滚动无关 — 即使没有滚动, 第二个分支也不被访问。TDD: 此测试当前 FAIL"

Legacy system reports `CompletionReason = AllVisited` despite listB having 0/16 items visited. The test assertions demand TotalSteps < 500, a "back" action in history, and both "Go to List A" and "Go to List B" in visited pages — all of which the current buggy behavior fails.

---

**8. FAILURE OR SUCCESS CLAIM**

Legacy system claims: SUCCESS (`CompletionReason = AllVisited`)

---

**9. CONTRADICTORY EVIDENCE**

**YES — direct contradiction between legacy claim and observed facts.**

| Legacy Claim | Observed Fact |
|---|---|
| AllVisited (all content visited) | listB visited count = 0/16 |
| Completion (task finished) | Second navigation button never tapped |
| Exhaustive enumeration | Only first branch traversed |

The bug persists despite an archived OpenSpec change (`navigation-subpage-frames`) marked as complete. The test file explicitly documents TDD failures that have not been fixed.

---

**10. EXPECTED EXTERNAL BEHAVIOR**

Both navigation branches must be visited. All 32 items (16 per branch) must be visited. Completion must not be reported until all reachable branches are exhausted. Back navigation between branches must occur. This expectation is documented in the test assertions (tests currently FAIL).

---

**11. EVIDENCE PROVENANCE**

DETERMINISTIC_SIMULATION

- Executable: YES (plain `[Fact]`, always runs — tests currently FAIL)
- Historical Failure: YES (documented unfixed bug; archived OpenSpec change `navigation-subpage-frames` marked complete but bug persists)
- Reproducible: YES (deterministic, reproduces every run)

---

**12. LEGACY MECHANISM CONTEXT**

Non-normative: `TraversalEngine`, `DynamicMatch`, `ChildrenStrategy`, `StateFixture`, `ScrollableMockVisionService`, `PagedItemGenerator`, `CompletionReason.AllVisited`, `TraversalResult`, `TotalSteps`, `ActionHistory`.

---

**13. NORMALIZED BEHAVIORAL STATEMENT**

Given a hub page with two navigation buttons leading to two independent branches (each containing 16 scrollable items), when asked to exhaustively enumerate all content, the system traversed only the first branch (16/16 items visited), never tapped the second navigation button (0/16 items visited on second branch), and reported AllVisited — contradicting the observable fact that the second branch was never entered. This behavior is independent of scroll (reproduces with static non-scrollable branches of 3 items each). The expected behavior — visiting both branches and all 32 items — is documented in failing test assertions.

---

### E-08 — TraceReplayFromRunTests

**Title:** Real-run artifact discovery → deterministic replay → depth runaway diagnosis → fix verification

**Source Evidence:** `tests/UniClaw.Core.Tests/Simulation/TraceReplay/TraceReplayFromRunTests.cs`

---

**1. USER / TASK INTENT**

Reproduce a real integration run failure deterministically without an emulator. Use recorded observations to diagnose a depth-runaway bug (subframe depth reached 4, should be ≤2). Verify the fix constrains depth.

---

**2. INITIAL EXTERNAL WORLD**

**Original run:** Android emulator running Settings app. Real AI vision provider. Real ADB. Run ID `20260805T052309367Z` (max_steps loop), run `20260806T072558649Z` (subtitle double-click, Flash misclick, PressBack-exits-desktop).

**Replay:** No external world. Replay harness loads recorded `analysis.jsonl` frames (JSONL PageAnalysis records) and recorded action sequences from `run.log`. The replay runs without emulator, without AI, without ADB. Execution is deterministic at <1s per iteration.

---

**3. AVAILABLE OBSERVATION**

**Original run artifacts** (auto-discovered from `artifacts/runs/{scenario}/{runId}/`):
- `plan.json` — serialized traversal plan
- `result.json` — run outcome (completionReason, actionsAttempted)
- `assets/{runId}/analysis.jsonl` — recorded vision frames (PageAnalysis per step)
- `trace/{runId}/run.log` — FSM trace log with action sequences

**Replay input:** `TraceReplayHarness.FromRunDir(runDir)` loads plan + analysis frames. `TraceReplayVisionService` replays frames in sequence (frame index → PageAnalysis). `TraceReplayActionExecutor` replays actions from `run.log` or uses engine decisions (no real device I/O, always returns success).

---

**4. ACTION / DECISION TAKEN**

Step 1 (AutoDiscoverAndReplay): Find latest run directory for scenario `skill-test/enumerate-settings-safely` by scanning `artifacts/runs/`. Load artifacts. Replay through `TraceReplayHarness`. Diagnose result.

Step 2 (Diagnose_DepthRunaway): Compute max subframe depth from visited page names (split by `_subframe`, count segments - 1). Before fix: depth=4 observed. After fix: depth "no longer meaningful" (vision frames don't match post-fix engine behavior). Verify engine ran without crash (`TotalSteps > 0`).

Step 3 (FixVerify_RestoreConstrainsDepth): Compile a new plan with `safe_mode` template, `BindCurrentScreen` entry, `full` scope. Run replay with this plan. Verify engine completes without crash.

---

**5. WORLD TRANSITION**

No external world transitions. Replay reproduces the internal execution path: recorded observations drive the same FSM transitions that occurred during the original run.

---

**6. EVENT / DISTURBANCE**

Pre-fix disturbance: subframe depth reached 4 (should be ≤2). This was a real bug reproduced from run artifacts.

Post-fix: depth constrained. Replay verification shows engine no longer produces depth=4.

Additional historical disturbances referenced: run `20260806T072558649Z` — subtitle double-click (same element clicked twice because subtitle degradation not detected), Flash notifications mis-click (text-type element matched by Contains search meant for menu items), PressBack to desktop (erroneous gate triggered by consecutive errors from non-idempotent container re-execution).

---

**7. OBSERVED OUTCOME**

Pre-fix replay: max subframe depth = 4. Engine runs to completion (TotalSteps > 0) but depth constraint violated.

Post-fix replay: engine runs (TotalSteps > 0). Depth constraint verification deferred to L2/L3 fixture tests (E-09).

Replay export: `trace-replay-export.json` written to `artifacts/sim-replay/` (committed, 1552 lines, 19 steps, 172 analysis frames).

---

**8. FAILURE OR SUCCESS CLAIM**

Pre-fix: FAILURE (depth=4 violates constraint). Post-fix: SUCCESS (depth constrained). Replay itself: SUCCESS (engine runs deterministically).

---

**9. CONTRADICTORY EVIDENCE**

NO — replay faithfully reproduces recorded behavior. However, post-fix verification notes that "vision frames and engine behavior no longer match" after the fix, meaning replay of old traces against new engine code may diverge. The replay harness can detect this mismatch but the test only asserts TotalSteps > 0 (no crash).

---

**10. EXPECTED EXTERNAL BEHAVIOR**

- Real-run failures must be reproducible deterministically from recorded artifacts without emulator
- Subframe depth must never exceed maxDepth constraint
- After a fix, old traces may diverge from new engine behavior — this is expected and detectable
- Fix verification requires fixture-based tests (E-09 L2/L3) in addition to replay

---

**11. EVIDENCE PROVENANCE**

REPLAY (derived from RECORDED_RUN)

- Executable: YES (self-skip when `artifacts/runs/` absent)
- Historical Failure: YES (depth=4 bug, max_steps loop, subtitle double-click, Flash misclick, PressBack to desktop)
- Reproducible: YES (deterministic replay <1s, no emulator)

---

**12. LEGACY MECHANISM CONTEXT**

Non-normative: `TraceReplayHarness`, `TraceReplayVisionService`, `TraceReplayActionExecutor`, `analysis.jsonl`, `run.log`, `plan.json`, `result.json`, `PlanCompiler`, `IntentSlots`, `BindCurrentScreen`, `safe_mode`, `FindLatestRun`, `ExportReplayJson`.

---

**13. NORMALIZED BEHAVIORAL STATEMENT**

Given recorded observation frames, action logs, and execution plan from a real integration run that failed with a depth-runaway bug (subframe depth reached 4, exceeding the constraint of 2), when these artifacts are replayed through a deterministic harness without any emulator or AI, the same depth violation is reproduced in under 1 second. After applying a depth constraint fix, the replay harness confirms the engine no longer crashes. However, post-fix replay of pre-fix traces shows divergence between recorded observations and post-fix engine behavior — the fix changes what actions would have been taken.

---

### E-09 — FixVerificationTests

**Title:** 8-layer layered regression verification encoding distinct historical bug classes

**Source Evidence:** `tests/UniClaw.Core.Tests/Simulation/TraceReplay/FixVerificationTests.cs`

---

**1. USER / TASK INTENT**

Encode 8 distinct historical bug classes as permanent deterministic regression tests. Each layer reproduces a specific failure pattern observed in real runs and verifies the fix prevents recurrence.

---

**2. INITIAL EXTERNAL WORLD**

Depends on layer:
- L1: Replayed real trace (no fixture — uses recorded PageAnalysis frames)
- L2–L8: Synthetic fixtures representing specific bug scenarios:
  - L2/L3: 4-level nested Settings pages (settings → Network & internet → Internet → Wi‑Fi)
  - L4: Settings page with "Stale item" (click does nothing, no transition) and "Working item" (navigates to detail)
  - L5: Settings page with 5 menu items including empty-text "" and whitespace "   "
  - L6: No fixture — pure text normalization function test
  - L7: No fixture — pure depth-semantics formula test with synthetic node stack
  - L8: Settings page with subtitle text "Bluetooth, pairing" between menu items

SIMULATION_ASSUMPTION: Fixtures encode exact element types and coordinates as they appeared in the buggy real runs.

---

**3. AVAILABLE OBSERVATION**

L1: Recorded PageAnalysis frames from real run trace.
L2–L5, L8: Synthetic vision from fixture.
L6: Raw text strings with OCR variants.
L7: Synthetic execution context with configurable stack depth.

---

**4. ACTION / DECISION TAKEN**

**L1 — ReplayRegression:** Replay old trace with post-fix engine. Verify engine runs without crash. Depth constraint verification deferred to L2/L3.

**L2 — DepthConstraint_StopsAtLevel2:** Plan with maxDepth=2, Exhaustive completion. Engine must NOT visit any page containing "Wi‑Fi"/"Wi‑Fi"/"Advanced" (depth=3 pages). Must visit pages containing "Network & internet". Must NOT contain `dyn_menu_container_internet` (Internet child skipped by depth guard, not infinite re-select/click).

**L3 — FsmInvariant_SubframeDepthNeverExceedsMaxDepth:** Same fixture as L2. Computes `MaxSubframeDepth(result)`. Asserts ≤ 2.

**L4 — StaleClick_NodeSkippedAfterLimit:** "Stale item" clicked exactly once (depth guard prevents re-click — depth out of range, frame completes immediately). "Working item" clicked ≥ 1 time. TotalSteps < 50 (no infinite retry). Circuit breaker threshold (3 consecutive same-page clicks) never fires in this fixture because depth guard prevents re-clicks.

**L5 — EmptyTextItem_SkippedInGenerate:** Empty (`""`) and whitespace-only (`"   "`) OCR text items generate no child nodes. Pages for these items do NOT appear in VisitedPages. "Wi‑Fi", "Bluetooth", "Display" are visited normally. No exception thrown.

**L6 — NormalizeItemText_OcrVariants:** 9 test cases. Normalization: lowercase, normalize comma-spacing (`, ` ↔ `,`), collapse whitespace. `null` → `""`. `"  Bluetooth   pairing  "` → `"bluetooth pairing"`. `"Bluetooth, pairing"` → `"bluetooth, pairing"`.

**L7 — DepthSemantics_FormulaBranches:** At stackDepth=2 (root depth 1 + 1 child = depth 2), with maxDepth=2: since `Depth >= MaxDepth+1`? No (2 >= 3 is false) → template `menu_container` → Container type, Click operation. At stackDepth=3: `3 >= 3` → template degrades to `leaf_info` → LeafInfo type, NoAction operation. Children at max depth are discovered but not clicked.

**L8 — SubtitleDegraded_NoDoubleClick_SamePage:** "Connected devices" menu item at (0.38, 0.54) is tapped exactly once. Subtitle text "Bluetooth, pairing" at (0.31, 0.57) with dy_full=0.0336 is never tapped (correctly excluded from navigation candidates). TotalSteps < 30.

---

**5. WORLD TRANSITION**

L2: Engine stops at depth=2. Never enters depth-3 pages.
L4: Stale item clicked once, then skipped. Working item navigated to successfully.
L5: Empty-text items silently skipped. Valid items navigated to.
L8: Connected devices page visited once. Subtitle never clicked. No double-click.

---

**6. EVENT / DISTURBANCE**

Each layer encodes a specific disturbance class from real runs:
- L2/L3: Depth constraint violation (DynamicMatch sub-frame generation ignored maxDepth)
- L4: Stale click (element exists in observation but clicking it produces no page change)
- L5: Empty OCR text (vision returns element with empty/missing text)
- L6: OCR text variants (same element labeled differently across observations: "Bluetooth, pairing" vs "Bluetooth,pairing")
- L7: Depth semantics at boundary (container should become non-interactive at max depth)
- L8: Subtitle misidentification (text element adjacent to menu item should not be treated as navigable)

Additional historical bugs referenced (from run `20260806T072558649Z`):
- (a) Flash notifications mis-click: Contains-match on text type element (should only match menu items)
- (b) Erroneous PressBack to desktop: non-idempotent container re-execution loop → consecutive errors ≥ 3 → gate fires → PressBack exits app

---

**7. OBSERVED OUTCOME**

All L1–L8 assertions pass. Each encoded bug class is prevented by the corresponding fix.

---

**8. FAILURE OR SUCCESS CLAIM**

SUCCESS — all regression guards pass. The historical bugs they encode are prevented.

---

**9. CONTRADICTORY EVIDENCE**

NO — all layers pass. However, L1 notes that post-fix replay of pre-fix traces shows "vision frames no longer match engine behavior" — the replay harness detects behavioral divergence but the test only asserts no-crash. The real verification of depth constraint is in L2/L3 fixture tests, not L1 replay.

---

**10. EXPECTED EXTERNAL BEHAVIOR**

- Depth constraint must be enforced during sub-frame generation (not just at plan construction)
- Stale elements (click produces no observable change) must be detected and skipped after a limit (3 consecutive same-page clicks)
- Empty or whitespace-only OCR text must not generate navigable child tasks
- OCR variants of the same text must normalize to identical form for deduplication
- At max depth, containers must degrade to non-interactive (discover only, no click)
- Adjacent text elements must not be mistaken for navigable menu items

---

**11. EVIDENCE PROVENANCE**

MIXED — L1 is REPLAY (real trace), L2–L8 are DETERMINISTIC_SIMULATION

- Executable: YES (all plain `[Fact]`, always run)
- Historical Failure: YES (each layer encodes a bug from real run `20260806T072558649Z` and other historical failures)
- Reproducible: YES (deterministic)

---

**12. LEGACY MECHANISM CONTEXT**

Non-normative: `FixVerificationTests`, `TraceReplayHarness`, `StateFixture`, `DynamicChildManager`, `NormalizeItemText`, `NodeStack`, `DepthSemanticsContext`, `TraversalRuntimeContext`, `MaxSubframeDepth`, `menu_container`, `leaf_info`, `GenerateChild`, `Container`, `LeafInfo`, `Click`, `NoAction`.

---

**13. NORMALIZED BEHAVIORAL STATEMENT**

Given 8 distinct historical failure patterns observed in real Android Settings exploration runs — depth runaway (subframe depth reached 4, should be ≤2), stale clicks causing infinite retry, empty OCR text generating invalid navigation targets, OCR text variants causing duplicate visits, and subtitle text being mistaken for navigable menu items — each pattern was encoded as a deterministic regression test. The fixes prevent: depth exceeding maxDepth at sub-frame generation, infinite retry on unchanged pages (circuit breaker after 3 same-page clicks), empty-text items generating navigable tasks, OCR variants causing deduplication failures, containers at max depth being treated as interactive, and adjacent text elements being misidentified as navigation targets.

---

### E-10 — TraceReplay_20260805T052309367Z

**Title:** Concrete real-run failure reproduction: DFS revisit loop + search-box stuck

**Source Evidence:** `tests/UniClaw.Core.Tests/Simulation/TraceReplay/20260805T052309367Z_TraceReplayTests.cs`, `20260805T052309367Z_EnumerateFixtures.cs`

---

**1. USER / TASK INTENT**

Reproduce a specific real integration run failure: run `20260805T052309367Z-1bc7a25ea6384e3`, scenario `enumerate-settings-safely`, provider=local, mode=direct. The run failed with `max_steps (120)`, `settings_home_not_restored`. The task was to enumerate Settings entries safely.

---

**2. INITIAL EXTERNAL WORLD**

**Original run:** Android emulator running Settings app (`com.android.settings`). Local vision provider. Safe mode (menu-only interaction). Plan with depth=2, restore=false, 4 DynamicRules.

**Replay fixture:** Hand-reconstructed from real `analysis.jsonl` rows + `run.log` actions. Data provenance documented inline:
- Page data source: `assets/{runId}/analysis.jsonl` (real vision frames)
- Action source: `trace/{runId}/run.log` (real FSM trace log)
- Plan source: `plan.json` (safe_mode, depth=2)

Fixture pages reconstructed:
- `settings`: 8 elements including input "QSearch settings", menu items "Network & internet", "Connected devices", "Bluetooth, pairing", "Apps", "Recent apps,default apps", "Notifications", "Notification history, conversations", back button
- `network_internet`: "Internet", "SIMs", back button
- `internet`: "Wi‑Fi", "T-Mobile", switch "Mobile data", back button
- Self-loop transition: `t_int2: internet → internet` (DFS revisit loop — clicking "Internet" again from the Internet page navigates to... Internet)

Search-box variant:
- `settings_search`: "Q Search settings" misclassified as type `menu_item` (should be `input`), plus normal menu items
- `search_ui`: search input + "Wi‑Fi" search result + back button
- Self-loop: `t_search_stay: search_ui → search_ui` (clicking search result stays on search page → stuck)

---

**3. AVAILABLE OBSERVATION**

Replay harness replays reconstructed observation frames. Vision returns the elements declared in the fixture for each page.

---

**4. ACTION / DECISION TAKEN**

Test 1 (DfsRevisitLoop): Engine executes traversal plan on the DFS revisit loop fixture. The self-loop transition causes the engine to re-enter the same page repeatedly.

Test 2 (SearchBoxInput): Engine correctly skips the input-typed search box (DynamicRule only matches `menu_item`, `switch`, `button` — not `input`). Normal traversal succeeds.

Test 3 (SearchBoxMenuItem): Engine treats the misclassified search box (type=menu_item) as a navigable entry. Tapping it enters the search UI, where tapping a search result stays on the search page (self-loop). Engine gets stuck.

---

**5. WORLD TRANSITION**

Test 1: Settings → Network & internet → Internet → (self-loop: Internet → Internet) → (loop repeats) → back → Network & internet → back → Settings → (potentially re-enters the loop). Engine either exhausts max_steps or (if loop detection works) terminates with AllVisited.

Test 3: Settings → search_ui (misclick on search box) → (self-loop: search_ui → search_ui) → stuck. Engine cannot escape.

---

**6. EVENT / DISTURBANCE**

Two real-world disturbance patterns:
1. **DFS revisit loop**: After back from a sub-page, the engine re-enters the page it just left, creating an infinite self-loop on the transition graph.
2. **Search box misclassification**: YOLO classified a search input as `menu_item` instead of `input` → engine treats it as navigable → enters search UI → can't escape.

---

**7. OBSERVED OUTCOME**

Test 1: Completion reason = MaxSteps OR AllVisited (the "stuck" end state — engine either exhausts steps or detects loop and terminates).
Test 2: Normal completion. "Network & internet" in VisitedPages. Search input correctly skipped.
Test 3: Completion reason = MaxSteps OR AllVisited. Engine stuck in search UI.

---

**8. FAILURE OR SUCCESS CLAIM**

Original run: FAILURE (`max_steps (120), settings_home_not_restored`).
Replay: the test asserts MaxSteps OR AllVisited (accepts either as the "stuck" end state), so the test itself passes — but the behavior it documents is a failure.

---

**9. CONTRADICTORY EVIDENCE**

NO — the replay faithfully reproduces the original run's stuck behavior.

---

**10. EXPECTED EXTERNAL BEHAVIOR**

- DFS revisit loops should be detected and terminated (not exhaust MaxSteps)
- Element type classification errors (input misclassified as menu_item) should not cause permanent stuck states
- Search boxes should be excluded from navigation candidates
- After exhausting a sub-page, the engine should not immediately re-enter it

---

**11. EVIDENCE PROVENANCE**

MIXED — RECORDED_RUN (original artifacts) + DETERMINISTIC_SIMULATION (reconstructed fixture)

- Executable: YES (fixtures are committed; plain `[Fact]`, always runs)
- Historical Failure: YES (real run `20260805T052309367Z-1bc7a25ea6384e3` — max_steps 120, settings_home_not_restored)
- Reproducible: YES (deterministic replay of reconstructed fixture)

---

**12. LEGACY MECHANISM CONTEXT**

Non-normative: `TraceReplayHarness`, `EnumerateFixtures`, `analysis.jsonl`, `run.log`, `plan.json`, `StatefulMockVisionService`, `StatefulMockActionExecutor`, `DfsRevisitLoop`, `SearchBoxStuck`, `DynamicMatch`, `menu_container`, `safe_mode`.

---

**13. NORMALIZED BEHAVIORAL STATEMENT**

Given a real integration run that failed after 120 steps without restoring the Settings home page, when the recorded observations and actions are reconstructed into a deterministic fixture, the replay reproduces two failure modes: (1) a DFS revisit loop where the engine re-enters a page it just left, creating an infinite self-loop on the transition graph; (2) a search box misclassified as a navigable menu item, causing the engine to enter a search UI from which it cannot escape. In both cases the engine either exhausts its step budget or detects the loop and terminates without completing the intended enumeration.

---

### E-11 — SettingsEnumerateRegression

**Title:** Permanent regression: depth constraint violated by DynamicMatch sub-frame generation

**Source Evidence:** `tests/UniClaw.Core.Tests/Simulation/TraceReplay/SettingsEnumerateRegression.cs`

---

**1. USER / TASK INTENT**

Enumerate Android Settings entries with a hard depth constraint of maxDepth=2. The engine must never enter pages at depth=3 (e.g., Wi‑Fi under Internet under Network & internet). This is a regression test for a real bug where DynamicMatch sub-frame generation ignored the maxDepth constraint.

---

**2. INITIAL EXTERNAL WORLD**

Simulated API-35 Android Settings with 4 levels of nesting:
- Depth 0: Settings home (search input + 4 menu items: Network & internet, Connected devices, Apps, Notifications)
- Depth 1: Network & internet (6 items: Internet, SIMs, Airplane mode, Hotspot & tethering, Data Saver, VPN)
- Depth 2: Internet (Wi‑Fi, T-Mobile, Mobile data switch)
- Depth 3: Wi‑Fi ("Advanced" — should NOT be reached)
- Additional depth-2 sub-pages: SIMs, Airplane mode, Hotspot & tethering, Data Saver, VPN (each with back button)
- 18 transitions including self-loop on Wi‑Fi Advanced (clicking "Advanced" stays on Wi‑Fi page)

Coordinates and names mirror `uniclaw-lite-api35` emulator.

---

**3. AVAILABLE OBSERVATION**

Synthetic vision returns all declared elements with correct types and coordinates. Each page's elements are known.

---

**4. ACTION / DECISION TAKEN**

Plan: `IntentSlots("com.android.settings", "full", Depth: 2)`, CompletionPolicy Exhaustive, DynamicMatch root with `menu_container` rule only (match type: menu_item, GenerateChild).

PRE-FIX BUG: DynamicMatch sub-frame generation did not check maxDepth. When the engine was at depth=2 on the Internet page, it would still generate a child for "Wi‑Fi" (depth=3), enter it, and continue deeper. Real runs reached depth=3+ pages.

POST-FIX: Engine checks `Depth >= MaxDepth+1` formula. At depth=2 with maxDepth=2, any sub-frame generation is blocked. The engine stops at depth=2, never enters Wi‑Fi.

---

**5. WORLD TRANSITION**

Pre-fix: Settings → Network & internet → Internet → Wi‑Fi → (deeper or stuck).
Post-fix: Settings → Network & internet → Internet → (stops at depth=2, does not enter Wi‑Fi).

---

**6. EVENT / DISTURBANCE**

The bug was the disturbance: depth constraint declared in the plan (Depth: 2) was not enforced during sub-frame generation. The plan-level constraint existed but the execution-level expansion ignored it.

---

**7. OBSERVED OUTCOME**

Post-fix assertion: VisitedPages must NOT contain any page matching `"wifi"`, `"advanced"`, or `"Wi-Fi"` (case-sensitive for wifi/advanced). Must contain pages matching `"network"` and `"internet"` (case-insensitive). Engine stops at depth=2.

---

**8. FAILURE OR SUCCESS CLAIM**

Pre-fix: FAILURE (depth runaway). Post-fix: SUCCESS (depth constraint honored).

---

**9. CONTRADICTORY EVIDENCE**

NO — post-fix behavior matches the intent constraint.

---

**10. EXPECTED EXTERNAL BEHAVIOR**

A depth constraint declared at plan construction (Depth: N) must be enforced at every level of execution — including sub-frame generation. The effective depth at any point must never exceed the declared maximum. The constraint applies to dynamically discovered content, not just statically declared pages.

---

**11. EVIDENCE PROVENANCE**

DETERMINISTIC_SIMULATION

- Executable: YES (plain `[Fact]`, always runs)
- Historical Failure: YES (real bug: DynamicMatch sub-frame generation ignored maxDepth → real runs hit depth=3+)
- Reproducible: YES (deterministic)

---

**12. LEGACY MECHANISM CONTEXT**

Non-normative: `SettingsEnumerateRegression`, `Api35Settings`, `StateFixture`, `IntentSlots`, `Depth`, `DynamicMatch`, `menu_container`, `CompletionPolicyType.Exhaustive`, `maxDepth`, `sub-frame generation`.

---

**13. NORMALIZED BEHAVIORAL STATEMENT**

Given a 4-level nested Settings application and a plan with a hard depth constraint of 2 levels, before the fix the engine entered depth-3 pages (Wi‑Fi under Internet) because sub-frame generation did not check the depth limit. After the fix, the engine stops at depth 2 — it visits Network & internet and Internet, but never enters Wi‑Fi or any page at depth 3. The depth constraint declared in the plan must be enforced during content discovery, not just at plan construction time.

---

### E-12 — ContainerGatewayTests

**Title:** False completion patterns: scroll-only dead-end detection and unconsumed FrameCompleted

**Source Evidence:** `tests/UniClaw.Core.Tests/Traversal/ContainerGatewayTests.cs`

---

**1. USER / TASK INTENT**

Verify two completion fixes:
Pattern 1: When a container has scrollable content that never changes (same items on every observation), the engine must detect content stability and complete with AllVisited — not scroll infinitely until MaxSteps exhaustion.
Pattern 2: When a static sub-frame has no children and no operation, its FrameCompleted signal must be consumed and the parent must continue — the sub-frame must not remain permanently stuck.

---

**2. INITIAL EXTERNAL WORLD**

Pattern 1: A page with scrollable content that always returns the same elements (direction Left, items: MenuItems at fixed coordinate). `HasScroll() = true`, `GetScrollProgress() = 0.5`, `IsEndOfList() = false`. Content never changes regardless of scroll actions.

Pattern 2: A root page with one static child. The child has no children (`StaticChildren: []`) and no operation. The child has no meaningful work to do.

SIMULATION_ASSUMPTION: The simulation provides a "stable page analyzer" that returns identical content every time. Scroll state always reports "scrollable, not at end" regardless of scroll actions. The recording action executor counts taps, swipes, and back presses — all return success.

---

**3. AVAILABLE OBSERVATION**

Pattern 1: Every observation returns the same page content (identical element list, identical fingerprint). Scroll actions succeed but content never changes.

Pattern 2: Root page is observed. Child frame exists but has no observable content (no children, no action to dispatch).

---

**4. ACTION / DECISION TAKEN**

Pattern 1 (old behavior): Engine scrolls → observes same content → no dead-end detected (dead-end logic only triggered on "scroll failed" branch) → scrolls again → infinite loop until MaxSteps exhausted.

Pattern 1 (new behavior): Gateway detects content stability — 3 consecutive identical normalized content fingerprints with no child push → root frame completes → AllVisited. MaxEmptyScrollRetries raised to 10 so the only exit path is gateway stability (not scroll-end fallback).

Pattern 2 (old behavior): Child frame reports FrameCompleted (no work to do). This signal is unconsumed for non-root frames. Child frame remains permanently stuck. Engine eventually exhausts MaxSteps.

Pattern 2 (new behavior): Gateway performs Pop-only for non-root frames (frame never dispatched an operation → physical page unchanged → PressBack would be wrong). Root frame residual logic continues → root completes → AllVisited.

---

**5. WORLD TRANSITION**

Pattern 1: Scroll actions are dispatched but page content never changes. After 3 identical observations, root frame completes without reaching MaxSteps.

Pattern 2: Child frame is popped (removed from execution context) without any back navigation. Root frame then completes normally.

---

**6. EVENT / DISTURBANCE**

Pattern 1 disturbance: Content stability — the page is scrollable but content never changes (no new items appear). This is a real-world scenario (short lists, non-scrollable containers misidentified as scrollable, ADB scroll failure silently returning same content).

Pattern 2 disturbance: Exhausted sub-frame — a child context has no work to do (no children, no operation). In a real scenario, this could be a sub-page with zero interactable elements.

---

**7. OBSERVED OUTCOME**

Pattern 1: CompletionReason = AllVisited (NOT MaxSteps). TotalSteps < 30 (early termination, not exhaustion). Back presses = 0. Swipes > 0 (scroll actions were dispatched but content stability terminated the loop).

Pattern 2: CompletionReason = AllVisited. TotalSteps < 30. Back presses = 0 (Pop-only — no physical back navigation). Taps = 0 (child had no operation to dispatch).

---

**8. FAILURE OR SUCCESS CLAIM**

Pre-fix (old behavior): FAILURE — both patterns exhausted MaxSteps without completing.
Post-fix (new behavior): SUCCESS — both patterns complete with AllVisited.

---

**9. CONTRADICTORY EVIDENCE**

NO — post-fix behavior is self-consistent.

---

**10. EXPECTED EXTERNAL BEHAVIOR**

- When scrolling produces no new observable content after K consecutive attempts (K=3), the container is complete — do not scroll indefinitely
- Scroll-exhaustion detection must not depend on scroll-action failure; content stability is the primary signal
- When a sub-page has no interactable content, the system must recognize completion and return to the parent context without executing a physical back navigation (the device never left the parent page)

---

**11. EVIDENCE PROVENANCE**

DETERMINISTIC_SIMULATION

- Executable: YES (plain `[Fact]`, always runs)
- Historical Failure: YES (infinite scroll from scroll-only dead-end detection; stuck child frames from unconsumed FrameCompleted)
- Reproducible: YES (deterministic)

---

**12. LEGACY MECHANISM CONTEXT**

Non-normative: `ContainerGatewayTests`, `StablePageAnalyzer`, `ScrollingScreenState`, `RecordingAction`, `FakeBrain`, `NullAdvisor`, `FrameCompleted`, `Pop-only`, `PressBack`, `content stability`, `K=3`, `gateway`, `MaxEmptyScrollRetries`.

---

**13. NORMALIZED BEHAVIORAL STATEMENT**

Given a scrollable page whose content never changes regardless of scroll actions, when the system observes 3 consecutive identical content states with no new elements discovered, it terminates the scroll loop and reports AllVisited rather than scrolling until its step budget is exhausted. Given a sub-page with no interactable content, when the system recognizes there is nothing to do, it removes the sub-page context and continues with the parent page — without executing a physical back navigation since the device never left the parent page.

---

### E-13 — GAP-P0-02

**Title:** Documented behavioral gaps: EntryPolicy fake success and ADB scroll failure misclassified as "reached end"

**Source Evidence:** `docs/prd/2026-07-29-local-implementation-gap-prd.md`, `src/UniClaw.Host/Device/AdbScreenStateProvider.cs:38`

---

**1. USER / TASK INTENT**

Not a test — this is documentation of behavioral gaps discovered during implementation review. The gaps describe real behaviors that produce false success signals.

---

**2. INITIAL EXTERNAL WORLD**

Android device/emulator. The system is asked to:
- Launch an app (cold launch) or bind to a currently running app
- Scroll through content to determine if end-of-list has been reached

---

**3. AVAILABLE OBSERVATION**

Gap 1 (EntryPolicy): The system reports entry success without any actual observation of the device state. It returns a string "Cold launched..." or "Sent deeplink..." without executing any device command or verifying the app is in the foreground.

Gap 2 (Scroll): `AdbScreenStateProvider` calls `uiautomator dump` synchronously on every scroll state query. When XML attributes (`scrollY`, `scrollYMax`) are missing → `maxScrollY=0` → judged `IsEnd=true`. When any exception occurs → swallowed → falls back to `(HasScroll=false, Progress=0, IsEnd=true)`. No CancellationToken, no timeout, no error diagnostics.

---

**4. ACTION / DECISION TAKEN**

Gap 1: EntryPolicy always returns fake success. Real actions (launch intent, deeplink URI, wait-for-stability) are not executed. The engine proceeds to traverse based on a false assumption that the correct app is in the foreground.

Gap 2: Scroll state queries report `IsEnd=true` whenever the underlying ADB command fails or returns incomplete data. The engine interprets this as "nothing more to scroll" and stops traversal early.

---

**5. WORLD TRANSITION**

Gap 1: Device state unchanged — app may not be launched, may be on wrong screen, may have crashed. Engine oblivious.

Gap 2: ADB scroll failure or missing XML attributes → engine believes end-of-list reached → stops scrolling → reports completion. Actual device may still have scrollable content.

---

**6. EVENT / DISTURBANCE**

- ADB disconnect / command failure
- Missing UI hierarchy attributes
- Cold launch failure (app not installed, permission denied, crash on launch)
- Deeplink failure (wrong URI, app not handling intent)
- AI provider 429/5xx/timeout (no retry, no backoff, no circuit breaker — documented as GAP-P1-02)

---

**7. OBSERVED OUTCOME**

Gap 1: Engine may traverse the wrong app, report AllVisited/TargetFound when the intended app was never launched, or fail mysteriously because expected elements don't exist on whatever screen is actually shown.

Gap 2: Scrollable content prematurely terminated. Engine reports AllVisited when unvisited content remains. ADB failure indistinguishable from genuine end-of-list.

---

**8. FAILURE OR SUCCESS CLAIM**

Legacy system claims SUCCESS — entry reports success, scroll termination reports end-of-list.

---

**9. CONTRADICTORY EVIDENCE**

**YES — documented gap between claimed success and actual device state.**

| Claimed | Actual |
|---|---|
| Entry success ("Cold launched...") | No device command executed; app state unknown |
| End of list reached (`IsEnd=true`) | ADB command failed OR XML attributes missing; scroll state unknown |
| Completion (AllVisited/TargetFound) | May be based on false entry state and premature scroll termination |

---

**10. EXPECTED EXTERNAL BEHAVIOR**

- EntryPolicy must execute real device operations and verify app foreground state before reporting success. Failure must carry diagnosable reasons.
- Scroll failure must be distinguishable from end-of-list. ADB command failure must produce an explicit error state, not silently fold into "reached end."
- AI provider failures (429/5xx/timeout) must use bounded retry with backoff. User cancellation must propagate immediately.

---

**11. EVIDENCE PROVENANCE**

DOCUMENT_ONLY (references production code at `AdbScreenStateProvider.cs:38`)

- Executable: NO (documentation claim; no dedicated test for these gaps)
- Historical Failure: YES (documented as GAP-P0-02 and GAP-P0-03)
- Reproducible: UNKNOWN (gaps are in production code path, not isolated in a test)

---

**12. LEGACY MECHANISM CONTEXT**

Non-normative: `EntryPolicyExecutor.ExecuteStrategy`, `ColdLaunch`, `DirectDeeplink`, `BindCurrentScreen`, `AdbScreenStateProvider`, `HasScroll`, `GetScrollProgress`, `IsEndOfList`, `uiautomator dump`, `scrollY`, `scrollYMax`, `IActionExecutor`, `IPageAnalyzer`.

---

**13. NORMALIZED BEHAVIORAL STATEMENT**

When asked to launch an application, the system reports success without executing any device command or verifying the app is visible — the engine may traverse content on whatever screen is actually displayed. When asked whether a scrollable list has more content, any failure of the underlying device query (ADB disconnect, missing attributes, exception) is silently treated as "end of list reached," making it impossible to distinguish "nothing more to scroll" from "scroll command failed." These gaps mean the system can report completion when the intended app was never launched or when unvisited scrollable content remains.

---

### E-14 — PlanCompiler

**Title:** Deterministic IntentSlots → TraversalPlan transformation

**Source Evidence:** `src/UniClaw.Core/Graph/Services/PlanCompiler.cs`, `src/UniClaw.Core/Graph/Models/TraversalPlan.cs`

---

**INTENT TRANSFORMATION BOUNDARY 1: IntentSlots → TraversalPlan**

**Input:** `IntentSlots` record with 9 fields:
- `TargetApp` (required, non-empty): the application package to explore
- `Scope` (required): `"full"` (exhaustive traversal) or `"target_only"` (find-and-stop)
- `Target` (string?): required when Scope=target_only; ignored when Scope=full
- `Depth` (int?): null = unconstrained; else resolved as `min(config.MaxDepth, Depth)` — "tighter wins"
- `ElementHandling` (string?): template set key — `full_interaction`, `menu_only`, `safe_mode`, `read_only`; null → `full_interaction`
- `Navigation` (string?): free string describing navigation mode (e.g., `"bounded_settings"`)
- `Restore` (bool?): whether to restore initial state after completion
- `Completion` (string?): override — `"max_steps"` or `"timeout"`; overrides Scope-derived CompletionPolicy
- `Entry` (string?): traversal root entry page; null = app root

**Transformation steps (deterministic, no AI):**
1. **ValidateSlots**: fail-fast on null/empty TargetApp, invalid Scope (not "full"/"target_only"), target_only without Target, invalid ElementHandling key, negative Depth, invalid Completion value
2. **BuildEntryPolicy**: always returns `EntryPolicy(Strategy: ColdLaunch, TimeoutSeconds: 10)` — never varies by slots
3. **BuildRootNode**: builds `TraversalNode` with NodeType.Screen, OperationType.NoAction, ChildrenStrategy.DynamicMatch. DynamicRules generated from TemplateSets[ElementHandling] — each template name maps to a MatchCondition (e.g., `menu_container` → `{type: menu_item}`) and MatchAction.GenerateChild
4. **BuildCompletionPolicy**: if Completion override set: `"max_steps"` → MaxSteps(500), `"timeout"` → Timeout(300s). Else derived from Scope: `"target_only"` → TargetFound(TargetName, Contains, ExecuteThenStop); `"full"` → Exhaustive
5. **Assemble**: returns `TraversalPlan` with EntryApp, EntryPolicy, PlanName, PlanId, RootNode, TemplateRegistry, CompletionPolicy, IntentSlots

**Output:** `TraversalPlan` with:
- `EntryApp`: the application package
- `EntryPolicy`: how to enter the app (always ColdLaunch, 10s timeout)
- `RootNode`: root of the traversal node tree (Container/Screen, NoAction, DynamicMatch children)
- `TemplateRegistry`: determines which element types are navigable (menu_container, switch_leaf, slider_leaf, leaf_action, leaf_info)
- `CompletionPolicy`: when to stop (Exhaustive, TargetFound, Timeout, MaxSteps)
- `IntentSlots`: original intent (preserved for reference)

**TemplateSets:**
- `full_interaction`: menu_container, switch_leaf, slider_leaf, leaf_action
- `menu_only`: menu_container
- `safe_mode`: menu_container, switch_leaf, slider_leaf, leaf_action (same as full_interaction)
- `read_only`: leaf_info (matches anything, produces non-interactive leaf)

**Defaults:**
- `DefaultCompletionTimeoutSeconds = 300`
- `DefaultCompletionMaxSteps = 500`
- `EntryTimeoutSeconds = 10`

**Constraints / Fail-fast validation:**
- TargetApp must be non-empty
- Scope must be exactly "full" or "target_only"
- Scope=target_only requires non-empty Target
- ElementHandling must be a TemplateSets key or null
- Depth if set must be ≥ 0
- Completion if set must be "max_steps" or "timeout"
- TimeoutSeconds if set must be in (0, 86400]
- MaxSteps if set must be in [1, 1000000]
- EntryPolicy TimeoutSeconds must be in (0, 300]
- RootNode (if provided) must be Screen or Container type with NoAction operation (C-4)

**Executable evidence:** Production code. Test coverage via `GraphTests` (only production instantiator) and `FailFastValidationBaselineTests` (validation rules).

**Dormant correctness issue (IP-16):** Documented but not yet applied — field misreads, vocabulary misalignment, default drift (timeout 60↔300s, DirectDeeplink↔ColdLaunch, target_path scope NONE↔TargetFound). The compiler exists but is "dormant in baseline."

---

### E-15 — IntentExtractor

**Title:** AI-driven natural language → ExtractedIntentSlots extraction

**Source Evidence:** `src/UniClaw.Core/UniBrain/IntentExtractor.cs`

---

**INTENT TRANSFORMATION BOUNDARY 2: Natural Language → ExtractedIntentSlots**

**Input:** Natural-language scenario description string + factual context (targetApp, target?, maxDepth?, entryPage?)

**AI model:** `IModelProvider` (typically DeepSeek flash) via `CompleteTextAsync` with `PromptTemplateRegistry.ExtractIntent` template.

**Output:** `ExtractedIntentSlots` record (AI-inferred fields only):
- `Scope` (required): `"full"` or `"target_only"`
- `ElementHandling` (string?): `"full_interaction"`, `"menu_only"`, `"safe_mode"`, `"read_only"`, or null
- `Navigation` (string?): free string (e.g., `"bounded_settings"`)
- `Restore` (bool?): whether to restore state
- `Completion` (string?): `"max_steps"`, `"timeout"`, or null

**Post-extraction validation (locked vocabularies):**
- Scope ∈ {"full", "target_only"} — unknown values throw
- ElementHandling ∈ {null, "full_interaction", "menu_only", "safe_mode", "read_only"} — unknown values throw
- Completion ∈ {null, "max_steps", "timeout"} — unknown values throw

**Merge with factuals:** AI-inferred fields are merged with caller-supplied factuals (TargetApp, Target, Depth, Entry) to produce a complete `IntentSlots` record for PlanCompiler.

**AI-vs-caller boundary:**
- AI infers: Scope, ElementHandling, Navigation, Restore, Completion (probabilistic)
- Caller supplies: TargetApp, Target, Depth, Entry (deterministic, factual)
- AI does NOT infer target name, app package, depth limit, or entry page

**Failure behavior:**
- Model call failure → throw with error message
- Empty AI response → throw with model/tokens/latency diagnostics
- Invalid JSON → throw (JsonException)
- Missing `scope` field → throw (InvalidOperationException)
- Unknown vocabulary values → throw (InvalidOperationException)
- JSON code fences (```json ... ```) → stripped before parsing

**No JSON schema enforcement:** DeepSeek v4-flash doesn't fully support `json_object` response format in some deployments, so the prompt textually enforces "Respond ONLY with a single JSON object" — no structured output guarantee.

**Executable evidence:** Production code. Test coverage via `IntentExtractorTests` (14 tests).

---

### E-16 — ScenarioPlanLoader

**Title:** Plan mode: hand-authored Static plan JSON bypasses AI intent extraction and PlanCompiler

**Source Evidence:** `src/UniClaw.Host/Runner/ScenarioPlanLoader.cs`

---

**INTENT TRANSFORMATION BOUNDARY 3: Hand-authored Plan JSON → Executable TraversalPlan (bypasses IntentExtractor + PlanCompiler)**

**Input:** Hand-authored plan JSON (e.g., `locate-one-item.v1.json`). Contains explicit target coordinates, static node definitions, expected page identities.

**Transformation:** `ScenarioPlanLoader.Load(planJson)`:
1. Deserialize JSON → `TraversalPlan` via `DomainJsonOptions.Default` round-trip
2. Materialize coordinates: `JsonElement` objects with `"x"` and `"y"` properties are converted to `Coordinate` objects (needed for `OperationDispatcher` which requires real `Coordinate`, not `JsonElement`)
3. Materialize each `StaticNode`: recursively convert coordinate targets in each node's operation

**Output:** `TraversalPlan` with:
- `ChildrenStrategyType.Static` (NOT DynamicMatch)
- `StaticNodes`: dictionary of hand-authored node definitions with explicit targets, operations, and expected page changes
- Explicit coordinates for tap targets (not AI-discovered)

**Key difference from PlanCompiler output:**
- PlanCompiler produces DynamicMatch (rule-based element discovery)
- ScenarioPlanLoader produces Static + StaticNodes (hand-authored coordinates)
- Plan mode = "data, not code" — the plan JSON fully specifies what to do
- Intent mode = DynamicMatch — the engine discovers what to do based on observation

**Executable evidence:** Production code. Exercised by `EmulatorScenarioIntegrationTests` in locate mode (uses `locate-one-item.v1.json`).

**Failure behavior:** Plan JSON coordinates may not match real screen → tap misses target → locate fails. Plan mode assumes the world matches the JSON.

---

### E-17 — ITraversalAdvisor

**Title:** Goal-directed dynamic next-action generation (stateless, AI-driven)

**Source Evidence:** `src/UniClaw.Core/UniBrain/ITraversalAdvisor.cs`, `TraversalAdvisor.cs`

---

**INTENT TRANSFORMATION BOUNDARY 4: Goal string + current context → Single next action**

**Input:**
- `goal` (string): a natural-language goal description
- `pageAnalysis` (PageAnalysis): current page observation serialized to JSON
- `currentNodeId` (string?): current position in the exploration plan
- `depth` (int?): current exploration depth

**AI model:** `IModelProvider` via `CompleteTextAsync` with `PromptTemplateRegistry.decide_next_action` template. Uses structured output schema (`Schemas.DecideNextAction`). MaxTokens: 1024. Capability tag: `DecideNextAction`.

**Output:** `ContextDecisionResult` with:
- `Result` (DecisionResult enum): parsed from model output string
- `Action` (string): the action to take
- `Target` (string?): action target
- `Params` (ImmutableDictionary<string, object>?): action parameters (supports String→string, Number→double, True/False→bool from JsonElement)
- `Reasoning` (string?): model's reasoning
- `Confidence` (double): model's confidence
- `SafetyVerified` (bool): default true

**Statefulness:** Stateless — `goal` is a per-call string parameter. No goal persistence across calls. Each call is independent — the model must re-derive context from the goal string and current observation.

**Failure behavior:**
- Missing prompt template → `DomainValidationException` fail-fast (no model call)
- Model call failure → `DomainValidationException` fail-fast
- Invalid JSON response → `DomainValidationException` fail-fast
- Unrecognized DecisionResult value → `DomainValidationException` fail-fast (exposes model drift)
- Never returns a partial/failure result — always throws on error

**Other methods (all throw `NotImplementedException`):**
- `InferContainerTypeAsync` — "pending future slice"
- `HandleExceptionAsync` — "pending future slice"
- `ScreenSafetyAsync` — "pending future slice"

**Executable evidence:** Production code. Test coverage via `TraversalAdvisorTests` (`DecideNextActionAsync_ModelFailure_ThrowsWithError`). Only test callers exist — no production engine caller (GAP-P1-01).

---

### E-18 — Python task_parser.py Gap

**Title:** Python NL task → IntentSlots parser with no C# equivalent

**Source Evidence:** `docs/refactor/2026-07-15-python-csharp-gap-triage.md` (C-5), `docs/prd/2026-07-29-local-implementation-gap-prd.md:246`

---

**INTENT TRANSFORMATION BOUNDARY 5: Python task_parser.py — NL task → IntentSlots (unimplemented in C#)**

**What exists (Python):** `src/ai/task_parser.py` — a subsystem that converts natural-language task descriptions into structured IntentSlots. Part of the Python AI stack (providers/UniBrain/prompts/cache/task parser). Described as "自然语言任务 → IntentSlots" (natural-language task → IntentSlots).

**What exists (C#):** `UnderstandTextAsync` method — implemented but has only test callers. No production consumer. No "NL → IntentSlots / PlanCompiler" main chain integration.

**Gap:** The Python reference implementation has a deterministic (or AI-assisted) NL→IntentSlots parser that the C# port never completed. The existing C# `IntentExtractor` (E-15) is a different mechanism — it uses AI model inference with a prompt template, not a deterministic parser. The `UnderstandTextAsync` method exists as a capability stub with no production wiring.

**Documented as:** C-5 gap in triage document. Phase 3+ blocked by Mode A/B decisions.

**Relevance:** This is evidence of an unimplemented intent construction path. The legacy system had two NL→Intent mechanisms in Python (task_parser.py for deterministic parsing, plus AI-driven extraction) but only one in C# (AI-driven IntentExtractor). The deterministic parsing path was never ported.

---

## Cross-Source Contradictions

### Contradiction 1 (E-07 — MultiBranchNavigationTests)

**Legacy claim:** `CompletionReason = AllVisited` — all content visited.
**Observed fact:** listB has 0/16 items visited. Second navigation button never tapped.
**Status:** Known unfixed bug. Test file explicitly documents TDD failures. Related OpenSpec change archived as complete but bug persists. No other evidence source contradicts this — the bug is consistently reproduced.

### Contradiction 2 (E-13 — GAP-P0-02)

**Legacy claim (EntryPolicy):** "Cold launched..." — entry success.
**Observed fact:** No device command executed. App foreground state unknown.
**Legacy claim (Scroll):** `IsEnd = true` — end of list reached.
**Observed fact:** ADB command failure or missing XML attributes. Scroll state unknown.
**Status:** Documented gaps in PRD. No executable test isolates these behaviors. Gap severity: P0 (entry) and P0 (scroll).

### Additional inconsistency (E-03 + E-06 — deleted simulation features)

Simulation contracts reference "jump detection" and "adaptive step" pipelines that comments say are deleted ("跳跃检测管线已删", "自适应步长管线已删"). The contracts still assert correct behavior for sparse/overlapping content, but the mechanisms that originally handled these cases have been replaced by seen-set differential termination. The contracts were recalibrated (2026-07-14) to match the new mechanism's output. This is not a contradiction per se — the contracts were updated — but it indicates simulation behavior changed without corresponding mechanism tests being preserved.

---

## Simulation Assumptions Detected

The following simulation behaviors directly supply semantic conclusions that would need to be derived from observation in a real system:

| Evidence | Assumption |
|---|---|
| E-03, E-05 | Element type classification (`button`, `switch`, `menu_item`, `readonly`) is provided by fixture — no vision inference |
| E-03, E-05, E-06, E-07 | Page identity is known from fixture — no page-recognition step |
| E-03, E-07 | Transition graph is declared in fixture — click→navigate is pre-programmed, not observed |
| E-06 | Scroll progress (0.0–1.0) and IsEndOfList are known exactly — no scroll state inference |
| E-06 | Element visibility after scroll is mathematically modeled — no real scroll uncertainty |
| E-06 | Items have known unique names ("Network_0") — no OCR errors, no deduplication ambiguity |
| E-04 | Error strategies, page analysis results, precondition results are injected by harness — not derived from observation |
| E-05 | AI intent extraction returns perfect canned JSON — no real AI uncertainty in stub tests |
| E-09 (L7) | Depth formula is tested at the algorithm level — not through observable behavior |
| E-12 | "Content stability" is detected by comparing normalized fingerprints — the simulation controls what the fingerprint is |

---

## Intent Transformation Boundaries Observed

| Boundary | Input | Output | Mechanism | Deterministic? | Evidence |
|---|---|---|---|---|---|
| NL → ExtractedIntentSlots | Natural-language description + factual context | Scope, ElementHandling, Navigation, Restore, Completion | AI model (DeepSeek flash) via prompt template | NO (AI-dependent) | E-15 |
| IntentSlots → TraversalPlan | IntentSlots (9 fields) | TraversalPlan with EntryPolicy, RootNode, CompletionPolicy, TemplateRegistry | PlanCompiler.Compile() — 5-step deterministic compiler | YES | E-14 |
| Plan JSON → TraversalPlan | Hand-authored plan JSON with explicit Static nodes | TraversalPlan with Static+StaticNodes | ScenarioPlanLoader.Load() — JSON deserialization + coordinate materialization | YES | E-16 |
| Goal string + context → Next action | Goal string, PageAnalysis, currentNodeId, depth | Single ContextDecisionResult (action, target, params, reasoning, confidence) | AI model via decide_next_action template | NO (AI-dependent, stateless) | E-17 |
| Python NL task → IntentSlots | NL task description | Structured IntentSlots | Python task_parser.py | UNKNOWN (unimplemented in C#) | E-18 |

**Notable:** Boundaries 2 and 3 produce the same output type (TraversalPlan) from different inputs (IntentSlots vs hand-authored JSON). Both feed the same execution engine. Boundary 4 (dynamic action) is a different paradigm — per-step decision rather than upfront plan construction. Boundary 5 is unimplemented in C#.

---

## Evidence Insufficient For Later Extraction

None — all 18 primary items provided sufficient behavioral facts for normalization. The weakest evidence is E-13 (DOCUMENT_ONLY, not executable) and E-18 (Python code, not in C# codebase), but both provide enough factual content for their role as gap documentation.

---

## Readiness

**NORMALIZED_EVIDENCE_READY_FOR_REALITY_DISTINCTION_EXTRACTION**

All 18 primary items normalized. Architecture-neutral behavioral statements produced. Legacy vocabulary confined to Legacy Mechanism Context sections. Simulation assumptions explicitly flagged. Cross-source contradictions documented. Intent transformation boundaries cataloged without architecture recommendations.

---

## Repository Changes

NONE
