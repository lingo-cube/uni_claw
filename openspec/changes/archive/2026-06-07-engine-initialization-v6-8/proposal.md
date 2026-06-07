## Why

当前 GraphTraversalEngine 缺少初始化环节——无法从 TraversalPlan JSON 启动端到端遍历。虽然已有完整的数据模型、Trace 系统和 V6.7 智能状态机，但引擎缺少"启动"环节：如何加载计划、执行入口策略、验证设备状态、压入根节点并开始遍历。本变更补全这一链路，使引擎能从计划文件启动并完成端到端运行。

## What Changes

### 新增功能
- **计划验证**：初始化时验证 TraversalPlan 配置（根节点、类型等）
- **入口策略执行**：支持三种策略（direct_deeplink/cold_launch/bind_current_screen）及自动降级链
- **等待条件验证**：支持快速/轮询两种模式验证入口成功
- **EntryConfig 数据类**：类型安全的入口配置（wait_mode、wait_timeout、action_delay_ms、trace_level）
- **异常类型定义**：区分可恢复/不可恢复错误（ConfigurationError、EntryPolicyError、WaitConditionError）
- **StepTracker 初始化**：根节点压入时初始化 StepTracker

### 行为变更
- **initialize() 返回类型**：从 `bool` 改为抛出异常
- **配置方式**：支持 EntryConfig 数据类（推荐）和 meta 字典（向后兼容）

### 已知限制
- 冷启动应用查找过于简化（多页桌面或文件夹会失败）
- detailed Trace 模式性能影响（仅用于调试）

## Capabilities

### New Capabilities
- `plan-validation`: 验证 TraversalPlan 配置正确性
- `entry-strategy`: 执行入口策略及自动降级链
- `entry-verification`: 验证入口成功条件
- `entry-config`: 类型安全的入口配置
- `initialization-errors`: 初始化异常处理

### Modified Capabilities
- 无现有 spec 需求变更

## Impact

### 受影响代码
- `src/traversal/graph_engine.py`：添加初始化方法
- `src/graph/node.py`：添加 EntryConfig 数据类
- `src/graph/plan.py`：添加 entry_config 字段及序列化支持

### 依赖关系
- 依赖 V6.7 state-machine-intelligence（已完成）
- 依赖 V6.6 trace-handler-metrics-enhancement（已完成）
- 依赖 V6.5 engine-cycle（已完成）

### 向后兼容
- EntryConfig 优先，meta 字典作为后备
- 现有测试无需修改
