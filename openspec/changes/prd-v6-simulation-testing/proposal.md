# Proposal: PRD V6.0 Simulation Testing System

## Why

当前 V5.x 系列实现了 AI 集成和视觉管道优化，但仿真测试系统存在严重缺陷：

1. **仿真器破损** - SimulationRunner 只是占位符，未真正包装 GraphTraversalEngine
2. **上下文缺失** - MockVisionService 无法获取当前路径，无法返回正确页面分析
3. **文档缺失** - 无明确测试指南和组件使用说明
4. **CI 集成缺失** - 无自动化测试流水线
5. **测试用例不规范** - 缺少标准测试 fixtures 和验证方法

这导致：
- 测试不可靠，必须连接真实设备
- 开发迭代慢，调试困难
- 质量门禁缺失，CI/CD 集成困难
- 维护成本高，依赖昂贵的 AI API

V6.0 仿真测试系统的目标是构建一个 **CI 优先、生产级、零成本** 的仿真测试自动化系统。

## What Changes

### 核心理念：CI 优先架构

**传统自动化优先** - 用成熟的 CI/CD 工具而非不稳定的 AI
- 确定性的基于规则的断言，避免 AI 幻觉
- 快速反馈，无外部 API 调用，秒级测试执行
- 零成本运营，无 AI API 费用，频繁运行无负担
- 渐进增强，AI 作为可选增强，非必需组件

### 新增核心组件

#### 1. 增强的视觉分析系统
- **PageAnalyzer** - 结构化页面数据，正确转换为 PageAnalysis 格式
- **增强的 MockVisionService** - 路径感知，支持上下文集成

#### 2. 完整的仿真执行系统
- **完整的 SimulationRunner** - 正确包装 GraphTraversalEngine 及所有依赖
- **增强的 MockActionExecutor** - 综合操作记录，支持测试断言

#### 3. AI 友好的测试框架
- **AI 友好测试用例格式** - JSON 格式，既人可读又机器可解析
- **5 个核心标准 fixtures** - 覆盖 DFS、目标搜索、静态路径、弹窗、自动逃逸场景
- **SimulationTestRunner** - 简化测试执行的辅助类

#### 4. 自动化断言与验证
- **TraceAsserter** - 自动化追踪比较引擎
- **Intent Validator** - 意图槽位验证
- **Event Sequence Matcher** - 事件序列匹配

#### 5. CI/CD 集成生态系统
- **SimulationReportGenerator** - 生成 JSON/HTML 格式报告
- **simtest CLI 工具** - 便捷的测试执行和报告查看
- **GitHub Actions 工作流** - 完整的 CI 流水线集成
- **Pre-commit 钩子** - 开发工作流集成

### 新增文件结构

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

## Capabilities

### New Capabilities

- **page-analyzer**: 智能页面分析，正确结构化 UI 元素数据
- **path-aware-mock-vision**: 路径感知的 Mock 视觉服务
- **complete-simulation-runner**: 完整的仿真运行器，正确包装 GraphTraversalEngine
- **enhanced-action-recording**: 增强的操作记录，支持详细断言
- **ai-friendly-test-format**: AI 友好的 JSON 测试用例格式
- **standard-test-fixtures**: 5 个核心测试场景的标准 fixtures
- **automated-assertion-engine**: 自动化断言引擎，比较预期和实际追踪
- **simulation-report-generator**: 多格式报告生成（JSON/HTML）
- **simtest-cli**: 命令行工具，便捷的测试执行
- **ci-cd-integration**: 完整的 CI/CD 流水线集成

### Modified Capabilities

- **mock-vision-service**: 增强为路径感知，集成 PageAnalyzer
- **mock-action-executor**: 增强操作记录和上下文管理
- **simulation-runner**: 完整实现，正确包装所有组件

## Impact

### 预期收益

| 收益类型 | 当前状态 | 目标状态 | 改善幅度 |
|---------|----------|----------|----------|
| **测试可靠性** | 仿真器破损，路径映射错误 | 完整仿真，正确路径映射 | 100% |
| **开发速度** | 需真实设备，调试困难 | 本地仿真，秒级反馈 | 10x+ |
| **测试覆盖** | 手动测试，场景有限 | 5 大核心场景自动化 | 5x+ |
| **CI 集成** | 无自动化门禁 | 完整 CI 流水线 | 从无到有 |
| **维护成本** | AI API 依赖，费用高 | 零运营成本 | 节省 100% |
| **调试效率** | 缺乏可视化，定位困难 | 多格式报告，快速定位 | 5x+ |

### 性能指标

| 指标 | 目标值 | 测量方法 |
|------|--------|----------|
| **仿真执行时间** | <5 秒/测试 | 自动化测试报告 |
| **报告生成时间** | <1 秒/报告 | 性能测试 |
| **内存占用** | <100MB/测试 | 资源监控 |
| **CI 总执行时间** | <10 分钟/全量 | CI 流水线日志 |
| **测试稳定性** | 100% 通过率 | 连续 100 次运行 |

### 依赖

无新增外部依赖。使用现有 Python 标准库、pytest 和 JSON 处理。

### 测试影响

- 新增仿真测试框架，不影响现有测试
- 新增 5 个核心端到端测试场景
- 新增 CI/CD 集成测试
- 所有现有测试应继续通过

### 实施策略

四阶段实施，总计 10-14 天：

1. **Phase 1 (2-3 天)**: MockVisionService 增强 - PageAnalyzer 和路径映射
2. **Phase 2 (3-4 天)**: SimulationRunner 完善 - 完整包装和结果提取
3. **Phase 3 (3-4 天)**: 测试框架建立 - 标准 fixtures 和断言引擎
4. **Phase 4 (2-3 天)**: CI 生态集成 - 报告生成和 CI/CD 流水线

## 开发者体验

### 测试用例示例

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
    "total_steps_max": 30
  }
}
```

### CLI 使用示例

```bash
# 运行单个测试
simtest run tests/simulation/fixtures/e2e_all_traversal

# 运行测试套件
simtest suite tests/simulation/fixtures --report combined_report.json

# 查看测试报告
simtest show reports/latest_report.json
```

### Pytest 集成

```python
def test_all_traversal_simulation():
    """测试全菜单遍历场景"""
    result = run_simulation_test(
        test_case_path="tests/simulation/fixtures/e2e_all_traversal/test_case.json"
    )
    
    assert result.completion_reason == "completed"
    assert result.statistics["total_steps"] >= 15
    assert "遍历完成" in result.key_events_matched
```

## 未来增强

### AI 能力增强（可选）
- AI 测试生成器 - 从自然语言生成测试用例
- AI 失败分析器 - 分析失败测试并提供建议

### 扩展功能
- 性能分析 - 识别慢速测试和瓶颈
- 测试管理 - 并行执行和结果趋势分析
- 可视化增强 - 交互式报告和实时监控