# Scroll Simulation Enhancement — Proposal

> **Version**: 2.0
> **Date**: 2026-07-12
> **Status**: Ready for Implementation
> **Python 对齐**: PRD_V7_0_SimScroll.md

---

## Why

当前 C# 仿真基础设施缺少滚动模拟能力。StatefulMockVisionService 只能返回固定页面内容，无法模拟：
- 可滚动列表（元素随滚动进度逐渐可见）
- 分页加载（滚动到底部加载更多）
- is_end_of_list 状态变化（列表末尾检测）
- 滚动跳跃检测和恢复（防止遗漏元素）
- 自适应滚动步长（优化滚动效率）

这限制了测试覆盖率，无法验证遍历引擎在滚动场景下的行为。Python V7.0 已实现完整的滚动模拟（ScrollableMockVisionService + ScrollSegment 模型），C# 需要对齐并增强。

## What Changes

### 核心数据模型
- **NEW**: `Simulation/Scroll/` 命名空间，包含滚动模拟数据模型
- **NEW**: `ScrollSegment` — 滚动段模型（threshold + elements）
- **NEW**: `ScrollState` — 滚动状态追踪（progress + count + history）
- **NEW**: `ScrollDataStore` — 滚动数据存储和查询
- **NEW**: `ScrollAction` — 滚动动作记录

### 跳跃检测相关 (v2.0 新增)
- **NEW**: `OverlapStatus` — 滚动前后元素重叠状态
- **NEW**: `ScrollVerifyResult` — 滚动验证结果
- **NEW**: `JumpRecoveryResult` — 跳跃恢复结果
- **NEW**: `ScrollHandlerConfig` — 滚动处理器配置（所有参数可配置）

### Mock 服务
- **NEW**: `ScrollableMockVisionService` — 可滚动 Mock Vision 实现
- **NEW**: `ScrollableMockActionExecutor` — 滚动动作执行器
- **MODIFY**: `StateFixtureBuilder` 扩展 — 支持 ScrollSegment 定义

### ScrollHandler 7-step Pipeline (v2.0 新增)
- **NEW**: `ScrollabilityDetector` — 可滚动性检测
- **NEW**: `ScrollClassifier` — 滚动分类
- **NEW**: `ScrollDecider` — 滚动决策
- **NEW**: `ScrollActionExecutor` — 滚动执行（Hook dispatch + 异常兜底）
- **NEW**: `JumpDetector` — 跳跃检测（核心链路）
- **NEW**: `JumpRecoveryHandler` — 跳跃恢复（回滚 + 减半步长重试）
- **NEW**: `AdaptiveStepCalculator` — 自适应步长计算
- **NEW**: `ScrollStatisticsCollector` — 统计收集

### 测试
- **NEW**: 滚动场景测试 fixtures（分类存放，独立测试）
- **NEW**: 19+ 滚动场景测试（基础、边界、元素、步长、跳跃）

**设计原则**:
- 使用 C# 风格的 StateFixtureBuilder 扩展（不使用 JSON 格式）
- 滚动场景单独测试，按类别存放（tests/Simulation/Scroll/）
- 累积模式（Accumulation Mode）: threshold <= progress 的元素都可见
- 跳跃检测作为核心链路，而非测试验证
- 所有步长参数可配置，支持自适应调整

## Capabilities

### New Capabilities

- `scroll-simulation`: 滚动列表仿真测试能力
  - 支持多段滚动列表定义
  - 滚动进度追踪和状态管理
  - 元素可见性随滚动变化
  - is_end_of_list 自动计算
  - 元素去重（按 ID，低 threshold 优先）
  - 进度 clamp（0.0-1.0 边界保护）

- `jump-detection`: 跳跃检测和恢复能力（v2.0 新增）
  - 滚动前后元素重叠检测
  - 跳跃自动恢复（回滚 + 减半步长重试）
  - 可配置最大重试次数

- `adaptive-scroll`: 自适应滚动步长（v2.0 新增）
  - 基于重复元素比例自动调整步长
  - 可配置自适应参数（阈值、增长因子、最小样本）

- `configurable-scroll`: 可配置滚动策略（v2.0 新增）
  - 所有步长参数可配置（默认、最小、最大）
  - 跳跃恢复参数可配置
  - 自适应开关和参数可配置

### Modified Capabilities

无 — 本次变更不修改现有 spec 级行为，仅新增仿真测试能力。

## Impact

**受影响代码**:
- `src/UniClaw.Core/Simulation/Scroll/` — 新增滚动数据模型
  - ScrollSegment.cs, ScrollState.cs, ScrollAction.cs, ScrollDataStore.cs
  - OverlapStatus.cs, ScrollVerifyResult.cs, JumpRecoveryResult.cs
  - ScrollHandlerConfig.cs, ScrollActionResult.cs, ScrollContext.cs
- `src/UniClaw.Core/Simulation/StateFixtureBuilder.cs` — 扩展滚动段定义方法
- `src/UniClaw.Core/Simulation/StatefulMockVisionService.cs` — 新增子类 ScrollableMockVisionService
- `src/UniClaw.Core/Simulation/StatefulMockActionExecutor.cs` — 新增子类 ScrollableMockActionExecutor
- `src/UniClaw.Core/StateMachine/Scroll/` — 新增 ScrollHandler 组件（v2.0）
  - ScrollabilityDetector.cs, ScrollClassifier.cs, ScrollDecider.cs
  - ScrollActionExecutor.cs, JumpDetector.cs, JumpRecoveryHandler.cs
  - AdaptiveStepCalculator.cs, ScrollStatisticsCollector.cs, ScrollHandler.cs

**新增测试**:
- `tests/UniClaw.Core.Tests/Simulation/Scroll/` — 滚动场景测试
  - 数据模型测试: ScrollSegmentTests.cs, ScrollStateTests.cs, ScrollActionTests.cs
  - Service 测试: ScrollableMockVisionServiceTests.cs, ScrollableMockActionExecutorTests.cs
  - 场景测试: ScrollScenarioTests.cs（端到端场景，19+ 测试）
- `tests/UniClaw.Core.Tests/StateMachine/Scroll/` — ScrollHandler 组件测试（v2.0）
  - ScrollabilityDetectorTests.cs, ScrollClassifierTests.cs, ScrollDeciderTests.cs
  - ScrollActionExecutorTests.cs, JumpDetectorTests.cs, JumpRecoveryHandlerTests.cs
  - AdaptiveStepCalculatorTests.cs, ScrollHandlerTests.cs

**依赖影响**:
- 无新增 NuGet 依赖
- 不影响现有 Simulation/Baseline 测试
- 向后兼容现有 StateFixtureBuilder 用法

**Python 对齐**:
- 对齐 Python V7.0 `src/simulation/scroll/` 模块
- ScrollSegment/ScrollState 数据模型一致
- 累积模式元素可见性逻辑一致
- **增强**: 跳跃检测（Python 无，C# 新增）
- **增强**: 自适应步长（Python 无，C# 新增）
- **增强**: 可配置策略（Python 有限，C# 完整）
