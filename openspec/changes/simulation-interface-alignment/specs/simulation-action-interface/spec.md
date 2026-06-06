## ADDED Requirements

### Requirement: MockActionExecutor 实现 OperationExecutor 接口
系统 SHALL 提供继承 `OperationExecutor` ABC 的 `MockActionExecutor`，通过统一 `execute()` 入口与引擎对接。

#### Scenario: 类型检查通过
- **WHEN** 创建 `MockActionExecutor` 实例
- **THEN** `isinstance(executor, OperationExecutor)` 返回 `True`

#### Scenario: execute 签名匹配
- **WHEN** 调用 `mock.execute(context: ExecutionContext)`
- **THEN** 返回 `ExecutionResult` 实例
- **AND** `result.success` 为 `True`

#### Scenario: get_executed_actions 返回操作列表
- **WHEN** 已执行 3 个操作
- **THEN** `get_executed_actions()` 返回包含 3 个操作描述的列表

#### Scenario: clear_history 清空记录
- **WHEN** 调用 `clear_history()`
- **THEN** `get_executed_actions()` 返回空列表
- **AND** `history` 属性返回空列表

### Requirement: execute 记录操作历史
MockActionExecutor SHALL 在每次 `execute()` 调用时记录操作详情到内部历史。

#### Scenario: 记录操作详情
- **WHEN** 调用 `execute(ExecutionContext(node_id="n1", node_name="Settings", operation={"action": "click", "target": "btn_wifi"}))`
- **THEN** 内部历史包含一条记录，包含 `node_id="n1"`、`action="click"`、`target="btn_wifi"`

#### Scenario: simulate_delay 生效
- **WHEN** `MockActionExecutor(simulate_delay=0.01)` 已创建
- **AND** 调用 `execute(context)`
- **THEN** 调用耗时至少为 `simulate_delay` 秒

### Requirement: 删除旧方法
系统 SHALL 删除 `MockActionExecutor` 中零散的 `tap/swipe/click/press_back/press_home/input_text/scroll/go_back` 方法。

#### Scenario: 旧方法不存在
- **WHEN** 查看 `MockActionExecutor` 类
- **THEN** 存在 `execute(ExecutionContext) -> ExecutionResult` 方法
- **AND** 存在 `get_executed_actions() -> list[str]` 方法
- **AND** 存在 `clear_history()` 方法
- **AND** 不存在 `tap`、`swipe`、`click`、`press_back`、`press_home`、`input_text`、`scroll`、`go_back` 方法
