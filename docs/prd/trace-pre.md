Ready for review
Select text to add comments on the plan
Trace Collection Design — 3份分开设计文档
Context
C-7 (Trace JSONL 存储) 已完成 27/27，但生产端只有 ~50% 组件往管道写数据。 Trace 的核心用途是重放 + 运行信息收集 + 优化分析（ADB耗时、视觉链路耗时/token、状态机扭转），不是按组件分而是按用途分设计文档。

当前 P1 空洞：ContainerHandler 零 trace、PageTransition 从未调用、3 SpanType 未激活(PopupHandling/DfsBacktrack/CacheOp)、GlobalFSM Context=null。 P2 空洞：DurationMs 永远 0、PageId 永远空、Metadata 永远不填。

3份设计文档 (按用途分组)
C-8: State Flow Trace (状态流转 trace — 重放用途)
目的: 使遍历 session 可完整重放——双 FSM 转换 + 页面导航 + 页面级索引

变更:

变更项	当前	目标	文件
GlobalFSM Context	null	closure 捕获 engineCtx → TraceContext(NodeId, StepSpanId=null, StepNumber, TraceId)	TraversalEngine.cs RegisterGlobalFsmTraceCallbacks
GlobalFSM 状态覆盖	4 states (Completed/Error/Traversing/Idle)	8 states (all GlobalState values)	同上
PageTransition	RecordPageTransitionAsync 存在但从未调用	2 个插入点: (a) RunAsync 页面指纹变化 (b) InterceptionHandler PressBack+Pop	TraversalEngine.cs + InterceptionHandler.cs
PageId	ExecutionRecord.PageId 永远 null	GetCurrentPageId() helper → 8 个 Record 方法全部填充	TraceCoordinator 区域
关键设计决策:

GlobalFSM 回调 Context: StepSpanId=null (事件在步骤循环间发生，不属于任何 step span)
ForceState 不触发回调 — 恢复路径不产生 trace (正确语义)
PageId 保留在 ExecutionRecord 上 (不移入 TraceContext — 只有 ExecutionRecord 用到，TraceContext 只放 ALL-5-shared 字段)
估计: 2 天, 9 tests, 3 files

C-9: Handler Lifecycle Trace (Handler 生命周期 trace — 优化分析用途)
目的: 分析 handler 频率/类型分布、容器完成模式、错误恢复有效性

核心新增: RecordHandlerLifecycleAsync(string action, SpanType spanType, Dictionary<string, object>? metadata) → 通用方法，3 个 handler 共用，用 ExecutionRecord + SpanType 分类 + Metadata 扩展点

决策: 不为每个 handler 加专门方法 (RecordPopupHandlingAsync 等)，违反既有 "ExecutionRecord + SpanType 分类 + Metadata" 模式。Metadata 是已有的扩展机制，专为 handler 特有字段设计。

Handler trace mapping:

Handler	当前 trace	目标	数据映射
PopupHandler	2 calls (StateTransition + Decision)	1 call RecordHandlerLifecycleAsync with SpanType.PopupHandling	Metadata: popup_type, dismiss_strategy, dismiss_target, urgency, blocking_type, handling_success, handling_action
ContainerHandler	0 calls	1 call RecordHandlerLifecycleAsync with SpanType.ContainerHandling	Metadata: completion_reason, fallback_action, container_success, elapsed_ms, depth, total_children, visited_child_count
ErrorHandler	2 calls (Decision + ErrorSpan)	1 call RecordHandlerLifecycleAsync with SpanType.ErrorHandling + keep RecordErrorSpanAsync	Metadata: classified_error_type, strategy, outcome, backoff_delay_seconds, consecutive_errors, can_backtrack, can_skip, stack_depth, error_policy
必需的 record 类型扩展 (向后兼容，新增可选字段 default null):

PopupHandlingResult: 加 PopupClassification? Classification = null — HandlePopup 返回时携带分类信息
ContainerActionResult: 加 CompletionReason? CompletionReason = null, int? TotalChildren, int? VisitedChildCount, int? Depth — HandleContainer 返回时携带完成指标
必需的签名变更: DecideFrameCompletion → DecideFrameCompletionAsync (sync → async，因为 await trace call)

估计: 3 天, 9 tests, 5 files

C-10: Operation Timing Trace (操作耗时 trace — 性能优化用途)
目的: 性能优化分析 — 步骤耗时、DFS 回溯成本、AI 调用延迟

变更:

变更项	当前	目标	实现
DurationMs	永远 0	Stopwatch 在 TraceCoordinator 内，RecordStepStartAsync.Restart() → RecordStepEndAsync.Stop() + Elapsed.TotalMilliseconds	TraceCoordinator + Stopwatch 字段
DfsBacktrack	从未赋值	3 个 Pop() 插入点调用 RecordHandlerLifecycleAsync(SpanType.DfsBacktrack)	TraversalEngine.cs + InterceptionHandler.cs
AICallRecord.Metadata	无此字段 (唯一缺 Metadata 的 record 类型)	加 Dictionary<string, object>? Metadata = null	ITraceRecorder.cs
RecordAICallSpanAsync	5 参数	加 Dictionary<string, object>? metadata = null 参数	TraceCoordinator
关键设计决策:

DurationMs: Stopwatch 方案 (已在 TraversalEngine.cs 用 System.Diagnostics.Stopwatch) — 亚毫秒精度，无需新增外部依赖
DfsBacktrack: 3 种 backtrack_reason 区分回溯成本 — "leaf_execution_complete" / "pop_only_parent_frame_matches" / "press_back_parent_frame_differs"
AICallRecord.Metadata: Phase 3-A future-ready — ["adb_operation"]="tap", ["adb_latency_ms"]=150, ["chain_step"]="screenshot" 无需新 SpanType
必需的接口扩展: ITraceCoordinator 加 double CurrentStepDurationMs { get; } — TraceRecord 从此属性读取 StepDurationMs

估计: 2 天, 12 tests, 4 files

实施顺序
C-9 → C-10 → C-8 (推荐)

理由:

C-9 定义 RecordHandlerLifecycleAsync，C-10 的 DfsBacktrack 依赖它
C-8 的 PageTransition PressBack 插入点在 InterceptionHandler 与 C-9 的 DecideFrameCompletionAsync 共享，避免冲突
C-8 独立性最强，可以最后做
总估计: 7 天, 30 tests, 8 file changes

关键修改文件清单
文件	C-8	C-9	C-10
TraversalEngine.cs (TraceCoordinator + RunAsync + GlobalFSM callbacks)	✅	✅	✅
TraversalFSM.cs (HandlePopupHandlingAsync + HandleErrorHandlingAsync)		✅	
InterceptionHandler.cs (DecideFrameCompletion + OnDynamicMatchNodeSelect)	✅	✅	✅
ITraceRecorder.cs (AICallRecord 定义)			✅
PopupHandler.cs (PopupHandlingResult)		✅	
ContainerHandler.cs (ContainerActionResult)		✅	
验证方法
每个设计完成后:

dotnet build src/UniClaw.Core.sln — 0 错误
dotnet test src/UniClaw.Core.sln — 全绿
跑 sim 测试看 JSONL 输出 — 确认新 SpanType/Metadata/PageId 出现在 trace.jsonl
ArchitectureGuardTests — 确认 enum 值数未变 (SpanType 11 值锁定)
不在范围内 (全部 3 份)
❌ TraceContext VisitSpanId/ParentSpanId (Phase 3 通用关联扩展)
❌ CacheOp SpanType 激活 (无连接组件)
❌ Handler 内部步骤级 trace (detect→classify→preserve→handle→restore→validate 太细)
❌ Handler 直接注入 ITraceCoordinator (违反 "handler 是纯管道" 原则)
❌ ADB/Vision 操作计时 (Phase 3-A，通过 AICallRecord.Metadata 预留)
❌ SpanType enum 值增删 (11 值锁定，需 constitution change flow)
❌ Replay 执行器组件 (Phase 3 功能)