## ADDED Requirements

### Requirement: MockVisionService 定义

系统 SHALL 提供 MockVisionService 类用于虚拟视觉分析。

#### Scenario: 创建 Mock 服务
- **WHEN** 创建 MockVisionService 实例
- **THEN** 系统 SHALL 接受以下参数：
  - virtual_pages: Dict[str, PageAnalysis]（必需）

#### Scenario: 虚拟页面存储
- **WHEN** 初始化 MockVisionService
- **THEN** 系统 SHALL 存储 virtual_pages 映射

### Requirement: Mock 视觉分析

系统 SHALL 提供 analyze_screenshot() 方法返回虚拟页面分析。

#### Scenario: 匹配路径
- **WHEN** 当前路径对应虚拟页面存在
- **THEN** 系统 SHALL 返回对应的 PageAnalysis

#### Scenario: 未匹配路径
- **WHEN** 当前路径无对应虚拟页面
- **THEN** 系统 SHALL 返回空的 PageAnalysis

#### Scenario: 空路径
- **WHEN** 当前路径为空
- **THEN** 系统 SHALL 返回空的 PageAnalysis

### Requirement: 调用计数

系统 SHALL 记录 analyze_screenshot() 的调用次数。

#### Scenario: 增加计数
- **WHEN** 每次调用 analyze_screenshot()
- **THEN** 系统 SHALL 增加 call_count

#### Scenario: 获取计数
- **WHEN** 访问 call_count 属性
- **THEN** 系统 SHALL 返回总调用次数

### Requirement: 当前路径获取

系统 SHALL 能够获取当前遍历路径。

#### Scenario: 从上下文获取
- **WHEN** TraversalContext 可用
- **THEN** 系统 SHALL 从 context.current_path 获取路径

#### Scenario: 从注入获取
- **WHEN** TraversalContext 不可用但路径已注入
- **THEN** 系统 SHALL 使用注入的路径

#### Scenario: 无路径信息
- **WHEN** 既无上下文也无注入路径
- **THEN** 系统 SHALL 使用空路径

### Requirement: MockActionExecutor 定义

系统 SHALL 提供 MockActionExecutor 类用于虚拟操作执行。

#### Scenario: 创建 Mock 执行器
- **WHEN** 创建 MockActionExecutor 实例
- **THEN** 系统 SHALL 初始化空的操作历史列表

### Requirement: Mock 点击操作

系统 SHALL 提供 tap() 方法记录点击操作。

#### Scenario: 记录点击
- **WHEN** 调用 tap(x, y)
- **THEN** 系统 SHALL 记录包含以下信息的操作：
  - action: "tap"
  - x: x 坐标
  - y: y 坐标
  - timestamp: 时间戳

#### Scenario: 返回成功
- **WHEN** 调用 tap()
- **THEN** 系统 SHALL 返回 True

### Requirement: Mock 滑动操作

系统 SHALL 提供 swipe() 方法记录滑动操作。

#### Scenario: 记录滑动
- **WHEN** 调用 swipe(start, end)
- **THEN** 系统 SHALL 记录包含以下信息的操作：
  - action: "swipe"
  - start: 起始坐标 (x, y)
  - end: 结束坐标 (x, y)
  - timestamp: 时间戳

#### Scenario: 返回成功
- **WHEN** 调用 swipe()
- **THEN** 系统 SHALL 返回 True

### Requirement: Mock 返回操作

系统 SHALL 提供 press_back() 方法记录返回操作。

#### Scenario: 记录返回
- **WHEN** 调用 press_back()
- **THEN** 系统 SHALL 记录包含以下信息的操作：
  - action: "back"
  - timestamp: 时间戳

#### Scenario: 返回成功
- **WHEN** 调用 press_back()
- **THEN** 系统 SHALL 返回 True

### Requirement: 操作历史

系统 SHALL 提供操作历史的访问接口。

#### Scenario: 获取历史
- **WHEN** 调用 get_history()
- **THEN** 系统 SHALL 返回操作历史的副本

#### Scenario: 历史不可变
- **WHEN** 修改 get_history() 返回的列表
- **THEN** 系统 SHALL 不影响内部历史记录

#### Scenario: 历史顺序
- **WHEN** 获取操作历史
- **THEN** 系统 SHALL 按执行时间顺序返回

### Requirement: Mock 组件与真实组件接口一致

系统 SHALL 确保 Mock 组件与真实组件接口一致。

#### Scenario: VisionService 接口
- **WHEN** 使用 MockVisionService 替换 VisionService
- **THEN** 所有方法调用 SHALL 正常工作

#### Scenario: ActionExecutor 接口
- **WHEN** 使用 MockActionExecutor 替换 ActionExecutor
- **THEN** 所有方法调用 SHALL 正常工作

### Requirement: Mock 组件可配置

系统 SHALL 支持配置 Mock 组件的行为。

#### Scenario: 配置返回值
- **WHEN** 配置 MockVisionService 返回特定页面
- **THEN** 系统 SHALL 在匹配路径时返回该页面

#### Scenario: 配置延迟
- **WHEN** 配置 MockActionExecutor 模拟延迟
- **THEN** 系统 SHALL 在执行操作前等待

### Requirement: Mock 组件验证

系统 SHALL 支持 Mock 组件行为的验证。

#### Scenario: 验证调用次数
- **WHEN** 测试需要验证 analyze_screenshot 调用次数
- **THEN** 系统 SHALL 提供 call_count

#### Scenario: 验证操作序列
- **WHEN** 测试需要验证操作执行顺序
- **THEN** 系统 SHALL 提供操作历史

#### Scenario: 验证特定操作
- **WHEN** 测试需要验证特定操作是否执行
- **THEN** 系统 SHALL 可在历史中查找该操作
