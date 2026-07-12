using UniClaw.Core.Graph.Models;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.StateMachine.Progress;

/// <summary>
/// Progress context read-only interface — Progress control and pacing.
/// 只读属性暴露，mutation 方法只在 concrete class。
/// </summary>
public interface IProgressContext
{
    /// <summary>步骤计数</summary>
    int StepCount { get; }

    /// <summary>最大深度</summary>
    int MaxDepth { get; }

    /// <summary>完成策略</summary>
    CompletionPolicy? CompletionPolicy { get; }

    /// <summary>动作历史 (最多 5 条)</summary>
    IReadOnlyList<ActionRecord> ActionHistory { get; }

    /// <summary>动作后等待时间 (毫秒)</summary>
    int WaitAfterActionMs { get; }
}
