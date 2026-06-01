## ADDED Requirements

### Requirement: AI 调用点嵌入
系统 SHALL 在 `TraversalEngine` 中嵌入三个 AI 调用点，用于容器推断、目标决策和异常兜底。

#### Scenario: 容器推断调用点
- **WHEN** 规则引擎无法确定容器类型
- **THEN** 调用 `advisor.infer_container_type(ui, context)`
- **AND** 根据返回的 `ContainerInference` 生成子节点

#### Scenario: 目标决策调用点
- **WHEN** 需要达成特定目标但规则无法定位目标元素
- **THEN** 调用 `advisor.decide_next_action(goal, ui, context)`
- **AND** 根据返回的 `TraversalNode` 执行操作

#### Scenario: 异常兜底调用点
- **WHEN** 责任链所有处理器无法处理异常
- **THEN** 调用 `advisor.handle_exception(exception, ui, context)`
- **AND** 根据返回的 `TraversalNode` 尝试恢复

### Requirement: 配置开关控制
系统 SHALL 提供配置开关 `enable_ai_advisor` 控制是否启用 AI 功能。

#### Scenario: 默认禁用 AI
- **WHEN** 配置中 `enable_ai_advisor=false` 或未设置
- **THEN** 使用 `NoOpAIAdvisor` 实现
- **AND** 所有 AI 调用返回默认值（UNSURE 或 GIVE_UP）

#### Scenario: 启用 AI
- **WHEN** 配置中 `enable_ai_advisor=true`
- **THEN** 使用配置的 AI Advisor 实现
- **AND** AI 调用点真正执行 AI 决策

### Requirement: AI 调用超时控制
系统 SHALL 为所有 AI 调用设置超时限制，防止阻塞遍历流程。

#### Scenario: 超时默认值
- **WHEN** AI 调用未指定超时
- **THEN** 使用默认超时 30 秒

#### Scenario: 超时处理
- **WHEN** AI 调用超过超时时间
- **THEN** 抛出 `TimeoutError`
- **AND** 返回 `DecisionResult.UNSURE`
- **AND** 规则引擎接管决策

#### Scenario: 自定义超时
- **WHEN** 配置中设置 `ai_call_timeout`
- **THEN** 使用配置的超时值

### Requirement: AI 响应缓存
系统 SHALL 缓存 AI 响应，减少相同上下文的重复调用。

#### Scenario: 缓存命中
- **WHEN** AI 调用的上下文（ui_hash + path_hash）与缓存中的记录匹配
- **THEN** 直接返回缓存的响应
- **AND** 不调用 AI 方法

#### Scenario: 缓存未命中
- **WHEN** AI 调用的上下文与缓存不匹配
- **THEN** 调用 AI 方法
- **AND** 将响应存入缓存

#### Scenario: 缓存 TTL
- **WHEN** 缓存条目超过 TTL（5 分钟）
- **THEN** 该条目过期
- **AND** 下次调用时重新执行 AI 方法

### Requirement: 防抖机制
系统 SHALL 限制同一节点同一异常的连续 AI 调用次数。

#### Scenario: 连续调用限制
- **WHEN** 同一节点同一异常连续调用 `handle_exception` 超过 2 次
- **THEN** 第 3 次及后续调用直接返回 `DecisionResult.GIVE_UP`
- **AND** 防止 AI 陷入循环

#### Scenario: 重置计数
- **WHEN** 节点状态变化或异常类型变化
- **THEN** 重置调用计数

### Requirement: SafetyFilter 集成
系统 SHALL 在执行 AI 返回的 `TraversalNode` 前通过 `SafetyFilter` 验证。

#### Scenario: 验证通过执行
- **WHEN** `SafetyFilter.validate(node, context)` 返回 `is_safe=True`
- **THEN** 执行该 `TraversalNode` 的操作

#### Scenario: 验证拒绝执行 Fallback
- **WHEN** `SafetyFilter.validate(node, context)` 返回 `is_safe=False`
- **THEN** 不执行 AI 返回的操作
- **AND** 执行 `fallback_node` 操作
- **AND** 记录审计日志

### Requirement: 置信度阈值
系统 SHALL 根据 AI 返回的置信度决定是否采纳推断结果。

#### Scenario: 高置信度采纳
- **WHEN** AI 返回的置信度 >= 阈值（默认 0.7）
- **THEN** 采纳 AI 的推断或决策

#### Scenario: 低置信度忽略
- **WHEN** AI 返回的置信度 < 阈值
- **THEN** 忽略 AI 的结果
- **AND** 返回 `DecisionResult.UNSURE`
