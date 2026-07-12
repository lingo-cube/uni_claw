# Scroll-Enabled Baseline Test Design

> **Date**: 2026-07-12
> **Status**: Design Complete (Brainstormed)
> **Priority**: P2 - Enhancement

---

## 设计决策摘要

通过头脑风暴讨论，确定了以下关键决策：

| 决策点 | 选择 | 理由 |
|--------|------|------|
| 场景范围 | 全场景覆盖 (A) | 验证所有滚动行为：向下、向上、跳跃、自适应、去重、边界 |
| Fixture策略 | 混合策略 (C) | 一个主Fixture + 特殊场景专用Fixtures，平衡复用和独立性 |
| 测试组织 | 新建独立类 (B) | ScrollableBaselineTests.cs，职责清晰，不影响现有基线测试 |
| 主Fixture规模 | 7屏WiFi列表 (B) | 25元素，更复杂的元素分布，支撑大部分场景 |
| 验证策略 | 扩展验证 (B) | numericAnchor扩展，添加滚动特定指标 |
| 特殊Fixture | 预定义数据 (C) | 数据与代码分离，易于调整 |
| 命名约定 | 场景描述式 (A) | 测试名称清晰表达业务场景 |
| 特殊场景复杂度 | 单一精心设计 (A) | 基线聚焦核心验证，边缘情况由单元测试覆盖 |
| 向上滚动 | 混合场景 (C) | 在真实遍历中触发向上滚动（返回顶部） |
| 文档更新 | 全部更新 (ABC) | simulation-baseline.md + XML注释 + 概览文档 |

---

## 背景

当前 `SimulationBaselineTests.cs` 包含 2 个基线场景（全量遍历 + 目标搜索），均基于 `StatefulMockVisionService`（无滚动支持）。滚动模拟增强功能已完整实现（`scroll-simulation-enhancement` change 已归档），需要添加带滚动功能的基线测试用例。

## 目标

创建新的基线测试类 `ScrollableBaselineTests.cs`，演示滚动功能在全局遍历中的完整集成，覆盖全场景滚动行为。

---

## 测试组织结构

```
tests/Baseline/
  ├── SimulationBaselineTests.cs          (现有：2个非滚动场景)
  ├── ScrollableBaselineTests.cs         (新增：6个滚动场景)
  └── Fixtures/expected/scroll/          (新增：滚动场景ExpectedBehavior JSON)
       ├── wifi-list-scroll-all-screens.json
       ├── wifi-list-scroll-back-to-top.json
       ├── wifi-list-element-deduplication.json
       ├── wifi-list-boundary-conditions.json
       ├── sparse-list-jump-recovery.json
       └── overlapping-list-adaptive-step.json
```

---

## Fixture设计

### 主Fixture：WiFi列表（7屏，25元素）

**用途**: 支撑向下滚动遍历、向上回退、元素去重、顶部/底部边界场景

```
Segment 0.0 (6元素): 
  - BackToSettings (顶部导航按钮)
  - WiFi Switch (开关)
  - Network1, Network2, Network3 (网络按钮)

Segment 0.2 (4元素):
  - Network3 (重叠), Network4, Network5, Network6

Segment 0.4 (4元素):
  - Network6 (重叠), Network7, Network8, Network9

Segment 0.6 (4元素):
  - Network10, Network11, Network12, Network13

Segment 0.8 (4元素):
  - Network14, Network15, Network16, Network17

Segment 1.0 (8元素):
  - Network18, Network19, Network20, Network21, 
    Network22, Network23, Network24, Network25 (底部)
```

**重叠元素**: Network3, Network6 - 用于验证元素去重

### 特殊Fixture 1：跳跃恢复（稀疏分段）

**用途**: 验证跳跃检测与恢复

```
Segment 0.0: Item1, Item2
Segment 0.4: Item3, Item4    (大间隙，30%步长会跳跃)
Segment 0.7: Item5, Item6
Segment 1.0: Item7, Item8
```

**预期**: 30%步长触发跳跃 → Rollback → 15%重试 → 成功

### 特殊Fixture 2：自适应步长（高重叠）

**用途**: 验证自适应步长优化

```
Segment 0.0: Item1, Item2, Item3, Item4
Segment 0.2: Item4, Item5, Item6    (Item4重叠，3新+1重复 = 75%重复)
Segment 0.4: Item6, Item7, Item8    (Item6重叠)
Segment 0.6: Item8, Item9, Item10   (Item8重叠)
Segment 0.8: Item10, Item11, Item12 (Item10重叠)
Segment 1.0: Item12, Item13, Item14, Item15 (Item12重叠)
```

**预期**: 重复比例 >= 70% → 步长从 0.3 增长到 0.45

---

## 测试场景

| # | 场景名称 | Fixture | 验证重点 |
|---|---------|---------|---------|
| 1 | `WiFiList_ScrollThroughAllScreens_AllNetworksVisited` | 主Fixture | 7屏完整遍历，25元素全覆盖，6次向下滚动 |
| 2 | `WiFiList_ScrollBackToTop_ProgressRevertsCorrectly` | 主Fixture | 点击BackToSettings，向上滚动回顶部，进度正确回退 |
| 3 | `WiFiList_ElementDeduplication_OverlappingElementsVisitedOnce` | 主Fixture | Network3/6只访问一次，VisitedChildren正确跟踪 |
| 4 | `WiFiList_BoundaryConditions_TopAndBottomCorrect` | 主Fixture | 进度0.0/1.0边界正确处理，IsEndOfList准确 |
| 5 | `SparseList_JumpRecovery_AllElementsVisited` | 特殊Fixture1 | 跳跃检测→恢复，无遗漏，恢复成功率 |
| 6 | `OverlappingList_AdaptiveStep_StepSizeIncreases` | 特殊Fixture2 | 高重复触发步长增长，滚动效率提升 |

---

## ExpectedBehavior扩展

### 扩展的 numericAnchor 结构

```json
{
  "completion": { "success": true, "reason": "all_visited" },
  "pageCoverage": {
    "required": ["auto_derive"],
    "forbidden": []
  },
  "elementCoverage": {
    "required": ["auto_derive"],
    "requiredRatio": 0.95
  },
  "collisionProof": "auto_derive",
  "dfsProperties": {
    "rootFirst": true,
    "parentBeforeChild": true,
    "backAfterForward": true
  },
  "numericAnchor": {
    // ===== 现有字段 =====
    "totalSteps": 45,
    "visitedPagesCount": 1,
    "actionHistoryCount": 28,
    "elapsedSecondsMax": 6.0,

    // ===== 新增滚动特定字段 =====
    "scrollCount": 6,              // 向下滚动次数
    "scrollDistance": 1.0,         // 总滚动距离
    "scrollUpCount": 1,            // 向上滚动次数
    "jumpDetected": 0,             // 检测到跳跃次数
    "jumpRecovered": 0,            // 成功恢复跳跃次数
    "finalProgress": 1.0,           // 最终进度
    "adaptiveStepIncreases": 0     // 步长增长次数
  }
}
```

### 场景差异化 numericAnchor

不同场景有不同的滚动指标重点：

| 场景 | scrollCount | scrollUpCount | jumpDetected | adaptiveStepIncreases |
|------|-------------|---------------|--------------|----------------------|
| 全屏遍历 | 6 | 0 | 0 | 0 |
| 向上回退 | 6 | 1 | 0 | 0 |
| 元素去重 | 6 | 0 | 0 | 0 |
| 边界条件 | 6 | 0 | 0 | 0 |
| 跳跃恢复 | 4 | 0 | 1 | 0 |
| 自适应步长 | 5 | 0 | 0 | 2 |

---

## 代码结构

```csharp
/// <summary>
/// Scroll-Enabled Baseline Tests — 全场景滚动基线测试。
/// Spec reference: docs/system/layers/simulation-baseline.md §2
/// </summary>
public class ScrollableBaselineTests
{
    // ===== 共享Fixtures =====

    /// <summary>7屏WiFi列表主Fixture（25元素）</summary>
    private static StateFixture WiFiListFixture7Screens() => new StateFixtureBuilder()
        .Page("wifi_list", p => p.Name("Wi-Fi Settings"))
        .Build();

    /// <summary>主Fixture滚动数据（7屏递增，包含重叠）</summary>
    private static ScrollDataStore WiFiScrollData()
    {
        // Segment定义...
    }

    /// <summary>跳跃恢复场景Fixture（稀疏分段）</summary>
    private static ScrollDataStore SparseJumpData()
    {
        // 稀疏分段定义...
    }

    /// <summary>自适应步长场景Fixture（高重叠）</summary>
    private static ScrollDataStore OverlappingAdaptiveData()
    {
        // 高重叠分段定义...
    }

    // ===== 共享Helpers =====

    private static TraversalNode CreateWiFiListRoot() { ... }
    private static TraversalEngine CreateScrollableEngine(...) { ... }
    private static ExpectedBehavior LoadScrollExpectedBehavior(...) { ... }

    // ===== 测试场景 =====

    /// <summary>WiFi列表全屏遍历 — 7屏滚动遍历所有网络按钮。</summary>
    [Fact]
    public void WiFiList_ScrollThroughAllScreens_AllNetworksVisited() { ... }

    /// <summary>WiFi列表向上回退 — 点击BackToSettings，向上滚动回顶部。</summary>
    [Fact]
    public void WiFiList_ScrollBackToTop_ProgressRevertsCorrectly() { ... }

    /// <summary>元素去重验证 — Network3/6只访问一次。</summary>
    [Fact]
    public void WiFiList_ElementDeduplication_OverlappingElementsVisitedOnce() { ... }

    /// <summary>边界条件验证 — 顶部/底部进度正确。</summary>
    [Fact]
    public void WiFiList_BoundaryConditions_TopAndBottomCorrect() { ... }

    /// <summary>跳跃恢复验证 — 稀疏分段跳跃检测与恢复。</summary>
    [Fact]
    public void SparseList_JumpRecovery_AllElementsVisited() { ... }

    /// <summary>自适应步长验证 — 高重复触发步长增长。</summary>
    [Fact]
    public void OverlappingList_AdaptiveStep_StepSizeIncreases() { ... }
}
```

---

## 文档更新策略

### A. simulation-baseline.md

添加 §2 滚动场景章节，与现有 §1 并列：

```markdown
## 2. 滚动基线场景 (Scroll-Enabled Baselines)

### 2.1 WiFi列表全屏遍历
7屏WiFi列表，25个网络按钮，6次滚动操作。

### 2.2 向上滚动回退
点击顶部BackToSettings按钮，向上滚动回顶部，验证进度回退。

### 2.3 元素去重验证
Network3/6在相邻屏重复，验证只访问一次。

### 2.4 边界条件
验证进度0.0/1.0边界，IsEndOfList准确计算。

### 2.5 跳跃检测与恢复
稀疏分段场景，验证跳跃检测→恢复流程，无元素遗漏。

### 2.6 自适应步长优化
高重叠分段场景，验证步长自适应增长，滚动效率提升。
```

### B. XML注释

每个测试方法添加详细XML注释：

```csharp
/// <summary>
/// WiFi列表全屏遍历 — 7屏滚动遍历所有网络按钮。
/// 
/// 验证点：
///   - 所有25个网络元素访问（包含Network3/6重叠去重）
///   - 6次向下滚动操作
///   - 最终进度 = 1.0（到底）
///   - 无跳跃检测（步长适中）
/// 
/// ExpectedBehavior: wifi-list-scroll-all-screens.json
/// Spec reference: simulation-baseline.md §2.1
/// </summary>
[Fact]
public void WiFiList_ScrollThroughAllScreens_AllNetworksVisited()
```

### C. 滚动基线概览文档

新建或更新 `docs/system/layers/simulation-baseline.md` 对比章节：

```markdown
## 滚动基线 vs 非滚动基线对比

| 维度 | SimulationBaselineTests | ScrollableBaselineTests |
|------|-------------------------|-------------------------|
| Fixture类型 | 7页Settings App | 7屏WiFi列表 |
| 页面数 | 7页（静态跳转） | 1页（滚动分段） |
| 元素数 | ~30 | 25 |
| 滚动支持 | 否 | 是（ScrollableMockVisionService） |
| 验证重点 | 页面跳转、DFS遍历 | 滚动遍历、元素去重、跳跃恢复 |
| 向上滚动 | 否 | 是（BackToSettings） |
| 特殊场景 | 目标搜索 | 跳跃恢复、自适应步长 |
```

---

## 依赖检查

### 当前状态

| 组件 | 状态 | 备注 |
|------|------|------|
| ScrollableMockVisionService | ✅ 已实现 | `src/UniClaw.Core/Simulation/Scroll/` |
| ScrollableMockActionExecutor | ✅ 已实现 | `src/UniClaw.Core/Simulation/Scroll/` |
| ScrollDataStore | ✅ 已实现 | `src/UniClaw.Core/Simulation/Scroll/` |
| ScrollHandler | ✅ 已实现 | `src/UniClaw.Core/StateMachine/Scroll/` |
| ExpectedBehavior | ✅ 已实现 | `src/UniClaw.Core/Simulation/ExpectedBehavior/` |
| TraversalResult | ⚠️ 需确认 | 需检查属性定义 |

### TraversalResult 属性需求

测试需要访问以下属性：
- `Completed` - 遍历是否完成
- `StepCount` - 总步数
- `VisitedElements` - 已访问元素集合
- `VisitedPages` - 已访问页面集合
- `FoundTarget` - 是否找到目标（TargetFound场景）

**需确认**: TraversalResult 是否包含这些属性，或者需要通过其他方式获取。

---

## 实施步骤

### Phase 1: 依赖确认 (P0)

1. **检查 TraversalResult 定义**
   - 确认可用属性
   - 如不匹配，调整测试断言或扩展类型

2. **确认滚动集成点**
   - 检查 TraversalEngine 是否已集成 ScrollHandler
   - 或是否需要在测试中手动触发滚动

### Phase 2: 基础实现 (P1)

1. **创建测试文件**
   - `tests/UniClaw.Core.Tests/Baseline/ScrollableBaselineTests.cs`

2. **实现主Fixture**
   - WiFiListFixture7Screens()
   - WiFiScrollData() - 7屏分段数据

3. **实现 Scenario 1**
   - WiFiList_ScrollThroughAllScreens_AllNetworksVisited
   - 验证基本滚动遍历

4. **创建 ExpectedBehavior JSON**
   - `wifi-list-scroll-all-screens.json`

5. **运行并调整**
   - 初次运行获取实际值
   - 调整 numericAnchor

### Phase 3: 主场景扩展 (P2)

1. **实现 Scenario 2-4**（基于主Fixture）
   - WiFiList_ScrollBackToTop
   - WiFiList_ElementDeduplication
   - WiFiList_BoundaryConditions

2. **创建对应 ExpectedBehavior JSON**

### Phase 4: 特殊场景 (P3)

1. **实现 Scenario 5-6**（特殊Fixtures）
   - SparseList_JumpRecovery
   - OverlappingList_AdaptiveStep

2. **创建特殊Fixture数据**
   - SparseJumpData()
   - OverlappingAdaptiveData()

3. **创建对应 ExpectedBehavior JSON**

### Phase 5: 文档更新 (P4)

1. **更新 simulation-baseline.md**
   - 添加 §2 滚动场景章节
   - 添加对比表格

2. **添加 XML注释**
   - 每个测试方法的详细文档

3. **更新概览文档**
   - 滚动 vs 非滚动基线对比

---

## 风险与缓解

| 风险 | 影响 | 缓解 |
|------|------|------|
| TraversalResult 属性不匹配 | 需调整测试断言 | Phase 1 先确认类型定义 |
| 滚动未自动集成到 TraversalEngine | 测试需手动触发 | 设计支持手动触发的测试模式 |
| ExpectedBehavior 扩展需求 | JSON schema 不支持 | numericAnchor已预留扩展空间 |

---

## 参考文档

- `docs/system/layers/simulation-baseline.md` - 基线测试规格
- `tests/UniClaw.Core.Tests/Baseline/SimulationBaselineTests.cs` - 现有基线测试
- `tests/UniClaw.Core.Tests/Simulation/Scroll/ScrollScenarioTests.cs` - 滚动场景测试
- `openspec/changes/archive/2026-07-12-scroll-simulation-enhancement/` - 滚动增强实现
- Decision Log: D-32 ~ D-37 (滚动相关架构决策)
