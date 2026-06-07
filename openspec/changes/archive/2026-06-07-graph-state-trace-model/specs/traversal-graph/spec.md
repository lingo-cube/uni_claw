## ADDED Requirements

### Requirement: TraversalNode 统一节点抽象
系统 SHALL 提供 `TraversalNode` 数据类作为遍历操作的统一抽象。

#### Scenario: 节点包含必要字段
- **WHEN** 创建 `TraversalNode` 实例
- **THEN** 该实例包含以下字段：
  - `node_id: str` - 唯一标识
  - `name: str` - 展示名称
  - `node_type: NodeType` - 节点类型（container/leaf_switch/leaf_slider/leaf_action/leaf_info）
  - `operation: Operation` - 执行的动作
  - `precondition: Optional[Precondition]` - 前置条件
  - `children_strategy: ChildrenStrategy` - 子节点生成策略
  - `error_policy: Optional[ErrorPolicy]` - 异常处理策略
  - `meta: Dict[str, Any]` - 运行时状态

### Requirement: Operation 操作定义
系统 SHALL 提供 `Operation` 数据类定义节点执行的操作。

#### Scenario: 操作包含必要字段
- **WHEN** 创建 `Operation` 实例
- **THEN** 该实例包含以下字段：
  - `action: str` - 动作类型（click/swipe/back/input_text/no_action）
  - `target: Optional[Target]` - 目标元素定位
  - `params: Dict[str, Any]` - 动作参数
  - `restore: Optional[RestoreAction]` - 恢复操作

### Requirement: Target 目标定位
系统 SHALL 提供 `Target` 数据类定义目标元素的定位方式。

#### Scenario: 支持多种定位方式
- **WHEN** 创建 `Target` 实例
- **THEN** 支持以下定位方式：
  - `by: "text"` - 通过文本内容定位
  - `by: "coordinate"` - 通过归一化坐标定位
  - `by: "ui_index"` - 通过 UI 列表索引定位

### Requirement: Precondition 前置条件
系统 SHALL 提供 `Precondition` 数据类定义节点执行前必须满足的条件。

#### Scenario: 支持多种前置条件
- **WHEN** 定义节点前置条件
- **THEN** 支持以下条件类型：
  - `page_name: Optional[str]` - 要求当前页面名称
  - `path: Optional[List[str]]` - 要求完整路径
  - `ui_condition: Optional[str]` - UI 条件表达式

#### Scenario: 前置条件验证
- **WHEN** 节点执行前置条件验证
- **THEN** 系统检查当前 `current_path` 和屏幕 UI 是否满足条件
- **AND** 条件不满足时执行自动导航（如连续返回）直到满足或超时

### Requirement: ChildrenStrategy 子节点生成策略
系统 SHALL 提供 `ChildrenStrategy` 数据类定义子节点的生成方式。

#### Scenario: 静态子节点
- **WHEN** `children_strategy.type = STATIC`
- **THEN** 从 `static_children` 列表获取预定义的子节点 ID

#### Scenario: 动态匹配子节点
- **WHEN** `children_strategy.type = DYNAMIC_MATCH`
- **THEN** 根据当前屏幕 `MenuItem` 列表和 `dynamic_rules` 匹配生成子节点

#### Scenario: 叶子节点
- **WHEN** `children_strategy.type = NONE`
- **THEN** 节点为叶子节点，不生成子节点

### Requirement: DynamicRule 动态匹配规则
系统 SHALL 提供 `DynamicRule` 定义如何匹配 UI 元素到模板。

#### Scenario: 匹配条件
- **WHEN** 评估 `DynamicRule`
- **THEN** 根据 `match_condition` 检查 `MenuItem` 属性（type、expected_action 等）

#### Scenario: 匹配动作
- **WHEN** 匹配成功
- **THEN** 根据 `action` 字段决定：
  - `generate_child` - 生成子节点并添加到待处理队列
  - `skip` - 跳过该元素
  - `execute_inline` - 内联执行操作，不生成子节点

### Requirement: 静态图支持
系统 SHALL 支持预定义的静态图结构。

#### Scenario: 加载静态图
- **WHEN** 提供 `TraversalPlan` 包含静态节点树
- **THEN** 引擎按深度优先顺序执行预定义的节点

#### Scenario: 静态图节点执行
- **WHEN** 执行静态图中定义的节点
- **THEN** 按照节点的 `children_strategy` 处理子节点

### Requirement: 动态图支持
系统 SHALL 支持运行时动态生成图结构。

#### Scenario: 动态生成子节点
- **WHEN** 容器节点执行后且 `children_strategy.type = DYNAMIC_MATCH`
- **THEN** 获取当前屏幕 `PageAnalysis`
- **AND** 遍历 `items` 列表，对每个 `MenuItem` 应用 `dynamic_rules` 匹配
- **AND** 命中的规则实例化对应模板，生成子节点

#### Scenario: 动态图节点执行
- **WHEN** 动态生成的节点压入节点栈
- **THEN** 按深度优先顺序执行

### Requirement: 混合模式支持
系统 SHALL 支持静态图和动态图的混合模式。

#### Scenario: 主干固定枝叶探索
- **WHEN** 静态图仅定义主干节点（如设置根节点）
- **AND** 主干节点使用 `DYNAMIC_MATCH` 策略
- **THEN** 引擎执行静态节点后动态探索子菜单

### Requirement: 深度优先遍历
系统 SHALL 使用深度优先策略遍历图结构。

#### Scenario: 进入子节点
- **WHEN** 容器节点成功执行且生成子节点
- **THEN** 将子节点逆序压入节点栈（深度优先）

#### Scenario: 返回父节点
- **WHEN** 当前节点的所有子节点处理完毕
- **THEN** 从节点栈弹出当前节点
- **AND** 执行返回操作回到父节点
