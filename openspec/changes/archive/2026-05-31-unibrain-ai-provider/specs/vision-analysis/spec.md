## ADDED Requirements

### Requirement: 屏幕视觉分析能力
系统 SHALL 提供 `VisionAnalysisCapability` 能力，使用视觉 AI 分析车机屏幕截图。

#### Scenario: 分析截图提取页面结构
- **WHEN** 输入 PNG 图片数据
- **THEN** 返回 `PageAnalysis` 包含完整页面结构信息

#### Scenario: 识别菜单结构
- **WHEN** 截图包含一级和二级菜单
- **THEN** `PageAnalysis` 包含：
  - `level1_dir`: 一级菜单方向
  - `level1_menus`: 一级菜单列表（名称、位置、激活状态）
  - `level2_dir`: 二级菜单方向
  - `level2_menus`: 二级菜单列表

#### Scenario: 识别当前路径
- **WHEN** 某些菜单处于激活状态
- **THEN** `PageAnalysis.current_path` 包含激活菜单的名称列表

### Requirement: 元素类型识别
系统 SHALL 识别并分类页面中的所有可点击元素。

#### Scenario: 元素类型分类
- **WHEN** 识别元素类型
- **THEN** 支持以下类型：
  - `menu_item`: 导航到子页面的列表项
  - `tab`: 切换视图的标签页按钮
  - `back_button`: 返回导航按钮
  - `switch`: 改变状态的开关
  - `toggle`: 切换按钮
  - `button`: 通用操作按钮
  - `link`: 导航链接
  - `icon`: 无文字的图标按钮
  - `text`: 非交互文本
  - `readonly`: 只读元素

### Requirement: 预期行为预测
系统 SHALL 预测每个元素的预期行为。

#### Scenario: 导航行为
- **WHEN** 元素类型为 menu_item、tab、back_button
- **THEN** `expected_action` 为 "navigate"
- **AND** `expects_page_change` 为 true

#### Scenario: 切换行为
- **WHEN** 元素类型为 switch、toggle
- **THEN** `expected_action` 为 "toggle"
- **AND** `expects_state_change` 为 true

#### Scenario: 操作行为
- **WHEN** 元素类型为 button、link
- **THEN** `expected_action` 为 "action"
- **AND** `expects_page_change` 为 true

#### Scenario: 无响应行为
- **WHEN** 元素类型为 text、readonly
- **THEN** `expected_action` 为 "none"
- **AND** `expects_page_change` 和 `expects_state_change` 均为 false

### Requirement: 特殊元素识别
系统 SHALL 识别页面中的特殊 UI 元素。

#### Scenario: 弹窗识别
- **WHEN** 页面包含弹窗或对话框
- **THEN** `PageAnalysis.is_popup` 为 true
- **AND** `popup_info` 包含弹窗标题、内容、关闭按钮位置

#### Scenario: 关闭按钮识别
- **WHEN** 页面有明显的关闭按钮（X 或类似图标）
- **THEN** `PageAnalysis.close_button` 包含按钮坐标

#### Scenario: 返回按钮识别
- **WHEN** 页面有返回按钮
- **THEN** `PageAnalysis.back_button` 包含按钮坐标

#### Scenario: 滚动识别
- **WHEN** 页面内容可滚动
- **THEN** `PageAnalysis.has_scroll` 为 true

#### Scenario: 列表末尾识别
- **WHEN** 滚动到列表底部
- **THEN** `PageAnalysis.is_end_of_list` 为 true

### Requirement: 坐标归一化
系统 SHALL 使用归一化坐标 (0.0-1.0) 表示元素位置。

#### Scenario: 坐标格式
- **WHEN** 返回元素坐标
- **THEN** 坐标为 0.0-1.0 范围的相对坐标
- **AND** (0.0, 0.0) 表示左上角
- **AND** (1.0, 1.0) 表示右下角

#### Scenario: MenuInfo 坐标
- **WHEN** 返回菜单项信息
- **THEN** `MenuInfo.coordinate` 包含归一化坐标

#### Scenario: MenuItem 坐标
- **WHEN** 返回内容项信息
- **THEN** `MenuItem.coordinate` 包含归一化坐标

### Requirement: 父子关系标记
系统 SHALL 标记元素之间的父子关系。

#### Scenario: 开关与父项
- **WHEN** 开关属于某个菜单项
- **THEN** 开关的 `parent` 字段指向父项名称

#### Scenario: 嵌套菜单
- **WHEN** 菜单项包含子菜单
- **THEN** 子菜单项的 `parent` 字段指向父菜单名称

### Requirement: Prompt 模板
系统 SHALL 为视觉分析提供优化的 Prompt 模板。

#### Scenario: 系统提示词
- **WHEN** 获取系统 Prompt
- **THEN** 包含以下内容：
  - 分析任务说明
  - 元素类型分类定义
  - 预期行为预测规则
  - 输出 JSON 格式规范
  - 坐标归一化说明
  - 父子关系标记规则

#### Scenario: 用户提示词
- **WHEN** 获取用户 Prompt
- **THEN** 包含推理级别占位符 `{{REASONING_LEVEL}}`

### Requirement: 响应 Schema
系统 SHALL 定义视觉分析的 JSON Schema。

#### Scenario: Schema 验证
- **WHEN** AI 返回响应
- **THEN** 响应符合以下 Schema：
  - `level1_dir`: 方向枚举（必需）
  - `level1_menus`: 菜单数组（必需）
  - `level2_dir`: 方向枚举（必需）
  - `level2_menus`: 菜单数组（必需）
  - `current_path`: 字符串数组（必需）
  - `items`: 元素数组（必需）
  - `is_popup`: 布尔值（必需）
  - `popup_info`: 对象或 null（必需）
  - `close_button`: 对象或 null（必需）
  - `back_button`: 对象或 null（必需）
  - `has_scroll`: 布尔值（必需）
  - `is_end_of_list`: 布尔值（必需）

### Requirement: Vision Service 集成
系统 SHALL 通过 Vision Service 执行视觉分析。

#### Scenario: 使用 Vision Service
- **WHEN** 执行视觉分析
- **THEN** 调用注入的 Vision Service
- **AND** 传递图片数据和 Prompt
- **AND** 解析返回的 JSON 响应

### Requirement: 无文字图标命名
系统 SHALL 为无文字的图标提供描述性名称。

#### Scenario: 图标命名
- **WHEN** 识别无文字的图标
- **THEN** 使用 "[icon] 描述" 格式命名
- **AND** 描述尽可能说明图标功能

### Requirement: 解析器注册
系统 SHALL 注册 `PageAnalysis` 的解析器。

#### Scenario: 解析器函数
- **WHEN** 注册解析器
- **THEN** 解析器将 JSON 响应转换为 `PageAnalysis` 数据对象
- **AND** 验证坐标在 0.0-1.0 范围内
