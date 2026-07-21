## 1. C-9: Handler Lifecycle Trace — 接口与辅助

- [x] 1.1 新建 IHandlerTraceWriter.cs — 接口定义（1 方法: RecordHandlerLifecycleAsync）
- [x] 1.2 新建 HandlerTraceWriter.cs — 实现，委托 ITraceRecorder.RecordExecutionAsync
- [x] 1.3 新建 TraceMetadata.cs — Builder 链式辅助（Add 3 重载 + ToDict + null skip）
- [x] 1.4 新建 TraceHandlerAttribute.cs — AttributeUsage.Method，SpanType + Action 构造参数
- [x] 1.5 StepContext 加可选 IHandlerTraceWriter 属性（inject via constructor）

## 2. C-9: Handler Lifecycle Trace — 注入与编排

- [x] 2.1 PopupHandlingResult 加 PopupClassification? Classification（default null）
- [x] 2.2 ContainerActionResult 加 CompletionReason?/TotalChildren?/VisitedChildCount?/Depth?（default null）
- [x] 2.3 DecideFrameCompletion sync→async（改名 DecideFrameCompletionAsync）
- [x] 2.4 OnFrameComplete sync→async（调用 DecideFrameCompletionAsync）
- [x] 2.5 PopupHandler 编排层注入: 提取 PopupHandlingResult → RecordHandlerLifecycleAsync
- [x] 2.6 ContainerHandler 编排层注入: 提取 ContainerActionResult → RecordHandlerLifecycleAsync
- [x] 2.7 ErrorHandler 编排层注入: 提取 ErrorRecoveryResult → RecordHandlerLifecycleAsync（保留 RecordErrorSpanAsync 双写）

## 3. C-10: Operation Timing Trace

- [x] 3.1 TraceCoordinator 加 _stepStopwatch Stopwatch 字段
- [x] 3.2 RecordStepStartAsync 调用 _stepStopwatch.Restart()
- [x] 3.3 RecordStepEndAsync 设置 DurationMs = _stepStopwatch.Elapsed.TotalMilliseconds
- [x] 3.4 AICallRecord 加 Dictionary<string, object>? Metadata（default null）
- [x] 3.5 ITraceCoordinator.RecordAICallSpanAsync 加可选 metadata 参数
- [x] 3.6 TraversalEngine RunAsync 叶子节点回溯后插入 DfsBacktrack trace（leaf_execution_complete）
- [x] 3.7 InterceptionHandler OnDynamicMatchNodeSelect pop-only 分支插入 DfsBacktrack trace
- [x] 3.8 InterceptionHandler OnDynamicMatchNodeSelect press_back+pop 分支插入 DfsBacktrack trace

## 4. C-8: State Flow Trace

- [x] 4.1 RegisterGlobalFsmTraceCallbacks closure 捕获 engineCtx，填充 NodeId/StepNumber/TraceId（StepSpanId=null）
- [x] 4.2 GlobalFSM 注册扩展为 foreach 8 states（排除 Completed/Terminated）
- [x] 4.3 TraversalEngine RunAsync 循环内插入 PageTransition（启发式替换为正式 RecordPageTransitionAsync）
- [x] 4.4 InterceptionHandler OnDynamicMatchNodeSelect press_back+pop 分支插入 PageTransition
- [x] 4.5 RecordActionExecutionAsync 填充 ExecutionRecord.PageId = _ctx.CurrentFrame?.NodeId

## 5. 测试

- [x] 5.1 C-9 测试: IHandlerTraceWriter + HandlerTraceWriter 委托验证
- [x] 5.2 C-9 测试: TraceMetadata.Build 链式 API + null skip
- [x] 5.3 C-9 测试: TraceHandlerAttribute 属性值
- [x] 5.4 C-9 测试: PopupHandlingResult/ContainerActionResult 扩展向后兼容
- [x] 5.5 C-9 测试: DecideFrameCompletionAsync sync→async 行为不变
- [x] 5.6 C-9 测试: PopupHandler/ContainerHandler/ErrorHandler lifecycle trace metadata
- [x] 5.7 C-10 测试: RecordStepStart/End DurationMs 非零
- [x] 5.8 C-10 测试: DfsBacktrack 3 个分支 trace（leaf/pop_only/press_back）
- [x] 5.9 C-10 测试: AICallRecord.Metadata round-trip + 向后兼容
- [x] 5.10 C-8 测试: GlobalFSM Context 非空，含 NodeId/StepNumber
- [x] 5.11 C-8 测试: GlobalFSM 8 状态覆盖（6 active + 2 excluded）
- [x] 5.12 C-8 测试: PageTransition RunAsync + PressBack
- [x] 5.13 C-8 测试: PageId 填充（ExecutionRecord 有，其他类型无）
- [x] 5.14 C-8 测试: ForceState 不产生 trace 记录

## 6. 验证

- [x] 6.1 dotnet build 0 errors
- [x] 6.2 全部现有测试通过（833 passed）
- [x] 6.3 ArchitectureGuard 46/46（SpanType 11 值未变、TraceContext 4 字段未变）
