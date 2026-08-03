# Unit Test Status

**Project**: UniClaw (Core + Host)
**Version**: trace-parent-linkage (apply)
**Change**: trace-parent-linkage
**Task**: M0–M3 — TraceFields 字段目录、ai.call/ai.analyze 父链打通（AsyncLocal 通道）、TraceLevel 字段分级、验收与归档 spec 更新
**Generated**: 2026-08-03
**Git Branch**: feature/refactor
**Git Commit**: uncommitted (working tree, parallel local-vision-provider in flight)

---

## Executive Summary

trace-parent-linkage 全量落地（M0–M3，21/21 任务完成）：`TraceFields` 45 键常量目录（值冻结、业务代码零字面量）；`ai.call`/`ai.analyze` 父链挂到 `engine.step`（`ITraceContextProvider` + `EngineStepSpanContext` AsyncLocal 生产通道，4 调用点零签名改动，非引擎入口保留孤儿）；`SpanFieldProfile`/`TraceSpanFields` 分级描述符 + helper 层 level 过滤（Basic=核心、Detailed+=扩展，缺省 Detailed 与现状全量逐字节一致）。

全量测试 **1226/1235 通过**（1083 Core + 143 Host），0 失败；快照闸门 8/8（S1–S3 逐字节不变、S4 父链重冻结、S5 随并行 R-12 重冻结、S6 + NonEngineEntry 新增）。oracle 测试零 diff；AC1–AC7 验收矩阵全绿。

| Metric | Value |
|--------|-------|
| Total Tests | **1235** |
| Passed | **1226** |
| Failed | **0** |
| Error | **0** |
| Skipped | 9（emulator-gated + 既有 skip） |
| Build | 0 errors |
| New tests | 17（TraceFieldsTests 4 + SpanFieldLevelsTests 11 + S6/NonEngineEntry 2） |
| 快照闸门 | 8/8 |
| Oracle 零改动 | ✅（TraceSpanTests/TraceSpanTree/HandlerTraceWriter/InMemoryRecorder/ArchitectureGuard/PageAnalyzer/Traversal/SafetyGate/Analyzer/Baseline 零 diff） |

## Detailed Analysis

### AC 验收矩阵（M3）

| AC | 判定 | 证据 |
|----|------|------|
| AC1 快照闸门 | ✅ | 8/8：S1–S3 逐字节不变；S4 重冻结（ai.call parent=engine.step）；**S5 随并行 local-vision R-12 重冻结（53→70 行，用户裁决，3 次确定性复现 + Set/Reset 移除对照实验证明与本 change 无关）**；S6 完整父链含重试；NonEngineEntry 孤儿保留 |
| AC2 Oracle 零改动 | ✅ | git status 下 oracle 文件无 M；PageAnalyzer 可选 ctor 参数保证测试编译零改动 |
| AC3 无新脚手架 | ✅ | `StartSpanAsync/EndSpanAsync` 命中仅限 ITraceRecorder 声明、TraversalEngine passthrough、InMemoryTraceRecorder 实现 |
| AC4 目录与枚举冻结 | ✅ | `SpanType` enum 11 值（guard 绿）；TraceFields 45 键完整性测试绿；SpanTypes 本 change 未增删（并行 change +4：ai.yolo/ocr/fusion/scroll，无 guard 断言计数） |
| AC5 基线计数 | ✅ | 最终 Core 1083/2、Host 143/7；归因：M0 +4、M2 +11、M1 +2；+19 Core 差额来自并行 local-vision 测试 |
| AC6 分级缺省兼容 | ✅ | 缺省 Detailed 字段集与全量一致（S1–S6 快照 + 单元断言）；Basic 仅核心字段、None 空属性、profile=null 不过滤 |
| AC7 父链双向覆盖 | ✅ | S6（引擎入口 → parent=engine.step）+ NonEngineEntry（非引擎入口 → 保留孤儿根）同时绿 |

### 关键决策（apply 期，用户裁决）

1. **生产父链通道（2.7）**：D1 原"TraceCoordinator 实现 provider"在生产走不通（引擎 `Initialize()` 自建 coordinator 跟踪 step id；Host 组合根另建 `new TraceCoordinator(recorder)` 且 traceId=null → Active=false，双实例不相通）。用户裁决 **AsyncLocal 通道**：`EngineStepSpanContext`（静态单例 + `AsyncLocal<string?>`），引擎 step scope 开/合 Set/Reset，Host 注入 Instance；TraceCoordinator provider 实现移除。
2. **S5 快照冲突**：并行 change 的 R-12 滚动重试合法改变引擎行为。用户裁决立即重冻结（当前 70 行行为），AC1 措辞同步。
3. **M2 level 接线 inert（记录）**：`TraversalPlan.EntryConfig` 生产与测试均 null → `SpanTraceLevel ?? Detailed` 恒 Detailed。真正驱动引擎 span 分级需 ITraceCoordinator 接口变更（宪章级），留独立决策。

### 架构约束验证

- `ITraceRecorder` 9 声明方法不变（helper 全 additive）
- `ITraceCoordinator` 27 public 成员不变（显式接口实现 + internal 扩展）
- `IPageAnalyzer` 签名零改动；4 个 `AnalyzeCurrentPageAsync` 调用点零 diff（TraversalEngine/InterceptionHandler/TraversalFSM）
- 新增文件：`ITraceContextProvider`、`EngineStepSpanContext`、`TraceFields`、`SpanFieldProfile`（含 `TraceSpanFields`）；`InternalsVisibleTo("UniClaw.Host.Tests")`（fixture 访问 internal Set/Reset）

## Conclusions & Recommendations

- ✅ M0–M3 全部完成，AC1–AC7 全绿；span 树父子关系语义完整（`engine.run → engine.step → ai.call → ai.analyze`），非引擎入口观测不丢失
- ✅ 字段键名集中目录 + 按 TraceLevel 分级，为未来 `[TraceSpan]` source generator 提供 TSG002 字段校验输入
- ⏸️ **S5 重冻结的 R-12 归因**：local-vision-provider 验收时应知悉 S5 已在 trace-parent-linkage 内重冻结（70 行行为）；若 R-12 后续调整重试参数，S5 需再次重冻结
- ⏸️ **EntryConfig.TraceLevel 接线待决策**：当前 inert（恒 Detailed）；若需 Basic/None 生产生效，需宪章级决策（ITraceCoordinator 变更或 EngineStepSpanContext 通道扩展）
- Ready for `/opsx:archive`（trace-span-helpers 与 trace-parent-linkage 两个 change 均可归档）

---

*Report generated per validation-documentation skill standards. Data source: `.claude/skills/module-test/contracts/` (trace_unit.json, updated 2026-08-03).*
