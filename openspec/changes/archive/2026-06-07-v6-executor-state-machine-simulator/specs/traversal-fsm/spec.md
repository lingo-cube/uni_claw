## ADDED Requirements

### Requirement: TraversalState 扩展

系统 SHALL 在 TraversalState 枚举中添加新状态。

#### Scenario: 新增状态
- **WHEN** 定义 TraversalState 枚举
- **THEN** 系统 SHALL 包含以下状态：
  - 现有状态：NODE_SELECT, PRECONDITION_CHECK, EXECUTE, RESULT_VERIFY, BRANCH
  - V6 新增：FRAME_COMPLETE, ERROR_HANDLING, POPUP_HANDLING

### Requirement: 状态转移验证

系统 SHALL 扩展 VALID_TRANSITIONS 以包含新状态的转移。

#### Scenario: FRAME_COMPLETE 转移
- **WHEN** 从 BRANCH 转移到 FRAME_COMPLETE
- **THEN** 系统 SHALL 允许该转移

#### Scenario: ERROR_HANDLING 转移
- **WHEN** 从任何状态转移到 ERROR_HANDLING
- **THEN** 系统 SHALL 允许该转移

#### Scenario: POPUP_HANDLING 转移
- **WHEN** 从 RESULT_VERIFY 转移到 POPUP_HANDLING
- **THEN** 系统 SHALL 允许该转移

### Requirement: 状态处理方法

系统 SHALL 为每个新状态提供处理方法。

#### Scenario: handle_frame_complete()
- **WHEN** 处于 FRAME_COMPLETE 状态
- **THEN** 系统 SHALL 调用 handle_frame_complete()

#### Scenario: handle_error()
- **WHEN** 处于 ERROR_HANDLING 状态
- **THEN** 系统 SHALL 调用 handle_error()

#### Scenario: handle_popup()
- **WHEN** 处于 POPUP_HANDLING 状态
- **THEN** 系统 SHALL 调用 handle_popup()

### Requirement: 状态转移历史

系统 SHALL 记录状态转移历史。

#### Scenario: 记录转移
- **WHEN** 每次状态转移
- **THEN** 系统 SHALL 记录 StateTransition

#### Scenario: 转移历史访问
- **WHEN** 需要查看转移历史
- **THEN** 系统 SHALL 提供转移历史列表

### Requirement: 子状态支持

系统 SHALL 支持子状态概念。

#### Scenario: POPUP_HANDLING 作为子状态
- **WHEN** 在 POPUP_HANDLING 状态中
- **THEN** 系统 SHALL 可返回到 RESULT_VERIFY 状态

#### Scenario: 子状态转移
- **WHEN** 子状态处理完成
- **THEN** 系统 SHALL 转移回父状态

### Requirement: 状态机步进接口

系统 SHALL 提供 step() 方法执行单步状态转移。

#### Scenario: 步进接口
- **WHEN** 调用 state_machine.step()
- **THEN** 系统 SHALL：
  1. 读取当前状态
  2. 执行对应处理方法
  3. 返回 StateTransition

#### Scenario: 步进参数
- **WHEN** 调用 step()
- **THEN** 系统 SHALL 接受以下参数：
  - stack: NodeStack
  - context: TraversalContext
  - vision: VisionService
  - action: ActionExecutor

### Requirement: 状态转换结果

系统 SHALL 返回详细的状态转换信息。

#### Scenario: StateTransition 内容
- **WHEN** 返回 StateTransition
- **THEN** 系统 SHALL 包含：
  - from_state: 源状态
  - to_state: 目标状态
  - node_id: 相关节点 ID
  - metadata: 元数据字典

### Requirement: 非法转移拒绝

系统 SHALL 拒绝非法的状态转移。

#### Scenario: 非法转移检测
- **WHEN** 尝试非法转移
- **THEN** 系统 SHALL 抛出异常或记录错误

#### Scenario: 转移验证
- **WHEN** 执行状态转移前
- **THEN** 系统 SHALL 验证转移是否合法
