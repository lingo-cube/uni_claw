# Host / FSM 运行日志与查询体系 PRD

> 版本: v1.0 | 日期: 2026-08-04 | 状态: 设计中

## 1. 背景

### 1.1 现状

Host + FSM 日志基础设施已完成落地：

- **Provider**: `TraceCorrelatedFileProvider`（文件落盘）+ `TraceCorrelatedConsoleProvider`（stderr 同步）
- **落盘位置**: `trace/{runId}/run.log`（每 run 一个文件，不交叉）
- **日志格式**: `[HH:mm:ss.fff] [t=<runId>] [s=<spanId>] [LEVEL] Category: message`
- **关联通道**: `RunTraceContext.Instance`（AsyncLocal）提供 `t=<runId>`，`EngineStepSpanContext.Instance`（AsyncLocal）提供 `s=<spanId>`
- **日志级别控制**: `UNICLAW_LOG_LEVEL` 环境变量，默认 `Information`

### 1.2 问题

当前日志覆盖严重不足，以 Info 级别运行时不设 Debug 几乎看不到任何运行时信息：

| 当前记录 | Category | 级别 |
|----------|----------|------|
| run 启动/结束/终态 | `UniClaw.Host` | Info |
| step 启动（spanId） | `TraversalEngine` | Debug（默认不可见） |
| 非法状态转换被拒 | `TraversalFSM` | Warning |
| dispatch 异常 | `TraversalFSM` | Error |
| 错误分类 + 策略 | `ErrorHandler` | Info |

**未记录的关键信息**：

- **操作日志**：click / scroll / back 动作及结果 —— 排障第一问"引擎做了什么？"无法从日志直接回答
- **安全门日志**：哪个动作被哪个规则 deny —— 安全策略调试完全依赖 trace.jsonl 的 execution record，不够直观
- **页面分析摘要**：每次模型调用的结果（几项、当前页身份）—— 引擎决策的最核心输入，完全不可见
- **FSM 正常状态转换**：`NodeSelect→PreconditionCheck→Execute→...` 的流转 —— 只有被拒的转换才记录
- **引擎终止原因**：`all_visited` / `max_steps` / `target_found` —— 只能从 result.json 事后看

### 1.3 目标

**在默认 Info 级别下，run.log 可以回答以下问题**：

1. 引擎做了什么动作，成功了没？（操作日志）
2. 被拒绝的动作是什么，被哪个规则拒绝？（安全门日志）
3. 每次视觉分析看到了什么？（页面分析摘要）
4. 引擎在哪个状态之间流转？（FSM 转换日志）
5. 引擎为什么停下来？（终止原因日志）
6. 出错时错误被分类为什么，用了什么恢复策略？（ErrorHandler 已有）

**消费者端**：

- `host-test-runner` skill：可以实时 tail 日志 + 事后 grep 快速查看
- `trace-analyzer` agent：可以用 spanId 关联日志和 trace.jsonl 做交叉引用诊断

---

## 2. 需求

### 2.1 操作日志

**位置**: `SafeActionExecutor.ExecuteAsync`

**触发**: 每次安全门 allow 后的动作执行（click / scroll / back / input / long_press / wait）

**要求**:
- R1.1: Info 级别记录 `action=<类型> result=<ok|failed>`
- R1.2: 安全门 deny 时 Warning 级别记录 `action=<类型> → deny rule=<RuleId>`
- R1.3 日志行携带当前 spanId（无需手动传参，AsyncLocal 自动带）

### 2.2 页面分析摘要

**位置**: `InvalidatingPageAnalysisCache.AnalyzeCurrentPageAsync`

**触发**: 每次缓存 miss（实际模型调用）时

**要求**:
- R2.1: Info 级别记录 `page=<路径> items=<数量> scroll=<是否可滚动> endOfList=<是否列表尾>`
- R2.2: 缓存命中（`_cached` 非 null）时不重复记录
- R2.3: 如果视觉分析返回 null（异常路径），不记录

### 2.3 FSM 正常状态转换

**位置**: `TraversalFSM.StepAsync`

**触发**: 每次 `TransitionTo` 成功后

**要求**:
- R3.1: Info 级别记录 `FSM <FromState>→<ToState> step=<StepNumber>`
- R3.2: 不改变现有 Warning（非法转换被拒）和 Error（dispatch 异常）的记录逻辑

### 2.4 引擎终止原因

**位置**: `TraversalEngine.RunAsync`

**触发**: 每个 `Done(...)` 返回前

**要求**:
- R4.1: Info 级别记录 `Engine terminated reason=<Reason> steps=<StepCount>`
- R4.2: 含异常终止（`TraversalResult.Reasons.Error`）

### 2.5 日志查询 — host-test-runner skill

**位置**: `.claude/skills/host-test-runner/SKILL.md`

**要求**:
- R5.1: Phase 3 新增实时日志 tail（`tail -f run.log`）
- R5.2: Phase 4 新增日志完整性检查子步骤（文件存在 / run start 记录 / run end 记录 / ERROR 计数）
- R5.3: Phase 4 新增按组件/级别过滤查询子步骤
- R5.4: Phase 4 新增 spanId 交叉引用定位子步骤

### 2.6 日志查询 — trace-analyzer agent

**位置**: `.claude/agents/trace-analyzer.md`

**要求**:
- R6.1: Step 3（深入取证）新增 `run.log` 作为第一优先级补证来源
- R6.2: Step 4（完整性自评表）新增 run.log 行
- R6.3: 记忆系统（`knowledge.md`）新增日志格式/路径/查询方法
- R6.4: 支持"trace → 日志"和"日志 → trace"双向交叉引用

---

## 3. 设计

### 3.1 注入点

| 文件 | 类型 | ILogger 参数 | 级别 |
|------|------|-------------|------|
| `src/UniClaw.Host/Safety/SafetyGate.cs` | `SafeActionExecutor` | 新增可选 `ILogger<SafeActionExecutor>?` | Info |
| `src/UniClaw.Host/Runner/InvalidatingPageAnalysisCache.cs` | `InvalidatingPageAnalysisCache` | 新增可选 `ILogger<InvalidatingPageAnalysisCache>?` | Info |
| `src/UniClaw.Core/StateMachine/TraversalFSM.cs` | `TraversalFSM` | 已有 `ILogger<TraversalFSM>` | Info |
| `src/UniClaw.Core/Traversal/TraversalEngine.cs` | `TraversalEngine` | 已有 `ILogger<TraversalEngine>` | Info |

遵循现有模式：可选 ctor 参数 + `NullLogger<T>.Instance` 默认值，不破坏已有的非组合调用方。

### 3.2 组合根注入

**文件**: `src/UniClaw.Host/Commands/HostCommands.cs` → `CreateRunServices`

`SafeActionExecutor` 和 `InvalidatingPageAnalysisCache` 在 `CreateRunServices` 中装配，此处从 `loggerFactory` 创建 loggers 传入：

```csharp
// CreateRunServices 中新增
var safetyLogger = loggerFactory.CreateLogger<SafeActionExecutor>();
var analysisLogger = loggerFactory.CreateLogger<InvalidatingPageAnalysisCache>();
```

### 3.3 run.log 目标效果（Info 级别完整示例）

```
[09:31:29.801] [t=...f95fa18f] [s=-] [INFO ] Host: Run ...f95fa18f started mode=mode-a provider=mock
[09:31:48.676] [t=...f95fa18f] [s=...span00] [INFO ] TraversalFSM: FSM Idle→Initializing step=0
[09:31:48.684] [t=...f95fa18f] [s=...span01] [INFO ] TraversalFSM: FSM Initializing→Traversing step=0
[09:31:48.690] [t=...f95fa18f] [s=...span01] [INFO ] InvalidatingPageAnalysisCache: page=Settings items=11 scroll=true endOfList=false
[09:32:12.613] [t=...f95fa18f] [s=...step01] [INFO ] TraversalFSM: FSM NodeSelect→PreconditionCheck step=1
[09:32:28.907] [t=...f95fa18f] [s=...step02] [INFO ] TraversalFSM: FSM PreconditionCheck→Execute step=2
[09:32:28.908] [t=...f95fa18f] [s=...step02] [INFO ] TraversalFSM: FSM Execute→ResultVerify step=2
[09:32:36.xxx] [t=...f95fa18f] [s=...step03] [INFO ] InvalidatingPageAnalysisCache: page=Settings items=11 scroll=true endOfList=false
[09:33:17.xxx] [t=...f95fa18f] [s=...step04] [INFO ] SafeActionExecutor: action=scroll result=ok
[09:33:38.xxx] [t=...f95fa18f] [s=...step04] [INFO ] TraversalFSM: FSM ResultVerify→Branch step=4
[09:33:38.xxx] [t=...f95fa18f] [s=-] [INFO ] TraversalEngine: Engine terminated reason=all_visited steps=4
[09:33:54.xxx] [t=...f95fa18f] [s=-] [INFO ] Host: Run ...f95fa18f ended status=pending_verification duration=110000ms
[09:33:54.xxx] [t=...f95fa18f] [s=-] [INFO ] Host: Run ...f95fa18f final state: pending_verification reason=all_visited
```

### 3.4 消费者：skill 查询命令

| 查询意图 | 命令 |
|----------|------|
| 实时监控 | `tail -f <runDir>/trace/<runId>/run.log` |
| 看 FSM 流转 | `grep "FSM.*→" run.log` |
| 看所有操作 | `grep "SafeActionExecutor:" run.log` |
| 看安全门拒绝 | `grep "→ deny" run.log` |
| 看页面分析 | `grep "InvalidatingPageAnalysisCache:" run.log` |
| 看所有 ERROR | `grep "\[ERROR\]" run.log` |
| spanId 精确定位 | `grep "s=<spanId>" run.log` |
| 时间区间定位 | `sed -n "/09:32:/,/09:33:/p" run.log` |

### 3.5 消费者：trace-analyzer agent 交叉引用

```
trace.jsonl 发现 spanId=...step04 有 execution 异常
  → grep "s=...step04" run.log
  → 找到: [ERROR] TraversalFSM: Step dispatch failed from Execute: TimeoutException
  → ErrorHandler 日志往下: [INFO] ErrorHandler: Error classified: TimeoutException strategy=Retry retry=1
  → 结论: 执行超时，但错误处理器已触发重试
```

---

## 4. 非功能需求

- **NF1**: 不设 `UNICLAW_LOG_LEVEL=Debug` 的情况下，Info 级别即可看到完整操作→分析→状态链路
- **NF2**: ILogger 注入遵循现有 NullLogger 模式，不破坏已有调用方
- **NF3**: 页面分析日志在缓存命中时不重复，避免刷屏
- **NF4**: 日志行不超过 200 字符（超长路径/消息截断）

---

## 5. 实施步骤

| # | 内容 | 文件 |
|---|------|------|
| 1 | `SafeActionExecutor` 加 ILogger + 操作日志 + 安全门 deny 日志 | `SafetyGate.cs` |
| 2 | `InvalidatingPageAnalysisCache` 加 ILogger + 页面分析摘要日志 | `InvalidatingPageAnalysisCache.cs` |
| 3 | `TraversalFSM.StepAsync` 加 FSM 正常转换日志 | `TraversalFSM.cs` |
| 4 | `TraversalEngine.RunAsync` 加终止原因日志 | `TraversalEngine.cs` |
| 5 | 组合根注入新 loggers | `HostCommands.cs` |
| 6 | trace-analyzer agent Step 3/4 + knowledge 更新 | `.claude/agents/trace-analyzer.md` + `knowledge.md` |
| 7 | host-test-runner skill Phase 3/4 更新 | `.claude/skills/host-test-runner/SKILL.md` |
| 8 | mock run 验证 | 跑 locate-one-item，确认 run.log 包含上述全部日志类别 |

## 6. 验收标准

- [ ] 不设 `UNICLAW_LOG_LEVEL`（默认 Info），run.log 包含：Host 生命周期、FSM 状态转换、操作结果、页面分析摘要、引擎终止原因、ErrorHandler 错误分类
- [ ] `SafeActionExecutor` 的 deny 路径 Warning 级别单独可 grep
- [ ] 缓存命中的 `AnalyzeCurrentPageAsync` 不产生日志
- [ ] `UNICLAW_LOG_LEVEL=Warning` 时仅 ErrorHandler + deny + 异常可见，Info/Debug 不可见
- [ ] skill Phase 4 的 5 个 grep 命令（FSM / 操作 / deny / 页面分析 / ERROR）在真实 run.log 上都能返回结果
- [ ] trace-analyzer agent 能通过 spanId 在 run.log 中定位到对应的操作/分析/转换日志行
