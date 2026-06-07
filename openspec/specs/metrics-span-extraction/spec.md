## ADDED Requirements

### Requirement: 引擎提取 _record_metrics_as_spans 方法
GraphTraversalEngine SHALL 提供独立的 `_record_metrics_as_spans(metrics: Dict) -> None` 方法，将 handler metrics 转换为 SpanNode 并写入 TraceRecorder。

#### Scenario: ai_call metrics 转换
- **WHEN** metrics 包含 `"ai_call"` 键
- **THEN** 创建 `SpanNode(span_type="ai_call", capability=..., provider_id=..., success=..., latency_ms=..., input_tokens=..., output_tokens=..., page_id=..., element_count=...)`
- **AND** 调用 `self.trace_recorder.record_span(span)`

#### Scenario: execution metrics 转换
- **WHEN** metrics 包含 `"execution"` 键
- **THEN** 创建 `SpanNode(span_type="execution", action=..., status=..., target=..., duration_ms=...)`
- **AND** 调用 `self.trace_recorder.record_span(span)`

#### Scenario: error metrics 转换
- **WHEN** metrics 包含 `"error"` 键
- **THEN** 创建 `SpanNode(span_type="error", error_type=..., error_message=..., severity=...)`
- **AND** 调用 `self.trace_recorder.record_span(span)`

#### Scenario: 空 metrics 不报错
- **WHEN** metrics 为 None 或空 dict
- **THEN** 方法直接返回，不调用 record_span

### Requirement: _step_once 调用 _record_metrics_as_spans
`_step_once()` SHALL 用 `self._record_metrics_as_spans(metrics)` 替换当前 14 行内联代码。

#### Scenario: 功能等价
- **WHEN** 相同的 metrics dict 传入内联代码和 `_record_metrics_as_spans`
- **THEN** 生成的 SpanNode 数量、类型、字段值完全一致

### Requirement: SpanNode 新增 page_id 和 element_count
SpanNode SHALL 支持 `page_id: Optional[str]` 和 `element_count: Optional[int]` 字段，默认 None。

#### Scenario: ai_call span 序列化包含新字段
- **WHEN** `SpanNode(span_type="ai_call", page_id="home", element_count=5).to_dict()` 被调用
- **THEN** 返回 dict 包含 `"page_id": "home"` 和 `"element_count": 5`

#### Scenario: 非 ai_call span 不序列化新字段
- **WHEN** `SpanNode(span_type="execution").to_dict()` 被调用
- **THEN** 返回 dict 不包含 `page_id` 和 `element_count`

#### Scenario: 反序列化保留新字段
- **WHEN** 从 `{"span_type": "ai_call", "page_id": "home", "element_count": 3}` 调用 `SpanNode.from_dict`
- **THEN** 返回的 SpanNode 的 `page_id == "home"` 且 `element_count == 3`
