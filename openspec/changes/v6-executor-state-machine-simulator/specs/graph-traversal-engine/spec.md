## ADDED Requirements

### Requirement: GraphTraversalEngine 定义

系统 SHALL 提供 GraphTraversalEngine 类作为图遍历执行引擎。

#### Scenario: 创建执行引擎
- **WHEN** 创建 GraphTraversalEngine 实例
- **THEN** 系统 SHALL 接受以下参数：
  - plan: TraversalPlan（必需）
  - vision_service: VisionService（必需）
  - action_executor: ActionExecutor（必需）
  - exception_chain: ExceptionHandlingChain（可选）
  - trace_recorder: TraceRecorder（可选）

### Requirement: 引擎初始化

系统 SHALL 提供 initialize() 方法执行初始化流程。

#### Scenario: 执行入口策略
- **WHEN** 调用 initialize()
- **THEN** 系统 SHALL 根据 plan.entry_policy 执行入口策略

#### Scenario: COLD_LAUNCH 入口
- **WHEN** entry_policy.strategy 为 COLD_LAUNCH
- **THEN** 系统 SHALL：
  1. 返回桌面
  2. 查找应用图标
  3. 点击应用图标

#### Scenario: DIRECT_DEEPLINK 入口
- **WHEN** entry_policy.strategy 为 DIRECT_DEEPLINK
- **THEN** 系统 SHALL 使用 adb/am start 启动应用

#### Scenario: BIND_CURRENT_SCREEN 入口
- **WHEN** entry_policy.strategy 为 BIND_CURRENT_SCREEN
- **THEN** 系统 SHALL 验证当前屏幕状态

#### Scenario: 等待入口条件
- **WHEN** 执行入口策略后
- **THEN** 系统 SHALL 等待 entry_policy.wait_condition 条件满足

#### Scenario: 压入根节点
- **WHEN** plan.root_node 存在
- **THEN** 系统 SHALL 将根节点压入 NodeStack

#### Scenario: 初始化 Trace
- **WHEN** trace_recorder 可用
- **THEN** 系统 SHALL 调用 start_traversal() 开始记录

#### Scenario: 初始化成功
- **WHEN** 所有初始化步骤成功
- **THEN** initialize() SHALL 返回 True

#### Scenario: 初始化失败
- **WHEN** 任何初始化步骤失败
- **THEN** initialize() SHALL 返回 False

### Requirement: 主循环

系统 SHALL 提供 run() 方法执行完整遍历。

#### Scenario: 启动主循环
- **WHEN** 调用 run()
- **THEN** 系统 SHALL：
  1. 调用 initialize()
  2. 设置全局状态为 TRAVERSING
  3. 开始主循环

#### Scenario: 栈检查
- **WHEN** 主循环迭代时
- **THEN** 系统 SHALL 检查 NodeStack 是否为空

#### Scenario: 栈为空
- **WHEN** NodeStack 为空
- **THEN** 系统 SHALL 设置全局状态为 COMPLETED 并退出循环

#### Scenario: 完成策略检查
- **WHEN** NodeStack 不为空
- **THEN** 系统 SHALL 检查 CompletionPolicy 是否触发

#### Scenario: 完成策略触发
- **WHEN** CompletionPolicy 触发
- **THEN** 系统 SHALL 设置全局状态为 COMPLETED 并退出循环

#### Scenario: 执行状态机步进
- **WHEN** 遍历应继续
- **THEN** 系统 SHALL 调用 state_machine.step()

#### Scenario: 记录状态转换
- **WHEN** 状态转换发生
- **THEN** 系统 SHALL 调用 trace_recorder.record_transition()

#### Scenario: 返回遍历结果
- **WHEN** 主循环退出
- **THEN** run() SHALL 返回包含以下信息的 TraversalResult：
  - status: 最终状态
  - elapsed_seconds: 执行时间
  - total_steps: 总步数
  - visited_nodes: 已访问节点列表
  - trace: Trace 数据

### Requirement: 深度限制

系统 SHALL 支持深度限制以防止无限递归。

#### Scenario: 检查深度限制
- **WHEN** 生成子节点时
- **THEN** 系统 SHALL 检查当前深度是否超过 max_depth

#### Scenario: 达到深度限制
- **WHEN** 当前深度达到 max_depth
- **THEN** 系统 SHALL：
  1. 不生成 menu_item 类型的子节点
  2. 仅生成叶子类型的子节点

#### Scenario: 未达到深度限制
- **WHEN** 当前深度小于 max_depth
- **THEN** 系统 SHALL 正常生成所有子节点

### Requirement: 页面缓存管理

系统 SHALL 支持页面信息缓存以优化性能。

#### Scenario: 更新缓存
- **WHEN** 获得新的 PageAnalysis
- **THEN** 系统 SHALL 更新 TraversalContext.page_cache

#### Scenario: 缓存键生成
- **WHEN** 更新缓存
- **THEN** 系统 SHALL 使用当前路径作为缓存键

#### Scenario: 从缓存恢复
- **WHEN** 返回到已访问的路径
- **THEN** 系统 SHALL 尝试从缓存恢复页面信息

#### Scenario: 缓存命中
- **WHEN** 缓存中存在该路径的信息
- **THEN** 系统 SHALL 使用缓存数据跳过视觉分析

#### Scenario: 缓存未命中
- **WHEN** 缓存中不存在该路径的信息
- **THEN** 系统 SHALL 执行视觉分析并更新缓存

### Requirement: 完成策略检查

系统 SHALL 在每次循环迭代时检查完成策略。

#### Scenario: TARGET_FOUND 检查
- **WHEN** completion_policy.type 为 TARGET_FOUND
- **THEN** 系统 SHALL 检查是否找到目标

#### Scenario: 目标匹配
- **WHEN** 节点名称匹配 target_name
- **THEN** 系统 SHALL 根据 action_on_found 执行并终止

#### Scenario: TIMEOUT 检查
- **WHEN** completion_policy.type 为 TIMEOUT
- **THEN** 系统 SHALL 检查是否超过 timeout_seconds

#### Scenario: MAX_STEPS 检查
- **WHEN** completion_policy.type 为 MAX_STEPS
- **THEN** 系统 SHALL 检查是否达到 max_steps

### Requirement: 模板注册表加载

系统 SHALL 支持加载模板注册表用于动态匹配。

#### Scenario: 加载模板
- **WHEN** plan.template_registry 指定路径
- **THEN** 系统 SHALL 从该路径加载模板注册表

#### Scenario: 创建匹配器
- **WHEN** 模板注册表加载成功
- **THEN** 系统 SHALL 创建 DynamicMatcher 实例

#### Scenario: 无模板注册表
- **WHEN** plan.template_registry 为 None
- **THEN** 系统 SHALL 不创建 DynamicMatcher

### Requirement: 上下文管理

系统 SHALL 维护 TraversalContext 贯穿整个遍历过程。

#### Scenario: 上下文初始化
- **WHEN** 初始化执行引擎
- **THEN** 系统 SHALL 创建 TraversalContext 实例

#### Scenario: 上下文传递
- **WHEN** 调用 state_machine.step()
- **THEN** 系统 SHALL 传递 TraversalContext

#### Scenario: 步数计数
- **WHEN** 每次状态转换
- **THEN** 系统 SHALL 增加 context.step_count

#### Scenario: 访问节点记录
- **WHEN** 节点被访问
- **THEN** 系统 SHALL 将节点 ID 添加到 context.visited_nodes

### Requirement: 异常传播

系统 SHALL 正确处理执行过程中的异常。

#### Scenario: 初始化异常
- **WHEN** initialize() 抛出异常
- **THEN** run() SHALL 返回失败的 TraversalResult

#### Scenario: 主循环异常
- **WHEN** 主循环中抛出未捕获异常
- **THEN** 系统 SHALL：
  1. 记录异常
  2. 返回失败的 TraversalResult

#### Scenario: 状态机异常
- **WHEN** state_machine.step() 抛出异常
- **THEN** 系统 SHALL 将异常传递给 ERROR_HANDLING 状态

### Requirement: 性能监控

系统 SHALL 记录执行性能指标。

#### Scenario: 执行时间记录
- **WHEN** 遍历完成
- **THEN** 系统 SHALL 记录总执行时间

#### Scenario: 步数统计
- **WHEN** 遍历完成
- **THEN** 系统 SHALL 记录总步数

#### Scenario: 节点访问统计
- **WHEN** 遍历完成
- **THEN** 系统 SHALL 记录访问的节点数量
