## ADDED Requirements

### Requirement: TraversalPlan 定义

系统 SHALL 提供 TraversalPlan 类作为遍历计划的顶层容器。

TraversalPlan SHALL 包含以下字段：
- `entry_app`: 目标应用名称（字符串，必需）
- `entry_policy`: 应用入口策略（EntryPolicy，可选，默认 COLD_LAUNCH）
- `root_node`: 根遍历节点（TraversalNode，可选）
- `static_nodes`: 静态节点注册表（Dict[str, TraversalNode]，可选，默认空）
- `template_registry`: 模板注册表 JSON 路径（字符串，可选）
- `mode`: 遍历模式（TraversalMode，可选，默认 HYBRID）
- `completion_policy`: 全局完成策略（CompletionPolicy，可选，默认 NONE）
- `intent_slots`: AI 提取的意图槽位（IntentSlots，可选）
- `meta`: 元数据（Dict[str, Any]，可选，默认空）

#### Scenario: 创建基本遍历计划
- **WHEN** 创建 TraversalPlan 实例，仅提供 entry_app
- **THEN** 系统 SHALL 创建有效实例，所有可选字段使用默认值

#### Scenario: 从 JSON 反序列化
- **WHEN** 从 JSON 文件加载 TraversalPlan
- **THEN** 系统 SHALL 正确解析所有字段并验证数据完整性

### Requirement: EntryPolicy 定义

系统 SHALL 提供 EntryPolicy 类定义如何进入目标应用。

EntryPolicy SHALL 包含以下字段：
- `strategy`: 入口策略（EntryStrategy，可选，默认 COLD_LAUNCH）
- `fallback`: 失败时的回退入口（字符串，可选）
- `wait_condition`: 进入后期望的屏幕状态（Dict[str, Any]，可选）
- `timeout_seconds`: 超时时间（浮点数，可选，默认 10.0）

#### Scenario: 冷启动策略
- **WHEN** strategy 为 COLD_LAUNCH
- **THEN** 系统 SHALL 从桌面找到并点击应用图标

#### Scenario: 深度链接策略
- **WHEN** strategy 为 DIRECT_DEEPLINK
- **THEN** 系统 SHALL 使用 adb/am start 通过 Intent 启动

#### Scenario: 绑定当前屏幕
- **WHEN** strategy 为 BIND_CURRENT_SCREEN
- **THEN** 系统 SHALL 假设已在目标屏幕，仅验证当前状态

### Requirement: CompletionPolicy 定义

系统 SHALL 提供 CompletionPolicy 类定义全局遍历终止条件。

CompletionPolicy SHALL 包含以下字段：
- `type`: 完成策略类型（CompletionPolicyType，可选，默认 NONE）
- `target_name`: 目标名称（字符串，可选）
- `match_mode`: 文本匹配模式（MatchMode，可选，默认 CONTAINS）
- `action_on_found`: 找到目标后的动作（TargetFoundAction，可选，默认 MARK_AND_STOP）
- `timeout_seconds`: 超时时间（浮点数，可选）
- `max_steps`: 最大步数（整数，可选）

#### Scenario: 无完成策略
- **WHEN** type 为 NONE
- **THEN** 系统 SHALL 运行到自然完成（栈为空）

#### Scenario: 目标搜索策略
- **WHEN** type 为 TARGET_FOUND 且找到匹配目标
- **THEN** 系统 SHALL 根据 action_on_found 执行相应动作并终止

#### Scenario: 超时策略
- **WHEN** type 为 TIMEOUT 且超过 timeout_seconds
- **THEN** 系统 SHALL 终止遍历

#### Scenario: 最大步数策略
- **WHEN** type 为 MAX_STEPS 且达到 max_steps
- **THEN** 系统 SHALL 终止遍历

### Requirement: IntentSlots 定义

系统 SHALL 提供 IntentSlots 类存储 AI 从自然语言提取的意图槽位。

IntentSlots SHALL 包含以下可选字段：
- `target_app`: 目标应用名称
- `scope`: 遍历范围（"full", "partial", "target_only"）
- `target`: 具体目标（如"版本号"）
- `depth`: 最大遍历深度
- `element_handling`: 元素处理策略
- `navigation`: 导航策略
- `restore`: 是否恢复状态
- `completion`: 完成标准

#### Scenario: AI 提取完整意图
- **WHEN** AI 从自然语言提取意图并创建 IntentSlots
- **THEN** 系统 SHALL 存储所有提取的槽位值

#### Scenario: 部分槽位提取
- **WHEN** AI 仅能提取部分意图槽位
- **THEN** 系统 SHALL 将未提取的字段设为 None

### Requirement: TraversalPlan 序列化

系统 SHALL 支持 TraversalPlan 到 JSON 的序列化和反序列化。

#### Scenario: 序列化到 JSON
- **WHEN** 调用 TraversalPlan.to_json()
- **THEN** 系统 SHALL 返回有效的 JSON 字符串，包含所有字段

#### Scenario: 从 JSON 反序列化
- **WHEN** 调用 TraversalPlan.from_json(json_string)
- **THEN** 系统 SHALL 返回等效的 TraversalPlan 实例

### Requirement: 静态节点注册

系统 SHALL 支持通过 static_nodes 字段注册静态节点。

#### Scenario: 注册静态节点
- **WHEN** 将 TraversalNode 添加到 static_nodes 字典
- **THEN** 系统 SHALL 可通过 node_id 引用该节点

#### Scenario: 引用静态节点
- **WHEN** 节点的 children_strategy 引用 static_nodes 中的节点 ID
- **THEN** 系统 SHALL 解析引用并使用对应节点

### Requirement: 模板注册表集成

系统 SHALL 支持通过 template_registry 字段指定模板注册表路径。

#### Scenario: 加载模板注册表
- **WHEN** template_registry 指向有效的 JSON 文件
- **THEN** 系统 SHALL 加载模板并用于动态匹配

#### Scenario: 无模板注册表
- **WHEN** template_registry 为 None
- **THEN** 系统 SHALL 不使用动态匹配功能
