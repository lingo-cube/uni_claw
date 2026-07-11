## Context

6 Traversal 子组件全是 `sealed class` 无 interface，StepContext 的 4 个字段引用 concrete types。当前已有 3 个 interface (INodeRegistry, IGraphTraversalEngine, IActionExecutor)，加上 IVisionProvider 和 INodeStack (StateMachine 层)。但 DynamicChildManager、TraceCoordinator、EntryPolicyExecutor、PageCacheManager、PageSnapshotManager、NodeStackAdapter 全无 interface abstraction。

核心痛点：
- StepOrchestrator 14-step 主循环无法用 mock 子组件隔离测试
- TraversalEngine 构造器直接 `new` 子组件，无法注入测试替身
- StepContext 4 个 concrete 字段 (ChildMgr, Trace, SnapshotMgr, Stack) 阻止 mock

代码现状：
- 所有 6 个类是 `TraversalEngine.cs` 内的 nested public sealed class（除了 DictionaryNodeRegistry 是独立文件）
- StepContext 是 StateMachine namespace 的 sealed record class，13 个 positional init-only 参数
- TraversalEngine.Initialize() 直接 `new` 所有子组件（lines 95-99）

## Goals / Non-Goals

**Goals:**

1. 提取 6 个新 interface，每个对应 1 个 sealed class 的 public API
2. StepContext 4 个 concrete 字段改 interface 类型 (ChildMgr, Trace, SnapshotMgr, Stack)
3. TraversalEngine 构造器新增可选 interface 参数注入（保留 `new` 默认值向后兼容）
4. 每个 sealed class 实现对应 interface
5. 新增 interface compliance guard test — 验证每个 sealed class 实现了对应 interface 的全部方法

**Non-Goals:**

- **Do NOT decompose TraversalRuntimeContext** — D-I (P2), 依赖 D-15 (已完成)
- **Do NOT change ITraversalContext** — D-III (P2)
- **Do NOT decompose StepOrchestrator** — D-IV (P3)
- **Do NOT refactor TraceCoordinator method signatures** — 只提取现有 public 方法为 interface，不改方法签名
- **Do NOT move interfaces to separate namespace** — 保持在 Traversal namespace 与 sealed class 同位置

## Decisions

### D-V-1: Interface 定义位置 — 同文件嵌套

新 interface 定义在 `TraversalEngine.cs` 内，与现有 `INodeRegistry` 同位置（nested public interface in same file）。

**理由**: (1) 保持一致性 — INodeRegistry 已在 TraversalEngine.cs 内; (2) 避免过度拆分文件 — 6 个 interface 若各占 1 文件，多 6 文件但内容极简; (3) 现阶段 interface 是 sealed class 的镜像提取，不是独立设计，与 class 同文件最直观。

**Alternative**: 每个 interface 单独文件 (IPageSnapshotManager.cs 等)。Rejected — 内容极简 (2-3 方法签名)，不值得独立文件。将来当 interface 成长（D-IV 分解后子组件可能扩展 interface），可以再拆文件。

### D-V-2: Interface 方法签名 — 精确镜像 public API

每个 interface 的方法签名 SHALL 精确镜像对应 sealed class 的 public 方法，不改参数类型或返回类型。

**关键考量**:

| Interface | 方法数 | 特殊问题 |
|-----------|--------|---------|
| IDynamicChildManager | 3 (GetNextUnvisitedChild, Generate, Invalidate) | 构造器参数含 concrete TraceCoordinator → 改为 ITraceCoordinator |
| ITraceCoordinator | 18 (Active + 16 Record + ShouldRecord* + GetStepSnapshot) | 最大 interface，但方法签名稳定 |
| IEntryPolicyExecutor | 2 (Execute, BuildChain) | 最小 interface |
| IPageCacheManager | 2 (Update, Restore) | 方法参数含 TraversalRuntimeContext → 改为 ITraversalContext |
| IPageSnapshotManager | 2 (Fingerprint, HasChanged) | **static → instance**: 当前是 static 方法，interface 需 instance 方法 |
| INodeStackAdapter | 3 (Push, Pop, Peek) | 构造器参数含 TraversalRuntimeContext → 改为 ITraversalContext |

### D-V-3: PageSnapshotManager static → instance 转换

PageSnapshotManager 当前 2 个 static 方法 (Fingerprint, HasChanged)。interface 要求 instance 方法。

**决策**: 将 static 方法改为 instance 方法。sealed class 内部逻辑不变，只是去掉 `static` 修饰符。

**理由**: (1) PageSnapshotManager 无任何 instance state — 改为 instance 方法后仍是纯计算; (2) StepContext 已有 `SnapshotMgr: PageSnapshotManager` instance 字段 (`new PageSnapshotManager()`)，调用时已是 `ctx.SnapshotMgr.Fingerprint(...)` 而非 `PageSnapshotManager.Fingerprint(...)`; (3) 改为 instance 方法是最小改动 — 不需要 singleton 或 static-adapter wrapper。

**Alternative**: 保持 static + 定义 IPageSnapshotManager 为 static 适配器。Rejected — C# interface 不能包含 static 方法 (C# 8 可以但需 default implementation，增加复杂度)。

**Alternative**: static 方法保留 + interface 新增 instance 包装。Rejected — 双倍代码量，无实际收益。

### D-V-4: PageCacheManager / NodeStackAdapter 参数类型 → ITraversalContext

PageCacheManager 的 Update/Restore 和 NodeStackAdapter 构造器当前接收 `TraversalRuntimeContext` (concrete class)。

**决策**: interface 方法签名改用 `ITraversalContext`。sealed class 实现保持接收 `TraversalRuntimeContext`，在 interface 实现中通过 cast 或 pattern 匹配桥接。

**理由**: (1) ITraversalContext 是已有 read-only interface，PageCacheManager/NodeStackAdapter 只需要 read 操作; (2) interface 方法签名的消费者不持有 TraversalRuntimeContext — StepContext.Context 是 ITraversalContext 类型; (3) D-I (P2) 拆分后 TraversalRuntimeContext 不再是单 class，interface 签名需要面向 ITraversalContext。

**Implementation pattern**:
```csharp
// Interface method uses ITraversalContext
public interface IPageCacheManager
{
    void Update(string path, PageCacheInfo pageInfo, ITraversalContext context);
    IReadOnlyList<MenuItem>? Restore(string path, ITraversalContext context);
}

// Sealed class implementation casts to concrete type for internal access
public sealed class PageCacheManager : IPageCacheManager
{
    public void Update(string path, PageCacheInfo pageInfo, ITraversalContext context)
    {
        // Cast to TraversalRuntimeContext for PageCache internal access
        ((TraversalRuntimeContext)context).PageCache[path] = pageInfo;
    }
}
```

**Alternative**: 保持 TraversalRuntimeContext 参数。Rejected — interface 上的 concrete 类型违反依赖倒置原则，mock 测试时无法注入替身。

### D-V-5: DynamicChildManager 构造器 → ITraceCoordinator

DynamicChildManager 构造器当前接收 `TraceCoordinator?` (concrete class)。

**决策**: 构造器参数改 `ITraceCoordinator?`。内部调用 `trace.RecordDynamicLifecycle(...)` 等方法不变，只是类型签名改为 interface。

**理由**: 最小改动 — DynamicChildManager 只调用 TraceCoordinator 的 Record 方法，这些方法在 interface 上完全定义。

### D-V-6: StepContext 参数类型同步

StepContext 4 个字段从 concrete → interface:

| 字段 | 当前类型 | 新类型 |
|------|---------|--------|
| ChildMgr | DynamicChildManager | IDynamicChildManager |
| Trace | TraceCoordinator | ITraceCoordinator |
| SnapshotMgr | PageSnapshotManager | IPageSnapshotManager |
| Stack | NodeStackAdapter | INodeStackAdapter |

**理由**: 这是 D-V 的必要连带变更 — 如果 StepContext 保持 concrete 类型，mock 测试仍然不可能。

**Alternative**: 只提取 interface 不改 StepContext。Rejected — roadmap §5 明确标注这是"必要连带变更，不是额外 task"。不改 StepContext 等于没解除测试天花板。

### D-V-7: TraversalEngine 构造器 — 保持向后兼容

TraversalEngine 构造器不改签名 — 保持 `IVisionProvider` + `IActionExecutor` + `TraversalPlan` + `TraversalEngineConfig` + `ITraceRecorder?`。

**理由**: (1) 构造器已在 Initialize() 中 `new` 所有子组件，这些子组件的构造依赖 TraversalRuntimeContext 和 INodeRegistry，而 context 和 registry 也在 Initialize() 中创建; (2) 将所有子组件作为构造器参数会爆炸参数列表 (5+6=11+参数); (3) 子组件是 engine 的 internal implementation detail，不是 public API。

**StepContext 组装改动**: Initialize() 中创建子组件时保持 `new`，但类型声明改为 interface:
```csharp
IDynamicChildManager childMgr = new DynamicChildManager(registry);
ITraceCoordinator trace = new TraceCoordinator(...);
IPageSnapshotManager snapshotMgr = new PageSnapshotManager();
INodeStackAdapter stack = new NodeStackAdapter(_ctx, registry);
```

**Alternative**: TraversalEngine 构造器新增子组件参数。Rejected — 破坏现有调用者，且子组件依赖 engine 内部创建的 context/registry，外部无法合理提供。

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| PageSnapshotManager static → instance 改动影响所有现有调用 | 现有调用全在 StepOrchestrator 中，使用 `ctx.SnapshotMgr.Fingerprint(...)` — instance 语法不变。不使用 `PageSnapshotManager.Fingerprint(...)` 的 static 调用需改为 instance。搜索确认：只有 StepOrchestrator 内调用，全是 instance 形式 |
| PageCacheManager/NodeStackAdapter 参数 ITraversalContext → concrete cast | cast 是临时的 — D-I (P2) 拆分后 TraversalRuntimeContext 的 PageCache/internal 字段将通过 sub-context interface 访问，消除 cast |
| TraceCoordinator interface 18 方法过大 | 大但稳定 — 方法数在 Phase 2.2 已锁定 (13/16 implemented + 2 stubs + Active + ShouldRecord* + GetStepSnapshot)。将来不增长。ISP 拆分（ITraceRecorder vs ITraceSpanRecorder）是 Phase 3 话题，不在 D-V scope |
| StepContext 签名变更影响所有 handler 测试 | handler 测试创建 StepContext 时用 `new DynamicChildManager()` / `new TraceCoordinator()` — 改为 interface 类型后构造器参数不变，只是字段类型改为 interface。编译器自动兼容 sealed→interface 赋值 |
| Interface 嵌套在 TraversalEngine.cs 使文件变长 | TraversalEngine.cs 当前已 ~1200 行。6 个 interface 声明约 60 行，总文件 ~1260 行。暂不拆 — interface 是 mirror 提取，将来可独立拆文件 |
