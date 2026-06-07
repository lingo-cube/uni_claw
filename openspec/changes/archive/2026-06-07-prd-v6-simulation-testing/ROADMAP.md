# PRD V6.0 仿真测试自动化系统 - 分阶段实施路线图

## 概述

本文档记录仿真测试自动化系统的分阶段实施策略，确保项目按优先级和依赖关系逐步推进，实现 CI 优先的生产级测试基础设施。

## 四阶段概览

```
┌─────────────────────────────────────────────────────────────────┐
│              Phase 1: MockVisionService 增强                     │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ • PageAnalyzer 组件开发                                  │   │
│  │ • 路径感知的 MockVisionService                           │   │
│  │ • 上下文集成（TraversalContext + InMemoryTracer）       │   │
│  │ • 单元测试 + 集成测试                                     │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│               Phase 2: SimulationRunner 完善                       │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ • 完整的 SimulationRunner 包装器                         │   │
│  │ • 增强的 MockActionExecutor 操作记录                     │   │
│  │ • 上下文设置与结果提取                                   │   │
│  │ • 可视化方法（ASCII 树、Mermaid 图）                      │   │
│  │ • 集成测试                                               │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│               Phase 3: 测试框架建立                                │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ • AI 友好测试用例格式（JSON）                             │   │
│  │ • 5 个核心标准 fixtures                                 │   │
│  │ • TraceAsserter 断言引擎                                │   │
│  │ • SimulationTestRunner 辅助类                            │   │
│  │ • Pytest 集成                                            │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│               Phase 4: CI 生态集成                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ • SimulationReportGenerator 报告生成                     │   │
│  │ • simtest CLI 工具                                       │   │
│  │ • GitHub Actions 工作流                                 │   │
│  │ • Pre-commit 钩子                                        │   │
│  │ • 文档与培训材料                                         │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

## Phase 1: MockVisionService 增强（2-3 天）

### 目标
修复 MockVisionService 以正确跟踪当前遍历路径，并基于路径返回正确的 PageAnalysis 数据。

### 实施范围

#### PageAnalyzer 组件
```python
class PageAnalyzer:
    def analyze_page(path: str) -> Dict[str, Any]
    def _structure_page_analysis(path: str, raw_data: Dict) -> Dict[str, Any]
    def _process_elements(elements: List[Dict]) -> List[Dict]
    def _infer_page_type(page_data: Dict) -> str
    def _infer_action_hint(element: Dict) -> str
```

**核心功能**：
- ✅ 结构化原始页面数据为 PageAnalysis 格式
- ✅ 智能页面类型推断（settings、list、web、unknown）
- ✅ UI 元素处理和元数据添加
- ✅ 操作提示推断（click、scroll、adjust、view）
- ✅ 性能优化：内存缓存（目标 <10ms/分析）

#### 增强的 MockVisionService
```python
class MockVisionService:
    def set_context(context: Any) -> None
    def analyze_screenshot() -> Dict[str, Any]
    def _get_current_path() -> str
    def _infer_path_from_tracer(tracer: InMemoryTracer) -> str
    def get_call_count() -> int
    def reset() -> None
```

**核心功能**：
- ✅ 集成 PageAnalyzer 进行所有分析调用
- ✅ 支持 TraversalContext 和 InMemoryTracer 上下文
- ✅ 路径映射和正确解析
- ✅ 调用计数和状态重置
- ✅ 错误处理（PageNotFoundError）

### 验收标准
- [ ] PageAnalyzer 所有方法测试覆盖率 >80%
- [ ] MockVisionService 路径映射测试通过
- [ ] 上下文集成测试通过（支持两种上下文类型）
- [ ] 性能测试：单次分析 <10ms
- [ ] 集成测试覆盖所有交互场景

### 风险与缓解
| 风险 | 缓解措施 |
|------|----------|
| 页面类型推断不准确 | 提供配置化规则 + 默认回退 |
| 路径解析复杂度高 | 简化映射逻辑 + 充分测试 |
| 性能不达标 | 缓存优化 + 提前性能测试 |

## Phase 2: SimulationRunner 完善（3-4 天）

### 目标
完成 SimulationRunner 包装器，正确封装 GraphTraversalEngine，处理上下文设置，并提供清晰的结果提取用于测试。

### 实施范围

#### 增强的 MockActionExecutor
```python
class MockActionExecutor:
    def _record_operation(action_type, target_info, result, metadata)
    def set_context(context: Any) -> None
    def set_page_context(page_context: Dict) -> None
    def get_history() -> List[OperationRecord]
    def get_operations_by_type(action_type: str) -> List[OperationRecord]
    def push_node(node_id: str) -> None
    def pop_node() -> Optional[str]
```

**核心功能**：
- ✅ 综合操作记录（上下文、目标、元数据、栈信息）
- ✅ 支持所有 UI 操作类型（click、scroll、input_text、go_back）
- ✅ 上下文管理和页面上下文跟踪
- ✅ 节点栈管理用于调试嵌套调用
- ✅ 历史查询和统计功能

#### 完整的 SimulationRunner
```python
class SimulationRunner:
    def __init__(virtual_pages, plan, config)
    def run() -> SimulationResult
    def _setup_context_integration() -> None
    def _extract_results(engine_result) -> StructuredResult
    def _extract_visited_tree() -> Dict[str, Dict[str, Any]]
    def _extract_completion_reason() -> str
    def _compute_statistics() -> Dict[str, Any]
    def _handle_error(error: Exception) -> SimulationResult
```

**可视化方法**：
```python
def render_tree(max_depth: Optional[int]) -> str
def render_mermaid() -> str
def export_trace(format: str) -> str
```

**核心功能**：
- ✅ 正确包装 GraphTraversalEngine 及所有依赖
- ✅ 上下文集成确保所有组件接收正确路径信息
- ✅ 完整结果提取（trace、actions、visited_tree、statistics）
- ✅ 多格式可视化输出（ASCII 树、Mermaid 图、JSONL、HTML）
- ✅ 错误处理和部分结果恢复

### 验收标准
- [ ] SimulationRunner 集成测试通过
- [ ] 所有结果提取方法验证
- [ ] MockActionExecutor 记录完整性验证
- [ ] 可视化方法输出正确性验证
- [ ] 端到端测试执行成功
- [ ] 错误处理测试通过

### 风险与缓解
| 风险 | 缓解措施 |
|------|----------|
| GraphTraversalEngine 接口变更 | 版本隔离 + 适配器模式 |
| 上下文集成复杂 | 详细文档 + 示例代码 |
| 结果提取不完整 | 全面测试 + 用例驱动 |

## Phase 3: 测试框架建立（3-4 天）

### 目标
创建包含标准 fixtures、AI 友好测试用例格式和辅助函数的综合测试框架，使仿真测试简单可靠。

### 实施范围

#### AI 友好测试用例格式
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
    "key_events": ["进入设置页", "点击'显示'", ...],
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

**核心特性**：
- ✅ 结构化 JSON 格式，既人可读又机器可解析
- ✅ 意图槽位（intent_slots）描述遍历意图
- ✅ 明确的预期结果（expected）和断言（assertions）
- ✅ 支持 fixtures 引用（plan、pages）
- ✅ 灵活的验证规则

#### 5 个核心测试 Fixtures

| 测试 ID | 描述 | 验证目标 | 预期结果 |
|---------|------|----------|----------|
| `e2e_all_traversal` | 全菜单遍历 | DFS 顺序、恢复操作 | completion_reason="completed" |
| `e2e_target_found` | 搜索"版本号"停止 | 目标搜索终止 | completion_reason="target_found" |
| `e2e_static_path` | 静态路径执行 | 路径序列正确 | 路径严格匹配 |
| `e2e_popup_handling` | 弹窗处理 | 弹窗被关闭继续 | 弹窗处理成功 |
| `e2e_auto_escape` | 自动同级切换 | 不弹栈继续遍历 | 自动切换成功 |

#### TraceAsserter 断言引擎
```python
class TraceAsserter:
    @staticmethod
    def step_to_nl(step: Dict[str, Any]) -> str
    @staticmethod
    def is_subsequence(expected: List[str], actual: List[str]) -> bool
    @staticmethod
    def assert_trace_matches_expected(trace, expected) -> AssertionResult
```

**核心功能**：
- ✅ 追踪步骤到自然语言转换
- ✅ 序列匹配算法（子序列检查）
- ✅ 关键事件验证
- ✅ 违禁词检测
- ✅ 步骤数量验证
- ✅ 完成原因验证

#### SimulationTestRunner 辅助类
```python
class SimulationTestRunner:
    @staticmethod
    def run_simulation_test(test_case_path: Path, config: Optional[Dict])
    def run_all_tests(tests_dir: Path, pattern: str, config: Optional[Dict])
```

**核心功能**：
- ✅ 从测试用例目录执行完整仿真测试
- ✅ 批量测试执行和报告生成
- ✅ 配置管理和错误处理
- ✅ 结果解析和验证

### 验收标准
- [ ] 5 个标准 fixtures 测试全部通过
- [ ] TraceAsserter 断言准确性 100%
- [ ] Pytest 集成无问题
- [ ] 新测试用例添加时间 <30 分钟
- [ ] 所有核心测试场景覆盖

### 风险与缓解
| 风险 | 缓解措施 |
|------|----------|
| 测试用例格式复杂 | 提供模板 + 示例 + 验证工具 |
| 断言逻辑准确性 | 充分测试 + 边缘案例覆盖 |
| Fixtures 维护困难 | 文档化 + 模块化设计 |

## Phase 4: CI 生态集成（2-3 天）

### 目标
创建生产就绪的 CI/CD 集成、自动化报告生成和开发友好工具，完成仿真测试生态系统。

### 实施范围

#### SimulationReportGenerator 报告生成器
```python
class SimulationReportGenerator:
    @staticmethod
    def generate_report(test_id, result, test_case, assertion_result)
    def export_json_report(report: TestReport) -> str
    def export_html_report(report: TestReport) -> str
```

**核心功能**：
- ✅ 综合测试报告生成（TestReport 结构）
- ✅ JSON 格式输出（机器可读，CI 消费）
- ✅ HTML 格式输出（人工审阅，可视化）
- ✅ 包含详细信息、统计、追踪摘要
- ✅ 错误信息和调试辅助

#### simtest CLI 工具
```bash
# 运行单个测试
simtest run <test_path> --output report.json

# 运行测试套件
simtest suite <tests_dir> --report combined_report.json

# 显示测试报告
simtest show <report_path>
```

**核心功能**：
- ✅ 命令行接口（pip 安装）
- ✅ 单个测试执行
- ✅ 测试套件批量执行
- ✅ 报告查看和导出
- ✅ 丰富的命令行选项（--output、--format、--verbose）

#### CI/CD 集成

**GitHub Actions 工作流**：
```yaml
name: Simulation Tests
on: [push, pull_request]
jobs:
  simulation-tests:
    runs-on: ubuntu-latest
    steps:
      - checkout
      - setup Python
      - install dependencies
      - run simulation tests
      - upload reports
      - comment PR with results
```

**Pre-commit 钩子**：
```bash
#!/bin/bash
# .git/hooks/pre-commit
echo "🧪 Running simulation tests..."
python -m simtest suite tests/simulation/fixtures --fast
if [ $? -ne 0 ]; then
    echo "❌ Tests failed. Commit aborted."
    exit 1
fi
echo "✅ Tests passed."
```

**核心功能**：
- ✅ GitHub Actions 自动化流水线
- ✅ 多 Python 版本矩阵测试
- [ ] 测试报告上传和归档
- [ ] PR 自动评论测试结果
- [ ] Pre-commit 质量门禁
- [ ] 分支保护规则集成

### 验收标准
- [ ] CI 流水线成功运行
- [ ] 报告生成测试通过（JSON + HTML）
- [ ] CLI 工具功能完整
- [ ] 开发工作流集成验证
- [ ] 测试报告支持机器消费和人工审阅
- [ ] 失败测试提供可操作的调试信息

### 风险与缓解
| 风险 | 缓解措施 |
|------|----------|
| CI 环境差异 | 容器化 + 本地验证 |
| 报告格式兼容性 | 标准化 + 版本控制 |
| CLI 工具兼容性 | 跨平台测试 + 依赖隔离 |

## 阶段间依赖关系

```
Phase 1 建立 Mock 基础：
├── PageAnalyzer（页面分析核心）
├── MockVisionService（路径感知视觉服务）
└── 单元测试 + 集成测试

Phase 2 完善 Simulation 层：
├── MockActionExecutor（操作记录增强）
├── SimulationRunner（完整仿真包装）
├── 可视化方法（调试支持）
└── 依赖 Phase 1 的 MockVisionService

Phase 3 建立 Test 框架：
├── AI 友好测试格式
├── 5 个核心 Fixtures
├── TraceAsserter（断言引擎）
├── SimulationTestRunner（辅助类）
└── 依赖 Phase 2 的完整仿真功能

Phase 4 集成 CI 生态：
├── SimulationReportGenerator（报告生成）
├── simtest CLI（工具）
├── GitHub Actions（CI 流水线）
├── Pre-commit 钩子（开发集成）
└── 依赖 Phase 3 的测试框架
```

## 实施优先级

| 阶段 | 优先级 | 预估工期 | 前置依赖 | 关键路径 |
|------|--------|----------|----------|----------|
| Phase 1 | P0 | 2-3 天 | 无 | ✅ 是 |
| Phase 2 | P0 | 3-4 天 | Phase 1 | ✅ 是 |
| Phase 3 | P1 | 3-4 天 | Phase 2 | ✅ 是 |
| Phase 4 | P1 | 2-3 天 | Phase 3 | ✅ 是 |
| **总计** | - | **10-14 天** | - | - |

## 总体验收标准

### Phase 1 验收
- [ ] PageAnalyzer 所有方法测试覆盖率 >80%
- [ ] MockVisionService 路径映射测试通过
- [ ] 上下文集成测试通过
- [ ] 性能测试：单次分析 <10ms

### Phase 2 验收
- [ ] SimulationRunner 集成测试通过
- [ ] 所有结果提取方法验证
- [ ] MockActionExecutor 记录完整性
- [ ] 端到端测试执行成功

### Phase 3 验收
- [ ] 5 个标准 fixtures 测试全部通过
- [ ] TraceAsserter 断言准确性 100%
- [ ] Pytest 集成无问题
- [ ] 新测试用例添加时间 <30 分钟

### Phase 4 验收
- [ ] CI 流水线成功运行
- [ ] 报告生成测试通过
- [ ] CLI 工具功能完整
- [ ] 开发工作流集成验证

## 风险管理

### 高优先级风险

#### 1. GraphTraversalEngine 接口变更
- **影响**：高 - 可能导致仿真包装失效
- **概率**：中 - V6 正在开发，接口可能变化
- **缓解措施**：
  - 使用版本隔离，明确依赖的 Engine 版本
  - 采用适配器模式降低接口变更影响
  - 与 V6 开发团队同步接口变更信息
  - 预留 1 天缓冲时间用于适配

#### 2. 性能指标不达标
- **影响**：中 - 可能影响 CI 使用体验
- **概率**：低 - 设计已考虑性能优化
- **缓解措施**：
  - 早期性能测试（Phase 1 开始）
  - 预留优化时间（每个 Phase 预留 0.5 天）
  - 缓存策略优化
  - 必要时调整性能目标

#### 3. CI 集成问题
- **影响**：中 - 可能影响自动化流程
- **概率**：中 - 跨平台兼容性问题
- **缓解措施**：
  - 本地环境充分验证
  - 渐进式部署（先手动，后自动）
  - 容器化 CI 环境
  - 预留 1 天调试时间

### 中优先级风险

#### 4. 测试用例维护困难
- **影响**：低 - 可能影响长期使用
- **概率**：中 - 格式复杂度
- **缓解措施**：
  - 提供详细文档和示例
  - 模板化工具（test 生成器）
  - 验证工具（schema 验证）
  - 图形化编辑工具（未来增强）

#### 5. 跨平台兼容性
- **影响**：中 - 可能影响开发者体验
- **概率**：中 - Windows/Linux/macOS 差异
- **缓解措施**：
  - 矩阵测试覆盖多平台
  - 抽象平台相关代码
  - 容器化统一环境
  - 预留 1 天平台适配时间

## 成功指标

### 定量指标

| 指标 | 当前状态 | 目标状态 | 改善幅度 | 测量方法 |
|------|----------|----------|----------|----------|
| **测试可靠性** | 仿真器破损 | 完整仿真 | 100% | 自动化测试通过率 |
| **开发速度** | 真实设备测试 | 本地仿真 | 10x+ | 测试执行时间对比 |
| **测试覆盖** | 手动测试 | 5 大场景 | 5x+ | 自动化场景数量 |
| **CI 集成** | 无自动化门禁 | 完整流水线 | 从无到有 | CI 流水线状态 |
| **维护成本** | AI API 依赖 | 零运营成本 | 节省 100% | 运营费用对比 |
| **调试效率** | 缺乏可视化 | 多格式报告 | 5x+ | 问题定位时间 |

### 性能指标

| 指标 | 目标值 | 测量方法 | 验证阶段 |
|------|--------|----------|----------|
| **仿真执行时间** | <5 秒/测试 | 自动化测试报告 | Phase 2 |
| **报告生成时间** | <1 秒/报告 | 性能测试 | Phase 4 |
| **内存占用** | <100MB/测试 | 资源监控 | Phase 2 |
| **CI 总执行时间** | <10 分钟/全量 | CI 流水线日志 | Phase 4 |
| **测试稳定性** | 100% 通过率 | 连续 100 次运行 | Phase 4 |

### 定性指标

#### 开发者体验
- ✅ 测试用例创建直观（<30 分钟）
- ✅ 失败信息清晰可操作
- ✅ CLI 工具便捷易用
- ✅ 文档完整准确

#### 系统质量
- ✅ 代码可维护性高
- ✅ 架构可扩展性好
- ✅ 错误处理完善
- ✅ 性能稳定可靠

#### 团队协作
- ✅ CI/CD 无缝集成
- ✅ 质量门禁明确
- ✅ 问题定位快速
- ✅ 知识传承完整

## 更新记录

- **2026-06-03**: 创建路线图，明确四阶段实施策略
- **2026-06-03**: 基于 PRD V6.0 定义完整仿真测试系统
- **2026-06-03**: 设置各阶段验收标准和风险管理策略