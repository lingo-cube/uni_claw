using UniClaw.Core.Graph.Models;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.StateMachine.Progress;

/// <summary>
/// Progress context — Progress control and pacing.
/// 5 个字段封装所有进度相关状态：步骤计数、深度控制、完成策略、动作历史。
/// </summary>
public sealed class ProgressContext : IProgressContext
{
    // --- 5 private fields ---
    private int _stepCount;
    private readonly int _maxDepth;
    private CompletionPolicy? _completionPolicy;
    private readonly List<ActionRecord> _actionHistory;
    private int _waitAfterActionMs;

    /// <summary>构造 ProgressContext</summary>
    public ProgressContext(int maxDepth)
    {
        _stepCount = 0;
        _maxDepth = maxDepth;
        _completionPolicy = null;
        _actionHistory = new List<ActionRecord>(5);
        _waitAfterActionMs = 300;
    }

    // --- IProgressContext implementation ---

    /// <inheritdoc />
    public int StepCount => _stepCount;

    /// <inheritdoc />
    public int MaxDepth => _maxDepth;

    /// <inheritdoc />
    public CompletionPolicy? CompletionPolicy => _completionPolicy;

    /// <inheritdoc />
    public IReadOnlyList<ActionRecord> ActionHistory => _actionHistory;

    /// <inheritdoc />
    public int WaitAfterActionMs => _waitAfterActionMs;

    // --- Mutation methods (engine-only) ---

    /// <summary>递增步骤计数</summary>
    public void IncrementStepCount() => _stepCount++;

    /// <summary>添加动作历史 (保持最多 5 条)</summary>
    public void AddActionHistory(ActionRecord record)
    {
        _actionHistory.Add(record);
        if (_actionHistory.Count > 5)
            _actionHistory.RemoveAt(0);
    }

    /// <summary>设置完成策略</summary>
    public void SetCompletionPolicy(CompletionPolicy? value) => _completionPolicy = value;

    /// <summary>设置等待时间</summary>
    public void SetWaitAfterActionMs(int value) => _waitAfterActionMs = value;
}
