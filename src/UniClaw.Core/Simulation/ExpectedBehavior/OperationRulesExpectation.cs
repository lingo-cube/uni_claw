using System.Collections.Immutable;

namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// 操作规则验证预期 (D-E4: operation_rules 维度)。
/// 对照 TraversalResult.ActionHistory 验证遍历操作序列的健康性。
/// </summary>
/// <param name="DepthFirstOrder">是否启用 DFS 栈规程检查（tap=push/back=pop，深度永不负数 + 至少一次回退）</param>
/// <param name="NoDuplicateActionsMax">同元素连续重复最大允许次数（0=跳过检查）</param>
public sealed record class OperationRulesExpectation(
    bool DepthFirstOrder = false,
    int NoDuplicateActionsMax = 0);
