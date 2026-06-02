# 测试资产文档

本文档说明如何使用Mock测试采集和trace数据，形成测试资产。

## 📁 目录结构

```
tests/ai/assets/
├── traces/                        # Trace数据资产
│   ├── overview.json             # 总览文件
│   ├── vision_analysis_standard.json  # 视觉分析场景
│   ├── instruction_parse_standard.json # 指令解析场景
│   ├── page_verification.json     # 页面验证场景
│   ├── decision_making.json      # 决策场景
│   ├── safety_screening.json     # 安全筛选场景
│   └── error_handling.json       # 错误处理场景
├── mock_responses/                # Mock响应数据
│   ├── deepseek_responses.json   # DeepSeek响应
│   ├── claude_responses.json     # Claude响应
│   └── mimo_responses.json       # MiMo响应
└── README.md                      # 本文档
```

## 🎯 测试资产用途

### 1. 开发调试
- **提供标准响应数据** - 用于单元测试和集成测试
- **模拟各种场景** - 包括正常、异常、边界情况
- **加速测试执行** - 无需真实API调用，测试速度快

### 2. 性能基准
- **建立性能基线** - 记录各种操作的标准延迟
- **监控性能变化** - 对比实际性能与基线
- **识别性能退化** - 及时发现性能问题

### 3. 监控验证
- **验证追踪系统** - 确保trace数据正确采集
- **验证指标计算** - 确保metrics统计准确
- **验证告警机制** - 测试告警阈值和触发条件

### 4. 文档示例
- **提供真实示例** - 展示API调用和响应格式
- **帮助理解系统** - 通过真实数据理解系统行为
- **支持培训学习** - 新成员可以通过资产了解系统

## 🚀 使用方法

### 采集测试资产

运行资产采集脚本：

```bash
# 采集标准场景的trace数据
python tests/ai/test_asset_collection.py

# 分析已采集的资产
python tests/ai/test_asset_collection.py analyze
```

### 场景覆盖

脚本会采集以下标准场景：

1. **视觉分析** (`vision_analysis_standard`)
   - 标准设置页面分析
   - WiFi设置页面分析
   - 不同页面类型识别

2. **指令解析** (`instruction_parse_standard`)
   - 简单指令解析
   - 复杂指令解析
   - 多步骤指令

3. **页面验证** (`page_verification`)
   - 页面类型验证
   - 结构验证
   - 元素验证

4. **决策能力** (`decision_making`)
   - 目标导向决策
   - 上下文感知决策
   - 多选项决策

5. **安全筛选** (`safety_screening`)
   - 正常操作筛选
   - 危险操作识别
   - 风险等级评估

6. **错误处理** (`error_handling`)
   - 低置信度情况
   - 空输入处理
   - 异常响应处理

### 资产数据格式

每个资产包含以下信息：

```json
{
  "asset_id": "vision_analysis_standard_analyze_visual_0",
  "scenario": "vision_analysis_standard",
  "capability": "analyze_visual",
  "provider_id": "claude",
  "mode": "vision",

  "input_data": {
    "image_size": "1080x1920",
    "image_format": "PNG",
    "app_context": "Settings main page"
  },

  "output_data": {
    "current_path": ["Home", "Settings"],
    "page_type": "menu_list",
    "elements": [...],
    "confidence": 0.92
  },

  "latency_ms": 2500.0,
  "input_tokens": 1100,
  "output_tokens": 350,
  "total_tokens": 1450,

  "trace_context": {
    "span_id": "analyze_visual_0_1234567890",
    "operation": "unibrain.analyze_visual",
    "tags": {...}
  },

  "custom_context": {
    "traversal_session": "session_001",
    "current_depth": 2,
    "target_goal": "configure_wifi"
  },

  "created_at": "2026-06-02T17:30:00",
  "tags": ["vision", "settings", "menu_list"],
  "description": "分析设置主页的视觉结构"
}
```

## 📊 资产统计

### 总览文件结构

`overview.json` 包含全局统计信息：

```json
{
  "total_assets": 15,
  "scenarios": {
    "vision_analysis_standard": 2,
    "instruction_parse_standard": 2,
    "page_verification": 1,
    "decision_making": 1,
    "safety_screening": 2,
    "error_handling": 2
  },
  "created_at": "2026-06-02T17:30:00",
  "assets_summary": {
    "capability_stats": {...},
    "provider_stats": {...},
    "mode_stats": {...},
    "averages": {...}
  }
}
```

### 统计指标

- **capability_stats**: 按能力统计调用次数和资源消耗
- **provider_stats**: 按Provider统计使用情况
- **mode_stats**: 按模式统计使用情况
- **averages**: 平均延迟、平均Token消耗

## 🔧 在测试中使用资产

### 1. 单元测试

```python
import json
from pathlib import Path

def test_analyze_visual():
    """使用采集的资产进行测试"""
    # 加载资产
    asset_file = Path("tests/ai/assets/traces/vision_analysis_standard.json")
    with open(asset_file) as f:
        assets = json.load(f)

    # 使用第一个资产进行测试
    asset = assets[0]

    # Mock Provider返回资产中的output_data
    mock_provider.return_value = asset['output_data']

    # 执行测试
    result = await unibrain.analyze_visual(
        image_data=b"fake_image_data",
        context=asset['custom_context']
    )

    # 验证结果
    assert result.page_type == asset['output_data']['page_type']
    assert result.current_path == asset['output_data']['current_path']
```

### 2. 性能基准测试

```python
def test_performance_baseline():
    """验证性能符合基线"""
    asset_file = Path("tests/ai/assets/traces/overview.json")
    with open(asset_file) as f:
        overview = json.load(f)

    baseline_latency = overview['assets_summary']['averages']['avg_latency_ms']

    # 测试当前性能
    start_time = time.time()
    result = await unibrain.analyze_visual(image_data)
    actual_latency = (time.time() - start_time) * 1000

    # 允许20%的性能波动
    assert actual_latency <= baseline_latency * 1.2
```

### 3. 监控验证

```python
def test_trace_collection():
    """验证trace数据正确采集"""
    # 加载资产作为期望值
    asset_file = Path("tests/ai/assets/traces/vision_analysis_standard.json")
    with open(asset_file) as f:
        expected_asset = json.load(f)[0]

    # 执行操作
    result = await unibrain.analyze_visual(image_data)

    # 验证trace数据
    assert result.trace_context['capability'] == expected_asset['capability']
    assert result.trace_context['provider_id'] == expected_asset['provider_id']
    assert result.metrics['latency_ms'] > 0
    assert result.metrics['total_tokens'] > 0
```

## 📈 资产维护

### 更新资产

当系统更新时，需要更新测试资产：

```bash
# 1. 重新采集资产
python tests/ai/test_asset_collection.py

# 2. 分析差异
python tests/ai/test_asset_collection.py analyze

# 3. 更新相关测试
# 手动检查和更新使用资产的测试用例
```

### 扩展场景

添加新的测试场景：

```python
# 在 test_asset_collection.py 中添加新场景
async def collect_custom_scenario():
    collector = MockTraceCollector()

    custom_calls = [
        {
            'capability': 'your_capability',
            'provider_id': 'claude',
            'mode': 'vision',
            'latency_ms': 1000.0,
            'input_tokens': 500,
            'output_tokens': 200,
            # ... 更多配置
        }
    ]

    await collector.collect_scenario('custom_scenario', custom_calls)
    collector.save_assets()
```

## 🎨 最佳实践

### 1. 资产命名
- 使用描述性的场景名称
- 包含能力类型和场景特征
- 保持命名一致性

### 2. 数据完整性
- 确保所有必需字段都有值
- 提供详细的custom_context
- 添加清晰的description

### 3. 场景覆盖
- 覆盖正常流程
- 覆盖异常情况
- 覆盖边界条件

### 4. 性能真实性
- 延迟数据应反映真实性能
- Token消耗应准确
- 包含成功和失败场景

## 🔄 资产生命周期

1. **创建**: 通过采集脚本生成
2. **验证**: 检查数据完整性和真实性
3. **使用**: 在测试和监控中使用
4. **维护**: 定期更新和扩展
5. **归档**: 保留历史版本用于对比

## 📞 支持和问题

如有问题或建议，请联系：
- 测试团队
- AI团队
- DevOps团队

---

**最后更新**: 2026-06-02
**维护者**: Uni-Clow 测试团队
