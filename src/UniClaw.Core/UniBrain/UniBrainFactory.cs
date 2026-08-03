using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Observability;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// UniBrainCredentials — provider 凭证载体 (M2b)。
/// <b>独立于 UniBrainConfig</b>：UniBrainConfig 只声明路由拓扑（哪个子接口 → 哪个 providerId），
/// 不含任何凭证字段（ApiKey/Secret/Token/Password/Credential）。凭证由本类型承载，由 Host/组合根
/// 读取并构造具体 IModelProvider 后，以 pre-built providers dict 形式注入 UniBrainFactory。
/// 本类型仅是凭证的结构化载体；UniBrainFactory 本身不消费它——不把凭证塞进 Core 的装配逻辑，
/// 避免 Core 依赖传输层细节。Host 负责 UniBrainCredentials → IModelProvider 的映射。
/// </summary>
/// <param name="Providers">providerId → ProviderCredential。可空（无凭证则 Host 自行构造 provider）。</param>
public sealed record class UniBrainCredentials(
    ImmutableDictionary<string, ProviderCredential>? Providers = null);

/// <summary>
/// ProviderCredential — 单个 provider 的凭证三元组 (ApiKey / Model / BaseUrl)。
/// 匹配 AnthropicModelProvider / DeepSeekProviderConfig 等传输层配置的最小公共字段。
/// BaseUrl 可空（provider 默认端点）。Model 可空（provider 默认模型）。
/// </summary>
public sealed record class ProviderCredential(
    string? ApiKey = null,
    string? Model = null,
    string? BaseUrl = null);

/// <summary>
/// UniBrainFactory — 配置驱动的 AI 装配接缝 (M2b, resolves C2)。
/// 接收 <see cref="UniBrainConfig"/>（路由拓扑）+ pre-built providers dict（providerId → 已构造的裸
/// <see cref="IModelProvider"/>）+ <see cref="IPromptLibrary"/>/<see cref="IScreenCapture"/>/
/// <see cref="ITraceRecorder"/>，装配出 <see cref="UniBrainService"/>。
/// <para>
/// <b>凭证边界</b>：工厂不接收也不解析凭证（<see cref="UniBrainCredentials"/> 是独立载体，由 Host 消费）。
/// 具体 provider 的构造（含 HTTP 传输、ApiKey、BaseUrl 等 Device/传输层细节）由 Host 完成，工厂只见
/// <see cref="IModelProvider"/> 抽象。这让 Core 与 Device/传输层解耦——工厂不引用任何具体 provider 类型。
/// </para>
/// <para>
/// <b>路由键翻译</b>：<see cref="UniBrainConfig.CapabilityRouting"/> 用子接口语义名
/// (<c>"page_analysis"</c>/<c>"traversal_advisor"</c>/<c>"text_understanding"</c>)，而
/// <see cref="ModelRouter"/>/<see cref="ModelCapabilities"/> 用 call-name。工厂在内部翻译：
/// <list type="bullet">
/// <item><c>page_analysis</c> → <see cref="ModelCapabilities.AnalyzeVisual"/></item>
/// <item><c>traversal_advisor</c> → <see cref="ModelCapabilities.DecideNextAction"/></item>
/// <item><c>text_understanding</c> → <see cref="ModelCapabilities.ParseInstruction"/></item>
/// </list>
/// 缺失子接口键 → 回落到 <see cref="UniBrainConfig.DefaultProvider"/>。
/// </para>
/// </summary>
public static class UniBrainFactory
{
    /// <summary>子接口语义名 → 该子接口主要 ModelCapabilities call-name 的映射。</summary>
    private static readonly IReadOnlyDictionary<string, string> SubInterfaceToCapability =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["page_analysis"] = ModelCapabilities.AnalyzeVisual,
            ["traversal_advisor"] = ModelCapabilities.DecideNextAction,
            ["text_understanding"] = ModelCapabilities.ParseInstruction,
        };

    /// <summary>
    /// 装配 <see cref="UniBrainService"/>。
    /// </summary>
    /// <param name="config">路由拓扑（子接口语义名 → providerId）+ DefaultProvider。凭证-free。</param>
    /// <param name="providers">providerId → 已构造的裸 <see cref="IModelProvider"/>（Host 负责，含凭证解析）。
    /// 不能为 null/空。</param>
    /// <param name="promptLibrary">prompt 模库（按 capability 检索）。</param>
    /// <param name="screenCapture">屏幕截图捕获抽象（PageAnalyzer 视觉输入接缝）。</param>
    /// <param name="recorder">trace 记录器（装配期经 ModelRouter 套 ObservingModelProvider）。</param>
    /// <param name="traceContext">引擎上下文 provider（trace-parent-linkage M1；null → ai.call 保留孤儿根）。
    /// 走与 recorder 相同的装配路径注入 PageAnalyzer。</param>
    /// <returns>组装好的 <see cref="UniBrainService"/>（IUniBrain facade）。</returns>
    /// <exception cref="DomainValidationException">路由引用未知 providerId（经 ModelRouter ctor 委派），
    /// 或装配期 Resolve 未命中且 default 不存在。</exception>
    public static UniBrainService Create(
        UniBrainConfig config,
        IReadOnlyDictionary<string, IModelProvider> providers,
        IPromptLibrary promptLibrary,
        IScreenCapture screenCapture,
        ITraceRecorder recorder,
        ITraceContextProvider? traceContext = null)
    {
        if (config is null)
            throw new DomainValidationException(nameof(config), config, "UniBrainConfig must not be null.");
        if (providers is null || providers.Count == 0)
            throw new DomainValidationException(nameof(providers), providers, "providers must not be null or empty.");
        if (promptLibrary is null)
            throw new DomainValidationException(nameof(promptLibrary), promptLibrary);
        if (screenCapture is null)
            throw new DomainValidationException(nameof(screenCapture), screenCapture);
        if (recorder is null)
            throw new DomainValidationException(nameof(recorder), recorder);

        // 1. 翻译子接口语义名 → ModelCapabilities call-name；缺失键回落 DefaultProvider。
        var routingBuilder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var (subInterface, callName) in SubInterfaceToCapability)
        {
            var providerId = (config.CapabilityRouting is not null
                && config.CapabilityRouting.TryGetValue(subInterface, out var routed) && !string.IsNullOrWhiteSpace(routed))
                ? routed
                : config.DefaultProvider;
            routingBuilder[callName] = providerId;
        }
        var capabilityRouting = routingBuilder.ToImmutable();

        // 2. 构造 ModelRouter（ctor 校验每个 routing value 引用的 providerId 必须存在于 providers）。
        var providerDict = providers.ToImmutableDictionary(StringComparer.Ordinal);
        var router = new ModelRouter(capabilityRouting, providerDict, recorder, config.DefaultProvider);

        // 3. 装配期 Resolve 三个已观测 provider（经 ObservingModelProvider 包装，调用必然产生 AICallRecord）。
        var analyzeVisualProvider = router.Resolve(ModelCapabilities.AnalyzeVisual);
        var decideNextActionProvider = router.Resolve(ModelCapabilities.DecideNextAction);
        var parseInstructionProvider = router.Resolve(ModelCapabilities.ParseInstruction);

        // 4. 构造三个子接口实现（D-8: ctor 注入已路由/已观测 provider，方法体内无 router.Resolve）。
        //    trace-parent-linkage M1: traceContext（ITraceContextProvider 视角）随 recorder 注入
        //    PageAnalyzer，ai.call 父链挂到当前 engine.step span；null → 保留孤儿。
        var pageAnalyzer = new PageAnalyzer(analyzeVisualProvider, promptLibrary, screenCapture, recorder, traceContext);
        var advisor = new TraversalAdvisor(decideNextActionProvider, promptLibrary);
        var text = new TextUnderstanding(parseInstructionProvider, promptLibrary);

        // 5. 返回纯组合 facade。
        return new UniBrainService(pageAnalyzer, advisor, text);
    }
}