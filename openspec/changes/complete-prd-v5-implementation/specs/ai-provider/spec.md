## ADDED Requirements

### Requirement: AIProvider Interface Definition

系统 SHALL 定义 `AIProvider` 抽象接口，包含四个核心方法。

接口 SHALL 在构造时强制注入 `SafetyPolicy` 实例，无注入则抛出异常。

系统 SHALL 提供 `NoOpAIProvider` 作为默认实现，所有方法返回安全默认值。

#### Scenario: AIProvider 构造需 SafetyPolicy
- **WHEN** 创建 AIProvider 实例但未注入 SafetyPolicy
- **THEN** 系统抛出 TypeError 异常

#### Scenario: NoOpAIProvider 返回安全默认值
- **WHEN** 调用 NoOpAIProvider.parse_task_to_plan()
- **THEN** 返回包含根节点的 TraversalPlan
- **AND** 该根节点使用动态匹配策略

---

### Requirement: Natural Language to Traversal Plan

AIProvider SHALL 实现 `parse_task_to_plan(task: str) -> TraversalPlan` 方法。

方法 SHALL 接受自然语言任务描述，返回可执行的 TraversalPlan。

返回的 TraversalPlan MUST 包含：
- `root_node`: TraversalNode 实例
- `template_registry_ref`: 引用的模板注册表
- `metadata`: 任务解析元数据

如果解析失败，方法 SHALL 抛出 `TaskParsingException`。

所有生成的节点 MUST 通过 SafetyPolicy.validate() 验证。

#### Scenario: 成功解析简单任务
- **WHEN** 输入任务 "遍历车辆设置菜单"
- **THEN** 返回的 TraversalPlan.root_node.name 为 "车辆设置"
- **AND** root_node.node_type 为 NodeType.CONTAINER
- **AND** root_node.children_strategy.type 为 DYNAMIC_MATCH

#### Scenario: 解析失败抛出异常
- **WHEN** 输入无法理解的任务 "执行任务 X"
- **THEN** 抛出 TaskParsingException
- **AND** 异常消息包含 "无法解析任务"

#### Scenario: 生成的节点通过安全验证
- **WHEN** AI 生成包含危险操作的节点
- **THEN** SafetyPolicy.validate() 拦截该节点
- **AND** 返回包含安全回退节点的 TraversalPlan

---

### Requirement: Page Type Verification

AIProvider SHALL 实现 `verify_page_type(analysis: PageAnalysis, expectation: PageExpectation) -> TypeCheckResult` 方法。

方法 SHALL 分析 PageAnalysis 内容，判断是否满足 PageExpectation。

返回的 TypeCheckResult MUST 包含：
- `is_match`: 是否匹配（布尔值）
- `confidence`: 匹配置信度（0-1）
- `actual_type`: 实际页面类型（字符串）
- `reasons`: 判断理由列表

如果置信度低于阈值（默认 0.7），is_match SHALL 为 False。

#### Scenario: 页面类型匹配成功
- **WHEN** 当前页面为 "设置页"，期望也为 "设置页"
- **THEN** TypeCheckResult.is_match 为 True
- **AND** confidence >= 0.8

#### Scenario: 页面类型不匹配
- **WHEN** 当前页面为 "车辆设置"，期望为 "DiLink"
- **THEN** TypeCheckResult.is_match 为 False
- **AND** actual_type 为 "车辆设置"

#### Scenario: 低置信度导致匹配失败
- **WHEN** confidence 为 0.65
- **THEN** is_match 为 False
- **AND** reasons 包含 "置信度过低"

---

### Requirement: Element Safety Pre-screening

AIProvider SHALL 实现 `screen_elements(items: List[MenuItem], context: Dict) -> List[MenuItem]` 方法。

方法 SHALL 批量分析元素列表，为每个元素的 `safety_tag` 字段赋值。

safety_tag 可选值：
- `safe`: 安全元素，可正常点击
- `caution`: 需谨慎，降低优先级
- `skip`: 跳过，不执行操作
- `unknown`: 无法判断，交由后续决策

方法 SHALL 返回修改后的 MenuItem 列表（原列表的副本或修改版本）。

#### Scenario: 标记危险元素为 skip
- **WHEN** MenuItem 文本为 "恢复出厂设置"
- **THEN** 该元素的 safety_tag 为 "skip"
- **AND** 状态机将跳过该元素

#### Scenario: 标记普通元素为 safe
- **WHEN** MenuItem 文本为 "亮度调节"
- **THEN** 该元素的 safety_tag 为 "safe"

#### Scenario: 无法判断时标记为 unknown
- **WHEN** MenuItem 文本为未知内容
- **THEN** 该元素的 safety_tag 为 "unknown"
- **AND** 该元素交由规则引擎进一步判断

---

### Requirement: Context-Aware Decision Making

AIProvider SHALL 实现 `make_decision(context: DecisionContext) -> (DecisionResult, Optional[TraversalNode])` 方法。

DecisionContext SHALL 包含：
- `trigger_reason`: 触发原因
- `ui_analysis`: 当前页面分析
- `traversal_context`: 遍历上下文（路径、栈状态等）
- `exception`: 异常信息（如果有）

DecisionResult SHALL 包含：
- `decision_type`: 决策类型枚举
- `confidence`: 决策置信度
- `reasoning`: 决策理由
- `action_params`: 动作参数字典

如果返回 TraversalNode，该节点 MUST 通过 SafetyPolicy.validate() 验证。

#### Scenario: 异常时建议重试
- **WHEN** 触发原因为 "element_not_found"
- **AND** 重试次数为 1
- **THEN** DecisionResult.decision_type 为 RETRY
- **AND** action_params 包含 wait_time: 2.0

#### Scenario: 路径错误时建议导航
- **WHEN** 当前路径与目标路径不匹配
- **THEN** DecisionResult.decision_type 为 NAVIGATE
- **AND** action_params.target_path 为正确路径

#### Scenario: 返回的节点通过安全验证
- **WHEN** AI 生成的节点包含危险操作
- **THEN** SafetyPolicy.validate() 拦截
- **AND** 返回 None 作为 TraversalNode

---

### Requirement: AI Provider Configuration

系统 SHALL 支持 `AIProviderConfig` 配置类。

配置 SHALL 包含：
- `provider_type`: 提供商类型（noop/claude/mimo）
- `model`: 模型名称
- `confidence_threshold`: 置信度阈值
- `timeout`: 调用超时时间
- `max_retries`: 失败重试次数

系统 SHALL 根据配置创建对应的 AIProvider 实例。

#### Scenario: 根据 provider_type 创建实例
- **WHEN** provider_type 为 "claude"
- **THEN** 创建 ClaudeAIProvider 实例
- **AND** 注入 SafetyPolicy

#### Scenario: provider_type 无效时抛出异常
- **WHEN** provider_type 为 "unknown"
- **THEN** 抛出 ValueError

---

### Requirement: Prompt Template Management

系统 SHALL 提供 Prompt 模板管理，支持模板定义和变量替换。

每个 AI 能力 SHALL 对应一个 Prompt 模板文件。

模板 SHALL 支持以下变量：
- `{{context}}`: 上下文信息
- `{{task}}`: 任务描述
- `{{items}}`: 元素列表
- `{{exception}}`: 异常信息

系统 SHALL 在调用 AI 前替换所有模板变量。

#### Scenario: 模板变量替换
- **WHEN** 模板包含 "{{task}}"，任务为 "遍历设置"
- **THEN** 替换后的 Prompt 包含 "遍历设置"

#### Scenario: 缺少必需变量时抛出异常
- **WHEN** 模板包含 "{{task}}"，但未提供任务
- **THEN** 抛出 TemplateVariableError
