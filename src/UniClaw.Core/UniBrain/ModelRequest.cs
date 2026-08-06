namespace UniClaw.Core.UniBrain;

/// <summary>
/// ModelRequest — AI 模型请求。
/// 对齐 Python AIProvider input: prompt, system_prompt, schema, max_tokens, capability。
/// Capability 为语义标签，流经 IModelRouter / ObservingModelProvider / 传输层，可选且向后兼容。
/// ImageOriginalWidth/ImageOriginalHeight 为原始全屏尺寸（像素）——仅视觉补全路径由
/// PageAnalyzer 从 RawScreenBuffer 填充，供 vision provider 在 Python→C# 边界完成
/// crop/resize 像素逆变换；0 表示未知（fallback 路径），provider 应跳过变换。
/// </summary>
public sealed record class ModelRequest(
    string Prompt,
    string? SystemPrompt = null,
    object? Schema = null,
    int MaxTokens = 4096,
    string? Capability = null,
    int ImageOriginalWidth = 0,
    int ImageOriginalHeight = 0);

/// <summary>
/// ModelResponse — AI 模型响应。
/// 对齐 Python AIResponse 全字段: content, provider_id, mode, input/output tokens, latency, model, success, error。
/// </summary>
public sealed record class ModelResponse(
    string Content,
    string ProviderId,
    string Mode,
    int InputTokens,
    int OutputTokens,
    double LatencyMs,
    string Model = "",
    bool Success = true,
    string? ErrorMessage = null)
{
    /// <summary>
    /// Optional transport diagnostics. Values must be safe for trace output;
    /// providers must never put credentials or prompt/image content here.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Diagnostics { get; init; }

    /// <summary>
    /// True when the model returned a successful HTTP response but zero
    /// content bytes.  This is a structural failure — retrying the same
    /// input will not produce output — and callers should fail fast.
    /// </summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Content);
}
