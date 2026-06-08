# Uni-Claw 测试标准 (Testing Standards)

**版本**: V6.3
**最后更新**: 2026-06-08
**适用范围**: 所有模块开发

---

## 📋 概述

本文档定义 Uni-Claw 项目的测试标准和质量要求，确保所有模块代码质量和测试覆盖率达到统一标准。

---

## 🎯 覆盖率要求

### 最低覆盖率标准

| 模块类型 | 最低覆盖率 | 关键路径覆盖率 | 分支覆盖率 |
|----------|------------|----------------|------------|
| 核心模块 (src/*) | 80% | 95% | 70% |
| 工具模块 (src/utils/) | 85% | 95% | 75% |
| 测试工具 (tests/) | 70% | 90% | 60% |
| 示例/演示 (examples/) | 60% | 80% | 50% |

### 覆盖率计算

```bash
# 检查单个模块覆盖率
python -m pytest tests/{module}/ --cov=src.{module} --cov-report=term-missing

# 检查所有模块覆盖率
python -m pytest tests/ --cov=src --cov-report=term-missing
```

### 覆盖率豁免条件

只有在以下情况下可以申请覆盖率豁免：
- 纯数据类/DTO类（无业务逻辑）
- 平台特定代码（Android/iOS特定实现）
- 已标记为 deprecated 的代码

---

## 🧪 测试类型要求

### 1. 正常路径测试 (Happy Path)

**目的**: 验证功能在正常输入下的正确行为

**要求**:
- 每个公开方法至少有1个正常路径测试
- 验证输入 → 输出转换的正确性
- 验证与外部依赖的集成

**示例**:
```python
def test_valid_input_produces_expected_output():
    """测试正常输入产生预期输出"""
    processor = DataProcessor()
    result = processor.process({"key": "value"})
    assert result.status == "success"
    assert result.data["key"] == "value"
```

### 2. 边界条件测试 (Boundary Testing)

**目的**: 验证功能在边界值下的行为

**要求**:
- 测试输入边界值（最小值、最大值、空值）
- 测试输出边界值
- 测试资源限制（内存、时间、并发）

**示例**:
```python
def test_empty_input_handling():
    """测试空输入处理"""
    processor = DataProcessor()
    result = processor.process({})
    assert result.status == "empty"
    assert result.data == {}

def test_maximum_capacity():
    """测试最大容量处理"""
    cache = LRUCache(max_size=100)
    for i in range(150):
        cache.set(f"key_{i}", f"value_{i}")
    assert len(cache.cache) == 100  # 不应超过最大值
```

### 3. 异常处理测试 (Error Handling)

**目的**: 验证功能对异常情况的处理

**要求**:
- 测试无效输入（None、错误类型、超出范围）
- 测试错误条件（文件不存在、网络失败）
- 验证异常处理（不崩溃、有错误消息）

**示例**:
```python
def test_invalid_input_raises_error():
    """测试无效输入抛出错误"""
    processor = DataProcessor()
    with pytest.raises(ValueError, match="Invalid input type"):
        processor.process(None)

def test_file_not_found_handling():
    """测试文件不存在处理"""
    loader = ConfigLoader()
    result = loader.load("/nonexistent/file.json")
    assert result.success == False
    assert result.error.contains("File not found")
```

---

## ✅ 测试断言标准

### 每个测试用例的断言要求

**最少断言数**: 3个
**断言类型多样性**:
- 至少1个相等性断言 (`assert ==`, `assert ==`)
- 至少1个布尔断言 (`assert True/False`, `assert in`)
- 至少1个异常断言（如果适用）

### 断言质量要求

**❌ 避免**:
```python
# 过于宽泛
assert True  # 没有实际验证
assert result is not None  # 验证不足
assert len(result) > 0  # 没有验证具体内容
```

**✅ 推荐**:
```python
# 具体验证
assert result.status == "success"
assert result.data["key"] == "expected_value"
assert len(result.items) == 5  # 验证具体数量

# 验证副作用和状态变化
assert cache.size == 1
assert cache.get("key") == "value"

# 边界条件验证
assert 0 <= result.percent <= 100
```

### 断言组织原则

```python
def test_complete_workflow():
    """测试完整工作流"""
    # Arrange: 准备测试数据
    input_data = {"key": "value"}
    processor = DataProcessor()

    # Act: 执行被测试的操作
    result = processor.process(input_data)

    # Assert: 验证结果
    # 1. 验证基本状态
    assert result.success == True

    # 2. 验证输出数据
    assert result.data["processed"] == True
    assert result.data["key"] == "value"

    # 3. 验证副作用（如果适用）
    assert processor.processed_count == 1
    assert "key" in processor.cache
```

---

## 📝 测试命名规范

### 文件命名

| 类型 | 命名格式 | 示例 |
|------|----------|------|
| 测试文件 | `test_{module}.py` | `test_graph_engine.py` |
| 测试类 | `Test{ClassName}` | `TestGraphEngine` |
| 测试方法 | `test_{scenario}_{expected_result}` | `test_invalid_input_raises_error` |

### 测试方法命名模式

```python
# 功能测试
test_{feature}_works()
test_{feature}_{condition}()

# 边界测试
test_{feature}_with_empty_input()
test_{feature}_with_maximum_value()

# 异常测试
test_{feature}_raises_{error_type}()
test_{feature}_handles_{error_condition}()

# 集成测试
test_{module}_integration_with_{dependency}()
```

---

## 🎭 测试数据管理

### Fixtures (测试夹具)

**位置**: `tests/fixtures/` 或 `tests/{module}/fixtures/`
**命名**: `{feature}_fixtures.py`

**示例**:
```python
# tests/fixtures/graph_fixtures.py
import pytest

@pytest.fixture
def simple_graph():
    """提供简单的测试图结构"""
    return Graph(nodes=[1, 2, 3], edges=[(1, 2), (2, 3)])

@pytest.fixture
def complex_graph():
    """提供复杂的测试图结构"""
    # ... 复杂设置
    return graph
```

### Mock 对象使用原则

**何时使用 Mock**:
- 外部服务依赖（HTTP API、数据库）
- 慢速操作（文件 I/O、网络请求）
- 不确定的行为（随机数、时间戳）
- 隔离测试（避免测试间相互影响）

**避免过度 Mock**:
- 不要 Mock 被测试的类本身
- 不要 Mock 简单的数据结构
- 优先使用真实对象和测试替身（Test Doubles）

**示例**:
```python
# ✅ 好的 Mock 使用
def test_with_http_mock():
    """测试使用 HTTP Mock"""
    with patch('requests.get') as mock_get:
        mock_get.return_value = Mock(status_code=200, text='{"data": "value"}')
        result = fetch_data("https://api.example.com")
        assert result["data"] == "value"

# ❌ 过度 Mock
def test_with_over_mock():
    """过度 Mock 导致测试无意义"""
    with patch.object(MyClass, 'method1', return_value=1):
        with patch.object(MyClass, 'method2', return_value=2):
            obj = MyClass()
            assert obj.method1() + obj.method2() == 3  # 只测试了 Mock 返回值
```

---

## 🚀 性能测试要求

### 性能基准

| 操作类型 | 最大耗时 | 并发要求 |
|----------|----------|----------|
| 单元测试执行 | < 5秒/文件 | 不适用 |
| 模块测试套件 | < 30秒 | 串行 |
| 集成测试套件 | < 2分钟 | 可并行 |
| 完整测试套件 | < 10分钟 | 并行(4 workers) |

### 性能测试示例

```python
import time

def test_large_dataset_performance():
    """测试大数据集处理性能"""
    processor = DataProcessor()
    large_dataset = generate_test_data(size=10000)

    start = time.time()
    result = processor.process(large_dataset)
    duration = time.time() - start

    assert result.success == True
    assert duration < 1.0  # 应在1秒内完成
```

---

## 🔗 集成测试要求

### 与其他模块的集成

每个模块应有集成测试验证：
- 与依赖模块的接口契约
- 数据格式兼容性
- 错误传播机制

### API 契约测试

**输入验证**:
- 验证必需参数
- 验证参数类型
- 验证参数范围

**输出格式**:
- 验证返回类型
- 验证必需字段
- 验证数据格式

**错误处理**:
- 验证错误类型
- 验证错误消息
- 验证错误传播

---

## ✅ 质量门禁

### 任务完成的阻止条件

❌ **以下情况必须阻止任务完成**:
- 测试失败数 > 0
- 测试错误数 > 0
- 覆盖率下降 > 5%
- 关键功能测试失败
- 存在跳过的测试（skip）

### 警告但不阻止的条件

⚠️ **以下情况给出警告但允许完成**:
- 覆盖率轻微下降 (< 2%)
- 新增代码缺少测试
- 测试命名不规范
- 测试文档不完整

### 质量检查清单

在提交代码前，确认：

- [ ] 所有测试通过 (`pytest tests/ -v`)
- [ ] 覆盖率达到要求 (`pytest --cov`)
- [ ] 没有跳过的测试
- [ ] 没有标记为 xfail 的测试（除非有充分理由）
- [ ] 测试命名遵循规范
- [ ] 断言具有足够的特异性
- [ ] 边界条件已测试
- [ ] 异常情况已测试

---

## 🔧 测试执行标准

### 运行测试

```bash
# 运行所有测试
python -m pytest tests/ -v

# 运行特定模块测试
python -m pytest tests/{module}/ -v

# 运行特定测试
python -m pytest tests/{module}/test_file.py::TestClass::test_method -v

# 生成覆盖率报告
python -m pytest tests/ --cov=src --cov-report=html --cov-report=term-missing
```

### 使用 module-test skill

```bash
# 运行模块测试（带智能失败处理）
python .claude/skills/module-test/test_runner.py --module {module_name}

# 检查覆盖率
python .claude/skills/module-test/coverage_checker.py --threshold 80
```

---

## 📚 相关文档

- **模块设计文档**: `docs/architecture/modules/`
- **测试指南**: `docs/testing/README.md`
- **快速参考**: `docs/testing/QUICK_REFERENCE.md`
- **module-test skill**: 使用 `/skill module-test`
- **validation-documentation skill**: 使用 `/skill validation-documentation`
- **PRD**: `docs/prd/PRD_V6_2_test_architecture_standardization_prd.md`

---

**维护说明**: 当项目测试要求变更时，及时更新本文档。

**版本历史**:
- V6.3 (2026-06-08): 更新版本号，修正相关文档路径
- V6.0 (2026-06-06): 初始版本
