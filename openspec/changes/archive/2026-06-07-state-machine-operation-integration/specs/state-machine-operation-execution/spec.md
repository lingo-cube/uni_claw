## ADDED Requirements

### Requirement: _handle_execute 调用 action.execute
状态机的 `_handle_execute` handler SHALL 调用注入的 `action.execute(context)` 执行操作，替换当前的 `{"success": True}` 占位符。

#### Scenario: 执行操作成功
- **WHEN** 进入 EXECUTE 状态
- **AND** `action.execute(context)` 返回 `ExecutionResult(success=True)`
- **THEN** 调用 `set_execution_result(result)` 记录成功结果
- **AND** 转移到 RESULT_VERIFY 状态

#### Scenario: 执行操作失败
- **WHEN** 进入 EXECUTE 状态
- **AND** `action.execute(context)` 抛出异常
- **THEN** `context.last_error` 被设置为异常对象
- **AND** 转移到 ERROR_HANDLING 状态

### Requirement: _handle_precondition_check 调用 vision.analyze_screenshot
状态机的 `_handle_precondition_check` handler SHALL 调用 `vision.analyze_screenshot(image_data)` 分析当前页面。

#### Scenario: 前置条件检查
- **WHEN** 进入 PRECONDITION_CHECK 状态
- **THEN** 调用 `vision.analyze_screenshot(image_data)` 获取 `PageAnalysis`
- **AND** 将结果存入 `context.current_page_analysis`

### Requirement: _handle_result_verify 调用 vision.analyze_screenshot
状态机的 `_handle_result_verify` handler SHALL 调用 `vision.analyze_screenshot(image_data)` 验证操作后页面状态。

#### Scenario: 结果验证
- **WHEN** 进入 RESULT_VERIFY 状态
- **THEN** 调用 `vision.analyze_screenshot(image_data)` 获取最新 `PageAnalysis`
- **AND** 与操作前的 `PageAnalysis` 对比判断页面是否变化
- **AND** 根据变化决定转移到 FRAME_COMPLETE 或 ERROR_HANDLING

### Requirement: _handle_error_state 处理错误
状态机的 `_handle_error_state` handler SHALL 根据错误类型执行恢复策略（重试、回退、跳过）。

#### Scenario: 可重试错误
- **WHEN** 错误类型为可重试（如临时超时）
- **AND** `retry_count < max_retries`
- **THEN** 增加 `retry_count` 并转移到 EXECUTE 重试

#### Scenario: 不可恢复错误
- **WHEN** 错误不可恢复或超过最大重试次数
- **THEN** 记录到 `context.failed_nodes`
- **AND** 转移到 FRAME_COMPLETE 继续下一个节点
