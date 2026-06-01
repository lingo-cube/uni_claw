## ADDED Requirements

### Requirement: 模板注册表加载
系统 SHALL 支持从 JSON 文件加载模板注册表。

#### Scenario: 加载默认路径
- **WHEN** 系统启动
- **THEN** 从默认路径（`config/templates.json`）加载模板注册表

#### Scenario: 加载自定义路径
- **WHEN** 配置 `template_registry_path`
- **THEN** 从指定路径加载模板注册表

#### Scenario: 加载失败处理
- **WHEN** 模板文件不存在或格式错误
- **THEN** 系统记录错误日志
- **AND** 使用内置默认模板
- **AND** 不影响遍历启动

### Requirement: 模板注册表结构
系统 SHALL 支持符合规范的模板注册表 JSON 格式。

#### Scenario: 注册表顶层结构
- **WHEN** 解析模板注册表
- **THEN** 顶层包含 `templates` 对象
- **AND** `templates` 包含多个模板定义

#### Scenario: 模板定义结构
- **WHEN** 解析单个模板
- **THEN** 模板包含以下字段：
  - `node_type` - 节点类型
  - `operation` - 操作定义
  - `precondition` - 前置条件（可选）
  - `children_strategy` - 子节点策略
  - `error_policy` - 异常策略（可选）

### Requirement: 模板占位符
系统 SHALL 支持模板中使用占位符实现运行时填充。

#### Scenario: 支持的占位符
- **WHEN** 定义模板
- **THEN** 支持以下占位符：
  - `{{item_text}}` - UI 元素的文本内容
  - `{{item_index}}` - UI 元素的索引
  - `{{coordinate_x}}` - X 坐标
  - `{{coordinate_y}}` - Y 坐标

#### Scenario: 运行时填充
- **WHEN** 实例化模板
- **THEN** 使用实际 UI 元素属性替换占位符
- **AND** 生成具体的 `TraversalNode`

### Requirement: 内置默认模板
系统 SHALL 提供内置默认模板，确保无注册表时也能运行。

#### Scenario: 默认菜单容器模板
- **WHEN** 系统使用内置默认模板
- **THEN** 包含 `menu_container` 模板
- **AND** 该模板定义菜单项的标准行为

#### Scenario: 默认开关叶子模板
- **WHEN** 系统使用内置默认模板
- **THEN** 包含 `switch_leaf` 模板
- **AND** 该模板定义开关控件的标准行为

#### Scenario: 默认滑块叶子模板
- **WHEN** 系统使用内置默认模板
- **THEN** 包含 `slider_leaf` 模板
- **AND** 该模板定义滑块控件的标准行为

### Requirement: 模板实例化
系统 SHALL 支持根据模板和 UI 元素实例化具体节点。

#### Scenario: 实例化流程
- **WHEN** 动态匹配命中规则
- **THEN** 系统执行以下流程：
  1. 根据 `child_template` 从注册表获取模板
  2. 使用 `MenuItem` 属性填充占位符
  3. 生成唯一 `node_id`
  4. 创建 `TraversalNode` 实例

#### Scenario: 唯一 ID 生成
- **WHEN** 生成实例化节点的 `node_id`
- **THEN** 使用模板 ID + UI 元素属性生成唯一标识
- **AND** 例如：`menu_container-设置-显示-0`

### Requirement: 模板验证
系统 SHALL 支持模板注册表的验证。

#### Scenario: 格式验证
- **WHEN** 加载模板注册表
- **THEN** 系统验证 JSON 格式正确性
- **AND** 验证必需字段存在

#### Scenario: 引用验证
- **WHEN** 模板引用其他模板（通过 `child_template`）
- **THEN** 系统验证被引用的模板存在
- **AND** 引用不存在则记录警告

#### Scenario: 占位符验证
- **WHEN** 模板包含占位符
- **THEN** 系统验证占位符格式正确
- **AND** 不支持的占位符记录警告

### Requirement: 模板热更新
系统 SHALL 支持模板注册表的热更新（可选）。

#### Scenario: 热更新配置
- **WHEN** 配置 `template_hot_reload = true`
- **THEN** 系统监听模板文件变化
- **AND** 文件变化时自动重新加载

#### Scenario: 热更新失败处理
- **WHEN** 热更新加载失败
- **THEN** 系统保持旧版本模板
- **AND** 记录错误日志

### Requirement: 模板版本管理
系统 SHALL 支持模板的版本管理（预留）。

#### Scenario: 版本字段
- **WHEN** 定义模板注册表
- **THEN** 可选包含 `version` 字段
- **AND** 记录模板版本号

#### Scenario: 版本兼容性
- **WHEN** 加载不同版本的模板
- **THEN** 系统验证版本兼容性
- **AND** 不兼容版本记录警告

### Requirement: 模板调试支持
系统 SHALL 支持模板调试功能。

#### Scenario: 模板匹配日志
- **WHEN** 启用调试模式
- **THEN** 记录每个 UI 元素的匹配过程
- **AND** 显示命中的规则和生成的节点

#### Scenario: 模板预览
- **WHEN** 调试模式
- **THEN** 可以预览模板实例化结果
- **AND** 不执行实际操作
