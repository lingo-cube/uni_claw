using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Core.Graph.Models;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// AI 推理输出的意图槽位 — 仅包含 AI 需要推理的维度。
/// 事实性字段（TargetApp / Target / Depth / Entry）由调用方从 scenario JSON 提供，
/// 不经过 AI 推理。此 DTO 与 PromptTemplateRegistry.ExtractIntent 的输出 schema 对齐。
/// </summary>
internal sealed record class ExtractedIntentSlots(
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("element_handling")] string? ElementHandling = null,
    [property: JsonPropertyName("navigation")] string? Navigation = null,
    [property: JsonPropertyName("restore")] bool? Restore = null,
    [property: JsonPropertyName("completion")] string? Completion = null);

/// <summary>
/// 从自然语言场景描述中提取结构化 <see cref="IntentSlots"/>。
/// 使用 AI 模型（典型为 DeepSeek flash）推理遍历意图，而非手工硬编码。
/// </summary>
public interface IIntentExtractor
{
    /// <summary>
    /// 从场景描述 + 事实性上下文中提取完整的 <see cref="IntentSlots"/>。
    /// AI 推理 Scope / ElementHandling / Navigation / Restore / Completion；
    /// TargetApp / Target / Depth / Entry 由调用方提供，不经 AI 推理。
    /// </summary>
    /// <param name="description">场景的自然语言描述</param>
    /// <param name="targetApp">目标应用包名（事实性）</param>
    /// <param name="target">目标项标签（事实性，可空）</param>
    /// <param name="maxDepth">最大遍历深度（事实性）</param>
    /// <param name="entryPage">入口页面身份（事实性）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>合并后的完整 IntentSlots</returns>
    Task<IntentSlots> ExtractAsync(
        string description,
        string targetApp,
        string? target,
        int? maxDepth,
        string? entryPage,
        CancellationToken ct = default);
}

/// <summary>
/// IntentExtractor — 使用 <see cref="IModelProvider"/> 从自然语言场景描述中
/// AI 推理提取 <see cref="IntentSlots"/>。Prompt 模板由
/// <see cref="PromptTemplateRegistry.ExtractIntent"/> 提供。
/// </summary>
public sealed class IntentExtractor : IIntentExtractor
{
    private readonly IModelProvider _modelProvider;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 构造 IntentExtractor。
    /// </summary>
    /// <param name="modelProvider">AI 模型 provider（典型为 DeepSeek flash），
    /// 必须支持 CompleteTextAsync + Schema（response_format json_object）。</param>
    public IntentExtractor(IModelProvider modelProvider)
    {
        _modelProvider = modelProvider ?? throw new ArgumentNullException(nameof(modelProvider));
    }

    /// <inheritdoc />
    public async Task<IntentSlots> ExtractAsync(
        string description,
        string targetApp,
        string? target,
        int? maxDepth,
        string? entryPage,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetApp);

        var template = PromptTemplateRegistry.ExtractIntent;
        var resolved = template.Resolve(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["description"] = description,
                ["target_app"] = targetApp,
                ["target"] = target ?? "(none - exhaustive traversal)",
                ["depth"] = maxDepth?.ToString() ?? "unbounded",
                ["entry"] = entryPage ?? "app root",
            });

        var request = new ModelRequest(
            resolved.User,
            SystemPrompt: resolved.System,
            MaxTokens: 4096);  // default ModelRequest.MaxTokens
        // NOTE: Schema（response_format json_object）不设置。
        // DeepSeek v4-flash 在某些部署下不完全支持 OpenAI-compatible json_object mode；
        // 改为在 prompt 中强制 JSON-only 输出（"Respond ONLY with a single JSON object"）。
        var response = await _modelProvider.CompleteTextAsync(request, ct);

        if (!response.Success)
        {
            throw new InvalidOperationException(
                $"Intent extraction failed: {response.ErrorMessage ?? "unknown error"}");
        }

        if (string.IsNullOrWhiteSpace(response.Content))
        {
            var rawDiag = response.Diagnostics is not null
                && response.Diagnostics.TryGetValue("raw_response", out var raw)
                && raw is string rawStr
                ? $" Raw response: {rawStr}"
                : "";
            throw new InvalidOperationException(
                $"Intent extraction returned empty content. " +
                $"Model={response.Model}, Tokens in={response.InputTokens} out={response.OutputTokens}, " +
                $"Latency={response.LatencyMs:F0}ms.{rawDiag}");
        }

        var extracted = DeserializeResponse(response.Content);
        return MergeWithFactuals(extracted, targetApp, target, maxDepth, entryPage);
    }

    /// <summary>去除 markdown code fence（```json ... ```），返回纯 JSON 文本。</summary>
    private static string StripCodeFences(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var end = trimmed.IndexOf("```", 3, StringComparison.Ordinal);
            if (end > 3)
            {
                // Skip the opening fence line (e.g. "```json") and the closing "```"
                var contentStart = trimmed.IndexOf('\n', 3);
                if (contentStart > 0)
                    return trimmed[(contentStart + 1)..end].Trim();
            }
        }
        return trimmed;
    }

    private static ExtractedIntentSlots DeserializeResponse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException(
                "Intent extraction returned empty response.");
        }

        // Strip markdown code fences (```json ... ```) that some models wrap around JSON output.
        var cleaned = StripCodeFences(json);

        ExtractedIntentSlots? result;
        try
        {
            result = JsonSerializer.Deserialize<ExtractedIntentSlots>(cleaned, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Intent extraction returned invalid JSON: {ex.Message}. Raw: {json[..Math.Min(json.Length, 200)]}",
                ex);
        }

        if (result is null)
        {
            throw new InvalidOperationException(
                $"Intent extraction returned null after deserialization. Raw: {json[..Math.Min(json.Length, 200)]}");
        }

        if (string.IsNullOrWhiteSpace(result.Scope))
        {
            throw new InvalidOperationException(
                $"Intent extraction returned missing required field 'scope'. Raw: {json[..Math.Min(json.Length, 200)]}");
        }

        return result;
    }

    private static IntentSlots MergeWithFactuals(
        ExtractedIntentSlots extracted,
        string targetApp,
        string? target,
        int? maxDepth,
        string? entryPage)
    {
        return new IntentSlots(
            TargetApp: targetApp,
            Scope: ValidateScope(extracted.Scope),
            Target: target,
            Depth: maxDepth,
            ElementHandling: ValidateElementHandling(extracted.ElementHandling),
            Navigation: extracted.Navigation,
            Restore: extracted.Restore,
            Completion: ValidateCompletion(extracted.Completion),
            Entry: entryPage);
    }

    private static string ValidateScope(string scope) =>
        scope switch
        {
            "full" => "full",
            "target_only" => "target_only",
            _ => throw new InvalidOperationException(
                $"AI extracted unknown scope '{scope}'. Expected 'full' or 'target_only'."),
        };

    private static string? ValidateElementHandling(string? handling) =>
        handling switch
        {
            null => null,
            "full_interaction" => "full_interaction",
            "menu_only" => "menu_only",
            "safe_mode" => "safe_mode",
            "read_only" => "read_only",
            _ => throw new InvalidOperationException(
                $"AI extracted unknown element_handling '{handling}'. " +
                "Expected one of: full_interaction, menu_only, safe_mode, read_only, or null."),
        };

    private static string? ValidateCompletion(string? completion) =>
        completion switch
        {
            null => null,
            "max_steps" => "max_steps",
            "timeout" => "timeout",
            _ => throw new InvalidOperationException(
                $"AI extracted unknown completion '{completion}'. " +
                "Expected one of: max_steps, timeout, or null."),
        };
}
