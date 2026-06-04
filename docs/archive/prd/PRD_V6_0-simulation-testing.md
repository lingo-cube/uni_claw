# Uni-Claw 仿真测试自动化系统 - PRD V6.0

> **文档版本**: V6.0
> **基于版本**: V5.x 系列PRD
> **创建日期**: 2026-06-03
> **状态**: 设计完成
> **变更类型**: 仿真测试自动化系统

---

## 文档说明

PRD V6.0 定义了**生产级仿真测试自动化系统**的完整架构和实施规范。通过CI优先的设计理念，实现无需真实设备的可靠遍历引擎测试，支持AI友好的测试用例格式和自动化报告生成。

**核心创新**：
- **CI优先架构** - 传统自动化代替AI依赖，确保可靠性和速度
- **仿真完整性** - 完整的GraphTraversalEngine包装，支持全场景测试
- **AI友好格式** - 测试用例既人可读又机器可解析
- **自动化断言** - 基于规则的智能验证引擎，无需人工干预
- **可视化调试** - 多格式报告输出，支持快速失败定位

---

# 1. 产品概述

## 1.1 背景与动机

当前V5.x系列实现了AI集成和视觉管道优化，但仿真测试系统存在严重缺陷：

| 问题 | 描述 | 影响 |
|------|------|------|
| **仿真器破损** | SimulationRunner只是占位符，未真正包装GraphTraversalEngine | 测试不可靠 |
| **上下文缺失** | MockVisionService无法获取当前路径，无法返回正确页面分析 | 仿真失效 |
| **文档缺失** | 无明确测试指南和组件使用说明 | 开发困惑 |
| **CI集成缺失** | 无自动化测试流水线 | 质量门禁缺失 |
| **测试用例不规范** | 缺少标准测试fixtures和验证方法 | 测试覆盖不足 |

## 1.2 解决方案：CI优先的仿真测试生态系统

```
┌─────────────────────────────────────────────────────────────────┐
│  CI-First Simulation Testing Ecosystem                          │
├─────────────────────────────────────────────────────────────────┤
│  Primary Automation (Reliable, Fast, Deterministic)              │
│  ├── CI Pipeline (GitHub Actions, Jenkins, etc.)                │
│  ├── Pytest Framework (standard, proven)                        │
│  ├── JSON Assertion Logic (rule-based, deterministic)           │
│  └── Structured Reporting (machine-readable)                    │
│        ↓                                                       │
│  Optional AI Enhancement (When needed, not required)            │
│  ├── AI Test Generator (future enhancement)                     │
│  ├── AI Failure Analyzer (debugging aid only)                  │
│  └── AI Report Summarizer (convenience feature)                 │
└─────────────────────────────────────────────────────────────────┘
```

**核心设计理念**：
- **传统自动化优先** - 用成熟的CI/CD工具而非不稳定的AI
- **确定性测试** - 基于规则的断言，避免AI幻觉
- **快速反馈** - 无外部API调用，秒级测试执行
- **零成本运营** - 无AI API费用，频繁运行无负担
- **渐进增强** - AI作为可选增强，非必需组件

## 1.3 预期收益

| 收益类型 | 当前状态 | 目标状态 | 改善幅度 |
|---------|---------|----------|----------|
| **测试可靠性** | 仿真器破损，路径映射错误 | 完整仿真，正确路径映射 | 100% |
| **开发速度** | 需真实设备，调试困难 | 本地仿真，秒级反馈 | 10x+ |
| **测试覆盖** | 手动测试，场景有限 | 5大核心场景自动化 | 5x+ |
| **CI集成** | 无自动化门禁 | 完整CI流水线 | 从无到有 |
| **维护成本** | AI API依赖，费用高 | 零运营成本 | 节省100% |
| **调试效率** | 缺乏可视化，定位困难 | 多格式报告，快速定位 | 5x+ |

---

# 2. 系统架构

## 2.1 完整系统架构

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
│  ├── Standard Test Fixtures (5 core scenarios)                 │
│  └── Test Case Generator (AI-driven, optional)                 │
│        ↓                                                       │
│  Simulation Execution Layer                                     │
│  ├── SimulationRunner (complete wrapper) ✅                    │
│  ├── MockVisionService (path-aware) ✅                         │
│  ├── MockActionExecutor (enhanced recording) ✅                │
│  └── InMemoryTracer (unified visualization) ✅                 │
│        ↓                                                       │
│  Assertion & Comparison Layer                                  │
│  ├── TraceAsserter (automated comparison)                      │
│  ├── Intent Validator                                          │
│  └── Event Sequence Matcher                                    │
│        ↓                                                       │
│  Reporting & Analysis Layer                                    │
│  ├── Test Report Generator (JSON + HTML)                       │
│  ├── AI-Failure Analyzer (optional)                            │
│  └── Visualization Exporter                                    │
│        ↓                                                       │
│  CI/CD Integration Layer                                       │
│  ├── Pipeline Integration                                      │
│  ├── Quality Gates                                              │
│  └── Artifact Archiving                                        │
└─────────────────────────────────────────────────────────────────┘
```

## 2.2 核心组件关系

```
┌─────────────────────────────────────────────────────────────┐
│  Component Interaction Flow                                   │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  Test Case (test_case.json)                                 │
│        ↓                                                     │
│  SimulationTestRunner.load_test_case()                      │
│        ↓                                                     │
│  SimulationRunner(virtual_pages, plan)                      │
│        ↓                                                     │
│  ├── MockVisionService.set_context()                        │
│  ├── MockActionExecutor.set_context()                       │
│  └── GraphTraversalEngine.run()                             │
│        ↓                                                     │
│  TraversalResult + Trace + Actions                           │
│        ↓                                                     │
│  TraceAsserter.assert_trace_matches_expected()              │
│        ↓                                                     │
│  SimulationReportGenerator.generate_report()                │
│        ↓                                                     │
│  JSON/HTML Report → CI/CD → Developer Notification           │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## 2.3 数据流架构

```
┌─────────────────────────────────────────────────────────────┐
│  Data Flow Architecture                                      │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  Input Layer                                                  │
│  ├── Virtual Pages (pages_all.json)                         │
│  ├── Traversal Plan (plan_all.json)                         │
│  └── Test Expectations (test_case.json)                     │
│        ↓                                                     │
│  Processing Layer                                            │
│  ├── PageAnalyzer.analyze_page()                            │
│  ├── MockVisionService.analyze_screenshot()                 │
│  ├── MockActionExecutor._record_operation()                  │
│  └── GraphTraversalEngine.run()                             │
│        ↓                                                     │
│  Output Layer                                                │
│  ├── Trace Data (List[TraceStep])                           │
│  ├── Action History (List[OperationRecord])                 │
│  ├── Visited Tree (Dict[NodeId, NodeInfo])                  │
│  └── Statistics (Dict[str, Metric])                         │
│        ↓                                                     │
│  Validation Layer                                            │
│  ├── TraceAsserter (Expected vs Actual)                     │
│  ├── AssertionResult (Pass/Fail + Details)                   │
│  └── TestReport (Structured Output)                         │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

# 3. 四阶段实施策略

## 3.1 实施阶段概览

| 阶段 | 重点 | 交付物 | 验证标准 | 预计工期 |
|-------|-------|--------------|------------|----------|
| **Phase 1** | MockVisionService增强 | 路径映射、上下文处理、PageAnalyzer | 单元测试通过 | 2-3天 |
| **Phase 2** | SimulationRunner完善 | 完整包装、结果提取、增强操作记录 | 集成测试通过 | 3-4天 |
| **Phase 3** | 测试框架建立 | 标准fixtures、E2E测试、辅助函数 | 完整测试套件通过 | 3-4天 |
| **Phase 4** | CI生态集成 | 断言引擎、报告生成、CI流水线 | 自动化验证通过 | 2-3天 |

## 3.2 Phase 1: MockVisionService增强

### 3.2.1 目标
修复MockVisionService以正确跟踪当前遍历路径，并基于路径返回正确的PageAnalysis数据。

### 3.2.2 核心设计

#### PageAnalyzer组件
```python
class PageAnalyzer:
    """
    分析和结构化页面数据用于仿真测试。
    
    充当真实视觉分析管道的仿真等价物，
    将原始页面数据转换为正确的PageAnalysis格式。
    """
    
    def analyze_page(self, path: str) -> Dict[str, Any]:
        """分析页面并返回结构化的PageAnalysis"""
        
    def _structure_page_analysis(self, path: str, raw_data: Dict) -> Dict[str, Any]:
        """将原始页面数据转换为正确的PageAnalysis结构"""
        
    def _process_elements(self, elements: List[Dict]) -> List[Dict]:
        """处理UI元素，添加类型和元数据"""
        
    def _infer_page_type(self, page_data: Dict) -> str:
        """从内容推断页面类型"""
        
    def _infer_action_hint(self, element: Dict) -> str:
        """推断元素的建议操作"""
```

#### 增强的MockVisionService
```python
class MockVisionService:
    """具有PageAnalyzer集成的增强Mock视觉服务"""
    
    def __init__(self, virtual_pages: Dict[str, Dict[str, Any]]):
        self.virtual_pages = virtual_pages
        self._analyzer = PageAnalyzer(virtual_pages)
        self._path_mapping = self._build_path_mapping(virtual_pages)
        self._call_count = 0
        self._current_context = None
        
    def set_context(self, context: Any) -> None:
        """设置遍历上下文用于路径解析"""
        self._current_context = context
        # 支持TraversalContext和InMemoryTracer
        if hasattr(context, 'current_path'):
            self._path_getter = lambda: "/".join(context.current_path)
        elif hasattr(context, 'visited_tree'):
            self._path_getter = lambda: self._infer_path_from_tracer(context)
            
    def analyze_screenshot(self) -> Dict[str, Any]:
        """使用PageAnalyzer分析当前屏幕截图"""
        self._call_count += 1
        current_path = self._get_current_path()
        return self._analyzer.analyze_page(current_path)
```

### 3.2.3 成功标准
- ✅ PageAnalyzer正确结构化原始页面数据为PageAnalysis格式
- ✅ PageAnalyzer正确推断页面类型和操作提示
- ✅ MockVisionService集成PageAnalyzer进行所有分析调用
- ✅ 路径映射和上下文解析正确工作
- ✅ 单元测试覆盖PageAnalyzer逻辑（页面类型推断、元素处理等）
- ✅ 集成测试覆盖MockVisionService + PageAnalyzer交互

---

## 3.3 Phase 2: SimulationRunner完善

### 3.3.1 目标
完成SimulationRunner包装器，正确封装GraphTraversalEngine，处理上下文设置，并提供清晰的结果提取用于测试。

### 3.3.2 核心设计

#### 完整的SimulationRunner
```python
class SimulationRunner:
    """
    V6离线测试的完整仿真运行器。
    
    正确包装GraphTraversalEngine及所有Mock组件，
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
        
        # 创建Mock组件及正确初始化
        self.vision = MockVisionService(virtual_pages)
        self.action = MockActionExecutor(
            simulate_delay=self.config.get("action_delay", 0.0)
        )
        self.tracer = InMemoryTracer()
        
        # 创建GraphTraversalEngine及所有依赖
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
```

#### 增强的MockActionExecutor
```python
class MockActionExecutor:
    """
    具有综合操作记录的Mock操作执行器。
    
    记录详细的操作信息用于测试断言、调试和追踪验证。
    """
    
    def _record_operation(
        self,
        action_type: str,
        target_info: Optional[Dict[str, Any]] = None,
        result: bool = True,
        metadata: Optional[Dict[str, Any]] = None,
    ) -> None:
        """记录综合的操作信息"""
        operation_record = {
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
```

### 3.3.3 成功标准
- ✅ SimulationRunner正确包装GraphTraversalEngine及所有依赖
- ✅ 上下文集成确保MockVisionService接收正确路径信息
- ✅ 完整结果提取提供断言所需的所有数据
- ✅ 可视化方法正确支持调试
- ✅ 辅助方法提供便捷的测试工具
- ✅ 错误处理提供有意义的失败信息
- ✅ 集成测试验证端到端功能

---

## 3.4 Phase 3: 测试框架建立

### 3.4.1 目标
创建包含标准fixtures、AI友好测试用例格式和辅助函数的综合测试框架，使仿真测试简单可靠。

### 3.4.2 AI友好测试用例格式

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

### 3.4.3 标准测试Fixtures

#### 5个核心测试用例

| 测试ID | 描述 | 验证目标 | 预期结果 |
|-------|------|----------|----------|
| `e2e_all_traversal` | 全菜单遍历 | DFS顺序、恢复操作 | completion_reason="completed" |
| `e2e_target_found` | 搜索"版本号"停止 | 目标搜索终止 | completion_reason="target_found" |
| `e2e_static_path` | 静态路径执行 | 路径序列正确 | 路径严格匹配 |
| `e2e_popup_handling` | 弹窗处理 | 弹窗被关闭继续 | 弹窗处理成功 |
| `e2e_auto_escape` | 自动同级切换 | 不弹栈继续遍历 | 自动切换成功 |

### 3.4.4 测试辅助函数

```python
class SimulationTestRunner:
    """AI友好测试格式的辅助类"""
    
    @staticmethod
    def run_simulation_test(
        test_case_path: Path,
        config: Optional[Dict] = None,
    ) -> Tuple[SimulationResult, Dict[str, Any]]:
        """从测试用例目录执行完整仿真测试"""
        
    @staticmethod
    def run_all_tests(
        tests_dir: Path,
        pattern: str = "*/test_case.json",
        config: Optional[Dict] = None,
    ) -> List[Tuple[str, SimulationResult, Dict]]:
        """执行目录中的所有仿真测试"""
```

### 3.4.5 成功标准
- ✅ 5个标准测试fixtures使用AI友好格式实现
- ✅ 辅助函数使测试执行简单一致
- ✅ 自动化断言引擎验证所有预期行为
- ✅ Pytest集成支持标准测试工作流
- ✅ 测试文件结构支持轻松添加新测试用例
- ✅ 所有核心测试场景（DFS、目标搜索、静态路径、弹窗、自动逃逸）覆盖

---

## 3.5 Phase 4: CI生态集成

### 3.5.1 目标
创建生产就绪的CI/CD集成、自动化报告生成和开发友好工具，完成仿真测试生态系统。

### 3.5.2 自动化报告生成器

```python
class SimulationReportGenerator:
    """为CI/CD集成生成综合测试报告"""
    
    @staticmethod
    def generate_report(
        test_id: str,
        result: SimulationResult,
        test_case: Dict[str, Any],
        assertion_result: AssertionResult,
    ) -> TestReport:
        """生成综合测试报告"""
        
    @staticmethod
    def export_json_report(report: TestReport) -> str:
        """导出JSON格式报告供CI消费"""
        
    @staticmethod
    def export_html_report(report: TestReport) -> str:
        """导出HTML格式报告供人工审阅"""
```

### 3.5.3 CLI工具

```bash
# simtest CLI工具（可通过pip安装）
simtest run <test_path> --output report.json
simtest suite <tests_dir> --report combined_report.json
simtest show <report_path>  # 显示测试报告
```

### 3.5.4 CI/CD集成

#### GitHub Actions工作流
```yaml
name: Simulation Tests
on: [push, pull_request]

jobs:
  simulation-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Run simulation tests
        run: |
          pip install -e .
          simtest suite tests/simulation/fixtures --report ci_report.json
      - name: Upload reports
        uses: actions/upload-artifact@v3
        with:
          name: simulation-reports
          path: reports/
```

### 3.5.5 开发工作流集成

#### Pre-commit钩子
```bash
#!/bin/bash
# .git/hooks/pre-commit

echo "🧪 Running simulation tests..."
python -m simtest suite tests/simulation/fixtures

if [ $? -ne 0 ]; then
    echo "❌ Simulation tests failed. Commit aborted."
    exit 1
fi

echo "✅ Simulation tests passed."
```

### 3.5.6 成功标准
- ✅ 自动化报告生成支持JSON和HTML格式
- ✅ CLI工具提供便捷的测试执行和报告
- ✅ CI/CD集成确保质量门禁
- ✅ 开发工作流无缝集成仿真测试
- ✅ 测试报告支持机器消费和人工审阅
- ✅ 失败测试提供可操作的调试信息

---

# 4. 详细技术规格

## 4.1 数据结构规格

### TestReport结构
```python
@dataclass
class TestReport:
    test_id: str
    timestamp: str
    result: str  # "passed" or "failed"
    duration_seconds: float
    details: TestReportDetails
    trace_summary: str
    visualization: Dict[str, str]
    error_info: Optional[Dict[str, Any]] = None

@dataclass
class TestReportDetails:
    completion_reason: str
    total_steps: int
    key_events_matched: int
    missing_events: List[str]
    extra_events: List[str]
    violations: List[str]
    statistics: Dict[str, Any]
```

### SimulationResult结构
```python
@dataclass
class SimulationResult:
    engine_result: Dict[str, Any]
    trace: List[Dict[str, Any]]
    executed_actions: List[Dict[str, Any]]
    visited_tree: Dict[str, Dict[str, Any]]
    elapsed_seconds: float
    completion_reason: str
    statistics: Dict[str, Any]
```

### OperationRecord结构
```python
class OperationRecord(TypedDict):
    action_type: str
    timestamp: float
    result: str  # "success" or "failed"
    current_node: Optional[str]
    current_path: List[str]
    page_context: Dict[str, Any]
    target_info: Dict[str, Any]
    metadata: Dict[str, Any]
    node_stack: List[str]
```

## 4.2 接口规格

### TraceAsserter接口
```python
class TraceAsserter:
    @staticmethod
    def step_to_nl(step: Dict[str, Any]) -> str:
        """将追踪步骤转换为自然语言描述"""
        
    @staticmethod
    def is_subsequence(expected: List[str], actual: List[str]) -> bool:
        """检查期望序列是否为实际序列的子序列"""
        
    @staticmethod
    def assert_trace_matches_expected(
        trace: List[Dict[str, Any]],
        expected: Dict[str, Any],
    ) -> AssertionResult:
        """断言追踪与预期行为匹配"""
```

### SimulationRunner接口
```python
class SimulationRunner:
    def __init__(
        self,
        virtual_pages: Dict[str, Dict[str, Any]],
        plan: TraversalPlan,
        config: Optional[Dict[str, Any]] = None,
    ):
        """初始化仿真运行器"""
        
    def run(self) -> SimulationResult:
        """执行仿真并返回结果"""
        
    def render_tree(self, max_depth: Optional[int] = None) -> str:
        """渲染遍历树为ASCII"""
        
    def render_mermaid(self) -> str:
        """渲染状态图为Mermaid"""
        
    def export_trace(self, format: str = "jsonl") -> str:
        """导出指定格式的追踪"""
```

## 4.3 文件组织结构

```
tests/simulation/
├── fixtures/
│   ├── e2e_all_traversal/
│   │   ├── test_case.json
│   │   ├── plan_all.json
│   │   └── pages_all.json
│   ├── e2e_target_found/
│   ├── e2e_static_path/
│   ├── e2e_popup_handling/
│   └── e2e_auto_escape/
├── helpers/
│   ├── test_runner.py
│   ├── assertions.py
│   └── report_generator.py
└── test_simulation_framework.py

src/simulation/
├── __init__.py
├── mock_vision.py          # MockVisionService + PageAnalyzer
├── mock_action.py          # Enhanced MockActionExecutor
├── runner.py               # Complete SimulationRunner
└── visualizer.py           # InMemoryTracer

cli/
└── simtest.py              # CLI tool

scripts/
├── check_simulation_results.py
└── verify_simulation_setup.py

.github/workflows/
└── simulation-tests.yml
```

---

# 5. 质量保证与验收

## 5.1 测试策略

### 单元测试
- **PageAnalyzer逻辑** - 页面类型推断、元素处理、操作提示
- **MockVisionService** - 路径解析、上下文集成
- **MockActionExecutor** - 操作记录、上下文管理
- **TraceAsserter** - 序列匹配、断言逻辑

### 集成测试
- **SimulationRunner** - 端到端仿真执行
- **组件交互** - Vision + Action + Tracer集成
- **结果提取** - 完整的数据流验证

### 端到端测试
- **5个核心场景** - 完整的仿真测试执行
- **报告生成** - JSON/HTML输出验证
- **CI集成** - 自动化流水线测试

## 5.2 验收标准

### Phase 1验收
- [ ] PageAnalyzer所有方法测试覆盖率>80%
- [ ] MockVisionService路径映射测试通过
- [ ] 上下文集成测试通过
- [ ] 性能测试：单次分析<10ms

### Phase 2验收
- [ ] SimulationRunner集成测试通过
- [ ] 所有结果提取方法验证
- [ ] MockActionExecutor记录完整性
- [ ] 端到端测试执行成功

### Phase 3验收
- [ ] 5个标准fixtures测试全部通过
- [ ] TraceAsserter断言准确性100%
- [ ] Pytest集成无问题
- [ ] 新测试用例添加时间<30分钟

### Phase 4验收
- [ ] CI流水线成功运行
- [ ] 报告生成测试通过
- [ ] CLI工具功能完整
- [ ] 开发工作流集成验证

## 5.3 性能指标

| 指标 | 目标值 | 测量方法 |
|------|--------|----------|
| **仿真执行时间** | <5秒/测试 | 自动化测试报告 |
| **报告生成时间** | <1秒/报告 | 性能测试 |
| **内存占用** | <100MB/测试 | 资源监控 |
| **CI总执行时间** | <10分钟/全量 | CI流水线日志 |
| **测试稳定性** | 100%通过率 | 连续100次运行 |

---

# 6. 开发计划

## 6.1 里程碑时间表

| 里程碑 | 目标日期 | 交付物 | 负责人 |
|-------|---------|--------|--------|
| **M1: Phase 1完成** | D+3 | PageAnalyzer + MockVisionService | Backend Team |
| **M2: Phase 2完成** | D+7 | SimulationRunner + MockActionExecutor | Backend Team |
| **M3: Phase 3完成** | D+11 | 测试框架 + 5个fixtures | QA Team |
| **M4: Phase 4完成** | D+14 | CI集成 + 报告系统 | DevOps Team |
| **M5: 生产就绪** | D+18 | 完整文档 + 培训 | 全体团队 |

## 6.2 风险管理

| 风险 | 影响 | 概率 | 缓解措施 |
|------|------|------|----------|
| **GraphTraversalEngine接口变更** | 高 | 中 | 版本隔离、适配器模式 |
| **性能不达标** | 中 | 低 | 早期性能测试、优化预留 |
| **CI集成问题** | 中 | 中 | 本地验证、渐进式部署 |
| **测试用例维护困难** | 低 | 中 | 文档化、模板化工具 |

## 6.3 资源需求

### 人力资源
- **后端开发** ×2：核心组件实现
- **QA工程师** ×1：测试框架和fixtures
- **DevOps工程师** ×1：CI集成和工具
- **技术文档** ×1：文档和培训材料

### 技术资源
- **开发环境**：Python 3.10+, 虚拟化测试环境
- **CI环境**：GitHub Actions或同等平台
- **监控工具**：测试执行监控、性能分析

---

# 7. 未来增强

## 7.1 AI能力增强（可选）

### AI测试生成器
- **功能**：从自然语言描述生成测试用例
- **触发**：`simtest generate --description "测试设置页面的WiFi切换功能"`
- **输出**：完整的test_case.json + fixtures

### AI失败分析器
- **功能**：分析失败的测试并提供建议
- **触发**：CI失败时的自动调用
- **输出**：失败原因分析 + 修复建议

## 7.2 扩展功能

### 性能分析
- **执行时间分析**：识别慢速测试
- **资源使用监控**：内存和CPU使用
- **瓶颈识别**：性能优化建议

### 测试管理
- **测试依赖管理**：处理测试间依赖
- **并行执行**：加速大规模测试
- **结果趋势分析**：历史数据对比

### 可视化增强
- **交互式报告**：Web界面查看报告
- **实时监控**：测试执行实时状态
- **对比分析**：不同版本测试结果对比

---

# 8. 附录

## 8.1 术语表

| 术语 | 定义 |
|------|------|
| **仿真测试** | 无需真实设备的自动化测试方法 |
| **AI友好格式** | 既人可读又机器可解析的结构化数据 |
| **意图槽位** | 描述遍历意图的结构化元数据 |
| **关键事件** | 测试执行中必须按顺序出现的事件 |
| **完成原因** | 遍历结束的状态或原因 |
| **违禁词** | 测试结果中不应出现的词汇 |

## 8.2 参考文档

- [Uni-Claw项目架构总览](ARCHITECTURE.md)
- [PRD V5.2 - 两步视觉管道](PRD_V5_2-flattened-screen.md)
- [状态机设计文档](state_machine_design.md)
- [测试指南](TEST_GUIDE.md)

## 8.3 变更历史

| 版本 | 日期 | 变更内容 | 负责人 |
|------|------|----------|--------|
| V6.0 | 2026-06-03 | 初始版本 - 完整仿真测试系统 | System Design |

---

**文档状态**: ✅ 设计完成，待实施
**下一步行动**: 创建实施计划并开始Phase 1开发