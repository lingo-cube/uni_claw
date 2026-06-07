## Why

V6.4 打通了 `SimulationRunner → GraphTraversalEngine → TraversalStateMachine → TraceRecorder` 调用链，但状态机 6 个 handler 的内部实现仍是占位符（`result = {"success": True}`）。引擎的 AI call span 和 execution span 生成方法已就绪，但从未被触发。仿真 trace 中缺少 `ai_call` 和 `execution` span，无法验证完整的操作链路。

## What Changes

**让状态机 handler 真正调用注入的服务**：

- `_handle_execute` 调用 `action.execute(context)` 执行实际操作
- `_handle_precondition_check` 调用 `vision.analyze_screenshot()` 分析当前页面
- `_handle_result_verify` 调用 `vision.analyze_screenshot()` 验证操作结果
- `_handle_error_state` 触发引擎的 `_record_error_span()`
- 引擎在调用 vision/action 前后记录 `ai_call` 和 `execution` span
- 仿真 trace 中出现完整的 span 类型（state_transition + ai_call + execution + error）

## Capabilities

### New Capabilities

- `state-machine-operation-execution`: 状态机 handler 调用注入的 action/vision 服务，替换占位符实现
- `engine-span-trigger`: 引擎在 vision/action 调用前后自动生成 ai_call 和 execution span

### Modified Capabilities

<!-- V6.5 不修改现有 spec，handler 从占位符到真实调用属于新增行为 -->

## Impact

**Affected Code**:
- `src/state_machine/traversal_fsm.py` — `_handle_execute` / `_handle_precondition_check` / `_handle_result_verify` / `_handle_error_state` 实现
- `src/traversal/graph_engine.py` — `_step_once()` 中包裹 vision/action 调用以生成 span
- `src/trace/context.py` — `TraversalRuntimeContext` 可能需要新增 `last_page_analysis` 等字段

**New Dependencies**: 依赖 V6.4 simulation-interface-alignment 完成

**API Changes**: `_handle_execute` 签名中的 `action` 参数类型从 `"ActionExecutor"` 放宽为 `OperationExecutor`
