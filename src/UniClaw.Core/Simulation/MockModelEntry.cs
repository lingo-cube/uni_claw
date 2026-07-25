namespace UniClaw.Core.Simulation;

/// <summary>
/// MockModelEntry — 单条预设响应条目 (capability → 预设内容/token/延迟)。
/// 供 MockModelProvider 按 capability 查表返回，对齐 Python mock fixture 的响应单元。
/// </summary>
public sealed record class MockModelEntry(
    string Content,
    int InputTokens = 0,
    int OutputTokens = 0,
    double LatencyMs = 0,
    bool Success = true,
    string? ErrorMessage = null);
