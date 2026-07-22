namespace UniClaw.Core.UniBrain;

/// <summary>
/// ModelRequest — AI 模型请求。
/// 对齐 Python AIProvider input: prompt, system_prompt, schema, max_tokens。
/// </summary>
public sealed record class ModelRequest(
    string Prompt,
    string? SystemPrompt = null,
    object? Schema = null,
    int MaxTokens = 4096);

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
