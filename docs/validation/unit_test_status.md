# Unit Test Status

**Project**: UniClaw (Core + Host)
**Version**: core-observation-pipeline (apply)
**Change**: core-observation-pipeline
**Task**: Group 1-6 + 7.1 — 坐标补丁、FSM 仿真回归、观测管线收敛、AI 重试策略、FSM 导航增强、意图+模型适配、验证
**Generated**: 2026-08-02
**Git Branch**: feature/refactor
**Git Commit**: 3e5839c (base; change uncommitted)

---

## Executive Summary

core-observation-pipeline 全量落地：坐标/过滤补丁（倒置 bounds 归一化、y-clamp、summary 过滤、ImageButton 过滤）、FSM 仿真回归 Harness（`FsmSimulationHarness`，无 emulator 无 AI <1ms）、`ObservationPipeline`（UIA→AI 三级级联，新命名空间 `UniClaw.Core.Observation`）、AI 空响应不重试、ErrorHandling 双闸门、PreconditionChecker 门禁、意图 AI 回退机械映射。
全量测试 **1144/1153 通过**（1013 Core + 131 Host），0 失败；含 27 个新增测试（7 FSM 回归 + 14 管线 + 2 AC5 设备边界 + 4 上轮新增计数调整）。

**仿真回归暴露并修复 3 个生产缺陷**：`IncrementNodeFailedItems` no-op（同页 item 闸门永不触发）、`ConsecutiveErrors` 成功后未重置（连续闸门总是先触发，item 闸门死代码）、Advisor 属性访问位于 FSM try/catch 外。

| Metric | Value |
|--------|-------|
| Total Tests | **1153** |
| Passed | **1144** |
| Failed | **0** |
| Error | **0** |
| Skipped | 9（emulator-gated + 既有 skip） |
| Build | 0 errors |
| New tests | 27 |
| Duration | ~30s |
| AC1 门禁（仿真） | ✅ 全绿（Core 1013 + Host 131） |

**Overall Status**: ✅ PASSED

## Module-Scoped Results (this change)

Data source: `.claude/skills/module-test/contracts/host_unit.json` (2026-08-02, FRESH)

| Scope | Total | Passed | Failed | Notes |
|-------|-------|--------|--------|-------|
| Host.Tests | 139 | 131 | 0 | 含 8 skipped（emulator 集成门禁） |
| Core.Tests | 1014 | 1013 | 0 | 含 1 skipped（VisionGolden 金样） |
| 新增测试 | 27 | 27 | 0 | FSM 回归(7) + ObservationPipeline(14) + AC5 设备边界(2) + 上轮 Store/Sink/lite(4 计入) |

## New Tests (27)

| Test | Verifies | Status |
|------|----------|--------|
| `FsmSimulationRegressionTests` (7) | ErrorHandling 双闸门（5 item 交错 deny/success → PressBack；3 连续错误 → PressBack）、弹窗单次重试、无变化 → Branch、Execute dispatch、PreconditionChecker 门禁、AI 空响应 IsTransient=false (2.1-2.10) | ✅ PASS |
| `ObservationPipelineTests` (14) | UIA-only 路径、<N item 回落 AI、popup 回落 AI、dump 失败 → AI、AI 空响应抛错不重试、UIA_disabled、back-reuse、config 应用 (3.1-3.2) | ✅ PASS |
| AC5 设备边界 (2) | `AdbScreenStateProvider` 首次 dump 失败 → `UIA_Available=false`，后续跳过 L1 (3.5) | ✅ PASS |
| `HandleErrorHandlingTests` 更新 (4) | ConsecutiveErrors 在所有 ErrorStrategy 下递增（旧语义「非 Retry 重置」已废弃） | ✅ PASS |

## Design Coverage

Doc: `openspec/changes/core-observation-pipeline/design.md` (D1-D9) + `docs/system/layers/host.md`

| Component | Change | Direct tests |
|-----------|--------|--------------|
| `ObservationPipeline` | 新增 — UIA→AI 三级级联（D1），`UniClaw.Core.Observation` 桥接命名空间（D-131） | ✅ ObservationPipelineTests (14) |
| `ObservationConfig` | 新增 — UIA_MinItems/EnablePopupDetection/SkipUIAOnBackNavigation/UIA_Enabled (D2) | ✅ ObservationPipelineTests |
| `UiAutomatorPageAnalysis` | 新增 — UIA XML → PageAnalysis（从 augmenter 迁移）(3.3) | ✅ ObservationPipelineTests |
| `UiAutomatorAugmentingPageAnalyzer` | `[Obsolete]` 保留（逻辑已迁移） | ✅ 既有 8 测试零改动 |
| `AdbScreenStateProvider` | +`IsUiAutomatorAvailable` 首败置 false (D6/AC5) | ✅ AC5 设备边界 (2) |
| `ErrorContext` | +`IncrementNodeFailedItems(nodeId)` 去重计数（原 no-op 修复） | ✅ FsmSimulationRegressionTests + HandleErrorHandlingTests |
| `TraversalFSM` | verification_passed 重置 ConsecutiveErrors；双闸门各自只重置自己的计数器；PreconditionChecker 门禁；Advisor 接入 | ✅ FsmSimulationRegressionTests (7) |
| `IPreconditionChecker` | 新增可选门禁 (5.4) | ✅ PreconditionCheck_CheckerReturnsFalse |
| `ModelResponse.IsEmpty` / `PageAnalyzer` | 空响应结构性错误不重试 (4.1-4.3) | ✅ PageAnalyzer_IsTransient_EmptyResponse |
| `ScenarioPlanCompiler.ResolveIntentSlots` | AI 失败回退机械映射 (6.1) | ✅ 既有意图测试 |

**Gaps**: 7.2 locate 集成（≤120s, ≤1 AI call）与 7.3 enumerate 集成（≥5 entries, ≥1 scroll）为待办，需 emulator。

## Blocked / Paused Items (明确范围)

| Item | 状态 | 原因 |
|------|------|------|
| 7.2 Locate 集成 | ⏸️ 待办 | 需 emulator（AC2/AC3 已按用户指示改为待办，非自动门禁） |
| 7.3 Enumerate 集成 | ⏸️ 待办 | 需 emulator |
| 8.2 lite 路由（上一 change 遗留） | 🔒 BLOCKED | Core 公共契约 Non-Goal 冲突；与 ObservationPipeline 无关 |

## Cross-Module Contract Aggregation

⚠️ 部分 contract 文件为历史快照；当前全量 1144/1153 绿是权威状态。

| Module | Tests (P/F) | Timestamp | Freshness |
|--------|------------|-----------|-----------|
| host (this change) | 1144/0 | 2026-08-02 | ✅ FRESH |
| traversal (steporchestrator-decomposition) | 671/0 | 2026-07-15 | ✅ FRESH |
| state_machine (globalfsm-activation) | 677/0 | 2026-07-15 | ✅ FRESH |
| graph / simulation / e2e_test / trace / v6_9 | — | 2026-06 至 07 | 🟡 历史快照 |

## Code Changes (this change)

| File | Change |
|------|--------|
| `src/UniClaw.Core/Observation/ObservationPipeline.cs` | 新增 — UIA→AI 级联（D1/D2/D6），back-reuse，trace 决策 UIA/AI/UIA_disabled |
| `src/UniClaw.Core/Observation/ObservationConfig.cs` | 新增 — 管线配置（D2） |
| `src/UniClaw.Core/Observation/UiAutomatorPageAnalysis.cs` | 新增 — UIA XML 解析（自 augmenter 迁移） |
| `src/UniClaw.Core/StateMachine/Error/ErrorContext.cs` | `IncrementNodeFailedItems(nodeId)` 去重记录（原 no-op） |
| `src/UniClaw.Core/StateMachine/TraversalRuntimeContext.cs` | 委托解析当前 frame nodeId |
| `src/UniClaw.Core/StateMachine/TraversalFSM.cs` | verification 成功重置 CE；双闸门计数器解耦；PreconditionChecker |
| `src/UniClaw.Core/StateMachine/StepContext.cs` | +`IPreconditionChecker` |
| `src/UniClaw.Device/AdbScreenStateProvider.cs` | +`IUiAutomatorAvailability` 首败置 false（AC5） |
| `src/UniClaw.Host/Commands/HostCommands.cs` | 组装 ObservationPipeline + `MarkBackNavigation`；移除 augmenter 组装 |
| `src/UniClaw.Host/Runner/ScenarioObservation.cs` | 移除 `useUiAutomatorAnalysis` 开关（3.4） |
| `src/UniClaw.Host/Runner/InvalidatingPageAnalysisCache.cs` | augmenter `[Obsolete]` |
| `docs/conventions/namespace-isolation.md` + `AGENTS.md` | D-131：Observation 桥接命名空间 |
| `tests/` (8 文件) | 27 新增/更新测试 + 既有测试语义适配 |

## Conclusions

- ✅ Groups 1-6 完成：坐标补丁、仿真回归（暴露并修复 3 个真实 FSM 缺陷）、ObservationPipeline 收敛、空响应不重试、双闸门、PreconditionChecker、意图回退
- ✅ 1144/1153 全绿；AC1 门禁（仿真测试）验证通过；ArchitectureGuardTests 宪章守卫（D-130/D-131）通过
- ✅ 命名空间裁决（用户确认）：保留 `UniClaw.Core.Observation`，proposal.md 已同步
- ⏸️ 7.2/7.3 实机集成测试待办（需 emulator）；Qwen 视觉模型切换由另一 agent 完成（`default-qwen-vision` 已归档）
- Ready for archive or device-verification phase

---

*Report generated per validation-documentation skill standards. Data source: `.claude/skills/module-test/contracts/` (standardized JSON contracts).*
