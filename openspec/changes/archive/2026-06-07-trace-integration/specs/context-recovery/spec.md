## ADDED Requirements

### Requirement: TraversalRuntimeContext 数据模型
系统 SHALL 定义 TraversalRuntimeContext 作为可变运行时上下文。

#### Scenario: RuntimeContext 字段定义
- **WHEN** 查看 TraversalRuntimeContext 定义
- **THEN** 包含以下字段：
  - `trace_id: str` - Trace ID（由引擎注入）
  - `node_stack: List[StackFrame]` - 逻辑任务栈
  - `current_path: List[str]` - 界面位置
  - `current_page_analysis: Optional[PageAnalysis]` - 当前页面分析
  - `cache_valid: bool` - 缓存有效性
  - `page_tree: Dict[str, PageNode]` - 页面树
  - `current_fingerprint: Optional[str]` - 当前页面指纹
  - `visited_pages: Set[str]` - 已访问页面集合
  - `visited_level1_menus: Set[str]` - 已访问一级菜单
  - `visited_level2_menus: Set[str]` - 已访问二级菜单
  - `action_history: List[ActionRecord]` - 操作历史
  - `failed_nodes: Dict[str, ErrorRecord]` - 失败节点记录
  - `consecutive_errors: int` - 连续错误计数
  - `device_experience: Optional[DeviceExperience]` - 设备经验
  - `max_depth: int` - 最大深度
  - `completion_policy: CompletionPolicy` - 完成策略

### Requirement: Context 转换为只读
系统 SHALL 支持将 TraversalRuntimeContext 转换为只读的 TraversalContext。

#### Scenario: 转换为只读副本
- **WHEN** 调用 TraversalRuntimeContext.to_readonly()
- **THEN** 返回 TraversalContext 实例
- **AND** 可变字段转换为不可变类型（List→Tuple, Set→FrozenSet）
- **AND** 原始 RuntimeContext 保持不变

#### Scenario: 传递给 AI 顾问
- **WHEN** 引擎调用 AI 顾问
- **THEN** 传递 TraversalContext（只读）
- **AND** AI 无法修改运行时状态

### Requirement: Context 恢复策略
系统 SHALL 支持可扩展的 Context 恢复策略。

#### Scenario: RecoveryStrategy 枚举
- **WHEN** 定义恢复策略
- **THEN** 支持 FULL（完整恢复）、REPLAY（回放恢复）、MINIMAL（最小恢复）

#### Scenario: FULL 策略实现
- **WHEN** 使用 RecoveryStrategy.FULL
- **THEN** 完整恢复所有必要字段
- **AND** 当前是唯一实现的策略

#### Scenario: 未来策略扩展
- **WHEN** 需要新的恢复策略
- **THEN** 添加新的 RecoveryStrategy 值
- **AND** 实现 ContextRebuilder 的对应方法

### Requirement: FULL 恢复字段
系统 SHALL 在 FULL 策略中恢复指定的上下文字段。

#### Scenario: 恢复必需字段
- **WHEN** 执行 FULL 恢复
- **THEN** 恢复 current_path
- **AND** 恢复 node_stack
- **AND** 恢复 visited_pages
- **AND** 恢复 visited_level1_menus
- **AND** 恢复 visited_level2_menus

#### Scenario: 恢复可选字段
- **WHEN** 执行 FULL 恢复
- **THEN** 可选恢复 action_history
- **AND** 可选恢复 failed_nodes
- **AND** 可选恢复 consecutive_errors

#### Scenario: 不恢复字段
- **WHEN** 执行 FULL 恢复
- **THEN** 不恢复 page_tree（可按需重建）
- **AND** 不恢复 current_page_analysis（可按需重建）
- **AND** 不恢复 page_cache（可按需重建）

### Requirement: ContextRebuilder 接口
系统 SHALL 提供 ContextRebuilder 类，从 Trace 重建 Context。

#### Scenario: 初始化 Rebuilder
- **WHEN** 创建 ContextRebuilder()
- **THEN** 准备好接受 Trace 数据

#### Scenario: 重建 Context
- **WHEN** 调用 ContextRebuilder.rebuild(spans, trace_id, strategy)
- **THEN** 按 Span 序列回放重建 Context
- **AND** 设置 Context.trace_id = trace_id
- **AND** 使用指定的恢复策略

#### Scenario: Span 回放顺序
- **WHEN** 回放 Span 流
- **THEN** 按原始写入顺序处理
- **AND** 逐步重建状态变化

### Requirement: Span 流回放机制
系统 SHALL 通过回放 Span 流重建 Context。

#### Scenario: 设置 Trace ID
- **WHEN** 开始恢复
- **THEN** 创建新的 TraversalRuntimeContext
- **AND** 设置 context.trace_id = trace_id

#### Scenario: 回放 state_transition Span
- **WHEN** 遇到 state_transition Span
- **THEN** 更新 Context 状态相关字段
- **AND** 记录状态变化

#### Scenario: 回放 execution Span
- **WHEN** 遇到 execution Span
- **THEN** 更新 action_history
- **AND** 更新 visited_pages（从 page_after）

#### Scenario: 回放 error Span
- **WHEN** 遇到 error Span
- **THEN** 更新 failed_nodes
- **AND** 更新 consecutive_errors

#### Scenario: 回放 StepNode
- **WHEN** 遇到 StepNode
- **THEN** 更新 node_stack
- **AND** 更新 current_path（从 page_path）

### Requirement: 恢复验证
系统 SHALL 验证恢复后的 Context 正确性。

#### Scenario: 验证 Trace ID
- **WHEN** 恢复完成
- **THEN** context.trace_id 等于原始 Trace ID

#### Scenario: 验证路径一致性
- **WHEN** 恢复完成
- **THEN** context.current_path 与原始一致
- **AND** context.node_stack 深度与原始一致

#### Scenario: 验证访问记录
- **WHEN** 恢复完成
- **THEN** context.visited_pages 包含所有已访问页面
- **AND** 访问计数与原始一致

### Requirement: 引擎直接恢复
系统 SHALL 由引擎直接处理恢复，不通过分析器。

#### Scenario: 引擎读取 Trace
- **WHEN** 需要恢复 Context
- **THEN** 引擎调用 TraceStorage.read(trace_id)
- **AND** 直接获取 Span 流

#### Scenario: 引擎重建 Context
- **WHEN** 获取 Span 流
- **THEN** 引擎使用 ContextRebuilder.rebuild()
- **AND** 不经过 TraceAnalyzer

#### Scenario: 继续遍历
- **WHEN** Context 恢复完成
- **THEN** 引擎使用恢复的 Context 继续遍历
- **AND** Trace 系统继续记录
