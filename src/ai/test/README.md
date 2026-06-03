# AI 模块单元测试

本目录包含 AI 模块的所有单元测试。

## 运行测试

### 快速运行

```bash
# 从项目根目录运行
python src/ai/run_tests.py

# 从 ai 目录运行
cd src/ai && python run_tests.py
```

### 详细输出

```bash
python src/ai/run_tests.py -v
```

### 包含覆盖率

```bash
python src/ai/run_tests.py --coverage
```

### 只测试特定模块

```bash
# 只测试 providers
python src/ai/run_tests.py --module providers

# 只测试 prompts
python src/ai/run_tests.py --module prompts

# 只测试 trace
python src/ai/run_tests.py --module trace
```

## 测试报告

测试运行后会生成两个报告文件：

1. **JSON 报告** (`test_report.json`) - AI 可识别的机器可读格式
2. **HTML 报告** (`test_report.html`) - 可视化测试结果

## 测试文件

| 目录/文件 | 说明 |
|-----------|------|
| `test_*.py` | 主测试文件 |
| `providers/` | Provider 抽象层测试 |
| `prompts/` | Prompt 管理系统测试 |
| `trace/` | Trace 集成测试 |
| `integration/` | 集成测试 |
| `fixtures/` | 测试 fixtures 和 mock 数据 |

## 测试组织

- **Unit Tests**: 测试单个组件（providers, prompts, trace）
- **Integration Tests**: 测试组件间交互
- **Fixtures**: 共享测试数据和 mock 对象

## CI 集成

在 CI/CD 中运行测试：

```yaml
- name: Run AI module tests
  run: python src/ai/run_tests.py

- name: Upload test results
  uses: actions/upload-artifact@v3
  with:
    name: ai-test-results
    path: src/ai/test/test_report.*
```
