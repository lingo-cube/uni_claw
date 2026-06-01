## ADDED Requirements

### Requirement: 安全过滤器验证
系统 SHALL 提供 `SafetyFilter` 类，验证 AI 输出的 `TraversalNode` 操作安全性。

#### Scenario: 验证通过
- **WHEN** AI 输出的操作类型在白名单中且目标文本不在黑名单中
- **THEN** 返回 `SafetyResult` 其中 `is_safe=True`
- **AND** 允许执行该操作

#### Scenario: 操作类型不在白名单
- **WHEN** AI 输出的操作类型不在白名单中
- **THEN** 返回 `SafetyResult` 其中 `is_safe=False`
- **AND** 包含拒绝原因
- **AND** 提供 `fallback_node` 为跳过当前操作

#### Scenario: 目标文本在黑名单
- **WHEN** AI 输出的目标文本匹配黑名单（如"恢复出厂设置"、"清除数据"）
- **THEN** 返回 `SafetyResult` 其中 `is_safe=False`
- **AND** 记录审计日志
- **AND** 提供 `fallback_node` 为跳过当前操作

### Requirement: 白名单操作类型
系统 SHALL 定义允许的操作类型白名单。

#### Scenario: 白名单包含的操作
- **WHEN** 查看白名单
- **THEN** 包含以下操作：`click`, `swipe`, `back`, `input_text`, `no_action`

#### Scenario: 白名单外的操作
- **WHEN** AI 输出操作为 `delete` 或 `format` 等危险操作
- **THEN** 安全过滤器拒绝该操作
- **AND** 返回 `fallback_node`

### Requirement: 黑名单危险文本
系统 SHALL定义危险操作文本黑名单，防止执行不可逆操作。

#### Scenario: 黑名单包含的文本
- **WHEN** 查看黑名单
- **THEN** 包含以下文本："恢复出厂设置", "清除数据", "删除所有", "格式化", "重置系统"

#### Scenario: 部分匹配黑名单
- **WHEN** AI 输出的目标文本包含黑名单关键词（如"清除数据"）
- **THEN** 安全过滤器拒绝该操作
- **AND** 记录审计日志

### Requirement: 审计日志
系统 SHALL 记录所有被安全过滤器拒绝的操作。

#### Scenario: 记录拒绝操作
- **WHEN** 安全过滤器拒绝一个操作
- **THEN** 记录以下信息到审计日志：
  - AI 输出的原始操作
  - 拒绝原因（操作类型或目标文本）
  - 时间戳
  - 当前遍历路径

### Requirement: Fallback 节点
系统 SHALL 为被拒绝的操作提供安全的 fallback 节点。

#### Scenario: 跳过当前操作
- **WHEN** 操作被安全过滤器拒绝
- **THEN** `fallback_node` 操作类型为 `no_action`
- **AND** 标记该节点为"已跳过"
- **AND** 遍历继续进行
