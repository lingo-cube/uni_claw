# Tasks: trace-parent-linkage

## 1. M0 — TraceFields 字段目录（零行为变化）

- [x] 1.1 创建 `TraceFields` 静态目录类（`src/UniClaw.Core/Observability/`），收录 §2.3 清单全部键名（`ai.*`/`action.*`/`entry.*`/`analyze.*`/`error.*`），常量值不得改变
- [x] 1.2 新增目录完整性测试（键名非空、`layer.` 命名空间、目录与 SpanTypes 18 常量一致约束）
- [x] 1.3 业务代码键名替换为常量引用（PageAnalyzer、TraversalEngine、InterceptionHandler、SafetyGate、CompletionMonitor、ErrorLoopAnalyzer、EnumerateCompletionAnalyzer、TraversalFSM）
- [x] 1.4 验证零行为变化：S1–S5 快照全绿（快照不受键名替换影响），oracle 测试零改动全绿

## 2. M1 — 父链打通

- [x] 2.1 新增 `ITraceContextProvider` 接口（`string? CurrentSpanId { get; }`，`src/UniClaw.Core/Observability/`）
- [x] 2.2 `TraceCoordinator` 实现 `ITraceContextProvider`（`CurrentSpanId => _currentEngineStepSpanId`，internal 通道复用）
- [x] 2.3 `PageAnalyzer` 构造注入 provider（可选，null 时保留孤儿行为）；`ai.call` scope 的 `parentSpanId` 改为 `_traceContext?.CurrentSpanId`
- [x] 2.4 `UniBrainFactory` 接线 provider；4 个 `AnalyzeCurrentPageAsync` 调用点零改动验证
- [x] 2.5 S4 快照重冻结（`ai.call` parent = `engine.step`，非引擎入口构造断言保留孤儿）
- [x] 2.6 新增 S6 场景：完整父链 `engine.run → engine.step → ai.call → ai.analyze`，含重试路径（`ai.retry_count` 断言）
- [x] 2.7 生产链路点亮（AsyncLocal 通道）：新增 `EngineStepSpanContext`（`ITraceContextProvider` + `AsyncLocal<string?>`，静态单例），引擎 step scope 开/合处 Set/Reset，`HostCommands` 注入该实例替换静态 coordinator；移除 `TraceCoordinator` 的 `ITraceContextProvider` 实现（生产已不用）；S4/S6 fixture 改用该通道注入

## 3. M2 — 字段分级（TraceLevel 门控）

- [x] 3.1 新增 `SpanFieldProfile` 描述符（Basic/Extended 字段数组）与 `TraceSpanFields` 每 spanType 实例，按 design D3 草案逐键核对
- [x] 3.2 helper additive 演进：`BeginSpanAsync`/`RecordEventAsync` 增加 profile + level 参数（`ITraceRecorder` 9 方法 guard 不受影响）
- [x] 3.3 level 来源接线：`EntryConfig.TraceLevel`（缺省 Detailed = 现状全量行为；TraversalEngine 经 `_plan.EntryConfig` 接线，其余调用点保持缺省）
- [x] 3.4 记录层按 level 过滤 Extended 键
- [x] 3.5 分级测试：缺省（Detailed）字段集与 change 前全量一致；Basic 记录核心字段、不记录 Detailed+ 字段
- [x] 3.6 验证 S1–S5 在缺省级别下仍全绿（快照不受分级影响）

## 4. M3 — 验收与归档

- [x] 4.1 验收矩阵 AC1–AC7 全绿（快照闸门 / oracle 零改动 / 无新脚手架 / 目录与枚举冻结 / 基线计数 / 分级缺省兼容 / 父链双向覆盖）
- [x] 4.2 AC3 白名单 grep 校验（`src/` 中 `StartSpanAsync`/`EndSpanAsync` 命中仅限 helpers、TraversalEngine passthrough、recorder 实现）
- [x] 4.3 AC5 基线计数核对（Core/Host 通过数与 M0 记录一致，新增测试只加不改）
- [x] 4.4 归档 spec 更新（`openspec/specs/trace-span/spec.md` 合并本 change 的 3 个新增 requirement），change 状态确认 apply 完成，建议 `/opsx:archive`
