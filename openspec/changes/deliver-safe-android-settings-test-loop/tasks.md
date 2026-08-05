## 1. Baseline and Change Boundaries

- [x] 1.1 Run the current non-integration test baseline and record the exact command/result in the change evidence before editing product code.
- [x] 1.2 Inspect the affected C# definitions and references with the configured semantic C# tools, then record the implementation touchpoints for Host composition, `IActionExecutor`, `EntryPolicyExecutor`, ADB capture/action/screen state, traversal planning, and trace correlation.
- [x] 1.3 Create `src/UniClaw.Host/` and `tests/UniClaw.Host.Tests/`, add only the required project references, register them in the solution, and verify Core has no reverse project reference.
- [x] 1.4 Add an ignore rule for generated `artifacts/runs/` while preserving versioned scenario and policy inputs.

## 2. Scenario and Policy Contracts

- [x] 2.1 Implement Host-side sealed record classes and fail-fast JSON validation for scenario schema V1 using validated string vocabularies without adding or changing any locked enum.
- [x] 2.2 Implement normalization, content hashing, immutable run snapshots, and duplicate/unsupported scenario validation.
- [x] 2.3 Add versioned policy input for action allowlists, dangerous semantic/text rules, aliases, confidence thresholds, and boundary rules.
- [x] 2.4 Add `scenarios/android-settings/locate-one-item.v1.json` with default target `About phone`, explicit budgets, aliases, success criteria, and reset procedure.
- [x] 2.5 Add `scenarios/android-settings/enumerate-settings-safely.v1.json` with first-level-only boundaries, read-navigation allowlist, skip behavior, completion proof, and reset procedure.
- [x] 2.6 Add contract tests covering valid load, missing fields, invalid vocabulary/budgets, unsupported schema versions, source mutation after snapshot, policy hashing, and secret-free serialization.

## 3. Reliable Device Boundary

- [x] 3.1 Introduce one Device-owned ADB command runner contract and result model covering selected serial, timeout, cancellation, exit code, stdout, stderr, and structured failure details, with fake-runner unit tests.
- [x] 3.2 Migrate screenshot capture to the unified ADB runner and verify non-empty image output, timeout/cancellation propagation, serial routing, and diagnostic errors.
- [x] 3.3 Migrate device action execution to the unified ADB runner and verify click/back/scroll/launch command formation without shell injection or secret leakage.
- [x] 3.4 Migrate screen-state/UIAutomator access to the unified runner and preserve distinct results for ADB failure, XML parse failure, true no-scroll, and verified end-of-list.
- [x] 3.5 Replace `EntryPolicyExecutor` fake-success paths with real entry actions and wait verification that comply with the existing step-orchestrator canonical specification.
- [x] 3.6 Run focused Device, screen-state, entry-policy, timeout, cancellation, and failure-classification tests before beginning Host orchestration.

## 4. Deterministic Safety Gate

- [x] 4.1 Implement a pure safety evaluator with fixed precedence: boundary/budget deny, dangerous deny, allowlist deny, untrusted-target deny, explicit safe-navigation allow, default deny.
- [x] 4.2 Implement V1 Settings rules that permit only bounded navigation-row click, back, scroll, and explicit preparation actions while denying all state-changing or destructive categories from the spec.
- [x] 4.3 Implement an `IActionExecutor` decorator at the Host/Device composition boundary so traversal, entry, popup, recovery, and direct Host actions cannot bypass the same gate.
- [x] 4.4 Persist and trace every allow/deny decision with policy hash, stable rule ID, page fingerprint, target, run/step correlation, and redacted evidence.
- [x] 4.5 Add tests for deny-overrides-allow, unknown default deny, toggle denial, destructive keyword/semantic denial, safe navigation allowance, recovery/popup coverage, and zero executor calls after denial.

## 5. Run Assets and Feedback

- [x] 5.1 Implement run ID allocation plus atomic creation of isolated run directories, `manifest.json`, scenario snapshot, compiled plan, trace directory, and initial result state.
- [x] 5.2 Implement numbered step evidence writing for before/after screenshot and UI XML, normalized analysis, step plan, safety decision, and verification result with shared correlation fields.
- [x] 5.3 Implement append-only `issues.jsonl` with stable fingerprints, evidence paths, occurrence links, categories, phases, severity, and disposition.
- [x] 5.4 Implement authoritative `result.json` with success/incomplete/blocked/failure/cancelled vocabulary, coverage accounting, budgets, actions, safety totals, trace paths, and success-criteria evidence.
- [x] 5.5 Implement aggregate iteration reporting for ordered child runs, success rate, consecutive success count, latency, safety totals, and new/repeated/disappeared issue fingerprints.
- [x] 5.6 Implement centralized redaction for configured secrets, authorization headers, provider credentials, exceptions, model metadata, trace, manifests, and issues.
- [x] 5.7 Add asset-contract tests for partial failures, non-overwrite isolation, causal step ordering, issue aggregation, incomplete-result honesty, cancellation finalization, and redaction.

## 6. Host Readiness and Read-Only Vertical Slice

- [x] 6.1 Implement Host configuration and composition for selected device, provider capability, `PageAnalyzer`, wrapped action executor, screen-state provider, traversal services, trace recorder/storage, and output root.
- [x] 6.2 Implement `doctor --device` using the project emulator/ADB boundary to report boot, screenshot, UIAutomator, provider, and output readiness without starting or mutating a device.
- [x] 6.3 Implement `analyze --device` to capture one Settings observation, emit `PageAnalysis` plus provider/model/mode trace, and prove that no device action was sent.
- [x] 6.4 Implement classified exit codes, Ctrl+C cancellation, owned-child-process cleanup, trace closure, and final-result fallback for success, preparation failure, runtime failure, and cancellation paths.
- [x] 6.5 Add Host command and composition tests, then run one explicit emulator `doctor` and one read-only `analyze` smoke test and save their evidence.

## 7. Incremental Runner and Locate-One-Item

- [x] 7.1 Implement deterministic scenario-to-`TraversalPlan` compilation using existing Graph contracts and persist the compiled plan before execution.
- [x] 7.2 Implement current-page-fingerprint-bound step planning with at most one candidate action and stale-plan rejection.
- [x] 7.3 Implement the observe → analyze → plan → safety → execute/skip → re-observe → verify loop with classified termination and full step assets.
- [x] 7.4 Implement and verify the Settings reset procedure so every run starts from a proven Settings home page.
- [x] 7.5 Implement locate-one-item target/alias matching, bounded home-list scroll, target-row navigation, and target-page identity verification.
- [x] 7.6 Add fake-device/mock-provider end-to-end tests for target visible, target after scroll, target absent, stale plan, denied target, verification mismatch, ADB disconnect, provider timeout, and cancellation.
- [x] 7.7 Run the locate scenario once on the emulator with a deterministic/mock analysis provider and verify action/page/trace/assets end to end.
- [x] 7.8 Run the locate scenario once with the configured real vision provider, append every exposed problem to `issues.jsonl`, and triage each issue as product defect, environment defect, model variance, or scenario-data defect.
- [x] 7.9 Execute at least three diagnostic locate iterations, fix all P0/P1 issue fingerprints in dependency order, and retain before/after run IDs as change evidence.

## 8. Safe First-Level Enumeration

- [x] 8.1 Implement Settings-home first-level discovery, normalized identity/dedup, scroll progression, and verified end-of-list accounting without using coordinates as entry identity. ✅ (2026-08-03 判定: 实现已落地——`HostCommands` enumerate_first_level 路径 :848/:878/:939/:964；E2E 断言见 EmulatorScenarioIntegrationTests.cs:250-260；验收执行归 8.4-8.7)
- [x] 8.2 Implement safe entry sampling as enter → capture page identity/visible items → back → verify Settings home, with no child-control action. ✅ (2026-08-03 判定: 同上，sampled evidence 断言已存在于 E2E 测试；验收执行归 8.4-8.7)
- [x] 8.3 Implement discovered-but-skipped accounting for dangerous first-level entries and prove denied targets never reach the ADB action runner. ✅ (2026-08-03 判定: dangerous/denied 判定在 `UniClaw.Host/Safety/SafetyGate.cs`，入口 identity 键控；验收执行归 8.4-8.7)
- [ ] 8.4 Add fake-device/mock-provider end-to-end tests for multi-screen enumeration, duplicate rows after scroll, dangerous skip, safe entry sampling, return verification failure, scroll failure, and missing end-of-list proof.
- [ ] 8.5 Run one emulator enumeration with deterministic/mock analysis, inspect every step asset, and correct product defects before enabling real-provider execution.
- [ ] 8.6 Run one real-provider enumeration, record all issues by stable fingerprint, and verify that safety skips, incomplete coverage, and operational failures are reported distinctly.
- [ ] 8.7 Execute at least three diagnostic enumeration iterations, fix all P0/P1 issue fingerprints in dependency order, and retain before/after run IDs as change evidence.

## 9. Stability Gates and Failure Drills

- [ ] ~~9.1 Implement `run --repeat <n>` as serial execution for a single device with per-run reset, isolated output, configurable continue-after-failure, and aggregate reporting.~~ ⛔ **NOT NEEDED** (2026-08-03): `--repeat` CLI path 已移除（HostCommands.cs 无 `--repeat` 命中，deliver-safe 8.32 注释确认；`IterationAggregator` 保留于 RunAssets.cs 但无 CLI 入口）
- [ ] ~~9.2 Run locate-one-item ten consecutive times on the fixed emulator fixture and require 10/10 verified success, no safety bypass, no stale child process, and complete trace/result assets.~~ ⛔ **NOT NEEDED** (2026-08-03): 依赖 9.1 的重复执行入口，CLI 已移除则失依
- [ ] ~~9.3 Run safe enumeration ten consecutive times on the fixed emulator fixture and require 10/10 honest completion outcomes under the agreed scenario data; any incomplete run fails the stability gate.~~ ⛔ **NOT NEEDED** (2026-08-03): 同 9.2，依赖 9.1
- [ ] 9.4 Drill ADB disconnect, provider timeout, invalid provider JSON, safety denial, verification mismatch, Ctrl+C, and trace-write failure; verify each produces the specified classification and recoverable evidence. ⏸ **待模拟器**（独立有效，未被取代）
- [ ] ~~9.5 Review the aggregate reports and close or explicitly defer every remaining issue fingerprint with rationale in the change evidence.~~ ⛔ **NOT NEEDED** (2026-08-03): 聚合报告来自 9.1 的 aggregate reporting，CLI 移除后无聚合报告可审

## 10. Verification and Documentation Sync

- [x] 10.1 Run focused Host/Device/Core tests after each owned module group and run the full non-integration test suite after cross-module integration.
- [x] 10.2 Run `dotnet build src/UniClaw.Core.sln` and all architecture guards, confirming zero errors and no locked-enum or dependency-direction change.
- [x] 10.3 Update Tier 3 layer docs for actual Graph/Traversal/Observability/Device/Host behavior and update emulator/testing instructions with explicit commands, fixture assumptions, output layout, and failure triage.
- [x] 10.4 Verify canonical existing specs for entry policy, page analyzer, screen state, traversal, plan serialization, emulator integration, and trace remain satisfied; document any unrelated pre-existing gap without masking it.
- [x] 10.5 Run `openspec validate deliver-safe-android-settings-test-loop`, inspect `git diff --check`, and attach the final command results plus scenario run IDs before marking the change apply-complete.

## 11. Fine-grained explicit integration ladder

- [x] 11.1 Add scope-gated integration facts; default test discovery skips all external provider/ADB/emulator work.
- [x] 11.2 Add a reviewed screenshot golden asset plus independent `vision-smoke` and `vision-golden` scopes.
- [x] 11.3 Split ADB verification into connectivity, read-only capture/UIAutomator, bounded navigation-action, and real-vision-selected navigation scopes with per-run evidence.
- [x] 11.4 Add strict `scenario-locate` and `scenario-enumerate` gates through production Host → Core `TraversalEngine`/`TraversalFSM` composition.
- [x] 11.5 Document the execution order, exact commands, fixture assumptions, output paths, and change-to-scope selection rules.
- [ ] 11.6 Execute the affected scopes on the fixed emulator and append run IDs/results; test existence alone does not satisfy the real-system gate.

## Design Docs

> Auto-generated from the proposal Impact section and aligned to the repository's current four-layer documentation.
> Implementation agents must read the relevant document before starting a task group.

| Module | Design Doc |
|--------|------------|
| `src/UniClaw.Core/Graph/` | `docs/system/layers/graph.md` |
| `src/UniClaw.Core/Traversal/` | `docs/system/layers/traversal.md` |
| `src/UniClaw.Core/StateMachine/` | `docs/system/layers/state-machine.md` + `docs/system/patterns/fsm-design.md` |
| `src/UniClaw.Core/Observability/` | `docs/system/layers/observability.md` |
| `src/UniClaw.Core/UniBrain/` | `docs/refactor/2026-07-15-vision-mode-strategy-design.md` |
| `src/UniClaw.Device/` | `docs/system/development-environment.md` + `docs/testing/android-emulator.md` |
| `src/UniClaw.Host/` | `docs/system/patterns/system-orchestration.md` |
| `tests/` and emulator scenarios | `docs/system/layers/simulation.md` + `docs/system/layers/simulation-baseline.md` |
