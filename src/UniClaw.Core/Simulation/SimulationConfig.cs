namespace UniClaw.Core.Simulation;

/// <summary>仿真运行配置</summary>
public sealed record class SimulationConfig
{
    /// <summary>最大步数（安全上限，防止死循环）</summary>
    public int MaxSteps { get; init; } = 1000;

    /// <summary>栈最大深度</summary>
    public int MaxDepth { get; init; } = 10;

    /// <summary>true = handler 异常立即中断; false = 记录后继续（走 ErrorHandling 路径）</summary>
    public bool ThrowOnError { get; init; } = false;

    /// <summary>仿真步间延时（毫秒），模拟真实操作延迟。0 = 无延时</summary>
    public int SimulateDelayMs { get; init; } = 0;
}
