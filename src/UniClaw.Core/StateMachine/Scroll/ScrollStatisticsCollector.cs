namespace UniClaw.Core.StateMachine.Scroll;

/// <summary>
/// 步骤 7：滚动统计收集
/// 跟踪滚动操作的各种统计指标。
/// </summary>
public sealed class ScrollStatisticsCollector
{
    /// <summary>已滚动次数</summary>
    public int ScrolledCount { get; private set; }

    /// <summary>跳过次数（不可滚动或已到底部）</summary>
    public int SkippedCount { get; private set; }

    /// <summary>检测到跳跃次数</summary>
    public int JumpDetectedCount { get; private set; }

    /// <summary>恢复成功次数</summary>
    public int JumpRecoveredCount { get; private set; }

    /// <summary>总滚动距离</summary>
    public double TotalDistance { get; private set; }

    /// <summary>所有滚动步长历史</summary>
    private readonly List<double> _stepHistory = new();

    /// <summary>滚动步长历史（只读）</summary>
    public IReadOnlyList<double> StepHistory => _stepHistory;

    /// <summary>
    /// 创建滚动统计收集器
    /// </summary>
    public ScrollStatisticsCollector()
    {
    }

    /// <summary>重置所有统计</summary>
    public void Reset()
    {
        ScrolledCount = 0;
        SkippedCount = 0;
        JumpDetectedCount = 0;
        JumpRecoveredCount = 0;
        TotalDistance = 0.0;
        _stepHistory.Clear();
    }

    /// <summary>记录滚动操作</summary>
    /// <param name="distance">滚动距离</param>
    /// <param name="step">步长</param>
    public void RecordScroll(double distance, double step)
    {
        ScrolledCount++;
        TotalDistance += distance;
        _stepHistory.Add(step);
    }

    /// <summary>记录跳过操作</summary>
    public void RecordSkip()
    {
        SkippedCount++;
    }

    /// <summary>记录跳跃检测</summary>
    public void RecordJumpDetected()
    {
        JumpDetectedCount++;
    }

    /// <summary>记录跳跃恢复成功</summary>
    public void RecordJumpRecovered()
    {
        JumpRecoveredCount++;
    }

    /// <summary>计算平均步长</summary>
    public double AverageStep =>
        _stepHistory.Count > 0 ? _stepHistory.Average() : 0.0;

    /// <summary>计算最大步长</summary>
    public double MaxStep =>
        _stepHistory.Count > 0 ? _stepHistory.Max() : 0.0;

    /// <summary>计算最小步长</summary>
    public double MinStep =>
        _stepHistory.Count > 0 ? _stepHistory.Min() : 0.0;

    /// <summary>跳跃检测率</summary>
    public double JumpDetectionRate =>
        ScrolledCount > 0 ? (double)JumpDetectedCount / ScrolledCount : 0.0;

    /// <summary>跳跃恢复成功率</summary>
    public double JumpRecoveryRate =>
        JumpDetectedCount > 0 ? (double)JumpRecoveredCount / JumpDetectedCount : 0.0;

    /// <summary>获取统计摘要</summary>
    public string GetSummary()
    {
        return $"Scroll Statistics: " +
               $"Scrolled={ScrolledCount}, " +
               $"Skipped={SkippedCount}, " +
               $"Jumps={JumpDetectedCount}, " +
               $"Recovered={JumpRecoveredCount}, " +
               $"TotalDistance={TotalDistance:F3}, " +
               $"AvgStep={AverageStep:F3}";
    }
}
