# Graph 模块单元测试

本目录包含 Graph 模块的所有单元测试。

## 运行测试

### 快速运行

```bash
# 从项目根目录运行
python src/graph/run_tests.py

# 从 graph 目录运行
cd src/graph && python run_tests.py
```

### 详细输出

```bash
python src/graph/run_tests.py -v
```

### 包含覆盖率

```bash
python src/graph/run_tests.py --coverage
```

### 自定义输出路径

```bash
python src/graph/run_tests.py -o /path/to/report.json
```

## 测试报告

测试运行后会生成两个报告文件：

1. **JSON 报告** (`test_report.json`) - AI 可识别的机器可读格式
2. **HTML 报告** (`test_report.html`) - 可视化测试结果

### JSON 报告格式

```json
{
  "timestamp": "2026-06-03T...",
  "module": "graph",
  "status": "passed",
  "summary": {
    "total": 15,
    "passed": 15,
    "failed": 0,
    "skipped": 0,
    "errors": 0,
    "duration": 2.34
  },
  "tests": [...],
  "failures": [],
  "errors": [],
  "coverage": {...}
}
```

## 测试文件

| 文件 | 说明 |
|------|------|
| `test_node.py` | TraversalNode 和相关数据类测试 |
| `test_template.py` | 模板系统测试 |
| `test_graph_models.py` | 图模型测试 |
| `test_graph_nodes.py` | 节点测试 |

## CI 集成

在 CI/CD 中运行测试：

```yaml
- name: Run Graph module tests
  run: python src/graph/run_tests.py

- name: Upload test results
  uses: actions/upload-artifact@v3
  with:
    name: graph-test-results
    path: src/graph/test/test_report.*
```

## 预提交钩子

添加到 `.git/hooks/pre-commit`:

```bash
#!/bin/bash
python src/graph/run_tests.py || exit 1
```
