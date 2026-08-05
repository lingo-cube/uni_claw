## Context

日志零出口（裸 `Console.Error.WriteLine`，无级别/格式/trace 关联）；状态机异常只进 trace 事件流（`handle_error` span）、控制台不可见；stderr 易失导致 run 后无法排查（P3.1 教训）。trace/span 基础设施已就位：TraceId = runId（事件级注入）、`EngineStepSpanContext`（AsyncLocal，trace-parent-linkage 2.7）、`TraceSpanScope`（全部 span 的唯一生命周期封装：`BeginSpanAsync` 创建 / `DisposeAsync` 结束）、result.json `TracePath` 先例（读侧回退链）。

设计源 PRD：`docs/prd/2026-08-04-trace-correlated-logging-prd.md`（12 条决策，映射 log.md D-222~D-233）。

Constraints:
- `TraceFields` 45-key frozen catalog 不动——日志不进 trace.jsonl，日志与 trace 靠 id 关联。
- S1-S6 span-tree 快照门必须回归（D-3 ai.call parent 语义扩展直接冲击）。
- 既有 1083+ Core / 185+ Host 测试零构造签名变化（可选注入默认 NullLogger）。
- SourceGen Emitter 零改动（span 上下文同步点收敛 TraceSpanScope）。
- TraceTool CLI 提示格式不变（`trace: error: ...` 保留）。

## Goals / Non-Goals

**Goals:**
- 标准 `ILogger` 抽象 + 统一格式日志出口（stderr + run.log 双 provider）。
- 全 span 覆盖：任何 span 区域内日志取当前 span id。
- 状态机异常日志（FSM 路由、ErrorHandler 分类/fallback）+ Host 生命周期关键点。
- 存储 `trace/{runId}/run.log` + 对外告知（result.json `runLogPath` + 布局解析 + 配置）。
- S1-S6 回归 green（或经审查按新语义重冻结）。

**Non-Goals:**
- 日志事件化进 trace.jsonl（frozen catalog 不动）。
- TraceTool CLI 提示格式改造；既有裸 Console 调用渐进迁移（后续）。
- run.log 轮转/大小上限（后续）。
- 不引入 DI 容器 / `Microsoft.Extensions.Logging.Console` 包（自写轻量 provider，仅 Abstractions）。

## Decisions

### D-1 — span 上下文同步点 = `TraceSpanScope`（构造 push / Dispose pop），非 SourceGen Emitter
**Decision:** `TraceSpanScope` 是全部 span 的唯一生命周期封装；构造（spanId 非 null 时）push 到 `EngineStepSpanContext` 栈，`DisposeAsync` pop。`CreateNoOp()`（无 recorder）不 push。SourceGen 生成代码零变更。
**Rationale:** 一处改动全 span 覆盖；Emitter 不动则 S1-S6 回归面最小。`RecordEventAsync`（unpaired marker）不进栈——事件不产生"当前 span 区域"语义。
**Alternatives:** 改 `TraceHandlerGenerator.Emitter` 生成代码——拒绝：生成代码触碰所有 span 创建路径，回归面大且不必要。

### D-2 — `EngineStepSpanContext` 栈化（Push/Pop/栈顶读取），删除 TraversalEngine 显式 Set/Reset
**Decision:** `AsyncLocal<Stack<string?>>`（每 flow 独立）；`Push(string?)`/`Pop()`；`CurrentSpanId => 栈顶`（空栈 null）。保留静态单例与 `ITraceContextProvider` 契约。`TraversalEngine.cs:282/:541` 显式 `Set`/`Reset` 删除（stepScope 由 TraceSpanScope 自动管理，:541"run 后防悬挂"由 scope Dispose 天然保证）。
**Rationale:** 嵌套 span（handle_error 内 ai.call）需要保存/恢复语义；单值 Set/Reset 无法表达嵌套。
**Alternatives:** 保持单值 + 新增独立通道——拒绝：双通道语义割裂，栈化一个通道表达嵌套最简。

### D-3 — ai.call parent 从"仅 engine.step"扩展为"当前最内层 span"
**Decision:** `PageAnalyzer` 经 `ITraceContextProvider.CurrentSpanId`（栈顶）parent ai.call——任何 span 区域内（如 handle_error 内）发起的 AI 调用 parent 到当前最内层 span。
**Rationale:** 树结构更正确（错误处理中的 AI 调用属于错误处理 span）；代价是 S1-S6 快照需回归确认（§Risks 列回退方案）。
**Alternatives:** 保持"仅 engine.step"——日志仍可取栈顶但 AI parent 语义不变（保守）；选扩展因为语义正确且回归可审查。

### D-4 — 可选构造注入 `ILogger<T>? = null`（NullLogger 缺省）
**Decision:** 记录日志的类构造注入 `ILogger<T>? logger = null`，null → `NullLogger<T>.Instance`；组合根装配真实 logger。不引入静态 accessor / DI 容器。
**Rationale:** 标准 ILogger 抽象 + 既有测试零波及（默认参数）；控制台项目手写组合根（HostRunServices）下是最小侵入标准路径。
**Alternatives:** 静态 accessor——拒绝（反 DI，非标准）；必填注入——拒绝（波及全部构造调用点与测试）。

### D-5 — 自写 `TraceCorrelatedConsoleProvider`/`TraceCorrelatedFileProvider`，仅依赖 Abstractions
**Decision:** 两 provider 实现 `ILoggerProvider`/`ILogger`，输出格式 `[HH:mm:ss.fff] [t={TraceId}] [s={SpanId}] [LVL] {Category}: {message}`（Category 取短名 LastSegment）；console 写 stderr、file 写 `trace/{runId}/run.log`（同契约行格式）；provider 内 lock 串行化；异常堆栈缩进输出（Error/Critical 级）。
**Rationale:** 输出格式本需自定义（t=/s= 前缀）；避免 `Logging.Console` 包重依赖。文件 provider 每 run 创建（runId 隔离）。
**Alternatives:** 用 `Microsoft.Extensions.Logging.Console`——拒绝：格式需自定义且引入额外依赖。

### D-6 — 日志不进 trace.jsonl；日志与 trace 靠 id 关联
**Decision:** trace.jsonl 事件流不变（无 log 事件类型）；日志是文本诊断、trace 是结构化事件，两者靠 TraceId/spanId 交叉关联。
**Rationale:** TraceFields frozen catalog 不动；信息/物理分离原则（unified PRD §3）。
**Alternatives:** 日志事件化（log span type）——拒绝：frozen catalog + 事件流语义污染。

### D-7 — `UNICLAW_LOG_LEVEL` 命名独立
**Decision:** 级别 env `UNICLAW_LOG_LEVEL`（合法值 `trace|debug|information|warning|error|critical`，默认 information），命名独立于 `UNICLAW_VISION_MODE`/`UNICLAW_RUN_MODE` 族。
**Rationale:** P2.8 教训（一变量两义污染已两次）；新 env 独立命名并登记 integration-config §9.1 表。
**Alternatives:** 复用视觉模式变量——拒绝（正是 P2.8 病根）。

### D-8 — 日志要存储：`trace/{runId}/run.log`，分析器可查
**Decision:** run 目录留档日志文件，地址固定 `trace/{runId}/run.log`（V2 布局 trace 侧、与 trace.jsonl 同级）；run 入口创建、finally Flush+Close（异常路径也关闭句柄）。
**Rationale:** stderr 易失；trace-analyzer"运行日志补充取证"无址可查；P3.1 教训。trace/{runId}/ = 该 run 全部事件/诊断记录（assets/ = 字节资产侧）。
**Alternatives:** 只 stderr——拒绝（run 后不可查）；assets 侧（走 pipeline）——拒绝（流式文本 ≠ 批量 flush 资产，D-216 各入口自持）。

### D-9 — run.log 走旁路直接写（不经 ITracePipeline/FileAssetStore）
**Decision:** 文件 provider 直接写文件（流式追加），非资产管线产物；布局增补行声明（技术事实同步 unified PRD §3.1）。
**Rationale:** 流式追加文本与批量 flush 资产语义不同；D-216 写侧各入口自持——logger 自持路径不冲突。
**Alternatives:** 作为资产经 pipeline 提交——拒绝：批量语义与追加语义不符，且 finalize 前 flush 时序不可控。

### D-10 — 双 provider（console + file）注册同一 LoggerFactory
**Decision:** `LoggerFactory.Create(builder => SetMinimumLevel(...).AddProvider(console).AddProvider(file))`；同一格式契约（分析器同一正则解析）。
**Rationale:** 微软标准做法；职责单一；级别/格式单点配置。
**Alternatives:** 单 provider 双写——拒绝：混职责，文件生命周期管理复杂。

### D-11 — 对外告知 = result.json 新增 `RunLogPath` 字段
**Decision:** `RunResult` 新增 `RunLogPath`，finalize 写 `"runLogPath": "trace/{runId}/run.log"`（相对路径，对称 `TracePath` 先例）；schemaVersion 不 bump（字段级扩展，缺字段读侧回退默认）；V1 run 回退目标不存在 → 分析器得知"无日志"。
**Rationale:** 分析器读 run 元数据即知日志地址；`TracePath` 已有同类先例（读侧回退链）。统计类元数据（事件数等）不写——D-214 原则（统计属事件/日志域，不回写 run 元数据快照；重复存储漂移）。
**Alternatives:** 地址清单化（assetsDir/criteriaPath 全放）——拒绝：布局+runId 可解析（D-12），冗余漂移；事件统计入 result——拒绝（D-214）。

### D-12 — 读侧解析收敛 `RunLayoutV2`；写侧配置仅级别
**Decision:** `RunLayoutV2` 增加 run.log 布局常量/解析辅助；`TraceRunLoader` 解析链 `result?.RunLogPath ?? "trace/{runId}/run.log"`（同 TracePath 回退模式）。写侧配置：级别 `UNICLAW_LOG_LEVEL` + `integration.config.json` 新 `logging.level`（可选，测试装配注入 env，模式同 P2.4 visionServer；loader 校验合法值枚举 fail-fast）；落盘无开关（布局契约固定）。
**Rationale:** D-217 读侧 CLI 参数即配置 + 布局单点收敛；D-216 写侧各入口自持；测试可静音/开启 Debug 噪音。
**Alternatives:** run.log 路径配置化（可改路径）——拒绝：布局契约固定，配置化引入漂移。

## Risks / Trade-offs

- **[高] D-3 改变 ai.call parent 语义 → S1-S6 快照可能失败。** → Mitigation: 回归先行；树变化若仅由 parent 扩展引起（span 集合/字段不变），按既有快照重冻结流程（S5 refrozen 先例）；意外结构变化则回退 D-3 为"仅 engine.step parent + 日志读栈顶"（日志功能不受影响）。
- **[中] TraceSpanScope 改动触及所有 span 创建路径。** → Mitigation: 改动面收敛构造 + DisposeAsync 两处 + 单测覆盖 no-op/嵌套/异常路径；S1-S6 + 全量回归兜底。
- **[中] result.json `RunLogPath` 字段 = run 元数据 schema 扩展。** → Mitigation: 字段级扩展不 bump schemaVersion；读侧缺失回退默认；TraceRunLoader 容错测试覆盖旧 run（无字段）。
- **[低] 可选注入默认参数漏注入 → 静默 NullLogger。** → Mitigation: 组合根装配测试断言关键组件（FSM/Engine）收到非 NullLogger；漏注入只损失日志不损失功能。
- **[低] stderr 并发交错。** → Mitigation: provider lock 串行化单行写入。
- **[低] run.log 句柄泄漏（异常路径）。** → Mitigation: run 边界 finally Flush+Close；验收 10 断言异常路径句柄关闭。

## Migration Plan

| Step | What | Verify |
|------|------|--------|
| **M1** | Core: csproj 加 `Microsoft.Extensions.Logging.Abstractions`；`EngineStepSpanContext` 栈化（Push/Pop）；`TraceSpanScope` 构造 push / Dispose pop；删除 TraversalEngine :282/:541 显式 Set/Reset | 栈/嵌套/flow 单测；既有 span 测试 green |
| **M2** | Core: 新增 `RunTraceContext`（AsyncLocal runId，push/pop/Current）；`TraceCorrelatedConsoleProvider` + `TraceCorrelatedFileProvider` | provider 格式/级别/前缀单测 |
| **M3** | Core: `TraversalFSM`/`ErrorHandler` 接入 `ILogger<T>?`（L1-L3）；`TraversalEngine` step 级 Debug（L4） | FSM 异常日志测试（断言 t=/s=）；构造签名零变化 |
| **M4** | Host: 组合根 LoggerFactory 双 provider 装配；run 边界 RunTraceContext push/pop + 文件 provider 生命周期；L5-L8 日志点 | 组合根装配测试（非 NullLogger）；集成观察 `[t=runId]` |
| **M5** | Host: `RunResult.RunLogPath` 字段 + finalize 写出；TraceTool: `RunLayoutV2` 布局辅助 + `TraceRunLoader` 回退链；config `logging.level` + loader 校验 | RunResult 序列化测试；读侧回退容错测试（旧 run 无字段）；config 校验测试 |
| **M6** | 回归：S1-S6 快照门（D-3 审查/重冻结）+ 全量 Core/Host 测试 + trace-analyzer agent 文档 L3 增补 run.log 地址 | 全绿；快照审查结论记录 |

Dependencies: M1→M3（通道先于日志点）；M1→M2（栈先于 provider 取栈顶）；M4 依赖 M1/M2；M5 独立可并行；M6 最后。

Rollback: 每步独立提交；M1/M2 是纯 Core 增量（无行为变化直到 M3 消费）；M5 字段缺失读侧回退（旧读侧读新 run 无损）。

## Open Questions

无（PRD 已审阅确认；12 条决策闭环）。
