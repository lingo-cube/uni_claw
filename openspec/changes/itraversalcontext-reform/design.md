## Context

**当前状态**:
- `ITraversalContext` 接口暴露了 3 个可写属性（CurrentFrame, GlobalState, LastError）
- 任何持有该接口的代码都可以修改这些状态，包括 FSM 外部消费者
- 这与 D-I (Context Decomposition) 和 D-V (Interface Extraction) 建立的"接口只读，concrete 可变"模式不一致

**前置依赖**:
- D-I (Context Decomposition) ✅ 完成 — 5 个 sub-contexts 已提取
- D-V (Interface Extraction) 模式已建立

**约束**:
- ITraversalStateMachine.Context 必须保持 ITraversalContext 类型（只读视图）
- FSM/Handler 内部需要 mutation 能力
- 617 CI tests 必须保持全绿

## Goals / Non-Goals

**Goals**:
- ITraversalContext 变成纯只读接口（移除所有 setters）
- 提供明确的 mutation 入口（concrete class 的 SetXxx() 方法）
- 符合 D-I/D-V 的"接口隔离 mutation"模式
- 解决 D-7 (GlobalState 暂留 ITraversalContext)

**Non-Goals**:
- 不移除属性（只移除 setters，getters 保留）
- 不改变 spec 级别行为
- 不新增 NuGet 依赖

## Decisions

### 决策 1: 保持所有属性，只移除 setters

**选择**：ITraversalContext 保留所有 9 个属性的 getters，只移除 3 个 setters

**理由**：
- AI advisor 已有 `TraversalContextSnapshot`（8 个不可变字段），不需要通过 ITraversalContext 访问
- 外部通过 ITraversalStateMachine.Context 读取 GlobalState/LastError 是合理的（了解当前状态）
- 问题在于 **setter**，不在于 getter
- 最小改动原则

**替代方案**：
- 对齐 Snapshot，移除 GlobalState/LastError 属性 — 收益有限，需验证所有外部使用

### 决策 2: Mutation 通过 concrete class 方法

**选择**：在 `TraversalRuntimeContext` 添加 `SetXxx()` 方法

**理由**：
- 符合 D-I/D-V 模式 — 接口只读，concrete 可变
- 方法调用（`SetGlobalState()`）比属性赋值（`GlobalState =`）更明确这是 mutation 操作
- FSM 已经持有 TraversalRuntimeContext 引用，只需添加 RuntimeContext 属性暴露它

**替代方案**：
- IWritableTraversalContext 子接口 — 多一个接口类型，FSM 需要转换
- Context 改为 concrete 类型 — 失去接口抽象

### 决策 3: TraversalFSM 添加 RuntimeContext 属性

**选择**：TraversalFSM 添加 `RuntimeContext` 属性（concrete 类型），内部用于 mutation

**理由**：
- ITraversalStateMachine.Context 保持 ITraversalContext（只读视图）— 不破坏现有接口
- RuntimeContext 提供可写视图 — FSM 内部使用
- 符合"接口隔离"原则

**实现**：
```csharp
public sealed class TraversalFSM : ITraversalStateMachine
{
    private readonly TraversalRuntimeContext _runtimeContext;

    // 只读视图（接口）
    public ITraversalContext Context => _runtimeContext;

    // 可写视图（concrete）— 新增
    public TraversalRuntimeContext RuntimeContext => _runtimeContext;
}
```

## Risks / Trade-offs

### Risk 1: 测试大量修改

**风险**：5 处赋值改为 SetXxx() 调用，相关测试可能需要更新

**缓解**：
- 变更范围明确且小（5 处）
- 每步完成后运行 `dotnet test` 验证
- 渐进式实施，每步有测试保护

### Risk 2: 外部代码依赖 ITraversalContext setters

**风险**：未知的外部代码可能依赖这些 setters

**缓解**：
- ITraversalContext 主要服务于 FSM 内部使用
- 检查所有 StateMachine 命名空间下的使用
- 编译时错误会立即暴露问题

### Trade-off: 接口保留 GlobalState/LastError 属性

**取舍**：
- 优点：外部可以读取当前状态（如监控、日志）
- 缺点：接口暴露了 Engine 内部状态

**结论**：可接受 — 读取不违反设计原则，问题在于 setter

## Migration Plan

**步骤顺序**（逐步验证，每步测试全绿）：

1. **修改 ITraversalContext** — 移除 3 个 setters
2. **修改 TraversalRuntimeContext** — 添加 3 个 SetXxx() 方法
3. **修改 TraversalFSM** — 添加 RuntimeContext 属性，改用 SetXxx()
4. **修改 PopupHandler** — 改用 SetXxx()
5. **更新测试** — 所有赋值改为 SetXxx()
6. **验证** — 617 tests 全绿
7. **更新文档** — docs/system/decisions/log.md D-7 状态改为 Fixed

**Rollback 策略**：
- 每步提交一个 commit
- 如果测试失败，revert 到上一个 commit
- 无需数据库迁移或外部 API 变更

## Open Questions

无 — 所有设计决策已确认。

## Implementation Record

**完成时间**: 2026-07-12

**实施内容**:
1. ✅ ITraversalContext 接口移除 3 个 setters（CurrentFrame, GlobalState, LastError）
2. ✅ TraversalRuntimeContext 添加 3 个 SetXxx() 方法
3. ✅ TraversalFSM 添加 RuntimeContext 属性（concrete 类型用于内部 mutation）
4. ✅ TraversalFSM 和 PopupHandler 改用 SetXxx() 方法（5 处赋值点）
5. ✅ 所有相关测试更新为使用 SetXxx() 方法
6. ✅ 617 CI tests 全绿验证通过
7. ✅ docs/system/decisions/log.md D-7 状态更新为 Fixed

**结果**:
- ITraversalContext 现在是纯只读接口（符合 D-I/D-V 模式）
- FSM 内部通过 RuntimeContext 进行可变操作
- 解决了 D-7 (GlobalState 暂留 ITraversalContext) 设计债务
