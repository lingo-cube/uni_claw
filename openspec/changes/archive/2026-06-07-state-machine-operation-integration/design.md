## Context

V6.4 打通了调用链，但状态机 handler 仍是占位符。`_handle_execute` 返回 `{"success": True}` 硬编码，`_handle_precondition_check` / `_handle_result_verify` 不调用 vision。引擎的 `_record_ai_call_span()` / `_record_execution_span()` 已就绪但无触发点。

**约束**：
- V6.4 完成后方可实施
- 状态机 handler 签名（`stack`, `context`, `vision`, `action`）保持兼容
- 引擎 span 生成方法是同步的，不阻塞主流程

## Goals / Non-Goals

**Goals:**
- 状态机 handler 真正调用注入的 `action` 和 `vision` 服务
- 引擎在 vision/action 调用前后自动生成对应 span
- 仿真 trace 中出现完整的 `ai_call` 和 `execution` span
- 错误处理路径可追踪（`error` span 包含 handler 异常）

**Non-Goals:**
- 不修改 `VisionService` ABC 或 `OperationExecutor` ABC
- 不引入异步 AI 调用（保持同步）
- 不实现复杂的错误恢复策略（仅基础重试/跳过）

## Decisions

### 1. 引擎包裹 service 调用以生成 span

**决策**：引擎的 `_step_once()` 不直接修改。改为在 handler 内部调用 service 时，引擎通过回调或上下文传递 span 生成器。

**实际方案**：由于 handler 由引擎的 `_step_once()` 通过 `state_machine.step()` 间接调用，引擎无法直接包裹 handler 内部的 service 调用。改为：handler 调用 service 后返回 metrics 数据，引擎根据返回的 metrics 生成 span。

```python
# handler 返回操作记录
def _handle_execute(self, stack, context, vision, action):
    t0 = time.time()
    try:
        result = action.execute(context)
        return TraversalState.RESULT_VERIFY, {
            "execution": {"action": "click", "status": "success", "duration_ms": (time.time()-t0)*1000}
        }
    except Exception as e:
        return TraversalState.ERROR_HANDLING, {
            "error": {"type": type(e).__name__, "message": str(e)}
        }
```

引擎的 `_step_once()` 检查返回的 metrics 并调用对应的 `_record_*_span()`。

**理由**：
- 引擎保持对 span 格式和写入的完全控制
- handler 只返回结构化数据，不依赖 trace 系统
- 与设计原则 #7 "Components collect raw metrics; Engine assembles Span nodes" 一致

### 2. PageAnalysis 缓存避免重复截图分析

**决策**：`_handle_result_verify` 调用 `vision.analyze_screenshot()` 获取操作后页面，与 `context.current_page_analysis`（操作前）对比。不引入额外的缓存机制。

**理由**：
- 每次 step 最多 2 次 vision 调用（precondition + result_verify），性能可接受
- 仿真中 vision 调用瞬时完成（查表），无真实延迟

## Risks / Trade-offs

### 风险：handler 返回值格式变更影响现有代码
- **缓解**：handler 当前返回单值 `TraversalState`，改为返回 `(TraversalState, dict)`；`_step_once()` 同步更新

### 风险：Mock 服务查表返回的 PageAnalysis 与真实行为不一致
- **缓解**：这是仿真与真实的固有差异；V6.5 专注代码路径一致性，行为精度留给后续迭代

## Migration Plan

1. 更新 `_step_once()` 接收 handler 返回的 metrics 并生成 span
2. 更新 4 个 handler 实现（execute / precondition / result_verify / error）
3. 更新仿真测试验证 trace 中包含 ai_call 和 execution span
4. 运行全量测试确认无回归

## Open Questions

无。
