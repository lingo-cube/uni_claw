# Current Internal Gaps — Calibrated

> Date: 2026-07-30
> Calibrates: `docs/refactor/2026-07-29-current-internal-gaps.md` (preserved as historical snapshot).
> Method: static verification only — MCP/Read symbol lookup + source inspection. No `dotnet` execution (host lacks `dotnet` on PATH; per the original doc's own Verification Note).
> OpenSpec status: one active change exists — `deliver-safe-android-settings-test-loop` (≈80% complete). This contradicts the original doc's premise.

## Why this calibration exists

The 2026-07-29 gaps doc opened with "no active OpenSpec change exists" and treated G2/G3 as unbuilt gaps. That premise is stale. The active change `deliver-safe-android-settings-test-loop` has already built the Host composition root (G3), the safety gate, run assets, the locate-one-item loop, and ADB screen-state access. Its remaining tasks 8 (safe first-level enumeration) and 9 (stability drills) are the real frontier. This document re-maps every G1-G6 item against the verified repository state, marks what is already covered, what still holds, and what the doc got wrong, then rewrites the recommended order.

## Verification legend

- ✅ Still real — code confirms the gap as described.
- ⚠️ Stale / re-shaped — the gap exists but the doc's description no longer matches.
- ❌ Covered — already built by the active change; remove from inventory.

---

## G1: UniBrain Capability Completion — ✅ Still real

Static verification confirms the doc exactly. All stubs remain `NotImplementedException`:

- `PageAnalyzer.FindAppEntryAsync` — `src/UniClaw.Core/UniBrain/PageAnalyzer.cs:96`
- `PageAnalyzer.VerifyPageTypeAsync` — `src/UniClaw.Core/UniBrain/PageAnalyzer.cs:104`
- `TraversalAdvisor.InferContainerTypeAsync` — `src/UniClaw.Core/UniBrain/TraversalAdvisor.cs:111`
- `TraversalAdvisor.HandleExceptionAsync` — `src/UniClaw.Core/UniBrain/TraversalAdvisor.cs:118`
- `TraversalAdvisor.ScreenSafetyAsync` — `src/UniClaw.Core/UniBrain/TraversalAdvisor.cs:125`
- `DeepSeekModelProvider.CompleteVisionAsync` / `CompleteMultimodalAsync` — `src/UniClaw.DeepSeekProvider/DeepSeekModelProvider.cs:105,109`
- `MockModelProvider` vision/multimodal — `src/UniClaw.Core/Simulation/MockModelProvider.cs:59,64`

Coverage by active change: none. `deliver-safe-android-settings-test-loop` only consumes `AnalyzeCurrentPageAsync` (via `PageAnalyzer`) and `DecideNextActionAsync` (via `TraversalAdvisor`). The five unimplemented capabilities are untouched.

**Guidance — recommended changes (unchanged, ordered by traversal need):**
1. `unibrain-screen-safety` — needed before the safe-enumeration runner can delegate dangerous-item screening to the AI layer instead of the static `DangerousSemantics`/`DangerousText` policy lists.
2. `unibrain-verify-page-type` — needed to verify the runner landed on the *type* of page it expected, not just a fuzzy page-identity string.
3. `unibrain-find-app-entry` — needed for non-Settings targets where cold launch + wait is insufficient.
4. `unibrain-exception-advisor` — needed for task 9 failure drills where the runner must classify runtime exceptions.
5. `mockmodelprovider-vision-fixtures` — unblocks fixture-driven testing of vision/multimodal slices without real SDK cost. Build this *first* if any of 1-4 needs replay-based tests.

**Sequencing rationale:** screen-safety leads because the active change's static safety gate (see Host doc §4) is a stopgap; the runner's task-8 "discovered-but-skipped dangerous entry" accounting currently has no AI-side safety partner. verify-page-type is second because the runner's `Verify()` (Host doc §6, vulnerability 5) leans on fragile string/byte heuristics that page-type verification would replace.

---

## G2: Mode A Scroll-State Bridge — ✅ Still real, partially touched

Static verification confirms the seam. `TraversalEngine` consumes `IUniBrain` and `IScreenStateProvider` as **independent** constructor deps (`src/UniClaw.Core/Traversal/TraversalEngine.cs:26-27,61-78`). No internal bridge caches `PageAnalysis.HasScroll` / `PageAnalysis.IsEndOfList` into the screen-state provider after each `AnalyzeCurrentPageAsync` call.

Coverage by active change: **partial, not complete.** Task 3.4 migrated UIAutomator screen-state access to the unified ADB runner and preserves distinct results for ADB failure / XML parse failure / true no-scroll / verified end-of-list. Task 8.1 will need end-of-list accounting. But the doc's suggested seam — a `PageAnalysisAwareScreenStateProvider` that the *AI* analysis feeds — is **not** implemented. The `AdbScenarioObservationSource` currently picks one of two paths per observation: UIAutomator rule parse *or* `IPageAnalyzer` AI call (`src/UniClaw.Host/Runner/ScenarioObservation.cs:86-91`), and the AI path's scroll fields are not bridged back into screen state.

**Guidance:** `pageanalysis-screenstate-bridge` remains the right change. Construct it as a decorator over `IScreenStateProvider` that holds the latest `PageAnalysis` and exposes its `HasScroll`/`IsEndOfList` when the underlying UIAutomator metadata is ambiguous. Keep it inside the Host composition (not in Core) so `IScreenStateProvider` stays platform state, not AI-service responsibility — consistent with D-126. The bridge unblocks task 8.1's "verified end-of-list accounting" and lets the runner trust AI-derived scroll fields on apps where UIAutomator scroll metadata is unreliable.

---

## G3: First Device Composition Root — ❌ Covered (remove from inventory)

The doc claimed "no executable host or integration composition root." This is no longer true. Verified against the active change:

- Task 6.1: `HostCompositionFactory.CreateRunServices` wires PageAnalyzer + wrapped `SafeActionExecutor` + `AdbScreenStateProvider` + traversal services + trace recorder + run assets (`src/UniClaw.Host/Commands/HostCommands.cs:415-463`).
- Task 3.x: `AdbScreenCapture` / `AdbActionExecutor` / `AdbScreenStateProvider` all exist and run through the unified `AdbCommandRunner`.
- Task 6.2-6.5: `doctor --device`, `analyze --device`, classified exit codes, Ctrl+C cancellation, trace closure all implemented (`src/UniClaw.Host/Commands/HostCommands.cs:95-260,262-382,734-892`).

What remains is **not** a composition-root gap. It is the active change's own tasks 8 (enumerate runner) and 9 (stability drills). The composition root is in place; it just hasn't been exercised end-to-end for the enumerate mode.

**Guidance:** drop `first-device-traversal-integration` as a separate change. The work is finishing tasks 8/9 under the existing change. The Host design doc (companion file) covers the composition root in detail and identifies the concrete blockers for task 8 — most notably that `IncrementalScenarioRunner` hard-rejects anything but `locate_one_item` (`src/UniClaw.Host/Runner/IncrementalScenarioRunner.cs:56-61`).

---

## G4: Traversal Phase 3 Behavior — ✅ Still real

Static verification confirms. `HandlePreconditionCheckAsync` still assumes pass and records `precondition_assume_pass` (`src/UniClaw.Core/StateMachine/TraversalFSM.cs:164,170`). No `ITraversalNode.Precondition` extension exists. Toggle-after-restore and dangerous-button detection remain unimplemented. Outside the active change's scope.

**Guidance:** the three suggested changes stand but should be **deferred** until tasks 8/9 close. Rationale: Phase 3 behavior is exercised by real-device traversal; there is no point hardening preconditions or dangerous-action screening for the `TraversalEngine` path while the runner actually driving the device (the `IncrementalScenarioRunner`) bypasses `TraversalFSM` entirely and implements its own observe→plan→verify loop (see Host doc §3, vulnerability 6). Sequencing: finish the runner's enumerate path first, *then* decide whether Phase 3 hardening targets the FSM path, the runner path, or unifies both.

---

## G5: Scroll Metrics And Advanced Baselines — ⚠️ Stale, re-shaped

The doc claimed `JumpDetected` / `JumpRecovered` / `AdaptiveStepIncreases` are "placeholders returning zero." Static verification shows the reality shifted:

- `NumericAnchor.cs` records a **C-11 schema change that removed** these three fields: `src/UniClaw.Core/Simulation/ExpectedBehavior/NumericAnchor.cs:6` — "移除 JumpDetected/JumpRecovered/AdaptiveStepIncreases".
- The source tree has **no** `src/UniClaw.Core/StateMachine/Scroll/` directory. The `ScrollStatisticsCollector` and `JumpDetector` symbols the search surfaced live only in stale `bin/Release/net9.0/*.xml` build artifacts from a previous compilation — not in current source.
- `DefaultScreenStateProvider.GetScrollProgress() => 0.0` (`src/UniClaw.Core/Traversal/DefaultScreenStateProvider.cs:14`) is a default-implementation placeholder, not a metrics placeholder.

So the gap is not "placeholders return zero." It is: **the advanced scroll metrics were removed (C-11) and the question of whether to reintroduce them is open.** The archived baseline tasks remain unchecked because the work was *deleted*, not deferred.

**Guidance:** rewrite this gap as an open decision, not an implementation task. Before proposing `advanced-scroll-metrics`, decide: (a) does the runner's observe→verify loop (which already detects page-fingerprint change after scroll) subsume what the removed metrics measured? (b) if jump-detection is still needed for adaptive step sizing, where should it live now that the `StateMachine/Scroll` module is gone? Recommend deferring this decision until task 8's scroll-progress accounting is in place — its needs will tell you whether C-11's removal left a real hole.

---

## G6: Documentation And Spec Drift — ✅ Still real (self-confirmed)

All three drift items verified against source:

1. `docs/system/README.md:90` — still says "TraversalFSM 6/8 handler 是 stub." Reality: all 8 handlers have real implementations; the dispatch table at `src/UniClaw.Core/StateMachine/TraversalFSM.cs:143-151` routes all 8 `TraversalState` values to real `Handle*Async` methods (`:164,174,266,394,456` + `HandleNodeSelectAsync`/`HandleBranchAsync`/`HandleFrameCompleteAsync`). Stale.
2. `openspec/specs/traversal-advisor/spec.md:118-120` — still says `TraversalAdvisor` consumes `IModelRouter`. Reality: code injects `IModelProvider` (D-8). Stale.
3. `tests/UniClaw.Core.Tests/Baseline/MultiBranchNavigationTests.cs:15,36,151,202,249` — comments still describe "BUG" / "当前行为 (BUG)" while `openspec/changes/archive/2026-07-14-navigation-subpage-frames/` is archived as complete. Stale comments vs. archived resolution.

**Guidance:** `docs-spec-sync-current-state` is the right change and should run **first**, before new work depends on any of these documents. It must include this gaps doc itself in its sync scope (the original premise was stale). Per the original doc's Verification Note, the `MultiBranchNavigationTests` comment edits need a `.NET`-enabled host to confirm assertion behavior before rewriting comments — keep that caveat, don't edit test comments blind.

---

## Not Counted As Active Gaps (calibrated)

The original "not counted" list holds and needs no change: archived deferred checkboxes later covered by other changes, old `docs/refactor/` checklists whose OpenSpec changes are archived/implemented, and test-only fake/spy providers throwing `NotImplementedException` outside their scenario. Add one item: the stale `bin/Release` build artifacts that surfaced fake `ScrollStatisticsCollector` symbols (G5) — these are build output, not source, and should be cleared by a clean rebuild rather than treated as code.

---

## Recommended Order (calibrated)

The original order assumed G3 was an unbuilt gap. Since the composition root is built and the active change owns the frontier, the order collapses and refocuses on finishing what is open:

1. **`docs-spec-sync-current-state`** — fix the stale `README`/`traversal-advisor` spec/`MultiBranchNavigationTests` comments *and* supersede the 2026-07-29 gaps doc with this calibration. Do this first so no new work depends on misleading docs.
2. **Finish `deliver-safe-android-settings-test-loop` tasks 8 → 9** — the real frontier. This replaces the original "G3 first-device-integration" line item entirely. The Host design doc identifies the concrete task-8 blockers (enumerate runner missing, fragile visual-transition heuristic, observation dual-path contract).
3. **`pageanalysis-screenstate-bridge`** — needed by task 8.1's verified end-of-list accounting; slot it as the first sub-step of task 8 if the UIAutomator scroll metadata proves insufficient on the emulator fixture.
4. **G1 UniBrain slices** in the order above — `screen-safety` first (it partners with task 8.3's dangerous-entry skip accounting), then `verify-page-type` (it replaces the runner's fragile verification heuristics), then the rest.
5. **G4 Phase 3 behavior** — only after the runner/FSM path relationship (Host doc §6, vulnerability 6) is decided; don't harden an FSM path the runner bypasses.
6. **G5 advanced scroll metrics** — reframe as an open decision after task 8, not a task itself.

The net effect: the inventory shrinks from six gaps to four real ones (G1, G2, G4, G6), with G3 removed and G5 recharacterized, and the execution path becomes "finish the active change, then the G1/G2 supporting slices, then deferred Phase 3."

---

## Verification Note (unchanged + amendment)

The original note holds: `dotnet` is not on `PATH` on this host, so test-behavior-dependent items (G6's `MultiBranchNavigationTests` comment edits) cannot be verified here and must wait for a `.NET`-enabled environment. Amendment: the G5 finding relies on *source-tree* inspection (the `bin/Release` artifacts are not evidence of current source), so it does not need `dotnet` to confirm — a clean rebuild clearing the stale symbols is recommended but not required for this calibration.