using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.Simulation;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// TraversalAdvisor 单元测试 — task 4.1 (6 spec 场景) + task 4.2 (观测记录断言)。
/// 用真实 PromptLibrary + ModelRouter(MockModelProvider) + InMemoryTraceRecorder 组装，
/// 验证 decide_next_action 7 步链路：happy path / 模板缺失 / 模型失败 / 非法 result enum /
/// provider-agnostic 路由 / 其余 3 方法 NotImplementedException + 经 router 必然产生 AICallRecord。
/// 结构镜像 TextUnderstandingTests（同范式切片）。provider-agnostic：仅依赖 IModelRouter 抽象。
/// 对齐 OpenSpec change unibrain-traversaladvisor-vertical-slice。
/// </summary>
public sealed class TraversalAdvisorTests
{
    // ── 组装辅助 ────────────────────────────────────────────

    private static ImmutableDictionary<string, string> Routing(
        params (string capability, string providerId)[] entries)
        => entries.ToImmutableDictionary(e => e.capability, e => e.providerId);

    private static ImmutableDictionary<string, IModelProvider> Providers(
        params (string id, IModelProvider provider)[] entries)
        => entries.ToImmutableDictionary(e => e.id, e => e.provider);

    /// <summary>decide_next_action 模板（变量 goal/page_analysis/current_node_id/depth，对齐 spec）。</summary>
    private static PromptLibrary MakePromptLibrary() =>
        new(new PromptTemplate(
            ModelCapabilities.DecideNextAction,
            SystemPrompt: "You are a mobile UI traversal decision advisor. Given a goal, the current page state (JSON), the current node id, and traversal depth, decide the single next action that best advances the goal. Respond ONLY with a JSON object: result (one of Success/Unsure/GiveUp), action (verb such as tap/scroll/input/back/wait), target (element id or null), params (flat object of primitive values, optional), reasoning (one sentence), confidence (0-1), safety_verified (boolean).",
            UserPrompt: "Goal: {goal}\n\nCurrent page analysis (JSON):\n{page_analysis}\n\nCurrent node id: {current_node_id}\nTraversal depth: {depth}\n\nDecide the next action.",
            Variables: ImmutableArray.Create("goal", "page_analysis", "current_node_id", "depth")));

    /// <summary>构造含单条 decide_next_action 预设的 fixture。</summary>
    private static MockModelFixture FixtureFor(string content, bool success = true, string? error = null) =>
        new(ImmutableDictionary.CreateRange(new[]
        {
            KeyValuePair.Create(
                ModelCapabilities.DecideNextAction,
                new MockModelEntry(content, Success: success, ErrorMessage: error)),
        }));

    /// <summary>最小合法 PageAnalysis（仅必填的 Level1Dir / Level2Dir，其余默认）。</summary>
    private static PageAnalysis MinimalPage() => new(Direction.Left, Direction.Top);

    private static ModelRouter Router(
        IModelProvider provider,
        string providerId = "mock",
        ImmutableDictionary<string, string>? routing = null) =>
        new(
            routing ?? Routing((ModelCapabilities.DecideNextAction, providerId)),
            Providers((providerId, provider)),
            new InMemoryTraceRecorder(new InMemoryTraceStorage()),
            providerId);

    // ── 1. Happy path (spec scenario: Happy path decides next action) ────

    [Fact(DisplayName = "Happy path: 合法 JSON → ContextDecisionResult 7 字段正确，params.timeout 为 double")]
    public async Task DecideNextActionAsync_ValidResponse_ReturnsParsedResult()
    {
        var content = """{"result":"Success","action":"tap","target":"wifi_item","params":{"timeout":5000},"reasoning":"visible list item","confidence":0.9,"safety_verified":true}""";
        var provider = new MockModelProvider(FixtureFor(content), "mock");
        var advisor = new TraversalAdvisor(Router(provider), MakePromptLibrary());

        var result = await advisor.DecideNextActionAsync("find WiFi settings", MinimalPage(), "node_1", 3);

        Assert.Equal(DecisionResult.Success, result.Result);
        Assert.Equal("tap", result.Action);
        Assert.Equal("wifi_item", result.Target);
        // D3 ValueKind 映射：JSON number 5000 → CLR double（非 int）
        Assert.Equal(5000.0, Assert.IsType<double>(result.Params!["timeout"]));
        Assert.Equal("visible list item", result.Reasoning);
        Assert.Equal(0.9, result.Confidence, precision: 5);
        Assert.True(result.SafetyVerified);
    }

    // ── 2. 模板缺失 fail-fast (spec scenario: Missing prompt template fails fast) ────

    [Fact(DisplayName = "模板缺失 → DomainValidationException fail-fast，未发起模型调用")]
    public async Task DecideNextActionAsync_MissingTemplate_ThrowsWithoutModelCall()
    {
        // Canary provider：若被触达则抛 InvalidOperationException（区别于预期异常），证明未发起模型调用
        var provider = new ThrowIfCalledProvider();
        // 空 PromptLibrary：无 decide_next_action 模板
        var advisor = new TraversalAdvisor(Router(provider), new PromptLibrary());

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => advisor.DecideNextActionAsync("find WiFi settings", MinimalPage()));

        Assert.Contains("template", ex.Message);
        Assert.False(provider.WasCalled, "模板缺失时不应发起模型调用");
    }

    // ── 3. 模型失败 fail-fast (spec scenario: Model call failure propagates) ────

    [Fact(DisplayName = "模型返回 Success=false → DomainValidationException 含 ErrorMessage")]
    public async Task DecideNextActionAsync_ModelFailure_ThrowsWithError()
    {
        var provider = new MockModelProvider(
            FixtureFor("ignored", success: false, error: "boom"), "mock");
        var advisor = new TraversalAdvisor(Router(provider), MakePromptLibrary());

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => advisor.DecideNextActionAsync("find WiFi settings", MinimalPage()));

        Assert.Contains("boom", ex.Message);
    }

    // ── 4. 非法 result enum fail-fast (spec scenario: Invalid result enum fails fast) ────

    [Fact(DisplayName = "模型返回未识别 result enum → DomainValidationException（D4 parse 失败）")]
    public async Task DecideNextActionAsync_InvalidResultEnum_Throws()
    {
        var content = """{"result":"Maybe","confidence":0.5}""";
        var provider = new MockModelProvider(FixtureFor(content), "mock");
        var advisor = new TraversalAdvisor(Router(provider), MakePromptLibrary());

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => advisor.DecideNextActionAsync("find WiFi settings", MinimalPage()));

        Assert.Contains("Maybe", ex.Message);
    }

    // ── 5. provider-agnostic 路由 (spec scenario: Provider-agnostic routing) ────

    [Fact(DisplayName = "provider-agnostic: 经 router.Resolve 精确路由到指定 provider，不绑定具体类型")]
    public async Task DecideNextActionAsync_RoutesViaRouter_ToConfiguredProvider()
    {
        // 两个 provider，decide_next_action 精确路由到 alpha；default 是 beta。
        // 若 TraversalAdvisor 正确经 router.Resolve 路由，结果应来自 alpha（而非 default beta），
        // 从而证明 advisor 仅与 IModelRouter 对话，由 capability 路由决定命中的 provider。
        var alpha = new MockModelProvider(
            FixtureFor("""{"result":"Success","action":"tap","target":"alpha_target","confidence":0.9}"""),
            "alpha");
        var beta = new MockModelProvider(
            FixtureFor("""{"result":"Success","action":"tap","target":"beta_target","confidence":0.9}"""),
            "beta");

        var router = new ModelRouter(
            Routing((ModelCapabilities.DecideNextAction, "alpha")),
            Providers(("alpha", alpha), ("beta", beta)),
            new InMemoryTraceRecorder(new InMemoryTraceStorage()),
            "beta");  // default=beta，但 decide_next_action 路由到 alpha

        var advisor = new TraversalAdvisor(router, MakePromptLibrary());

        var result = await advisor.DecideNextActionAsync("anything", MinimalPage());

        Assert.Equal("alpha_target", result.Target);
    }

    // ── 6. 其余 3 方法 NotImplementedException (spec scenario: Other three interface methods) ────

    [Fact(DisplayName = "InferContainerTypeAsync 抛 NotImplementedException (pending future slice)")]
    public async Task InferContainerTypeAsync_ThrowsNotImplemented()
    {
        var advisor = new TraversalAdvisor(
            Router(new MockModelProvider(FixtureFor("{}"), "mock")), MakePromptLibrary());

        var ex = await Assert.ThrowsAsync<NotImplementedException>(
            () => advisor.InferContainerTypeAsync(MinimalPage()));

        Assert.Contains("pending", ex.Message);
    }

    [Fact(DisplayName = "HandleExceptionAsync 抛 NotImplementedException (pending future slice)")]
    public async Task HandleExceptionAsync_ThrowsNotImplemented()
    {
        var advisor = new TraversalAdvisor(
            Router(new MockModelProvider(FixtureFor("{}"), "mock")), MakePromptLibrary());

        var ex = await Assert.ThrowsAsync<NotImplementedException>(
            () => advisor.HandleExceptionAsync(new InvalidOperationException("x"), MinimalPage()));

        Assert.Contains("pending", ex.Message);
    }

    [Fact(DisplayName = "ScreenSafetyAsync 抛 NotImplementedException (pending future slice)")]
    public async Task ScreenSafetyAsync_ThrowsNotImplemented()
    {
        var advisor = new TraversalAdvisor(
            Router(new MockModelProvider(FixtureFor("{}"), "mock")), MakePromptLibrary());

        var ex = await Assert.ThrowsAsync<NotImplementedException>(
            () => advisor.ScreenSafetyAsync(MinimalPage(), "do not tap payment"));

        Assert.Contains("pending", ex.Message);
    }

    // ── 7. 观测记录 (task 4.2: 经 router 的调用必然产生 AICallRecord) ────

    [Fact(DisplayName = "经 router 的调用产生 AICallRecord，Capability=decide_next_action")]
    public async Task DecideNextActionAsync_RecordsAICallViaRouter()
    {
        // 共享 storage：ModelRouter 组装期套 ObservingModelProvider → 调用必写入 AICallRecord
        var storage = new InMemoryTraceStorage();
        var content = """{"result":"Success","action":"tap","target":"wifi_item","confidence":0.9}""";
        var provider = new MockModelProvider(FixtureFor(content), "mock");
        var router = new ModelRouter(
            Routing((ModelCapabilities.DecideNextAction, "mock")),
            Providers(("mock", provider)),
            new InMemoryTraceRecorder(storage),
            "mock");
        var advisor = new TraversalAdvisor(router, MakePromptLibrary());

        await advisor.DecideNextActionAsync("find WiFi settings", MinimalPage());

        var calls = storage.GetAICalls();
        Assert.NotEmpty(calls);
        Assert.Equal(ModelCapabilities.DecideNextAction, calls[0].Capability);
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
                "provider should not be called when decide_next_action template is missing");
        }

        public Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
