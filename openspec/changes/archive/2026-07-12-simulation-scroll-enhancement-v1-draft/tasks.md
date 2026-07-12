# Scroll Simulation Enhancement — Implementation Tasks

> **Version**: 2.0
> **Date**: 2026-07-12
> **Status**: Ready for Implementation

---

## Task Summary

| Phase | Tasks | Status |
|-------|-------|--------|
| Phase 1: 数据模型 | 7 tasks | ⏸️ Pending |
| Phase 2: Builder 扩展 | 6 tasks | ⏸️ Pending |
| Phase 3: Vision Service | 9 tasks | ⏸️ Pending |
| Phase 4: ScrollHandler 组件 | 9 tasks | ⏸️ Pending |
| Phase 5: ActionExecutor | 8 tasks | ⏸️ Pending |
| Phase 6: 测试 | 19+ tasks | ⏸️ Pending |
| Phase 7: 文档 | 3 tasks | ⏸️ Pending |
| Phase 8: 验证 | 2 tasks | ⏸️ Pending |

---

## Phase 1: 数据模型实现 (Simulation/Scroll/)

- [ ] **1.1** 创建 `src/UniClaw.Core/Simulation/Scroll/` 目录
- [ ] **1.2** 实现 `ScrollSegment.cs` (sealed record: Threshold + Elements)
- [ ] **1.3** 实现 `ScrollState.cs` (sealed record: CurrentProgress + ScrollCount + ScrollHistory)
- [ ] **1.4** 实现 `ScrollAction.cs` (sealed record: Action + StepPercent + BeforeProgress + AfterProgress + Timestamp)
- [ ] **1.5** 实现 `ScrollDataStore.cs` (存储和查询 ScrollSegment 数据)
- [ ] **1.6** 实现滚动验证相关类型:
  - `OverlapStatus.cs` (enum: HasOverlap, NoOverlap_BothHaveElements, NoOverlap_BeforeEmpty, NoOverlap_AfterEmpty, BothEmpty)
  - `ScrollVerifyResult.cs` (sealed record)
  - `JumpRecoveryResult.cs` (sealed record)
  - `ScrollHandlerConfig.cs` (sealed record - 所有配置可配置)
  - `ScrollActionResult.cs` (sealed record)
  - `ScrollContext.cs` (sealed record)
- [ ] **1.7** 编写数据模型单元测试:
  - `ScrollSegmentTests.cs`
  - `ScrollStateTests.cs`
  - `ScrollActionTests.cs`
- [ ] **1.8** 验证编译通过 (`dotnet build`)

**Acceptance Criteria**:
- 所有数据模型使用 `sealed record class` + `ImmutableArray<T>`
- 所有字段有清晰的 XML 文档注释
- 单元测试覆盖所有构造函数和属性

---

## Phase 2: StateFixtureBuilder 扩展

- [ ] **2.1** 添加 `ScrollSegmentBuilder.cs` (Fluent Builder for scroll segments)
- [ ] **2.2** 在 `PageStateBuilder` 添加 `ScrollSegments()` 方法
- [ ] **2.3** 扩展 `PageState` 模型支持 ScrollSegment 存储
- [ ] **2.4** 更新 `StateFixtureBuilder` 支持滚动数据传递
- [ ] **2.5** 编写 Builder 扩展测试:
  - 验证 fluent API 可用
  - 验证向后兼容（无 ScrollSegments 的现有代码仍可工作）
- [ ] **2.6** 验证编译通过

**Acceptance Criteria**:
- Fluent API 链式调用流畅
- 向后兼容现有测试代码
- 支持 ScrollSegment 的序列化/反序列化

---

## Phase 3: ScrollableMockVisionService 实现

- [ ] **3.1** 创建 `ScrollableMockVisionService.cs` (继承 StatefulMockVisionService)
- [ ] **3.2** 实现滚动状态管理 (`_scrollStates` 字典)
- [ ] **3.3** 实现 `GetOrCreateScrollState()` 方法
- [ ] **3.4** 实现 `GetVisibleElements()` (累积模式逻辑)
- [ ] **3.5** 实现 `CalculateIsEndOfList()` (max threshold 判断)
- [ ] **3.6** 实现 `CalculateHasScroll()` (threshold 比较逻辑)
- [ ] **3.7** 实现 `SimulateScroll()` 方法 (进度更新 + 状态记录)
- [ ] **3.8** 实现 `GetScrollProgress()` 查询方法
- [ ] **3.9** 实现 `SetScrollProgress()` 设置方法 (用于跳跃恢复回滚)
- [ ] **3.10** 重写 `AnalyzeCurrentPageAsync()` (集成滚动逻辑)
- [ ] **3.11** 实现元素去重 (按 ID，低 threshold 优先)
- [ ] **3.12** 实现 `GetDataStore()` 访问器
- [ ] **3.13** 验证编译通过

**Acceptance Criteria**:
- 累积模式正确实现 (threshold <= progress)
- 元素去重正确 (低 threshold 优先)
- IsEndOfList 计算正确
- HasScroll 计算正确
- 进度 clamp 到 [0.0, 1.0]

---

## Phase 4: ScrollHandler 组件实现

- [ ] **4.1** 创建 `src/UniClaw.Core/StateMachine/Scroll/` 目录
- [ ] **4.2** 实现 `ScrollabilityDetector.cs` (Step 1: Detect)
  - `Scrollability` enum
  - `Detect()` 方法
- [ ] **4.3** 实现 `ScrollClassifier.cs` (Step 2: Classify)
  - `ScrollDecision` record
  - `Classify()` 方法
  - `DetermineRecommendedStep()` 方法
- [ ] **4.4** 实现 `ScrollDecider.cs` (Step 3: Decide)
  - `ScrollActionType` enum
  - `Decide()` 方法
- [ ] **4.5** 实现 `ScrollActionExecutor.cs` (Step 4: Execute)
  - Hook dispatch table
  - `DefaultScrollDown()`, `DefaultScrollUp()`, `DefaultNone()` 方法
  - 异常兜底
- [ ] **4.6** 实现 `JumpDetector.cs` (Step 5: Verify)
  - `ScrollVerifyResult` record
  - `Verify()` 方法
  - `DetectOverlapStatus()` 私有方法
- [ ] **4.7** 实现 `JumpRecoveryHandler.cs` (Step 6: Recover)
  - `RecoverFromJump()` 方法
  - 回滚逻辑
  - 减半步长重试循环
- [ ] **4.8** 实现 `AdaptiveStepCalculator.cs` (自适应步长)
  - `CalculateNextStep()` 方法
  - `CalculateInitialStep()` 方法
  - 重复元素比例计算
- [ ] **4.9** 实现 `ScrollStatisticsCollector.cs` (Step 7: Statistics)
  - 统计方法 (RecordScroll, RecordSkip, RecordJumpDetected, etc.)
  - `GetStatistics()` 方法
  - `ScrollHandlerStatistics` record
- [ ] **4.10** 实现 `ScrollHandler.cs` (7-step pipeline 编排)
  - `HandleScroll()` 主方法
  - `ExecuteWithJumpRecovery()` 私有方法
  - 集成所有 7 个步骤
- [ ] **4.11** 编写 ScrollHandler 组件单元测试:
  - `ScrollabilityDetectorTests.cs`
  - `ScrollClassifierTests.cs`
  - `ScrollDeciderTests.cs`
  - `ScrollActionExecutorTests.cs`
  - `JumpDetectorTests.cs`
  - `JumpRecoveryHandlerTests.cs`
  - `AdaptiveStepCalculatorTests.cs`
  - `ScrollStatisticsCollectorTests.cs`
  - `ScrollHandlerTests.cs` (集成测试)
- [ ] **4.12** 验证编译通过

**Acceptance Criteria**:
- 7-step pipeline 严格按照顺序执行
- 跳跃检测正确识别 NoOverlap_BothHaveElements
- 跳跃恢复正确回滚并减半步长
- 自适应步长在重复元素过多时增大步长
- 统计数据正确记录

---

## Phase 5: ScrollableMockActionExecutor 实现

- [ ] **5.1** 创建 `ScrollableMockActionExecutor.cs` (继承 StatefulMockActionExecutor)
- [ ] **5.2** 实现构造器接受 ScrollableMockVisionService
- [ ] **5.3** 实现 `ScrollDown()` 方法 (调用 SimulateScroll)
- [ ] **5.4** 实现 `ScrollUp()` 方法 (调用 SimulateScroll with negative delta)
- [ ] **5.5** 实现滚动动作记录 (`RecordScrollAction`)
- [ ] **5.6** 实现 `GetScrollCount()` 查询方法
- [ ] **5.7** 实现 `GetScrollActions()` 查询方法 (返回历史)
- [ ] **5.8** 编写 ScrollableMockActionExecutor 单元测试
- [ ] **5.9** 验证编译通过

**Acceptance Criteria**:
- ScrollDown 正确增加进度
- ScrollUp 正确减少进度
- 进度 clamp 到 [0.0, 1.0]
- 滚动动作正确记录到历史

---

## Phase 6: 测试实现

### 6.1 数据模型测试 (已在 Phase 1)

### 6.2 Vision Service 测试

- [ ] **6.2.1** 编写累积模式测试
- [ ] **6.2.2** 编写元素去重测试
- [ ] **6.2.3** 编写 is_end_of_list 计算测试
- [ ] **6.2.4** 编写 has_scroll 计算测试
- [ ] **6.2.5** 编写 SimulateScroll 进度追踪测试
- [ ] **6.2.6** 编写滚动进度 clamping 测试

### 6.3 场景测试 (ScrollScenarioTests.cs)

#### 基础场景 (4 tests)
- [ ] **6.3.1** `Scroll_SingleScreenList_NoScrollNeeded`
- [ ] **6.3.2** `Scroll_TwoScreenList_OneScroll`
- [ ] **6.3.3** `Scroll_MultiScreenList_MultipleScrolls`
- [ ] **6.3.4** `Scroll_EmptyList_NoScroll`

#### 边界场景 (4 tests)
- [ ] **6.3.5** `Scroll_TopBoundary_NoScrollUp`
- [ ] **6.3.6** `Scroll_BottomBoundary_IsEndOfList`
- [ ] **6.3.7** `Scroll_NearBottom_ClampedStep`
- [ ] **6.3.8** `Scroll_PreciseEndOfList_ExactMatch`

#### 元素场景 (3 tests)
- [ ] **6.3.9** `Scroll_NoDuplicates_AllUnique`
- [ ] **6.3.10** `Scroll_WithDuplicates_Deduplicated`
- [ ] **6.3.11** `Scroll_ElementDeduplication_LowestThreshold`

#### 步长场景 (4 tests)
- [ ] **6.3.12** `Scroll_SmallStep_5Percent`
- [ ] **6.3.13** `Scroll_DefaultStep_30Percent`
- [ ] **6.3.14** `Scroll_LargeStep_50Percent`
- [ ] **6.3.15** `Scroll_AdaptiveStep_IncreaseOnDuplicates`

#### 跳跃场景 (4 tests)
- [ ] **6.3.16** `Scroll_NormalOverlap_NoJump`
- [ ] **6.3.17** `Scroll_JumpDetection_NoOverlap`
- [ ] **6.3.18** `Scroll_JumpRecovery_HalfStepRetry`
- [ ] **6.3.19** `Scroll_JumpFailure_MaxRetriesExceeded`

### 6.4 端到端集成测试

- [ ] **6.4.1** WiFi 列表滚动场景 (5 segment, 10+ networks)
- [ ] **6.4.2** 嵌套列表滚动场景
- [ ] **6.4.3** 滚动决策逻辑测试 (HasScroll && !IsEndOfList 判断)
- [ ] **6.4.4** TraversalFSM 集成测试 (HandleBranch 滚动检查点)

### 6.5 验证 CI 测试

- [ ] **6.5.1** 运行 `dotnet test` 验证所有测试通过
- [ ] **6.5.2** 验证原有 617+ 测试保持全绿
- [ ] **6.5.3** 验证新增滚动测试全绿

---

## Phase 7: 文档更新

- [ ] **7.1** 更新 `docs/system/layers/simulation-baseline.md` (添加滚动能力说明)
- [ ] **7.2** 更新 `docs/system/layers/state-machine.md` (如有需要)
- [ ] **7.3** 添加滚动场景示例代码注释
- [ ] **7.4** 合并 PRD 文档到统一文档 (已完成 - design.md v2.0)

---

## Phase 8: 验证与归档

- [ ] **8.1** 最终验证:
  - 所有测试通过 (原有 617+ + 新增滚动测试)
  - 代码审查通过
  - 文档审查通过
- [ ] **8.2** 运行 `/opsx:archive` 归档此 change

---

## Dependencies

### 内部依赖
- StateFixture ✅ (已有)
- StatefulMockVisionService ✅ (已有)
- StatefulMockActionExecutor ✅ (已有)
- ExpectedBehavior ✅ (已有)

### 外部依赖
- 无新增 NuGet 包依赖

---

## Risk Mitigation

| Risk | 缓解措施 |
|------|---------|
| 滚动进度计算不准确 | 使用归一化 0.0-1.0 进度，预留 Phase 2 屏幕尺寸配置 |
| 测试 fixture 复杂度增加 | 提供预定义的滚动场景 helper 方法 |
| 跳跃检测性能影响 | 仅在滚动后执行，复杂度 O(n) where n = 元素数量 |
| 自适应步长不稳定 | 提供配置开关，默认启用但可关闭 |

---

## Rollback Strategy

- ScrollableMockVisionService 是新增类，不影响现有代码
- StateFixtureBuilder 扩展向后兼容
- ScrollHandler 组件在独立命名空间
- 每个Phase 独立 commit，有问题可 revert

---

**文档所有者**: UniClaw.Core C# 迁移项目
**状态**: Ready for Implementation
**最后更新**: 2026-07-12
**版本**: 2.0
