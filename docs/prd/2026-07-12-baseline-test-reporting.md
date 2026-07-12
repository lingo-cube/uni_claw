# Baseline Test Reporting — PRD

> **版本**: 1.0
> **日期**: 2026-07-12
> **状态**: 设计完成，待实施
> **来源**: brainstroming 流程 — 方案 A (轻量级 ReportWriter)

---

## 1. 背景与问题

### 1.1 现状

基线测试体系 (`Baseline/` 目录) 当前状态：

```
✅ ExpectedBehavior record (6 维度契约定义)
✅ JSON 预期定义文件 (8 场景: 2 settings + 6 scroll)
✅ Verify(TraversalResult) → VerificationReport (验证逻辑)
✅ Assert.True(report.AllPassed, report.Summary) (测试断言)
❌ 报告不落盘 — VerificationReport 只存在于内存
❌ Scroll 数值未验证 — NumericAnchor 有字段但 VerifyNumericAnchor 不检查
❌ 无流程集成 — 本地 dotnet test 后无可查看的报告产物
```

### 1.2 问题分析

| 问题 | 影响 | 场景 |
|------|------|------|
| 报告不持久 | 开发者无法跨运行比较基线数值变化 | 修改引擎后回查基线偏移 |
| Scroll 数值缺口 | 滚动场景的 ScrollCount、JumpRecovered 等指标未纳入验证 | ScrollableBaselineTests 6 场景 |
| 无可见反馈 | `dotnet test` 输出只有 PASS/FAIL，缺乏结构化摘要 | 本地开发调试 |

### 1.3 根本原因

验证链条止于 `VerificationReport` 内存对象和 `Assert.True` 断言，缺少输出环节。

---

## 2. 目标与范围

### 2.1 核心目标

1. **双格式报告输出** — 每次 `dotnet test` 运行后生成 JSON 机器报告 + Markdown 人读摘要
2. **本地开发流程集成** — 开发者 `dotnet test` 后可直接查看 `reports/index.md`
3. **Scroll 数值补全** — VerifyNumericAnchor 扩展，覆盖 7 个滚动指标字段

### 2.2 范围界定

| 包含 (Phase 1) | 不包含 (后续) |
|----------------|-------------|
| ✅ Baseline/ 目录下全部 8 场景报告 | ❌ CI artifact 上传 |
| ✅ JSON 机器可读报告 (每场景) | ❌ 历史趋势比较 |
| ✅ Markdown 汇总报告 (index.md) | ❌ Web dashboard |
| ✅ Scroll 数值验证补全 | ❌ 独立 CLI 工具 |
| ✅ 每次运行全量覆盖 | ❌ 跨运行 diff 报告 |
| ✅ .gitignore reports/ | ❌ 邮件/通知集成 |

### 2.3 成功标准

- `dotnet test` 后在 `tests/.../Baseline/reports/` 生成 8 个 JSON + 1 个 index.md
- index.md 汇总全部场景通过率和关键数值
- Scroll 场景的 ScrollCount、JumpDetected 等指标在报告中可见
- 现有测试无需重构，每个测试仅加 1 行代码

---

## 3. 架构设计

### 3.1 数据流

```
TraversalEngine.Run() → TraversalResult
    → ExpectedBehavior.Verify(result) → VerificationReport
    → Collector.Add(scenario, expected, result, report)
        ↓
Collector.Dispose() (all tests complete)
    → WriteAll()
        → reports/{scenario}.json    (每场景机器报告)
        → reports/index.md           (汇总表)
```

### 3.2 组件图

```
┌──────────────────────────────────────────────────┐
│                  Tests                            │
│  SimulationBaselineTests.cs                       │
│  ScrollableBaselineTests.cs                       │
│    └─ expected.Verify(result) → Assert.True       │
│    └─ Collector.Add(…)                ← 1 行新增  │
└───────────────────────┬──────────────────────────┘
                        │
┌───────────────────────▼──────────────────────────┐
│           BaselineReportCollector                 │
│  - Add(scenario, expected, result, report)        │
│  - Dispose() → WriteAll()                        │
└───────────────────────┬──────────────────────────┘
                        │
┌───────────────────────▼──────────────────────────┐
│           BaselineReportWriter                    │
│  - WriteJson(scenario, report) → {scenario}.json  │
│  - WriteIndex(allReports) → index.md              │
└──────────────────────────────────────────────────┘
```

### 3.3 文件结构

```
tests/UniClaw.Core.Tests/
  ├── Baseline/
  │   ├── SimulationBaselineTests.cs       (改: +Collector.Add)
  │   ├── ScrollableBaselineTests.cs       (改: +Collector.Add)
  │   ├── BaselineReportCollector.cs       (新: 收集+触发写入)
  │   ├── BaselineReportWriter.cs          (新: JSON+Markdown 输出)
  │   ├── Fixtures/expected/
  │   │   ├── settings-full-traversal.json
  │   │   ├── settings-target-search.json
  │   │   └── scroll/ (6 files)
  │   └── reports/                         (新: 运行产物, gitignore)
  │       ├── index.md                     (汇总, 每次覆盖)
  │       ├── settings-full-traversal.json
  │       ├── settings-target-search.json
  │       ├── wifi-list-scroll-all-screens.json
  │       ├── wifi-list-target-search.json
  │       ├── wifi-list-full-traversal.json
  │       ├── sparse-list-jump-recovery.json
  │       ├── overlapping-adaptive.json
  │       └── persistent-dedup.json
  └── .gitignore                           (改: +reports/)
```

---

## 4. 数据模型

### 4.1 BaselineReport record

```csharp
public sealed record class BaselineReport(
    string Scenario,
    string Description,
    DateTime Timestamp,
    bool AllPassed,
    ImmutableArray<RuleResult> Details,
    NumericAnchor ExpectedNumeric,
    NumericAnchor ActualNumeric,
    int TotalScenarios,
    int PassedScenarios);
```

字段 | 类型 | 说明
------|------|------
Scenario | string | 场景标识，匹配 expected JSON 文件名
Timestamp | DateTime | 运行时间戳
AllPassed | bool | 排除 numeric_anchor 后的 blocking 规则通过率
Details | ImmutableArray<RuleResult> | 逐条规则 PASS/FAIL/INFO
ExpectedNumeric | NumericAnchor | JSON 预期数值（含 scroll 字段）
ActualNumeric | NumericAnchor | 实际运行数值（含 scroll 字段）
TotalScenarios | int | 本批总场景数（仅 index 汇总有意义）
PassedScenarios | int | 本批通过数

### 4.2 JSON 报告格式

```json
{
  "scenario": "settings-full-traversal",
  "timestamp": "2026-07-12T19:30:00+08:00",
  "allPassed": true,
  "details": [
    {
      "ruleId": "completion:success",
      "passed": true,
      "message": "Success=True"
    }
  ],
  "expectedNumeric": {
    "totalSteps": 145,
    "visitedPagesCount": 19,
    "actionHistoryCount": 38,
    "scrollCount": 0,
    "jumpDetected": 0
  },
  "actualNumeric": {
    "totalSteps": 145,
    "visitedPagesCount": 19,
    "actionHistoryCount": 38,
    "scrollCount": 0,
    "jumpDetected": 0
  }
}
```

### 4.3 index.md 格式

```markdown
# Baseline Test Report

> **Run**: 2026-07-12 19:30 UTC
> **Pass Rate**: 8/8 (100%)

| Scenario | Status | Steps | Pages | Actions | Scrolls | Details |
|----------|--------|-------|-------|---------|---------|---------|
| settings-full-traversal | ✅ PASS | 145 | 19 | 38 | — | completion, page_coverage, ... |
| wifi-list-scroll-all-screens | ✅ PASS | 45 | 1 | 28 | 6 | completion, scroll_count, ... |
| ... | ... | ... | ... | ... | ... | ... |
```

---

## 5. 输出规范

### 5.1 字段命名

JSON 字段使用 **camelCase**，与项目 `DomainJsonOptions` 一致。

### 5.2 覆盖策略

| 维度 | 策略 |
|------|------|
| JSON 报告 | 每次全量覆盖，保留最新快照 |
| index.md | 每次全量重写 |
| reports/ 目录 | 加入 .gitignore，不提交运行产物 |

### 5.3 BaselineReportCollector 生命周期

```csharp
[CollectionDefinition("BaselineTests", DisableParallelization = true)]
public class BaselineTestCollection : ICollectionFixture<BaselineReportCollector> { }
```

或更简单的 `class dispose` 模式：在测试类构造函数注册，`Dispose` 时写入。

---

## 6. Scroll 数值补全

### 6.1 现状

`NumericAnchor` record 已定义 7 个滚动字段，但 `VerifyNumericAnchor` 不检查它们。

### 6.2 补全方案

滚动数据由**测试层传入** ReportCollector，不修改 `TraversalResult`：

```csharp
// ScrollableBaselineTests.cs — 提取实际滚动数据
var scrollCount = executor.GetScrollCount();
var scrollDistance = vision.GetTotalScrollDistance();
var finalProgress = vision.GetScrollProgress("wifi_list");
var jumpDetected = vision.GetJumpCount();

var actualNumeric = new NumericAnchor(
    TotalSteps: result.TotalSteps,
    VisitedPagesCount: result.VisitedPages.Length,
    ActionHistoryCount: result.ActionHistory.Length,
    ElapsedSecondsMax: result.ElapsedSeconds,
    ScrollCount: scrollCount,
    ScrollDistance: scrollDistance,
    ScrollUpCount: executor.GetScrollUpCount(),
    JumpDetected: jumpDetected,
    JumpRecovered: vision.GetJumpRecoveryCount(),
    FinalProgress: finalProgress,
    AdaptiveStepIncreases: vision.GetAdaptiveStepIncreaseCount());

Collector.Add("wifi-list-scroll-all-screens", expected, result, report, actualNumeric);
```

### 6.3 受影响场景

| 场景 | 需提供的实际滚动数据 |
|------|--------------------|
| WiFiList_ScrollThroughAllScreens | ScrollCount=6, FinalProgress=1.0, JumpDetected=0 |
| WiFiList_ScrollBackToTop | ScrollCount=6, ScrollUpCount=1 |
| WiFiList_ElementDeduplication | ScrollCount=6, 去重后元素数 |
| WiFiList_BoundaryConditions | ScrollCount=6, finalProgress=1.0 |
| SparseList_JumpRecovery | JumpDetected=1, JumpRecovered=1 |
| OverlappingList_AdaptiveStep | AdaptiveStepIncreases=2 |

### 6.4 Mock 服务接口确认

| 方法 | 服务 | 状态 |
|------|------|------|
| `GetScrollCount()` | ScrollableMockActionExecutor | ✅ 已存在 |
| `GetScrollUpCount()` | ScrollableMockActionExecutor | ⚠️ 需确认/新增 |
| `GetTotalScrollDistance()` | ScrollableMockVisionService | ⚠️ 需确认/新增 |
| `GetJumpCount()` | ScrollableMockVisionService | ⚠️ 需确认/新增 |
| `GetJumpRecoveryCount()` | ScrollableMockVisionService | ⚠️ 需确认/新增 |
| `GetAdaptiveStepIncreaseCount()` | ScrollableMockVisionService | ⚠️ 需确认/新增 |

---

## 7. 流程集成

### 7.1 本地开发流程

```
开发者: dotnet test
  → 基线测试运行
  → Assert.True 保证 CI-blocking 质量
  → Collector 收集 → ReportWriter 输出
  → reports/index.md 可查看
  
开发者: open tests/.../Baseline/reports/index.md
  → 一眼看到全部场景状态
  → 对比预期 vs 实际数值偏移
```

### 7.2 .gitignore

```
# tests/.../Baseline/.gitignore 或顶层忽略
tests/UniClaw.Core.Tests/Baseline/reports/
```

### 7.3 与非/滚动基线测试的关系

```
SimulationBaselineTests (2 场景)
  → ExpectedBehavior.Verify → Collector.Add → reports/settings-*.json

ScrollableBaselineTests (6 场景)
  → ExpectedBehavior.Verify + actualNumeric → Collector.Add → reports/wifi-list-*.json

同一 Collector, 同一 index.md, 一视同仁
```

---

## 8. 实施计划

### Phase 1: ReportWriter + ReportCollector (2 文件, P0)

| # | 文件 | 内容 |
|---|------|------|
| 1 | `BaselineReportCollector.cs` | 收集器: Add() + Dispose() → WriteAll() |
| 2 | `BaselineReportWriter.cs` | 静态方法: WriteJson(), WriteIndex() |

依赖: 无。仅 System.Text.Json + System.IO。

### Phase 2: 测试集成 (改 2 文件, P1)

| # | 文件 | 变更 |
|---|------|------|
| 1 | `SimulationBaselineTests.cs` | 每个测试加 `Collector.Instance.Add(...)` |
| 2 | `ScrollableBaselineTests.cs` | 每个测试加 `Collector.Instance.Add(...)` + 提取 actualNumeric |
| 3 | `.gitignore` | 追加 `tests/.../Baseline/reports/` |

### Phase 3: Scroll 数值补全 (改 Verify.cs + Mock 服务, P2)

| # | 文件 | 变更 |
|---|------|------|
| 1 | `ExpectedBehavior.Verify.cs` | VerifyNumericAnchor 扩展 scroll 字段检查 |
| 2 | Mock 服务 (视需要) | 补充 GetScrollUpCount / GetTotalScrollDistance 等方法 |

### Phase 4: 文档同步 (P3)

| # | 文件 | 变更 |
|---|------|------|
| 1 | `docs/system/layers/simulation-baseline.md` | §3 缺口表更新: 报告系统已就绪 |
| 2 | `docs/system/decisions/log.md` | 追加 D-N: Baseline Reporting 架构决策 |

---

## 9. 风险与缓解

| 风险 | 影响 | 缓解 |
|------|------|------|
| xUnit 测试不保证执行顺序 | Collector 收不到完整集合 | 使用 Collection Fixture 确保 Dispose 触发 |
| 并行测试竞争 Collector | 数据丢失或错乱 | `DisableParallelization = true` |
| Mock 服务缺少滚动查询方法 | Scroll 数值无法提取 | Phase 2 确认接口，Phase 1 先输出空值 |
| reports/ 磁盘写入失败 | 测试不阻塞 | 写入失败静默忽略，不影响 Assert |

---

## 10. 现有文档更新

### 10.1 simulation-baseline.md §3 缺口表

当前:
```
| **基线测试报告系统** | 报告不落盘，无流程集成 | — |
```

改为:
```
| **基线测试报告系统** | ✅ 已实现: JSON + Markdown, 本地 dotnet test 自动生成 | ReportWriter + Collector |
```

### 10.2 decisions/log.md 追加

```
| D-N | Baseline Reporting 架构 | ✅ Design | 2026-07-12 |
|    | 选择: 轻量级 ReportWriter (方案 A) | | |
|    | 理由: 最小侵入，零新依赖，自然延伸 Verify 链 | | |
|    | 输出格式: JSON 每场景 + Markdown index.md 汇总 | | |
|    | 覆盖策略: 每次全量覆盖，只保留最新 | | |
```

---

## 附录 A: 设计决策记录

| 决策点 | 选择 | 理由 |
|--------|------|------|
| 报告格式 | JSON + Markdown 双格式 | 兼顾 CI 自动化和人工审查 |
| Markdown 深度 | 仅 index.md 汇总 (无单场景 md) | 减少产物数量，汇总够看 |
| 覆盖策略 | 每次全量覆盖 | 保留最新快照，不保留历史 |
| 收集器模式 | Collector.Add + Dispose 触发写入 | 不强制继承基类，测试只需加 1 行 |
| Scroll 数据来源 | 测试层传入 (非 TraversalResult) | 不改生产代码类型 |
| 新文件位置 | tests/ 项目 (非 src/) | 报告是测试基础设施，不是生产功能 |
| 测试并行 | DisableParallelization | 避免 Collector 竞争 |

## 附录 B: 文件变更索引

| 操作 | 文件 | 行数估计 |
|------|------|---------|
| 新增 | `tests/.../Baseline/BaselineReportCollector.cs` | ~80 |
| 新增 | `tests/.../Baseline/BaselineReportWriter.cs` | ~120 (含模板) |
| 修改 | `tests/.../Baseline/SimulationBaselineTests.cs` | +2 行 |
| 修改 | `tests/.../Baseline/ScrollableBaselineTests.cs` | +8 行 (+ actualNumeric) |
| 新增 | `tests/.../Baseline/reports/.gitkeep` | 1 |
| 修改 | `src/.../Simulation/ExpectedBehavior/ExpectedBehavior.Verify.cs` | +~30 (scroll 验证) |
| 新增 | `docs/prd/2026-07-12-baseline-test-reporting.md` | 本文 |
| 修改 | `.gitignore` | +1 行 |
| 修改 | `docs/system/decisions/log.md` | +1 条 |
