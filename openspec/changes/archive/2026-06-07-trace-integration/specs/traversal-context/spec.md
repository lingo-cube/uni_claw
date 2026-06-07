## MODIFIED Requirements

### Requirement: 遍历上下文数据结构
系统 SHALL 提供 `TraversalContext` 和 `TraversalRuntimeContext` 数据类，分别用于只读传递和可变运行时状态。

#### Scenario: TraversalRuntimeContext 可变字段
- **WHEN** 查看 `TraversalRuntimeContext` 定义
- **THEN** 包含以下可变字段：
  - `trace_id: str` - Trace ID（由引擎注入）
  - `node_stack: List[StackFrame]` - 逻辑任务栈
  - `current_path: List[str]` - 界面位置（真相源）
  - `visited_pages: Set[str]` - 已访问页面集合
  - `visited_level1_menus: Set[str]` - 已访问一级菜单
  - `visited_level2_menus: Set[str]` - 已访问二级菜单
  - `action_history: List[ActionRecord]` - 最近操作历史
  - `failed_nodes: Dict[str, ErrorRecord]` - 失败节点记录
  - `consecutive_errors: int` - 连续错误计数
  - 以及其他 PRD_V6_3 定义的字段

#### Scenario: TraversalContext 只读字段
- **WHEN** 查看 `TraversalContext` 定义
- **THEN** 包含以下不可变字段：
  - `node_stack: Tuple[str, ...]` - 逻辑任务栈（不可变）
  - `current_path: Tuple[str, ...]` - 界面位置（不可变）
  - `visited_pages: FrozenSet[str]` - 已访问页面集合（不可变）
  - 以及其他从 RuntimeContext 转换的字段

#### Scenario: 转换方法
- **WHEN** 需要传递给 AI 顾问
- **THEN** 调用 `TraversalRuntimeContext.to_readonly()`
- **AND** 返回 `TraversalContext` 实例
- **AND** 可变类型转换为不可变类型

### Requirement: 只读上下文
系统 SHALL 确保 `TraversalContext` 是只读的（frozen=True），AI 不能修改运行时状态。

#### Scenario: TraversalContext 不可变
- **WHEN** AI 代码尝试修改 `TraversalContext` 字段
- **THEN** 抛出 `FrozenInstanceError` 或类似异常
- **AND** 使用 `@dataclass(frozen=True)` 实现

#### Scenario: TraversalRuntimeContext 可变
- **WHEN** 引擎需要更新运行时状态
- **THEN** 使用 `TraversalRuntimeContext`
- **AND** 可以修改所有字段

### Requirement: Trace ID 注入
系统 SHALL 在 Context 初始化时注入 Trace ID。

#### Scenario: 初始化时注入 Trace ID
- **WHEN** 引擎创建 TraversalRuntimeContext
- **THEN** 设置 context.trace_id = session.session_id
- **AND** Trace ID 贯穿整个遍历生命周期

#### Scenario: 恢复时使用 Trace ID
- **WHEN** 从 Trace 恢复 Context
- **THEN** 使用原始 Trace ID 设置 context.trace_id
- **AND** 确保恢复的 Context 与原始 Trace 关联

### Requirement: Context 用途分离
系统 SHALL 分离 Context 的两种用途：引擎运行时和 AI 顾问。

#### Scenario: 引擎使用 RuntimeContext
- **WHEN** 引擎内部需要运行时状态
- **THEN** 使用 TraversalRuntimeContext
- **AND** 可以修改状态
- **AND** 传递给状态机和规则引擎

#### Scenario: AI 使用只读 Context
- **WHEN** AI 顾问需要上下文信息
- **THEN** 接收 TraversalContext（只读）
- **AND** 无法修改引擎状态
- **AND** 保证决策一致性
