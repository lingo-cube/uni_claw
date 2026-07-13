# Proposal: Advanced Simulation Baseline

## Why

当前基线测试体系（8 个场景）覆盖了基础的 2-3 层级导航和 6-25 项滚动列表，但无法验证更复杂真实场景下的遍历行为。需要更高阶的基线测试来：

1. **压力测试 DFS 遍历**：4 层级导航暴露深层回退导航和状态恢复的边界情况
2. **验证滚动可靠性**：20-30 项列表验证跳跃恢复和自适应步长在更长列表下的表现
3. **多页面滚动支持**：验证单个遍历过程中处理多个独立可滚动页面的能力

## What Changes

- **新增 HierarchyBaselineTests.cs**：4 个场景测试 4 层级导航（12 页，3 个可滚动页面）
  - Full Traversal：DFS 遍历所有层级和可滚动页面
  - Target Search (Level 3)：深层目标搜索，提前终止
  - Multi-Scroll Traversal：访问所有 3 个可滚动页面
  - Scroll + Deep Back：滚动后多层返回

- **新增 LongListBaselineTests.cs**：3 个场景测试 20-30 项长列表
  - Long List Full Traversal：30 项完整遍历
  - Sparse List Full Traversal：25 项稀疏列表，跳跃恢复
  - Dense List Full Traversal：20 项密集列表，自适应步长

- **新增 ExpectedBehavior JSON 文件**：7 个场景的预期行为定义
  - `hierarchy/` 目录：4 个 JSON 文件
  - `long-list/` 目录：3 个 JSON 文件

- **更新文档**：
  - `simulation-baseline.md`：新增 §4 Advanced Baseline Scenarios
  - `decisions/log.md`：记录 D-18 架构决策

## Capabilities

### New Capabilities

- `hierarchy-baseline`: 4 层级导航基线测试，验证深层 DFS 遍历、多页面滚动状态管理、多层返回导航
- `long-list-baseline`: 长列表滚动基线测试，验证 20-30 项列表的完整遍历、跳跃恢复、自适应步长

### Modified Capabilities

无。此变更为新增测试代码，不修改现有功能规格。

## Impact

**代码变更**：
- `tests/UniClaw.Core.Tests/Baseline/HierarchyBaselineTests.cs`（新增，~300 行）
- `tests/UniClaw.Core.Tests/Baseline/LongListBaselineTests.cs`（新增，~250 行）
- `tests/UniClaw.Core.Tests/Baseline/Fixtures/expected/hierarchy/*.json`（新增，4 文件）
- `tests/UniClaw.Core.Tests/Baseline/Fixtures/expected/long-list/*.json`（新增，3 文件）

**文档变更**：
- `docs/system/layers/simulation-baseline.md`（新增 §4）
- `docs/system/decisions/log.md`（新增 D-18）

**依赖**：
- 使用现有 `ScrollableMockVisionService` 和 `ScrollableMockActionExecutor`
- 遵循现有 `ExpectedBehavior` 驱动验证模式
- 集成到现有 `BaselineReportCollector`

**CI 影响**：
- 新增 7 个基线测试，均为 CI-blocking
- 基线报告将包含 15 个场景（8 现有 + 7 新）

**向后兼容**：
- 完全向后兼容，仅新增测试
- 不修改任何生产代码或现有测试
