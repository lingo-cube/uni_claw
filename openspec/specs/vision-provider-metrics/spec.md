## ADDED Requirements

### Requirement: VisionService ABC 新增 last_call_metrics 属性
`VisionService` ABC SHALL 提供 `last_call_metrics` 属性，默认返回 None。

#### Scenario: 默认实现返回 None
- **WHEN** `VisionService` 子类未覆盖 `last_call_metrics`
- **THEN** `instance.last_call_metrics` 返回 `None`

#### Scenario: 子类覆盖返回 provider 指标
- **WHEN** 子类覆盖 `last_call_metrics`
- **THEN** 返回 dict 可包含 `provider_id`, `input_tokens`, `output_tokens`

### Requirement: Handler 读取 vision.last_call_metrics
状态机 handler SHALL 在调用 `vision.analyze_screenshot()` 后通过 `getattr(vision, 'last_call_metrics', None)` 安全读取。

#### Scenario: 无额外指标时不补充
- **WHEN** `vision.last_call_metrics` 返回 None（默认）
- **THEN** `_build_ai_call_metrics` 返回的 dict 仅包含 handler 自身计算的字段

#### Scenario: 有额外指标时合并
- **WHEN** `vision.last_call_metrics` 返回 `{"provider_id": "claude", "input_tokens": 1000}`
- **THEN** `_build_ai_call_metrics` 返回的 dict 包含 `provider_id: "claude"` 和 `input_tokens: 1000`

### Requirement: last_call_metrics 同步更新
`vision.last_call_metrics` SHALL 在每次 `analyze_screenshot` 调用返回后反映当次调用的指标。

#### Scenario: 顺序调用指标隔离
- **WHEN** 连续调用 `analyze_screenshot` 两次
- **THEN** 每次调用后读取的 `last_call_metrics` 对应当次调用
