# Testing Workflows

本文档描述 uni-claw 项目的常用测试工作流程和脚本使用方法。

## E2E 仿真测试

### 快速运行

```bash
# 推荐使用：无特殊字符，最大兼容性
python scripts/visualization/show_html_report.py

# 运行仿真测试
python -m pytest tests/simulation/test_runner.py -v
```

### 可用的分析脚本

| 脚本 | 位置 | 用途 |
|------|------|------|
| `analyze_nodes_visited.py` | `scripts/analysis/` | 分析节点访问情况 |
| `show_html_report.py` | `scripts/visualization/` | 显示 HTML 报告 |
| `show_test_details.py` | `scripts/visualization/` | 显示测试详情 |
| `test_mock_fix.py` | `scripts/verify/` | 验证 Mock 组件修复 |
| `check_trace_structure.py` | `scripts/verify/` | 检查 trace 数据结构 |
| `test_simulation_runner.py` | `scripts/debug/` | 调试 SimulationRunner |

### 测试报告

- 测试报告生成在 `tests/reports/` 目录
- HTML 报告使用 UTF-8 编码，包含：
  - 摘要仪表板
  - 访问树可视化
  - 操作对比表
  - 状态转换跟踪

### 故障排除

```bash
# 检查 Python 环境
python --version

# 验证测试框架
python -m pytest tests/simulation/ --collect-only

# 运行特定测试
python -m pytest tests/simulation/test_runner.py::TestSimulationRunner::test_dfs_traversal -v
```

## 单元测试 vs 集成测试

### 单元测试
位于 `src/` 各模块目录，测试单个组件：
```bash
# 运行所有单元测试
pytest src/ -v

# 运行特定模块的单元测试
pytest src/ai/ -v
pytest src/traversal/ -v
```

### 集成测试
位于 `tests/` 目录，测试多组件协作：
```bash
# 运行所有集成测试
pytest tests/ -v

# 运行仿真测试
pytest tests/simulation/ -v

# 运行性能测试
pytest tests/performance/ -v
```

## 性能测试

```bash
# 运行 AI 性能基准测试
pytest tests/performance/test_ai_performance.py -v
```

## CI/CD 集成

测试配置支持 CI/CD 集成：
- GitHub Actions
- Pre-commit hooks
- 自动化测试管道

详见 `docs/SIMULATION_TESTING_GUIDE.md` 获取完整测试系统文档。
