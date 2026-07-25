namespace UniClaw.Core.UniBrain;

/// <summary>
/// ModelRequest — AI 模型请求。
/// 对齐 Python AIProvider input: prompt, system_prompt, schema, max_tokens, capability。
/// Capability 为语义标签，流经 IModelRouter / ObservingModelProvider / 传输层，可选且向后兼容。
/// </summary>
public sealed record class ModelRequest(
    string Prompt,
    string? SystemPrompt = null,
    object? Schema = null,
    int MaxTokens = 4096,
    string? Capability = null);

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
    string? ErrorMessage = null);
