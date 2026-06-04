# Utils 模块设计文档

> **模块**: `src/utils/`
> **版本**: V1.0
> **创建日期**: 2026-06-03
> **维护者**: Uni-Claw 开发团队

---

## 一、模块概述

### 1.1 职责定义

`src/utils/` 模块提供项目中可复用的工具函数和基础设施组件。当前版本专注于**分布式追踪（Distributed Tracing）**功能，为整个系统提供结构化日志记录和调用链追踪能力。

### 1.2 设计原则

- **轻量级**: 最小化依赖，避免引入外部追踪库
- **可选集成**: 组件可以无降级地使用追踪功能
- **线程安全**: 支持多线程环境下的追踪写入
- **易于调试**: 提供清晰的日志格式和可视化支持

### 1.3 模块组成

```
src/utils/
└── trace.py                    # 分布式追踪日志系统
```

---

## 二、核心组件

### 2.1 TraceContext - 追踪上下文

```python
@dataclass
class TraceContext:
    """追踪上下文，跟踪操作通过整个管道的过程"""
    trace_id: str              # 追踪ID（8位UUID）
    parent_id: Optional[str]    # 父span ID（用于调用链）
    span_id: str                # 当前span ID
    component: str              # 组件名称
    operation: str              # 操作名称
    start_time: float           # 开始时间
    tags: Dict[str, Any]        # 自定义标签
    metadata: Dict[str, Any]    # 元数据
```

**职责**:
- 维护单个操作的追踪上下文
- 支持父子关系建立调用链
- 计算操作持续时间

### 2.2 TraceLogger - 结构化日志记录器

```python
class TraceLogger:
    """带追踪上下文和文件写入的结构化日志记录器"""

    def __init__(self, component: str):
        """初始化组件日志记录器"""

    def start_span(self, operation: str, parent_context: Optional[TraceContext] = None, **tags) -> TraceContext
    """开始新的追踪span"""

    def finish_span(self, context: TraceContext, result: Optional[Any] = None, error: Optional[Exception] = None)
    """完成追踪span并记录结果"""

    def log_input(self, context: TraceContext, **data)
    """记录操作输入"""

    def log_output(self, context: TraceContext, **data)
    """记录操作输出（自动清理）"""

    def log_event(self, context: TraceContext, event: str, **data)
    """记录自定义事件"""

    @contextmanager
    def span(self, operation: str, parent_context: Optional[TraceContext] = None, **tags)
    """自动管理span生命周期的上下文管理器"""
```

**职责**:
- 管理span生命周期（开始/结束）
- 记录操作输入输出
- 捕获异常和错误状态
- 将日志写入文件

**特性**:
- **自动清理**: 输出数据自动截断（字符串200字符，数组前3项）
- **上下文管理器**: 支持 `with` 语句自动管理span
- **异常处理**: 自动捕获和记录异常信息

### 2.3 TraceFileWriter - 文件写入器

```python
class TraceFileWriter:
    """将追踪日志写入文件以供分析"""

    def __init__(self, log_dir: Path = Path(".traces"))
    """初始化追踪文件写入器"""

    def write_trace(self, trace_id: str, data: Dict)
    """写入完整追踪数据到JSON文件"""

    def append_span(self, trace_id: str, span_data: Dict)
    """追加span数据到JSONL文件"""
```

**职责**:
- 管理追踪文件目录
- 持久化追踪数据
- 使用JSONL格式便于增量写入

**文件格式**:
- JSONL (JSON Lines) - 每行一个JSON对象
- 文件命名: `{trace_id}.jsonl`
- 目录: `.traces/` (可配置)

### 2.4 全局单例管理

```python
# 全局追踪写入器实例
_trace_writer: Optional["TraceFileWriter"] = None
_trace_writer_lock = Lock()

def get_trace_writer() -> TraceFileWriter
"""获取全局追踪写入器实例（线程安全）"""

def enable_trace_writing(log_dir: Optional[Path] = None)
"""启用追踪文件写入"""
```

**职责**:
- 提供全局共享的追踪写入器
- 确保线程安全
- 延迟初始化

---

## 三、使用模式

### 3.1 组件集成模式

各组件通过可选导入集成追踪功能：

```python
# 在组件初始化时尝试导入
def __init__(self):
    self._trace = None
    try:
        from ..utils.trace import TraceLogger
        self._trace = TraceLogger("component_name")
    except ImportError:
        pass  # 追踪功能不可用时优雅降级
```

**使用此模式的组件**:
- `vision/base_vision.py` - VisionService
- `state/state_manager.py` - StateManager
- `adb/adb_client.py` - ADBClient
- `ai/core/capability.py` - AICapability

### 3.2 Span生命周期管理

**方式1: 手动管理**
```python
context = self._trace.start_span("analyze_screen")
try:
    result = self._do_analysis()
    self._trace.finish_span(context, result=result)
except Exception as e:
    self._trace.finish_span(context, error=e)
    raise
```

**方式2: 上下文管理器（推荐）**
```python
with self._trace.span("analyze_screen") as context:
    result = self._do_analysis()
    # 自动处理异常和完成
```

### 3.3 输入输出记录

```python
# 记录输入
self._trace.log_input(context, prompt="分析屏幕", image_size=1024)

# 记录输出（自动清理）
self._trace.log_output(context, items=[...], confidence=0.95)
```

### 3.4 自定义事件

```python
self._trace.log_event(context, "screen_changed", prev_screen="home", new_screen="settings")
```

---

## 四、依赖关系

### 4.1 内部依赖

```
utils/trace.py
    └── (被以下模块导入)
        ├── src/analysis/trace_analyzer.py    # 分析追踪数据
        ├── src/vision/base_vision.py         # 视觉服务追踪
        ├── src/state/state_manager.py         # 状态管理追踪
        ├── src/adb/adb_client.py             # ADB操作追踪
        └── src/ai/core/capability.py         # AI能力追踪
```

### 4.2 外部依赖

```python
import logging        # 标准日志库
import time           # 时间戳
import uuid           # 唯一ID生成
from pathlib import Path    # 文件路径
from threading import Lock  # 线程安全
```

**特点**: 仅依赖Python标准库，无第三方依赖

### 4.3 与 src.trace 模块的关系

| 特性 | src/utils/trace.py | src.trace/ |
|------|-------------------|------------|
| 用途 | 分布式追踪日志 | 遍历记录与回放 |
| 数据格式 | JSONL span事件 | 完整遍历会话 |
| 粒度 | 组件级操作 | 遍历步骤级 |
| 主要使用者 | 内部组件调试 | 测试、回放、分析 |
| 文件位置 | .traces/*.jsonl | ./traces/sessions/ |

两个模块互补:
- `utils/trace.py`: 实时操作追踪，用于调试和性能分析
- `src.trace/`: 完整遍历记录，用于测试回放

---

## 五、数据流

### 5.1 追踪数据生成流程

```
┌─────────────────┐
│  Component Code │
│                 │
│  trace.start_span()
│       ↓
│  [执行操作]
│       ↓
│  trace.log_input()
│  trace.log_output()
│       ↓
│  trace.finish_span()
└────────┬────────┘
         │
         ↓
┌─────────────────────────────────┐
│  TraceLogger (in-memory)         │
│  - 管理TraceContext              │
│  - 清理敏感数据                  │
│  - 附加元数据                    │
└────────┬────────────────────────┘
         │
         ↓
┌─────────────────────────────────┐
│  TraceFileWriter (persistent)   │
│  - 追加到JSONL文件               │
│  - .traces/{trace_id}.jsonl     │
└─────────────────────────────────┘
```

### 5.2 追踪数据分析流程

```
┌─────────────────────┐
│  .traces/*.jsonl    │
└──────────┬──────────┘
           │
           ↓
┌─────────────────────────────────┐
│  TraceAnalyzer                  │
│  (src/analysis/trace_analyzer) │
│                                 │
│  - load_all_traces()            │
│  - analyze_component_performance()
│  - get_slowest_operations()     │
│  - get_trace_timeline()         │
└─────────────────────────────────┘
           │
           ↓
┌─────────────────────────────────┐
│  可视化与分析                    │
│  - 性能仪表板                    │
│  - 调用链图                      │
│  - 瓶颈识别                      │
└─────────────────────────────────┘
```

---

## 六、设计决策

### 6.1 为什么使用JSONL格式？

**决策**: 使用JSONL（JSON Lines）而非纯JSON

**原因**:
1. **增量写入**: 支持追加写入，不需要加载整个文件
2. **流式处理**: 便于逐行解析和处理
3. **容错性**: 单行损坏不影响其他记录
4. **调试友好**: 可以直接用 `tail` 和 `jq` 查看

### 6.2 为什么使用可选导入？

**决策**: 组件通过try-except可选导入TraceLogger

**原因**:
1. **解耦**: 追踪系统不是核心功能，不应影响组件正常工作
2. **测试**: 便于在没有追踪的情况下进行单元测试
3. **部署**: 允许在不同环境中选择性启用追踪

### 6.3 为什么全局单例TraceFileWriter？

**决策**: 使用全局单例模式管理文件写入器

**原因**:
1. **资源共享**: 避免多个组件写入同一文件时的冲突
2. **线程安全**: 使用锁确保并发写入安全
3. **配置统一**: 集中管理输出目录和写入策略
4. **性能**: 减少文件句柄开销

### 6.4 为什么自动清理输出数据？

**决策**: 在`_sanitize_output()`中自动截断字符串和数组

**原因**:
1. **存储效率**: 避免追踪文件过大
2. **隐私保护**: 不记录完整的敏感数据
3. **性能**: 减少I/O开销
4. **可读性**: 保持追踪文件精简可读

---

## 七、扩展点

### 7.1 当前限制

1. **单一输出**: 只支持文件输出，不支持网络传输
2. **简单采样**: 没有采样策略（全部记录）
3. **无聚合**: 不支持跨组件的指标聚合
4. **手动管理**: 需要手动启用 `enable_trace_writing()`

### 7.2 潜在扩展

1. **追踪后端**:
   - 添加远程追踪后端（如Jaeger、Zipkin）
   - 支持OpenTelemetry协议

2. **采样策略**:
   - 概率采样（避免数据量过大）
   - 基于组件的动态采样

3. **指标聚合**:
   - 自动计算组件级指标
   - 生成性能报告

4. **自动配置**:
   - 通过环境变量自动启用
   - 配置文件驱动的追踪策略

---

## 八、使用示例

### 8.1 完整追踪流程

```python
# 启用追踪写入
from src.utils.trace import enable_trace_writing
enable_trace_writing(Path(".traces"))

# 组件内部使用
class VisionService:
    def __init__(self):
        self._trace = TraceLogger("vision")

    def analyze_screen(self, image):
        with self._trace.span("analyze_screen", screen_size=len(image)) as ctx:
            # 记录输入
            self._trace.log_input(ctx, image_size=len(image))

            # 执行分析
            result = self._call_vision(image)

            # 记录输出
            self._trace.log_output(ctx, items_count=len(result))
            self._trace.log_event(ctx, "analysis_complete", confidence=0.95)

            return result
```

### 8.2 调用链示例

```python
# 组件A发起追踪
context_a = trace_a.start_span("operation_a")

# 组件B继承父追踪
context_b = trace_b.start_span("operation_b", parent_context=context_a)

# 结果：context_b.trace_id == context_a.trace_id
#      context_b.parent_id == context_a.span_id
```

### 8.3 分析追踪数据

```python
from src.analysis.trace_analyzer import TraceAnalyzer

analyzer = TraceAnalyzer(Path(".traces"))

# 获取所有会话
sessions = analyzer.load_all_traces()

# 分析组件性能
perf = analyzer.analyze_component_performance()
print(f"Vision平均耗时: {perf['vision']['avg_duration_ms']:.0f}ms")

# 获取最慢的操作
slowest = analyzer.get_slowest_operations(limit=5)
for op in slowest:
    print(f"{op['component']}.{op['operation']}: {op['duration_ms']:.0f}ms")
```

---

## 九、最佳实践

### 9.1 组件集成

1. **可选导入**: 始终使用try-except导入
2. **延迟初始化**: 在需要时才创建TraceLogger
3. **降级优雅**: 追踪不可用时不影响核心功能

### 9.2 命名规范

1. **组件名**: 使用小写模块名（如"vision"、"adb"）
2. **操作名**: 使用动词_名词格式（如"analyze_screen"、"execute_action"）
3. **事件名**: 使用过去式（如"screen_changed"、"item_clicked"）

### 9.3 数据记录

1. **输入记录**: 记录关键参数，避免大块数据
2. **输出记录**: 依赖自动清理，只记录摘要信息
3. **事件记录**: 用于状态转换和重要里程碑

### 9.4 性能考虑

1. **避免过度追踪**: 只在关键路径上追踪
2. **使用上下文管理器**: 确保span正确关闭
3. **控制日志量**: 使用采样或选择性记录

---

## 十、维护指南

### 10.1 添加新追踪点

1. 在目标组件中添加可选导入
2. 在构造函数中初始化TraceLogger
3. 在关键操作周围添加span
4. 记录有意义的输入输出

### 10.2 测试

1. **单元测试**: 验证TraceLogger行为
2. **集成测试**: 验证端到端追踪流程
3. **性能测试**: 确保追踪不影响性能

### 10.3 文件管理

1. **清理策略**: 定期清理旧的.traces文件
2. **归档**: 将重要追踪数据归档到其他位置
3. **监控**: 监控.traces目录大小

---

## 附录

### A. 追踪数据格式

```jsonl
// Span开始
{"type": "span_start", "timestamp": 1717411200.0, "trace_id": "abc123", "span_id": "def456", "component": "vision", "operation": "analyze_screen", "tags": {"screen_size": 1024}}

// 输入
{"type": "input", "timestamp": 1717411200.1, "trace_id": "abc123", "span_id": "def456", "component": "vision", "operation": "analyze_screen", "data": {"image_size": 1024}}

// 输出
{"type": "output", "timestamp": 1717411201.5, "trace_id": "abc123", "span_id": "def456", "component": "vision", "operation": "analyze_screen", "data": {"items_count": 5}}

// Span结束
{"type": "span_end", "timestamp": 1717411201.6, "trace_id": "abc123", "span_id": "def456", "component": "vision", "operation": "analyze_screen", "duration_ms": 1500, "status": "success", "metadata": {"success": true, "has_result": true}}
```

### B. 相关文档

- [OBSERVABILITY.md](../OBSERVABILITY.md) - 可观测性系统总览
- [ARCHITECTURE.md](../ARCHITECTURE.md) - 系统架构
- [src/analysis/trace_analyzer.py](../../src/analysis/trace_analyzer.py) - 追踪分析器

---

**最后更新**: 2026-06-03
**维护者**: Uni-Claw 开发团队
