## ADDED Requirements

### Requirement: 指令解析能力
系统 SHALL 提供 `ParseToPlanCapability` 能力，将自然语言指令解析为遍历计划结构。

#### Scenario: 解析设置遍历指令
- **WHEN** 输入指令为 "遍历系统设置"
- **THEN** 返回 `TraversalPlan` 包含：
  - `entry_app`: "设置"
  - `root_node`: 包含目标为 "设置" 的操作
  - `mode`: "hybrid"
  - `template_registry`: "default"

#### Scenario: 解析具体路径指令
- **WHEN** 输入指令为 "只检查设置→显示→亮度"
- **THEN** 返回 `TraversalPlan` 包含：
  - `entry_app`: "设置"
  - `static_nodes`: 包含 "显示" 和 "亮度" 节点
  - `mode`: "concrete"

#### Scenario: 解析失败默认值
- **WHEN** AI 无法解析指令
- **THEN** 返回默认计划：
  - `entry_app`: "设置"
  - `mode`: "hybrid"
  - 使用动态匹配策略

### Requirement: 遍历计划数据结构
系统 SHALL 定义 `TraversalPlan` 数据结构，包含遍历所需的所有信息。

#### Scenario: 计划结构
- **WHEN** 返回 `TraversalPlan`
- **THEN** 包含以下字段：
  - `entry_app`: 入口应用名称
  - `root_node`: 根节点配置
  - `static_nodes`: 静态节点列表（可选）
  - `template_registry`: 模板注册表名称
  - `mode`: 遍历模式（hybrid/concrete/dynamic）

#### Scenario: 节点操作结构
- **WHEN** 定义节点操作
- **THEN** 操作包含统一格式：
  - `action`: 动作类型（click/back/swipe/input_text/no_action）
  - `target`: 目标描述（包含 `by` 定位方式和 `value` 值）
  - `params`: 动作参数（可选）
  - `restore`: 恢复操作（可选）

### Requirement: 危险操作过滤
系统 SHALL 在指令解析阶段过滤掉包含破坏性词汇的操作。

#### Scenario: 拒绝恢复出厂设置
- **WHEN** 指令包含 "恢复出厂设置"
- **THEN** 计划中不包含该操作
- **AND** AI 提供说明该操作被跳过

#### Scenario: 拒绝删除数据操作
- **WHEN** 指令包含 "清除数据"、"删除"、"卸载"、"格式化"、"重置"
- **THEN** 计划中不包含该操作
- **AND** AI 提供说明该操作被跳过

### Requirement: Prompt 模板
系统 SHALL 为指令解析提供优化的 Prompt 模板。

#### Scenario: 系统提示词
- **WHEN** 获取系统 Prompt
- **THEN** 包含以下内容：
  - 角色定义为任务解析器
  - 输出 JSON 格式规范
  - 操作统一格式说明
  - 危险操作禁止规则
  - 默认值说明

#### Scenario: 用户提示词
- **WHEN** 获取用户 Prompt
- **THEN** 包含用户指令占位符 `{instruction}`
- **AND** 包含推理级别占位符 `{{REASONING_LEVEL}}`

### Requirement: 响应 Schema
系统 SHALL 定义指令解析的 JSON Schema，确保返回结构化的遍历计划。

#### Scenario: Schema 验证
- **WHEN** AI 返回响应
- **THEN** 响应符合以下 Schema：
  - `entry_app`: 字符串或 null
  - `root_node`: 对象（必需）
  - `static_nodes`: 数组（可选）
  - `template_registry`: 字符串（必需）
  - `mode`: 枚举值（必需）
- **AND** 验证失败时抛出异常

### Requirement: 解析器注册
系统 SHALL 注册 `TraversalPlan` 的解析器到 `ResponseValidator`。

#### Scenario: 解析器函数
- **WHEN** 注册解析器
- **THEN** 解析器将 JSON 响应转换为 `TraversalPlan` 数据对象
- **AND** 验证所有必需字段存在
- **AND** 提供默认值处理可选字段
