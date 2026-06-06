## Why

当前仿真系统 (`SimulationRunner`) 存在死导入（`src.graph.graph_traversal_engine` 不存在），永远走手写 DFS 兜底逻辑，绕过了真实的 `GraphTraversalEngine`、`TraversalStateMachine` 和 V6.3 Trace 系统。Mock 服务未实现真实接口，导致仿真代码路径与生产环境完全不同——仿真无法验证任何核心逻辑。

## What Changes

**修复仿真调用链**，让 `SimulationRunner` 使用真实核心组件：

- 修复 `SimulationRunner` 导入路径，指向 `src.traversal.graph_engine.GraphTraversalEngine`
- `MockVisionService` 继承 `VisionService` ABC，实现 `analyze_screenshot()` 和 `find_app_entry()`
- `MockActionExecutor` 继承 `OperationExecutor` ABC，实现 `execute()` / `get_executed_actions()` / `clear_history()`
- 用 V6.3 `TraceRecorder` + `MemoryStorage` 替换旧版 `InMemoryTracer`
- 删除手写 DFS fallback（`_execute_fallback_simulation` 等 ~250 行）
- `SimulationResult` 数据源改为 `TraceAnalyzer` 提取
- 删除 `SimulationRunner` 上的冗余状态字段（`current_path`、`visited_pages` 等）

## Capabilities

### New Capabilities

- `simulation-vision-interface`: MockVisionService 实现 VisionService ABC，仿真视觉服务通过接口契约与真实引擎对接
- `simulation-action-interface`: MockActionExecutor 实现 OperationExecutor ABC，仿真动作执行通过统一接口与引擎对接
- `simulation-trace-integration`: SimulationRunner 集成 V6.3 TraceRecorder + MemoryStorage，仿真结果通过 TraceAnalyzer 提取

### Modified Capabilities

<!-- V6.4 不修改现有 spec，仅新增 -->

## Impact

**Affected Code**:
- `src/simulation/runner.py` — 重写：修复导入、集成 Trace、删除 fallback
- `src/simulation/mock_vision.py` — 重写：继承 VisionService ABC
- `src/simulation/mock_action.py` — 重写：继承 OperationExecutor ABC
- `src/simulation/test/` — 更新断言为 TraceAnalyzer 方式
- `tests/v6/test_simulation.py` — 对齐新接口
- `tests/v6/test_executor.py` — 接口兼容微调

**New Dependencies**: 无（依赖 V6.3 trace-integration 已完成的模块）

**API Changes**:
- `MockVisionService.analyze_screenshot(image_data: bytes) -> PageAnalysis`（签名变更）
- `MockActionExecutor` 删除 `tap/swipe/click` 等方法，统一为 `execute(context: ExecutionContext) -> ExecutionResult`

**Storage Changes**: 无

**Removed**: `_execute_fallback_simulation()`, `_interact_with_next_element()`, `_execute_element_action()`, `_go_back()`, `InMemoryTracer` 引用, `SimulationRunner.current_path/visited_pages/visited_elements` 等字段
