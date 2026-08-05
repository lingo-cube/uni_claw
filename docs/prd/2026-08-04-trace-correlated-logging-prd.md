# TraceCorrelated Logging PRD — 统一日志出口 + trace/span 关联

> 生成: 2026-08-04 · 状态: 设计待审 · 语言: 中文（代码标识符保留原文）
> 关联: [trace-parent-linkage 设计](2026-08-03-trace-parent-linkage-design.md) · [unified 资产管线 PRD](2026-08-04-unified-asset-pipeline-trace-validation-prd.md) · 决策日志 [log.md](../system/decisions/log.md)（本 PRD 决策预留 D-222 起）

## 1. 背景与问题

现状（2026-08-04 代码事实）：

- **日志零出口**：全仓库无 `ILogger` 使用，日志为裸 `Console.Error.WriteLine`（Core 5 处、Host 12 处、TraceTool 13 处），无统一格式、无级别、无结构化字段。
- **状态机异常不可见**：Core `StateMachine/`（`TraversalFSM` step catch 路由、`ErrorHandler` 分类与 pipeline fallback）异常**只进 trace 事件流**（`[TraceHandler(SpanType.ErrorHandling, "handle_error")]` → trace.jsonl 有 `handle_error` span），控制台零文本输出——ErrorHandler 的分类结果（errorType/strategy/retryCount）与 pipeline fallback（未处理异常 → Abort）在排查时不可见。
- **trace/span 关联基础设施已就位但日志未接入**：
  - TraceId = runId（Host 装配期注入 `TraceContext(TraceId: runId)`）；
  - `EngineStepSpanContext`（AsyncLocal）在引擎 step scope 内可见当前 span id（trace-parent-linkage 2.7）；
  - `TraceSpanScope` 是所有 span 的唯一生命周期封装（`BeginSpanAsync` 创建、`DisposeAsync` 结束）。
- **P3.1 教训**（integration-pipeline-issues）：hook 异常被静默吞掉、失败现场无留痕——日志是"失败可查"的第一道防线。

## 2. 目标 / 非目标

**目标：**

- 引入标准 `ILogger` 抽象（`Microsoft.Extensions.Logging.Abstractions`），Core/Host 关键路径接入日志。
- 每条日志行携带 **TraceId（runId）与当前 span id**，格式统一，日志与 trace.jsonl 事件流可按 id 交叉关联。
- **全 span 覆盖**：任何 span 区域内（含 SourceGen 生成的 `handle_error`、`step`、`ai.call` 等）的日志都能取到当前 span id，实现点收敛在 `TraceSpanScope`，SourceGen 零改动。
- 重点补充**状态机异常日志**（FSM 异常路由、ErrorHandler 分类/fallback）+ Host run 生命周期 + 资产提交失败。
- **日志随 run 留档**：`trace/{runId}/run.log` 固定地址，trace-analyzer"运行日志补充取证"有址可查（§4.6）。
- 现有测试零波及（可选注入默认 `NullLogger`）。

**非目标：**

- 不把日志事件化进 trace.jsonl——TraceFields 45-key frozen catalog 不动；日志是文本诊断，trace 是结构化事件，两者靠 id 关联而非合并。
- 不改 TraceTool CLI 提示格式（`trace: error: ...` 保留）。
- 不引入 DI 容器 / `Microsoft.Extensions.Logging.Console` 包（自写轻量 provider，仅依赖 Abstractions 接口包）。
- 不迁移既有裸 `Console.WriteLine` 的语义（CLI 用户提示保留原样）；只接管"诊断日志"语义的调用点。

## 3. 设计总览

```
组合根 (Host)
  └─ LoggerFactory.Create(...)          ← UNICLAW_LOG_LEVEL 门控
       ├─ TraceCorrelatedConsoleProvider（Core.Observability）
       │    └─ stderr: [HH:mm:ss.fff] [t={runId}] [s={spanId}] [LVL] {Category}: {message}
       └─ TraceCorrelatedFileProvider（同格式）
            └─ trace/{runId}/run.log    ← 分析器查询地址（§4.6）
                          ↑                     ↑
                     RunTraceContext     EngineStepSpanContext（栈式）
                     (AsyncLocal runId)  (push/pop ← TraceSpanScope 接管)
业务代码
  ├─ FSM: ILogger<T>?（可选注入）→ NullLogger 缺省
  ├─ Engine: 同（step 级 Debug 日志）
  └─ Host run: ILogger<T> 装配 + run 边界 push/pop RunTraceContext
```

## 4. 组件设计

### 4.1 依赖

- `src/UniClaw.Core/UniClaw.Core.csproj` 新增 `<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />`（仅接口 + NullLogger + LoggerExtensions，无运行时重依赖）。
- Host/TraceTool 经 Core 传递引用获得（不直接引包）。

### 4.2 `TraceCorrelatedConsoleProvider`（Core.Observability，新增）

实现 `ILoggerProvider` + 内部 `ILogger`，输出 **stderr**：

```
[HH:mm:ss.fff] [t={TraceId}] [s={SpanId}] [LVL] {Category}: {message}
{Exception 堆栈（缩进，仅 Error/Critical 级时）}
```

- **TraceId**：`RunTraceContext.Current`（§4.4）；无 run 上下文输出 `-`。
- **SpanId**：`EngineStepSpanContext.Instance.CurrentSpanId`（栈顶）；无当前 span 输出 `-`。
- **级别**：原生 `LogLevel`（Trace/Debug/Information/Warning/Error/Critical）；最低级别由组合根 `LoggerFactory` `SetMinimumLevel` 控制，env `UNICLAW_LOG_LEVEL`（合法值 `trace|debug|information|warning|error|critical`，默认 `information`）。命名避开 `UNICLAW_VISION_MODE`/`UNICLAW_RUN_MODE` 族（P2.8 教训，§9.1 表同族登记）。
- **类别**：`ILogger<T>` 的 `T` 全名（`UniClaw.Core.StateMachine.ErrorHandler`），格式简短化（provider 内只取 `LastSegment`：`ErrorHandler`）。
- 无 recorder 依赖、无异步（同步写 stderr，`lock` 串行化防交错）。

### 4.3 栈式 span 通道（EngineStepSpanContext 改造 + TraceSpanScope 接管）

**实现点（关键）：** `TraceSpanScope` 是全部 span 的唯一生命周期封装（`BeginSpanAsync` 创建、`DisposeAsync` 结束），在**这里**同步 span 上下文，即可全 span 覆盖——**SourceGen Emitter 零改动**：

- `TraceSpanScope` 构造（spanId 非 null 时）→ `EngineStepSpanContext.Instance.Push(spanId)`；
- `TraceSpanScope.DisposeAsync` → `Pop()`。
- `TraceSpanScope.CreateNoOp()`（无 recorder）不 push（保持 no-op 语义）。

**EngineStepSpanContext 栈化：**

- `AsyncLocal<Stack<string?>>`（或等价的每-flow 独立栈）；`Push(string?)` / `Pop()`；`CurrentSpanId => 栈顶`（空栈 null）。
- 保留静态单例 `Instance` 与 `ITraceContextProvider` 契约（PageAnalyzer 读栈顶——ai.call parent 逻辑不变）。
- `TraversalEngine.cs:282/:541` 的显式 `Set`/`Reset` **删除**（stepScope 由 `TraceSpanScope` 自动 push/pop，语义等价；:541 注释"run 后 Reset 防悬挂"由 scope Dispose 天然保证）。

**语义变化（记录在案）：** 原 `EngineStepSpanContext` 只 Set 引擎 step span → ai.call 只 parent 到 `engine.step`；栈化后任何 span 区域内（如 `handle_error` 内发起的 AI 调用）都会 parent 到当前最内层 span——**树结构更正确**（错误处理中的 AI 调用属于错误处理 span），但影响 S1-S6 span-tree 快照门（§8 回归项）。

**`RecordEventAsync`（unpaired marker）不进栈**：point-in-time 事件不产生"当前 span 区域"语义。

### 4.4 `RunTraceContext`（Core.Observability，新增）

AsyncLocal 单值通道，语义同 `EngineStepSpanContext` 模式：

- `static RunTraceContext`：`Instance.Push(string runId)` / `Pop()` / `Current`（空 null）。
- **Host 组合根**在 run 边界（locate/enumerate run 入口）`try { Push(runId); ... } finally { Pop(); }`——引擎/FSM 内日志取 runId 不经参数传递。
- 与 TraceId = runId 的既有事实对齐（§1）；无 run 上下文（CLI/测试）输出 `-`。

### 4.5 ILogger 注入形态（可选构造注入）

- 记录日志的类：**构造注入 `ILogger<T>? logger = null`**，`null` → `NullLogger<T>.Instance`——默认参数使既有调用点/测试**零波及**；组合根（`HostRunServices` 装配）传入真实 logger。
- 组合根装配：`LoggerFactory` 单例创建于 Host run 入口（`LoggerFactory.Create(builder => builder.SetMinimumLevel(...).AddProvider(new TraceCorrelatedConsoleProvider()).AddProvider(new TraceCorrelatedFileProvider(runLogPath)))`），按类注入。
- 不引入静态 accessor（用户要求标准 ILogger 抽象；可选注入是标准与零波及的平衡点）。

### 4.6 存储与查询地址（分析器契约）

**决策（2026-08-04 确认）：要存储。** stderr 易失——trace-analyzer agent 的定位包含"运行日志补充取证"，但 run 结束后（常是数分钟/小时后）stderr 已不可查；P3.1 教训即"失败现场不可查"。日志必须随 run 目录留档。

**查询地址（分析器契约）：`trace/{runId}/run.log`** —— V2 布局 `trace/{runId}/`（event-stream 侧）与 `trace.jsonl` 同级。理由：

- V2 语义：`trace/{runId}/` = 该 run 全部事件/诊断记录；`assets/{runId}/` = 字节资产（pipeline 批量 flush + staging 原子写）。run.log 是**流式追加的文本诊断**，非资产字节——不经过 `ITracePipeline`/`FileAssetStore`（旁路直接写，D-216 写侧各入口自持不受影响），放 trace 侧而非 assets 侧。
- 分析器 L3 产物层在 run 目录一处拿全：`trace/{runId}/trace.jsonl` + `trace/{runId}/run.log`；TraceRunLoader 扫描该目录时天然可见。
- run 根是 Host metadata 区（manifest/result/criteria），日志不属于 metadata。

**布局增量声明**：本 PRD 向 V2 布局（unified PRD §3.1）增补一行——`trace/{runId}/run.log ← trace-correlated logging（文本诊断，非资产，流式追加）`；该行同步补入 unified PRD 布局图（技术事实修订，语言保持原样）。

**写入实现（双 provider，微软标准）**：同一 `LoggerFactory` 注册两个 provider——

- `TraceCorrelatedConsoleProvider`：stderr（§4.2，实时可见）；
- `TraceCorrelatedFileProvider`：写 `trace/{runId}/run.log`，行格式与 console **相同契约**（分析器按同一正则解析）。

**文件生命周期（Host 组合根）**：run 入口创建文件 provider（路径 = run 目录 + `trace/{runId}/run.log`，先建目录），`finally` 中 `Flush + Close`（异常路径也必须关闭句柄）；每 run 一个文件（runId 隔离，天然不串）。

### 4.7 配置与对外告知（"哪里可以查"）

**告知 1 — 产物字段（result.json）**：`RunResult` 新增 `RunLogPath` 字段，finalize 时写 `"runLogPath": "trace/{runId}/run.log"`（**相对路径**，对称 `TracePath` 先例）。读取侧（`TraceRunLoader`）解析链：`result?.RunLogPath ?? "trace/{runId}/run.log"` 默认回退（同 TracePath 回退模式）；V1 run（无 run.log）回退目标不存在 → 分析器得知"该 run 无日志"。**schemaVersion 不 bump**：字段级扩展（D-213 是布局版本化，缺字段读侧回退，向后兼容）。

**告知 2 — 读侧布局解析**：`RunLayoutV2` 增加布局常量/解析辅助（`RunLogRelativePath(runId)` 或等价），`TraceRunLoader` 用它解析 `runLogPath` 为完整路径——分析器侧不拼字符串。

**告知 3 — 写侧配置**：
- 级别：`UNICLAW_LOG_LEVEL`（§4.2，默认 information）；
- 落盘：布局契约固定开启（无开关）；路径模式固定 `trace/{runId}/run.log`（无配置项）；
- 测试装配：`integration.config.json` 新增 `logging` 段（`{ "logging": { "level": "debug" } }`，可选，缺省不设），模式同 P2.4 visionServer env 注入——测试装配期把 level 注入 `UNICLAW_LOG_LEVEL`（允许测试静音/开启 step 级 Debug 噪音）。loader 校验：level 合法值枚举（同 §4.2），未知值 fail-fast。

**分析器更新（实现时）**：trace-analyzer agent 文档 L3 产物层增补 run.log 地址（`trace/{runId}/run.log`，result.json `runLogPath` 字段可查）；"运行日志补充取证"从无址变为固定地址。

## 5. 补充日志点清单

| # | 位置 | 级别 | 内容 | 关联 |
|---|------|------|------|------|
| L1 | `TraversalFSM.step()` catch（:94 DomainValidation、:120 通用） | Warning / Error | 异常类型 + 消息 → 路由到 `ErrorHandling`；通用异常带异常对象 | `s=step span` |
| L2 | `ErrorHandler.HandleError` 分类结果 | Information | `classified={errorType} strategy={strategy} retry={retryCount}` | `s=handle_error span` |
| L3 | `ErrorHandler.HandleError` pipeline fallback（:212） | Error | `unhandled exception during error handling` + 完整异常 | `s=handle_error span` |
| L4 | `TraversalEngine` step 开/关 | Debug | `step {n} start/end span={spanId}`（高频，Debug 门控） | `s=step span` |
| L5 | Host run 开始 | Information | `run {runId} mode={mode} provider={provider}` | `t=runId` |
| L6 | Host run 结束 | Information | `run {runId} status={status} duration={ms}` | `t=runId` |
| L7 | 资产提交失败（与 `asset_write_failed` issue 同步处） | Error | 相对路径 + 异常 | `t=runId s=step span` |
| L8 | run 终态（pending_verification / 终判） | Information | `result={status} 引擎事实摘要` | `t=runId` |

**原则**：trace 事件（结构化、可查询）与日志（文本、实时可见）各司其职；日志点不复制 trace 字段全集，只带 id 关联 + 人类可读摘要。TraceTool 层不加（非目标）。

## 6. 决策表

> 编号为 PRD 内部引用；实现/归档时按序映射为 log.md D-222 起的正式决策号（D-1→D-222、D-2→D-223 …，共 12 条至 D-233）。

| # | 决策 | 理由 |
|---|------|------|
| D-1 | span 上下文同步点 = `TraceSpanScope`（构造 push / Dispose pop），非 SourceGen Emitter | 所有 span 唯一生命周期封装；一处改动全 span 覆盖；SourceGen 生成代码零变更，S1-S6 快照回归面最小 |
| D-2 | `EngineStepSpanContext` 栈化（Push/Pop/栈顶读取），删除 TraversalEngine 显式 Set/Reset | 嵌套 span（handle_error 内 ai.call）需要保存/恢复语义；单值 Set/Reset 无法表达嵌套 |
| D-3 | 日志关联语义：ai.call parent 从"仅 engine.step"扩展为"当前最内层 span" | 树结构更正确；代价是 S1-S6 需回归确认（§8） |
| D-4 | 可选构造注入 `ILogger<T>? = null`（NullLogger 缺省），组合根装配真实 logger | 标准 ILogger 抽象 + 既有测试零波及；不引入 DI 容器 |
| D-5 | 自写 `TraceCorrelatedConsoleProvider`（仅依赖 Abstractions），不用 `Logging.Console` 包 | 输出格式（t=/s= 前缀）本需自定义；避免重依赖 |
| D-6 | 日志不进 trace.jsonl；日志与 trace 靠 id 关联 | TraceFields frozen catalog 不动；信息/物理分离原则（unified PRD §3） |
| D-7 | `UNICLAW_LOG_LEVEL` 命名独立（非 VISION_MODE/RUN_MODE 族） | P2.8 教训：一变量两义污染已发生两次，新 env 独立命名并登记 §9.1 |
| D-8 | **日志要存储**：run 目录留档 `trace/{runId}/run.log`，分析器可查 | stderr 易失，trace-analyzer"运行日志补充取证"无地址可查；P3.1 教训 |
| D-9 | run.log 走**旁路直接写**（不经 `ITracePipeline`/`FileAssetStore`） | 流式追加文本 ≠ 批量 flush 资产；D-216 写侧各入口自持，logger 自持路径不冲突 |
| D-10 | 双 provider（console + file）注册同一 LoggerFactory | 微软标准做法；职责单一；行格式契约一致 |
| D-11 | **对外告知 = result.json 新增 `runLogPath` 字段**（相对路径，对称 `TracePath` 先例） | 分析器读 run 元数据即知日志地址；缺失回退默认（V1 run 无日志） |
| D-12 | 读侧解析收敛 `RunLayoutV2`（布局常量 + `TraceRunLoader` 回退链）；写侧配置仅 `UNICLAW_LOG_LEVEL` + config `logging.level` | D-217 读侧 CLI 参数即配置 + 布局单点；写侧各入口自持（D-216），落盘无开关 |

## 7. 验证

### 7.1 新增单测（Core.Tests）

- `TraceCorrelatedConsoleProviderTests`：格式（t=/s= 前缀、类别短名、级别标签）、级别过滤（低于最低级别不输出）、无 run/span 上下文输出 `-`、异常堆栈缩进。
- `EngineStepSpanContextTests`（扩展既有）：栈化 push/pop 嵌套（A → B → 读 = B → pop → 读 = A）、AsyncLocal flow（`Task.Run` 内读可见/外层不受污染）、空栈返回 null。
- `TraceSpanScope` 集成：`BeginSpanAsync` → scope 内 `CurrentSpanId == spanId` → dispose → 回退父 span；`CreateNoOp` 不改变当前 span。
- FSM 异常日志：注入异常 → `TraversalFSM.step` catch 断言日志含 `t={runId}` + `s={step spanId}`；`ErrorHandler.HandleError` pipeline fallback 断言含完整异常类型。
- `RunTraceContext`：push/pop 生命周期 + flow 隔离。

### 7.2 回归（必须 green）

- **S1-S6 span-tree 快照门**（Core.Tests）——D-3 语义变化直接冲击快照；若场景含 handle_error 内 AI 调用则树变，需按新语义重新冻结快照（同 R-12 先例），并审查树变化是否仅由 parent 扩展引起。
- 既有 1083+ Core 测试（构造签名零变化应全绿）。
- Host 185+ 测试 + 集成测试（run 输出含 `[t={runId}]` 可观察）。

### 7.3 存储与查询验证

- `TraceCorrelatedFileProviderTests`：写 `trace/{runId}/run.log`（目录自动创建）、行格式与 console 同契约、`Flush/Close` 幂等、异常路径句柄关闭（finally 断言无泄漏句柄）。
- 分析器查询契约测试：run 目录存在 `trace/{runId}/run.log` 且首行匹配格式正则；与 `trace.jsonl` 同目录（布局断言）。
- 集成观察：run 结束后 `trace/{runId}/run.log` 可被 TraceTool/分析器按固定路径读取。

### 7.3 集成观察

- 跑一次 emulator run：stderr 出现 `[t={runId}] [s={stepSpanId}] [INFO] run ...` 行；错误场景（asset 失败）出现 `[ERROR]` + 异常堆栈，id 与 trace.jsonl 中 `handle_error`/`asset_write_failed` 事件可交叉定位。

## 8. 风险与迁移

- **[高] D-3 改变 ai.call parent 语义 → S1-S6 快照可能失败**。缓解：回归先行；树变化若仅由 parent 扩展引起（span 集合/字段不变），按既有快照重冻结流程（S5 refrozen 先例）处理；若出现意外结构变化，回退 D-3 为"仅 engine.step parent + 日志读栈顶"（日志功能不受影响，ai.call parent 保持原语义）。
- **[中] TraceSpanScope 改动触及所有 span 创建路径**。缓解：改动面收敛为构造 + DisposeAsync 两处 + 单测覆盖 no-op/嵌套/异常路径；S1-S6 + 全量回归兜底。
- **[低] 可选注入默认参数让漏注入静默 NullLogger**。缓解：组合根装配测试断言关键组件（FSM/Engine）收到非 NullLogger；漏注入只损失日志不损失功能（可接受降级）。
- **[低] stderr 并发交错**。缓解：provider 内 `lock` 串行化单行写入。

## 9. 验收标准

1. `Microsoft.Extensions.Logging.Abstractions` 仅出现在 Core.csproj（Host/TraceTool 经传递引用）。
2. 格式契约：所有经 `TraceCorrelatedConsoleProvider` 的日志行匹配 `^\[\d{2}:\d{2}:\d{2}\.\d{3}\] \[t=(runId|-)\] \[s=(spanId|-)\] \[\w+\] `（单测断言）。
3. 引擎 step scope 内日志 `s=` 等于该 step 的 span id；`handle_error` span 内日志 `s=` 等于 handle_error span id（FSM 异常日志测试断言）。
4. Host run 输出含 `[t={runId}]`，runId 与 trace 目录名一致（集成观察）。
5. `UNICLAW_LOG_LEVEL=debug` 开启 L4 引擎 step 日志；缺省不输出 L4。
6. S1-S6 快照门 green（或经审查后按新语义重冻结）。
7. 全量测试 green（Core 1083+ / Host 185+），既有测试零改动。
8. `TraceFields` 45-key catalog 不变；trace.jsonl 事件流不变（无 log 事件类型）。
9. run 结束后 `trace/{runId}/run.log` 存在、行格式与 console 同契约（单测 + 集成观察断言）；分析器按固定路径可读。
10. 异常路径（run 中途崩溃/取消）run.log 句柄关闭、已写内容可读（finally 保证）。
11. `result.json` 含 `runLogPath: "trace/{runId}/run.log"`（V2 run）；`TraceRunLoader` 缺失回退默认、V1 run 解析为"无日志"。
12. `integration.config.json` `logging.level` 合法值校验 fail-fast；测试装配注入 `UNICLAW_LOG_LEVEL` 生效（debug 时 step 级日志出现）。

## 10. 后续（非本期）

- TraceTool verify/watch 输出带 runId 关联（CLI 提示层）。
- 既有裸 `Console.WriteLine` 诊断调用的渐进迁移（BaselineProfile/CompletionMonitor 等）。
- 日志轮转/大小上限（run.log 超长场景，如 enumerate 长 run）——按需。
