## ADDED Requirements

### Requirement: 页面类型验证能力
系统 SHALL 提供 `VerifyPageTypeCapability` 能力，验证当前页面类型是否符合预期。

#### Scenario: 成功验证页面类型
- **WHEN** 当前页面与预期类型匹配
- **THEN** 返回 `PageTypeVerification` 包含：
  - `is_match`: true
  - `confidence`: 0.7-1.0
  - `actual_type`: 匹配的页面类型
  - `reasoning`: 验证通过的原因

#### Scenario: 页面类型不匹配
- **WHEN** 当前页面与预期类型不匹配
- **THEN** 返回 `PageTypeVerification` 包含：
  - `is_match`: false
  - `actual_type`: 实际检测到的页面类型
  - `mismatch_details`: 不匹配详情
  - `suggestion`: 恢复建议

#### Scenario: 无法确定页面类型
- **WHEN** AI 无法确定页面类型
- **THEN** 返回 `actual_type` 为 "unknown"
- **AND** `confidence` 低于 0.5

### Requirement: 页面类型定义
系统 SHALL 定义标准的页面类型枚举。

#### Scenario: 页面类型列表
- **WHEN** 定义页面类型
- **THEN** 支持以下类型：
  - `menu_list`: 顶部有水平菜单，内容区大量 menu_item
  - `settings_group`: 混合多种控件类型的设置页
  - `dialog`: 弹窗对话框
  - `home_desktop`: 主页面，包含应用图标
  - `leaf_page`: 纯信息展示页
  - `unknown`: 无法归类

### Requirement: 不匹配详情
系统 SHALL 提供详细的页面不匹配信息。

#### Scenario: 缺失必要元素
- **WHEN** 预期页面应包含某些元素但实际缺失
- **THEN** `mismatch_details.missing_items` 列出缺失元素

#### Scenario: 出现意外元素
- **WHEN** 当前页面包含预期之外的元素
- **THEN** `mismatch_details.unexpected_items` 列出意外元素

#### Scenario: 类型冲突
- **WHEN** 页面特征与多种类型冲突
- **THEN** `mismatch_details.type_conflict` 说明冲突原因

### Requirement: 恢复建议
系统 SHALL 为页面不匹配提供恢复建议。

#### Scenario: 返回操作建议
- **WHEN** 页面类型不匹配且需要返回
- **THEN** `suggestion.action` 为 "back"
- **AND** `suggestion.reason` 说明需要返回的原因

#### Scenario: 重试建议
- **WHEN** 页面加载可能不完整
- **THEN** `suggestion.action` 为 "retry"

#### Scenario: 跳过建议
- **WHEN** 页面不匹配但不影响继续
- **THEN** `suggestion.action` 为 "skip"

#### Scenario: 关闭弹窗建议
- **WHEN** 检测到弹窗阻挡目标页面
- **THEN** `suggestion.action` 为 "close_popup"
- **AND** `suggestion.target` 指向关闭按钮位置

### Requirement: Prompt 模板
系统 SHALL 为页面验证提供优化的 Prompt 模板。

#### Scenario: 系统提示词
- **WHEN** 获取系统 Prompt
- **THEN** 包含以下内容：
  - 各页面类型的定义和特征
  - 判断规则和优先级
  - 弹窗处理规则
  - 输出 JSON 格式规范

#### Scenario: 用户提示词
- **WHEN** 获取用户 Prompt
- **THEN** 包含以下占位符：
  - `{expected_type}`: 预期页面类型
  - `{expected_page_name}`: 预期页面名称
  - `{required_items}`: 预期必要元素
  - `{current_path}`: 当前路径
  - `{is_popup}`: 是否弹窗
  - `{level1_menus_summary}`: 一级菜单摘要
  - `{level2_menus_summary}`: 二级菜单摘要
  - `{elements_detail}`: 元素详情列表

### Requirement: 响应 Schema
系统 SHALL 定义页面验证的 JSON Schema。

#### Scenario: Schema 验证
- **WHEN** AI 返回响应
- **THEN** 响应符合以下 Schema：
  - `is_match`: 布尔值（必需）
  - `confidence`: 0.0-1.0 数字（必需）
  - `actual_type`: 页面类型枚举（必需）
  - `reasoning`: 字符串（必需）
  - `mismatch_details`: 对象（可选）
  - `suggestion`: 对象（可选）

### Requirement: 解析器注册
系统 SHALL 注册 `PageTypeVerification` 的解析器。

#### Scenario: 解析器函数
- **WHEN** 注册解析器
- **THEN** 解析器将 JSON 响应转换为 `PageTypeVerification` 数据对象
- **AND** 处理可选字段的默认值
