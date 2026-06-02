## ADDED Requirements

### Requirement: POPUP_HANDLING 状态

系统 SHALL 在 TraversalStateMachine 中添加 POPUP_HANDLING 状态。

#### Scenario: 检测到弹窗
- **WHEN** RESULT_VERIFY 状态检测到弹窗存在
- **THEN** 系统 SHALL 转移到 POPUP_HANDLING 状态

#### Scenario: 执行弹窗处理
- **WHEN** 处于 POPUP_HANDLING 状态
- **THEN** 系统 SHALL 调用 handle_popup() 方法

### Requirement: 优先级 1 - 查找取消按钮

系统 SHALL 优先查找并点击"取消"或"关闭"按钮。

#### Scenario: 找到取消按钮
- **WHEN** 页面中存在包含"取消"或"关闭"文本的按钮
- **THEN** 系统 SHALL：
  1. 点击该按钮
  2. 等待 0.5 秒
  3. 转移到 RESULT_VERIFY 状态

#### Scenario: 按钮文本匹配
- **WHEN** 查找取消按钮
- **THEN** 系统 SHALL 匹配以下文本变体：
  - "取消"、"关闭"、"Close"、"Cancel"
  - 及其常见变体

### Requirement: 优先级 2 - 执行 Back 操作

系统 SHALL 在找不到取消按钮时尝试 Back 操作。

#### Scenario: 执行 Back
- **WHEN** 未找到取消按钮
- **THEN** 系统 SHALL：
  1. 执行 Back 键操作
  2. 等待 0.5 秒
  3. 继续验证弹窗是否消失

### Requirement: 弹窗验证

系统 SHALL 在执行关闭操作后验证弹窗是否消失。

#### Scenario: 弹窗消失
- **WHEN** 执行关闭操作后弹窗消失
- **THEN** 系统 SHALL：
  1. 转移到 RESULT_VERIFY 状态
  2. 继续正常流程

#### Scenario: 弹窗仍存在
- **WHEN** 执行关闭操作后弹窗仍然存在
- **THEN** 系统 SHALL 进入优先级 3 处理

### Requirement: 优先级 3 - AI 决策

系统 SHALL 在常规方法失败时调用 AI 决策。

#### Scenario: AI 提供解决方案
- **WHEN** AI 可用且返回操作建议
- **THEN** 系统 SHALL：
  1. 执行 AI 建议的操作
  2. 转移到 RESULT_VERIFY 状态

#### Scenario: AI 无解决方案
- **WHEN** AI 不可用或未返回操作建议
- **THEN** 系统 SHALL 转移到 ERROR_HANDLING 状态

### Requirement: 弹窗检测

系统 SHALL 能够识别页面中的弹窗元素。

#### Scenario: 弹窗特征识别
- **WHEN** 分析页面时
- **THEN** 系统 SHALL 识别具有以下特征的弹窗：
  - 覆盖大部分屏幕的半透明或实心背景
  - 包含标题、消息和按钮区域
  - 位于其他元素之上（z-index）

#### Scenario: 弹窗判定
- **WHEN** 检测到弹窗特征
- **THEN** 系统 SHALL 判定为弹窗存在

### Requirement: 弹窗处理超时

系统 SHALL 对弹窗处理设置超时限制。

#### Scenario: 处理超时
- **WHEN** 弹窗处理尝试超过 3 次
- **THEN** 系统 SHALL：
  1. 停止尝试
  2. 转移到 ERROR_HANDLING 状态

#### Scenario: 超时计数器
- **WHEN** 每次弹窗处理尝试
- **THEN** 系统 SHALL 增加尝试计数器

### Requirement: 弹窗处理日志

系统 SHALL 记录所有弹窗处理事件。

#### Scenario: 记录弹窗检测
- **WHEN** 检测到弹窗
- **THEN** 系统 SHALL 在 Trace 中记录：
  - 弹窗类型
  - 检测时间
  - 尝试的处理方法

#### Scenario: 记录处理结果
- **WHEN** 弹窗处理完成
- **THEN** 系统 SHALL 在 Trace 中记录：
  - 使用的处理方法
  - 处理结果（成功/失败）
  - 目标状态

### Requirement: 常见弹窗类型处理

系统 SHALL 支持处理常见类型的弹窗。

#### Scenario: 权限请求弹窗
- **WHEN** 检测到权限请求弹窗
- **THEN** 系统 SHALL 点击"允许"或"拒绝"按钮

#### Scenario: 广告弹窗
- **WHEN** 检测到广告弹窗
- **THEN** 系统 SHALL 查找并点击"跳过"或"关闭"按钮

#### Scenario: 系统对话框
- **WHEN** 检测到系统对话框
- **THEN** 系统 SHALL 优先执行 Back 操作

### Requirement: 弹窗处理失败转移

系统 SHALL 在弹窗处理完全失败时转移到错误处理状态。

#### Scenario: 所有方法失败
- **WHEN** 所有弹窗处理方法都失败
- **THEN** 系统 SHALL 转移到 ERROR_HANDLING 状态

#### Scenario: 错误信息传递
- **WHEN** 转移到 ERROR_HANDLING
- **THEN** 系统 SHALL 传递弹窗处理失败信息
