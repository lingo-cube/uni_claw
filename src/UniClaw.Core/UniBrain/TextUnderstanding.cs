using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Core.Domain;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// TextUnderstanding — ITextUnderstanding 真实实现 (task 5.2)。
/// 组装 IPromptLibrary + IModelRouter：按 parse_instruction capability 取模板 → 解析变量 →
/// 经 router 路由到 provider（已套观测 decorator）→ 解析 JSON 响应为 TextUnderstandingResult。
/// provider-agnostic：仅依赖 IModelRouter/IPromptLibrary 抽象，不引用任何具体 provider 类型
/// （DeepSeek/Claude/Mock 等传输层实现），上层换 provider 无需改本类。
/// 对齐 OpenSpec change unibrain-modelprovider-vertical-slice。
/// </summary>
public sealed class TextUnderstanding : ITextUnderstanding
{
    private readonly IModelRouter _router;
    private readonly IPromptLibrary _promptLibrary;

    /// <summary>
    /// 构造 TextUnderstanding。router / promptLibrary 为 null → DomainValidationException fail-fast。
    /// </summary>
    /// <param name="router">capability → 已观测 IModelProvider 的解析器</param>
    /// <param name="promptLibrary">prompt 模板库（按 capability 检索）</param>
    public TextUnderstanding(IModelRouter router, IPromptLibrary promptLibrary)
    {
        _router = router ?? throw new DomainValidationException(nameof(router), router);
        _promptLibrary = promptLibrary ?? throw new DomainValidationException(nameof(promptLibrary), promptLibrary);
    }

    /// <inheritdoc />
    public async Task<TextUnderstandingResult> UnderstandTextAsync(
        TextUnderstandingRequest request,
        CancellationToken ct = default)
    {
        // 1. 取 parse_instruction 模板；缺失 → fail-fast（不发起模型调用）
        var template = _promptLibrary.GetTemplate(ModelCapabilities.ParseInstruction);
        if (template is null)
            throw new DomainValidationException(
                nameof(ModelCapabilities.ParseInstruction),
                null,
                "parse_instruction prompt template not configured.");

        // 2. 解析模板变量（text / context）
        var resolved = template.Resolve(new Dictionary<string, string>
        {
            ["text"] = request.Text,
            ["context"] = request.Context ?? "",
        });

        // 3. 构造 ModelRequest：结构化输出 schema + 语义标签 capability + 收紧 MaxTokens
        var modelRequest = new ModelRequest(
            resolved.User,
            resolved.System,
            Schemas.ParseInstruction,
            MaxTokens: 1024,
            Capability: ModelCapabilities.ParseInstruction);

        // 4. 经 router 解析到（已被观测 decorator 包裹的）provider
        var provider = _router.Resolve(modelRequest.Capability!);

        // 5. 调用模型
        var resp = await provider.CompleteTextAsync(modelRequest, ct);

        // 6. 模型失败 → fail-fast
        if (!resp.Success)
            throw new DomainValidationException(
                nameof(resp.Content),
                resp.Content,
                $"parse_instruction model call failed: {resp.ErrorMessage}");

        // 7. 解析 JSON 响应为 TextUnderstandingResult
        ParseInstructionDto dto;
        try
        {
            // null 反序列化结果视为无效 JSON，转 JsonException 走统一 fail-fast 通路
            dto = JsonSerializer.Deserialize<ParseInstructionDto>(resp.Content, DomainJsonOptions.Default)
                ?? throw new JsonException("deserialized to null");
        }
        catch (JsonException ex)
        {
            throw new DomainValidationException(
                nameof(resp.Content),
                resp.Content,
                $"parse_instruction response was not valid JSON: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(dto.Category))
            throw new DomainValidationException(
                nameof(resp.Content),
                resp.Content,
                "parse_instruction response was not valid JSON.");

        // Confidence 0-1 校验由 TextUnderstandingResult 构造器自带 fail-fast
        return new TextUnderstandingResult(
            dto.Category,
            dto.Confidence,
            ImmutableArray.CreateRange(dto.Entities ?? Array.Empty<string>()),
            dto.Summary);
    }

    /// <summary>parse_instruction 响应内部 DTO（仅用于 JSON 反序列化，不暴露）。</summary>
    private sealed class ParseInstructionDto
    {
        public string Category { get; init; } = "";
        public double Confidence { get; init; }
        public string[]? Entities { get; init; }
        public string? Summary { get; init; }
    }
}
