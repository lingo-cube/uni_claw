using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Core.Domain;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// TextUnderstanding — ITextUnderstanding 真实实现 (D-8 refactored)。
/// 组装 IPromptLibrary + IModelProvider：按 parse_instruction capability 取模板 → 解析变量 →
/// 直接调 provider（装配期已观测）→ 解析 JSON 响应为 TextUnderstandingResult。
/// provider-agnostic：仅依赖 IModelProvider/IPromptLibrary 抽象，不引用任何具体 provider 类型
/// （DeepSeek/Claude/Mock 等传输层实现），上层换 provider 无需改本类。
/// D-8: ctor 注入 IModelProvider 替代 IModelRouter（router 降为装配期工厂，方法体内无 router.Resolve）。
/// </summary>
public sealed class TextUnderstanding : ITextUnderstanding
{
    private readonly IModelProvider _modelProvider;
    private readonly IPromptLibrary _promptLibrary;

    /// <summary>
    /// 构造 TextUnderstanding。modelProvider / promptLibrary 为 null → DomainValidationException fail-fast。
    /// </summary>
    /// <param name="modelProvider">已路由/已观测的模型 provider（D-8: router 装配在 ctor 之前完成）</param>
    /// <param name="promptLibrary">prompt 模板库（按 capability 检索）</param>
    public TextUnderstanding(IModelProvider modelProvider, IPromptLibrary promptLibrary)
    {
        _modelProvider = modelProvider ?? throw new DomainValidationException(nameof(modelProvider), modelProvider);
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

        // 4. D-8: 路由装配期完成，直接调已注入的 provider（不经 router.Resolve）
        var resp = await _modelProvider.CompleteTextAsync(modelRequest, ct);

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
