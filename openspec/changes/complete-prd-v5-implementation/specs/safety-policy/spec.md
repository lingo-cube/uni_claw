## ADDED Requirements

### Requirement: SafetyPolicy Core Interface

系统 SHALL 定义 `SafetyPolicy` 抽象接口。

SafetyPolicy SHALL 提供 `validate(node: TraversalNode, context: DecisionContext) -> ValidationResult` 方法。

ValidationResult SHALL 包含：
- `is_safe`: 是否安全（布尔值）
- `reason`: 不安全原因（如果 is_safe=False）
- `fallback_node`: 安全回退节点（可选）

如果节点不安全，方法 SHALL 返回 is_safe=False 和可选的 fallback_node。

#### Scenario: 安全节点通过验证
- **WHEN** 节点操作为点击"移动数据"
- **THEN** ValidationResult.is_safe 为 True
- **AND** fallback_node 为 None

#### Scenario: 危险节点被拦截
- **WHEN** 节点操作包含"恢复出厂设置"
- **THEN** ValidationResult.is_safe 为 False
- **AND** reason 包含"危险操作"

---

### Requirement: Layer 0 - Element Pre-screening

系统 SHALL 在视觉分析后、AI 调用前执行第零层安全检查。

第零层 SHALL 使用规则黑名单进行初步过滤。

黑名单规则 SHALL 支持：
- 精确文本匹配
- 正则表达式匹配
- 文本包含匹配

被标记为 `skip` 的元素 SHALL 不传递给 AI 进行预筛。

#### Scenario: 黑名单文本元素被过滤
- **WHEN** MenuItem 文本为 "删除所有数据"
- **AND** 该文本在黑名单中
- **THEN** 元素不传递给 AI
- **AND** 元素的 safety_tag 直接设为 "skip"

#### Scenario: 正则表达式匹配
- **WHEN** MenuItem 文本为 "格式化 /sdcard"
- **AND** 黑名单包含正则 "格式化.*"
- **THEN** 元素被标记为 skip

---

### Requirement: Layer 1 - AIProvider Internal Validation

AIProvider SHALL 在生成 TraversalNode 后、返回前调用 SafetyPolicy.validate()。

如果验证失败，AIProvider SHALL：
1. 使用 fallback_node（如果提供）
2. 或返回无操作节点
3. 或返回空/None

AIProvider 不得绕过此验证直接返回不安全节点。

#### Scenario: AI 生成的危险节点被拦截
- **WHEN** AI 生成节点操作为"重启设备"
- **AND** SafetyPolicy.validate() 返回 is_safe=False
- **THEN** AIProvider 使用 fallback_node
- **OR** AIProvider 返回不包含危险操作的节点

#### Scenario: 无回退节点时返回空
- **WHEN** 验证失败且无 fallback_node
- **THEN** 返回 None
- **AND** 调用方处理空返回情况

---

### Requirement: Layer 2 - Global Safety Filter

系统 SHALL 提供全局 SafetyFilter 单例。

SafetyFilter SHALL 在所有节点执行前进行最终检查。

检查 SHALL 包括：
- 操作白名单验证
- 坐标越界检查
- 文本黑名单二次验证

如果检查失败，系统 SHALL 阻止执行并记录审计日志。

#### Scenario: 操作白名单验证
- **WHEN** 节点操作为 "format_command"（非白名单）
- **THEN** SafetyFilter 阻止执行
- **AND** 记录审计日志

#### Scenario: 坐标越界检查
- **WHEN** 节点坐标为 (1.5, 0.5)
- **THEN** SafetyFilter 阻止执行
- **AND** 记录"坐标越界"错误

#### Scenario: 白名单操作通过
- **WHEN** 节点操作为 "click"
- **AND** 坐标在有效范围内
- **THEN** SafetyFilter 允许执行

---

### Requirement: Layer 3 - Device Driver Protection

设备驱动层 SHALL 在执行操作时进行系统级安全检查。

检查 SHALL 包括：
- ADB 权限验证
- 系统命令白名单
- 文件路径保护

驱动层 SHALL 拒绝未授权的操作并抛出异常。

#### Scenario: ADB 权限不足
- **WHEN** 执行需要 root 权限的操作
- **AND** ADB 未获得 root 权限
- **THEN** 驱动层抛出 PermissionDeniedException

#### Scenario: 系统命令被拒绝
- **WHEN** 尝试执行 "rm -rf /system" 命令
- **THEN** 驱动层拒绝执行
- **AND** 抛出 CommandNotAllowedException

---

### Requirement: Blacklist Configuration

系统 SHALL 支持黑名单配置文件。

配置文件 SHALL 为 JSON 格式，包含以下字段：
- `text_blacklist`: 精确文本列表
- `pattern_blacklist`: 正则表达式列表
- `action_blacklist`: 危险操作列表

系统 SHALL 在启动时加载黑名单配置。

黑名单 SHALL 支持热重载（可选功能）。

#### Scenario: 加载黑名单文件
- **WHEN** 系统启动
- **THEN** 从 config/safety_blacklist.json 加载黑名单
- **AND** 解析为内存数据结构

#### Scenario: 黑名单匹配逻辑
- **WHEN** 文本黑名单包含 ["恢复出厂设置", "删除数据"]
- **AND** 元素文本为 "恢复出厂设置"
- **THEN** 匹配成功

---

### Requirement: Whitelist Configuration

系统 SHALL 支持白名单配置文件。

白名单 SHALL 定义允许的操作类型。

系统 SHALL 在第二层（全局过滤器）使用白名单验证。

不在白名单的操作 SHALL 被拒绝。

#### Scenario: 白名单操作通过
- **WHEN** 白名单包含 ["click", "swipe", "back"]
- **AND** 节点操作为 "click"
- **THEN** 验证通过

#### Scenario: 非白名单操作被拒绝
- **WHEN** 白名单包含 ["click"]
- **AND** 节点操作为 "execute_shell"
- **THEN** 验证失败

---

### Requirement: Safety Audit Logging

系统 SHALL 记录所有安全拦截事件。

审计日志 SHALL 包含：
- 时间戳
- 拦截层级
- 节点信息
- 拦截原因
- 上下文快照

日志 SHALL 输出到独立的安全日志文件。

系统 SHALL 支持日志查询和统计分析。

#### Scenario: 记录安全拦截
- **WHEN** SafetyFilter 拦截危险节点
- **THEN** 写入安全日志
- **AND** 日志包含节点 ID 和拦截原因

#### Scenario: 查询安全日志
- **WHEN** 调用 get_safety_log() 方法
- **THEN** 返回所有拦截记录
- **AND** 记录按时间倒序排列

---

### Requirement: Safety Policy Customization

系统 SHALL 支持用户自定义安全策略。

用户 SHALL 能够：
- 添加自定义黑名单规则
- 添加自定义白名单规则
- 注册自定义安全检查器

自定义检查器 SHALL 实现 `SafetyChecker` 接口。

系统 SHALL 在各层级按优先级执行检查器。

#### Scenario: 注册自定义检查器
- **WHEN** 用户注册 CustomSafetyChecker
- **THEN** 检查器被添加到执行链
- **AND** 每次验证时调用该检查器

#### Scenario: 自定义黑名单优先级
- **WHEN** 同时存在默认黑名单和自定义黑名单
- **THEN** 两者都生效
- **AND** 任一匹配即拦截
