# Trace 模块设计文档

> **模块**: `src/trace/`
> **版本**: V6.3
> **更新日期**: 2026-06-06

---

## 1. 模块概述

### 1.1 职责

Trace 模块实现**分布式追踪系统**，使用行业标准术语（Trace ID、Span ID、Parent Span ID）和 ULID 标识符，记录遍历引擎运行过程中的关键操作。不参与运行时决策，只提供事后审计、回放、分析、仿真验证和断点恢复能力。

### 1.2 核心设计原则

- **纯追加写入**: Trace 数据仅追加，不修改已写入内容
- **存储可插拔**: `TraceStorage` 抽象接口，支持 FileStorage（生产）和 MemoryStorage（仿真）
- **全局链路追踪**: 三层节点模型（Session/Step/Span）构建完整调用树
- **分析器重建**: 分析时从 Span 流重建树结构、回填状态
- **Span 流恢复**: 断点恢复时直接回放 Trace Span 流重建 Context

### 1.3 模块文件

```
src/trace/
  models.py      # TraceNode, SessionNode, StepNode, SpanNode, generate_id()
  storage.py     # TraceStorage(ABC), FileStorage, MemoryStorage
  recorder.py    # TraceRecorder, StepTracker
  analyzer.py    # TraceAnalyzer, build_tree()
  context.py     # Session, StackFrame, TraversalRuntimeContext
  recovery.py    # ContextRebuilder, RecoveryStrategy
  metrics.py     # AICallMetrics, ExecutionMetrics, ErrorMetrics
```

---

## 2. 核心类和接口

### 2.1 数据模型 (`models.py`)

#### generate_id()

```python
def generate_id() -> str:
    """生成 26-char Crockford Base32 ULID，时间可排序，URL 安全"""
```

#### TraceNode (基类)

```python
@dataclass
class TraceNode:
    trace_id: str           # 全局 Trace ID（同一次遍历所有节点共享）
    span_id: str            # 节点唯一 ID（ULID 格式）
    parent_span_id: Optional[str]  # 父节点 span_id，建立调用链
    node_type: str          # "session" | "step" | "span"
    timestamp: float
```

#### SessionNode(TraceNode)

```python
@dataclass
class SessionNode(TraceNode):
    """Trace 根节点，对应一次遍历运行"""
    device_id, device_name, device_model, os_version
    app_version, app_package
    start_time, end_time, status, traversal_mode, config
    children: List[TraceNode]
    # span_id 即 trace_id，parent_span_id 为 None
```

#### StepNode(TraceNode)

```python
@dataclass
class StepNode(TraceNode):
    """遍历步骤，对应一次 NODE_SELECT → … → FRAME_COMPLETE"""
    node_id: str            # 图节点 ID
    step_type: str          # NODE_SELECT | FRAME_COMPLETE
    page_path: List[str]    # 页面路径
    result: Optional[Dict]  # 由 step_end Span 回填
    children: List[TraceNode]
```

#### SpanNode(TraceNode)

```python
@dataclass
class SpanNode(TraceNode):
    """细粒度操作，6 种类型"""
    span_type: str        # state_transition|execution|ai_call|error|step_end|session_end

    # state_transition: from_state, to_state, state_machine
    # execution: action, status, target, page_before, page_after, duration_ms
    # ai_call: capability, provider_id, success, latency_ms, input_tokens, output_tokens
    # error: error_type, error_message, severity, stack_trace
    # step_end: step_span_id, result (回填 StepNode)
    # session_end: status, end_time (回填 SessionNode)
```

### 2.2 存储 (`storage.py`)

#### TraceStorage (ABC)

```python
class TraceStorage(ABC):
    @abstractmethod
    def write(self, node: TraceNode) -> None: ...
    @abstractmethod
    def read(self, trace_id: str) -> List[TraceNode]: ...
```

#### FileStorage — 生产环境

```python
class FileStorage(TraceStorage):
    """异步 JSONL 文件存储，后台线程写入，队列缓冲（max 10k），不阻塞遍历"""
    def write(self, node)        # 入队（非阻塞）
    def read(self, trace_id)     # 读取 trace.jsonl
    def write_session(data, tid) # 写入 session.json
    def read_session(tid)        # 读取 session.json
    def flush(timeout=5.0)       # 等待队列排空
```

目录结构：
```
traces/{trace_id}/
  session.json          # 会话元数据
  trace.jsonl           # 每行一个节点 JSON
  screenshots/index.json  # ref_id → filename 映射
```

#### MemoryStorage — 仿真/测试

```python
class MemoryStorage(TraceStorage):
    """内存存储，按 trace_id 隔离，无 I/O"""
```

### 2.3 录制器 (`recorder.py`)

#### StepTracker

```python
class StepTracker:
    """管理步骤 span_id 栈，自动计算 parent_span_id"""
    def on_node_enter(span_id)    # 压栈 → 新节点成为 parent
    def on_node_exit()            # 弹栈 → 恢复上一层 parent
    def get_parent_span_id()      # 返回栈顶
```

#### TraceRecorder

```python
class TraceRecorder:
    def init(session_node)                              # 初始化会话，写入 SessionNode
    def record_step_start(step_node, parent_span_id)     # 记录步骤开始
    def record_span(span, parent_span_id)                # 记录 Span
    def record_step_end(step_span_id, result)            # 记录步骤结束（step_end Span）
    def finalize(status, end_time)                       # 记录会话结束（session_end Span）

    # 错误处理: "log and continue" — 写入失败记录警告日志，不中断遍历
```

### 2.4 分析器 (`analyzer.py`)

#### build_tree()

```python
def build_tree(nodes: List[TraceNode]) -> Optional[SessionNode]:
    """从扁平节点列表重建完整树结构：
    1. 按 span_id 索引所有节点
    2. 按 parent_span_id 建立父子关系
    3. step_end Span → 回填 StepNode.result
    4. session_end Span → 回填 SessionNode.status / end_time
    5. 返回 SessionNode 根节点
    """
```

#### TraceAnalyzer

```python
class TraceAnalyzer:
    def extract_page_tree() -> Dict         # 嵌套页面层级 + 访问计数
    def extract_state_sequence() -> List     # 按时间排序的状态转移
    def extract_span_chain(span_id) -> List  # 从根到指定 span 的完整调用链
    def extract_ai_calls() -> List           # AI 调用记录（能力/延迟/token）
    def extract_action_sequence() -> List    # 动作执行序列
    def extract_error_statistics() -> Dict   # 按类型/严重度/页面分类的错误统计
    def extract_time_analysis() -> Dict      # 总耗时/P50/P95/最慢操作
    def extract_coverage_analysis() -> Dict  # 页面/节点覆盖率 + 热力图数据
```

### 2.5 上下文模型 (`context.py`)

```python
@dataclass
class Session:
    """遍历会话元数据，session_id 即全局 trace_id。
    独立存储为 traces/{trace_id}/session.json。"""
    session_id: str  (= trace_id)
    device_model, os_version, app_package
    start_time, end_time, status, traversal_mode, config

@dataclass
class StackFrame:
    """节点栈条目，node_id + span_id + node_type"""

@dataclass
class TraversalRuntimeContext:
    """可变运行时上下文（引擎使用）。包含 trace_id、node_stack、current_path、
    visited_pages、visited_level1/2_menus、action_history、failed_nodes 等。
    通过 to_readonly() 转换为不变的 TraversalContext 传给 AI。"""
```

### 2.6 上下文恢复 (`recovery.py`)

```python
class RecoveryStrategy(str, Enum):
    FULL = "full"        # 完整恢复（current_path + node_stack + visited_pages + menus）
    REPLAY = "replay"    # 仅回放关键步骤
    MINIMAL = "minimal"  # 最小恢复

class ContextRebuilder:
    def rebuild(nodes, trace_id, strategy) -> TraversalRuntimeContext:
        """回放 Span 流重建 Context。按原始写入顺序处理，逐步重建状态。"""
```

### 2.7 指标收集 (`metrics.py`)

```python
@dataclass
class AICallMetrics:      # capability, provider_id, success, latency_ms, tokens
class ExecutionMetrics:   # action, status, target, page_before/after, duration_ms
class ErrorMetrics:       # error_type, error_message, severity, stack_trace
```

---

## 3. 三层节点模型

```
SessionNode (trace root, span_id = trace_id, parent_span_id = None)
  ├── StepNode (NODE_SELECT, page_path=["home"])
  │     ├── SpanNode (ai_call, capability="vision", latency_ms=350)
  │     ├── SpanNode (execution, action="click", status="success")
  │     └── SpanNode (step_end, 回填 StepNode.result)
  ├── StepNode (NODE_SELECT, page_path=["home","settings"])
  │     ├── SpanNode (ai_call)
  │     ├── SpanNode (state_transition, IDLE→TRAVERSING)
  │     ├── SpanNode (execution)
  │     └── SpanNode (step_end)
  └── SpanNode (session_end, 回填 SessionNode.status / end_time)
```

---

## 4. 依赖关系

```
src/trace/
  models.py       → (无内部依赖)
  storage.py      → models.py
  recorder.py     → models.py, storage.py
  analyzer.py     → models.py
  context.py      → models.py
  recovery.py     → context.py, models.py
  metrics.py      → (无内部依赖)

外部消费者:
  src/traversal/graph_engine.py  → TraceRecorder, MemoryStorage, Session, TraversalRuntimeContext
  src/simulation/runner.py       → TraceRecorder, MemoryStorage, TraceAnalyzer
  dashboards/trace_server.py     → FileStorage, TraceAnalyzer, build_tree
```

---

## 5. 设计决策

### 5.1 三层节点模型（Session / Step / Span）
- SessionNode: 任务级锚点（trace_id 唯一标识一次遍历）
- StepNode: 遍历级步骤（NODE_SELECT 到 FRAME_COMPLETE 为一个 step）
- SpanNode: 操作级记录（6 种类型覆盖全部组件交互）
- 纯追加：所有节点写入后不修改，step_end/session_end 作为独立 Span 回填

### 5.2 ULID 标识符
- 48-bit 时间戳 + 80-bit 随机数 = 128-bit
- Crockford Base32 编码，26 字符
- 时间可排序，URL 安全，无需协调即可生成全局唯一 ID

### 5.3 Log-and-Continue
- Trace 写入是辅助功能，不是关键路径
- 写入失败记录警告日志，不中断遍历

### 5.4 组件收集 → 引擎组装
- 低级组件（StateMachine, AIClient, ActionExecutor）收集原始 metrics
- 引擎组装完整 SpanNode（统一格式、完整上下文）

### 5.5 Context 双态（可变 vs 只读）
- TraversalRuntimeContext: 引擎内部可变状态
- TraversalContext (frozen): 传给 AI advisor 的只读快照
- to_readonly() 在传递前做不可变拷贝

---

## 7. 存储目录

```
traces/{trace_id}/
  session.json          # Session 元数据（独立于 trace 流）
  trace.jsonl           # 每行一个 TraceNode JSON
  screenshots/
    index.json          # ref_id → filename 映射
```

---

**最后更新**: 2026-06-06
**维护者**: Uni-Claw 开发团队
