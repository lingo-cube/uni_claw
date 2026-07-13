# Design: Advanced Simulation Baseline

## Context

**Current State:**
- 基线测试体系包含 8 个场景（2 个多页面 + 6 个单页面滚动）
- SimulationBaselineTests: 2 个场景覆盖 7 页 Settings 应用
- ScrollableBaselineTests: 6 个场景覆盖最大 25 项的滚动列表
- 使用 ExpectedBehavior 驱动验证，所有测试 CI-blocking

**Constraints:**
- 必须遵循现有的 ExpectedBehavior 驱动验证模式
- 必须集成到现有的 BaselineReportCollector 报告系统
- 不修改生产代码，仅新增测试代码
- ExpectedBehavior 结构限制：NumericAnchor 只有一份滚动指标（FinalProgress 无法表示多页面滚动）

**Stakeholders:**
- 基线测试使用者：需要可靠的回归检测
- CI 系统：需要快速、稳定的测试执行

## Goals / Non-Goals

**Goals:**
1. 实现 4 层级导航基线测试，暴露深层 DFS 遍历问题
2. 实现 20-30 项长列表滚动测试，验证滚动机制在更长列表下的表现
3. 支持多页面滚动场景（单个遍历中访问多个可滚动页面）
4. 保持基线稳定性，所有测试 ExpectedBehavior 驱动验证

**Non-Goals:**
- 弹窗/对话框交互行为测试（数据结构就绪，场景延后到 Phase 3）
- 性能基准测试
- 视觉回归测试
- 修改现有基线测试或生产代码

## Decisions

### 1. 测试类组织：按 Complexity Dimension 分离

**选择：** 创建两个独立的测试类（HierarchyBaselineTests + LongListBaselineTests）

**理由：**
- 遵循现有模式（SimulationBaselineTests vs ScrollableBaselineTests）
- 清晰的关注点分离：深层导航 vs 长列表滚动
- 每个文件职责单一，易于维护和扩展

**考虑过的替代方案：**
- 合并为单个 AdvancedBaselineTests.cs：文件会变得过大，混合关注点
- 按场景数量分配到现有类：会破坏现有类的清晰边界

### 2. 层级深度：4 层级

**选择：** 12 页 "Advanced Settings" 应用，4 层级深度

**理由：**
- 比现有 2-3 层更深，能暴露深层返回导航问题
- 3 个可滚动页面分布在第 3 层，验证多页面滚动状态管理
- 总共 75 个滚动元素（3 × 25），足够复杂但不冗余

**层级结构：**
```
Level 0: home (6 个菜单项)
Level 1: network, apps, privacy, storage
Level 2: network → wifi, bluetooth, data_usage
         apps → installed_apps, running_apps
         privacy → permissions, location_history
Level 3: wifi → network_list (25 项)
         installed_apps → app_list (30 项)
         permissions → perm_list (20 项)
```

### 3. ExpectedBehavior 限制处理

**问题：** NumericAnchor 只有一个 FinalProgress 字段，无法表示多页面滚动场景

**选择：** 层级场景设置 FinalProgress = 0.0，添加 `_note` 说明不适用

**理由：**
- 不破坏现有 ExpectedBehavior 结构（避免 schema 变更）
- 通过约定解决限制，文档化行为
- 保持 JSON 结构简单一致

**约定：**
```json
{
  "numericAnchor": {
    "finalProgress": 0.0,
    "_note": "层级场景包含多个可滚动页面，finalProgress 设为 0.0 表示不适用"
  }
}
```

### 4. ElementCoverage 与滚动数据

**问题：** 滚动列表元素在 ScrollDataStore 中，不在 StateFixture 中，无法使用 auto_derive

**选择：** 手动列出所有滚动元素在 ElementCoverage.Required 中

**理由：**
- WithFixtureDerivation 只能从 StateFixture 推导
- 扩展推导逻辑会增加复杂度
- 20-30 个元素手动列出是可控的

**影响：**
- LongListBaselineTests 的 ElementCoverage.Required 需完整列出所有元素
- HierarchyBaselineTests 的滚动元素需手动列出，固定 fixture 元素可使用 auto_derive

### 5. 滚动数据设计

**选择：** 每个场景使用独立的 ScrollDataStore，按 pageId 组织

**理由：**
- ScrollDataStore 已支持多页面（Dictionary<string, ImmutableArray<ScrollSegment>>）
- ScrollableMockVisionService 已支持每页面独立的滚动状态（Dictionary<string, ScrollState>）
- 复用现有基础设施，无需新增代码

**层级场景滚动数据：**
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

### 6. 长列表场景设计

**选择：** 3 个场景覆盖不同滚动特性

**理由：**
- Long List (30 项)：验证基础完整遍历
- Sparse List (25 项，大间隙)：验证跳跃恢复
- Dense List (20 项，高重叠)：验证自适应步长

**段数设计：**
| 场景 | 项数 | 段数 | 特征 |
|------|------|------|------|
| Long List | 30 | 8 | 均匀分布，15% 重叠 |
| Sparse List | 25 | 6 | 大间隙（40%+）触发跳跃 |
| Dense List | 20 | 10 | 高重叠（80%+）触发自适应 |

## Risks / Trade-offs

### Risk 1: 基线值不稳定

**风险：** 引擎改动导致滚动行为变化，基线值需要频繁更新

**缓解：**
- NumericAnchor 设为 informational（±5% tolerance），不 CI-blocking
- 验证重点在 completion 和 coverage，不在精确数值

### Risk 2: 多页面滚动状态管理复杂

**风险：** ScrollableMockVisionService 的每页面状态可能存在边界情况

**缓解：**
- 滚动状态已通过 Dictionary<string, ScrollState> 实现每页面隔离
- 现有 ScrollableBaselineTests 已验证单页面场景
- 新增测试会暴露多页面边界问题

### Risk 3: ExpectedBehavior JSON 文件维护成本

**风险：** 7 个新 JSON 文件增加维护负担

**缓解：**
- 遵循现有模式，与 8 个现有 JSON 文件一致
- 使用 auto_derive where possible（减少手动维护）
- 文档化约定，降低理解成本

### Trade-off: 测试执行时间

**权衡：** 新增 7 个场景会增加测试执行时间

**平衡：**
- Mock 环境执行速度快（单场景 < 100ms）
- 总影响预计 < 1 秒，可接受
- CI-blocking 测试的价值超过执行时间成本

## Migration Plan

**部署步骤：**
1. 创建测试文件和 ExpectedBehavior JSON（本次变更）
2. 运行测试捕获基线值
3. 提交代码和基线值到仓库
4. CI 自动集成新测试

**回滚策略：**
- 移除新增测试文件
- 从 BaselineReportCollector 中移除新场景引用
- 回滚文档更新

**无迁移影响：** 此变更仅新增测试，不影响生产代码或现有测试

## Open Questions

**无。** 所有设计决策已明确，ExpectedBehavior 限制通过约定解决。
