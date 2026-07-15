using System.Text.Json.Serialization;
using UniClaw.Core.Domain;

namespace UniClaw.Core.Graph.Models;

/// <summary>
/// 等待模式 — 对齐 Python WaitMode(str,Enum)。
/// </summary>
public enum WaitMode
{
    /// <summary>快速模式（单次检查）</summary>
    [JsonPropertyName("fast")] Fast,
    /// <summary>轮询模式（重复检查直到超时）</summary>
    [JsonPropertyName("polling")] Polling
}

/// <summary>
/// 追踪级别 — 对齐 Python TraceLevel(str,Enum)。
/// </summary>
public enum TraceLevel
{
    /// <summary>不记录</summary>
    [JsonPropertyName("none")] None,
    /// <summary>基本记录</summary>
    [JsonPropertyName("basic")] Basic,
    /// <summary>详细记录</summary>
    [JsonPropertyName("detailed")] Detailed,
    /// <summary>完全记录</summary>
    [JsonPropertyName("full")] Full
}

/// <summary>
/// 入口配置 — V6.8 入口行为参数。对齐 Python entry_config。
/// </summary>
/// <param name="WaitMode">等待模式</param>
/// <param name="WaitTimeoutSeconds">等待超时秒数 (必须为正且不超过 300)</param>
/// <param name="WaitIntervalMs">轮询间隔毫秒 (必须为正且不超过 60000)</param>
/// <param name="ActionDelayMs">动作后延迟毫秒 (必须非负且不超过 60000)</param>
/// <param name="TraceLevel">追踪级别</param>
public sealed record class EntryConfig
{
    /// <summary>等待模式</summary>
    public WaitMode WaitMode { get; init; }

    /// <summary>等待超时秒数</summary>
    public double WaitTimeoutSeconds { get; init; }

    /// <summary>轮询间隔毫秒</summary>
    public int WaitIntervalMs { get; init; }

    /// <summary>动作后延迟毫秒</summary>
    public int ActionDelayMs { get; init; }

    /// <summary>追踪级别</summary>
    public TraceLevel TraceLevel { get; init; }

    /// <summary>
    /// 构造 EntryConfig — 校验参数边界（下界 + 安全上界）。
    /// </summary>
    public EntryConfig(
        WaitMode WaitMode = WaitMode.Fast,
        double WaitTimeoutSeconds = 10.0,
        int WaitIntervalMs = 500,
        int ActionDelayMs = 300,
        TraceLevel TraceLevel = TraceLevel.None)
    {
        if (WaitTimeoutSeconds <= 0 || WaitTimeoutSeconds > 300)
            throw new DomainValidationException(nameof(WaitTimeoutSeconds), WaitTimeoutSeconds);
        if (WaitIntervalMs <= 0 || WaitIntervalMs > 60000)
            throw new DomainValidationException(nameof(WaitIntervalMs), WaitIntervalMs);
        if (ActionDelayMs < 0 || ActionDelayMs > 60000)
            throw new DomainValidationException(nameof(ActionDelayMs), ActionDelayMs);

        this.WaitMode = WaitMode;
        this.WaitTimeoutSeconds = WaitTimeoutSeconds;
        this.WaitIntervalMs = WaitIntervalMs;
        this.ActionDelayMs = ActionDelayMs;
        this.TraceLevel = TraceLevel;
    }
}
