## Why

V6.5 打通了 handler → metrics → engine → span 链路，但 metrics→span 转换逻辑内联在 `_step_once()` 中，3 个 handler 的 `ai_call` metrics 构建逻辑重复，`ai_call` span 缺少 `provider_id`/`input_tokens`/`output_tokens`，且 `MockVisionService._build_page_analysis` 存在数据解析 bug。

## What Changes

- 引擎提取 `_record_metrics_as_spans()` 独立方法（替换 `_step_once()` 内联 14 行）
- `SpanNode` 新增 `page_id`、`element_count` 可选中字段
- `VisionService` ABC 新增 `last_call_metrics` 属性（默认 None，子类可选覆盖）
- 状态机提取 `_build_ai_call_metrics()` 辅助函数，3 个 handler 统一调用
- 修复 `MockVisionService._build_page_analysis`：`items` → `elements`

## Capabilities

### New Capabilities

- `metrics-span-extraction`: 引擎 `_record_metrics_as_spans()` 将 handler metrics 统一转换为 SpanNode
- `vision-provider-metrics`: VisionService ABC 暴露 `last_call_metrics` 属性，handler 读取补充 provider 级指标
- `ai-metrics-builder`: 状态机 `_build_ai_call_metrics()` 统一构建 ai_call metrics

## Impact

**Affected Code**:
- `src/traversal/graph_engine.py` — 提取 `_record_metrics_as_spans()`
- `src/trace/models.py` — SpanNode + `page_id`/`element_count`
- `src/vision/vision_service.py` — ABC + `last_call_metrics`
- `src/state_machine/traversal_fsm.py` — `_build_ai_call_metrics()` + 3 handler 重构
- `src/simulation/mock_vision.py` — `items` → `elements` bug fix

**New Dependencies**: 无

**API Changes**: `VisionService` ABC 新增可选属性（向后兼容）
