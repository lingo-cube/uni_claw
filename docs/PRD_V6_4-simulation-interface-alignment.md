# V6.4 仿真接口对齐 PRD

**版本**: V6.4  
**日期**: 2026-06-06  
**状态**: 设计已确认  
**依赖**: V6.3 trace-integration（已完成）

---

## 1. 概述

当前仿真系统 (`SimulationRunner`) 存在死导入 → 永远走手写 DFS 兜底 → 绕过了真实的 `GraphTraversalEngine`、`TraversalStateMachine`、以及 V6.3 Trace 系统。Mock 服务未实现真实接口，导致仿真与生产环境代码路径完全不同。

**目标**：打通 `SimulationRunner → GraphTraversalEngine → TraversalStateMachine → TraceRecorder` 完整调用链。AI 视觉和 ADB 执行使用 Mock（但必须实现真实接口并接入 Trace），其余全部走真实核心组件。

**核心原则**：
- **接口驱动**：Mock 必须继承真实 ABC，签名与返回值完全匹配
- **代码路径一致**：仿真和生产走同一引擎、同一状态机、同一 Trace 系统
- **最小删除**：移除手写 DFS fallback（~250 行），不再维护两套逻辑
- **Trace 可观测**：仿真结果通过 `TraceAnalyzer` 提取，不再直接访问 mock 内部状态

---

## 2. 架构变更

### 现状

```
SimulationRunner
  ├── from src.graph.graph_traversal_engine import ...  ← 死导入（文件不存在）
  ├── ImportError → self.engine = None
  └── run()
        └── _execute_fallback_simulation()   ← 手写 DFS
              ├── MockVisionService (独立类，无接口)
              ├── MockActionExecutor (独立类，无接口)
              └── InMemoryTracer (旧版 trace)
```

### 目标

```
SimulationRunner
  ├── from src.traversal.graph_engine import GraphTraversalEngine
  ├── self.engine = GraphTraversalEngine(
  │       plan=plan,
  │       vision_service=MockVisionService(VisionService ABC),
  │       action_executor=MockActionExecutor(OperationExecutor ABC),
  │       trace_recorder=TraceRecorder(MemoryStorage),
  │   )
  └── run()
        └── engine.run()
              ├── TraversalStateMachine (真实状态机)
              ├── MockVisionService implements VisionService
              ├── MockActionExecutor implements OperationExecutor
              └── TraceRecorder → MemoryStorage (V6.3)
```

---

## 3. 改动详细设计

### 3.1 MockVisionService 重写

**文件**: `src/simulation/mock_vision.py`

**改前**：独立类，`analyze_screenshot(screenshot_path: str) -> Dict`，无基类。

**改后**：继承 `src.vision.vision_service.VisionService` ABC。

```python
from src.vision.vision_service import VisionService
from src.state.content_tree import PageAnalysis

class MockVisionService(VisionService):
    def __init__(self, virtual_pages: Dict[str, Dict]):
        self._virtual_pages = virtual_pages
        self._current_path: List[str] = []
        self._call_count = 0

    def set_path_context(self, path: List[str]) -> None:
        """由引擎在每次 step 前更新当前路径，用于查表。"""
        self._current_path = list(path)

    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """从 virtual_pages 按 current_path 查表，组装 PageAnalysis。
        image_data 参数在仿真中被忽略（不从真实截图提取特征）。
        """
        self._call_count += 1
        key = "/".join(self._current_path) if self._current_path else "home"
        data = self._virtual_pages.get(key, self._virtual_pages.get("home", {}))
        return self._build_page_analysis(data)

    def find_app_entry(self, image_data: bytes, target: str) -> Optional[dict]:
        """仿真始终返回屏幕中心坐标。"""
        return {"x": 0.5, "y": 0.5}

    def _build_page_analysis(self, data: Dict) -> PageAnalysis:
        """将 virtual_page 字典转换为 PageAnalysis 对象。
        复用 PageAnalyzer 的解析逻辑，确保返回类型匹配真实接口。
        """
        # ... 内部调用 PageAnalyzer 或直接构造 PageAnalysis
```

**关键决策**：
- `analyze_screenshot` 忽略 `image_data: bytes` 参数——仿真不需要真实截图，通过 `current_path` 确定当前页面
- `set_path_context()` 由引擎在遍历循环中调用，保持路径同步
- 返回真实的 `PageAnalysis` pydantic 模型，不是 dict

---

### 3.2 MockActionExecutor 重写

**文件**: `src/simulation/mock_action.py`

**改前**：独立类，`tap/swipe/click/press_back/press_home/input_text/scroll/go_back` 各自方法，全部 `return True`。

**改后**：继承 `src.simulation.operation_executor.OperationExecutor` ABC，统一 `execute()` 入口。

```python
from src.simulation.operation_executor import (
    OperationExecutor, ExecutionContext, ExecutionResult
)

class MockActionExecutor(OperationExecutor):
    def __init__(self, simulate_delay: float = 0.0):
        self._history: List[Dict] = []
        self._delay = simulate_delay

    def execute(self, context: ExecutionContext) -> ExecutionResult:
        """记录操作并返回成功。不执行真实 ADB 命令。"""
        if self._delay > 0:
            time.sleep(self._delay)
        self._history.append({
            "node_id": context.node_id,
            "node_name": context.node_name,
            "operation": context.operation,
            "timestamp": context.timestamp or datetime.now(),
        })
        return ExecutionResult(success=True, action=str(context.operation))

    def get_executed_actions(self) -> list[str]:
        return [h.get("operation", {}).get("action", "unknown") for h in self._history]

    def clear_history(self) -> None:
        self._history.clear()

    @property
    def history(self) -> List[Dict]:
        return list(self._history)
```

**关键决策**：
- 删除零散的 `tap/swipe/click` 方法，统一为 `execute(context)` 
- `ExecutionResult` 始终 `success=True`（仿真不模拟失败，除非测试需要）
- 保留 `history` 属性用于测试断言，但主推 TraceAnalyzer 断言方式

---

### 3.3 SimulationRunner 重写

**文件**: `src/simulation/runner.py`

**改动点**：

#### 3.3.1 修复导入

```python
# 改前（删除）
from src.graph.graph_traversal_engine import GraphTraversalEngine

# 改后
from src.traversal.graph_engine import GraphTraversalEngine
from src.trace.recorder import TraceRecorder
from src.trace.storage import MemoryStorage
from src.trace.analyzer import TraceAnalyzer
```

#### 3.3.2 构造函数

```python
def __init__(self, virtual_pages, plan, config=None):
    self.virtual_pages = virtual_pages
    self.plan = plan
    self.config = config or {}

    # Mock 服务（实现真实接口）
    self.vision = MockVisionService(virtual_pages)
    self.action = MockActionExecutor(
        simulate_delay=self.config.get("action_delay", 0.0)
    )

    # V6.3 Trace 系统（替代 InMemoryTracer）
    self._storage = MemoryStorage()
    self._recorder = TraceRecorder(storage=self._storage)

    # 真实引擎（不再 try/except fallback）
    self.engine = GraphTraversalEngine(
        plan=plan,
        vision_service=self.vision,
        action_executor=self.action,
        trace_recorder=self._recorder,
    )

    # 删除以下字段：
    # self.tracer = InMemoryTracer()
    # self.current_path / visited_pages / visited_elements
    # self.current_element_index / step_count
```

#### 3.3.3 run() 方法

```python
def run(self) -> SimulationResult:
    self._start_time = time.time()
    try:
        engine_result = self.engine.run()
        self._result = self._build_simulation_result(engine_result)
        return self._result
    except Exception as e:
        return self._handle_error(e)
    finally:
        self._end_time = time.time()
```

**删除 `if self.engine: ... else: ...` 分支**——不再有 fallback。

#### 3.3.4 _build_simulation_result()

```python
def _build_simulation_result(self, engine_result) -> SimulationResult:
    tid = getattr(engine_result, 'trace_id', '')
    nodes = self._storage.read(tid)
    analyzer = TraceAnalyzer(nodes)

    return SimulationResult(
        engine_result={"status": str(engine_result.status)},
        trace=analyzer.extract_action_sequence(),
        executed_actions=analyzer.extract_action_sequence(),
        visited_tree={},  # 由 TraceAnalyzer 的 page_tree 替代
        elapsed_seconds=time.time() - self._start_time,
        statistics={
            "time": analyzer.extract_time_analysis(),
            "errors": analyzer.extract_error_statistics(),
            "coverage": analyzer.extract_coverage_analysis(),
        },
    )
```

#### 3.3.5 删除清单

| 删除内容 | 行数估计 |
|----------|---------|
| `_execute_fallback_simulation()` | ~110 |
| `_interact_with_next_element()` | ~15 |
| `_execute_element_action()` | ~50 |
| `_go_back()` | ~25 |
| `self.tracer` / `InMemoryTracer` 相关引用 | ~20 |
| `self.current_path` / `visited_pages` / `visited_elements` 等冗余状态 | ~15 |
| `_setup_context_integration()` | ~10 |
| **合计** | **~250 行** |

---

### 3.4 测试更新

**文件**: `src/simulation/test/*.py`、`tests/v6/test_simulation.py`

**原则**：断言从 mock 内部状态迁移到 Trace 分析结果。

```python
# 改前：直接访问 mock 状态
assert mock_vision.call_count == 5
assert mock_action.history[0]["action"] == "click"
assert len(tracer.steps) == 20

# 改后：通过 TraceAnalyzer 断言
analyzer = TraceAnalyzer(storage.read(trace_id))
assert len(analyzer.extract_ai_calls()) == 0  # V6.5 才会有
assert len(analyzer.extract_action_sequence()) >= 1
assert analyzer.extract_error_statistics()["total_errors"] == 0

# V6.4 新增验证
nodes = storage.read(trace_id)
assert any(n.node_type == "session" for n in nodes)
assert any(n.node_type == "step" for n in nodes)
assert any(n.span_type == "session_end" for n in nodes if hasattr(n, 'span_type'))
```

---

## 4. 不变的组件

| 组件 | 说明 |
|------|------|
| `PageAnalyzer` | 保留，MockVisionService 内部使用其解析 virtual_page |
| `virtual_pages` 机制 | 保留，仿真数据源 |
| `InMemoryTracer` / `Visualizer` | 保留文件不删除，不再被 SimulationRunner 引用 |
| `OperationExecutor` ABC | 保留，MockActionExecutor 实现它 |
| `VisionService` ABC | 保留，MockVisionService 实现它 |
| `ExecutionContext` / `ExecutionResult` | 保留，MockActionExecutor 使用 |
| `SimulationResult` | 保留结构，数据来源改为 TraceAnalyzer |
| `TraversalRuntimeContext` | 保留，引擎管理状态 |

---

## 5. 与 V6.5 的边界

| 维度 | V6.4（本 PRD） | V6.5（后续） |
|------|---------------|-------------|
| Mock 接口 | ✅ 实现 VisionService / OperationExecutor ABC | - |
| 引擎调用链 | ✅ 打通 SimulationRunner → Engine → StateMachine | - |
| Trace 集成 | ✅ TraceRecorder + MemoryStorage | - |
| 删除 fallback | ✅ 删除手写 DFS | - |
| 状态机 handler | ⚠️ 仍是占位符 `{"success": True}` | ✅ 真正调用 action.execute() / vision.analyze_screenshot() |
| AI call span | ❌ 不产生（handler 不调 vision） | ✅ 引擎 `_record_ai_call_span` 被触发 |
| execution span | ❌ 不产生（handler 不调 action） | ✅ 引擎 `_record_execution_span` 被触发 |
| error span | ⚠️ 仅在引擎级异常时产生 | ✅ handler 异常 → error span |

---

## 6. 风险与缓解

| 风险 | 影响 | 缓解 |
|------|------|------|
| 引擎 `run()` 循环不终止 | 仿真挂起 | 状态机 handler 是占位符，`_should_continue()` 检查 node_stack，栈空则退出；Mock 操作瞬时完成 |
| `PageAnalysis` 构造失败 | 类型错误 | MockVisionService 从 virtual_pages 查表后构造 `PageAnalysis` pydantic 模型，字段对齐由单元测试保证 |
| 旧测试大量失败 | CI 阻塞 | Phase 4 统一迁移测试断言到 TraceAnalyzer |
| 已存在的 V6 测试挂起 | 开发效率 | `tests/v6/test_executor.py` 中引擎初始化测试已知通过，其余挂起测试是预存问题（与本次改动无关） |
| `OperationExecutor.execute()` 参数 `ExecutionContext` 与状态机 step() 传参不匹配 | 运行时错误 | V6.4 状态机 handler 仍为占位符，不真正调用 action；V6.5 再对齐参数 |

---

## 7. 验收标准

1. ✅ `SimulationRunner` 成功创建 `GraphTraversalEngine` 实例（不再 ImportError）
2. ✅ `MockVisionService` 是 `VisionService` 的子类，`isinstance` 检查通过
3. ✅ `MockActionExecutor` 是 `OperationExecutor` 的子类，`isinstance` 检查通过
4. ✅ 仿真运行后 `MemoryStorage` 中包含 session/step/span 节点
5. ✅ `TraceAnalyzer` 能从仿真 trace 中提取 `action_sequence`、`time_analysis`、`error_statistics`、`coverage_analysis`
6. ✅ 手写 DFS fallback 相关代码已删除（`_execute_fallback_simulation` 等方法不存在）
7. ✅ `SimulationRunner` 上不再有 `current_path` / `visited_pages` / `visited_elements` / `tracer` 等冗余字段
8. ✅ 现有仿真测试更新为 TraceAnalyzer 断言方式
9. ✅ 新增测试验证引擎创建成功且 trace 结构完整
10. ✅ 现有已通过的 V6 测试（引擎 creation 3 个 + trace 123 个）无回归

---

## 8. 文件变更清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `src/simulation/runner.py` | 重写 | 修复导入、集成 Trace、删除 fallback |
| `src/simulation/mock_vision.py` | 重写 | 继承 VisionService ABC |
| `src/simulation/mock_action.py` | 重写 | 继承 OperationExecutor ABC |
| `src/simulation/test/test_mock_vision.py` | 更新 | TraceAnalyzer 断言 |
| `src/simulation/test/test_mock_action.py` | 更新 | TraceAnalyzer 断言 |
| `src/simulation/test/test_runner.py` | 更新 | TraceAnalyzer 断言 |
| `tests/v6/test_simulation.py` | 更新 | 对齐新接口 |
| `tests/v6/test_executor.py` | 微调 | 接口兼容 |

---

## 9. 不在此 PRD 范围

- 状态机 handler 操作执行逻辑（留给 V6.5）
- AI 服务真实调用指标采集（留给 V6.5）
- 仿真可视化仪表盘（dashboard 已完成，独立于本 PRD）
- 真实 ADB 执行器仿真集成（留给 V6.5）
