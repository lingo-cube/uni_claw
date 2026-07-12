## Why

`ITraversalContext` 接口暴露了 3 个可写属性（CurrentFrame, GlobalState, LastError），允许任何持有该接口的代码修改这些状态。这违反了 D-I (Context Decomposition) 和 D-V (Interface Extraction) 建立的"接口只读，concrete 可变"模式，且与 FSM 独立原则不一致。

D-7 (GlobalState 暂留 ITraversalContext) 是遗留的设计债务，需在 D-I 完成后解决。

## What Changes

- **BREAKING**: `ITraversalContext` 移除 3 个属性的 setters（CurrentFrame, GlobalState, LastError），改为只读 getters
- **NEW**: `TraversalRuntimeContext` 添加 3 个 mutation 方法：`SetCurrentFrame()`, `SetGlobalState()`, `SetLastError()`
- **NEW**: `TraversalFSM` 添加 `RuntimeContext` 属性（concrete 类型），用于内部 mutation
- **MODIFY**: `TraversalFSM` 和 `PopupHandler` 改用 `SetXxx()` 方法进行状态修改

## Capabilities

### New Capabilities

None — 本次变更不引入新功能，仅为架构健康度改进。

### Modified Capabilities

None — 本次变更不改变 spec 级别的行为，仅改变 API 形状。

## Impact

**受影响代码**:
- `StateMachine/TraversalState.cs` — ITraversalContext 接口定义
- `StateMachine/TraversalRuntimeContext.cs` — 添加 SetXxx() 方法
- `StateMachine/TraversalFSM.cs` — 添加 RuntimeContext 属性，5 处赋值改为 SetXxx()
- `StateMachine/PopupHandler.cs` — StateRestorer 改用 SetXxx()

**测试影响**:
- 相关测试需更新为使用 SetXxx() 而非属性赋值
- 617 CI tests 预期保持全绿

**依赖影响**:
- 无新增 NuGet 依赖
- ITraversalStateMachine.Context 保持 ITraversalContext 类型（只读视图）
