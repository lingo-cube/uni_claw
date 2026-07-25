using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// TraversalAdvisor — ITraversalAdvisor 真实实现（本 slice 仅覆盖 decide_next_action capability）。
/// 组装 IPromptLibrary + IModelRouter：按 decide_next_action capability 取模板 → 序列化 PageAnalysis
/// 进 prompt → 经 router 路由到 provider（已套观测 decorator）→ 解析 JSON 响应为 ContextDecisionResult。
/// provider-agnostic：仅依赖 IModelRouter/IPromptLibrary 抽象，不引用任何具体 provider 类型
/// （DeepSeek/Claude/Mock 等传输层实现），上层换 provider 无需改本类。
/// 对齐 OpenSpec change unibrain-traversaladvisor-vertical-slice。
/// </summary>
public sealed class TraversalAdvisor : ITraversalAdvisor
{
    private readonly IModelRouter _router;
    private readonly IPromptLibrary _promptLibrary;

    /// <summary>
    /// 构造 TraversalAdvisor。router / promptLibrary 为 null → DomainValidationException fail-fast。
    /// </summary>
    /// <param name="router">capability → 已观测 IModelProvider 的解析器</param>
    /// <param name="promptLibrary">prompt 模板库（按 capability 检索）</param>
    public TraversalAdvisor(IModelRouter router, IPromptLibrary promptLibrary)
    {
        _router = router ?? throw new DomainValidationException(nameof(router), router);
        _promptLibrary = promptLibrary ?? throw new DomainValidationException(nameof(promptLibrary), promptLibrary);
    }

    /// <inheritdoc />
    public async Task<ContextDecisionResult> DecideNextActionAsync(
        string goal,
        PageAnalysis pageAnalysis,
        string? currentNodeId = null,
        int? depth = null,
        CancellationToken ct = default)
    {
        // 1. 取 decide_next_action 模板；缺失 → fail-fast（不发起模型调用）
        var template = _promptLibrary.GetTemplate(ModelCapabilities.DecideNextAction);
        if (template is null)
            throw new DomainValidationException(
                nameof(ModelCapabilities.DecideNextAction),
                null,
                "decide_next_action prompt template not configured.");

        // 2. 序列化 PageAnalysis 进 prompt + 解析模板变量
        var resolved = template.Resolve(new Dictionary<string, string>
        {
            ["goal"] = goal,
            ["page_analysis"] = JsonSerializer.Serialize(pageAnalysis, DomainJsonOptions.Default),
            ["current_node_id"] = currentNodeId ?? "",
            ["depth"] = depth?.ToString() ?? "",
        });

        // 3. 构造 ModelRequest：结构化输出 schema + 语义标签 capability + 收紧 MaxTokens
        var modelRequest = new ModelRequest(
            resolved.User,
            resolved.System,
            Schemas.DecideNextAction,
            MaxTokens: 1024,
            Capability: ModelCapabilities.DecideNextAction);

        // 4. 经 router 解析到（已被观测 decorator 包裹的）provider
        var provider = _router.Resolve(modelRequest.Capability!);

        // 5. 调用模型
        var resp = await provider.CompleteTextAsync(modelRequest, ct);

        // 6. 模型失败 → fail-fast
        if (!resp.Success)
            throw new DomainValidationException(
                nameof(resp.Content),
                resp.Content,
                $"decide_next_action model call failed: {resp.ErrorMessage}");

        // 7. 解析 JSON 响应为 ContextDecisionResult
        DecideNextActionDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<DecideNextActionDto>(resp.Content, DomainJsonOptions.Default)
                ?? throw new JsonException("deserialized to null");
        }
        catch (JsonException ex)
        {
            throw new DomainValidationException(
                nameof(resp.Content),
                resp.Content,
                $"decide_next_action response was not valid JSON: {ex.Message}");
        }

        // result 字段大小写不敏感 parse 为锁定 enum；未识别 → fail-fast（暴露模型漂移）
        if (!Enum.TryParse<DecisionResult>(dto.Result, ignoreCase: true, out var result))
            throw new DomainValidationException(
                nameof(resp.Content),
                resp.Content,
                $"decide_next_action response had unrecognized result enum: {dto.Result}");

        return new ContextDecisionResult(
            result,
            dto.Action,
            dto.Target,
            MapParams(dto.Params),
            dto.Reasoning,
            dto.Confidence,
            dto.SafetyVerified);
    }

    /// <inheritdoc />
    /// <remarks>本 slice 仅覆盖 decide_next_action；容器推断留待后续 slice。</remarks>
    public Task<ContainerInference> InferContainerTypeAsync(
        PageAnalysis pageAnalysis, string? currentNodeId = null, CancellationToken ct = default)
        => throw new NotImplementedException(
            "TraversalAdvisor slice covers decide_next_action only; InferContainerTypeAsync pending future slice.");

    /// <inheritdoc />
    /// <remarks>本 slice 仅覆盖 decide_next_action；异常恢复规划留待后续 slice。</remarks>
    public Task<ContextDecisionResult> HandleExceptionAsync(
        Exception exception, PageAnalysis pageAnalysis, string? currentNodeId = null, CancellationToken ct = default)
        => throw new NotImplementedException(
            "TraversalAdvisor slice covers decide_next_action only; HandleExceptionAsync pending future slice.");

    /// <inheritdoc />
    /// <remarks>本 slice 仅覆盖 decide_next_action；安全筛选留待后续 slice。</remarks>
    public Task<SafetyScreeningResult> ScreenSafetyAsync(
        PageAnalysis pageAnalysis, string instruction, string? pageType = null, CancellationToken ct = default)
        => throw new NotImplementedException(
            "TraversalAdvisor slice covers decide_next_action only; ScreenSafetyAsync pending future slice.");

    /// <summary>
    /// 将模型返回的 params（扁平 JSON object）按 ValueKind 映射为 CLR 原始值，规避 JsonElement 的
    /// buffer 生命周期问题。嵌套 object/array 不支持（本 slice Non-Goal，未来扩展）。
    /// </summary>
    private static ImmutableDictionary<string, object>? MapParams(Dictionary<string, JsonElement>? raw)
    {
        if (raw is null) return null;

        var builder = ImmutableDictionary.CreateBuilder<string, object>();
        foreach (var (key, el) in raw)
        {
            builder[key] = el.ValueKind switch
            {
                JsonValueKind.String => (object)(el.GetString() ?? ""),
                JsonValueKind.Number => el.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => el.GetRawText(),
            };
        }
        return builder.ToImmutable();
    }

    /// <summary>decide_next_action 响应内部 DTO（仅用于 JSON 反序列化，不暴露）。</summary>
    private sealed class DecideNextActionDto
    {
        public string Result { get; init; } = "";
        public string? Action { get; init; }
        public string? Target { get; init; }
        public Dictionary<string, JsonElement>? Params { get; init; }
        public string? Reasoning { get; init; }
        public double Confidence { get; init; }
        public bool SafetyVerified { get; init; } = true;
    }
}
