# Trace 系统改进设计文档

> **文档版本**: v1.0
> **创建日期**: 2026-06-02
> **状态**: TODO - 待实施
> **类型**: 系统改进设计文档
> **关联**: PRD V4.0 Trace 系统设计

---

## 文档说明

本文档记录 uni-claw V4.0 Trace 系统与 PRD 设计相比的**关键差异和潜在问题**，以及相应的改进建议。这些问题在当前实现中已识别，但**暂不实施**，作为未来改进的参考。

**改进原则**：
1. 向后兼容 - 新增字段不破坏现有功能
2. 分阶段实施 - 优先解决核心能力问题
3. 可审计性 - 增强 AI 决策的质量评估能力

---

## 问题概览

| ID | 问题 | 优先级 | 阶段建议 | 状态 |
|----|------|--------|----------|------|
| 1 | AI 交互记录不够独立 | 高 | Phase 1 | TODO |
| 2 | 缺少完整页面记录 | 中 | Phase 2 | TODO |
| 3 | 决策来源覆盖不全 | 高 | Phase 1 | TODO |
| 4 | 缺少 restore_performed 标记 | 中 | Phase 2 | TODO |
| 5 | SIMULATION 模式定义模糊 | 中 | Phase 2 | TODO |
| 6 | reasoning 强制填写问题 | 低 | Phase 1 | TODO |
| 7 | 辅助方法签名不明确 | 低 | Phase 2 | TODO |

---

## 问题1：AI 交互记录不够独立

### 当前状态

在 `TraceDecision` 中，AI 相关信息仅包含：
- `reasoning: Optional[str]` - 决策推理
- `confidence: float` - 置信度

**缺失的信息**：
- AI 的原始输入 (`input_summary`)
- AI 的原始输出 (`output_raw`)
- 是否被采纳 (`was_accepted`)
- 如果被拒绝，原因 (`rejection_reason`)
- 模型版本 (`model_version`)
- Prompt 版本 (`prompt_version`)

### 问题影响

1. **无法独立分析 AI 质量**
   - 想统计 `screen_elements` 的准确率，无法知道 AI 给了什么标签
   - 想对比不同 Prompt 版本的效果，缺少 `prompt_version` 字段

2. **审计能力不足**
   - 无法追溯 AI 决策的完整上下文
   - 无法进行 AI 效果的 A/B 测试分析

### 改进方案

#### 新增 `AIInteraction` 模型

```python
@dataclass
class AIInteraction:
    """单次 AI 调用的完整记录"""

    # 基本信息
    interaction_id: str
    timestamp: datetime
    method: str  # "screen_elements", "make_decision", "verify_page_type", etc.

    # 输入
    input_summary: Dict[str, Any]  # 页面摘要、上下文摘要
    context_snapshot: Dict[str, Any]  # 运行时上下文快照

    # 输出
    output_raw: Dict[str, Any]  # AI 原始响应
    output_parsed: Optional[Dict[str, Any]] = None  # 解析后的结构化输出

    # 元数据
    model_version: str  # 使用的模型版本
    prompt_version: Optional[str] = None  # Prompt 版本
    duration_ms: float = 0.0  # 调用耗时

    # 采纳情况
    was_accepted: bool = False  # 是否被采纳
    rejection_reason: Optional[str] = None  # 拒绝原因
    safety_blocked: bool = False  # 是否被安全策略拦截

    # 关联
    related_step_id: Optional[int] = None  # 关联的 TraceStep ID
```

#### 新增 `ai_interactions.jsonl` 文件

```
traces/
└── trace_20240602_143022/
    ├── trace.jsonl
    ├── ai_interactions.jsonl    # 新增
    ├── snapshots.jsonl
    ├── session.json
    ├── summary.json
    └── screenshots/
```

#### 记录流程

```python
# 在 AIProvider 调用时记录
def record_ai_interaction(
    method: str,
    input_data: Dict[str, Any],
    output_data: Dict[str, Any],
    model_version: str,
    prompt_version: Optional[str] = None,
) -> AIInteraction:
    interaction = AIInteraction(
        interaction_id=generate_id(),
        timestamp=datetime.now(),
        method=method,
        input_summary=summarize_input(input_data),
        context_snapshot=capture_context(),
        output_raw=output_data,
        model_version=model_version,
        prompt_version=prompt_version,
        duration_ms=elapsed_ms,
    )
    return interaction

# 在采纳/拒绝时更新
def mark_ai_outcome(
    interaction: AIInteraction,
    was_accepted: bool,
    rejection_reason: Optional[str] = None,
    safety_blocked: bool = False,
):
    interaction.was_accepted = was_accepted
    interaction.rejection_reason = rejection_reason
    interaction.safety_blocked = safety_blocked
```

### 实施要点

1. **不破坏现有逻辑** - 新增独立的记录文件，不影响现有 `TraceDecision`
2. **异步写入** - AI 交互记录异步写入，避免阻塞主流程
3. **可配置启用** - 通过配置控制是否记录详细的 AI 交互

---

## 问题2：缺少完整页面记录

### 当前状态

`TraceStep.page_analysis_summary` 只存储了摘要字符串：
```python
page_analysis_summary: Optional[str] = None  # e.g., "设置页面 (15 items)"
```

### 问题影响

1. **无法构建虚拟页面库** - 不能从 Trace 中提取完整的 `PageAnalysis`
2. **回放精度受限** - 无法使用当时的页面数据进行精确对比

### 改进方案

#### 新增 `FullPageRecord` 模型

```python
@dataclass
class FullPageRecord:
    """完整页面分析记录"""

    page_hash: str  # SHA256 哈希，用于去重
    timestamp: datetime

    # 完整页面数据
    page_analysis: PageAnalysis  # 完整的页面分析结果
    screenshot_ref: Optional[str] = None  # 关联的截图

    # 元数据
    first_seen_step: int  # 首次出现的步骤 ID
    occurrence_count: int = 1  # 出现次数
```

#### 分离存储结构

```
traces/
└── trace_20240602_143022/
    ├── trace.jsonl              # 步骤中仅存 page_hash
    ├── pages.jsonl              # 新增：完整页面记录
    ├── ai_interactions.jsonl
    ├── snapshots.jsonl
    ├── session.json
    ├── summary.json
    └── screenshots/
```

#### TraceStep 中的引用

```python
@dataclass
class TraceStep:
    # ... 其他字段

    # 改为哈希引用
    page_hash: Optional[str] = None  # 引用 pages.jsonl 中的记录
    page_analysis_summary: Optional[str] = None  # 保留，用于快速显示
```

### 实施要点

1. **去重策略** - 使用 `PageAnalysis` 内容的 SHA256 哈希去重
2. **存储开销** - 需要评估存储空间增加，可配置是否启用
3. **向后兼容** - 保留 `page_analysis_summary` 字段

---

## 问题3：决策来源覆盖不全

### 当前状态

`TraceDecision` 中没有显式的 `decision_source` 字段，无法区分：
- 规则引擎决策
- AI 决策
- 安全过滤器决策
- 人工干预

### 改进方案

```python
class DecisionSource(str, Enum):
    """决策来源"""
    RULE_ENGINE = "rule_engine"  # 规则引擎（动态匹配）
    AI_SCREEN_ELEMENTS = "ai_screen_elements"  # AI 元素预筛
    AI_DECISION = "ai_decision"  # AI 上下文决策
    AI_PAGE_VERIFY = "ai_page_verify"  # AI 页面验证
    SAFETY_FILTER = "safety_filter"  # 安全过滤器
    HUMAN = "human"  # 人工干预
    STATIC = "static"  # 静态图节点
```

```python
@dataclass
class TraceDecision:
    # ... 现有字段

    # 新增
    decision_source: DecisionSource = DecisionSource.STATIC
    source_details: Optional[Dict[str, Any]] = None  # 来源相关的额外信息
```

### 实施要点

1. **默认值兼容** - 默认为 `STATIC`，不影响现有逻辑
2. **细粒度追踪** - 可区分不同 AI 能力的贡献

---

## 问题4：缺少 restore_performed 标记

### 当前状态

`TraceExecution` 记录了执行状态，但没有标记是否执行了恢复操作（restore）。

### 问题影响

- 无法统计恢复操作的次数和成功率
- 无法验证开关/滑块遍历的安全性

### 改进方案

```python
@dataclass
class TraceExecution:
    # ... 现有字段

    # 新增
    restore_performed: bool = False  # 是否执行了恢复操作
    restore_duration_ms: float = 0.0  # 恢复操作耗时
    restore_success: bool = True  # 恢复是否成功
```

### 实施要点

1. **叶子节点专用** - 主要用于 `leaf_switch` 和 `leaf_slider`
2. **可选字段** - 不需要恢复的节点此字段无意义

---

## 问题5：SIMULATION 模式定义模糊

### 当前状态

`ReplayMode.SIMULATION` 描述为"无设备连接的干跑"，但：
- 返回了 `unique_nodes_visited` 等统计信息
- 没有明确是否执行任何操作

### PRD 定义

根据 PRD V4.0，SIMULATION 模式应该是：
- **完全离线分析 Trace 数据**
- **不连接设备**
- **不调用任何视觉或操作组件**
- **仅做统计和分析**

### 改进方案

#### 明确方法行为

```python
class ReplayEngine:
    def replay_simulation(self) -> ReplayResult:
        """
        模拟回放模式 - 完全离线分析。

        不执行任何操作，仅分析 Trace 数据：
        - 计算覆盖率指标
        - 分析节点类型分布
        - 统计操作分布
        - 重建运行时图

        Returns:
            ReplayResult 包含分析统计，无设备交互
        """
        # 仅分析已加载的 Trace 数据
        analysis = self._analyze_trace_only()
        return ReplayResult(
            success=True,
            mode=ReplayMode.SIMULATION,
            # ... 分析结果
        )
```

#### 明确不需要回调

```python
class ReplayEngine:
    def __init__(self, mode: ReplayMode = ReplayMode.STRICT):
        self.mode = mode

        # SIMULATION 模式不需要任何回调
        if mode != ReplayMode.SIMULATION:
            self._operation_callback: Optional[Callable] = None
            self._screenshot_callback: Optional[Callable] = None
            self._navigation_callback: Optional[Callable] = None
```

### 实施要点

1. **文档更新** - 明确 SIMULATION 为纯分析模式
2. **代码澄清** - 移除 SIMULATION 模式下所有回调依赖

---

## 问题6：reasoning 强制填写问题

### 当前状态

`TraceDecision.reasoning` 字段主要服务于 AI 决策的可解释性，但：
- 规则引擎的决策是确定性的，不需要解释
- 强制填写会导致冗余或空值

### 改进方案

`reasoning` 已经是 `Optional[str]`，保持现状即可。只需：

1. **文档说明** - 明确 `reasoning` 仅在 AI 决策时有意义
2. **记录指导** - 规则引擎决策时不填写此字段

### 实施要点

无需代码修改，仅完善文档和使用规范。

---

## 问题7：辅助方法签名不明确

### 当前状态

`rebuild_runtime_graph` 作为辅助功能提及，但：
- 输入参数不明确
- 返回结构不明确
- 从 Trace 的哪些字段重建不清楚

### 改进方案

#### 明确方法签名

```python
class ReplayEngine:
    def rebuild_runtime_graph(self) -> RuntimeGraph:
        """
        从 Trace 重建运行时节点图。

        Returns:
            RuntimeGraph 包含节点和边的关系
        """
        if not self.current_trace:
            raise ValueError("No trace loaded")

        graph = RuntimeGraph()

        for step in self.current_trace.steps:
            if step.decision:
                # 添加节点
                graph.add_node(
                    node_id=step.decision.node_id,
                    node_type=step.decision.node_type,
                    action=step.decision.operation_action,
                )

                # 从路径推断边
                if len(step.path_after) > 1:
                    parent = step.path_after[-2]
                    child = step.decision.node_id
                    graph.add_edge(parent, child)

        return graph


@dataclass
class RuntimeGraph:
    """运行时图结构"""
    nodes: Dict[str, NodeInfo] = field(default_factory=dict)
    edges: List[Tuple[str, str]] = field(default_factory=list)

    def add_node(self, node_id: str, node_type: str, action: str):
        self.nodes[node_id] = NodeInfo(
            node_id=node_id,
            node_type=node_type,
            action=action,
        )

    def add_edge(self, parent: str, child: str):
        self.edges.append((parent, child))


@dataclass
class NodeInfo:
    """节点信息"""
    node_id: str
    node_type: str
    action: str
```

### 实施要点

1. **类型安全** - 使用明确的数据类而非 Dict
2. **文档完善** - 说明数据来源和重建逻辑

---

## 实施建议

### 分阶段方案

#### Phase 1（核心能力增强）

**目标**：解决 AI 审计和决策分析的核心能力问题

| 问题 | 工作量 | 优先级 |
|------|--------|--------|
| 1. AI 交互记录独立化 | 2天 | 高 |
| 3. 决策来源标记 | 0.5天 | 高 |
| 6. reasoning 文档化 | 0.5天 | 低 |

**交付物**：
- `src/trace/ai_interaction.py` - AI 交互模型
- `src/trace/models.py` - 扩展 `DecisionSource` 枚举
- 更新文档说明

#### Phase 2（完善补充）

**目标**：解决存储、回放、API 规范问题

| 问题 | 工作量 | 优先级 |
|------|--------|--------|
| 2. 完整页面存储 | 2天 | 中 |
| 4. 恢复操作记录 | 1天 | 中 |
| 5. SIMULATION 模式 | 1天 | 中 |
| 7. 辅助方法签名 | 1天 | 低 |

**交付物**：
- `src/trace/page_record.py` - 页面记录模型
- 更新 `replay.py` 的方法和文档

### 实施前检查清单

在开始实施任何改进前，确认：

- [ ] 当前测试覆盖率充足（models 测试通过）
- [ ] 评估存储空间影响（特别是完整页面存储）
- [ ] 确认配置项设计（可控制各功能开关）
- [ ] 准备数据迁移方案（如需兼容旧 Trace）

---

## 参考资料

- [PRD V4.0 - Trace 系统设计](../PRD_V5_0-initial.md#5-trace-系统设计)
- [当前实现 - src/trace/](../../src/trace/)
- [核心业务模型](./core_business_models.md#8-trace-模型)

---

*本文档作为 TODO 列表，按需更新和实施。*
