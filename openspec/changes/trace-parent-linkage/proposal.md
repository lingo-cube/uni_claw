# Proposal: trace-parent-linkage

## Why

`ai.call`/`ai.analyze` 是 span 树中仅存的孤儿根——D-134 P3 以"跨层无通道"为由留下它们，但实际调用链（4 个调用点全在 engine.step 上下文内）可携带 parent_id，`entry.visited` 已在用同一通道；同时 span 属性键名是散落的手写字符串（无目录、防不了漂移），且 `TraceLevel`（None/Basic/Detailed/Full）只控制事件开关、不能按字段粒度分级。父链完整化 + 字段常量化/分级化后，span 树语义完整、观测可按级别裁剪，也为未来 `[TraceSpan]` source generator 提供字段校验输入。

## What Changes

- **父链打通**：`ai.call`/`ai.analyze` 的 parent 变为当前 `engine.step` span id——通过新增 `ITraceContextProvider`（由 `TraceCoordinator` 实现）注入 `PageAnalyzer`，`AnalyzeCurrentPageAsync` 调用处零签名改动；非引擎入口（拿不到 parent）**保留孤儿**（根 span 继续记录，向后兼容）。目标树：`engine.run → engine.step → ai.call → ai.analyze`。
- **字段目录 `TraceFields`**：全部 span 属性键名集中为静态常量（`ai.provider_id`、`action.adb_ms` 等），常量值不变（JSONL 持久化字段兼容），业务代码引用常量。
- **字段分级（`TraceLevel` 门控）**：新增每 spanType 字段分级描述符（Basic=核心结果、Detailed+=耗时/计费类细节），helper 层按级别过滤扩展键，缺省级别行为与现状全量记录一致（向后兼容）。
- **快照闸门更新**：`SpanTreeEquivalenceTests` S4 重冻结（`ai.call` 从根变为 `engine.step` 子节点）；新增 S6 父链场景（`engine.run → engine.step → ai.call → ai.analyze`，含重试路径 `ai.retry_count`）；S1–S3/S5 快照必须 unchanged。

## Capabilities

- **Modified Capabilities**: `trace-span`（`openspec/specs/trace-span/spec.md`）— 父链归属、字段目录、字段分级是对现有 span 记录行为的要求变更，需要 delta spec。

## Impact

- **代码**：`src/UniClaw.Core/UniBrain/PageAnalyzer.cs`（注入 provider，`ai.call` parent）、`src/UniClaw.Core/Traversal/TraversalEngine.cs`（`ITraceContextProvider` 实现 + internal 通道）、新增 `TraceFields`/字段分级描述符（helper 层，`ITraceRecorder` 9 方法 guard 不受影响）、`src/UniClaw.Host/Analysis/*` 与 `SafetyGate`（字段键引用改常量，仅键名常量替换）。
- **测试**：`SpanTreeEquivalenceTests`（S4 重冻结 + 新增 S6）、新增字段目录/分级测试；oracle 套件零改动原则保持。
- **不触碰**：`SpanType` enum（11 值）、`SpanTypes` 目录（18 常量）、`ITraceCoordinator` public 27 成员、`ITraceRecorder` 接口 9 方法、AC3 白名单（同步 passthrough 接缝）。
- **依赖**：`trace-span-helpers`（已归档，helper 与 AC1–AC6 框架复用）。
