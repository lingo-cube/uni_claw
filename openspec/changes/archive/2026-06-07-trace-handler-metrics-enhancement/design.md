## Context

V6.5 handler → metrics → engine → span 链路已就绪。本变更提取重复代码、补齐 SpanNode 字段、加 AI provider 指标接口、修 MockVisionService 数据解析 bug。

**约束**：不改 `record_span` 签名（自动 parent_span_id）；SpanNode 向后兼容；VisionService ABC 最小接口变更。

## Goals / Non-Goals

**Goals:**
- 提取 `_record_metrics_as_spans` → `_step_once` 简化为 1 行调用
- 提取 `_build_ai_call_metrics` → 3 个 handler 消除重复
- SpanNode 支持 `page_id`/`element_count`
- VisionService ABC 暴露 `last_call_metrics`（默认 None，向后兼容）

**Non-Goals:**
- 不丰富 handler 业务逻辑（Phase B）
- 不实现真实 VisionService 的 `last_call_metrics`
- 不迁移 AI 模块 trace

## Decisions

### 1. 用强类型字段而非 data 字典
SpanNode 已有 `capability`/`action`/`status` 等具名字段。`_record_metrics_as_spans` 直接设置字段值，不引入 `data` 字典。
**理由**：强类型保证序列化一致性，type checker 可验证。

### 2. `_build_ai_call_metrics` 为实例方法
放 `TraversalStateMachine` 上，handler 通过 `self._build_ai_call_metrics(...)` 调用。
**理由**：handler 已有 `self` 引用，无需跨模块导入。

### 3. `last_call_metrics` 用属性而非 `analyze_screenshot` 返回值
不改 ABC 的 `analyze_screenshot` 签名字（返回 PageAnalysis）。另加 `@property`。
**理由**：不破坏现有实现，子类可选覆盖。

## Risks / Trade-offs

### Risk: MockVisionService elements fix 改变仿真行为
- **Mitigation**: 仅修改 `_build_page_analysis` 内部字段名，外部接口不变（仍返回 PageAnalysis）

### Risk: SpanNode 新字段序列化影响旧 trace 读取
- **Mitigation**: `from_dict` 用 `.get()` 安全读取，缺失值默认 None
