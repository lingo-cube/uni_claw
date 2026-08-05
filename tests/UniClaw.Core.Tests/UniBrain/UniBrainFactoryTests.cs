using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Core.Domain;
using UniClaw.Core.Observability;
using UniClaw.Core.Simulation;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// UniBrainFactory 单元测试 — M2b (resolves C2 assembly seam)。
/// 验证配置驱动的 AI 注入点：UniBrainConfig + pre-built providers dict + 支撑服务 → UniBrainService。
///
/// 覆盖 scenario:
/// 1. 默认 provider → 装配 facade，三个子接口非空。
/// 2. CapabilityRouting 子接口语义名 → providerId 翻译生效（经 ModelCapabilities call-name）。
/// 3. MockModelFixture-backed MockModelProvider → facade 装配成功（vision-replay 依赖 M2a）。
/// 4. 未知路由目标 → DomainValidationException（委派 ModelRouter ctor）。
/// 5. UniBrainConfig 无凭证字段（invariant 守卫）。
/// </summary>
public sealed class UniBrainFactoryTests
{
    // ── 共享支撑构造 ──────────────────────────────────────────────

    /// <summary>含全部 3 个模板的 PromptLibrary（PageAnalyzer 需要 analyze_visual）。</summary>
    private static PromptLibrary MakePromptLibrary() =>
        new(PromptTemplateRegistry.AnalyzeVisual);

    /// <summary>InMemoryTraceRecorder + storage（观测闭环装配）。</summary>
    private static InMemoryTraceRecorder MakeRecorder()
        => new(new InMemoryTraceStorage());

    /// <summary>固定返回 bytes 的 IScreenCapture fake。</summary>
    private sealed class FakeScreenCapture : IScreenCapture
    {
        private readonly byte[] _bytes;
        public FakeScreenCapture(byte[] bytes) => _bytes = bytes;
        public Task<byte[]> CaptureAsync(CancellationToken ct = default)
            => Task.FromResult(_bytes);
        public Task<RawScreenBuffer> CaptureRawAsync(CancellationToken ct = default)
            => throw new NotSupportedException("Raw capture not supported in test fake");
    }

    /// <summary>
    /// Fake IModelProvider — CompleteVisionAsync 返回固定 JSON（PageAnalysis happy path），
    /// CompleteTextAsync 返回固定 JSON。用于验证 facade 装配 + replay link shape。
    /// </summary>
    private sealed class FakeVisionProvider : IModelProvider
    {
        public string ProviderId { get; }
        private readonly string _visionContent;
        private readonly string _textContent;

        public FakeVisionProvider(string providerId, string visionContent, string textContent = "{}")
        {
            ProviderId = providerId;
            _visionContent = visionContent;
            _textContent = textContent;
        }

        public Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
            => Task.FromResult(new ModelResponse(_visionContent, ProviderId, "vision", 50, 200, 15.0));

        public Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)
            => Task.FromResult(new ModelResponse(_textContent, ProviderId, "text", 10, 20, 5.0));

        public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    /// <summary>happy path PageAnalysis JSON（同 PageAnalyzerTests.HappyPathJson）。</summary>
    private static string HappyPathJson() =>
        "{\"level1_dir\":\"left\","
        + "\"level1_menus\":[{\"name\":\"Settings\",\"coordinate\":{\"x\":0.1,\"y\":0.5},\"active\":true}],"
        + "\"level2_dir\":\"top\",\"level2_menus\":[],"
        + "\"current_path\":[\"Settings\"],"
        + "\"items\":["
        + "{\"name\":\"WiFi\",\"type\":\"menu_item\",\"coordinate\":{\"x\":0.5,\"y\":0.2},\"parent\":null}"
        + "],\"is_popup\":false,\"popup_info\":null,\"close_button\":null,\"back_button\":null,"
        + "\"has_scroll\":false,\"is_end_of_list\":false}";

    // ── 1. 默认 provider 装配 ─────────────────────────────────────

    [Fact(DisplayName = "Create: DefaultProvider=mock + 单 mock provider → 非空 facade，三子接口非空")]
    public void Create_WithDefaultProvider_BuildsFacade()
    {
        var config = new UniBrainConfig(DefaultProvider: "mock");
        var providers = new Dictionary<string, IModelProvider>
        {
            ["mock"] = new FakeVisionProvider("mock", HappyPathJson()),
        };

        var facade = UniBrainFactory.Create(
            config,
            providers,
            MakePromptLibrary(),
            new FakeScreenCapture(new byte[] { 1, 2, 3 }),
            MakeRecorder());

        Assert.NotNull(facade);
        Assert.NotNull(facade.PageAnalyzer);
        Assert.NotNull(facade.Advisor);
        Assert.NotNull(facade.Text);
    }

    // ── 2. CapabilityRouting 子接口语义名翻译 ────────────────────

    [Fact(DisplayName = "Create: CapabilityRouting 子接口语义名 → providerId 翻译生效，facade 非空（无抛）")]
    public void Create_HonorsCapabilityRouting()
    {
        // 子接口语义名 → providerId；工厂内部翻译为 ModelCapabilities call-name
        var routing = ImmutableDictionary.CreateRange<string, string>(new[]
        {
            KeyValuePair.Create("page_analysis", "anthropic"),
            KeyValuePair.Create("traversal_advisor", "deepseek"),
            KeyValuePair.Create("text_understanding", "deepseek"),
        });
        var config = new UniBrainConfig(DefaultProvider: "mock", CapabilityRouting: routing);

        var providers = new Dictionary<string, IModelProvider>
        {
            ["mock"] = new FakeVisionProvider("mock", HappyPathJson()),
            ["anthropic"] = new FakeVisionProvider("anthropic", HappyPathJson()),
            ["deepseek"] = new FakeVisionProvider("deepseek", HappyPathJson()),
        };

        // 不抛 → 说明 ModelRouter ctor 接受了翻译后的 call-name routing（每个 providerId 都存在）
        var facade = UniBrainFactory.Create(
            config,
            providers,
            MakePromptLibrary(),
            new FakeScreenCapture(new byte[] { 1, 2, 3 }),
            MakeRecorder());

        Assert.NotNull(facade);
        Assert.NotNull(facade.PageAnalyzer);
        Assert.NotNull(facade.Advisor);
        Assert.NotNull(facade.Text);
    }

    // ── 3. MockModelFixture-backed provider → 装配 facade ────────

    [Fact(DisplayName = "Create: MockModelFixture-backed MockModelProvider → facade 装配成功（vision-replay 依赖 M2a）")]
    public void Create_MockDefault_ProducesReplayFacade()
    {
        // MockModelFixture 预设 analyze_visual（vision replay 由 M2a 实现）。
        // 当前 MockModelProvider.CompleteVisionAsync 抛 NIE（M2a 未合并），
        // 故本测试仅断言 facade 装配非空（配置选择 replay link shape，非 Host 手工 new）。
        // M2a 合并后可扩展为调用 AnalyzeCurrentPageAsync 返回非 null PageAnalysis。
        var fixture = new MockModelFixture(
            ImmutableDictionary.CreateRange(new[]
            {
                KeyValuePair.Create("analyze_visual", new MockModelEntry(HappyPathJson(), 50, 200, 15.0)),
            }));
        var mockProvider = new MockModelProvider(fixture, "mock");

        var config = new UniBrainConfig(DefaultProvider: "mock");
        var providers = new Dictionary<string, IModelProvider> { ["mock"] = mockProvider };

        var facade = UniBrainFactory.Create(
            config,
            providers,
            MakePromptLibrary(),
            new FakeScreenCapture(new byte[] { 1, 2, 3 }),
            MakeRecorder());

        Assert.NotNull(facade);
        Assert.NotNull(facade.PageAnalyzer);
        // 装配链路经 ModelRouter.Resolve(AnalyzeVisual) → ObservingModelProvider(MockModelProvider)
        // facade.PageAnalyzer.AnalyzeCurrentPageAsync 会触达 mock（M2a 实现 vision 后返回 PageAnalysis）。
        // vision-replay 依: 见 MockModelProvider.CompleteVisionAsync（M2a 拥有）。
    }

    // ── 4. 未知路由目标 → DomainValidationException ──────────────

    [Fact(DisplayName = "Create: CapabilityRouting 引用未知 providerId → DomainValidationException（委派 ModelRouter ctor）")]
    public void Create_RejectsUnknownRoutingTarget()
    {
        var routing = ImmutableDictionary.CreateRange<string, string>(new[]
        {
            KeyValuePair.Create("page_analysis", "nonexistent"),
        });
        var config = new UniBrainConfig(DefaultProvider: "mock", CapabilityRouting: routing);

        var providers = new Dictionary<string, IModelProvider>
        {
            ["mock"] = new FakeVisionProvider("mock", HappyPathJson()),
        };

        // 翻译后 page_analysis → AnalyzeVisual → "nonexistent"；ModelRouter ctor 校验 fail-fast
        Assert.Throws<DomainValidationException>(() => UniBrainFactory.Create(
            config,
            providers,
            MakePromptLibrary(),
            new FakeScreenCapture(new byte[] { 1, 2, 3 }),
            MakeRecorder()));
    }

    // ── 5. UniBrainConfig 无凭证字段（invariant 守卫）───────────

    [Fact(DisplayName = "UniBrainConfig 无凭证字段 (ApiKey/Secret/Token/Password/Credential) — invariant 守卫")]
    public void UniBrainCredentials_NotInConfig()
    {
        // 反射枚举 UniBrainConfig 所有 public property，断言无凭证字段名。
        var credentialFieldNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "ApiKey", "Secret", "Token", "Password", "Credential",
        };
        var props = typeof(UniBrainConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var offending = props.Where(p => credentialFieldNames.Contains(p.Name)).ToList();
        Assert.Empty(offending);
    }

    // ── 6. null guard ─────────────────────────────────────────────

    [Fact(DisplayName = "Create: config null → DomainValidationException")]
    public void Create_ConfigNull_Throws()
    {
        var ex = Assert.Throws<DomainValidationException>(() => UniBrainFactory.Create(
            null!,
            new Dictionary<string, IModelProvider> { ["mock"] = new FakeVisionProvider("mock", "{}") },
            MakePromptLibrary(),
            new FakeScreenCapture(new byte[] { 1 }),
            MakeRecorder()));
        Assert.Equal("config", ex.FieldName);
    }

    [Fact(DisplayName = "Create: providers null/empty → DomainValidationException")]
    public void Create_ProvidersEmpty_Throws()
    {
        var config = new UniBrainConfig(DefaultProvider: "mock");
        Assert.Throws<DomainValidationException>(() => UniBrainFactory.Create(
            config,
            new Dictionary<string, IModelProvider>(),
            MakePromptLibrary(),
            new FakeScreenCapture(new byte[] { 1 }),
            MakeRecorder()));
    }
}