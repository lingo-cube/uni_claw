## ADDED Requirements

### Requirement: 全局状态机
系统 SHALL 提供全局状态机管理遍历任务的生命周期。

#### Scenario: 全局状态转换
- **WHEN** 遍历任务运行
- **THEN** 全局状态按以下流程转换：
  - `IDLE` → `INITIALIZING` → `TRAVERSING` → `COMPLETED`
  - `INITIALIZING` → `ERROR` → `RECOVERING` → `TRAVERSING`
  - `INITIALIZING` → `ERROR` → `TERMINATED`
  - `TRAVERSING` → `PAUSED` → `TRAVERSING`

#### Scenario: IDLE 状态
- **WHEN** 全局状态为 `IDLE`
- **THEN** 系统等待任务启动
- **AND** 可以接收初始化指令

#### Scenario: INITIALIZING 状态
- **WHEN** 全局状态转换到 `INITIALIZING`
- **THEN** 系统执行以下操作：
  - 加载 `TraversalPlan`（静态图或根节点+模板）
  - 初始化 `TraversalContext`
  - 定位入口页面
  - 将根节点压入节点栈

#### Scenario: TRAVERSING 状态
- **WHEN** 全局状态转换到 `TRAVERSING`
- **THEN** 遍历状态机激活
- **AND** 系统持续处理节点直到节点栈为空或发生异常

#### Scenario: PAUSED 状态
- **WHEN** 外部触发暂停指令
- **THEN** 全局状态转换到 `PAUSED`
- **AND** 系统保存当前状态
- **AND** 可以接收恢复指令回到 `TRAVERSING`

#### Scenario: ERROR 状态
- **WHEN** 遍历过程中发生严重异常
- **THEN** 全局状态转换到 `ERROR`
- **AND** 系统记录异常上下文
- **AND** 根据异常可恢复性决定进入 `RECOVERING` 或 `TERMINATED`

#### Scenario: RECOVERING 状态
- **WHEN** 全局状态转换到 `RECOVERING`
- **THEN** 系统尝试执行恢复流程（如重启 APP）
- **AND** 恢复成功后回到 `INITIALIZING`

#### Scenario: COMPLETED 状态
- **WHEN** 节点栈为空或遍历达到终止条件
- **THEN** 全局状态转换到 `COMPLETED`
- **AND** 系统生成遍历摘要

#### Scenario: TERMINATED 状态
- **WHEN** 异常无法恢复或达到最大重试次数
- **THEN** 全局状态转换到 `TERMINATED`
- **AND** 系统终止遍历

### Requirement: 遍历状态机
系统 SHALL 提供遍历状态机处理单个节点的执行流程。

#### Scenario: 遍历状态转换
- **WHEN** 处理单个节点
- **THEN** 遍历状态按以下流程转换：
  - `NODE_SELECT` → `PRECONDITION_CHECK` → `EXECUTE` → `RESULT_VERIFY` → `BRANCH`
  - `BRANCH` → `NODE_SELECT`（循环）

#### Scenario: NODE_SELECT 状态
- **WHEN** 遍历状态为 `NODE_SELECT`
- **THEN** 系统从节点栈顶部获取当前帧
- **AND** 从帧的 `child_queue` 中取出下一个子节点
- **AND** 如果没有子节点，标记帧为完成

#### Scenario: PRECONDITION_CHECK 状态
- **WHEN** 遍历状态为 `PRECONDITION_CHECK`
- **THEN** 系统检查当前屏幕是否满足节点的 `precondition`
- **AND** 条件满足则转换到 `EXECUTE`
- **AND** 条件不满足则执行自动导航（如连续返回）直到满足或超时

#### Scenario: EXECUTE 状态
- **WHEN** 遍历状态为 `EXECUTE`
- **THEN** 系统执行节点的 `operation`
- **AND** 等待操作完成并截图
- **AND** 捕获执行过程中的异常

#### Scenario: RESULT_VERIFY 状态
- **WHEN** 遍历状态为 `RESULT_VERIFY`
- **THEN** 系统分析执行后的屏幕
- **AND** 判断操作是否成功（页面变化、弹窗、无响应等）
- **AND** 记录执行结果

#### Scenario: BRANCH 状态 - 容器节点
- **WHEN** 节点类型为 `container` 且执行成功
- **THEN** 根据 `children_strategy` 生成子节点
- **AND** 将子节点逆序压入节点栈
- **AND** 转换到 `NODE_SELECT`

#### Scenario: BRANCH 状态 - 叶子节点恢复
- **WHEN** 节点类型为叶子节点且需要恢复
- **THEN** 执行节点的 `restore` 操作
- **AND** 转换到 `NODE_SELECT`

#### Scenario: BRANCH 状态 - 返回父节点
- **WHEN** 当前节点的所有子节点处理完毕
- **THEN** 执行 `back` 操作返回父节点
- **AND** 从节点栈弹出当前节点
- **AND** 转换到 `NODE_SELECT`

#### Scenario: BRANCH 状态 - 异常处理
- **WHEN** 操作执行失败
- **THEN** 根据 `error_policy` 或全局异常链决定：
  - 重试：转换到 `PRECONDITION_CHECK`
  - 跳过：转换到 `NODE_SELECT`
  - 回退：从节点栈弹出节点，转换到 `NODE_SELECT`

### Requirement: 节点栈
系统 SHALL 提供节点栈维护深度优先遍历的上下文。

#### Scenario: StackFrame 结构
- **WHEN** 创建 `StackFrame` 实例
- **THEN** 该实例包含以下字段：
  - `node: TraversalNode` - 当前正在处理的节点
  - `child_queue: List[str]` - 待处理的子节点 ID 列表
  - `current_child_idx: int` - 当前处理到的子节点索引
  - `pending_restore: bool` - 是否需要执行恢复操作

#### Scenario: push 操作
- **WHEN** 进入新节点
- **THEN** 将新 `StackFrame` 压入节点栈
- **AND** `child_queue` 初始化为子节点 ID 列表（逆序）

#### Scenario: top 操作
- **WHEN** 获取当前帧
- **THEN** 返回节点栈顶部的 `StackFrame`
- **AND** 不弹出栈

#### Scenario: pop 操作
- **WHEN** 当前节点及所有子节点处理完毕
- **THEN** 检查 `pending_restore`
- **AND** 如果需要恢复，执行 `restore` 操作
- **AND** 弹出节点栈顶部的 `StackFrame`

#### Scenario: 栈深度限制
- **WHEN** 节点栈深度超过配置的上限（默认 10）
- **THEN** 系统停止压栈
- **AND** 记录警告日志
- **AND** 标记当前分支为完成

### Requirement: 状态机与图模型交互
系统 SHALL 支持状态机与图模型的协同工作。

#### Scenario: 初始化阶段
- **WHEN** 全局状态转换到 `INITIALIZING`
- **THEN** 加载静态图或根节点+模板注册表
- **AND** 将根节点压入节点栈

#### Scenario: 遍历阶段
- **WHEN** 全局状态为 `TRAVERSING`
- **THEN** 遍历状态机持续运行
- **AND** 从节点栈顶部获取待处理节点

#### Scenario: 动态扩展
- **WHEN** 容器节点执行后
- **THEN** 调用图模型的动态匹配模块
- **AND** 传入当前屏幕 `PageAnalysis`
- **AND** 生成子节点列表
- **AND** 更新栈顶帧的 `child_queue`

#### Scenario: 异常回退
- **WHEN** 遍历状态机中的操作失败且需要回退
- **THEN** 通过弹出节点栈实现回退
- **AND** 回到父节点继续遍历

### Requirement: 配置开关
系统 SHALL 提供配置开关控制是否启用图模式。

#### Scenario: 禁用图模式
- **WHEN** 配置 `use_graph_mode = false`
- **THEN** 系统使用 V3.0 线性遍历逻辑
- **AND** 状态机和节点栈不激活

#### Scenario: 启用图模式
- **WHEN** 配置 `use_graph_mode = true`
- **THEN** 系统使用图模式遍历
- **AND** 状态机和节点栈激活
