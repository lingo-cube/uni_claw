# TODO: PRD V5.2 两步视觉管道 - 未实现功能

**Change ID**: `prd-v5-2-flattened-screen`
**Created**: 2026-06-02
**Status**: Design Phase

---

## 概述

本文档记录了 PRD V5.2 两步视觉管道实现中暂不实现的功能。这些功能可能因为以下原因被推迟：

1. **优先级较低** - 对核心功能影响不大
2. **依赖条件不成熟** - 需要更多数据或基础设施
3. **复杂度较高** - 需要更多时间实现
4. **初期不需要** - 可在后续版本中添加

---

## 1. 高优先级 TODO

### 1.1 感知哈希算法优化

**当前状态**: 使用简单的 MD5 哈希

**目标**: 使用感知哈希（perceptual hash）算法，提高缓存鲁棒性

**原因**:
- MD5 对微小变化敏感，导致缓存命中率低
- 截图的微小变化（如时间戳、加载状态）不应导致缓存失效

**实现方案**:
```python
# 使用 imagehash 库
from imagehash import phash
from PIL import Image

def generate_perceptual_hash(image_data: bytes) -> str:
    """生成感知哈希"""
    image = Image.open(BytesIO(image_data))
    return str(phash(image))
```

**预估工作量**: 0.5 天

**建议实施时机**: P6 阶段完成后

---

### 1.2 Few-shot Prompt 示例

**当前状态**: Prompt 中仅包含格式说明

**目标**: 在 Prompt 中添加 Few-shot 示例，提高 AI 输出质量

**原因**:
- 初期测试发现 AI 对某些场景理解不够准确
- Few-shot 可以显著提升输出质量

**实现方案**:
```python
FEW_SHOT_EXAMPLES = """
## 示例 1：分栏布局
输入：[截图]
输出：
{
  "elements": [
    {"id": 0, "text": "WiFi", "type_hint": "clickable_text", ...},
    ...
  ],
  "screen_hints": {
    "layout_type": "split_pane",
    ...
  }
}
"""
```

**预估工作量**: 1 天

**建议实施时机**: P6 阶段优化时

---

## 2. 中优先级 TODO

### 2.1 DualVisionService

**当前状态**: 不实现

**目标**: 同时运行新旧两种方案，实时对比结果

**原因**:
- 初期可通过离线测试完成对比
- 实时双运行会增加延迟和成本

**实现方案**:
```python
class DualVisionService:
    """双模式视觉服务，同时运行新旧方案"""

    def analyze_screenshot(self, image_data: bytes) -> DualResult:
        """同时运行新旧方案"""
        # 并行运行
        with ThreadPoolExecutor(max_workers=2) as executor:
            legacy_future = executor.submit(
                self.legacy_service.analyze_screenshot, image_data
            )
            flattened_future = executor.submit(
                self.flattened_service.analyze_screenshot, image_data
            )

        return DualResult(
            legacy=legacy_future.result(),
            flattened=flattened_future.result(),
        )
```

**预估工作量**: 1 天

**建议实施时机**: 需要生产环境实时监控时

---

### 2.2 Redis 分布式缓存

**当前状态**: 使用内存缓存

**目标**: 使用 Redis 实现分布式缓存

**原因**:
- 初期单机运行，内存缓存足够
- 多实例部署时需要共享缓存

**实现方案**:
```python
class RedisScreenCache(ScreenCache):
    """Redis 实现"""

    def __init__(self, redis_client: Redis, ttl: int = 300):
        self.redis = redis_client
        self.ttl = ttl

    def get(self, image_data: bytes) -> Optional[FlattenedScreen]:
        key = self._generate_key(image_data)
        data = self.redis.get(key)
        if data:
            return FlattenedScreen.from_dict(json.loads(data))
        return None

    def set(self, image_data: bytes, value: FlattenedScreen) -> None:
        key = self._generate_key(image_data)
        self.redis.setex(
            key,
            self.ttl,
            json.dumps(value.to_dict()),
        )
```

**预估工作量**: 1 天

**建议实施时机**: 需要多实例部署时

---

### 2.3 指标持久化

**当前状态**: 指标仅在内存中收集

**目标**: 将指标持久化到数据库或文件

**原因**:
- 初期可通过日志查看
- 长期监控需要持久化存储

**实现方案**:
```python
class DatabaseMetricsCollector(VisionMetricsCollector):
    """数据库持久化指标收集器"""

    def record(self, metrics: VisionMetrics) -> None:
        # 写入数据库
        self.db.insert('vision_metrics', metrics.to_dict())
```

**预估工作量**: 1.5 天

**建议实施时机**: 需要长期监控分析时

---

## 3. 低优先级 TODO

### 3.1 指标可视化 Dashboard

**当前状态**: 无

**目标**: 实现实时性能监控面板

**原因**:
- 初期可通过日志和测试报告查看
- 需要额外的前端开发工作

**实现方案**:
- 使用 Grafana + Prometheus
- 或使用现有的 Dashboard 系统

**预估工作量**: 3 天

**建议实施时机**: 运维需求明确时

---

### 3.2 自动 Prompt 优化

**当前状态**: 人工优化

**目标**: 基于反馈自动优化 Prompt

**原因**:
- 需要大量数据积累
- 需要复杂的优化算法

**实现方案**:
```python
class PromptOptimizer:
    """Prompt 优化器"""

    def optimize(self, failures: List[FailureCase]) -> str:
        """基于失败案例优化 Prompt"""
        # 分析失败案例
        # 生成新的 Prompt
        pass
```

**预估工作量**: 5 天

**建议实施时机**: 积累足够数据后

---

### 3.3 多模态模型备选方案

**当前状态**: 仅支持 Claude Sonnet

**目标**: 支持多个多模态模型（GPT-4V 等）

**原因**:
- 初期使用 Claude Sonnet 足够
- 多模型支持增加复杂度

**实现方案**:
```python
class GPT4MultimodalAnalyzer(MultimodalAnalyzer):
    """GPT-4V 多模态分析器"""

    def analyze(self, image_data: bytes) -> MultimodalAnalysisResult:
        # 使用 OpenAI API
        pass
```

**预估工作量**: 2 天

**建议实施时机**: 需要模型冗余或成本优化时

---

### 3.4 自动降级触发

**当前状态**: 异常时降级

**目标**: 基于性能指标自动触发降级

**原因**:
- 需要更复杂的监控逻辑
- 可能引入不稳定因素

**实现方案**:
```python
class SmartFlattenedVisionService(FlattenedVisionService):
    """智能降级视觉服务"""

    def __init__(self, ...):
        # 配置降级阈值
        self.error_rate_threshold = 0.1  # 10% 错误率
        self.latency_threshold_ms = 5000  # 5 秒延迟

    def should_fallback(self) -> bool:
        """判断是否应该降级"""
        recent_metrics = self.metrics.get_recent(minutes=5)
        error_rate = recent_metrics.error_rate
        avg_latency = recent_metrics.avg_latency

        return (error_rate > self.error_rate_threshold or
                avg_latency > self.latency_threshold_ms)
```

**预估工作量**: 1.5 天

**建议实施时机**: 需要智能运维时

---

### 3.5 测试数据集扩充

**当前状态**: 8 张标准截图

**目标**: 扩充到 50+ 张截图

**原因**:
- 初期 8 张足以覆盖主要场景
- 扩充需要人工标注

**实现方案**:
- 收集更多真实截图
- 人工标注 ground truth
- 添加到测试集

**预估工作量**: 3 天

**建议实施时机**: 需要更全面测试时

---

## 4. 技术债务

### 4.1 类型注解完善

**当前状态**: 部分代码缺少类型注解

**目标**: 完善所有类型注解

**原因**:
- 初期快速开发，部分类型注解缺失
- 完善类型注解可提高代码质量

**预估工作量**: 0.5 天

---

### 4.2 错误处理完善

**当前状态**: 部分异常处理较简单

**目标**: 细化异常类型，提供更详细的错误信息

**原因**:
- 初期使用通用异常
- 细化异常有助于调试

**预估工作量**: 1 天

---

### 4.3 日志完善

**当前状态**: 日志较为简单

**目标**: 增加结构化日志，便于分析

**原因**:
- 初期使用简单 print
- 结构化日志便于监控和分析

**预估工作量**: 0.5 天

---

## 5. 实施建议

### 5.1 优先级排序

如果需要实施部分 TODO 功能，建议按以下顺序：

1. **P0**: 感知哈希算法优化（提高缓存命中率）
2. **P1**: Few-shot Prompt 示例（提高准确率）
3. **P2**: 指标持久化（便于长期监控）
4. **P3**: Redis 分布式缓存（支持多实例）
5. **P4**: 其他功能

### 5.2 实施时机

- **P6 阶段后**: 感知哈希、Few-shot Prompt
- **V5.3 版本**: DualVisionService、Redis 缓存、指标持久化
- **V6.0 版本**: Dashboard、自动优化、多模型支持

### 5.3 工作量估算

| 优先级 | 功能 | 工作量 |
|--------|------|--------|
| P0 | 感知哈希优化 | 0.5 天 |
| P1 | Few-shot 示例 | 1 天 |
| P2 | 指标持久化 | 1.5 天 |
| P3 | Redis 缓存 | 1 天 |
| - | DualVisionService | 1 天 |
| - | Dashboard | 3 天 |
| - | 自动优化 | 5 天 |
| - | 多模型支持 | 2 天 |
| - | 测试集扩充 | 3 天 |
| - | 技术债务 | 2 天 |
| **总计** | | **20 天** |

---

## 6. 总结

本 TODO 列表包含了 14 项未实现功能，总计约 20 天工作量。这些功能不影响 V5.2 的核心目标，可以在后续版本中逐步实施。

**核心原则**：
- 先实现核心功能，再优化
- 先满足基本需求，再增强体验
- 先单机运行，再分布式部署

---

**文档版本**: 1.0
**最后更新**: 2026-06-02
