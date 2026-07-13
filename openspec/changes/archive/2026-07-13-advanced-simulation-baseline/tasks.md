# Implementation Tasks

## 1. 核心结构搭建

- [x] 1.1 创建 `tests/UniClaw.Core.Tests/Baseline/HierarchyBaselineTests.cs` 文件骨架
- [x] 1.2 创建 `tests/UniClaw.Core.Tests/Baseline/LongListBaselineTests.cs` 文件骨架
- [x] 1.3 添加 `[Collection("Baseline Tests")]` 特性和 BaselineReportCollector 集成

## 2. HierarchyBaselineTests - Fixture 实现

- [x] 2.1 实现 `AdvancedSettingsFixture()` - 12 页 4 层级 StateFixture
  - Level 0: home (6 个菜单项)
  - Level 1: network, apps, privacy, storage (4 个页面)
  - Level 2: wifi, bluetooth, data_usage, installed_apps, running_apps, permissions, location_history
  - Level 3: network_list, app_list, perm_list (可滚动), usage_details, history_log (静态)
- [x] 2.2 添加 11 个页面间 transition 定义
- [x] 2.3 实现 `CreateHierarchyRoot()` - DynamicMatch 根节点
- [x] 2.4 实现 `AdvancedHierarchyScrollData()` - 3 个可滚动页面的滚动数据
  - network_list: 25 项，6 段
  - app_list: 30 项，8 段
  - perm_list: 20 项，5 段

## 3. HierarchyBaselineTests - 场景实现

- [x] 3.1 实现 `Hierarchy_FullTraversal_AllLevelsVisited` 场景
  - 验证: 所有 12 页访问，75+ 唯一元素，scroll_count ≥ 15
- [x] 3.2 实现 `Hierarchy_TargetSearchLevel3_StopsAtTarget` 场景
  - CompletionPolicy: TargetFound (app_list 中的目标元素)
  - 验证: 最多 8 页访问，target_found: true
- [x] 3.3 实现 `Hierarchy_MultiScrollTraversal_AllScrollablePagesVisited` 场景
  - 验证: 3 个可滚动页面访问，scroll_count ≥ 15
- [x] 3.4 实现 `Hierarchy_ScrollThenDeepBack_PreservesState` 场景
  - 验证: 滚动后 3 步返回，状态保持

## 4. LongListBaselineTests - Fixture 实现

- [x] 4.1 实现 `LongListFixture()` - 单页面 fixture
- [x] 4.2 实现 `LongListScrollData()` - 30 项，8 段，均匀分布
- [x] 4.3 实现 `SparseLongListFixture()` 和 `SparseLongListScrollData()` - 25 项，6 段，大间隙
- [x] 4.4 实现 `DenseLongListFixture()` 和 `DenseLongListScrollData()` - 20 项，10 段，高重叠
- [x] 4.5 实现 `CreateLongListRoot()` - DynamicMatch 根节点（共享）

## 5. LongListBaselineTests - 场景实现

- [x] 5.1 实现 `LongList_FullTraversal_AllItemsVisited` 场景
  - 验证: 30 项全部访问，scroll_count ≥ 7，final_progress = 1.0
- [x] 5.2 实现 `SparseList_FullTraversal_JumpRecoveryWorks` 场景
  - 验证: 25 项全部访问，jump_detected ≥ 2，jump_recovered ≥ 2
- [x] 5.3 实现 `DenseList_FullTraversal_AdaptiveStepIncreases` 场景
  - 验证: 20 项全部访问，adaptive_step_increases ≥ 3

## 6. ExpectedBehavior JSON 文件创建

- [x] 6.1 创建 `tests/.../Fixtures/expected/hierarchy/` 目录
- [ ] 6.2 运行测试获取实际基线值
- [ ] 6.3 创建 `hierarchy-full-traversal.json` (finalProgress=0.0，添加 note)
- [ ] 6.4 创建 `hierarchy-target-search.json`
- [ ] 6.5 创建 `hierarchy-multi-scroll.json`
- [ ] 6.6 创建 `hierarchy-scroll-deep-back.json`
- [x] 6.7 创建 `tests/.../Fixtures/expected/long-list/` 目录
- [ ] 6.8 创建 `long-list-full-traversal.json`
- [ ] 6.9 创建 `sparse-list-full-traversal.json`
- [ ] 6.10 创建 `dense-list-full-traversal.json`
- [ ] 6.11 验证所有 7 个场景测试通过

**状态**: 🔴 **BLOCKED** - ScrollHandler Integration Required

**原因**: 
- TraversalEngine 缺少 ScrollHandler 集成
- DynamicMatch 只能看到 threshold=0.0 时的元素（初始视口）
- 没有滚动触发机制来访问后续 scroll segment 的内容
- AllChildrenVisited 退出条件过早触发

**影响**:
- HierarchyBaselineTests: 4/4 场景失败（max_steps, 仅3页访问）
- LongListBaselineTests: 3/3 场景失败（元素覆盖率4-13%，远低于100%）

**参考**: `openspec/changes/advanced-simulation-baseline/SCROLL_HANDLER_INTEGRATION_PLAN.md`

## 7. 文档更新

- [ ] 7.1 更新 `docs/system/layers/simulation-baseline.md`
  - 添加 §4: Advanced Baseline Scenarios
  - 包含 §4.1 HierarchyBaselineTests
  - 包含 §4.2 LongListBaselineTests
  - 包含 §4.3 ExpectedBehavior 限制说明
  - 包含 §4.4 未来扩展
- [ ] 7.2 更新 `docs/system/decisions/log.md`
  - 添加 D-18: Advanced Simulation Baseline 架构决策

## 8. 集成验证

- [ ] 8.1 运行 `dotnet test` 验证所有基线测试通过
- [ ] 8.2 验证 BaselineReportCollector 包含所有 15 个场景（8 现有 + 7 新）
- [ ] 8.3 验证基线报告正确生成
- [ ] 8.4 提交代码和 JSON 文件到 git

---

## 🔄 Phase 3 前置依赖

本变更的完成依赖于 **Phase 3: ScrollHandler 集成**。

详细集成计划见: `SCROLL_HANDLER_INTEGRATION_PLAN.md`
