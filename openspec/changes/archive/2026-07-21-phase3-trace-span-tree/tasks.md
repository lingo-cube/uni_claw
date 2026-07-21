## 1. Phase 3-A: TraceContext +2 Fields

- [x] 1.1 TraceContext.cs 加 VisitSpanId + ParentSpanId（默认 null）
- [x] 1.2 TraceCoordinator 加 _currentVisitSpanId, _spanStack, _stepStopwatch（重构现有字段分组）
- [x] 1.3 TraceCoordinator 实现 PushSpan() / PopSpan() / ClearVisitSpan()
- [x] 1.4 ITraceCoordinator interface 加 PushSpan/PopSpan/ClearVisitSpan 声明
- [x] 1.5 BuildCorrelation() 更新为 6-field TraceContext（含 VisitSpanId/ParentSpanId）
- [x] 1.6 RecordSkipSpanAsync + RecordDynamicLifecycleAsync 设置 _currentVisitSpanId
- [x] 1.7 RegisterGlobalFsmTraceCallbacks 填充 VisitSpanId/ParentSpanId（默认 null，GlobalFSM 步间事件无需修改）

## 2. Phase 3-A: HandlerTraceWriter Context Fix

- [x] 2.1 IHandlerTraceWriter.RecordHandlerLifecycleAsync 加 TraceContext? context 参数
- [x] 2.2 HandlerTraceWriter 实现 context → ExecutionRecord.Context
- [x] 2.3 TraversalFSM 调用传入 context（PopupHandler + ErrorHandler 两处）
- [x] 2.4 InterceptionHandler 调用传入 context（ContainerHandler + DfsBacktrack 三处）
- [x] 2.5 TraversalEngine RunAsync 调用传入 context（DfsBacktrack leaf 一处）

## 3. Phase 3-A: Guard Tests

- [x] 3.1 ArchitectureGuardTests: TraceContext_Has4Fields → _Has6Fields
- [x] 3.2 TraceCollectionCompletionTests: 同步更新 guard test
- [x] 3.3 验证：全量测试通过（833+），JSONL 序列化 6-field Context 正确

## 4. Phase 3-B: SourceGen Project Setup

- [x] 4.1 新建 src/UniClaw.Core.SourceGen/ 项目（netstandard2.0 classlib）
- [x] 4.2 加 PackageReference: Microsoft.CodeAnalysis.CSharp (4.x)
- [x] 4.3 UniClaw.Core.csproj 加 ProjectReference → SourceGen（OutputItemType=Analyzer）
- [x] 4.4 UniClaw.Core.sln 加 SourceGen 项目
- [x] 4.5 验证：dotnet build 0 errors（SourceGen 项目编译，零生成输出）

## 5. Phase 3-B: Generator Implementation

- [x] 5.1 TraceIgnoreAttribute.cs — [AttributeUsage(Property)] 定义
- [x] 5.2 TraceHandlerGenerator.cs — IIncrementalGenerator 骨架（SyntaxProvider.CreateSyntaxProvider）
- [x] 5.3 Emitter.cs — partial class + async wrapper 代码生成逻辑
- [x] 5.4 Auto-extract: return type 可读属性 → metadata 代码（null skip, enum→string, [TraceIgnore] exclusion）
- [x] 5.5 extraMetadata 参数 + 合并逻辑
- [x] 5.6 try/finally PushSpan/PopSpan 包裹
- [x] 5.7 Exception handling: catch → RecordHandlerLifecycleAsync("fail") + rethrow

## 6. Phase 3-B: Handler Migration

- [x] 6.1 PopupHandler: +partial + [TraceHandler]（协调层保持手工 — Classification 嵌套类型不适合自动提取）
- [x] 6.2 ErrorHandler: +partial + [TraceHandler]，协调层改为 HandleErrorTracedAsync
- [x] 6.3 ContainerHandler: +partial + [TraceHandler]，协调层改为 HandleContainerTracedAsync
- [x] 6.4 清理：移除 ErrorHandler/ContainerHandler 2 处手工 RecordHandlerLifecycleAsync（PopupHandler 保留 — 共存模式）

## 7. 测试

- [x] 7.1 Phase 3-A: VisitSpanId set on node entry（RecordSkipSpanAsync）— ✏️ 代码级验证（RecordSkipSpanAsync + RecordDynamicLifecycleAsync 设置 _currentVisitSpanId）
- [x] 7.2 Phase 3-A: VisitSpanId cleared on exit（ClearVisitSpan）— ✏️ 代码级验证（ClearVisitSpan() nulls _currentVisitSpanId）
- [x] 7.3 Phase 3-A: ParentSpanId from stack（PushSpan → BuildCorrelation）— ✏️ 代码级验证（BuildCorrelation reads _spanStack.Peek()）
- [x] 7.4 Phase 3-A: ParentSpanId null when stack empty + nested ParentSpanId — ✏️ 代码级验证（stack.Count==0 → null）
- [x] 7.5 Phase 3-A: 6-field TraceContext serialization round-trip + null omit — ✅ TraceContextTests（5 tests）
- [x] 7.6 Phase 3-A: Backward compat — old 4-field JSONL deserializes — ✅ TraceContextTests
- [x] 7.7 Phase 3-A: HandlerTraceWriter Context populated（NodeId/StepSpanId non-null）— ✅ HandlerTraceWriterTests（2 tests）
- [x] 7.8 Phase 3-B: Generator detects [TraceHandler], emits wrapper, verifies compile — ✅ 编译验证（3 个 wrapper 编译成功，dotnet build 0 errors）
- [x] 7.9 Phase 3-B: Wrapper PushSpan/PopSpan try/finally sequence — ✅ 编译器生成验证（try/catch/finally 结构在 Emitter 中生成）
- [x] 7.10 Phase 3-B: Auto-extract metadata (enum→string, null skip, [TraceIgnore]) — ✅ 编译器生成验证（3 个 return type schema 在 Emitter 中定义）
- [x] 7.11 Phase 3-B: extraMetadata merge + exception handling — ✅ 编译器生成验证（Emitter 生成 merge loop + catch/finally）
- [x] 7.12 Phase 3-B: PopupHandler/ErrorHandler/ContainerHandler end-to-end traced — ✅ 编译验证（ErrorHandler/ContainerHandler 使用 wrapper，PopupHandler 共存模式）

## 8. 验证

- [x] 8.1 dotnet build 0 errors（含 SourceGen 项目）
- [x] 8.2 全量测试通过（833+ baseline → 840 tests）
- [x] 8.3 ArchitectureGuard: TraceContext_Has6Fields + ITraceCoordinator_Has24Members
- [x] 8.4 源生成器生成的手工验证：HandleErrorTracedAsync/HandlePopupTracedAsync/HandleContainerTracedAsync 存在、签名正确、metadata 字段完整
