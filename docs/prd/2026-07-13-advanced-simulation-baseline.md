# Advanced Simulation Baseline — Design Document

> **版本**: 1.0
> **日期**: 2026-07-13
> **状态**: 设计完成，待实施
> **来源**: brainstorming 流程

---

## 1. 背景与目标

### 1.1 现状

当前基线测试体系包含 8 个场景：
- **SimulationBaselineTests.cs**: 2 个场景（7 页 Settings 应用）
- **ScrollableBaselineTests.cs**: 6 个场景（WiFi 列表，最大 25 项）

### 1.2 需求

创建更高阶的仿真测试基线，覆盖更复杂的真实场景：
- **4 层级导航**：比当前 2-3 层更深的导航层级
- **20-30 项滚动列表**：比当前 6-25 项更长的列表
- **多页面滚动**：单个遍历中多个可滚动页面

### 1.3 目标

1. **压力测试 DFS 遍历**：更深的层级暴露回退导航和状态恢复的边界情况
2. **验证滚动可靠性**：20-30 项列表验证滚动检测、跳跃恢复和自适应步长行为
3. **多页面滚动支持**：验证遍历过程中处理多个独立可滚动页面的能力
4. **保持基线稳定性**：所有新场景遵循 ExpectedBehavior 驱动验证，CI-blocking

### 1.4 非目标

- 弹窗/对话框交互行为测试（数据结构就绪，场景延后到 Phase 3）
- 性能基准测试
- 视觉回归测试

---

## 2. 架构设计

### 2.1 文件结构

```
tests/UniClaw.Core.Tests/Baseline/
  ├── SimulationBaselineTests.cs          (existing: 2 scenarios)
  ├── ScrollableBaselineTests.cs          (existing: 6 scenarios)
  ├── HierarchyBaselineTests.cs          (new: 4 scenarios, 4-level nav)
  └── LongListBaselineTests.cs           (new: 3 scenarios, 20-30 item lists)

tests/UniClaw.Core.Tests/Baseline/Fixtures/expected/
  ├── hierarchy/
  │   ├── hierarchy-full-traversal.json
  │   ├── hierarchy-target-search.json
  │   ├── hierarchy-multi-scroll.json
  │   └── hierarchy-scroll-deep-back.json
  ├── long-list/
  │   ├── long-list-full-traversal.json
  │   ├── sparse-list-full-traversal.json
  │   └── dense-list-full-traversal.json
  └── (existing directories unchanged)
```

### 2.2 测试类概览

| 测试类 | 场景数 | 核心验证点 | 滚动支持 |
|--------|-------|-----------|---------|
| HierarchyBaselineTests | 4 | 4 层级导航、多页面滚动 | ✅ 3 个可滚动页面 |
| LongListBaselineTests | 3 | 长列表完整遍历 | ✅ 单页面 20-30 项 |

### 2.3 数据流

```
StateFixture (12 pages) + ScrollDataStore (3 scrollable pages)
    ↓
ScrollableMockVisionService + ScrollableMockActionExecutor
    ↓
TraversalEngine.Run() → TraversalResult
    ↓
ExpectedBehavior.Verify(result) → VerificationReport
    ↓
Assert.True(report.AllPassed) + BaselineReportCollector
```

---

## 3. HierarchyBaselineTests — 4 层级导航场景

### 3.1 测试类

**文件**: `tests/UniClaw.Core.Tests/Baseline/HierarchyBaselineTests.cs`
**Collection**: `"Baseline Tests"` (共享 BaselineReportCollector)

### 3.2 Fixture: 4 层级 "Advanced Settings" 应用

```
Level 0: home                    (6 个菜单项)
Level 1: network, apps, privacy, storage       (4 个页面)
Level 2: network → wifi, bluetooth, data_usage
         apps → installed_apps, running_apps
         privacy → permissions, location_history
Level 3: wifi → network_list           🔸 可滚动 (25 个网络)
         installed_apps → app_list    🔸 可滚动 (30 个应用)
         permissions → perm_list      🔸 可滚动 (20 个权限)
         data_usage → usage_details   (5 个静态项)
         location_history → history_log (5 个静态项)
```

**总计**: 12 个页面，3 个可滚动页面

### 3.3 场景定义

| # | 场景名 | 描述 | 关键验证点 |
|---|--------|------|-----------|
| 1 | Full Traversal | DFS 遍历所有 4 层级，3 个可滚动页面 | 所有 12 页访问，75+ 唯一元素，scroll_count ≥ 15 |
| 2 | Target Search (Level 3) | 在第 3 层找到目标元素 | 在 app_list 中找到目标，提前终止，8 页访问 |
| 3 | Multi-Scroll Traversal | 访问所有 3 个可滚动页面 | 3 个独立滚动会话，scroll_count ≥ 15 |
| 4 | Scroll + Deep Back | 滚动后多层返回 | 滚动 app_list，然后 3 步返回 home |

### 3.4 滚动数据

```csharp
private static ScrollDataStore AdvancedHierarchyScrollData()
{
    var builder = ScrollDataStore.CreateBuilder();
    builder.Add("network_list", CreateWiFiListSegments());     // 25 项，6 段
    builder.Add("app_list", CreateAppListSegments());         // 30 项，8 段
    builder.Add("perm_list", CreatePermListSegments());       // 20 项，5 段
    return builder.Build();
}
```

### 3.5 共享 Root Node

```csharp
private static TraversalNode CreateHierarchyRoot() => new TraversalNode(
    NodeId: "root",
    Name: "Advanced Settings",
    NodeType: NodeType.Container,
    Operation: new Operation(OperationType.NoAction),
    ChildrenStrategy: new ChildrenStrategy(
        ChildrenStrategyType.DynamicMatch,
        DynamicRules: new Dictionary<string, DynamicRule>
        {
            ["button_rule"] = new DynamicRule(
                RuleId: "button_rule",
                MatchCondition: new MatchCondition(Type: "button"),
                ChildTemplate: "button_leaf",
                Action: MatchAction.GenerateChild),
        }),
    ExitCondition: new ExitCondition(
        ExitConditionType.AllChildrenVisited,
        Fallback: FallbackAction.AutoEscape));
```

---

## 4. LongListBaselineTests — 长列表完整遍历场景

### 4.1 测试类

**文件**: `tests/UniClaw.Core.Tests/Baseline/LongListBaselineTests.cs`
**Collection**: `"Baseline Tests"` (共享 BaselineReportCollector)

### 4.2 Fixture: 单页面长列表

| Fixture | 项数 | 段数 | 特征 |
|---------|------|------|------|
| LongListFixture | 30 项 | 8 段 | 均匀分布，15% 重叠 |
| SparseLongListFixture | 25 项 | 6 段 | 大间隙（40%+），触发跳跃恢复 |
| DenseLongListFixture | 20 项 | 10 段 | 高重叠（80%+），触发自适应步长 |

### 4.3 场景定义

| # | 场景名 | Fixture | 描述 | 关键验证点 |
|---|--------|--------|------|-----------|
| 1 | Long List Full Traversal | LongListFixture (30 项) | 完整遍历 30 项列表 | 所有 30 项访问，scroll_count ≥ 7，final_progress = 1.0 |
| 2 | Sparse List Full Traversal | SparseLongListFixture (25 项) | 大间隙跳跃恢复 | 所有 25 项访问，jump_detected ≥ 2，jump_recovered ≥ 2 |
| 3 | Dense List Full Traversal | DenseLongListFixture (20 项) | 高重叠自适应步长 | 所有 20 项访问，adaptive_step_increases ≥ 3 |

### 4.4 滚动数据示例

```csharp
// 30 项，8 段（均匀分布）
private static ScrollDataStore LongListScrollData()
{
    var builder = ScrollDataStore.CreateBuilder();
    builder.Add("long_list", new ScrollSegment(0.0, CreateItems(1, 5)));
    builder.Add("long_list", new ScrollSegment(0.15, CreateItems(5, 9)));   // item 5 重叠
    builder.Add("long_list", new ScrollSegment(0.30, CreateItems(9, 13)));
    builder.Add("long_list", new ScrollSegment(0.45, CreateItems(13, 17)));
    builder.Add("long_list", new ScrollSegment(0.60, CreateItems(17, 21)));
    builder.Add("long_list", new ScrollSegment(0.75, CreateItems(21, 25)));
    builder.Add("long_list", new ScrollSegment(0.90, CreateItems(25, 29)));
    builder.Add("long_list", new ScrollSegment(1.0, CreateItems(29, 30)));
    return builder.Build();
}
```

---

## 5. ExpectedBehavior JSON 文件

### 5.1 目录结构

```
tests/UniClaw.Core.Tests/Baseline/Fixtures/expected/
  ├── hierarchy/
  │   ├── hierarchy-full-traversal.json
  │   ├── hierarchy-target-search.json
  │   ├── hierarchy-multi-scroll.json
  │   └── hierarchy-scroll-deep-back.json
  └── long-list/
      ├── long-list-full-traversal.json
      ├── sparse-list-full-traversal.json
      └── dense-list-full-traversal.json
```

### 5.2 JSON 结构示例

```json
{
  "scenario": "hierarchy-full-traversal",
  "description": "4-level hierarchy full traversal with 3 scrollable pages",
  "completion": {
    "success": true,
    "reason": "all_visited",
    "finalState": null
  },
  "pageCoverage": {
    "required": ["home", "network", "wifi", "network_list", "apps", "installed_apps", "app_list", "privacy", "permissions", "perm_list", "storage", "data_usage"],
    "forbidden": []
  },
  "elementCoverage": {
    "required": ["menu_network", "menu_wifi", "network_1", "network_2", "...", "app_1", "app_2", "...", "perm_1", "perm_2", "..."],
    "requiredRatio": 0.95
  },
  "collisionProof": [],
  "dfsProperties": {
    "rootFirst": true,
    "parentBeforeChild": true,
    "backAfterForward": true
  },
  "numericAnchor": {
    "totalSteps": 80,
    "visitedPagesCount": 12,
    "actionHistoryCount": 45,
    "elapsedSecondsMax": 0.1,
    "scrollCount": 15,
    "scrollDistance": 0.0,
    "scrollUpCount": 0,
    "jumpDetected": 0,
    "jumpRecovered": 0,
    "finalProgress": 0.0,
    "adaptiveStepIncreases": 0,
    "_note": "层级场景包含多个可滚动页面，finalProgress 设为 0.0 表示不适用"
  }
}
```

### 5.3 生成流程

1. 运行测试一次获取实际基线值
2. 使用实际值创建 JSON 文件
3. 验证测试通过新的 JSON 文件
4. 提交 JSON 到仓库

---

## 6. ExpectedBehavior 限制与约定

### 6.1 多页面滚动场景的指标聚合

由于 `NumericAnchor` 只有一份滚动指标，层级场景需要以下约定：

| 指标 | 约定 |
|------|------|
| **FinalProgress** | 层级场景设为 0.0（表示不适用），添加 `_note` 说明 |
| **ScrollCount** | 所有可滚动页面的滚动次数总和 |
| **ScrollDistance** | 不适用（多页面），设为 0.0 |
| **JumpDetected** | 所有可滚动页面的跳跃检测总和 |
| **JumpRecovered** | 所有可滚动页面的跳跃恢复总和 |

### 6.2 ElementCoverage 与滚动数据

由于滚动列表元素在 `ScrollDataStore` 中而非 `StateFixture` 中：

- **LongListBaselineTests**: ElementCoverage.Required 需手动列出所有元素（不支持 auto_derive）
- **HierarchyBaselineTests**: 滚动元素需手动列出，固定 fixture 元素可使用 auto_derive

### 6.3 GetScrollCount 语义

`ScrollableMockActionExecutor.GetScrollCount(string pageId)` 当前忽略 pageId 参数，返回总滚动次数。此设计合理用于聚合指标。

---

## 7. 文档更新

### 7.1 simulation-baseline.md

**新增 §4: Advanced Baseline Scenarios**

```markdown
## §4. Advanced Baseline Scenarios (Phase 2)

> **新增 (2026-07-13)**: 更高阶的仿真测试基线，覆盖更复杂的真实场景。
> 包含 4 层级导航 + 20-30 项滚动列表 + 多页面滚动。

### §4.1 HierarchyBaselineTests — 4 层级导航

**测试类**: `tests/UniClaw.Core.Tests/Baseline/HierarchyBaselineTests.cs`
**场景数**: 4
**Fixture**: 12 页 "Advanced Settings" 应用，3 个可滚动页面

| 场景 | 验证点 | 基线数值 |
|------|--------|----------|
| Full Traversal | 所有 12 页访问，3 个可滚动页面遍历完成 | steps: 80+, pages: 12, scroll_count: 15+ |
| Target Search (Level 3) | 在第 3 层找到目标元素，提前终止 | steps: 40+, pages: 8, target_found: true |
| Multi-Scroll Traversal | 访问所有 3 个可滚动页面 | scroll_count: 15+, 3 个独立滚动会话 |
| Scroll + Deep Back | 滚动后多层返回 | scroll_count: 5+, back_count: 3 |

### §4.2 LongListBaselineTests — 长列表遍历

**测试类**: `tests/UniClaw.Core.Tests/Baseline/LongListBaselineTests.cs`
**场景数**: 3
**Fixture**: 单页面可滚动列表（20-30 项）

| 场景 | 验证点 | 基线数值 |
|------|--------|----------|
| Long List Full Traversal | 30 项全部访问 | scroll_count: 7+, items: 30, final_progress: 1.0 |
| Sparse List Full Traversal | 25 项全部访问，有跳跃恢复 | scroll_count: 5+, jump_detected: 2+, jump_recovered: 2+ |
| Dense List Full Traversal | 20 项全部访问，高重叠自适应步长 | scroll_count: 9+, adaptive_step_increases: 3+ |

### §4.3 ExpectedBehavior 限制说明

**多页面滚动场景的指标聚合：**

1. **FinalProgress**: 层级场景设为 0.0（表示不适用）
2. **ScrollCount**: 表示所有可滚动页面的滚动次数总和
3. **ElementCoverage**: 滚动列表元素需手动列出（不支持 auto_derive）

### §4.4 未来扩展

- **弹窗场景** (Phase 3): 确认对话框、权限请求、多弹窗序列
- **性能基线** (Phase 4): 长时间运行的稳定性指标
```

### 7.2 decisions/log.md

```markdown
| D-18 | Advanced Simulation Baseline 架构 | ✅ Design | 2026-07-13 |
|    | 选择: 按 Complexity Dimension 分离（Approach B） | | |
|    | 理由: 清晰的关注点分离，遵循现有模式（Simulation vs Scrollable） | | |
|    | 测试类: HierarchyBaselineTests (4 场景) + LongListBaselineTests (3 场景) | | |
|    | 约定: 层级场景 FinalProgress=0.0，滚动元素手动列出 | | |
```

---

## 8. 实施阶段

### Phase 1: 核心结构 (P0)

| # | 任务 | 文件 |
|---|------|------|
| 1 | 创建 `HierarchyBaselineTests.cs` 骨架 | tests/.../HierarchyBaselineTests.cs |
| 2 | 创建 `LongListBaselineTests.cs` 骨架 | tests/.../LongListBaselineTests.cs |
| 3 | 实现 4 层级 StateFixture | HierarchyBaselineTests.cs |
| 4 | 实现 3 个长列表 ScrollDataStore | LongListBaselineTests.cs |

### Phase 2: 场景实现 (P1)

| # | 任务 | 场景 |
|---|------|------|
| 1 | Hierarchy 场景 1: Full Traversal | 12 页，3 滚动 |
| 2 | Hierarchy 场景 2: Target Search (Level 3) | 提前终止 |
| 3 | Hierarchy 场景 3: Multi-Scroll Traversal | 3 滚动页面 |
| 4 | Hierarchy 场景 4: Scroll + Deep Back | 深层返回 |
| 5 | LongList 场景 1: 30 项完整遍历 | 30 项，8 段 |
| 6 | LongList 场景 2: 25 项稀疏列表跳跃恢复 | 25 项，大间隙 |
| 7 | LongList 场景 3: 20 项密集列表自适应步长 | 20 项，高重叠 |

### Phase 3: 基线值捕获 (P1)

| # | 任务 | 输出 |
|---|------|------|
| 1 | 运行所有 7 个场景 | 实际运行结果 |
| 2 | 创建 7 个 ExpectedBehavior JSON 文件 | hierarchy/ + long-list/ |
| 3 | 验证所有测试通过 | 全绿 |
| 4 | 生成基线报告 | BaselineReportCollector |

### Phase 4: 文档同步 (P2)

| # | 任务 | 文件 |
|---|------|------|
| 1 | 更新 `simulation-baseline.md` | 添加 §4 |
| 2 | 更新 `decisions/log.md` | 添加 D-18 |

---

## 9. 风险与缓解

| 风险 | 影响 | 缓解 |
|------|------|------|
| ExpectedBehavior 限制导致验证不完整 | 某些滚动指标无法验证 | 文档约定，Accept 当前限制 |
| 多页面滚动状态管理复杂 | 滚动状态可能混淆 | ScrollableMockVisionService 已支持每页面独立状态 |
| 基线值不稳定 | 引擎改动导致频繁更新基线 | NumericAnchor 设为 informational，±5% tolerance |

---

## 10. 与现有基线的关系

| 维度 | SimulationBaselineTests | ScrollableBaselineTests | HierarchyBaselineTests | LongListBaselineTests |
|------|------------------------|-------------------------|------------------------|----------------------|
| **场景数** | 2 | 6 | 4 | 3 |
| **Fixture** | 7+2 页 Settings | 单页滚动 | 12 页层级 | 单页长列表 |
| **层级深度** | 2-3 层 | 1 层 | 4 层 | 1 层 |
| **滚动页面** | 0 | 1（每场景） | 3（单场景） | 1（每场景） |
| **最大项数** | N/A | 25 项 | 75 项（3×25） | 30 项 |
| **验证重点** | DFS 完整性、目标搜索 | 滚动机制 | 深层导航、多页面滚动 | 长列表稳定性 |
| **CI-blocking** | ✅ | ✅ | ✅ | ✅ |

---

## 11. 成功标准

- [ ] 7 个新场景全部实现并测试通过
- [ ] ExpectedBehavior JSON 文件创建（7 个）
- [ ] 基线报告生成包含所有 15 个场景（8 现有 + 7 新）
- [ ] `simulation-baseline.md` 更新完成
- [ ] `decisions/log.md` 更新完成
- [ ] 所有测试集成到 BaselineReportCollector

---

## 附录 A: 设计决策记录

| 决策点 | 选择 | 理由 |
|--------|------|------|
| 文件组织 | 按 Complexity Dimension 分离（Approach B） | 清晰的关注点分离，遵循现有模式 |
| 命名 | HierarchyBaselineTests + LongListBaselineTests | 描述性强，避免"Advanced"模糊性 |
| FinalProgress（层级） | 设为 0.0 + 注释 | 当前结构限制，合理约定 |
| ElementCoverage（滚动） | 手动列出元素 | ScrollDataStore 不在 auto_derive 范围内 |
| 弹窗支持 | 延后到 Phase 3 | 数据结构就绪，场景非当前优先级 |

## 附录 B: 文件变更索引

| 操作 | 文件 | 行数估计 |
|------|------|---------|
| 新增 | `tests/.../HierarchyBaselineTests.cs` | ~300 |
| 新增 | `tests/.../LongListBaselineTests.cs` | ~250 |
| 新增 | `tests/.../Fixtures/expected/hierarchy/*.json` | 4 文件 |
| 新增 | `tests/.../Fixtures/expected/long-list/*.json` | 3 文件 |
| 修改 | `docs/system/layers/simulation-baseline.md` | +100 行 |
| 修改 | `docs/system/decisions/log.md` | +5 行 |
| 新增 | `docs/prd/2026-07-13-advanced-simulation-baseline.md` | 本文档 |
