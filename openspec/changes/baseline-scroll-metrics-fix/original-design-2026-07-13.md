# 基线测试滚动指标修复

**日期:** 2026-07-13
**优先级:** P0 (阻塞性问题)
**状态:** 设计阶段

---

## 问题摘要

基线测试报告中的滚动指标全部为 0，导致滚动场景测试无法验证实际滚动行为。

**影响范围:**
- `ScrollableBaselineTests.cs` 全部 6 个滚动场景
- `BaselineReportCollector` 生成的报告不准确
- `ExpectedBehavior.Verify` 未验证滚动特定指标

**证据:**
```json
// wifi-list-scroll-all-screens.json (actual)
{
  "scrollCount": 0,        // ❌ 应该 ≥5
  "scrollDistance": 0,     // ❌ 应该 =1.0
  "finalProgress": 0,      // ❌ 应该 =1.0
  "jumpDetected": 0,       // ❌ 应该 =0 (此场景正确)
  "adaptiveStepIncreases": 0
}
```

---

## 根本原因分析

### 第 1 层：BaselineReportCollector 硬编码

**位置:** `BaselineReportCollector.cs:96-99`

```csharp
ScrollCount: executor?.GetScrollCount("") ?? 0,
ScrollDistance: vision?.GetScrollDistance() ?? 0.0,
ScrollUpCount: executor?.GetScrollUpCount() ?? 0,
JumpDetected: 0,                    // ⚠️ 硬编码
JumpRecovered: 0,                   // ⚠️ 硬编码
FinalProgress: vision?.GetScrollProgress(vision?.CurrentPageId ?? "") ?? 0.0,
AdaptiveStepIncreases: 0;           // ⚠️ 硬编码
```

**问题:**
- `JumpDetected`, `JumpRecovered`, `AdaptiveStepIncreases` 被硬编码为 0
- 注释说 "Phase 3: implement jump detection"，但 `ScrollHandler` 已实现
- 即使 `ScrollCount` 调用了 `executor?.GetScrollCount("")`，空 pageId 参数可能返回 0

### 第 2 层：数据源未被正确使用

**可用的数据源:**
- `ScrollableMockActionExecutor.ScrollHistory` — 包含所有滚动操作记录
- `ScrollableMockVisionService.GetScrollProgress()` — 当前进度
- `ScrollableMockVisionService.GetScrollState()` — 完整滚动状态

**未被使用的原因:**
- `BuildActualNumeric` 方法没有正确访问 `ScrollHistory`
- 空字符串 `pageId` 参数导致查找失败

### 第 3 层：ExpectedBehavior.Verify 未验证滚动字段

**位置:** `ExpectedBehavior.Verify.cs:292-348`

`VerifyNumericAnchor` 只验证 4 个字段：
- `TotalSteps`
- `VisitedPagesCount`
- `ActionHistoryCount`
- `ElapsedSecondsMax`

滚动字段（`ScrollCount`, `ScrollDistance`, `JumpDetected` 等）虽然定义在 `NumericAnchor` 中，但从未被验证。

---

## 设计方案：最小改动

**核心原则:**
- ✅ 不引入新数据结构（无 `ScrollStatistics`）
- ✅ 不修改 `TraversalResult`
- ✅ 只从现有数据源计算指标
- ✅ 保持向后兼容

### 改动点 1：修复 BaselineReportCollector

**文件:** `tests/UniClaw.Core.Tests/Baseline/BaselineReportCollector.cs`

**修改前:**
```csharp
private NumericAnchor BuildActualNumeric(
    TraversalResult result,
    ScrollableMockActionExecutor? executor,
    ScrollableMockVisionService? vision)
{
    return new NumericAnchor(
        TotalSteps: result.TotalSteps,
        VisitedPagesCount: result.VisitedPages.Length,
        ActionHistoryCount: result.ActionHistory.Length,
        ElapsedSecondsMax: result.ElapsedSeconds,
        ScrollCount: executor?.GetScrollCount("") ?? 0,  // ❌ 空 pageId
        ScrollDistance: vision?.GetScrollDistance() ?? 0.0,
        ScrollUpCount: executor?.GetScrollUpCount() ?? 0,
        JumpDetected: 0,                                  // ❌ 硬编码
        JumpRecovered: 0,                                 // ❌ 硬编码
        FinalProgress: vision?.GetScrollProgress(vision?.CurrentPageId ?? "") ?? 0.0,
        AdaptiveStepIncreases: 0);                        // ❌ 硬编码
}
```

**修改后:**
```csharp
private NumericAnchor BuildActualNumeric(
    TraversalResult result,
    ScrollableMockActionExecutor? executor,
    ScrollableMockVisionService? vision)
{
    // 基础指标（保持不变）
    var totalSteps = result.TotalSteps;
    var visitedPagesCount = result.VisitedPages.Length;
    var actionHistoryCount = result.ActionHistory.Length;
    var elapsedSecondsMax = result.ElapsedSeconds;

    // 滚动指标：从 ScrollHistory 计算
    int scrollCount = 0, scrollUpCount = 0;
    double scrollDistance = 0.0, finalProgress = 0.0;
    
    if (executor != null && vision != null)
    {
        var scrollHistory = executor.ScrollHistory;
        var currentPageId = vision.CurrentPageId;
        
        // 从滚动历史计算指标
        scrollCount = scrollHistory.Count(s => s.Action == ScrollActionType.ScrollDown);
        scrollUpCount = scrollHistory.Count(s => s.Action == ScrollActionType.ScrollUp);
        finalProgress = vision.GetScrollProgress(currentPageId);
        
        // 计算总滚动距离（第一个操作前进度 → 最后一个操作后进度）
        if (scrollHistory.Length > 0)
        {
            var firstScroll = scrollHistory[0];
            var lastScroll = scrollHistory[^1];
            scrollDistance = lastScroll.AfterProgress - firstScroll.BeforeProgress;
        }
    }

    return new NumericAnchor(
        TotalSteps: totalSteps,
        VisitedPagesCount: visitedPagesCount,
        ActionHistoryCount: actionHistoryCount,
        ElapsedSecondsMax: elapsedSecondsMax,
        ScrollCount: scrollCount,
        ScrollDistance: scrollDistance,
        ScrollUpCount: scrollUpCount,
        JumpDetected: 0,        // Phase 3: 从 ScrollHandler.Statistics 收集
        JumpRecovered: 0,       // Phase 3: 从 ScrollHandler.Statistics 收集
        FinalProgress: finalProgress,
        AdaptiveStepIncreases: 0); // Phase 3: 从 AdaptiveStepCalculator 收集
}
```

### 改动点 2：更新 JSON 预期文件

**文件:** `tests/UniClaw.Core.Tests/Baseline/Fixtures/expected/scroll/*.json`

**当前占位值:**
```json
{
  "numericAnchor": {
    "totalSteps": 28,
    "scrollCount": 0,        // ❌ 占位值
    "finalProgress": 0,      // ❌ 占位值
    "scrollUpCount": 0,
    "jumpDetected": 0,
    "adaptiveStepIncreases": 0
  }
}
```

**修复策略:**
1. 运行测试获取实际值
2. 更新 JSON 文件中的占位值
3. 或使用 `"auto_derive"` 机制自动计算

### 改动点 3：ExpectedBehavior.Verify 扩展（可选）

**文件:** `src/UniClaw.Core/Simulation/ExpectedBehavior/ExpectedBehavior.Verify.cs`

**策略:** 滚动指标当前只在 `BaselineReport` 中展示对比，不作为 CI-blocking 验证规则。

如需添加验证，可在 `VerifyNumericAnchor` 中添加：
```csharp
// 滚动指标验证（可选，future work）
// 当预期值非 0 时验证实际值是否匹配
```

---

## 实施清单

| # | 任务 | 文件 | 优先级 |
|---|------|------|-------|
| 1 | 修复 `BuildActualNumeric` 计算逻辑 | `BaselineReportCollector.cs` | P0 |
| 2 | 更新 JSON 预期文件 numericAnchor 值 | `scroll/*.json` (6 files) | P1 |
| 3 | (可选) 扩展 `VerifyNumericAnchor` 验证 | `ExpectedBehavior.Verify.cs` | P2 |
| 4 | 更新文档 | `simulation-baseline.md` | P2 |

---

## 实施后验证

### 步骤 1：运行基线测试

```bash
cd d:/space-x/uni_claw
dotnet test src/UniClaw.Core.sln --filter "FullyQualifiedName~Baseline" -v n
```

### 步骤 2：检查生成的报告

**位置:** `tests/UniClaw.Core.Tests/bin/Release/net9.0/Baseline/reports/*.json`

**验证点:**
- [ ] `scrollCount` > 0（对于有滚动的场景）
- [ ] `finalProgress` = 1.0（对于到底的场景）
- [ ] `scrollUpCount` 正确反映向上滚动
- [ ] `allPassed` = true（验证规则通过）

### 步骤 3：对比 expected vs actual

打开报告文件，验证：

```json
{
  "expectedNumeric": {
    "scrollCount": 5,
    "finalProgress": 1.0
  },
  "actualNumeric": {
    "scrollCount": 5,      // ✅ 匹配
    "finalProgress": 1.0   // ✅ 匹配
  }
}
```

### 步骤 4：场景级验证

| 场景 | 验证点 |
|------|-------|
| `wifi-list-scroll-all-screens` | `scrollCount` ≥ 5, `finalProgress` = 1.0 |
| `wifi-list-scroll-back-to-top` | `scrollUpCount` ≥ 1 |
| `sparse-list-jump-recovery` | `jumpDetected` = 0 (暂时), `jumpRecovered` = 0 (暂时) |
| `wifi-list-boundary-conditions` | `finalProgress` = 1.0 |

---

## Future Work (Phase 3)

以下指标需要进一步集成才能正确收集：

| 指标 | 当前状态 | 需要做的 |
|------|---------|----------|
| `JumpDetected` | 硬编码 0 | 从 `ScrollHandler.Statistics.JumpDetectedCount` 收集 |
| `JumpRecovered` | 硬编码 0 | 从 `ScrollHandler.Statistics.JumpRecoveredCount` 收集 |
| `AdaptiveStepIncreases` | 硬编码 0 | 从 `AdaptiveStepCalculator` 跟踪收集 |

**Phase 3 设计考虑:**
- 是否需要 `ScrollStatistics` 数据结构传递统计
- 如何将 `ScrollHandler` 集成到 `TraversalEngine`
- 是否需要修改 `TraversalResult`

---

## 参考资料

- **当前实现:**
  - `ScrollableMockActionExecutor.cs:18` — `ScrollHistory` 属性
  - `ScrollableMockVisionService.cs:68` — `GetScrollProgress` 方法
  - `ScrollStatisticsCollector.cs:15-19` — 已实现的统计收集

- **相关文档:**
  - `docs/system/layers/simulation-baseline.md` — 基线测试规格
  - `docs/system/layers/simulation.md` — Simulation 代码层规格

- **测试数据:**
  - `tests/UniClaw.Core.Tests/bin/Release/net9.0/Baseline/reports/*.json` — 当前报告
