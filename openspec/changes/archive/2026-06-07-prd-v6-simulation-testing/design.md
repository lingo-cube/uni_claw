# Design: PRD V6.0 Simulation Testing System

## 系统架构设计

### 完整系统架构

```
┌─────────────────────────────────────────────────────────────────┐
│  Complete AI-Friendly Simulation Testing Ecosystem              │
├─────────────────────────────────────────────────────────────────┤
│  Developer Experience Layer                                      │
│  ├── CLI Tools (simtest)                                       │
│  ├── Pytest Integration                                        │
│  └── AI-Assisted Debugging (optional)                          │
│        ↓                                                       │
│  Test Case Layer (AI-Friendly Format)                          │
│  ├── test_case.json (intent_slots + expected)                 │
│  ├── Standard Test Fixtures (5 core scenarios)                │
│  └── Test Case Generator (AI-driven, optional)                │
│        ↓                                                       │
│  Simulation Execution Layer                                     │
│  ├── SimulationRunner (complete wrapper) ✅                    │
│  ├── MockVisionService (path-aware) ✅                        │
│  ├── MockActionExecutor (enhanced recording) ✅               │
│  └── InMemoryTracer (unified visualization) ✅                 │
│        ↓                                                       │
│  Assertion & Comparison Layer                                  │
│  ├── TraceAsserter (automated comparison)                      │
│  ├── Intent Validator                                          │
│  └── Event Sequence Matcher                                    │
│        ↓                                                       │
│  Reporting & Analysis Layer                                    │
│  ├── Test Report Generator (JSON + HTML)                       │
│  ├── AI-Failure Analyzer (optional)                           │
│  └── Visualization Exporter                                    │
│        ↓                                                       │
│  CI/CD Integration Layer                                       │
│  ├── Pipeline Integration                                      │
│  ├── Quality Gates                                              │
│  └── Artifact Archiving                                        │
└─────────────────────────────────────────────────────────────────┘
```

## 核心组件设计

### 1. PageAnalyzer 组件

#### 设计目标
充当真实视觉分析管道的仿真等价物，将原始页面数据转换为正确的 PageAnalysis 格式。

#### 类设计

```python
class PageAnalyzer:
    """
    分析和结构化页面数据用于仿真测试。
    
    充当真实视觉分析管道的仿真等价物，
    将原始页面数据转换为正确的 PageAnalysis 格式。
    """
    
    def __init__(self, virtual_pages: Dict[str, Dict[str, Any]]):
        """使用虚拟页面数据初始化分析器"""
        self._virtual_pages = virtual_pages
        self._cache = {}
        
    def analyze_page(self, path: str) -> Dict[str, Any]:
        """分析页面并返回结构化的PageAnalysis"""
        if path in self._cache:
            return self._cache[path]
            
        raw_data = self._get_raw_page_data(path)
        page_analysis = self._structure_page_analysis(path, raw_data)
        self._cache[path] = page_analysis
        return page_analysis
        
    def _get_raw_page_data(self, path: str) -> Dict[str, Any]:
        """获取原始页面数据"""
        if path not in self._virtual_pages:
            raise PageNotFoundError(f"Page not found: {path}")
        return self._virtual_pages[path]
        
    def _structure_page_analysis(self, path: str, raw_data: Dict) -> Dict[str, Any]:
        """将原始页面数据转换为正确的PageAnalysis结构"""
        return {
            "page_type": self._infer_page_type(raw_data),
            "page_path": path,
            "elements": self._process_elements(raw_data.get("elements", [])),
            "metadata": {
                "timestamp": time.time(),
                "source": "simulation",
                "page_name": raw_data.get("page_name", "unknown")
            }
        }
        
    def _process_elements(self, elements: List[Dict]) -> List[Dict]:
        """处理UI元素，添加类型和元数据"""
        processed = []
        for element in elements:
            processed_element = {
                "element_id": element.get("id", f"element_{len(processed)}"),
                "element_type": element.get("type", "unknown"),
                "text": element.get("text", ""),
                "bounds": element.get("bounds", {}),
                "action_hint": self._infer_action_hint(element),
                "metadata": {
                    "clickable": element.get("clickable", False),
                    "scrollable": element.get("scrollable", False),
                    "enabled": element.get("enabled", True)
                }
            }
            processed.append(processed_element)
        return processed
        
    def _infer_page_type(self, page_data: Dict) -> str:
        """从内容推断页面类型"""
        page_name = page_data.get("page_name", "").lower()
        elements = page_data.get("elements", [])
        
        # 基于页面名称和元素特征推断类型
        if "settings" in page_name or "设置" in page_name:
            return "settings"
        elif any(e.get("type") == "list" for e in elements):
            return "list"
        elif any(e.get("type") == "webview" for e in elements):
            return "web"
        else:
            return "unknown"
            
    def _infer_action_hint(self, element: Dict) -> str:
        """推断元素的建议操作"""
        element_type = element.get("type", "").lower()
        clickable = element.get("clickable", False)
        scrollable = element.get("scrollable", False)
        
        if clickable and element_type in ["button", "switch"]:
            return "click"
        elif scrollable:
            return "scroll"
        elif element_type == "slider":
            return "adjust"
        else:
            return "view"
```

#### 关键设计决策

1. **缓存策略** - 使用内存缓存提高重复访问性能
2. **错误处理** - 明确的 PageNotFoundError 异常
3. **类型推断** - 基于启发式规则的页面类型推断
4. **可扩展性** - 支持自定义页面类型和操作提示

### 2. 增强的 MockVisionService

#### 设计目标
修复 MockVisionService 以正确跟踪当前遍历路径，并基于路径返回正确的 PageAnalysis 数据。

#### 类设计

```python
class MockVisionService:
    """具有PageAnalyzer集成的增强Mock视觉服务"""
    
    def __init__(self, virtual_pages: Dict[str, Dict[str, Any]]):
        """使用虚拟页面初始化Mock视觉服务"""
        self.virtual_pages = virtual_pages
        self._analyzer = PageAnalyzer(virtual_pages)
        self._path_mapping = self._build_path_mapping(virtual_pages)
        self._call_count = 0
        self._current_context = None
        self._path_getter = None
        
    def _build_path_mapping(self, virtual_pages: Dict) -> Dict[str, str]:
        """构建路径到页面数据的映射"""
        mapping = {}
        for path, data in virtual_pages.items():
            page_name = data.get("page_name", path)
            mapping[page_name] = path
        return mapping
        
    def set_context(self, context: Any) -> None:
        """设置遍历上下文用于路径解析"""
        self._current_context = context
        
        # 支持多种上下文类型
        if hasattr(context, 'current_path'):
            # TraversalContext 支持
            self._path_getter = lambda: "/".join(context.current_path)
        elif hasattr(context, 'visited_tree'):
            # InMemoryTracer 支持
            self._path_getter = lambda: self._infer_path_from_tracer(context)
        else:
            # 默认路径
            self._path_getter = lambda: "root"
            
    def _infer_path_from_tracer(self, tracer: 'InMemoryTracer') -> str:
        """从追踪器推断当前路径"""
        if not tracer.steps:
            return "root"
        last_step = tracer.steps[-1]
        return getattr(last_step, 'current_path', 'root')
        
    def analyze_screenshot(self, screenshot_path: Optional[str] = None) -> Dict[str, Any]:
        """使用PageAnalyzer分析当前屏幕截图"""
        self._call_count += 1
        current_path = self._get_current_path()
        return self._analyzer.analyze_page(current_path)
        
    def _get_current_path(self) -> str:
        """获取当前遍历路径"""
        if self._path_getter:
            return self._path_getter()
        return "root"
        
    def get_call_count(self) -> int:
        """获取分析调用次数（用于测试验证）"""
        return self._call_count
        
    def reset(self) -> None:
        """重置服务状态"""
        self._call_count = 0
        self._current_context = None
        self._path_getter = None
```

#### 关键设计决策

1. **多上下文支持** - 支持 TraversalContext 和 InMemoryTracer
2. **路径映射** - 智能路径到页面数据的映射
3. **调用跟踪** - 记录调用次数用于测试验证
4. **状态重置** - 支持重置以便重复使用

### 3. 完整的 SimulationRunner

#### 设计目标
完成 SimulationRunner 包装器，正确封装 GraphTraversalEngine，处理上下文设置，并提供清晰的结果提取用于测试。

#### 类设计

```python
class SimulationRunner:
    """
    V6 离线测试的完整仿真运行器。
    
    正确包装 GraphTraversalEngine 及所有 Mock 组件，
    为测试断言提供清晰的结果提取。
    """
    
    def __init__(
        self,
        virtual_pages: Dict[str, Dict[str, Any]],
        plan: TraversalPlan,
        config: Optional[Dict[str, Any]] = None,
    ):
        """使用所有组件初始化仿真运行器"""
        self.virtual_pages = virtual_pages
        self.plan = plan
        self.config = config or {}
        
        # 创建 Mock 组件及正确初始化
        self.vision = MockVisionService(virtual_pages)
        self.action = MockActionExecutor(
            simulate_delay=self.config.get("action_delay", 0.0)
        )
        self.tracer = InMemoryTracer()
        
        # 创建 GraphTraversalEngine 及所有依赖
        self.engine = GraphTraversalEngine(
            plan=plan,
            vision_service=self.vision,
            action_executor=self.action,
            trace_recorder=self.tracer,
            template_registry=self.config.get("template_registry"),
            exception_chain=self.config.get("exception_chain"),
        )
        
        # 设置上下文集成
        self._setup_context_integration()
        
        # 结果存储
        self._result: Optional[SimulationResult] = None
        self._start_time: Optional[float] = None
        self._end_time: Optional[float] = None
        
    def _setup_context_integration(self) -> None:
        """设置上下文集成，确保Mock组件接收正确的路径信息"""
        # 为 MockVisionService 设置 tracer 上下文
        self.vision.set_context(self.tracer)
        
        # 为 MockActionExecutor 设置 tracer 上下文
        self.action.set_context(self.tracer)
        
    def run(self) -> SimulationResult:
        """执行仿真及正确的设置和结果提取"""
        self._start_time = time.time()
        
        try:
            # 开始追踪记录
            self.tracer.start_traversal(self.plan)
            
            # 执行实际遍历引擎
            engine_result = self.engine.run()
            
            # 提取和结构化结果
            self._result = self._extract_results(engine_result)
            
            return SimulationResult(
                engine_result=self._result.to_dict(),
                trace=[step.to_dict() for step in self.tracer.steps],
                executed_actions=self.action.get_history(),
                visited_tree=self._extract_visited_tree(),
                elapsed_seconds=time.time() - self._start_time,
                completion_reason=self._extract_completion_reason(),
                statistics=self._compute_statistics(),
            )
            
        except Exception as e:
            return self._handle_error(e)
            
        finally:
            self._end_time = time.time()
            
    def _extract_results(self, engine_result: Any) -> Any:
        """提取和结构化引擎结果"""
        # 创建结构化的结果对象
        return StructuredResult(
            success=engine_result.success if hasattr(engine_result, 'success') else True,
            completion_reason=engine_result.completion_reason if hasattr(engine_result, 'completion_reason') else "unknown",
            visited_nodes=engine_result.visited_nodes if hasattr(engine_result, 'visited_nodes') else [],
            metadata={
                "engine_type": "GraphTraversalEngine",
                "plan_id": self.plan.plan_id if hasattr(self.plan, 'plan_id') else "unknown"
            }
        )
        
    def _extract_visited_tree(self) -> Dict[str, Dict[str, Any]]:
        """提取访问树结构"""
        visited_tree = {}
        
        for step in self.tracer.steps:
            node_id = getattr(step, 'current_node', 'unknown')
            if node_id not in visited_tree:
                visited_tree[node_id] = {
                    "node_id": node_id,
                    "visit_count": 0,
                    "first_visit": getattr(step, 'timestamp', None),
                    "last_visit": getattr(step, 'timestamp', None),
                    "operations": []
                }
            
            visited_tree[node_id]["visit_count"] += 1
            visited_tree[node_id]["last_visit"] = getattr(step, 'timestamp', None)
            
            if hasattr(step, 'operation') and step.operation:
                visited_tree[node_id]["operations"].append(step.operation)
                
        return visited_tree
        
    def _extract_completion_reason(self) -> str:
        """提取完成原因"""
        if not self.tracer.steps:
            return "no_steps"
            
        last_step = self.tracer.steps[-1]
        return getattr(last_step, 'completion_reason', 'unknown')
        
    def _compute_statistics(self) -> Dict[str, Any]:
        """计算统计信息"""
        total_steps = len(self.tracer.steps)
        unique_nodes = len(self._extract_visited_tree())
        action_count = len(self.action.get_history())
        
        return {
            "total_steps": total_steps,
            "unique_nodes": unique_nodes,
            "action_count": action_count,
            "steps_per_node": total_steps / max(unique_nodes, 1),
            "execution_time": self._end_time - self._start_time if self._end_time and self._start_time else 0
        }
        
    def _handle_error(self, error: Exception) -> SimulationResult:
        """处理执行错误"""
        return SimulationResult(
            engine_result={
                "success": False,
                "error": str(error),
                "error_type": type(error).__name__
            },
            trace=[step.to_dict() for step in self.tracer.steps],
            executed_actions=self.action.get_history(),
            visited_tree={},
            elapsed_seconds=time.time() - self._start_time if self._start_time else 0,
            completion_reason="error",
            statistics={
                "error": True,
                "error_message": str(error)
            }
        )
        
    # 可视化方法
    def render_tree(self, max_depth: Optional[int] = None) -> str:
        """渲染遍历树为 ASCII 格式"""
        return self.tracer.render_tree(max_depth=max_depth)
        
    def render_mermaid(self) -> str:
        """渲染状态图为 Mermaid 格式"""
        return self.tracer.render_mermaid()
        
    def export_trace(self, format: str = "jsonl") -> str:
        """导出指定格式的追踪"""
        if format == "jsonl":
            return "\n".join([json.dumps(step.to_dict()) for step in self.tracer.steps])
        elif format == "html":
            return self.tracer.generate_html_report()
        else:
            raise ValueError(f"Unsupported format: {format}")
```

#### 关键设计决策

1. **完整包装** - 正确包装所有 GraphTraversalEngine 依赖
2. **上下文集成** - 确保 Mock 组件接收正确的路径信息
3. **结果提取** - 结构化的结果提取用于测试断言
4. **错误处理** - 有意义的错误处理和报告
5. **可视化** - 多格式可视化支持调试

### 4. 增强的 MockActionExecutor

#### 设计目标
增强 MockActionExecutor 以提供综合的操作记录，支持测试断言和调试。

#### 类设计

```python
class MockActionExecutor:
    """
    具有综合操作记录的 Mock 操作执行器。
    
    记录详细的操作信息用于测试断言、调试和追踪验证。
    """
    
    def __init__(self, simulate_delay: float = 0.0):
        """初始化 Mock 操作执行器"""
        self.simulate_delay = simulate_delay
        self.action_history: List[OperationRecord] = []
        self._operation_context: Dict[str, Any] = {}
        self._page_context: Optional[Dict[str, Any]] = None
        self._node_stack: List[str] = []
        
    def set_context(self, context: Any) -> None:
        """设置操作上下文"""
        self._operation_context = {
            "current_node": getattr(context, 'current_node', None),
            "current_path": getattr(context, 'current_path', []),
            "depth": getattr(context, 'depth', 0)
        }
        
    def set_page_context(self, page_context: Dict[str, Any]) -> None:
        """设置页面上下文"""
        self._page_context = page_context.copy() if page_context else {}
        
    def click(self, element_id: str, **kwargs) -> bool:
        """模拟点击操作"""
        return self._record_operation(
            action_type="click",
            target_info={"element_id": element_id, **kwargs},
            result=True,
            metadata={"delay": self.simulate_delay}
        )
        
    def scroll(self, direction: str, distance: int = 1, **kwargs) -> bool:
        """模拟滚动操作"""
        return self._record_operation(
            action_type="scroll",
            target_info={"direction": direction, "distance": distance, **kwargs},
            result=True,
            metadata={"delay": self.simulate_delay}
        )
        
    def input_text(self, text: str, element_id: Optional[str] = None, **kwargs) -> bool:
        """模拟文本输入操作"""
        return self._record_operation(
            action_type="input_text",
            target_info={"text": text, "element_id": element_id, **kwargs},
            result=True,
            metadata={"delay": self.simulate_delay}
        )
        
    def go_back(self, **kwargs) -> bool:
        """模拟返回操作"""
        return self._record_operation(
            action_type="go_back",
            target_info=kwargs,
            result=True,
            metadata={"delay": self.simulate_delay}
        )
        
    def _record_operation(
        self,
        action_type: str,
        target_info: Optional[Dict[str, Any]] = None,
        result: bool = True,
        metadata: Optional[Dict[str, Any]] = None,
    ) -> bool:
        """记录综合的操作信息"""
        operation_record: OperationRecord = {
            # 基本操作信息
            "action_type": action_type,
            "timestamp": time.time(),
            "result": "success" if result else "failed",
            
            # 上下文信息
            "current_node": self._operation_context.get("current_node"),
            "current_path": self._operation_context.get("current_path", []),
            "page_context": self._page_context.copy() if self._page_context else {},
            
            # 目标信息
            "target_info": target_info or {},
            
            # 操作详情
            "metadata": metadata or {},
            
            # 调试用栈信息
            "node_stack": self._node_stack.copy(),
        }
        
        self.action_history.append(operation_record)
        
        # 模拟延迟
        if self.simulate_delay > 0:
            time.sleep(self.simulate_delay)
            
        return result
        
    def get_history(self) -> List[OperationRecord]:
        """获取操作历史记录"""
        return self.action_history.copy()
        
    def get_operations_by_type(self, action_type: str) -> List[OperationRecord]:
        """按操作类型筛选历史记录"""
        return [op for op in self.action_history if op["action_type"] == action_type]
        
    def get_operation_count(self) -> int:
        """获取操作总数"""
        return len(self.action_history)
        
    def reset(self) -> None:
        """重置执行器状态"""
        self.action_history.clear()
        self._operation_context.clear()
        self._page_context = None
        self._node_stack.clear()
        
    def push_node(self, node_id: str) -> None:
        """推入节点栈（用于追踪嵌套调用）"""
        self._node_stack.append(node_id)
        
    def pop_node(self) -> Optional[str]:
        """弹出节点栈"""
        return self._node_stack.pop() if self._node_stack else None
```

#### 关键设计决策

1. **详细记录** - 记录操作的完整上下文信息
2. **多种操作** - 支持常见 UI 操作类型
3. **历史查询** - 支持按类型筛选和统计
4. **栈跟踪** - 维护节点栈用于调试嵌套调用
5. **状态管理** - 支持重置以便重复使用

## 测试框架设计

### AI 友好测试用例格式

```json
{
  "test_id": "e2e_all_traversal",
  "description": "全菜单遍历：验证深度优先顺序和恢复操作",
  "intent_slots": {
    "target_app": "设置",
    "scope": "all_menus",
    "element_handling": "full_interaction",
    "navigation": "adaptive",
    "restore": "restore"
  },
  "fixtures": {
    "plan_file": "plan_all.json",
    "pages_file": "pages_all.json"
  },
  "expected": {
    "completion_reason": "completed",
    "key_events": [
      "进入设置页",
      "点击'显示'",
      "滑动'亮度'并恢复",
      "点击切换'自动亮度'并恢复",
      "点击'字体'",
      "点击'字号'",
      "点击'小'按钮",
      "返回'设置'页",
      "点击'声音'",
      "滑动'音量'并恢复",
      "遍历完成"
    ],
    "total_steps_min": 15,
    "total_steps_max": 30,
    "must_not_contain": ["错误", "异常终止", "崩溃"]
  },
  "assertions": {
    "visited_nodes_min": 8,
    "restore_operations_count": 4,
    "navigation_correctness": "depth_first"
  }
}
```

### TraceAsserter 设计

```python
class TraceAsserter:
    """追踪断言引擎，自动化比较预期和实际追踪数据"""
    
    @staticmethod
    def step_to_nl(step: Dict[str, Any]) -> str:
        """将追踪步骤转换为自然语言描述"""
        action_type = step.get("action_type", "unknown")
        current_node = step.get("current_node", "unknown")
        target = step.get("target_info", {}).get("element_id", "unknown")
        
        descriptions = {
            "enter": f"进入 {current_node}",
            "exit": f"离开 {current_node}",
            "click": f"点击 {target}",
            "scroll": f"滑动 {current_node}",
            "go_back": f"返回上一级"
        }
        
        return descriptions.get(action_type, f"{action_type} {current_node}")
        
    @staticmethod
    def is_subsequence(expected: List[str], actual: List[str]) -> bool:
        """检查期望序列是否为实际序列的子序列"""
        it = iter(actual)
        return all(any(item == expected_item for item in it) for expected_item in expected)
        
    @staticmethod
    def assert_trace_matches_expected(
        trace: List[Dict[str, Any]],
        expected: Dict[str, Any],
    ) -> AssertionResult:
        """断言追踪与预期行为匹配"""
        actual_events = [TraceAsserter.step_to_nl(step) for step in trace]
        key_events = expected.get("key_events", [])
        must_not_contain = expected.get("must_not_contain", [])
        
        # 检查关键事件
        key_events_matched = [event for event in key_events if event in actual_events]
        missing_events = [event for event in key_events if event not in actual_events]
        
        # 检查违禁词
        found_violations = [word for word in must_not_contain 
                          if any(word in event for event in actual_events)]
        
        # 检查步骤数量
        total_steps = len(trace)
        steps_in_range = (
            total_steps >= expected.get("total_steps_min", 0) and
            total_steps <= expected.get("total_steps_max", float('inf'))
        )
        
        # 检查完成原因
        completion_reason_match = False
        if trace and "completion_reason" in expected:
            last_step = trace[-1]
            actual_reason = last_step.get("completion_reason", "")
            completion_reason_match = actual_reason == expected["completion_reason"]
        
        is_success = (
            len(missing_events) == 0 and
            len(found_violations) == 0 and
            steps_in_range and
            completion_reason_match
        )
        
        return AssertionResult(
            success=is_success,
            key_events_matched=len(key_events_matched),
            missing_events=missing_events,
            extra_events=[event for event in actual_events if event not in key_events],
            violations=found_violations,
            steps_valid=steps_in_range,
            completion_reason_match=completion_reason_match,
            details={
                "total_steps": total_steps,
                "key_events_matched": key_events_matched,
                "completion_reason": trace[-1].get("completion_reason", "") if trace else "no_trace"
            }
        )
```

## CI/CD 集成设计

### GitHub Actions 工作流

```yaml
name: Simulation Tests

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]

jobs:
  simulation-tests:
    runs-on: ubuntu-latest
    
    strategy:
      matrix:
        python-version: [3.10, 3.11]
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Set up Python
      uses: actions/setup-python@v4
      with:
        python-version: ${{ matrix.python-version }}
    
    - name: Install dependencies
      run: |
        python -m pip install --upgrade pip
        pip install -e .
        pip install pytest pytest-cov
    
    - name: Run simulation tests
      run: |
        simtest suite tests/simulation/fixtures --report ci_report.json
    
    - name: Generate coverage report
      run: |
        pytest tests/simulation/ --cov=src/simulation --cov-report=xml
    
    - name: Upload test reports
      uses: actions/upload-artifact@v3
      if: always()
      with:
        name: simulation-reports-${{ matrix.python-version }}
        path: |
          reports/
          coverage.xml
    
    - name: Comment PR with results
      if: github.event_name == 'pull_request'
      uses: actions/github-script@v6
      with:
        script: |
          const fs = require('fs');
          const report = JSON.parse(fs.readFileSync('ci_report.json', 'utf8'));
          const body = `## Simulation Test Results\n\n` +
                     `**Total Tests**: ${report.total_tests}\n` +
                     `**Passed**: ${report.passed_tests}\n` +
                     `**Failed**: ${report.failed_tests}\n` +
                     `**Success Rate**: ${(report.passed_tests / report.total_tests * 100).toFixed(1)}%\n`;
          github.rest.issues.createComment({
            issue_number: context.issue.number,
            owner: context.repo.owner,
            repo: context.repo.repo,
            body: body
          });
```

### Pre-commit 钩子

```bash
#!/bin/bash
# .git/hooks/pre-commit

echo "🧪 Running simulation tests..."

# 运行快速仿真测试
python -m simtest suite tests/simulation/fixtures --fast --report pre_commit_report.json

# 检查结果
if [ $? -ne 0 ]; then
    echo "❌ Simulation tests failed. Commit aborted."
    echo "Run 'simtest show pre_commit_report.json' for details."
    exit 1
fi

echo "✅ Simulation tests passed."
```

## 数据流设计

### 完整数据流

```
┌─────────────────────────────────────────────────────────────┐
│  Data Flow Architecture                                      │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  Input Layer                                                  │
│  ├── Virtual Pages (pages_all.json)                         │
│  ├── Traversal Plan (plan_all.json)                        │
│  └── Test Expectations (test_case.json)                    │
│        ↓                                                     │
│  Processing Layer                                            │
│  ├── PageAnalyzer.analyze_page()                           │
│  │   └── Raw Data → PageAnalysis                            │
│  ├── MockVisionService.analyze_screenshot()                │
│  │   └── Path → PageAnalysis                                │
│  ├── MockActionExecutor._record_operation()                 │
│  │   └── Operation → OperationRecord                       │
│  └── GraphTraversalEngine.run()                             │
│      └── Plan → TraversalResult                             │
│        ↓                                                     │
│  Output Layer                                                │
│  ├── Trace Data (List[TraceStep])                          │
│  ├── Action History (List[OperationRecord])                │
│  ├── Visited Tree (Dict[NodeId, NodeInfo])                 │
│  └── Statistics (Dict[str, Metric])                        │
│        ↓                                                     │
│  Validation Layer                                            │
│  ├── TraceAsserter (Expected vs Actual)                   │
│  │   └── Trace + Expected → AssertionResult               │
│  ├── AssertionResult (Pass/Fail + Details)                 │
│  └── TestReport (Structured Output)                        │
│        ↓                                                     │
│  Reporting Layer                                             │
│  ├── JSON Report (Machine-Readable)                        │
│  └── HTML Report (Human-Readable)                          │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## 错误处理策略

### 分层错误处理

1. **组件级错误** - 各组件内部处理，返回默认值
2. **集成级错误** - SimulationRunner 捕获并结构化
3. **测试级错误** - TraceAsserter 记录到断言结果
4. **报告级错误** - 报告生成器包含错误信息

### 错误恢复机制

```python
class SimulationErrorHandler:
    """仿真测试错误处理器"""
    
    @staticmethod
    def handle_component_error(error: Exception, component: str) -> Dict[str, Any]:
        """处理组件级错误"""
        return {
            "error_type": "component_error",
            "component": component,
            "error_message": str(error),
            "error_details": {
                "exception_type": type(error).__name__,
                "traceback": traceback.format_exc()
            },
            "recovery_action": "use_default_value"
        }
        
    @staticmethod
    def handle_integration_error(error: Exception, context: Dict) -> SimulationResult:
        """处理集成级错误，返回部分结果"""
        return SimulationResult(
            engine_result={
                "success": False,
                "partial": True,
                "error": str(error),
                "context": context
            },
            trace=context.get("trace", []),
            executed_actions=context.get("actions", []),
            visited_tree={},
            elapsed_seconds=context.get("elapsed_time", 0),
            completion_reason="error",
            statistics={
                "error": True,
                "partial_results": True
            }
        )
```

## 性能优化策略

### 缓存策略

1. **页面分析缓存** - PageAnalyzer 内存缓存
2. **路径映射缓存** - MockVisionService 路径缓存
3. **结果缓存** - SimulationRunner 结果缓存

### 性能监控

```python
class SimulationPerformanceMonitor:
    """仿真测试性能监控器"""
    
    def __init__(self):
        self.metrics = {}
        
    def track_component_performance(self, component: str, duration: float):
        """跟踪组件性能"""
        if component not in self.metrics:
            self.metrics[component] = []
        self.metrics[component].append(duration)
        
    def get_performance_report(self) -> Dict[str, Any]:
        """生成性能报告"""
        report = {}
        for component, durations in self.metrics.items():
            report[component] = {
                "avg_duration": sum(durations) / len(durations),
                "max_duration": max(durations),
                "min_duration": min(durations),
                "call_count": len(durations)
            }
        return report
```

## 扩展性设计

### 插件系统

```python
class SimulationTestPlugin:
    """仿真测试插件基类"""
    
    def before_test(self, test_case: Dict) -> None:
        """测试前钩子"""
        pass
        
    def after_test(self, result: SimulationResult) -> None:
        """测试后钩子"""
        pass
        
    def on_assertion(self, assertion: AssertionResult) -> None:
        """断言钩子"""
        pass
```

### 自定义断言

```python
class CustomAssertion:
    """自定义断言基类"""
    
    def assert_condition(self, result: SimulationResult) -> bool:
        """自定义断言条件"""
        raise NotImplementedError
```

这个设计文档提供了完整的技术架构和实现细节，为四阶段实施提供了清晰的指导。