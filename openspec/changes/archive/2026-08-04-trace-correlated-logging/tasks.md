## 1. M1 — Core: 依赖 + 栈式 span 通道（D-1/D-2）

- [x] 1.1 `src/UniClaw.Core/UniClaw.Core.csproj` 新增 `<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />`；确认 Host/TraceTool 经传递引用可获得（不直接引包）
- [x] 1.2 `EngineStepSpanContext` 栈化：`AsyncLocal<Stack<string?>>`、`Push(string?)`/`Pop()`、`CurrentSpanId => 栈顶`（空栈 null）；保留静态单例与 `ITraceContextProvider` 契约
- [x] 1.3 `TraceSpanScope` 构造（spanId 非 null 时）push、`DisposeAsync` pop；`CreateNoOp()` 不 push
- [x] 1.4 删除 `TraversalEngine.cs:282/:541` 显式 `Set`/`Reset`（stepScope 由 TraceSpanScope 接管；:541"run 后防悬挂"由 scope Dispose 保证）
- [x] 1.5 测试：栈化 push/pop 嵌套（A→B→读=B→pop→读=A）、AsyncLocal flow（Task.Run 内可见/外层不污染）、空栈 null；`TraceSpanScope` 集成（BeginSpanAsync → CurrentSpanId==spanId → dispose → 回退父 span；CreateNoOp 不改变当前 span）

## 2. M2 — Core: provider + RunTraceContext（D-4/D-5/D-7/D-10）

- [x] 2.1 新增 `RunTraceContext`（AsyncLocal runId，`Push`/`Pop`/`Current`，空 null）
- [x] 2.2 新增 `TraceCorrelatedConsoleProvider`（stderr，格式 `[HH:mm:ss.fff] [t={TraceId}] [s={SpanId}] [LVL] {Category}: {message}`，Category 短名；t= 读 RunTraceContext、s= 读 EngineStepSpanContext 栈顶、缺省 `-`；异常堆栈缩进仅 Error/Critical；lock 串行化）
- [x] 2.3 新增 `TraceCorrelatedFileProvider`（写指定文件路径，行格式与 console 同契约；`Flush`/`Close` 幂等）
- [x] 2.4 级别门控：`UNICLAW_LOG_LEVEL`（合法值 `trace|debug|information|warning|error|critical`，默认 information）→ `LoggerFactory` `SetMinimumLevel`
- [x] 2.5 测试：provider 格式正则断言（t=/s=/级别标签/类别短名）、级别过滤、无上下文 `-`、异常堆栈、file provider 目录自动创建 + 幂等关闭

## 3. M3 — Core: 状态机/引擎日志点（L1-L4）

- [x] 3.1 `TraversalFSM.step()` 接入 `ILogger<T>? = null`：catch DomainValidation（:94）Warning 级 + catch 通用（:120）Error 级（异常类型/消息 → 路由到 ErrorHandling）
- [x] 3.2 `ErrorHandler.HandleError` 接入 `ILogger<T>? = null`：分类结果 Information（classified/strategy/retryCount）+ pipeline fallback（:212）Error 完整异常
- [x] 3.3 `TraversalEngine` step 开/关 Debug 级日志（L4，Debug 门控）
- [x] 3.4 测试：FSM 注入异常 → 断言日志含 `[t={runId}]` + `[s={step spanId}]`；ErrorHandler fallback 断言含完整异常类型 + handle_error span id；既有构造签名零变化（默认参数）

## 4. M4 — Host: 组合根 + run 生命周期日志（L5-L8）

- [x] 4.1 Host run 入口创建 `LoggerFactory`（console + file 双 provider，SetMinimumLevel(UNICLAW_LOG_LEVEL)）；run 边界 `try { RunTraceContext.Push(runId) ... } finally { Pop() }`
- [x] 4.2 文件 provider 生命周期：run 开始创建（路径 `trace/{runId}/run.log`，先建目录）、finally Flush+Close（异常路径也必须关闭）；每 run 一个文件
- [x] 4.3 注入真实 logger 到 FSM/Engine 组件（构造参数传 LoggerFactory.CreateLogger<T>()）；组合根装配测试断言关键组件收到非 NullLogger
- [x] 4.4 日志点：run 开始 Information（runId/mode/provider）、run 结束 Information（runId/status/duration）、资产提交失败 Error（与 `asset_write_failed` issue 同步处，相对路径 + 异常）、run 终态 Information（status + 引擎事实摘要）
- [x] 4.5 测试：集成观察 stderr 含 `[t={runId}]`（runId 与 trace 目录名一致）

## 5. M5 — 对外告知 + 配置（D-11/D-12）

- [x] 5.1 `RunResult` 新增 `RunLogPath` 字段 + finalize 写出 `"runLogPath": "trace/{runId}/run.log"`（相对路径；schemaVersion 不 bump）；RunResult 序列化测试
- [x] 5.2 `RunLayoutV2` 增加 run.log 布局常量/解析辅助（`RunLogRelativePath` 或等价）
- [x] 5.3 `TraceRunLoader` 解析链：`result?.RunLogPath ?? "trace/{runId}/run.log"`（同 TracePath 回退模式）；容错测试（旧 run 无字段 → 回退默认；V1 run 无文件 → "无日志"）
- [x] 5.4 `integration.config.json` 新增 `logging` 段（`{ "logging": { "level": "..." } }`，可选）+ loader 校验合法值枚举 fail-fast + 测试装配注入 `UNICLAW_LOG_LEVEL`（模式同 visionServer env 注入，P2.4）；config 校验测试
- [x] 5.5 unified PRD §3.1 布局图 run.log 行已同步（技术事实修订，语言保持原样）——确认

## 6. M6 — 回归 + 文档（D-3）

- [x] 6.1 S1-S6 span-tree 快照门回归：审查树变化是否仅由 D-3 parent 扩展引起（span 集合/字段不变）；按需按新语义重冻结快照（S5 refrozen 先例），审查结论记录
- [x] 6.2 全量测试回归：Core 1083+ / Host 185+ 全绿（既有测试零改动）
- [x] 6.3 trace-analyzer agent 文档 L3 产物层增补 run.log 地址（`trace/{runId}/run.log`，result.json `runLogPath` 可查）
- [x] 6.4 决策录 log.md：D-222~D-233（PRD D-1~D-12 按序映射）

## Design Docs

> 由 proposal Impact 段生成。实现代理：开始前先读这些。

| 模块 | 设计文档 |
|------|----------|
| `src/UniClaw.Core/Observability/` | docs/prd/2026-08-04-trace-correlated-logging-prd.md（设计源，12 条决策） |
| `src/UniClaw.Core/StateMachine/` + `Traversal/` | docs/prd/2026-08-04-trace-correlated-logging-prd.md §5（L1-L4 日志点） |
| `src/UniClaw.Host/` | docs/prd/2026-08-04-trace-correlated-logging-prd.md §4.5-4.7（组合根/存储/告知） |
| `src/UniClaw.TraceTool/` | docs/prd/2026-08-04-trace-correlated-logging-prd.md §4.7（读侧解析） |
| `tests/` | docs/prd/2026-08-04-trace-correlated-logging-prd.md §7（验证） |
| 关联 | openspec/specs/trace-pipeline + run-layout-v2 + run-metadata-enrichment（V2 布局/元数据契约） |
