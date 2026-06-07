## 1. SpanNode 新增字段

- [x] 1.1 `SpanNode` 加 `page_id: Optional[str] = None` 和 `element_count: Optional[int] = None`
- [x] 1.2 `to_dict()`：`span_type == "ai_call"` 时序列化 `page_id`、`element_count`
- [x] 1.3 `from_dict()`：读取 `page_id`、`element_count`

## 2. 引擎提取 _record_metrics_as_spans

- [x] 2.1 从 `_step_once()` 提取 metrics→span 转换逻辑为 `_record_metrics_as_spans(metrics: Dict) -> None`
- [x] 2.2 覆盖 3 种 span 类型：ai_call（含 page_id/element_count）、execution、error
- [x] 2.3 `_step_once()` 替换内联代码为 `self._record_metrics_as_spans(metrics)`

## 3. VisionService ABC 加 last_call_metrics

- [x] 3.1 `VisionService` ABC 加 `last_call_metrics` 属性，默认返回 None
- [x] 3.2 文档说明：子类可选覆盖，预期字段 `provider_id`, `input_tokens`, `output_tokens`

## 4. 状态机提取 _build_ai_call_metrics

- [x] 4.1 新增 `_build_ai_call_metrics(page_analysis, elapsed_ms, vision) -> Dict` 方法
- [x] 4.2 处理有 PageAnalysis / None 两种情况
- [x] 4.3 读取 `getattr(vision, 'last_call_metrics', None)` 安全补充 provider 指标
- [x] 4.4 `_handle_precondition_check` 调用 `_build_ai_call_metrics`
- [x] 4.5 `_handle_result_verify` 调用 `_build_ai_call_metrics`

## 5. 修复 MockVisionService elements bug

- [x] 5.1 `_build_page_analysis`：`data.get("items", [])` → `data.get("elements", [])`

## 6. 测试

- [x] 6.1 `TestRecordMetricsAsSpans`：3 种 span 类型 + 空 metrics
- [x] 6.2 `TestBuildAICallMetrics`：有/无 PageAnalysis + vision.last_call_metrics 合并
- [x] 6.3 `TestVisionServiceLastCallMetrics`：默认 None + 子类覆盖
- [x] 6.4 `TestSpanNodeNewFields`：page_id/element_count 序列化/反序列化
- [x] 6.5 `TestMockVisionElementsFix`：analyze_screenshot 返回 PageAnalysis.items 非空

## 7. 验收验证

- [x] 7.1 `_record_metrics_as_spans()` 方法存在，3 种 span 类型覆盖
- [x] 7.2 `SpanNode` 支持 `page_id`、`element_count` 序列化/反序列化
- [x] 7.3 `isinstance(MockVisionService(), VisionService)` → `last_call_metrics` 返回 None
- [x] 7.4 `_build_ai_call_metrics(page_analysis, 100, vision)` 返回含 `page_id`/`element_count` 的 dict
- [x] 7.5 MockVisionService elements 修复后 `analyze_screenshot` 返回的 `PageAnalysis.items` 非空
- [x] 7.6 仿真运行后 trace 含 `ai_call` span，`page_id`/`element_count` 不为 None
- [x] 7.7 现有测试无回归
