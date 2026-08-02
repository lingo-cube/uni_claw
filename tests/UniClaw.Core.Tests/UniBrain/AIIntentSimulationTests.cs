using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.ClaudeProvider;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Graph.Services;
using UniClaw.Core.Simulation;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// AI 意图推理 → PlanCompiler → 仿真引擎 端到端测试。
/// 验证：从自然语言场景描述经 AI（stub 或真实 DeepSeek）提取 IntentSlots，
/// PlanCompiler 确定性编译为 TraversalPlan，再在仿真引擎中跑通。
/// 不影响已有的 SimulationBaselineTests 和 SimulationE2ETests。
/// </summary>
public class AIIntentSimulationTests
{
    // ═══════════════════════════════════════════════════════
    // ── Stub HTTP Handler（模拟 DeepSeek API） ──
    // ═══════════════════════════════════════════════════════

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;
        public string? LastRequestBody { get; private set; }

        public StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
            => _responder = responder;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return await _responder(request, cancellationToken).ConfigureAwait(false);
        }
    }

    private static StringContent JsonContent(string json)
    {
        var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    /// <summary>构造 DeepSeek chat/completions 响应 JSON。</summary>
    private static string ChatResponse(string innerJsonContent, int promptTokens = 50, int completionTokens = 30)
    {
        // 手动 JSON 构造，内层 content 的引号转义由 JsonSerializer 保证正确。
        var escaped = JsonSerializer.Serialize(innerJsonContent);
        return $"{{\"choices\":[{{\"message\":{{\"content\":{escaped}}}}}],\"usage\":{{\"prompt_tokens\":{promptTokens},\"completion_tokens\":{completionTokens}}}}}";
    }

    /// <summary>创建一个返回固定 IntentSlots JSON 的 stub IntentExtractor（经 Sensenova provider）。</summary>
    private static IIntentExtractor CreateStubExtractor(string intentJson)
    {
        var stub = new StubHttpHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(ChatResponse(intentJson)),
            }));
        var config = new OpenAiCompatibleProviderConfig("sk-test", "deepseek-v4-flash", "https://token.sensenova.cn");
        return new IntentExtractor(new OpenAiCompatibleVisionProvider(new HttpClient(stub), config));
    }

    // ═══════════════════════════════════════════════════════
    // ── Settings App Fixture（使用 menu_item 类型，对齐 PlanCompiler 词表） ──
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// 6 页 Settings 模拟应用。
    /// 关键设计决策：导航元素使用 "menu_item" 类型（非 "button"），
    /// 对齐 PlanCompiler 的 menu_container 模板 MatchCondition(Type="menu_item")。
    /// home 页有 5 个一级菜单项 + 1 个 switch（用于验证 menu_only 只遍历菜单）。
    /// </summary>
    private static StateFixture SettingsAppFixture() => new StateFixtureBuilder()
        .Page("home", p => p
            .Name("Settings")
            .Element("menu_wifi", e => e.Type("menu_item").Text("Wi-Fi").At(0.50, 0.14))
            .Element("menu_bluetooth", e => e.Type("menu_item").Text("Bluetooth").At(0.50, 0.24))
            .Element("menu_display", e => e.Type("menu_item").Text("Display").At(0.50, 0.34))
            .Element("menu_about", e => e.Type("menu_item").Text("About phone").At(0.50, 0.44))
            .Element("menu_battery", e => e.Type("menu_item").Text("Battery").At(0.50, 0.54))
            .Element("sw_dark_mode", e => e.Type("switch").Text("Dark mode").At(0.90, 0.64)))
        .Page("wifi", p => p
            .Name("Wi-Fi")
            .Readonly("wifi_status", "Wi-Fi: Connected", 0.50, 0.20)
            .BackButton("btn_back_w", 0.05, 0.05))
        .Page("bluetooth", p => p
            .Name("Bluetooth")
            .Readonly("bt_status", "Bluetooth: Off", 0.50, 0.20)
            .BackButton("btn_back_bt", 0.05, 0.05))
        .Page("display", p => p
            .Name("Display")
            .Readonly("display_brightness", "Brightness: 80%", 0.50, 0.20)
            .BackButton("btn_back_d", 0.05, 0.05))
        .Page("about", p => p
            .Name("About phone")
            .Readonly("about_model", "Model: Pixel 9", 0.50, 0.20)
            .Readonly("about_version", "Android 15", 0.50, 0.30)
            .BackButton("btn_back_a", 0.05, 0.05))
        .Page("battery", p => p
            .Name("Battery")
            .Readonly("battery_level", "Battery: 85%", 0.50, 0.20)
            .BackButton("btn_back_bat", 0.05, 0.05))
        .Transition(t => t.Id("home_to_wifi").Click("menu_wifi").From("home").To("wifi"))
        .Transition(t => t.Id("home_to_bt").Click("menu_bluetooth").From("home").To("bluetooth"))
        .Transition(t => t.Id("home_to_display").Click("menu_display").From("home").To("display"))
        .Transition(t => t.Id("home_to_about").Click("menu_about").From("home").To("about"))
        .Transition(t => t.Id("home_to_battery").Click("menu_battery").From("home").To("battery"))
        .Transition(t => t.Id("wifi_back").Click("btn_back_w").From("wifi").To("home"))
        .Transition(t => t.Id("bt_back").Click("btn_back_bt").From("bluetooth").To("home"))
        .Transition(t => t.Id("display_back").Click("btn_back_d").From("display").To("home"))
        .Transition(t => t.Id("about_back").Click("btn_back_a").From("about").To("home"))
        .Transition(t => t.Id("battery_back").Click("btn_back_bat").From("battery").To("home"))
        .Build();

    private static TraversalEngine CreateEngine(StateFixture fixture, TraversalPlan plan)
    {
        var vision = new StatefulMockVisionService(fixture);
        var brain = new UniBrainService(vision, new MockTraversalAdvisor(), new MockTextUnderstanding());
        var action = new StatefulMockActionExecutor(vision);
        return new TraversalEngine(plan, brain, new DefaultScreenStateProvider(), action);
    }

    // ═══════════════════════════════════════════════════════
    // ── 场景 1: Locate（目标定位） ──
    // ═══════════════════════════════════════════════════════

    [Fact(DisplayName = "AI Intent → locate About phone → PlanCompiler → 仿真引擎成功定位并停止")]
    public async Task LocateScenario_AIExtractedIntent_EngineFindsTargetAndStops()
    {
        // 模拟 AI 返回 locate 意图（与真实 DeepSeek flash 输出对齐）
        var stubJson = @"{""scope"":""target_only"",""element_handling"":""menu_only"",""navigation"":""bounded_settings"",""restore"":true,""completion"":null}";
        var extractor = CreateStubExtractor(stubJson);

        // Step 1: AI 从描述中提取 IntentSlots
        var slots = await extractor.ExtractAsync(
            "Locate About phone from the Android Settings home list and verify the destination page.",
            "com.android.settings",
            "About phone",
            2,
            "Settings");

        Assert.Equal("target_only", slots.Scope);
        Assert.Equal("About phone", slots.Target);
        Assert.Equal("menu_only", slots.ElementHandling);

        // Step 2: PlanCompiler 确定性编译为 TraversalPlan
        var plan = new PlanCompiler().Compile(slots);
        // ScenarioPlanCompiler 对齐：locate 模式覆盖 ActionOnFound=ExecuteThenStop
        plan = plan with
        {
            CompletionPolicy = plan.CompletionPolicy! with
            {
                ActionOnFound = TargetFoundAction.ExecuteThenStop,
                TargetAliases = ["About device", "About emulated device", "Device information"],
            },
        };
        Assert.NotNull(plan.RootNode);
        Assert.Equal(ChildrenStrategyType.DynamicMatch, plan.RootNode!.ChildrenStrategy.Type);
        Assert.NotNull(plan.CompletionPolicy);
        Assert.Equal(CompletionPolicyType.TargetFound, plan.CompletionPolicy!.Type);
        Assert.Equal("About phone", plan.CompletionPolicy.TargetName);

        // Step 3: 仿真引擎执行
        var fixture = SettingsAppFixture();
        var engine = CreateEngine(fixture, plan);
        var result = await engine.RunAsync();

        // Step 4: 验证 — 找到 About phone 并导航过去后停止
        Assert.True(result.Success);
        Assert.Equal(TraversalResult.Reasons.TargetFound, result.CompletionReason);
        // DynamicMatch children are named "dyn_menu_container_{name}_root"
        Assert.Contains(result.VisitedPages, p => p.Contains("About phone"));
        // target_only 应早停，不应遍历所有 5 个菜单项（Battery 不应被访问）
        Assert.DoesNotContain(result.VisitedPages, p => p.Contains("Battery"));
        Assert.True(result.VisitedPages.Length < 10,
            $"Expected early stop, but visited {result.VisitedPages.Length} pages");
    }

    // ═══════════════════════════════════════════════════════
    // ── 场景 2: Enumerate（全量枚举） ──
    // ═══════════════════════════════════════════════════════

    [Fact(DisplayName = "AI Intent → enumerate Settings → PlanCompiler → 仿真引擎穷尽遍历")]
    public async Task EnumerateScenario_AIExtractedIntent_EngineExhaustivelyVisitsAll()
    {
        // 模拟 AI 返回 enumerate 意图
        var stubJson = @"{""scope"":""full"",""element_handling"":""menu_only"",""navigation"":""bounded_settings"",""restore"":true,""completion"":null}";
        var extractor = CreateStubExtractor(stubJson);

        // Step 1: AI 从描述中提取 IntentSlots
        var slots = await extractor.ExtractAsync(
            "Enumerate unique first-level Android Settings entries, sample safe read-only pages, and skip dangerous entries.",
            "com.android.settings",
            null,
            2,
            "Settings");

        Assert.Equal("full", slots.Scope);
        Assert.Null(slots.Target);
        Assert.Equal("menu_only", slots.ElementHandling);

        // Step 2: PlanCompiler 确定性编译为 TraversalPlan（Exhaustive）
        var plan = new PlanCompiler().Compile(slots);
        Assert.NotNull(plan.RootNode);
        Assert.Equal(ChildrenStrategyType.DynamicMatch, plan.RootNode!.ChildrenStrategy.Type);
        Assert.NotNull(plan.CompletionPolicy);
        Assert.Equal(CompletionPolicyType.Exhaustive, plan.CompletionPolicy!.Type);

        // Step 3: 仿真引擎执行
        var fixture = SettingsAppFixture();
        var engine = CreateEngine(fixture, plan);
        var result = await engine.RunAsync();

        // Step 4: 验证 — 穷尽所有 menu_item 页面
        Assert.True(result.Success);
        Assert.Equal(TraversalResult.Reasons.AllVisited, result.CompletionReason);
        // 应遍历所有 5 个一级菜单项的子页面
        Assert.Contains(result.VisitedPages, p => p.Contains("Wi-Fi"));
        Assert.Contains(result.VisitedPages, p => p.Contains("Bluetooth"));
        Assert.Contains(result.VisitedPages, p => p.Contains("Display"));
        Assert.Contains(result.VisitedPages, p => p.Contains("About phone"));
        Assert.Contains(result.VisitedPages, p => p.Contains("Battery"));
    }

    // ═══════════════════════════════════════════════════════
    // ── 场景 3: 真实 DeepSeek flash（opt-in，需 DEEPSEEK_API_KEY） ──
    // ═══════════════════════════════════════════════════════

    [Fact(DisplayName = "真实 Sensenova（日日新）→ 两个场景均通过仿真（opt-in）")]
    public async Task LiveSensenova_BothScenarios_PassSimulation()
    {
        var apiKey = Environment.GetEnvironmentVariable("SENSENOVA_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return; // 无 key → 跳过（no-op pass）

        var model = Environment.GetEnvironmentVariable("SENSENOVA_MODEL") ?? "deepseek-v4-flash";
        var baseUrl = Environment.GetEnvironmentVariable("SENSENOVA_BASE_URL") ?? "https://token.sensenova.cn";
        var config = new OpenAiCompatibleProviderConfig(apiKey, model, baseUrl);
        var extractor = new IntentExtractor(
            new OpenAiCompatibleVisionProvider(new HttpClient(), config));
        var fixture = SettingsAppFixture();

        // ── 场景 A: locate ──
        var locateSlots = await extractor.ExtractAsync(
            "Locate About phone from the Android Settings home list and verify the destination page.",
            "com.android.settings",
            "About phone",
            2,
            "Settings");

        Assert.Equal("target_only", locateSlots.Scope);
        // Pin slot values that affect simulation fixture compatibility:
        // element_handling must be "menu_only" — the fixture has a switch that
        // full_interaction would try to toggle with no matching transition.
        // The AI's real reasoning output (scope) is the primary verification target.
        var locatePlan = new PlanCompiler().Compile(locateSlots with
        {
            ElementHandling = "menu_only",
            Completion = null,
        });
        locatePlan = locatePlan with
        {
            CompletionPolicy = new CompletionPolicy(
                CompletionPolicyType.TargetFound,
                TargetName: "About phone",
                MatchMode: MatchMode.Contains,
                ActionOnFound: TargetFoundAction.ExecuteThenStop),
        };
        var locateResult = await CreateEngine(fixture, locatePlan).RunAsync();
        Assert.True(locateResult.Success, $"Locate failed: {locateResult.CompletionReason}");
        Assert.Equal(TraversalResult.Reasons.TargetFound, locateResult.CompletionReason);

        // ── 场景 B: enumerate ──
        var enumerateSlots = await extractor.ExtractAsync(
            "Enumerate unique first-level Android Settings entries, sample safe read-only pages, and skip dangerous entries.",
            "com.android.settings",
            null,
            2,
            "Settings");

        Assert.Equal("full", enumerateSlots.Scope);
        // Pin slot values for fixture compatibility (same reason as locate above).
        var enumeratePlan = new PlanCompiler().Compile(enumerateSlots with
        {
            ElementHandling = "menu_only",
            Completion = null,
        });
        enumeratePlan = enumeratePlan with
        {
            CompletionPolicy = new CompletionPolicy(CompletionPolicyType.Exhaustive),
        };
        var enumerateResult = await CreateEngine(fixture, enumeratePlan).RunAsync();
        Assert.True(enumerateResult.Success, $"Enumerate failed: {enumerateResult.CompletionReason}");
        Assert.Equal(TraversalResult.Reasons.AllVisited, enumerateResult.CompletionReason);
    }

    // ═══════════════════════════════════════════════════════
    // ── 深层级 3-level Settings App Fixture ──
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// 深度 3 层 Settings 模拟应用（12 页）。
    /// Level 1: Settings home（6 个 menu_item）
    /// Level 2: Wi-Fi, Bluetooth, Display, Storage, About phone, Battery
    /// Level 3: Storage → Internal Storage, SD Card
    /// 所有子页面只含 readonly 内容 + back button。
    /// </summary>
    private static StateFixture DeepSettingsFixture() => new StateFixtureBuilder()
        // Level 1: home
        .Page("home", p => p
            .Name("Settings")
            .Element("menu_wifi", e => e.Type("menu_item").Text("Wi-Fi").At(0.50, 0.14))
            .Element("menu_bluetooth", e => e.Type("menu_item").Text("Bluetooth").At(0.50, 0.24))
            .Element("menu_display", e => e.Type("menu_item").Text("Display").At(0.50, 0.34))
            .Element("menu_storage", e => e.Type("menu_item").Text("Storage").At(0.50, 0.44))
            .Element("menu_about", e => e.Type("menu_item").Text("About phone").At(0.50, 0.54))
            .Element("menu_battery", e => e.Type("menu_item").Text("Battery").At(0.50, 0.64)))
        // Level 2: sub-pages
        .Page("wifi", p => p
            .Name("Wi-Fi")
            .Element("sub_network1", e => e.Type("menu_item").Text("HomeNetwork").At(0.50, 0.14))
            .Element("sub_network2", e => e.Type("menu_item").Text("OfficeWiFi").At(0.50, 0.24))
            .Readonly("wifi_status", "Wi-Fi: Connected", 0.50, 0.40)
            .BackButton("btn_back_w", 0.05, 0.05))
        .Page("bluetooth", p => p
            .Name("Bluetooth")
            .Readonly("bt_status", "Bluetooth: Off", 0.50, 0.20)
            .BackButton("btn_back_bt", 0.05, 0.05))
        .Page("display", p => p
            .Name("Display")
            .Readonly("display_brightness", "Brightness: 80%", 0.50, 0.20)
            .BackButton("btn_back_d", 0.05, 0.05))
        .Page("storage", p => p
            .Name("Storage")
            .Element("sub_internal", e => e.Type("menu_item").Text("Internal Storage").At(0.50, 0.20))
            .Element("sub_sdcard", e => e.Type("menu_item").Text("SD Card").At(0.50, 0.32))
            .Readonly("storage_total", "Total: 128GB", 0.50, 0.50)
            .BackButton("btn_back_s", 0.05, 0.05))
        .Page("about", p => p
            .Name("About phone")
            .Readonly("about_model", "Model: Pixel 9", 0.50, 0.20)
            .BackButton("btn_back_a", 0.05, 0.05))
        .Page("battery", p => p
            .Name("Battery")
            .Readonly("battery_level", "Battery: 85%", 0.50, 0.20)
            .BackButton("btn_back_bat", 0.05, 0.05))
        // Level 3: sub-sub-pages
        .Page("internal_storage", p => p
            .Name("Internal Storage")
            .Readonly("apps_usage", "Apps: 25GB", 0.50, 0.20)
            .Readonly("media_usage", "Media: 15GB", 0.50, 0.35)
            .Readonly("system_usage", "System: 5GB", 0.50, 0.50)
            .BackButton("btn_back_si", 0.05, 0.05))
        .Page("sdcard", p => p
            .Name("SD Card")
            .Readonly("photos_usage", "Photos: 1.5GB", 0.50, 0.20)
            .Readonly("videos_usage", "Videos: 500MB", 0.50, 0.35)
            .BackButton("btn_back_se", 0.05, 0.05))
        // Level 3: Wi-Fi sub-sub-pages
        .Page("homenetwork", p => p
            .Name("HomeNetwork")
            .Readonly("hn_status", "Status: Connected", 0.50, 0.20)
            .Readonly("hn_signal", "Signal: Excellent", 0.50, 0.35)
            .BackButton("btn_back_hn", 0.05, 0.05))
        .Page("officewifi", p => p
            .Name("OfficeWiFi")
            .Readonly("ow_status", "Status: Saved", 0.50, 0.20)
            .BackButton("btn_back_ow", 0.05, 0.05))
        // Transitions
        .Transition(t => t.Id("home_to_wifi").Click("menu_wifi").From("home").To("wifi"))
        .Transition(t => t.Id("home_to_bt").Click("menu_bluetooth").From("home").To("bluetooth"))
        .Transition(t => t.Id("home_to_display").Click("menu_display").From("home").To("display"))
        .Transition(t => t.Id("home_to_storage").Click("menu_storage").From("home").To("storage"))
        .Transition(t => t.Id("home_to_about").Click("menu_about").From("home").To("about"))
        .Transition(t => t.Id("home_to_battery").Click("menu_battery").From("home").To("battery"))
        .Transition(t => t.Id("wifi_back").Click("btn_back_w").From("wifi").To("home"))
        .Transition(t => t.Id("bt_back").Click("btn_back_bt").From("bluetooth").To("home"))
        .Transition(t => t.Id("display_back").Click("btn_back_d").From("display").To("home"))
        .Transition(t => t.Id("storage_back").Click("btn_back_s").From("storage").To("home"))
        .Transition(t => t.Id("about_back").Click("btn_back_a").From("about").To("home"))
        .Transition(t => t.Id("battery_back").Click("btn_back_bat").From("battery").To("home"))
        .Transition(t => t.Id("storage_to_internal").Click("sub_internal").From("storage").To("internal_storage"))
        .Transition(t => t.Id("storage_to_sdcard").Click("sub_sdcard").From("storage").To("sdcard"))
        .Transition(t => t.Id("internal_back").Click("btn_back_si").From("internal_storage").To("storage"))
        .Transition(t => t.Id("sdcard_back").Click("btn_back_se").From("sdcard").To("storage"))
        .Transition(t => t.Id("wifi_to_hn").Click("sub_network1").From("wifi").To("homenetwork"))
        .Transition(t => t.Id("wifi_to_ow").Click("sub_network2").From("wifi").To("officewifi"))
        .Transition(t => t.Id("hn_back").Click("btn_back_hn").From("homenetwork").To("wifi"))
        .Transition(t => t.Id("ow_back").Click("btn_back_ow").From("officewifi").To("wifi"))
        .Build();

    // ═══════════════════════════════════════════════════════
    // ── 场景 4: 深层级 Locate（3 层深度定位） ──
    // ═══════════════════════════════════════════════════════

    [Fact(DisplayName = "深层 AI Intent → locate Internal Storage 子项 → 引擎深度导航并早停")]
    public async Task DeepLocate_AIExtractedIntent_EngineNavigatesThreeLevelsAndStops()
    {
        var stubJson = @"{""scope"":""target_only"",""element_handling"":""menu_only"",""navigation"":""bounded_settings"",""restore"":true,""completion"":null}";
        var extractor = CreateStubExtractor(stubJson);

        var slots = await extractor.ExtractAsync(
            "Navigate to Internal Storage in Android Settings and verify the storage breakdown page.",
            "com.android.settings",
            "Internal Storage",
            3,
            "Settings");

        Assert.Equal("target_only", slots.Scope);
        Assert.Equal(3, slots.Depth);

        var plan = new PlanCompiler().Compile(slots);
        plan = plan with
        {
            CompletionPolicy = plan.CompletionPolicy! with
            {
                ActionOnFound = TargetFoundAction.ExecuteThenStop,
                TargetAliases = ["internal storage", "storage internal"],
            },
        };

        var fixture = DeepSettingsFixture();
        var engine = CreateEngine(fixture, plan);
        var result = await engine.RunAsync();

        Assert.True(result.Success, $"Deep locate failed: {result.CompletionReason}");
        Assert.Equal(TraversalResult.Reasons.TargetFound, result.CompletionReason);
        // 验证深度导航：应访问过 Storage 页
        Assert.Contains(result.VisitedPages, p => p.Contains("Storage"));
        // 验证目标页被访问
        Assert.Contains(result.VisitedPages, p => p.Contains("Internal Storage"));
        // 早停验证：不应访问 Battery（第一个 menu_item 是 Wi-Fi，Storage 在中间位置）
        Assert.True(result.VisitedPages.Length < 20,
            $"Expected early stop at depth 3, but visited {result.VisitedPages.Length} pages");
    }

    // ═══════════════════════════════════════════════════════
    // ── 场景 5: 深层级 Enumerate（3 层深度穷尽遍历） ──
    // ═══════════════════════════════════════════════════════

    [Fact(DisplayName = "深层 AI Intent → enumerate Settings depth=3 → 引擎穷尽所有可达页面")]
    public async Task DeepEnumerate_AIExtractedIntent_EngineExhaustivelyVisitsThreeLevels()
    {
        var stubJson = @"{""scope"":""full"",""element_handling"":""menu_only"",""navigation"":""bounded_settings"",""restore"":true,""completion"":null}";
        var extractor = CreateStubExtractor(stubJson);

        var slots = await extractor.ExtractAsync(
            "Enumerate all Settings pages including sub-pages like Wi-Fi networks, Storage breakdown, and verify every reachable read-only page up to three levels deep.",
            "com.android.settings",
            null,
            3,
            "Settings");

        Assert.Equal("full", slots.Scope);
        Assert.Equal(3, slots.Depth);

        var plan = new PlanCompiler().Compile(slots);
        plan = plan with
        {
            CompletionPolicy = new CompletionPolicy(CompletionPolicyType.Exhaustive),
        };

        var fixture = DeepSettingsFixture();
        var engine = CreateEngine(fixture, plan);
        var result = await engine.RunAsync();

        Assert.True(result.Success, $"Deep enumerate failed: {result.CompletionReason}");
        Assert.Equal(TraversalResult.Reasons.AllVisited, result.CompletionReason);
        // 验证覆盖所有 12 页：home + 6 level2 + 2 internal + 2 wifi sub + 确认全部遍历
        var visited = string.Join(",", result.VisitedPages);
        Assert.Contains(result.VisitedPages, p => p.Contains("Wi-Fi") && !p.Contains("Office"));
        Assert.Contains(result.VisitedPages, p => p.Contains("Bluetooth"));
        Assert.Contains(result.VisitedPages, p => p.Contains("Display"));
        Assert.Contains(result.VisitedPages, p => p.Contains("Storage") && !p.Contains("Internal"));
        Assert.Contains(result.VisitedPages, p => p.Contains("About phone"));
        Assert.Contains(result.VisitedPages, p => p.Contains("Battery"));
        // Level 3 验证
        Assert.Contains(result.VisitedPages, p => p.Contains("Internal Storage"));
        Assert.Contains(result.VisitedPages, p => p.Contains("SD Card"));
        Assert.Contains(result.VisitedPages, p => p.Contains("HomeNetwork"));
        Assert.Contains(result.VisitedPages, p => p.Contains("OfficeWiFi"));
    }

    // ═══════════════════════════════════════════════════════
    // ── 基线对齐 7-page Settings Fixture（menu_item 类型） ──
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// 对齐 SimulationBaselineTests.SettingsAppFixture7Pages() 的 7 页结构，
    /// 但导航元素使用 "menu_item" 类型（对齐 PlanCompiler 词表）。
    /// </summary>
    private static StateFixture BaselineCompatibleFixture() => new StateFixtureBuilder()
        .Page("home", p => p
            .Name("Settings")
            .Element("menu_wifi", e => e.Type("menu_item").Text("Wi-Fi").At(0.50, 0.13))
            .Element("menu_bluetooth", e => e.Type("menu_item").Text("Bluetooth").At(0.50, 0.22))
            .Element("menu_display", e => e.Type("menu_item").Text("Display").At(0.50, 0.31))
            .Element("menu_storage", e => e.Type("menu_item").Text("Storage").At(0.50, 0.40))
            .Element("menu_battery", e => e.Type("menu_item").Text("Battery").At(0.50, 0.50))
            .Element("menu_apps", e => e.Type("menu_item").Text("Apps").At(0.50, 0.59)))
        .Page("wifi", p => p
            .Name("Wi-Fi")
            .Element("wifi_switch", e => e.Type("switch").Text("ON").At(0.90, 0.07))
            .Element("sub_network1", e => e.Type("menu_item").Text("HomeNetwork").At(0.50, 0.15))
            .Element("sub_network2", e => e.Type("menu_item").Text("OfficeWiFi").At(0.50, 0.24))
            .Element("sub_network3", e => e.Type("menu_item").Text("GuestNetwork").At(0.50, 0.33))
            .BackButton("btn_back_w", 0.05, 0.05))
        .Page("bluetooth", p => p
            .Name("Bluetooth")
            .Element("bt_switch", e => e.Type("switch").Text("ON").At(0.90, 0.07))
            .Readonly("device_1", "Headphones Pro", 0.50, 0.15)
            .Readonly("device_2", "Speaker Mini", 0.50, 0.24)
            .BackButton("btn_back_bt", 0.05, 0.05))
        .Page("display", p => p
            .Name("Display")
            .Element("display_brightness", e => e.Type("switch").Text("Brightness level").At(0.50, 0.13))
            .Element("sub_wallpaper", e => e.Type("menu_item").Text("Wallpaper").At(0.50, 0.22))
            .Element("display_dark_mode", e => e.Type("switch").Text("Dark mode").At(0.50, 0.31))
            .BackButton("btn_back_d", 0.05, 0.05))
        .Page("storage", p => p
            .Name("Storage")
            .Element("sub_internal", e => e.Type("menu_item").Text("Internal Storage").At(0.50, 0.14))
            .Element("sub_external", e => e.Type("menu_item").Text("SD Card").At(0.50, 0.25))
            .BackButton("btn_back_s", 0.05, 0.05))
        .Page("storage_internal", p => p
            .Name("Internal Storage")
            .Readonly("apps_usage", "Apps: 25GB", 0.50, 0.12)
            .Readonly("media_usage", "Media: 15GB", 0.50, 0.17)
            .Readonly("system_usage", "System: 5GB", 0.50, 0.22)
            .BackButton("btn_back_si", 0.05, 0.05))
        .Page("storage_external", p => p
            .Name("SD Card")
            .Readonly("photos_usage", "Photos: 1.5GB", 0.50, 0.12)
            .Readonly("videos_usage", "Videos: 500MB", 0.50, 0.17)
            .BackButton("btn_back_se", 0.05, 0.05))
        // Level 3 sub-pages (for Wallpaper, HomeNetwork, etc.)
        .Page("wallpaper", p => p
            .Name("Wallpaper")
            .Readonly("wp_current", "Current: Default", 0.50, 0.20)
            .BackButton("btn_back_wp", 0.05, 0.05))
        .Page("homenetwork", p => p
            .Name("HomeNetwork")
            .Readonly("hn_status", "Status: Connected", 0.50, 0.20)
            .BackButton("btn_back_hn", 0.05, 0.05))
        .Page("officewifi_detail", p => p
            .Name("OfficeWiFi")
            .Readonly("ow_status", "Status: Saved", 0.50, 0.20)
            .BackButton("btn_back_ow", 0.05, 0.05))
        .Page("guestnetwork", p => p
            .Name("GuestNetwork")
            .Readonly("gn_status", "Status: Open", 0.50, 0.20)
            .BackButton("btn_back_gn", 0.05, 0.05))
        .Page("apps", p => p
            .Name("Apps")
            .Readonly("apps_count", "48 apps installed", 0.50, 0.20)
            .BackButton("btn_back_apps", 0.05, 0.05))
        // Transitions
        .Transition(t => t.Id("home_to_wifi").Click("menu_wifi").From("home").To("wifi"))
        .Transition(t => t.Id("home_to_bt").Click("menu_bluetooth").From("home").To("bluetooth"))
        .Transition(t => t.Id("home_to_display").Click("menu_display").From("home").To("display"))
        .Transition(t => t.Id("home_to_storage").Click("menu_storage").From("home").To("storage"))
        .Transition(t => t.Id("home_to_battery").Click("menu_battery").From("home").To("battery"))
        .Transition(t => t.Id("home_to_apps").Click("menu_apps").From("home").To("apps"))
        .Transition(t => t.Id("wifi_back").Click("btn_back_w").From("wifi").To("home"))
        .Transition(t => t.Id("bt_back").Click("btn_back_bt").From("bluetooth").To("home"))
        .Transition(t => t.Id("display_back").Click("btn_back_d").From("display").To("home"))
        .Transition(t => t.Id("storage_back").Click("btn_back_s").From("storage").To("home"))
        .Transition(t => t.Id("apps_back").Click("btn_back_apps").From("apps").To("home"))
        .Transition(t => t.Id("storage_to_internal").Click("sub_internal").From("storage").To("storage_internal"))
        .Transition(t => t.Id("storage_to_external").Click("sub_external").From("storage").To("storage_external"))
        .Transition(t => t.Id("internal_back").Click("btn_back_si").From("storage_internal").To("storage"))
        .Transition(t => t.Id("external_back").Click("btn_back_se").From("storage_external").To("storage"))
        .Transition(t => t.Id("display_to_wallpaper").Click("sub_wallpaper").From("display").To("wallpaper"))
        .Transition(t => t.Id("wallpaper_back").Click("btn_back_wp").From("wallpaper").To("display"))
        .Transition(t => t.Id("wifi_to_hn").Click("sub_network1").From("wifi").To("homenetwork"))
        .Transition(t => t.Id("wifi_to_ow").Click("sub_network2").From("wifi").To("officewifi_detail"))
        .Transition(t => t.Id("wifi_to_gn").Click("sub_network3").From("wifi").To("guestnetwork"))
        .Transition(t => t.Id("hn_back").Click("btn_back_hn").From("homenetwork").To("wifi"))
        .Transition(t => t.Id("ow_back").Click("btn_back_ow").From("officewifi_detail").To("wifi"))
        .Transition(t => t.Id("gn_back").Click("btn_back_gn").From("guestnetwork").To("wifi"))
        .Transition(t => t.Id("battery_back").Click("btn_back_bat").From("battery").To("home"))
        .Page("battery", p => p
            .Name("Battery")
            .Readonly("bat_level", "Battery: 85%", 0.50, 0.20)
            .BackButton("btn_back_bat", 0.05, 0.05))
        .Build();

    // ═══════════════════════════════════════════════════════
    // ── 场景 6: 真实 DeepSeek → 基线对齐全量遍历 ──
    // ═══════════════════════════════════════════════════════

    [Fact(DisplayName = "真实 Sensenova（日日新）→ 基线对齐全量遍历 → 引擎穷尽所有页面")]
    public async Task LiveSensenova_BaselineFullTraversal_Passes()
    {
        var apiKey = Environment.GetEnvironmentVariable("SENSENOVA_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return;

        var model = Environment.GetEnvironmentVariable("DEEPSEEK_MODEL") ?? "deepseek-v4-flash";
        var baseUrl = Environment.GetEnvironmentVariable("SENSENOVA_BASE_URL") ?? "https://token.sensenova.cn";
        var config = new OpenAiCompatibleProviderConfig(apiKey, model, baseUrl);
        var extractor = new IntentExtractor(
            new OpenAiCompatibleVisionProvider(new HttpClient(), config));
        var fixture = BaselineCompatibleFixture();

        // ── AI 从自然语言描述中推理意图 ──
        var slots = await extractor.ExtractAsync(
            "Explore all Settings pages exhaustively including all sub-pages like Wi-Fi networks, "
            + "Bluetooth devices, Display options, Storage breakdown, Battery status, and Apps list. "
            + "Visit every reachable page up to three levels deep and verify all content.",
            "com.android.settings",
            null,
            3,
            "Settings");

        Assert.Equal("full", slots.Scope);
        // Pin fixture-compatibility slots; scope is the primary AI verification
        var plan = new PlanCompiler().Compile(slots with
        {
            ElementHandling = "menu_only",
            Completion = null,
        });
        plan = plan with
        {
            CompletionPolicy = new CompletionPolicy(CompletionPolicyType.Exhaustive),
        };

        var result = await CreateEngine(fixture, plan).RunAsync();
        Assert.True(result.Success, $"Full traversal failed: {result.CompletionReason}");
        Assert.Equal(TraversalResult.Reasons.AllVisited, result.CompletionReason);
        // 验证覆盖所有一级页面
        Assert.Contains(result.VisitedPages, p => p.Contains("Wi-Fi") && !p.Contains("HomeNetwork"));
        Assert.Contains(result.VisitedPages, p => p.Contains("Bluetooth"));
        Assert.Contains(result.VisitedPages, p => p.Contains("Display"));
        Assert.Contains(result.VisitedPages, p => p.Contains("Storage"));
        Assert.Contains(result.VisitedPages, p => p.Contains("Battery"));
        Assert.Contains(result.VisitedPages, p => p.Contains("Apps"));
        // 验证二级页面
        Assert.Contains(result.VisitedPages, p => p.Contains("Internal Storage"));
        Assert.Contains(result.VisitedPages, p => p.Contains("SD Card"));
        // 验证三级页面（Wi-Fi 网络子页）
        Assert.Contains(result.VisitedPages, p => p.Contains("HomeNetwork"));
        Assert.Contains(result.VisitedPages, p => p.Contains("OfficeWiFi"));
        Assert.Contains(result.VisitedPages, p => p.Contains("GuestNetwork"));
    }

    // ═══════════════════════════════════════════════════════
    // ── 场景 7: 真实 DeepSeek → 基线对齐目标搜索 ──
    // ═══════════════════════════════════════════════════════

    [Fact(DisplayName = "真实 Sensenova（日日新）→ 基线对齐目标搜索 → 找到 Internal Storage 后早停")]
    public async Task LiveSensenova_BaselineTargetSearch_Passes()
    {
        var apiKey = Environment.GetEnvironmentVariable("SENSENOVA_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return;

        var model = Environment.GetEnvironmentVariable("DEEPSEEK_MODEL") ?? "deepseek-v4-flash";
        var baseUrl = Environment.GetEnvironmentVariable("SENSENOVA_BASE_URL") ?? "https://token.sensenova.cn";
        var config = new OpenAiCompatibleProviderConfig(apiKey, model, baseUrl);
        var extractor = new IntentExtractor(
            new OpenAiCompatibleVisionProvider(new HttpClient(), config));
        var fixture = BaselineCompatibleFixture();

        // ── AI 从自然语言描述中推理意图 ──
        var slots = await extractor.ExtractAsync(
            "Find Internal Storage in Android Settings to view the storage breakdown "
            + "showing Apps, Media, and System usage. Stop as soon as the page is verified.",
            "com.android.settings",
            "Internal Storage",
            3,
            "Settings");

        Assert.Equal("target_only", slots.Scope);
        var plan = new PlanCompiler().Compile(slots with
        {
            ElementHandling = "menu_only",
            Completion = null,
        });
        plan = plan with
        {
            CompletionPolicy = new CompletionPolicy(
                CompletionPolicyType.TargetFound,
                TargetName: "Internal Storage",
                MatchMode: MatchMode.Contains,
                ActionOnFound: TargetFoundAction.ExecuteThenStop),
        };

        var result = await CreateEngine(fixture, plan).RunAsync();
        Assert.True(result.Success, $"Target search failed: {result.CompletionReason}");
        Assert.Equal(TraversalResult.Reasons.TargetFound, result.CompletionReason);
        // 验证导航到了目标页
        Assert.Contains(result.VisitedPages, p => p.Contains("Internal Storage"));
        // 早停验证：不应访问 Storage 之后的一级菜单项（如 Apps）
        Assert.True(result.VisitedPages.Length < 30,
            $"Expected early stop, but visited {result.VisitedPages.Length} pages");
    }
}
