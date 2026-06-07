## 1. MockVisionService 接口对齐

- [x] 1.1 重写 `src/simulation/mock_vision.py`：类继承 `VisionService` ABC
- [x] 1.2 实现 `analyze_screenshot(image_data: bytes) -> PageAnalysis` 方法
- [x] 1.3 实现 `find_app_entry(image_data: bytes, target: str) -> Optional[dict]` 方法
- [x] 1.4 实现 `set_path_context(path: List[str])` 方法
- [x] 1.5 实现 `_build_page_analysis(data: Dict) -> PageAnalysis` 内部方法（查表 + PageAnalyzer 或直接构造）
- [x] 1.6 保留 `virtual_pages` 初始化参数和 `call_count` 属性

## 2. MockActionExecutor 接口对齐

- [x] 2.1 重写 `src/simulation/mock_action.py`：类继承 `OperationExecutor` ABC
- [x] 2.2 实现 `execute(context: ExecutionContext) -> ExecutionResult` 方法
- [x] 2.3 实现 `get_executed_actions() -> list[str]` 方法
- [x] 2.4 实现 `clear_history()` 方法
- [x] 2.5 删除 `tap/swipe/click/press_back/press_home/input_text/scroll/go_back` 旧方法
- [x] 2.6 保留 `simulate_delay` 和 `history` 属性

## 3. SimulationRunner Trace 集成 + 引擎打通

- [x] 3.1 修复导入：`from src.traversal.graph_engine import GraphTraversalEngine`
- [x] 3.2 添加导入：`TraceRecorder`、`MemoryStorage`、`TraceAnalyzer`
- [x] 3.3 删除 `self.tracer = InMemoryTracer()` 及 `InMemoryTracer` 导入
- [x] 3.4 创建 `self._storage = MemoryStorage()` 和 `self._recorder = TraceRecorder(storage=self._storage)`
- [x] 3.5 `GraphTraversalEngine` 构造传入 `trace_recorder=self._recorder`
- [x] 3.6 删除 `try/except ImportError` 和 `self.engine = None` fallback
- [x] 3.7 删除 `_setup_context_integration()` 方法

## 4. 删除手写 DFS fallback

- [x] 4.1 删除 `_execute_fallback_simulation()` 方法（~110 行）
- [x] 4.2 删除 `_interact_with_next_element()` 方法（~15 行）
- [x] 4.3 删除 `_execute_element_action()` 方法（~50 行）
- [x] 4.4 删除 `_go_back()` 方法（~25 行）
- [x] 4.5 `run()` 方法移除 `if self.engine: ... else: ...` 分支，直接调用 `self.engine.run()`

## 5. 删除冗余运行时状态

- [x] 5.1 删除 `self.current_path` 字段
- [x] 5.2 删除 `self.visited_pages` 字段
- [x] 5.3 删除 `self.visited_elements` 字段
- [x] 5.4 删除 `self.current_element_index` 字段
- [x] 5.5 删除 `self.step_count` 字段

## 6. SimulationResult 数据源改为 TraceAnalyzer

- [x] 6.1 `_build_simulation_result()` 改用 `self._storage.read(trace_id)` 获取节点
- [x] 6.2 使用 `TraceAnalyzer` 提取 `action_sequence` 填入 `result.trace`
- [x] 6.3 使用 `TraceAnalyzer` 提取 `error_statistics`、`time_analysis`、`coverage_analysis` 填入 `result.statistics`
- [x] 6.4 `_enhance_visited_tree_from_trace()` 更新为从 TraceAnalyzer 提取页面树

## 7. 测试更新

- [x] 7.1 更新 `src/simulation/test/test_mock_vision.py`：验证 `isinstance(mock, VisionService)` 和 `analyze_screenshot` 返回 `PageAnalysis`
- [x] 7.2 更新 `src/simulation/test/test_mock_action.py`：验证 `isinstance(mock, OperationExecutor)` 和 `execute()` 返回 `ExecutionResult`
- [x] 7.3 更新 `src/simulation/test/test_runner.py`：断言改为 TraceAnalyzer 方式
- [x] 7.4 更新 `tests/v6/test_simulation.py`：对齐新接口，验证 trace 中 session/step/span 节点存在
- [x] 7.5 更新 `tests/v6/test_executor.py`：接口兼容微调

## 8. 验收验证

- [x] 8.1 `SimulationRunner` 成功创建 `GraphTraversalEngine`（不再 ImportError）
- [x] 8.2 `isinstance(MockVisionService(), VisionService)` 返回 True
- [x] 8.3 `isinstance(MockActionExecutor(), OperationExecutor)` 返回 True
- [x] 8.4 仿真运行后 MemoryStorage 包含 session/step/span 节点
- [x] 8.5 TraceAnalyzer 从仿真 trace 提取 analysis 数据成功
- [x] 8.6 现有已通过的 V6 测试无回归

---

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| `src/simulation/` | [simulation-design.md](../../docs/architecture/modules/simulation-design.md) |
| `src/vision/` | [vision-design.md](../../docs/architecture/modules/vision-design.md) |
| `src/trace/` | [trace-design.md](../../docs/architecture/modules/trace-design.md) |
| `src/traversal/` | [traversal-design.md](../../docs/architecture/modules/traversal-design.md) |
