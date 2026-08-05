# trace-analyzer 精简知识

> 从 L1–L4 层文档蒸馏。每条 1–3 句，结论必须仍可溯源到层文档。来源文档更新时按 INDEX.md 刷新规则重精简。

## L1 观测层 — span 语义

- SpanType 11 值：DfsForward·DfsBacktrack·RestoreOp·SkipDangerous·PopupHandling·ContainerHandling·ErrorHandling·PageAnalysis·CacheOp·AICall·StateDecision（完整语义见 observability.md）
- span 树：engine.run → engine.step → ai.call → ai.analyze；TraceContext 信封 = NodeId/StepSpanId/StepNumber/TraceId/VisitSpanId
- JSONL 每行首字段 record_type 判别；**只有 `record_type=="span"` 的行是 span**（execution/transition/error/ai_call/session 不是）；坏行静默跳过（D-93，无 stderr 警告）

## L2 运行层 — 状态机

- TraversalState 8 值 + GlobalState 8 值 + CompletionReason 4 值（全表见 state-machine.md）
- entry.visited / entry.skipped 产生时机；skip_dangerous；error loop 根源 = 状态不前进

## L3 产物层 — run 目录

- 布局 `{outputRoot}/{scenarioId}/{runId}/`：manifest.json + result.json + trace/{runId}/trace.jsonl + steps/D4/ + analysis.jsonl（D-197 页面分析快照）
- TracePath 双格式：最终 `trace/{runId}/trace.jsonl` vs 中断占位 `trace/trace.jsonl`
- result.json 字段全必填（schemaVersion/runId/status/completionReason/discoveredEntries/visitedEntries/skippedEntries/failedEntries/actionsAttempted/actionsSucceeded/safetyAllowed/safetyDenied/stepsConsumed/scrollsConsumed/durationMs/tracePath/issueFingerprints/successCriteriaSatisfied/successEvidence/updatedAt）
- **关键机制**：TraceRunLoader 无 result.json 时默认 "trace/trace.jsonl" 会被拆 2 段 → `{runDir}/trace/trace.jsonl/trace.jsonl` 找不到文件；裸 trace 必须带完整最小 result.json（tracePath 指向实际位置）才能分析——临时 run 目录构造法实测可行

- **run.log** — V2 布局 `trace/{runId}/run.log`，`TraceCorrelatedFileProvider` 写入。日志格式：`[HH:mm:ss.fff] [t=<runId>] [s=<spanId>] [LEVEL] Category: message`。Info 级别可见 FSM 转换（`TraversalFSM: FSM From→To step=N`）、操作执行（`SafeActionExecutor: action=X result=Y`）、页面分析摘要（`InvalidatingPageAnalysisCache: page=X items=N`）、引擎终止原因（`TraversalEngine: Engine terminated reason=X`）。查询：`grep "s=<spanId>" run.log`（精确关联 trace span）、`grep "\[ERROR\]" run.log`（严重级）、`grep "→ deny" run.log`（安全门拒绝）。与 trace.jsonl 互补——trace 有 span 树结构，run.log 有运行时语义线索。

## L4 分析层 — TraceTool

- 6 子命令：list/timeline/diagnose/diff/report/interactive；`--format json` stdout 纯净（schemaVersion "1"），日志在 stderr；非 TTY 自动转 JSON
- 退出码：0 成功 / 1 diff 差异 / 2 用法错误或 run 不存在 / 3 空 trace（GetAllSpans 判空门，TraceCommands.cs；文件读不到是 2，读到但 0 span 是 3）
- error_loop_stuck ← ErrorLoopAnalyzer(null) 离线复用：stuck_in_error_loop（≥5 连续全跳过）/ skip_rate_too_high（skipped>visited×4）→ cause "error_loop_stuck"；IssueFingerprints 透传 evidence
- diagnose 的 run 上下文带 taskId/purpose/system（ADB getprop）/machine
