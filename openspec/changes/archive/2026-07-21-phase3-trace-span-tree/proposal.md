## Why

C-9/C-10/C-8 补齐了 trace 采集端，但 span tree 关联能力缺失：TraceContext 只有 4 字段无法表达 span 父子关系，HandlerTraceWriter 记录的 ExecutionRecord 没有 Context（与引擎状态无法关联），6 处手工 trace 注入是重复代码。Phase 3-A 扩展 TraceContext 为 6 字段（+VisitSpanId, +ParentSpanId）建立 span tree 数据基础，Phase 3-B 通过 Roslyn 源生成器自动生成 handler span 包装代码，替换 3 个 handler 手工注入点。

## What Changes

- **Phase 3-A (TraceContext +2)**: TraceContext 从 4 字段扩展到 6 字段（+VisitSpanId, +ParentSpanId）。TraceCoordinator 增加 SpanStack（PushSpan/PopSpan/ClearVisitSpan）实现显式 parent span 关联。HandlerTraceWriter.RecordHandlerLifecycleAsync 增加 TraceContext? context 参数修复 handler 记录无 Context 的问题。**BREAKING**: TraceContext_Has4Fields guard → _Has6Fields。
- **Phase 3-B (Roslyn Source Generator)**: 新建 `UniClaw.Core.SourceGen` 项目（netstandard2.0, IIncrementalGenerator），扫描 `[TraceHandler]` 自动生成 async wrapper 方法。源生成器在编译期提取 return type 属性生成 metadata（null 跳过, enum→string），协调层通过 extraMetadata 字典传入跨来源字段。新增 `TraceIgnoreAttribute` 标记排除属性。

## Capabilities

### New Capabilities
- `trace-context-span-tree`: TraceContext 扩展为 6 字段（+VisitSpanId, +ParentSpanId），TraceCoordinator SpanStack push/pop 机制，HandlerTraceWriter Context 修复
- `trace-handler-source-generator`: Roslyn 增量源生成器扫描 [TraceHandler] 自动生成 async span wrapper，自动提取 return type 属性为 metadata

### Modified Capabilities
- `trace-coordinator-fill`: ITraceCoordinator 新增 PushSpan/PopSpan/ClearVisitSpan 方法，BuildCorrelation 输出 6-field TraceContext
- `handler-lifecycle-trace`: IHandlerTraceWriter.RecordHandlerLifecycleAsync 新增 TraceContext? context 参数
- `trace-record`: TraceContext record 从 4 字段扩展到 6 字段

## Impact

- **Observability/ (5 修改 + 1 新建)**: TraceContext.cs (+2 fields), TraceHandlerAttribute.cs (xml doc), IHandlerTraceWriter.cs (+context param), HandlerTraceWriter.cs (+context param), TraceIgnoreAttribute.cs (新建)
- **Traversal/ (1 修改)**: TraversalEngine.cs (TraceCoordinator +SpanStack, +PushSpan, +PopSpan, +ClearVisitSpan, BuildCorrelation 更新, RegisterGlobalFsmTraceCallbacks 更新)
- **StateMachine/ (3 修改 + 切换)**: TraversalFSM.cs (HandlerTrace 传入 context, 改为调用生成 wrapper), PopupHandler.cs (+partial +[TraceHandler]), ErrorHandler.cs (+partial +[TraceHandler])
- **StateMachine/ (1 修改 + 切换)**: ContainerHandler.cs (+partial +[TraceHandler])
- **Traversal/ (1 修改 + 切换)**: InterceptionHandler.cs (HandlerTrace 传入 context, ContainerHandler 改为生成 wrapper)
- **SourceGen/ (新建项目)**: UniClaw.Core.SourceGen/ (netstandard2.0, TraceHandlerGenerator, Emitter)
- **Tests/ (22 新测试)**: Phase 3-A 10 tests, Phase 3-B 12 tests
- **ArchitectureGuard**: TraceContext_Has4Fields → TraceContext_Has6Fields
