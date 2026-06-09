# V7.0-SimScroll 滚动列表模拟测试

测试套件用于 V7.0-SimScroll 滚动列表模拟功能。

## 测试结构

```
tests/simulation/scroll/
├── __init__.py                 # 测试包初始化
├── conftest.py                 # Pytest fixtures和配置
├── test_models.py              # 数据模型单元测试 (18 tests)
├── test_scrollable_vision.py   # 视觉服务集成测试 (22 tests)
└── test_scenarios.py          # PRD场景测试 (12 tests)

fixtures/scroll/
├── wifi_list.json             # WiFi列表数据
├── empty_list.json            # 空列表数据
├── duplicate_elements.json    # 重复元素数据
└── nested_list.json           # 嵌套列表数据
```

## 运行测试

### 运行所有测试

```bash
pytest tests/simulation/scroll/ -v
```

### 运行特定测试

```bash
# 模型测试
pytest tests/simulation/scroll/test_models.py -v

# 服务集成测试
pytest tests/simulation/scroll/test_scrollable_vision.py -v

# 场景测试
pytest tests/simulation/scroll/test_scenarios.py -v
```

### 运行特定场景

```bash
# 场景1: 正常多屏滚动
pytest tests/simulation/scroll/test_scenarios.py::TestBasicScenarios::test_scenario1_normal_multi_screen_scroll -v

# 场景4: 空列表处理
pytest tests/simulation/scroll/test_scenarios.py::TestEdgeScenarios::test_scenario4_empty_list_handling -v

# 所有边界场景
pytest tests/simulation/scroll/test_scenarios.py::TestEdgeScenarios -v
```

### 带覆盖率报告

```bash
pytest tests/simulation/scroll/ --cov=src/simulation/scroll --cov-report=term-missing
```

## 测试覆盖

### 单元测试 (18 tests)

- `TestScrollSegment`: ScrollSegment模型测试
- `TestScrollState`: ScrollState模型测试
- `TestScrollAction`: ScrollAction模型测试
- `TestScrollModelsIntegration`: 模型集成测试

### 集成测试 (22 tests)

- `TestScrollableMockVisionService`: 基础功能测试
- `TestScrollFailureInjection`: 故障注入测试
- `TestScrollAccumulationMode`: 累加模式测试
- `TestElementDeduplication`: 元素去重测试
- `TestScrollHistory`: 历史记录测试
- `TestEdgeCases`: 边界情况测试

### 场景测试 (12 tests)

- `TestBasicScenarios`: PRD场景1-2（基础场景）
- `TestEdgeScenarios`: PRD场景3-5（边界场景）
- `TestFaultScenarios`: PRD场景6-8（故障场景）
- `TestPerformanceScenarios`: PRD场景9-10（性能场景）
- `TestAllScenariosIntegration`: 综合测试

## PRD场景覆盖

| 场景 | 描述 | 状态 |
|------|------|------|
| 场景1 | 正常多屏滚动 | ✅ 已覆盖 |
| 场景2 | 滚动到底检测 | ✅ 已覆盖 |
| 场景3 | 跳跃检测与回滚 | ✅ 已覆盖 |
| 场景4 | 空列表处理 | ✅ 已覆盖 |
| 场景5 | 单屏列表 | ✅ 已覆盖 |
| 场景6 | 滚动卡顿模拟 | ✅ 已覆盖 |
| 场景7 | 滚动无响应模拟 | ✅ 已覆盖 |
| 场景8 | 重复元素去重 | ✅ 已覆盖 |
| 场景9 | 大量元素列表 | ✅ 已覆盖 |
| 场景10 | 深层嵌套列表 | ✅ 已覆盖 |

## 测试标记

测试使用以下标记进行分类：

- `@pytest.mark.scroll`: 滚动相关测试
- `@pytest.mark.scenario`: 场景测试
- `@pytest.mark.unit`: 单元测试
- `@pytest.mark.integration`: 集成测试

## 相关文档

- [PRD_V7_0_SimScroll.md](../../../docs/prd/PRD_V7_0_SimScroll.md) - 产品需求文档
- [DESIGN_V7_0_SimScroll.md](../../../docs/prd/DESIGN_V7_0_SimScroll.md) - 设计文档
- [V7_0_SimScroll_TEST_REPORT.md](../../../docs/testing/V7_0_SimScroll_TEST_REPORT.md) - 测试生成报告
