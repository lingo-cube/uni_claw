# Simulation 模块设计文档

> **模块**: `src/simulation/`
> **版本**: V6.4
> **更新日期**: 2026-06-06

---

## 1. 模块概述

### 1.1 职责

Simulation 模块提供**离线仿真测试能力**，使用 Mock 组件模拟 AI 视觉分析和 ADB 动作执行，但通过**真实的 GraphTraversalEngine 和 TraversalStateMachine** 执行遍历逻辑。仿真和生产走同一引擎、同一状态机、同一 Trace 系统。

### 1.2 V6.4 核心变更

- **MockVisionService → VisionService ABC**: 继承 `src.vision.vision_service.VisionService`，`analyze_screenshot(image_data: bytes) -> PageAnalysis`
- **MockActionExecutor → OperationExecutor ABC**: 继承 `src.simulation.operation_executor.OperationExecutor`，统一 `execute(ExecutionContext) -> ExecutionResult`
- **引擎打通**: SimulationRunner 使用真实的 `src.traversal.graph_engine.GraphTraversalEngine`（修复死导入）
- **Trace 集成**: `MemoryStorage` + `TraceRecorder` 替代旧 `InMemoryTracer`
- **删除 DFS fallback**: `_execute_fallback_simulation()` 等 ~580 行手写遍历逻辑

### 1.3 模块结构

```
src/simulation/
├── runner.py             # SimulationRunner, PlanDebugger
├── mock_vision.py        # MockVisionService(VisionService ABC), PageAnalysisBuilder
├── mock_action.py        # MockActionExecutor(OperationExecutor ABC)
├── operation_executor.py # OperationExecutor(ABC), MockOperationExecutor, RealOperationExecutor
├── visualizer.py         # InMemoryTracer (保留，不再被 runner 引用)
├── page_analyzer.py      # PageAnalyzer (virtual_pages 查表)
└── demo_visualization.py
```

---

## 2. 核心类和接口

### 2.1 SimulationRunner

```python
class SimulationRunner:
    def __init__(self, virtual_pages: Dict, plan: TraversalPlan, config: Dict = None):
        self.vision = MockVisionService(virtual_pages)     # implements VisionService ABC
        self.action = MockActionExecutor(simulate_delay=0) # implements OperationExecutor ABC
        self._storage = MemoryStorage()                    # V6.3 Trace
        self._recorder = TraceRecorder(storage=self._storage)
        self.engine = GraphTraversalEngine(                # 真实引擎（无 fallback）
            plan=plan,
            vision_service=self.vision,
            action_executor=self.action,
            trace_recorder=self._recorder,
        )

    def run(self) -> SimulationResult:
        engine_result = self.engine.run()
        # 通过 TraceAnalyzer 提取结果
```

**V6.4 删除的属性和方法**:
- ~~`self.tracer`~~ (InMemoryTracer → MemoryStorage)
- ~~`self.current_path / visited_pages / visited_elements / current_element_index / step_count`~~
- ~~`_execute_fallback_simulation()`~~, ~~`_interact_with_next_element()`~~, ~~`_execute_element_action()`~~, ~~`_go_back()`~~
- ~~`render_tree()`~~, ~~`render_mermaid()`~~, ~~`export_trace()`~~

### 2.2 MockVisionService

```python
class MockVisionService(VisionService):  # ← V6.4: 继承 ABC
    def __init__(self, virtual_pages: Dict): ...

    # VisionService ABC 实现
    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """按 current_path 查 virtual_pages 字典，返回 PageAnalysis pydantic 模型"""
    def find_app_entry(self, image_data: bytes, target: str) -> Optional[dict]:
        """仿真返回屏幕中心坐标 {"x": 0.5, "y": 0.5}"""

    # 路径上下文
    def set_path_context(self, path: List[str]) -> None: ...
    def inject_path(self, path: str) -> None: ...
```

**V6.4 变更**: ~~`analyze_screenshot(screenshot_path: str) -> Dict`~~ → `analyze_screenshot(image_data: bytes) -> PageAnalysis`

### 2.3 MockActionExecutor

```python
class MockActionExecutor(OperationExecutor):  # ← V6.4: 继承 ABC
    def __init__(self, simulate_delay: float = 0.0): ...

    # OperationExecutor ABC 实现
    def execute(self, context: ExecutionContext) -> ExecutionResult:
        """统一操作入口，记录到 history，返回 ExecutionResult(success=True)"""
    def get_executed_actions(self) -> list[str]: ...
    def clear_history(self) -> None: ...
```

**V6.4 删除的方法**: ~~`tap()`~~, ~~`swipe()`~~, ~~`click()`~~, ~~`press_back()`~~, ~~`press_home()`~~, ~~`input_text()`~~, ~~`scroll()`~~, ~~`go_back()`~~, ~~`set_context()`~~, ~~`set_page_context()`~~, ~~`push_node()`~~, ~~`pop_node()`~~

### 2.4 SimulationResult

```python
@dataclass
class SimulationResult:
    engine_result: Dict         # 引擎返回 {"status": "GlobalState.COMPLETED"}
    trace: List[Dict]           # TraceAnalyzer.extract_action_sequence()
    executed_actions: List[Dict] # TraceAnalyzer.extract_action_sequence()
    visited_tree: Dict          # TraceAnalyzer.extract_page_tree()
    elapsed_seconds: float
    completion_reason: str
    statistics: Dict            # time_analysis + error_statistics + coverage_analysis
    trace_id: str               # ← V6.4 新增
```

### 2.5 其他类

- **PageAnalyzer**: virtual_pages 查表，解析为 PageAnalysis 格式（V6.4 不变）
- **OperationExecutor (ABC)**: `execute(ExecutionContext) -> ExecutionResult`
- **PlanDebugger**: 遍历计划交互式调试（remove_rule, set_target, set_max_depth, undo）

---

## 3. 执行流程

```
SimulationRunner.run()
  └── GraphTraversalEngine.run()           # 真实引擎
        ├── Session 创建 → TraceRecorder.init()
        ├── TraversalStateMachine.step()   # 真实状态机（V6.5 handler 调用 vision/action）
        │     ├── PRECONDITION_CHECK → vision.analyze_screenshot(b"")
        │     ├── EXECUTE → action.execute(ExecutionContext)
        │     ├── RESULT_VERIFY → vision.analyze_screenshot(b"")
        │     └── ERROR_HANDLING → context.consecutive_errors++
        ├── 引擎 _step_once() 生成 span
        │     ├── _record_state_transition(from, to)
        │     ├── _record_ai_call_span(metrics)      # 从 handler metrics
        │     ├── _record_execution_span(metrics)
        │     └── _record_error_span(metrics)
        └── finalize() → session_end span
  └── TraceAnalyzer(storage.read(trace_id))
        ├── extract_action_sequence() → result.trace
        ├── extract_time_analysis() → result.statistics
        └── extract_error_statistics() → result.statistics
```

---

## 4. 依赖关系

```
src/simulation/
  mock_vision.py    → src.vision.vision_service.VisionService (ABC)
                    → src.state.content_tree.PageAnalysis
  mock_action.py    → src.simulation.operation_executor.OperationExecutor (ABC)
  runner.py         → src.traversal.graph_engine.GraphTraversalEngine
                    → src.trace.recorder.TraceRecorder
                    → src.trace.storage.MemoryStorage
                    → src.trace.analyzer.TraceAnalyzer
```

---

## 5. 设计决策

### 5.1 接口一致性（V6.4）
Mock 必须继承真实 ABC，`isinstance` 检查通过。引擎不区分仿真/生产环境，统一依赖注入。

### 5.2 单一路径（V6.4）
删除手写 DFS fallback，仿真和生产走同一引擎代码路径。不再维护 `if self.engine: ... else: ...` 分支。

### 5.3 Trace 统一（V6.4）
`InMemoryTracer` → `MemoryStorage` + `TraceRecorder`。仿真结果通过 `TraceAnalyzer` 提取，不再直接访问 mock 内部状态。

### 5.4 路径上下文注入（V6.4）
`VisionService.analyze_screenshot(image_data: bytes)` 不传路径。仿真通过 `set_path_context(List[str])` 带外注入当前路径，Mock 按路径查 virtual_pages 字典。

---

**最后更新**: 2026-06-06
**维护者**: Uni-Claw 开发团队
