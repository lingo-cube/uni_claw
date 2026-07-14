## Why

D-E4 定义了 7 个 ExpectedBehavior 验证维度，其中 5 个已实现（completion、pageCoverage、elementCoverage、collisionProof、dfsProperties），但 `operation_rules` 和 `trace_integrity` 两个维度一直是 TODO。这意味着测试能断言"遍历完了多少元素"，但无法断言"遍历过程是否健康"——无法检测死循环（同元素反复点击）、无法验证 DFS 栈规程（back before forward）、也无法确认 Trace 数据完整性。本次变更补齐这最后两块拼图，使 ExpectedBehavior 验证体系完整闭环。

## What Changes

- **新增 `OperationRulesExpectation` record**：2 字段 — `DepthFirstOrder`（DFS 栈规程检查：tap=push/back=pop，深度永不负数 + 至少一次回退）和 `NoDuplicateActionsMax`（同元素最大连续重复次数限制）
- **新增 `TraceIntegrityExpectation` record**：2 字段 — `RequiredSpanTypes`（Trace 中必须出现的 SpanType 集合）和 `MinPageTransitions`（最少页面跳转记录数）
- **ExpectedBehavior record 扩展**：主 record 新增 `OperationRules` 和 `TraceIntegrity` 两个可选参数（默认全关，缺失 JSON key 不产出 RuleResult）
- **Verify 方法扩展**：新增 `VerifyOperationRules` 和 `VerifyTraceIntegrity` 两个 private 方法，嵌入主 Verify 调度
- **引擎埋点修复**：`TraversalEngine.RunAsync()` 中 TraceRecord 创建处加 `_lastPageId` 跟踪，填充已有的 `PageFrom`/`PageTo`/`PageTransitionType` 字段（字段已存在，当前仅未填充）
- **基线 JSON 扩展**：`settings-full-traversal.json` 和 `settings-target-search.json` 加可选的 `operationRules` / `traceIntegrity` key
- **BREAKING — 无**：`ExpectedBehavior` 新参数有默认值向后兼容；对外接口（`IGraphTraversalEngine`/`IVisionProvider`/`IActionExecutor`）不变；无新 enum/接口方法

## Capabilities

### New Capabilities
_(无)_

### Modified Capabilities
- `expected-behavior`: 验证维度从 5+1 扩展到 7+1 —— 新增 `operation_rules`（2 规则：depth_first_order + no_duplicate_actions）和 `trace_integrity`（2 规则：span_types_present + page_transitions_recorded）；JSON schema 新增 `operationRules` 和 `traceIntegrity` 两个可选 key

## Impact

- **代码**: `src/UniClaw.Core/Simulation/ExpectedBehavior/`（ExpectedBehavior.cs +2 参数 +2 DTO、ExpectedBehavior.Verify.cs +2 方法、新增 OperationRulesExpectation.cs + TraceIntegrityExpectation.cs）、`src/UniClaw.Core/Traversal/TraversalEngine.cs`（+3 行 `_lastPageId` 跟踪）
- **测试**: 现有基线 JSON 加可选 key（向后兼容 — 不产出 RuleResult 若 key 缺失）；`settings-target-search.json` 的 `minPageTransitions` 按实际值设（target-search 深度优先到目标即停，页面跳转数可能 < 10）
- **依赖**: 无新增。SpanType enum 已有 11 值（5 个引擎 emit）；TraceRecord.PageFrom/PageTo/PageTransitionType 字段已存在（默认 null）
- **风险**: `settings-target-search` page_transitions 可能不够 10 → JSON 设较低值或不设。`SpanType` 反序列化 `Enum.Parse` 可能抛异常 → FromJson 中 try-catch safe default
- **详细设计**: 见 `docs/refactor/2026-07-13-execution-plan-digest-design.md`
