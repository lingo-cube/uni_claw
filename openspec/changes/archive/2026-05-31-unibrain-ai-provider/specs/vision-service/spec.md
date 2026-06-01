## ADDED Requirements

### Requirement: Vision Service 抽象接口
系统 SHALL 提供 `VisionService` 抽象基类，定义视觉分析服务的标准接口。

#### Scenario: 接口定义
- **WHEN** 查看 `VisionService` 抽象类
- **THEN** 该类包含以下抽象方法：
  - `analyze_screenshot(image_data: bytes) -> PageAnalysis`
  - `find_app_entry(image_data: bytes, target: str) -> dict | None`

### Requirement: 屏幕截图分析
系统 SHALL 分析车机屏幕截图并提取页面结构信息。

#### Scenario: 成功分析截图
- **WHEN** 输入 PNG 图片字节数据
- **THEN** 返回完整的 `PageAnalysis` 对象
- **AND** 包含所有检测到的元素信息

#### Scenario: 识别菜单方向
- **WHEN** 截图包含水平或垂直菜单
- **THEN** 正确识别菜单方向（left/right/top/bottom）

#### Scenario: 识别菜单激活状态
- **WHEN** 某些菜单项处于激活/高亮状态
- **THEN** 正确标记激活状态
- **AND** `current_path` 反映当前路径

### Requirement: 应用入口查找
系统 SHALL 在主页面上查找目标应用图标。

#### Scenario: 成功找到应用
- **WHEN** 目标应用图标存在于主页面
- **THEN** 返回包含以下字段的字典：
  - `found`: true
  - `name`: 应用名称
  - `x`: 归一化 X 坐标 (0.0-1.0)
  - `y`: 归一化 Y 坐标 (0.0-1.0)
  - `confidence`: 置信度 (0.0-1.0)

#### Scenario: 未找到应用
- **WHEN** 目标应用图标不存在
- **THEN** 返回包含：
  - `found`: false
  - `coordinates`: null

### Requirement: Claude Vision 实现
系统 SHALL 提供使用 Claude API 的 Vision Service 实现。

#### Scenario: 创建 Claude Vision Service
- **WHEN** 创建 `ClaudeVisionService`
- **THEN** 接受 API 密钥和模型名称参数
- **AND** 默认模型为 "claude-3-5-sonnet-20241022"

#### Scenario: 调用 Claude Vision API
- **WHEN** 执行视觉分析
- **THEN** 将图片编码为 base64
- **AND** 构建 Claude API 消息格式
- **AND** 调用 Claude API
- **AND** 解析返回的文本为 JSON

### Requirement: MiMo Vision 实现
系统 SHALL 提供使用小米 MiMo API 的 Vision Service 实现（如果可用）。

#### Scenario: 创建 MiMo Vision Service
- **WHEN** 创建 `MiMoVisionService`
- **THEN** 接受 API 密钥参数
- **AND** 配置 MiMo API 基础 URL

#### Scenario: 调用 MiMo Vision API
- **WHEN** 执行视觉分析
- **THEN** 调用 MiMo Vision API
- **AND** 处理 MiMo 特定的响应格式

### Requirement: Mock Vision 实现
系统 SHALL 提供用于测试的 Mock Vision Service。

#### Scenario: 创建 Mock Vision Service
- **WHEN** 创建 `MockVisionService`
- **THEN** 不需要 API 密钥

#### Scenario: 返回预设响应
- **WHEN** 使用 Mock 服务
- **THEN** 可以添加预设响应
- **AND** 按顺序返回预设响应

#### Scenario: 默认 Mock 响应
- **WHEN** Mock 服务无预设响应
- **THEN** 返回默认的 `PageAnalysis` 对象

### Requirement: Vision Service 配置
系统 SHALL 提供 Vision Service 配置数据类。

#### Scenario: 配置结构
- **WHEN** 创建 `VisionConfig`
- **THEN** 包含以下字段：
  - `service_type`: 服务类型（claude/mimo/mock）
  - `api_key`: API 密钥
  - `model`: 模型名称（可选）
  - `timeout`: 超时时间
  - `max_retries`: 最大重试次数

### Requirement: Vision Service 工厂
系统 SHALL 提供工厂函数创建 Vision Service 实例。

#### Scenario: 创建 Claude 服务
- **WHEN** `service_type` 为 "claude"
- **THEN** 返回 `ClaudeVisionService` 实例

#### Scenario: 创建 MiMo 服务
- **WHEN** `service_type` 为 "mimo"
- **THEN** 返回 `MiMoVisionService` 实例

#### Scenario: 创建 Mock 服务
- **WHEN** `service_type` 为 "mock"
- **THEN** 返回 `MockVisionService` 实例

#### Scenario: 未知服务类型
- **WHEN** `service_type` 为未知值
- **THEN** 抛出 `ValueError` 异常

### Requirement: 图片编码
系统 SHALL 提供 base64 图片编码功能。

#### Scenario: 编码 PNG 图片
- **WHEN** 输入 PNG 图片字节数据
- **THEN** 返回 base64 编码的字符串
- **AND** 包含正确的 MIME 类型前缀

### Requirement: JSON 提取
系统 SHALL 从 Vision API 响应中提取 JSON 数据。

#### Scenario: 提取纯 JSON 响应
- **WHEN** API 返回纯 JSON 文本
- **THEN** 直接解析为字典

#### Scenario: 提取代码块中的 JSON
- **WHEN** API 返回包含 ```json 代码块
- **THEN** 提取代码块内容并解析

#### Scenario: JSON 解析失败
- **WHEN** 响应无法解析为 JSON
- **THEN** 抛出 `VisionError` 异常

### Requirement: Vision 错误处理
系统 SHALL 定义 Vision Service 专用错误类型。

#### Scenario: Vision 错误类型
- **WHEN** Vision Service 发生错误
- **THEN** 抛出 `VisionError` 异常
- **AND** 错误信息说明失败原因

### Requirement: PageAnalysis 数据结构
系统 SHALL 定义 Vision Service 输出的数据结构。

#### Scenario: 方向枚举
- **WHEN** 定义菜单方向
- **THEN** 支持以下值：
  - LEFT
  - RIGHT
  - TOP
  - BOTTOM

#### Scenario: 坐标数据类
- **WHEN** 定义坐标
- **THEN** 包含 x 和 y 字段
- **AND** 值域为 0.0-1.0

#### Scenario: 菜单信息数据类
- **WHEN** 定义菜单项信息
- **THEN** 包含：
  - `name`: 菜单名称
  - `coordinate`: 坐标对象
  - `active`: 是否激活

#### Scenario: 菜单项类型枚举
- **WHEN** 定义元素类型
- **THEN** 支持以下类型：
  - MENU_ITEM, TAB, BACK_BUTTON
  - SWITCH, TOGGLE, BUTTON
  - ICON, LINK, TEXT, READONLY

#### Scenario: 预期行为枚举
- **WHEN** 定义预期行为
- **THEN** 支持以下值：
  - NAVIGATE, TOGGLE, ACTION, NONE

#### Scenario: 菜单项数据类
- **WHEN** 定义内容项
- **THEN** 包含：
  - `name`: 名称
  - `type`: 类型枚举
  - `coordinate`: 坐标对象
  - `parent`: 父项名称（可选）
  - `description`: 描述（可选）
  - `expected_action`: 预期行为枚举
  - `expects_page_change`: 是否期望页面变化
  - `expects_state_change`: 是否期望状态变化

#### Scenario: 弹窗信息数据类
- **WHEN** 定义弹窗信息
- **THEN** 包含：
  - `title`: 弹窗标题
  - `content`: 弹窗内容
  - `close_button`: 关闭按钮坐标（可选）

#### Scenario: 页面分析数据类
- **WHEN** 定义完整页面分析
- **THEN** 包含：
  - 菜单结构（level1/level2）
  - 当前路径
  - 内容项列表
  - 特殊元素（弹窗、按钮）
  - 导航提示（滚动、列表末尾）
