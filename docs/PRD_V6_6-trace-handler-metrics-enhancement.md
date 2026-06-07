# V6.6 Trace Handler Metrics 增强 PRD

**版本**: V6.6  
**日期**: 2026-06-07  
**依赖**: V6.5 state-machine-operation-integration（已完成）

---

## 1. 概述

V6.5 打通了 handler → metrics → engine → span 链路，但存在 4 个缺口：

1. metrics→span 转换内联在 `_step_once()` 14 行，未提取为独立方法
2. `ai_call` span 缺 `provider_id`/`input_tokens`/`output_tokens`，`VisionService` ABC 无暴露接口
3. 3 个 handler 中构建 `ai_metrics` 逻辑重复
4. `MockVisionService._build_page_analysis` 读 `data.get("items")`，但 `PageAnalyzer` 输出 `elements`

## 2. 已验证的设计前提

| 项目 | 结论 |
|------|------|
| SpanNode 字段格式 | 强类型（`capability`, `action`, `status`...），无 `data` 字典 |
| `record_span()` parent 关联 | 自动从 `StepTracker.get_parent_span_id()` 获取 |
| `last_call_metrics` 时效 | `analyze_screenshot` 同步返回后立即读取，无竞态 |

---

## 3. 改动设计

### 3.1 引擎：提取 `_record_metrics_as_spans()`

**文件**：`src/traversal/graph_engine.py`

**现状**：`_step_once()` 中 metrics→span 转换内联 14 行。

**目标**：提取为独立方法。

```python
def _record_metrics_as_spans(self, metrics: Dict) -> None:
    if not metrics:
        return
    if "ai_call" in metrics:
        ai = metrics["ai_call"]
        self.trace_recorder.record_span(SpanNode(
            span_type="ai_call",
            capability=ai.get("capability", "vision"),
            provider_id=ai.get("provider_id"),
            success=ai.get("success", True),
            latency_ms=ai.get("latency_ms", 0),
            input_tokens=ai.get("input_tokens"),
            output_tokens=ai.get("output_tokens"),
            page_id=ai.get("page_id"),
            element_count=ai.get("element_count"),
        ))
    if "execution" in metrics:
        ex = metrics["execution"]
        self.trace_recorder.record_span(SpanNode(
            span_type="execution",
            action=ex.get("action", "unknown"),
            status=ex.get("status", "success"),
            target=ex.get("target"),
            duration_ms=ex.get("duration_ms"),
        ))
    if "error" in metrics:
        err = metrics["error"]
        self.trace_recorder.record_span(SpanNode(
            span_type="error",
            error_type=err.get("error_type", "UnknownError"),
            error_message=err.get("error_message", ""),
            severity=err.get("severity", "error"),
        ))
```

`_step_once()` 中替换为 `self._record_metrics_as_spans(metrics)`。

### 3.2 SpanNode：新增 `page_id` / `element_count`

**文件**：`src/trace/models.py`

```python
@dataclass
class SpanNode(TraceNode):
    # ... existing fields ...
    page_id: Optional[str] = None
    element_count: Optional[int] = None
```

`to_dict()` 在 `span_type == "ai_call"` 时序列化这两个字段。`from_dict()` 同样处理。

### 3.3 VisionService ABC：新增 `last_call_metrics`

**文件**：`src/vision/vision_service.py`

```python
class VisionService(ABC):
    # ... existing methods ...

    @property
    def last_call_metrics(self) -> Optional[Dict[str, Any]]:
        """子类可选覆盖。返回 None 时忽略。预期字段: provider_id, input_tokens, output_tokens"""
        return None
```

### 3.4 状态机：提取 `_build_ai_call_metrics()`

**文件**：`src/state_machine/traversal_fsm.py`

```python
@staticmethod
def _build_ai_call_metrics(page_analysis, elapsed_ms: float, vision) -> Dict:
    metrics = {
        "capability": "vision",
        "success": page_analysis is not None,
        "latency_ms": elapsed_ms,
    }
    if page_analysis:
        metrics["page_id"] = "/".join(page_analysis.current_path) if page_analysis.current_path else None
        metrics["element_count"] = len(page_analysis.items) if page_analysis.items else 0
    extra = getattr(vision, 'last_call_metrics', None)
    if extra:
        metrics.update(extra)
    return metrics
```

3 个 handler（`_handle_precondition_check`, `_handle_execute`, `_handle_result_verify`）中调用：
```python
t0 = time.time()
analysis = vision.analyze_screenshot(b"")
elapsed = (time.time() - t0) * 1000
ai_metrics = self._build_ai_call_metrics(analysis, elapsed, vision)
self._last_handler_metrics = {"ai_call": ai_metrics, ...}
```

### 3.5 修复 MockVisionService elements bug

**文件**：`src/simulation/mock_vision.py`

```python
# _build_page_analysis 方法中
# 改前: items_data = page_data.get("items", [])
# 改后:
items_data = page_data.get("elements", [])
```

---

## 4. 文件变更

| 文件 | 变更 | 行数 |
|------|------|------|
| `src/traversal/graph_engine.py` | 提取 `_record_metrics_as_spans()`，删除内联代码 | +30/-14 |
| `src/trace/models.py` | SpanNode + `page_id`/`element_count` 字段 + to_dict/from_dict | +12 |
| `src/vision/vision_service.py` | ABC + `last_call_metrics` 属性 | +7 |
| `src/state_machine/traversal_fsm.py` | 新增 `_build_ai_call_metrics()`，3 handler 重构 | +30/-30 |
| `src/simulation/mock_vision.py` | `items` → `elements` | 1 行 |
| `tests/v6/test_v6_6_trace_handler_metrics.py` | 新测试文件 | ~30 |

---

## 5. 验收标准

1. `_record_metrics_as_spans()` 方法存在，3 种 span 类型覆盖
2. `SpanNode.page_id` / `SpanNode.element_count` 可序列化/反序列化
3. `isinstance(mock, VisionService)` → `mock.last_call_metrics` 返回 `None`（默认）
4. 自定义 `VisionService` 子类可覆盖 `last_call_metrics`
5. `_build_ai_call_metrics(page_analysis, 100, vision)` 返回含 `page_id`/`element_count` 的 dict
6. MockVisionService elements 修复后 `analyze_screenshot` 返回的 `PageAnalysis.items` 非空
7. 仿真运行后 trace 含 `ai_call` span，且 `page_id` / `element_count` 不为 None
8. 现有测试无回归

---

## 6. 测试（合并在单个文件 `tests/v6/test_v6_6_trace_handler_metrics.py`）

```
TestRecordMetricsAsSpans
  - 3 种 span 类型（ai_call / execution / error）
  - 空 metrics 不报错
  - ai_call 含 page_id / element_count

TestBuildAICallMetrics
  - 有 PageAnalysis → page_id + element_count
  - PageAnalysis 为 None → success=False
  - vision 覆盖 last_call_metrics → 字段合并

TestVisionServiceLastCallMetrics
  - 默认 VisionService → last_call_metrics is None
  - 子类覆盖 → 返回值正确

TestSpanNodeNewFields
  - page_id / element_count 序列化/反序列化

TestMockVisionElementsFix
  - analyze_screenshot 返回的 PageAnalysis.items 非空
```

---

## 7. 不在此 PRD 范围

- `_handle_frame_complete_state` action 调用实现
- `_handle_precondition_check` 自循环（关系驱动纠正）
- `_handle_branch` 决策 Span
- AI 模块 trace 迁移（`src/ai/trace/` → `src/trace/`）
- 真实 VisionService 的 `last_call_metrics` 实现
