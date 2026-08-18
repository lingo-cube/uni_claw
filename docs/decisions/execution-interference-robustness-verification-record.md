# Execution Interference Robustness — Deterministic Verification Record

> Date: 2026-08-16 | Mode: evidence collection (scenario → fixture verification)
> Trigger: user-reported execution interference scenarios (no data yet) under the frozen
> system baseline. Zero production change; zero OpenSpec change; test-only verification of
> already-graduated deterministic mechanisms.
> Files: `tests/UniClaw.Runtime.Tests/Scenario/RuntimeInterferenceRobustnessTests.cs` +
> 4 new variants in `Fakes/ScriptedEnvironmentVariants.cs` + harness mapping in `ScenarioHarness.cs`.

## User Scenarios → Verified Behavior (all deterministic, all terminated)

| # | User scenario | Fixture variant | Deterministic mechanism exercised | Verified result | Action history |
|---|---|---|---|---|---|
| 1 | 异常弹窗 | `unknown-overlay`（非 supported Popup：前台 Settings、页面 Unknown、计划中无 handling step） | SC-P3-002 escalate boundary (`IsLocalObstructionHypothesis` true → `CanHandleLocalObstruction` false → normal step → grounding fail) | **Failed（显式原因）**；不伪造处理/完成；无 Dismiss 派发 | `[LaunchApp, Tap(0)]` |
| 2a | UI 卡死（世界照常推进） | `repeat-timeout-advances`（全部 action transport TimedOut + 正常转场） | SC-P3-001 uncertain-action：TimedOut = dispatch 不确定，不阻塞世界证据 | **Completed**（fresh Observation 推进 → GoalEvidence）；每动作恰好一次（无盲重试） | `[LaunchApp, Tap(0), Tap(0), SetSwitch(1,true)]` |
| 2b | UI 卡死（世界自环） | `repeat-timeout-stuck`（全部 TimedOut + 自环） | SC-P3-001 + Plan 有界：TimedOut 不重派发；world 不变 → 后续 grounding 失败 | **Failed（显式原因）**；无盲重试、无无限循环、无伪造完成 | `[LaunchApp, Tap(0)]` |
| 3 | 退桌面反复打断 | `drift-again`（恢复成功后又 drift） | SC-P2-001 single-attempt recovery boundary：恢复后再次 drift → 不递归恢复 | **Failed（"恢复后再次 Agent-scope drift"）**；Trap(UnexpectedPage, Agent) 发射；Recovery-1 仅一次 | recovery 会话单次 |
| 4 | H5/广告页伪装 | `spoofed-page`（点击进入广告页，元素伪装 "WiFi"） | Plan≠Reality + Grounding≠Identity authority + dispatch≠world success：identity 规则误判 NetworkSettings → SetSwitch 派发到伪装元素 → 物理语义 Rejected → 无完成 | **Failed（显式原因）**；不伪造 switch 状态/完成 | `[LaunchApp, Tap(0), Tap(0), SetSwitch(0,true)]` |

## Verification totals

- New tests: **5/5 PASS** (2026-08-16, current build).
- Regression subset (PopupObstructionRecoveryTests + AgentRecoveryLauncherDriftTests +
  UncertainActionVerificationTests + UnexpectedNavigationReconciliationPhase2Tests +
  NormalWifiHappyPathTests + new tests): **32/32 PASS** — no regression to graduated mechanisms.

## Conclusions for buyer assessment (frozen baseline)

1. **All four user interference classes are handled (or explicitly terminated) by already-graduated
   deterministic mechanisms** — none reaches a state where deterministic rules cannot truthfully
   adjudicate. Per reentry rule #5, **outer intelligence (Trigger D) evidence: NOT ESTABLISHED** —
   no scenario demonstrated an adjudication gap requiring external advice.
2. **The earliest missing system link remains unchanged** (`OPEN_WORLD_VIEWPORT_EXHAUSTION_MECHANISM`,
   inventory-completeness domain) — unrelated to these interference scenarios.
3. **Control-plane read-only observability data exists**: each interference class leaves observable
   Trace semantics (Trap event + TrapKind/TrapScope for drift; explicit Failed reason for
   overlay/spoof/timeout-stuck; recovery session RecoveryId events). A read-only visualization
   (interference event cards + Agent handling trail) is technically feasible within the frozen
   read-only boundary — but remains a UI expansion requiring a concrete operator workflow (Trigger E).
4. **No new OpenSpec change; no production mutation; no reopened graduated capability.**

## Status

```text
INTERFERENCE_ROBUSTNESS_DETERMINISTICALLY_VERIFIED (test-only evidence)
Baseline: remains FROZEN; NO_IMMEDIATE_SYSTEM_EXPANSION
OuterIntelligenceTriggerDEvidence: NOT_ESTABLISHED
```
