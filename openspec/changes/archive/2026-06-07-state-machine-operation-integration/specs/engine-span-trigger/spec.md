## ADDED Requirements

### Requirement: 引擎在 vision 调用前后生成 ai_call span
引擎的 `_step_once()` 方法 SHALL 在调用 `vision.analyze_screenshot()` 前后记录 `ai_call` span。

#### Scenario: AI 调用成功
- **WHEN** 状态机 handler 调用 `vision.analyze_screenshot()`
- **AND** 调用返回 `PageAnalysis` 且无异常
- **THEN** 引擎生成 `SpanNode(span_type="ai_call", capability="vision", success=True)`
- **AND** span 包含 `latency_ms` 和 `provider_id` 字段

#### Scenario: AI 调用失败
- **WHEN** 状态机 handler 调用 `vision.analyze_screenshot()`
- **AND** 调用抛出异常
- **THEN** 引擎生成 `SpanNode(span_type="ai_call", capability="vision", success=False)`

### Requirement: 引擎在 action 调用前后生成 execution span
引擎的 `_step_once()` 方法 SHALL 在调用 `action.execute()` 前后记录 `execution` span。

#### Scenario: 动作执行成功
- **WHEN** 状态机 handler 调用 `action.execute(context)`
- **AND** 返回 `ExecutionResult(success=True)`
- **THEN** 引擎生成 `SpanNode(span_type="execution", action="<action_name>", status="success")`
- **AND** span 包含 `duration_ms` 和 `target` 字段

#### Scenario: 动作执行失败
- **WHEN** 状态机 handler 调用 `action.execute(context)`
- **AND** 返回 `ExecutionResult(success=False)`
- **THEN** 引擎生成 `SpanNode(span_type="execution", action="<action_name>", status="failed")`

### Requirement: span 不阻塞主流程
引擎 span 生成 SHALL 遵循 "log and continue" 原则——span 写入失败不中断状态机执行。

#### Scenario: span 写入失败
- **WHEN** `TraceRecorder.record_span()` 内部写入存储时抛出异常
- **THEN** 异常被记录到日志
- **AND** 状态机继续执行下一个状态
