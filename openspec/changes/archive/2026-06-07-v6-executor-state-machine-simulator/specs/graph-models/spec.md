## ADDED Requirements

### Requirement: ExitConditionType 枚举

系统 SHALL 提供 ExitConditionType 枚举定义容器节点退出触发条件。

ExitConditionType SHALL 包含以下值：
- `ALL_CHILDREN_VISITED`: 等待所有子节点处理完成
- `DEPTH_LIMITED`: 达到最大深度时退出
- `SINGLE_LEVEL`: 仅处理直接子节点，不递归

#### Scenario: 所有子节点访问后退出
- **WHEN** ExitConditionType 为 ALL_CHILDREN_VISITED
- **THEN** 系统 SHALL 在所有子节点处理完成后触发退出

#### Scenario: 达到深度限制时退出
- **WHEN** ExitConditionType 为 DEPTH_LIMITED 且达到 max_depth
- **THEN** 系统 SHALL 触发退出

#### Scenario: 单层遍历
- **WHEN** ExitConditionType 为 SINGLE_LEVEL
- **THEN** 系统 SHALL 仅处理直接子节点，不递归生成孙节点

### Requirement: FallbackAction 枚举

系统 SHALL 提供 FallbackAction 枚举定义退出容器时执行的动作。

FallbackAction SHALL 包含以下值：
- `BACK`: 按 Back 键，弹出当前帧
- `AUTO_ESCAPE`: 尝试点击同级菜单，无同级则 Back
- `SKIP`: 跳过，不执行 Back，直接弹栈
- `ABORT`: 终止整个遍历

#### Scenario: 执行 Back 动作
- **WHEN** FallbackAction 为 BACK
- **THEN** 系统 SHALL 执行 Back 键并弹出当前帧

#### Scenario: 自动逃避
- **WHEN** FallbackAction 为 AUTO_ESCAPE 且存在未访问同级菜单
- **THEN** 系统 SHALL 点击同级菜单，不弹栈

#### Scenario: 自动逃避回退
- **WHEN** FallbackAction 为 AUTO_ESCAPE 且无未访问同级菜单
- **THEN** 系统 SHALL 执行 Back 键并弹出当前帧

#### Scenario: 跳过动作
- **WHEN** FallbackAction 为 SKIP
- **THEN** 系统 SHALL 直接弹出帧，不执行任何操作

#### Scenario: 终止遍历
- **WHEN** FallbackAction 为 ABORT
- **THEN** 系统 SHALL 终止整个遍历

### Requirement: ExitCondition 数据类

系统 SHALL 提供 ExitCondition 数据类定义容器节点退出条件。

ExitCondition SHALL 包含以下字段：
- `type`: 退出条件类型（ExitConditionType，必需）
- `fallback`: 回退动作（FallbackAction，可选，默认 BACK）
- `max_depth`: 深度限制（整数，可选）

#### Scenario: 创建基本退出条件
- **WHEN** 创建 ExitCondition 实例
- **THEN** 系统 SHALL 接受有效的 type 和可选参数

#### Scenario: 深度限制退出
- **WHEN** type 为 DEPTH_LIMITED 且指定 max_depth
- **THEN** 系统 SHALL 在达到该深度时触发退出

### Requirement: CompletionPolicyType 枚举

系统 SHALL 提供 CompletionPolicyType 枚举定义全局遍历终止条件。

CompletionPolicyType SHALL 包含以下值：
- `NONE`: 运行到自然完成
- `TARGET_FOUND`: 找到目标后终止
- `TIMEOUT`: 超时后终止
- `MAX_STEPS`: 达到最大步数后终止

#### Scenario: 自然完成
- **WHEN** CompletionPolicyType 为 NONE
- **THEN** 系统 SHALL 运行到栈为空

#### Scenario: 目标触发终止
- **WHEN** CompletionPolicyType 为 TARGET_FOUND
- **THEN** 系统 SHALL 在找到目标后终止

### Requirement: TargetFoundAction 枚举

系统 SHALL 提供 TargetFoundAction 枚举定义找到目标后的行为。

TargetFoundAction SHALL 包含以下值：
- `MARK_AND_STOP`: 标记目标，立即终止
- `EXECUTE_THEN_STOP`: 执行操作后终止

#### Scenario: 标记并停止
- **WHEN** TargetFoundAction 为 MARK_AND_STOP
- **THEN** 系统 SHALL 标记目标节点并立即终止遍历

#### Scenario: 执行后停止
- **WHEN** TargetFoundAction 为 EXECUTE_THEN_STOP
- **THEN** 系统 SHALL 执行节点操作后再终止遍历

### Requirement: MatchMode 枚举

系统 SHALL 提供 MatchMode 枚举定义目标文本匹配模式。

MatchMode SHALL 包含以下值：
- `EXACT`: 精确匹配
- `CONTAINS`: 包含匹配

#### Scenario: 精确匹配
- **WHEN** MatchMode 为 EXACT
- **THEN** 系统 SHALL 仅在文本完全相等时匹配

#### Scenario: 包含匹配
- **WHEN** MatchMode 为 CONTAINS
- **THEN** 系统 SHALL 在目标文本包含查询字符串时匹配

### Requirement: EntryStrategy 枚举

系统 SHALL 提供 EntryStrategy 枚举定义进入应用的方式。

EntryStrategy SHALL 包含以下值：
- `COLD_LAUNCH`: 从桌面找到并点击应用图标
- `DIRECT_DEEPLINK`: 使用 adb/am start 通过 Intent 启动
- `BIND_CURRENT_SCREEN`: 假设已在目标屏幕

#### Scenario: 冷启动
- **WHEN** EntryStrategy 为 COLD_LAUNCH
- **THEN** 系统 SHALL 返回桌面并点击应用图标

#### Scenario: 深度链接
- **WHEN** EntryStrategy 为 DIRECT_DEEPLINK
- **THEN** 系统 SHALL 通过 Intent 启动应用

#### Scenario: 绑定当前屏幕
- **WHEN** EntryStrategy 为 BIND_CURRENT_SCREEN
- **THEN** 系统 SHALL 验证当前屏幕是否为目标屏幕

### Requirement: TraversalMode 枚举

系统 SHALL 提供 TraversalMode 枚举定义遍历执行模式。

TraversalMode SHALL 包含以下值：
- `HYBRID`: 混合模式（静态 + 动态）
- `CONCRETE`: 具体模式（仅预定义静态路径）
- `ABSTRACT`: 抽象模式（完全动态生成）

#### Scenario: 混合模式
- **WHEN** TraversalMode 为 HYBRID
- **THEN** 系统 SHALL 支持静态节点和动态匹配

#### Scenario: 具体模式
- **WHEN** TraversalMode 为 CONCRETE
- **THEN** 系统 SHALL 仅使用预定义的静态路径

#### Scenario: 抽象模式
- **WHEN** TraversalMode 为 ABSTRACT
- **THEN** 系统 SHALL 完全动态生成遍历路径

### Requirement: TraversalNode 扩展

系统 SHALL 扩展 TraversalNode 类以支持 exit_condition 字段。

#### Scenario: 添加退出条件
- **WHEN** 为 TraversalNode 设置 exit_condition
- **THEN** 系统 SHALL 存储该条件并在容器完成时应用

#### Scenario: 向后兼容
- **WHEN** TraversalNode 不设置 exit_condition
- **THEN** 系统 SHALL 使用默认行为（BACK）

### Requirement: ErrorPolicy 扩展

系统 SHALL 扩展 ErrorPolicy 以支持 BACKTRACK 动作。

#### Scenario: 回溯动作
- **WHEN** ErrorPolicy.on_error 为 "backtrack"
- **THEN** 系统 SHALL 执行回溯操作（弹出当前帧）
