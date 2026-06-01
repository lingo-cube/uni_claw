## ADDED Requirements

### Requirement: 遍历上下文数据结构
系统 SHALL 提供 `TraversalContext` 数据类，封装传递给 AI 的只读运行时状态。

#### Scenario: 上下文包含必要字段
- **WHEN** 查看 `TraversalContext` 定义
- **THEN** 包含以下字段：
  - `node_stack: List[str]` - 逻辑任务栈
  - `current_path: List[str]` - 界面位置（真相源）
  - `visited_pages: Set[str]` - 已访问页面集合
  - `failed_nodes: Dict[str, ErrorRecord]` - 失败节点记录
  - `action_history: List[ActionRecord]` - 最近 5 步操作历史
  - `inference_history: List[ContainerInference]` - 最近 3 次容器推断历史
  - `goal_attempts: Dict[str, int]` - 目标尝试次数统计

### Requirement: 只读上下文
系统 SHALL 确保 `TraversalContext` 是只读的，AI 不能修改运行时状态。

#### Scenario: 上下文不可变
- **WHEN** AI 代码尝试修改 `TraversalContext` 字段
- **THEN** 抛出 `TypeError` 或使用 `@dataclass(frozen=True)` 限制

### Requirement: 操作历史限制
系统 SHALL 只保留最近 5 步操作历史，避免上下文过大。

#### Scenario: 历史记录上限
- **WHEN** 操作历史超过 5 条
- **THEN** 保留最新的 5 条记录
- **AND** 移除最旧的记录

### Requirement: 容器推断历史限制
系统 SHALL 只保留最近 3 次容器推断历史，帮助 AI 理解上下文。

#### Scenario: 推断历史上限
- **WHEN** 容器推断历史超过 3 条
- **THEN** 保留最新的 3 条记录
- **AND** 移除最旧的记录

### Requirement: 目标尝试次数统计
系统 SHALL 统计每个目标的尝试次数，用于 AI 决策。

#### Scenario: 记录尝试次数
- **WHEN** 规则引擎或 AI 尝试达成某个目标（如"返回设置根"）
- **THEN** `goal_attempts` 中对应目标的计数增加
- **AND** AI 可根据该统计判断是否应该放弃

#### Scenario: 重置尝试次数
- **WHEN** 目标成功达成或遍历状态变化
- **THEN** 清空或重置对应目标的尝试次数

### Requirement: 上下文序列化
系统 SHALL 支持将 `TraversalContext` 序列化为 JSON，用于日志和调试。

#### Scenario: 序列化为 JSON
- **WHEN** 调用 `TraversalContext.to_json()`
- **THEN** 返回 JSON 字符串
- **AND** 包含所有字段的值
- **AND** 特殊类型（如 Set）正确转换为数组
