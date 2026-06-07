## ADDED Requirements

### Requirement: ERROR_HANDLING 状态

系统 SHALL 在 TraversalStateMachine 中添加 ERROR_HANDLING 状态。

#### Scenario: 进入错误处理状态
- **WHEN** 执行过程中发生异常
- **THEN** 系统 SHALL 转移到 ERROR_HANDLING 状态

#### Scenario: 执行错误处理
- **WHEN** 处于 ERROR_HANDLING 状态
- **THEN** 系统 SHALL 调用 handle_error() 方法

### Requirement: 第一层 - 节点错误策略

系统 SHALL 优先应用节点的 error_policy。

#### Scenario: 应用节点策略
- **WHEN** 当前节点设置了 error_policy
- **THEN** 系统 SHALL：
  1. 读取 error_policy
  2. 根据 on_error 值执行相应动作
  3. 转移到对应状态

#### Scenario: RETRY 动作
- **WHEN** error_policy.on_error 为 "retry"
- **THEN** 系统 SHALL：
  1. 重试当前节点操作
  2. 转移到 EXECUTE 状态

#### Scenario: SKIP 动作
- **WHEN** error_policy.on_error 为 "skip"
- **THEN** 系统 SHALL：
  1. 标记节点为失败
  2. 推进到下一个节点
  3. 转移到 NODE_SELECT 状态

#### Scenario: BACKTRACK 动作
- **WHEN** error_policy.on_error 为 "backtrack"
- **THEN** 系统 SHALL：
  1. 弹出当前帧
  2. 转移到 FRAME_COMPLETE 状态

#### Scenario: ABORT 动作
- **WHEN** error_policy.on_error 为 "abort"
- **THEN** 系统 SHALL：
  1. 设置全局状态为 TERMINATED
  2. 转移到 COMPLETED 状态

### Requirement: 第二层 - 异常处理链

系统 SHALL 在节点无策略时使用 ExceptionHandlingChain。

#### Scenario: 应用异常链
- **WHEN** 当前节点未设置 error_policy 但 exception_chain 可用
- **THEN** 系统 SHALL：
  1. 创建 ExceptionContext
  2. 调用 exception_chain.handle()
  3. 根据结果转移到对应状态

#### Scenario: RECOVER 结果
- **WHEN** 异常链返回 RECOVER
- **THEN** 系统 SHALL：
  1. 标记异常已恢复
  2. 转移到 NODE_SELECT 状态

#### Scenario: BACKTRACK 结果
- **WHEN** 异常链返回 BACKTRACK
- **THEN** 系统 SHALL：
  1. 弹出当前帧
  2. 转移到 FRAME_COMPLETE 状态

### Requirement: 第三层 - AI 异常处理

系统 SHALL 在前两层都不可用时使用 AI 异常处理。

#### Scenario: AI 处理异常
- **WHEN** 节点无策略且异常链不可用但 AI 可用
- **THEN** 系统 SHALL：
  1. 调用 ai_provider.handle_exception()
  2. 根据 AI 决策转移到对应状态

#### Scenario: AI 决策重试
- **WHEN** AI 返回重试决策
- **THEN** 系统 SHALL 转移到 EXECUTE 状态

### Requirement: 默认错误处理

系统 SHALL 在所有处理层都不可用时使用默认行为。

#### Scenario: 默认 SKIP
- **WHEN** 节点无策略、异常链不可用、AI 不可用
- **THEN** 系统 SHALL：
  1. 记录警告日志
  2. 转移到 NODE_SELECT 状态

### Requirement: 错误上下文创建

系统 SHALL 为异常处理创建完整的上下文信息。

#### Scenario: 创建 ExceptionContext
- **WHEN** 调用异常处理
- **THEN** 系统 SHALL 创建包含以下信息的 ExceptionContext：
  - exception: 发生的异常
  - state: 当前遍历上下文
  - node: 当前节点
  - stack_trace: 异常堆栈跟踪

### Requirement: 错误处理优先级

系统 SHALL 按照固定优先级应用错误处理层。

#### Scenario: 优先级顺序
- **WHEN** 多个错误处理层都可用
- **THEN** 系统 SHALL 按以下顺序应用：
  1. 节点 error_policy
  2. ExceptionHandlingChain
  3. AI 异常处理
  4. 默认 SKIP

#### Scenario: 第一层生效
- **WHEN** 节点设置了 error_policy
- **THEN** 系统 SHALL 仅应用该策略，不继续到下一层

### Requirement: 错误处理结果映射

系统 SHALL 将各层处理结果映射到对应的状态转移。

#### Scenario: 结果映射
- **WHEN** 错误处理返回特定结果
- **THEN** 系统 SHALL 按以下规则映射：
  - RETRY → EXECUTE
  - SKIP → NODE_SELECT
  - BACKTRACK → FRAME_COMPLETE
  - ABORT → COMPLETED

### Requirement: 错误日志记录

系统 SHALL 记录所有错误处理事件。

#### Scenario: 记录错误处理
- **WHEN** 执行错误处理
- **THEN** 系统 SHALL 在 Trace 中记录：
  - 异常类型和消息
  - 使用的处理层
  - 处理结果
  - 目标状态
