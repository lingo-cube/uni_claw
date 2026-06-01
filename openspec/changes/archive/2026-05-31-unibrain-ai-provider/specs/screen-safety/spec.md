## ADDED Requirements

### Requirement: 元素安全筛选能力
系统 SHALL 提供 `ScreenSafetyCapability` 能力，筛选页面中的安全/不安全元素。

#### Scenario: 成功筛选安全元素
- **WHEN** 页面包含多个元素
- **THEN** 返回 `SafetyScreeningResult` 包含每个元素的安全评估

#### Scenario: 任务上下文感知
- **WHEN** 提供用户任务指令
- **THEN** 安全评估考虑任务相关性
- **AND** 与任务相关的元素优先标记为 safe（除非明显危险）

### Requirement: 安全等级定义
系统 SHALL 定义四类安全等级。

#### Scenario: safe 等级
- **WHEN** 元素为常规菜单项、开关、标签页、返回按钮
- **THEN** 标记为 `safe`
- **AND** 操作不会产生不可逆后果

#### Scenario: caution 等级
- **WHEN** 元素含义模糊或可能触发敏感操作
- **THEN** 标记为 `caution`
- **AND** 需要谨慎处理

#### Scenario: skip 等级
- **WHEN** 元素包含破坏性词汇
- **THEN** 标记为 `skip`
- **AND** 绝对禁止点击

#### Scenario: unknown 等级
- **WHEN** 元素信息不足无法判断
- **THEN** 标记为 `unknown`

### Requirement: 破坏性词汇检测
系统 SHALL 自动识别包含破坏性词汇的元素并标记为 skip。

#### Scenario: 恢复出厂设置
- **WHEN** 元素名称包含 "恢复出厂设置"
- **THEN** 标记为 `skip`

#### Scenario: 数据清除操作
- **WHEN** 元素名称包含 "清除数据"、"删除"、"卸载"、"格式化"、"重置"
- **THEN** 标记为 `skip`

#### Scenario: 退出操作
- **WHEN** 元素名称包含 "退出"、"注销"、"登出"、"关机"
- **THEN** 标记为 `skip`

#### Scenario: 敏感权限请求
- **WHEN** 元素涉及 "读取通讯录"、"读取短信"、"定位权限"
- **THEN** 标记为 `skip`

#### Scenario: 支付相关
- **WHEN** 元素包含 "购买"、"支付"、"充值"、"付款"
- **THEN** 标记为 `skip`

### Requirement: 单个元素评估
系统 SHALL 为每个元素提供详细的安全评估。

#### Scenario: 安全评估结构
- **WHEN** 返回元素评估
- **THEN** 包含以下字段：
  - `name`: 元素名称
  - `safety_tag`: 安全等级
  - `confidence`: 置信度 (0.0-1.0)
  - `reason`: 判断理由
  - `context_dependency`: 上下文依赖说明（可选）
  - `task_relevance`: 任务相关性说明（可选）

### Requirement: 页面级安全指导
系统 SHALL 提供页面级别的安全指导。

#### Scenario: 页面安全状态
- **WHEN** 评估页面整体安全性
- **THEN** `page_level_guidance` 包含：
  - `overall_safe_to_proceed`: 是否整体安全
  - `recommended_max_parallel`: 推荐最大并行操作数
  - `special_precautions`: 特殊注意事项列表
  - `task_suitability`: 页面与任务匹配度说明（可选）

#### Scenario: 不安全页面
- **WHEN** 页面包含多个 skip 或 caution 元素
- **THEN** `overall_safe_to_proceed` 为 false
- **AND** `recommended_max_parallel` 降低到 1
- **AND** `special_precautions` 包含警告

### Requirement: 上下文感知规则
系统 SHALL 在安全评估中应用上下文感知规则。

#### Scenario: 任务相关性优先
- **WHEN** 元素与用户任务直接相关
- **THEN** 优先标记为 `safe`（除非明显危险）

#### Scenario: 路径依赖性
- **WHEN** 元素在任务路径上
- **THEN** 即使含义模糊也可以谨慎探索

#### Scenario: 危险操作绝对禁止
- **WHEN** 元素包含破坏性词汇
- **THEN** 无论任务如何，始终标记为 `skip`

#### Scenario: 分支选择建议
- **WHEN** 遇到 caution 元素
- **THEN** 根据任务相关性给出是否探索的建议

### Requirement: Prompt 模板
系统 SHALL 为安全筛选提供优化的 Prompt 模板。

#### Scenario: 系统提示词
- **WHEN** 获取系统 Prompt
- **THEN** 包含以下内容：
  - 各安全等级的定义
  - 破坏性词汇列表
  - 上下文感知规则
  - 输出 JSON 格式规范

#### Scenario: 用户提示词
- **WHEN** 获取用户 Prompt
- **THEN** 包含以下占位符：
  - `{instruction}`: 用户任务指令
  - `{current_path}`: 当前页面路径
  - `{page_type}`: 当前页面类型
  - `{is_popup}`: 是否弹窗
  - `{elements_list}`: 待评估元素列表

### Requirement: 响应 Schema
系统 SHALL 定义安全筛选的 JSON Schema。

#### Scenario: Schema 验证
- **WHEN** AI 返回响应
- **THEN** 响应符合以下 Schema：
  - `evaluations`: 数组（必需）
  - `page_level_guidance`: 对象（可选）

### Requirement: 安全降级策略
系统 SHALL 在安全筛选失败时进入安全模式。

#### Scenario: 安全筛选失败
- **WHEN** `ScreenSafetyCapability` 执行失败
- **THEN** 系统进入安全模式
- **AND** 仅允许 back、skip、no_action 操作
- **AND** 记录安全事件到审计日志

#### Scenario: 安全模式操作
- **WHEN** 系统处于安全模式
- **THEN** 禁止所有点击操作
- **AND** 执行 back 返回上一级
- **AND** 直到回到已知安全状态

### Requirement: 解析器注册
系统 SHALL 注册 `SafetyScreeningResult` 的解析器。

#### Scenario: 解析器函数
- **WHEN** 注册解析器
- **THEN** 解析器将 JSON 响应转换为 `SafetyScreeningResult` 数据对象
