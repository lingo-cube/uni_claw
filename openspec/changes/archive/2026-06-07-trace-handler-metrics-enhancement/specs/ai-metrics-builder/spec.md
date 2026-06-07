## ADDED Requirements

### Requirement: 状态机提取 _build_ai_call_metrics 辅助函数
`TraversalStateMachine` SHALL 提供 `_build_ai_call_metrics(page_analysis, elapsed_ms, vision) -> Dict` 静态/实例方法，3 个 handler 统一调用。

#### Scenario: 有页面的正常结果
- **WHEN** `page_analysis` 是有效 `PageAnalysis`，`current_path=["home","settings"]`, `items` 含 3 个元素
- **THEN** 返回 dict 含 `page_id: "home/settings"`, `element_count: 3`, `success: True`, `latency_ms: <elapsed_ms>`

#### Scenario: 页面分析为 None
- **WHEN** `page_analysis` 为 None
- **THEN** 返回 dict 含 `success: False`, `page_id: None`, `element_count: None`

#### Scenario: 补充 vision.last_call_metrics
- **WHEN** `vision.last_call_metrics` 返回 `{"provider_id": "deepseek", "input_tokens": 500}`
- **THEN** 返回 dict 含 `provider_id: "deepseek"`, `input_tokens: 500`

### Requirement: 3 个 handler 调用 _build_ai_call_metrics
`_handle_precondition_check`、`_handle_execute`、`_handle_result_verify` SHALL 调用 `_build_ai_call_metrics` 替换重复的 inline 构建逻辑。

#### Scenario: _handle_precondition_check 调用
- **WHEN** vision 分析完成
- **THEN** 调用 `ai_metrics = self._build_ai_call_metrics(page_analysis, elapsed_ms, vision)`
- **AND** `self._last_handler_metrics = {"ai_call": ai_metrics}`

#### Scenario: _handle_result_verify 调用
- **WHEN** vision 验证完成
- **THEN** 调用 `ai_metrics = self._build_ai_call_metrics(after_analysis, elapsed_ms, vision)`
- **AND** `self._last_handler_metrics = {"ai_call": ai_metrics}`

### Requirement: 修复 MockVisionService elements 解析
`MockVisionService._build_page_analysis` SHALL 从 `page_data` 读取 `"elements"` 键而非 `"items"` 键。

#### Scenario: elements 正确解析
- **WHEN** `PageAnalyzer.analyze_page("home")` 返回 `{"elements": [{...}]}`
- **AND** `_build_page_analysis` 处理该数据
- **THEN** 返回的 `PageAnalysis.items` 非空，包含对应元素

#### Scenario: 空 elements 不报错
- **WHEN** `page_data` 不含 `"elements"` 键
- **THEN** 返回的 `PageAnalysis.items` 为空列表，不抛异常
