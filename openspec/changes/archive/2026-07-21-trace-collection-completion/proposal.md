## Why

C-7 已完成 JSONL 存储后端（27/27），但生产端只有 ~50% 组件往管道写数据。当前 P1 空洞：ContainerHandler 零 trace、PageTransition 从未调用、3 SpanType 未激活（PopupHandling/DfsBacktrack/CacheOp）、GlobalFSM Context=null。P2 空洞：DurationMs 永远 0、PageId 永远空、Metadata 永远不填。

需要一次性补齐 3 条采集链路（C-9 Handler Lifecycle → C-10 Operation Timing → C-8 State Flow），同时在 TraceHandlerAttribute 中预埋自动父子 span 关联机制，为 Phase 3 通用关联扩展铺路。

## What Changes

- **C-9 (Handler Lifecycle Trace)**：新增 IHandlerTraceWriter/HandlerTraceWriter/TraceTraceMetadata/TraceHandlerAttribute，为 PopupHandler/ContainerHandler/ErrorHandler 注入生命周期 trace。DecideFrameCompletion sync→async。PopupHandlingResult/ContainerActionResult 扩展可选追踪字段。
- **C-10 (Operation Timing Trace)**：TraceCoordinator 自带 Stopwatch 记录 DurationMs。DfsBacktrack 3 个插入点（leaf_execution_complete/pop_only/press_back）。AICallRecord.Metadata 可选字段。
- **C-8 (State Flow Trace)**：GlobalFSM Context 修复（closure 捕获 engine 上下文）。8 状态全覆盖注册。PageTransition 2 个插入点（RunAsync 循环 + PressBack+Pop）。PageId 填充（ExecutionRecord 专用）。
- **TraceHandlerAttribute 扩展**：支持 SpanType + Action 文档化标注。Phase 3 路线图规划：Phase 3-A TraceContext 加 VisitSpanId/ParentSpanId，Phase 3-B Roslyn 源生成器自动注入父子 span 树。

## Capabilities

### New Capabilities
- `handler-lifecycle-trace`: Handler 生命周期 trace 采集 — IHandlerTraceWriter、TraceHandlerAttribute、TraceMetadata、Handler 编排层注入
- `operation-timing-trace`: 步骤 timing + DfsBacktrack trace — Stopwatch DurationMs、3 个 DfsBacktrack 插入点、AICallRecord.Metadata
- `state-flow-trace`: 双 FSM 状态流 + 页面导航 trace — GlobalFSM Context/8-state、PageTransition、PageId 填充

### Modified Capabilities
<!-- No spec-level requirement changes — all additions are implementation-layer fill-ins. -->

## Impact

- **Observability/ (4+ 新文件)**：IHandlerTraceWriter.cs、HandlerTraceWriter.cs、TraceMetadata.cs、TraceHandlerAttribute.cs
- **Observability/ (3 修改)**：ITraceCoordinator.cs（RecordAICallSpanAsync + metadata 参数）、TraceCoordinator.cs（Stopwatch + PageId + GlobalFSM Context）、ITraceRecorder.cs（AICallRecord.Metadata）
- **Traversal/ (2 修改)**：TraversalEngine.cs（GlobalFSM callbacks + RunAsync PageTransition + DfsBacktrack）、InterceptionHandler.cs（PageTransition + DecideFrameCompletion async + DfsBacktrack）
- **StateMachine/ (3 修改)**：PopupHandler.cs（PopupHandlingResult 扩展 + trace 注入）、ContainerHandler.cs（ContainerActionResult 扩展 + trace 注入）、ErrorHandler.cs（ErrorHandler trace）、StepContext.cs（可选 IHandlerTraceWriter）
- **Tests/ (30 新测试)**：C-9 9 tests、C-10 12 tests、C-8 9 tests
