## Why

uni-claw 车机菜单遍历引擎当前依赖硬编码的规则引擎处理所有场景，当遇到未知容器类型、异常边缘情况或无法定位目标元素时，遍历会中断。PRD V5.0 定义了 AI 策略顾问抽象，用于在这些边缘场景下提供智能决策支持。

Phase 1-2 旨在建立 AI 策略顾问的基础架构，包括数据模型、安全过滤器和与现有引擎的集成点，为后续接入真实 LLM（Phase 3-4）做好准备。

## What Changes

- **新增数据结构**：`TraversalContext`、`SafetyFilter`、`DecisionResult`、`AIStrategyAdvisor` 抽象接口
- **新增 AI Advisor 实现**：`NoOpAIAdvisor`（默认实现）、`MockAIAdvisor`（测试用）
- **引擎集成**：在 `TraversalEngine` 中嵌入 AI 调用点，包括容器推断、目标决策、异常兜底三个场景
- **安全机制**：`SafetyFilter` 验证 AI 输出的操作，防止危险操作
- **超时与缓存**：AI 调用超时控制与响应缓存机制
- **测试覆盖**：单元测试覆盖新增组件

**无破坏性变更** - 所有新功能通过配置开关启用，默认使用 NoOp 实现，不影响现有遍历流程。

## Capabilities

### New Capabilities
- `ai-strategy-advisor`: AI 策略顾问抽象接口与基础实现，提供容器推断、目标决策、异常兜底三个能力
- `safety-filter`: 操作安全过滤器，验证 AI 输出并防止危险操作
- `traversal-context`: 增强的遍历上下文，提供给 AI 的只读运行时状态
- `ai-integration`: AI 调用点嵌入到现有遍历引擎，支持超时与缓存

### Modified Capabilities
- 无现有规范需要修改

## Impact

- **新增代码文件**：
  - `src/ai/advisor.py` - AI 策略顾问抽象接口
  - `src/ai/noop_advisor.py` - 默认空实现
  - `src/ai/mock_advisor.py` - 测试用 Mock 实现
  - `src/safety/filter.py` - 安全过滤器
  - `src/context/traversal_context.py` - 遍历上下文数据模型

- **修改代码文件**：
  - `src/engine/traversal_engine.py` - 嵌入 AI 调用点
  - `src/state/traversal_state.py` - 扩展字段支持新上下文

- **新增依赖**：暂无（Phase 3 才引入真实 LLM）

- **配置变更**：新增开关配置 `enable_ai_advisor`（默认 false）
