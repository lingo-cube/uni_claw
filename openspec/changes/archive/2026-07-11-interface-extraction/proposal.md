## Why

StepOrchestrator 和 TraversalEngine 的 6 个关键子组件全是 concrete sealed class，StepContext 的 4 个字段引用 concrete types。这导致：
- **无法 mock 测试** — StepOrchestrator 14-step 主循环无法用 mock 子组件隔离测试
- **测试覆盖率天花板** — 单元测试只能走真实 DynamicChildManager/TraceCoordinator 路径，无法验证独立分支
- **D-IV 前置依赖** — StepOrchestrator 分解为 4 子组件需要可替换的依赖注入点

路线图 20-b §5 将 D-V 列为 P1（与 D-15 并行），核心产出是 6 个新 interface + StepContext 参数类型同步。

## What Changes

- **提取 6 个新 interface**：IDynamicChildManager, ITraceCoordinator, IEntryPolicyExecutor, IPageCacheManager, IPageSnapshotManager, INodeStackAdapter
- **StepContext 参数类型同步**：4 个 concrete 字段改用 interface 类型 (ChildMgr, Trace, SnapshotMgr, Stack)
- **TraversalEngine 构造器改用 interface 类型注入**：DynamicChildManager/TraceCoordinator 等字段改为 interface 类型
- **DictionaryNodeRegistry**: 已有 INodeRegistry，无需提取
- **PageSnapshotManager static → instance**: 2 个 static 方法改为 instance 方法以支持 interface

## Capabilities

### New Capabilities

- `interface-extraction`: 6 个新 interface 定义 + sealed class 实现 + StepContext 参数类型同步 + TraversalEngine 注入改用 interface

### Modified Capabilities

- `step-orchestrator`: StepContext 4 个字段从 concrete → interface 类型（改动 StepContext 构造器签名，不改行为）

## Impact

- **代码**: `TraversalEngine.cs` (6 个 inner class 提取 interface + 构造器改 interface 注入), `StepContext.cs` (4 字段改 interface 类型), `IGraphTraversalEngine.cs` (可能需同步 IActionExecutor 参数)
- **API**: 无 breaking change — 新 interface 是 additive, sealed class 保持不变只是多了 interface 实现
- **StepContext**: positional 参数类型从 concrete → interface, 是签名变更但行为不变
- **依赖**: D-IV (P3 StepOrchestrator 分解) 依赖此产出 — 分解后子组件需要可 mock 的依赖注入点
- **测试**: 新增 interface compliance guard test (验证每个 sealed class 实现了对应 interface 的全部方法)
