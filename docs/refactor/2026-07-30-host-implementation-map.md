# UniClaw.Host — Implementation Map and Control Gaps

> Date: 2026-07-30
> Scope: `src/UniClaw.Host/` — the executable composition root and iterative scenario runner.
> Audience: anyone who needs to understand, extend, or regain control of the Host layer before finishing `deliver-safe-android-settings-test-loop` tasks 8 (safe enumeration) and 9 (stability drills).
> Method: static source inspection of all 9 Host files (~4200 lines). No `dotnet` execution.

This document is both a map (what exists and how it wires together) and a control-gap catalog (where the layer resists understanding and what to do about it). Every claim cites a `file:line` so it can be verified and updated as code moves.

---

## 1. Module Panorama

9 source files, one composition root, three commands. Dependencies point inward only: Host → Core / Device / ClaudeProvider; Core has no reverse reference (verified by `ArchitectureGuardTests`).

| File | Lines | Role |
|------|------:|------|
| `Program.cs` | 29 | Entry point; wires `HostApplication` + Ctrl+C → `CancellationTokenSource` |
| `Commands/HostCommands.cs` | 892 | **Overloaded** — exit codes, options, doctor, analyzer, composition factory, run-services record, mock provider, CLI application |
| `Runner/IncrementalScenarioRunner.cs` | 774 | The `locate_one_item` observe→plan→verify loop; bypasses `TraversalEngine` |
| `Artifacts/RunAssets.cs` | 835 | Run manifest, step evidence, issues.jsonl, result.json, redaction |
| `Safety/SafetyGate.cs` | 568 | Evaluator + `AsyncLocal` context + safe executors + composite sink |
| `Scenarios/ScenarioCatalog.cs` | 474 | Scenario + policy loading, normalization, hashing, validation |
| `Runner/ScenarioObservation.cs` | 293 | ADB observation source + UIAutomator rule parser (dual path) |
| `Runner/ScenarioPlanning.cs` | 173 | `LocateScenarioStepPlanner` |
| `Scenarios/ScenarioContracts.cs` | 140 | Sealed records: scenario, boundaries, policy, snapshot |

```
Program.Main
  └─ HostApplication.RunAsync            (CLI parse + exit-code dispatch)
       ├─ doctor  → HostCompositionFactory.CreateDoctor   → DeviceDoctor
       ├─ analyze → HostCompositionFactory.CreateAnalyzer → PageAnalysisDeviceAnalyzer
       └─ run    → HostCompositionFactory.RunScenarioAsync
                    ├─ ScenarioCatalog.LoadSnapshot
                    ├─ ScenarioPlanCompiler.Compile
                    ├─ RunAssetStore.CreateAsync
                    ├─ CreateRunServices            (wires all run deps)
                    ├─ AdbScenarioObservationSource
                    └─ IncrementalScenarioRunner.RunAsync
```

---

## 2. The Three Command Assembly Paths

### doctor — read-only readiness probe
`HostCompositionFactory.CreateDoctor` (`HostCommands.cs:388-396`) builds a minimal graph: `AdbCommandRunner` → `AdbScreenCapture` + `ProviderReady` flag → `DeviceDoctor`. `DeviceDoctor.InspectAsync` (`:115-161`) runs 6 checks: device state, boot completed, screenshot non-empty, UIAutomator dump contains `<hierarchy`, provider configured, output root writable. Emits a `DoctorReport`; exit code is `Success` only if all checks are `ready`. **No device mutation.**

### analyze — single-shot read-only observation
`CreateAnalyzer` (`:398-413`) builds `PageAnalyzer(provider, PromptLibrary, AdbScreenCapture)` + `InMemoryTraceRecorder(FileTraceStorage)`. `PageAnalysisDeviceAnalyzer.AnalyzeAsync` (`:287-372`) captures one `AnalyzeCurrentPageAsync`, records an AI-call span, writes `<runId>.analysis.json`, and guarantees `DeviceActionsSent = 0`. **No device action sent.**

### run — the full iterative loop
`RunScenarioAsync` (`:465-516`) is the composition spine. It chains: catalog → snapshot → plan compiler → asset store → `CreateRunServices` → observation source → `IncrementalScenarioRunner`. This is where control is hardest to hold: 6 construction steps in one method, each returning an object the next needs.

---

## 3. The `run` Data Flow (end to end)

Traced with file:line so the loop can be followed without re-reading every file:

1. **Load** `ScenarioCatalog.LoadSnapshot` (`:475`) → `ScenarioSnapshot` (scenario + policy + hashes + normalized JSON). Validated, fail-fast (`ScenarioContracts.cs:114-140`).
2. **Compile** `ScenarioPlanCompiler.Compile` (`:477`) → `TraversalPlan` (Graph contracts, persisted to assets).
3. **Assets** `RunAssetStore.CreateAsync` (`:478-499`) → `RunAssetSession` with isolated run dir, manifest, snapshot, compiled plan, trace dir. Redaction seeded with `ANTHROPIC_API_KEY` + SenseNova key.
4. **Services** `CreateRunServices` (`:415-463`) wires 11 deps into `HostRunServices`: ADB runner, `PageAnalyzer`, `SafeActionExecutor` (wraps `AdbActionExecutor`), `AdbScreenStateProvider`, `SafeEntryActionDriver`, `SafetyExecutionContext`, evaluator, composite sink, journal, trace recorder, assets.
5. **Observe** `AdbScenarioObservationSource` (`:501-509`) — dual path selected by `providerId == "mock"`: mock → `UiAutomatorPageAnalysis.Parse` (XML rules), else → `IPageAnalyzer` (AI vision).
6. **Run** `IncrementalScenarioRunner.RunAsync` (`IncrementalScenarioRunner.cs:64-496`) — the loop below.

Loop body (one step): `ObserveAsync` → `ValidateBoundary` → `BeginStepAsync` → write before/analysis → `planner.Plan` → if `complete` finish success; if null finish incomplete; else re-check fingerprint (stale-plan rejection) → `BuildSafetyCandidate` → `SafetyContext.Push` → `ExecuteAsync` (Tap/Swipe) → `SafetyJournal.GetLatest` → if denied finish blocked; if not executed finish failure; else `ObserveAsync` again → `Verify` → write verification → if click+success finish success; if scroll + end-of-list + unchanged finish incomplete(target_absent); loop.

**Termination vocabulary** (`:150-446`): `success` / `incomplete` (step_budget_exhausted, duration_budget_exhausted, target_absent_at_verified_end) / `failure` (stale_plan, action_failed, verification_mismatch, runtime) / `blocked` (safety rule id) / `cancelled`.

---

## 4. Safety Gate Architecture

The gate is a decorator chain + an implicit context flow. Understanding the flow is essential because it is the single point that prevents any action from reaching ADB unvetted.

- **Candidate** `SafetyCandidate` (`SafetyGate.cs:10-26`) — 15 fields: action, target, semantic, page identity/path, package, confidence, coordinates-trusted, is-preparation, depth, remaining steps/scrolls, run/step/fingerprint, source.
- **Context** `SafetyExecutionContext` (`:247-267`) holds the candidate in `AsyncLocal<SafetyCandidate?>`. The runner pushes with `using (_services.SafetyContext.Push(candidate))` (`IncrementalScenarioRunner.cs:252`), the executor reads `Current` inside (`SafetyGate.cs:348`), and the scope restores on dispose. **Implicit flow — see vulnerability 4.**
- **Evaluator** `SettingsSafetyEvaluator.Evaluate` (`:94-171`) — fixed precedence (deny overrides allow, default deny):
  1. boundary: step budget / scroll budget / depth / package / page
  2. dangerous: semantic / text
  3. allowlist: action present in *both* scenario and policy allowlists
  4. click trust: target + semantic + coordinates + confidence ≥ threshold
  5. allow rules: preparation launch, back, scroll, safe-navigation-row semantic
  6. `deny.default` fallback
- **Sink** `CompositeSafetyDecisionSink` (`:555-568`) fans every decision to: `RunAssetSafetyDecisionSink` (persists to run assets), `TraceSafetyDecisionSink` (trace span), `SafetyDecisionJournal` (in-memory latest-per-step lookup). The journal is what the runner reads back via `GetLatest(runId, step)` (`:497-505`, `IncrementalScenarioRunner.cs:258`).
- **Decorators** `SafeActionExecutor` (`:269-371`) wraps `IActionExecutor`; `SafeEntryActionDriver` (`:373-453`) wraps entry actions. Both call `DecideAsync` before delegating. **A denied action returns `false`/no-op rather than throwing** — the runner reads the journal decision, not an exception, to classify `blocked`.

**Guidance — control principle to preserve:** the evaluator is pure and precedence-ordered; the decorators are the only seam. Any new action path (popup handler, recovery) must go through the same decorator — never call `AdbActionExecutor` directly. The `CompositeSafetyDecisionSink` is the right place to add new observers (e.g., live console) without touching the evaluator.

---

## 5. Observation Source — Dual-Path Contract

`AdbScenarioObservationSource.ObserveAsync` (`ScenarioObservation.cs:67-104`) produces a `ScenarioObservation` from two sources:

- **UIAutomator rule path** (`:86-87`, `UiAutomatorPageAnalysis.Parse` at `:146-187`): parses the hierarchy XML, extracts clickable nodes, maps to `MenuItem` (toggle vs. menu_item by class name), derives page identity from title resource suffixes (`homepage_title`, `collapsing_toolbar`, `toolbar_title`, `action_bar`), and pulls `HasScroll`/`IsEndOfList` from `AdbScreenStateResult`.
- **AI path** (`:88-91`): `IPageAnalyzer.AnalyzeCurrentPageAsync` over the screenshot; `PageIdentity` from `analysis.CurrentPath.LastOrDefault()` with UIAutomator fallback.

Selected by `providerId == "mock"` (`HostCommands.cs:506-509`). Both paths return a `PageAnalysis`, but **there is no shared contract asserting the two produce compatible `Items`/`CurrentPath`/`HasScroll`/`IsEndOfList` semantics.**

---

## 6. Control-Gap Catalog (with evidence + guidance)

These are the concrete places where the layer resists understanding. Each item: symptom → evidence → risk → recommended direction.

### 6.1 `HostCommands.cs` is six modules in a trench coat
- **Symptom:** one 892-line file holds exit codes, options, `DeviceDoctor`, `PageAnalysisDeviceAnalyzer`, `HostCompositionFactory`, `HostRunServices`, `DeterministicSettingsModelProvider`, `HostApplication`.
- **Risk:** any change forces scrolling 892 lines; types that should be separable (CLI parsing vs. composition vs. mock provider) drift together; tests must reference one giant namespace.
- **Guidance:** split along existing fault lines — keep `HostCommands.cs` for the CLI app + options only; move `HostCompositionFactory` + `HostRunServices` to `Composition/HostComposition.cs`; move `DeviceDoctor`/`PageAnalysisDeviceAnalyzer` to `Commands/DoctorCommand.cs` + `AnalyzeCommand.cs`; move `DeterministicSettingsModelProvider` to `Scenarios/`. Do this *during* task 8 work, not as a separate prep change — refactor the files you're already touching.

### 6.2 `IncrementalScenarioRunner` is hard-wired to `locate_one_item`
- **Symptom:** constructor throws if `snapshot.Scenario.Mode != "locate_one_item"` (`IncrementalScenarioRunner.cs:56-61`). Task 8 needs `enumerate_first_level`.
- **Risk:** task 8 cannot start without either generalizing this runner or adding a parallel one; the `Verify()` logic (`:601-653`) is locate-specific (target alias matching), so a naive generalization will entangle two success criteria.
- **Guidance:** do **not** add mode-branching inside `IncrementalScenarioRunner`. Extract an `IScenarioRunner` strategy with two implementations — `LocateScenarioRunner` (current logic) and `EnumerateScenarioRunner` (new). Share `ResetAsync`, `ValidateBoundary`, `RecordIssueAsync`, `FinishAsync`, `Normalize` via a `ScenarioRunnerBase`. The enumerate success criterion is "verified end-of-list + all first-level entries accounted (visited or dangerous-skipped)", which is fundamentally different from locate's "target page identity matched" — keep the `Verify` per-strategy. This is the structural prerequisite for task 8.

### 6.3 Observation dual-path has no equivalence contract
- **Symptom:** UIAutomator rule parse and AI `PageAnalysis` both flow into the same `ScenarioObservation` with no assertion they agree on item shape, scroll fields, or page identity (`ScenarioObservation.cs:86-91`).
- **Risk:** a test using the mock/UIAutomator path can pass while the AI path silently produces different `Items`/`IsEndOfList`, so emulator smoke tests (deterministic provider) don't catch AI-path regressions. This is the deepest hidden control gap — the two paths look interchangeable but aren't proven so.
- **Guidance:** add a contract test that runs both paths over the same fixture XML + screenshot and asserts structural agreement on `CurrentPath`, `HasScroll`, `IsEndOfList`, and `Items` (text + coordinate + type). Where they legitimately differ (AI sees scroll affordances UIAutomator misses), make the difference explicit and decide which path is authoritative per field. Until this contract exists, "mock green" does not imply "AI green."

### 6.4 Safety context flows through `AsyncLocal` implicitly
- **Symptom:** `SafetyExecutionContext` stores the candidate in `AsyncLocal<SafetyCandidate?>` (`SafetyGate.cs:247-259`); the runner pushes, the executor pops via `Current`.
- **Risk:** implicit flow is hard to trace in a debugger and in logs; a runner bug that forgets `Push` (or pushes the wrong candidate) silently falls back to the `unscoped` synthetic candidate (`:350-366`), which denies by default — a *safe* failure mode, but one that hides the real candidate and produces a misleading `deny.default` in the journal. Cross-run contamination is bounded by `AsyncLocal` reset on scope dispose, but only if every `Push` is paired with a `Dispose`.
- **Guidance:** acceptable as-is because the fallback is deny-by-default (safe), but add a correlation field to the `SafetyCandidate.Source` / decision trace recording whether the candidate was `scoped` or `unscoped` fallback, so journal entries from a forgotten `Push` are diagnosable. Do **not** switch to explicit passing — that would force the candidate through the executor's public `IActionExecutor` interface, which Core/Device also implement and which must stay safety-agnostic.

### 6.5 `LooksLikeVisualTransition` is a byte-length heuristic in a verification chain
- **Symptom:** when click target-identity matching fails, the runner calls `LooksLikeVisualTransition` (`IncrementalScenarioRunner.cs:635-636,655-671`), which returns `true` if `|len(before) - len(after)| ≥ 20% of max(len)`.
- **Risk:** screenshot byte size is dominated by compression noise and content density; a 20% length delta is neither necessary nor sufficient for a real page transition. This heuristic can declare `success` on a page that didn't change, or miss a real transition with similar byte size. It is technical debt disguised as acceptance logic, and it sits on the `success` exit path.
- **Guidance:** remove this heuristic from the success path in the enumerate runner from the start. Replace with `verify_page_type` (G1 slice) once available; until then, fall back to strict `target_page_identity_verified` only and let mismatches route to `failure`/`incomplete` honestly. An honest `incomplete` is more valuable for the task-9 stability drills than a false `success`. **Do not carry this heuristic into task 8.**

### 6.6 Two traversal paths coexist without a documented relationship
- **Symptom:** `HostRunServices.CreateTraversalEngine` (`HostCommands.cs:655-666`) builds a real `TraversalEngine`, but `IncrementalScenarioRunner` never calls it — the runner implements its own observe→plan→verify loop against `IPageAnalyzer` + `IActionExecutor` directly. Both paths consume the same `HostRunServices`.
- **Risk:** a reader assumes the runner uses `TraversalEngine`/`TraversalFSM`; it does not. Changes to the FSM (G4 Phase 3 hardening) would have no effect on the path actually driving the device. This is the single biggest reason the layer feels out of control: the "engine" exists but isn't on the critical path.
- **Guidance:** this is a decision point, not a bug. Document the relationship explicitly in this map (done here) and in `docs/system/layers/` as part of `docs-spec-sync-current-state`. Then decide, before task 8, whether the enumerate runner should: (a) stay self-contained (current) — simplest, but Phase 3 hardening won't reach it; (b) delegate the step loop to `TraversalEngine` — unifies the paths but requires the engine to accept the runner's observe/verify contracts; (c) converge later. **Recommend (a) for task 8** to avoid scope creep, with an explicit `// Does not use TraversalEngine; see Host design doc §6.6` comment at `HostCommands.cs:655` so the next reader isn't misled.

---

## 7. Relationship to Active Change Tasks 8 / 9

Mapping each control gap to where it bites the remaining work:

| Task | Blocked/affected by | What to do first |
|------|---------------------|------------------|
| 8.1 (discovery + dedup + scroll + end-of-list) | 6.2 (no enumerate runner), 6.3 (scroll field equivalence), G2 (screen-state bridge) | Build `EnumerateScenarioRunner` (6.2 guidance); add observation contract test (6.3); if UIAutomator scroll is insufficient, add the bridge (calibrated gaps G2) |
| 8.2 (safe entry sampling: enter→capture→back→verify home) | 6.5 (visual-transition heuristic would falsely "verify"), 6.6 (verify path) | Strict identity verification only (6.5 guidance); verify-home uses `ResetProcedure.ExpectedPageIdentity` matching already in `ResetAsync` |
| 8.3 (dangerous skip accounting) | G1 `screen-safety` (no AI safety partner for static gate) | Static `DangerousSemantics`/`DangerousText` suffices for Settings first-level; prove denied targets never reach the executor via the decorator (§4) — this is already guaranteed by construction, add a test asserting zero executor calls after denial |
| 8.4 (fake/mock E2E tests) | 6.3 (mock path ≠ AI path) | Contract test (6.3) must land before these E2E tests are trusted to represent the AI path |
| 8.5-8.7 (emulator + real-provider iterations) | 6.5 (false success), 6.1 (hard to edit) | Refactor 6.1 opportunistically while fixing issues each iteration |
| 9.x (stability drills, repeat) | 6.5 (false success breaks 10/10 gate), termination honesty | The 10/10 gate is only meaningful once 6.5 is gone — a false `success` passes the gate dishonestly |

**The single highest-leverage action:** remove vulnerability 6.5 (`LooksLikeVisualTransition`) from the success path before task 8.5's first emulator iteration. Without that, the stability drills in task 9 measure a heuristic, not real traversal.

---

## 8. Summary: What to Do, In Order

1. **`docs-spec-sync-current-state`** — include the §6.6 relationship note in `docs/system/layers/`; supersede the stale gaps doc with its calibrated version. (gaps doc G6)
2. **Remove `LooksLikeVisualTransition` from the success path** — strict identity-only verification. (§6.5) — unblocks honest task-9 gates.
3. **Extract `IScenarioRunner` + `EnumerateScenarioRunner`** sharing a base. (§6.2) — unblocks task 8 structurally.
4. **Add the observation dual-path contract test.** (§6.3) — makes mock-path green meaningful for the AI path.
5. **Land `pageanalysis-screenstate-bridge` if emulator scroll metadata is insufficient.** (gaps doc G2) — unblocks task 8.1 end-of-list accounting.
6. **Refactor `HostCommands.cs` opportunistically** during the above. (§6.1) — do not freeze the layer for a pure refactor.
7. **Decide the `TraversalEngine` relationship explicitly** (§6.6) and record it — even if the decision is "runner stays self-contained for task 8."
8. **G1 `screen-safety` then `verify-page-type`** — after task 8, to replace the static gate's limits and the verification heuristics respectively.

The throughline: the Host layer is functional but was built as a single vertical slice for `locate_one_item`. Regaining control means **separating the locate-specific logic from the scenario-runner skeleton** (6.2) and **making the observation paths prove equivalence** (6.3) before trusting either to the enumerate frontier.