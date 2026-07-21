## Context

C-7 完成 JSONL 存储后端后（FileTraceStorage + IFileProvider + PhysicalFileProvider），采集端仍有大量空洞。现有 TraceCoordinator 已定义 15 个 Record 方法，但约一半从未被调用（PopupHandling、ContainerHandling、ErrorHandling、DfsBacktrack 等 SpanType 的 ExecutionRecord 从未写入）。此外 GlobalFSM Context=null、DurationMs 固定 0、PageId 永远空。

完整设计参见 `docs/refactor/2026-07-21-trace-collection-completion-design.md`。

## Goals / Non-Goals

**Goals:**
- C-9: 补齐 Handler 生命周期 trace — PopupHandler/ContainerHandler/ErrorHandler 各产生一条 ExecutionRecord
- C-10: 补齐 Operation Timing — DurationMs 从 Stopwatch 真实采集，DfsBacktrack 3 个分支各一个 trace 点，AICallRecord.Metadata 可填
- C-8: 补齐 State Flow — GlobalFSM Context 带 NodeId/StepNumber、8 状态全覆盖、PageTransition 2 个触发路径、ExecutionRecord.PageId 填充
- TraceHandlerAttribute 定义 + Phase 3 路线图（ParentSpanId → 源生成器自动父子 span 树）

**Non-Goals:**
- ❌ TraceContext VisitSpanId/ParentSpanId（Phase 3-A）
- ❌ Roslyn 源生成器自动注入（Phase 3-B）
- ❌ CacheOp SpanType 激活（无连接组件）
- ❌ Handler 内部步骤级 trace（detect/classify/preserve/handle/restore/validate 太细）
- ❌ ADB/Vision 操作计时（Phase 3-A，通过 AICallRecord.Metadata 预留）

## Decisions

| # | Decision | Alternative | Rationale |
|---|----------|------------|-----------|
| D-1 | IHandlerTraceWriter ISP 分离 | 直接扩 ITraceCoordinator | ITraceCoordinator 已 15 方法，加 Handler 方法会更大。ISP 独立接口，Handler 只依赖 1 方法 |
| D-2 | Trace 注入在编排层，非 handler 内部 | handler 内部直接调用 | handler 是纯管道原则，不感知 trace。编排层（StepOrchestrator/InterceptionHandler）在 handler 返回后提取 result 字段写入 trace |
| D-3 | DecideFrameCompletion sync→async | 保持 sync + 异步写入 | 需 await RecordHandlerLifecycleAsync，不做 fire-and-forget（防止 trace 丢失） |
| D-4 | TraceCoordinator 自有 Stopwatch | 外部传入 DurationMs | 步骤 start/end 在同一协调器内，Stopwatch 封装简化调用方 |
| D-5 | GlobalFSM closure 捕获 engineCtx | RegisterGlobalFsmTraceCallbacks 传 ctx | ctx 在 engine 初始化后可用，closure 捕获避免额外参数 |
| D-6 | TraceHandlerAttribute C-10 只作文档标注 | C-10 就运行逻辑 | 源生成器是 Phase 3-B 目标，当前运行逻辑会增加耦合。先定义属性并手工注入，Phase 3-B 自动替换 |
| D-7 | Phase 3-A TraceContext +2 字段 | AICallRecord 加 ParentSpanId | ParentSpanId 不是 AI 专用，是所有 span 类型的通用关联字段。放在 TraceContext 使 5 种 record 全部受益 |

## Risks / Trade-offs

- [Risk] DecideFrameCompletion sync→async 可能影响步进延迟 → 记录 trace 后才返回，与 RecordStepStart/End 在同一 async 路径，不额外增加延迟
- [Risk] Handler result 扩展的可选字段（default null）确保向后兼容 → 所有现有调用方无需修改
- [Risk] GlobalFSM ForceState 不触发回调的语义要保持 → 不会误触发 trace
- [Risk] GlobalFSM closure 长期持有 ctx 引用 → ctx 是 engine 生命周期级对象，无泄漏风险
- [Risk] DfsBacktrack 3 个插入点可能 miss 某些回溯路径 → Phase 3 迭代填补

## Phase 3 路线图

| Phase | 内容 | 依赖 |
|-------|------|------|
| **3-A** | TraceContext +2: VisitSpanId, ParentSpanId. 手工传递 parent span | C-10 DurationMs, C-9 Handler trace 稳定 |
| **3-B** | Roslyn 增量源生成器: 扫描 `[TraceHandler]`，编译期注入 span 包装代码 | 3-A 完成，TraceHandlerAttribute 已验证 |
