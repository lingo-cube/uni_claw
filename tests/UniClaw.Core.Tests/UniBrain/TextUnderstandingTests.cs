using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Observability;
using UniClaw.Core.Simulation;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// TextUnderstanding 单元测试 — task 5.2: 4 场景对照 spec。
/// 用真实 PromptLibrary + ModelRouter(MockModelProvider) + InMemoryTraceRecorder 组装，
/// 验证 7 步链路：happy path / 模板缺失 fail-fast / 模型失败 fail-fast / provider-agnostic。
/// 对齐 OpenSpec change unibrain-modelprovider-vertical-slice。
/// </summary>
public class TextUnderstandingTests
{
    // ── 组装辅助 ────────────────────────────────────────────

    private static ImmutableDictionary<string, string> Routing(
        params (string capability, string providerId)[] entries)
        => entries.ToImmutableDictionary(e => e.capability, e => e.providerId);

    private static ImmutableDictionary<string, IModelProvider> Providers(
        params (string id, IModelProvider provider)[] entries)
        => entries.ToImmutableDictionary(e => e.id, e => e.provider);

    /// <summary>parse_instruction 模板（变量 text/context，对齐生产预期）。</summary>
    private static PromptLibrary MakePromptLibrary() =>
        new(new PromptTemplate(
            ModelCapabilities.ParseInstruction,
            "你是助手",
            "解析：{text} 上下文：{context}",
            ImmutableArray.Create("text", "context")));

    /// <summary>构造含单条 parse_instruction 预设的 fixture。</summary>
    private static MockModelFixture FixtureFor(string content, bool success = true, string? error = null) =>
        new(ImmutableDictionary.CreateRange(new[]
        {
            KeyValuePair.Create(ModelCapabilities.ParseInstruction, new MockModelEntry(content, Success: success, ErrorMessage: error)),
        }));

    private static ModelRouter Router(IModelProvider provider, string providerId = "mock",
        ImmutableDictionary<string, string>? routing = null) =>
        new(
            routing ?? Routing((ModelCapabilities.ParseInstruction, providerId)),
            Providers((providerId, provider)),
            new InMemoryTraceRecorder(new InMemoryTraceStorage()),
            providerId);

    // ── 1. Happy path ───────────────────────────────────────

    [Fact(DisplayName = "Happy path: 合法 JSON 响应 → TextUnderstandingResult 字段正确，经 router 路由")]
    public async Task UnderstandTextAsync_ValidResponse_ReturnsParsedResult()
    {
        var content = """{"category":"open_settings","confidence":0.9,"entities":["设置"],"summary":"打开设置"}""";
        var provider = new MockModelProvider(FixtureFor(content), "mock");
        var tu = new TextUnderstanding(Router(provider), MakePromptLibrary());

        var result = await tu.UnderstandTextAsync(new TextUnderstandingRequest("打开设置", "主页"));

        Assert.Equal("open_settings", result.Category);
        Assert.Equal(0.9, result.Confidence);
        Assert.Contains("设置", result.Entities);
        Assert.Equal("打开设置", result.Summary);
    }

    // ── 2. 模板缺失 fail-fast ───────────────────────────────

    [Fact(DisplayName = "模板缺失 → DomainValidationException fail-fast，未发起模型调用")]
    public async Task UnderstandTextAsync_MissingTemplate_ThrowsWithoutModelCall()
    {
        // Canary provider：若被触达则抛 InvalidOperationException（区别于预期异常），证明未发起模型调用
        var provider = new ThrowIfCalledProvider();
        // 空 PromptLibrary：无 parse_instruction 模板
        var tu = new TextUnderstanding(Router(provider), new PromptLibrary());

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => tu.UnderstandTextAsync(new TextUnderstandingRequest("打开设置")));

        Assert.Contains("template", ex.Message);
        Assert.False(provider.WasCalled, "模板缺失时不应发起模型调用");
    }

    // ── 3. 模型失败 fail-fast ───────────────────────────────

    [Fact(DisplayName = "模型返回 Success=false → DomainValidationException 含 ErrorMessage")]
    public async Task UnderstandTextAsync_ModelFailure_ThrowsWithError()
    {
        var provider = new MockModelProvider(
            FixtureFor("ignored", success: false, error: "boom"), "mock");
        var tu = new TextUnderstanding(Router(provider), MakePromptLibrary());

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => tu.UnderstandTextAsync(new TextUnderstandingRequest("打开设置")));

        Assert.Contains("boom", ex.Message);
    }

    // ── 4. provider-agnostic ────────────────────────────────

    [Fact(DisplayName = "provider-agnostic: 经 router.Resolve 路由到指定 provider，不绑定具体类型")]
    public async Task UnderstandTextAsync_RoutesViaRouter_ToConfiguredProvider()
    {
        // 两个 provider，parse_instruction 精确路由到 alpha；default 是 beta。
        // 若 TextUnderstanding 正确经 router.Resolve 路由，结果应来自 alpha（而非 default beta）。
        var alpha = new MockModelProvider(
            FixtureFor("""{"category":"from_alpha","confidence":0.5,"entities":[],"summary":""}"""),
            "alpha");
        var beta = new MockModelProvider(
            FixtureFor("""{"category":"from_beta","confidence":0.1,"entities":[],"summary":""}"""),
            "beta");

        var router = new ModelRouter(
            Routing((ModelCapabilities.ParseInstruction, "alpha")),
            Providers(("alpha", alpha), ("beta", beta)),
            new InMemoryTraceRecorder(new InMemoryTraceStorage()),
            "beta");  // default=beta，但 parse_instruction 路由到 alpha

        var tu = new TextUnderstanding(router, MakePromptLibrary());

        var result = await tu.UnderstandTextAsync(new TextUnderstandingRequest("anything"));

        Assert.Equal("from_alpha", result.Category);
    }

    // ── Canary: 不应被调用的 provider ───────────────────────

    /// <summary>
    /// 若 CompleteTextAsync 被触达，置 WasCalled=true 并抛 InvalidOperationException。
    /// 用于验证"模板缺失时不发起模型调用"：被调则会以非预期异常使测试失败。
    /// </summary>
    private sealed class ThrowIfCalledProvider : IModelProvider
    {
        public bool WasCalled { get; private set; }
        public string ProviderId => "throw-if-called";

        public Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)
        {
            WasCalled = true;
            throw new InvalidOperationException(
                "provider should not be called when parse_instruction template is missing");
        }

        public Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
