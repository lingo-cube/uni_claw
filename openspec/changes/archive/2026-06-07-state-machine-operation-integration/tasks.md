## 1. Engine: _step_once() 支持 handler metrics

- [x] 1.1 修改 `_step_once()` 接收 handler 返回的 `(TraversalState, dict)` 元组
- [x] 1.2 从返回 dict 中提取 `execution` metrics 并调用 `_record_execution_span()`
- [x] 1.3 从返回 dict 中提取 `ai_call` metrics 并调用 `_record_ai_call_span()`
- [x] 1.4 从返回 dict 中提取 `error` metrics 并调用 `_record_error_span()`
- [x] 1.5 保持向后兼容：handler 仅返回 `TraversalState` 时 metrics 为 `None`

## 2. _handle_precondition_check 实现

- [x] 2.1 构造 `image_data`（仿真传空 bytes，真实环境从设备获取截图）
- [x] 2.2 调用 `vision.analyze_screenshot(image_data)` 获取 `PageAnalysis`
- [x] 2.3 将 `PageAnalysis` 存入 `context.current_page_analysis`
- [x] 2.4 返回 `(TraversalState.EXECUTE, {"ai_call": {...}})` 包含 vision 调用 metrics

## 3. _handle_execute 实现

- [x] 3.1 从 `current_node.operation` 构造 `ExecutionContext`
- [x] 3.2 调用 `action.execute(context)` 执行操作
- [x] 3.3 记录执行耗时和结果
- [x] 3.4 成功返回 `(TraversalState.RESULT_VERIFY, {"execution": {...}})`
- [x] 3.5 失败返回 `(TraversalState.ERROR_HANDLING, {"error": {...}})`

## 4. _handle_result_verify 实现

- [x] 4.1 调用 `vision.analyze_screenshot(image_data)` 获取操作后 `PageAnalysis`
- [x] 4.2 对比操作前后的 `PageAnalysis`（通过 `current_path` 或页面指纹变化）
- [x] 4.3 页面变化 → 返回 `(TraversalState.FRAME_COMPLETE, {"ai_call": {...}})`
- [x] 4.4 页面无变化 → 返回 `(TraversalState.ERROR_HANDLING, {"error": {...}})`

## 5. _handle_error_state 实现

- [x] 5.1 根据错误类型和 `context.consecutive_errors` 决定恢复策略
- [x] 5.2 可重试：增加 `retry_count` → 返回 `(TraversalState.EXECUTE, {...})`
- [x] 5.3 不可恢复：记录到 `context.failed_nodes` → 返回 `(TraversalState.FRAME_COMPLETE, {...})`

## 6. 测试更新

- [x] 6.1 更新状态机测试验证 handler 返回 `(state, metrics)` 元组
- [x] 6.2 仿真测试验证 trace 中包含 `ai_call` span（类型检查 + 数量验证）
- [x] 6.3 仿真测试验证 trace 中包含 `execution` span（action/status 字段验证）
- [x] 6.4 MockVisionService 的 `analyze_screenshot` 调用后 `call_count` 递增
- [x] 6.5 MockActionExecutor 的 `execute` 调用后 `history` 包含操作记录

## 7. 验收验证

- [x] 7.1 仿真完整运行后 trace 包含 ≥ 1 个 `ai_call` span
- [x] 7.2 仿真完整运行后 trace 包含 ≥ 1 个 `execution` span
- [x] 7.3 TraceAnalyzer.extract_ai_calls() 返回非空列表
- [x] 7.4 TraceAnalyzer.extract_action_sequence() 返回非空列表
- [x] 7.5 错误场景下 TraceAnalyzer.extract_error_statistics() 包含对应错误
- [x] 7.6 现有已通过的 V6 测试无回归

---

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/state_machine/` | [state-machine-design.md](../../docs/architecture/modules/state-machine-design.md) |
| `src/traversal/` | [traversal-design.md](../../docs/architecture/modules/traversal-design.md) |
| `src/simulation/` | [simulation-design.md](../../docs/architecture/modules/simulation-design.md) |
| `src/trace/` | [trace-design.md](../../docs/architecture/modules/trace-design.md) |
