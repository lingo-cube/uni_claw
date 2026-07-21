using UniClaw.Core.StateMachine;

namespace UniClaw.Core.Traversal;

/// <summary>
/// IInterceptionHandler — FSM 拦截/覆盖逻辑接口 (StepOrchestrator 步骤 8-10)。
/// StepOrchestrator 只保留生命周期编排, 拦截决策全部委托本接口 (可 mock 独立测试)。
/// 调用守卫 (nextState 匹配 + BranchAllowedSources / DynamicMatch 判定) 留在 StepOrchestrator —
/// 它们是"是否触发拦截"的编排条件, 不是拦截逻辑本身。
/// </summary>
public interface IInterceptionHandler
{
    /// <summary>
    /// Step 8: BRANCH interception — 推下一个未访问子节点, 或 (DynamicMatch 耗尽时)
    /// 导航检测 (D-74) → 滚动发现 → frame 完成兜底。
    /// </summary>
    Task<InterceptionResult> OnBranch(StepContext ctx, TraversalState fromState);

    /// <summary>
    /// Step 9: NODE_SELECT + DYNAMIC_MATCH — 推子节点; 耗尽时导航检测 → 滚动 →
    /// 非根节点 PressBack+Pop 返回父帧 / 根节点标记帧完成。
    /// </summary>
    Task<InterceptionResult> OnDynamicMatchNodeSelect(StepContext ctx);

    /// <summary>
    /// Step 10: FRAME_COMPLETE override — DynamicMatch 仍有未访问子节点时
    /// 覆盖为 NodeSelect 并推子节点; 否则放行 FrameComplete。
    /// </summary>
    Task<InterceptionResult> OnFrameComplete(StepContext ctx);
}

/// <summary>
/// InterceptionResult — FSM override 结果值类型, 替代 3 个 ref bool + 1 个 ref TraversalState。
/// 可变 record struct (非 readonly): 内部 helper (TryHandleNavigation) 通过 ref 修改。
/// default 值 (default(TraversalState), false, false, false) 不得直接应用到 FSM 状态 —
/// StepOrchestrator 以 intercepted flag 守卫, 仅在 handler 实际被调用时应用。
/// </summary>
public record struct InterceptionResult(
    TraversalState NextState,
    bool ChildPushed,
    bool FrameCompleted,
    bool FrameOverrideTriggered);
