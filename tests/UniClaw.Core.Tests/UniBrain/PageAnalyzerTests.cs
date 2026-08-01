using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// PageAnalyzer 单元测试 — OpenSpec change unibrain-analyzevisual-vertical-slice tasks 5.1 + 5.2.
/// 用手写 fake IModelProvider.CompleteVisionAsync + fake IScreenCapture，无网络/真机。
/// 验证 7 步链路：ctor null guard / 模板缺失 fail-fast / happy path / §12-A 派生 4 分支 /
/// fail-fast (非法 type / Direction / 越界 coordinate / Items null) / 截图透传 / NIE /
/// 观测闭环 (5.2: 装配期 router.Resolve(AnalyzeVisual) 套 ObservingModelProvider + InMemoryTraceRecorder
/// → AICallRecord mode="vision" capability=analyze_visual)。
///
/// 注：PageAnalyzer / IScreenCapture 由并行 coder 实现；本测试按 spec 契约编写，
/// 生产侧合并前 build 会因缺类型失败（期）。spec §5 要求非法 type 抛 DomainValidationException，
/// 即 PageAnalyzer 必须用 ElementTypeMapper.IsValidType 主动校验（ToMenuItemType/ToExpectedAction
/// 有回落值不会抛）—— 本测试 §5.5 已反映此契约。
/// </summary>
public sealed class PageAnalyzerTests
{
    /// <summary>注册 analyze_visual 模板的 PromptLibrary（引自 PromptTemplateRegistry 单点真源）。</summary>
    private static PromptLibrary MakePromptLibrary() =>
        new(PromptTemplateRegistry.AnalyzeVisual);

    // ── fakes ──────────────────────────────────────────────────────

    /// <summary>
    /// Fake IModelProvider — CompleteVisionAsync 返回声明式 JSON content，并记录被传入的 byte[]。
    /// CompleteTextAsync/CompleteMultimodalAsync 不在本切片路径，抛 NIE。
    /// </summary>
    private sealed class FakeVisionProvider : IModelProvider
    {
        private readonly string _content;
        private readonly bool _success;
        private readonly string? _error;
        public string ProviderId => "fake-vision";
        public byte[]? CapturedImage { get; private set; }
        public ModelRequest? CapturedRequest { get; private set; }
        public int VisionCallCount { get; private set; }

        public FakeVisionProvider(string content, bool success = true, string? error = null)
        {
            _content = content;
            _success = success;
            _error = error;
        }

        public Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
        {
            VisionCallCount++;
            CapturedRequest = request;
            CapturedImage = imageData;
            var resp = new ModelResponse(_content, ProviderId, "vision", 50, 200, 15.0) with
            {
                Success = _success,
                ErrorMessage = _error,
            };
            return Task.FromResult(resp);
        }

        public Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)
            => throw new NotImplementedException("FakeVisionProvider does not implement CompleteTextAsync.");

        public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
            => throw new NotImplementedException("FakeVisionProvider does not implement CompleteMultimodalAsync.");
    }

    /// <summary>
    /// 若被触达则抛 InvalidOperationException — 用于验证模板缺失时不发起模型调用。
    /// </summary>
    private sealed class ThrowIfCalledProvider : IModelProvider
    {
        public bool WasCalled { get; private set; }
        public string ProviderId => "throw-if-called";

        public Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)
        {
            WasCalled = true;
            throw new InvalidOperationException("provider should not be called when analyze_visual template is missing");
        }

        public Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
        {
            WasCalled = true;
            throw new InvalidOperationException("provider should not be called when analyze_visual template is missing");
        }

        public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Fake IScreenCapture — CaptureAsync 返回固定 bytes。spec D5: IScreenCapture 在 Traversal/，
    /// Task&lt;byte[]&gt; CaptureAsync(CancellationToken)。
    /// </summary>
    private sealed class FakeScreenCapture : IScreenCapture
    {
        private readonly byte[] _bytes;
        public int CaptureCallCount { get; private set; }

        public FakeScreenCapture(byte[] bytes) => _bytes = bytes;

        public Task<byte[]> CaptureAsync(CancellationToken ct = default)
        {
            CaptureCallCount++;
            return Task.FromResult(_bytes);
        }
    }

    // ── fixture JSON helpers ───────────────────────────────────────

    /// <summary>happy path JSON：items 覆盖 §12-A 4 分支 (menu_item/button/switch/text)，不含 action 3 字段。</summary>
    private static string HappyPathJson() =>
        "{\"level1_dir\":\"left\","
        + "\"level1_menus\":[{\"name\":\"Settings\",\"coordinate\":{\"x\":0.1,\"y\":0.5},\"active\":true}],"
        + "\"level2_dir\":\"top\",\"level2_menus\":[],"
        + "\"current_path\":[\"Settings\"],"
        + "\"items\":["
        + "{\"name\":\"WiFi\",\"type\":\"menu_item\",\"coordinate\":{\"x\":0.5,\"y\":0.2},\"parent\":null},"
        + "{\"name\":\"OK\",\"type\":\"button\",\"coordinate\":{\"x\":0.5,\"y\":0.3},\"parent\":null},"
        + "{\"name\":\"Airplane Mode\",\"type\":\"switch\",\"coordinate\":{\"x\":0.5,\"y\":0.4},\"parent\":null},"
        + "{\"name\":\"Description\",\"type\":\"text\",\"coordinate\":{\"x\":0.5,\"y\":0.5},\"parent\":null}"
        + "],\"is_popup\":false,\"popup_info\":null,\"close_button\":null,\"back_button\":null,"
        + "\"has_scroll\":false,\"is_end_of_list\":false}";

    // ── 1. ctor null guards (spec: null → DomainValidationException) ────

    [Fact(DisplayName = "ctor: modelProvider null → DomainValidationException")]
    public void Ctor_ModelProviderNull_Throws()
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => new PageAnalyzer(
                null!,
                MakePromptLibrary(),
                new FakeScreenCapture(new byte[] { 1, 2, 3 })));
        Assert.Contains("modelProvider", ex.FieldName);
    }

    [Fact(DisplayName = "ctor: promptLibrary null → DomainValidationException")]
    public void Ctor_PromptLibraryNull_Throws()
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => new PageAnalyzer(
                new FakeVisionProvider(HappyPathJson()),
                null!,
                new FakeScreenCapture(new byte[] { 1, 2, 3 })));
        Assert.Contains("promptLibrary", ex.FieldName);
    }

    [Fact(DisplayName = "ctor: screenCapture null → DomainValidationException")]
    public void Ctor_ScreenCaptureNull_Throws()
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => new PageAnalyzer(
                new FakeVisionProvider(HappyPathJson()),
                MakePromptLibrary(),
                null!));
        Assert.Contains("screenCapture", ex.FieldName);
    }

    // ── 2. 模板缺失 fail-fast (spec: 不发模型调用) ────

    [Fact(DisplayName = "模板缺失 → DomainValidationException，未调 CompleteVisionAsync (spec: before any model call)")]
    public async Task AnalyzeCurrentPageAsync_MissingTemplate_ThrowsWithoutModelCall()
    {
        var provider = new ThrowIfCalledProvider();
        var capture = new FakeScreenCapture(new byte[] { 1, 2, 3 });
        // 空 PromptLibrary：无 analyze_visual 模板
        var analyzer = new PageAnalyzer(provider, new PromptLibrary(), capture);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => analyzer.AnalyzeCurrentPageAsync());

        Assert.Contains("template", ex.Message);
        // spec: "before any model call is made" — 仅断言模型 provider 未被触达
        // (设计 D2: 截图在模板检查之前，CaptureAsync 可被调)
        Assert.False(provider.WasCalled, "模板失时不应发起模型调用");
    }

    // ── 3. happy path (spec scenario: Happy path derives PageAnalysis) ────

    [Fact(DisplayName = "Happy path: mock JSON → PageAnalysis 各字段正确 (menu/path/popup/scroll)")]
    public async Task AnalyzeCurrentPageAsync_ValidJson_ReturnsParsedPageAnalysis()
    {
        var provider = new FakeVisionProvider(HappyPathJson());
        var capture = new FakeScreenCapture(new byte[] { 1, 2, 3 });
        var analyzer = new PageAnalyzer(provider, MakePromptLibrary(), capture);

        var page = await analyzer.AnalyzeCurrentPageAsync();

        Assert.NotNull(page);
        var p = page!;
        Assert.Equal(Direction.Left, p.Level1Dir);
        Assert.Equal(Direction.Top, p.Level2Dir);
        var menu = Assert.Single(p.Level1Menus);
        Assert.Equal("Settings", menu.Name);
        Assert.True(menu.Active);
        Assert.Equal(0.1, menu.Coordinate.X);
        Assert.Equal(0.5, menu.Coordinate.Y);
        Assert.Equal(new[] { "Settings" }, p.CurrentPath.ToArray());
        Assert.False(p.IsPopup);
        Assert.Null(p.PopupInfo);
        Assert.Null(p.CloseButton);
        Assert.Null(p.BackButton);
        Assert.False(p.HasScroll);
        Assert.False(p.IsEndOfList);
        Assert.Equal(4, p.Items.Length);
    }

    // ── 4. §12-A 派生 4 分支 (spec scenario: ElementTypeMapper derivation covers 4 branches) ────

    [Fact(DisplayName = "§12-A 派生: switch→Toggle/stateChange=true/pageChange=false")]
    public async Task Derive_Switch_TogglesStateChange()
    {
        var json = HappyPathJson();
        var provider = new FakeVisionProvider(json);
        var analyzer = new PageAnalyzer(provider, MakePromptLibrary(), new FakeScreenCapture(new byte[] { 1 }));

        var page = (await analyzer.AnalyzeCurrentPageAsync())!;
        var item = Assert.Single(page.Items, i => i.Name == "Airplane Mode");

        Assert.Equal(MenuItemType.Switch, item.Type);
        Assert.Equal(ExpectedAction.Toggle, item.ExpectedAction);
        Assert.True(item.ExpectsStateChange);
        Assert.False(item.ExpectsPageChange);
    }

    [Fact(DisplayName = "§12-A 派生: menu_item→Navigate/pageChange=true/stateChange=false")]
    public async Task Derive_MenuItem_NavigatesPageChange()
    {
        var provider = new FakeVisionProvider(HappyPathJson());
        var analyzer = new PageAnalyzer(provider, MakePromptLibrary(), new FakeScreenCapture(new byte[] { 1 }));

        var page = (await analyzer.AnalyzeCurrentPageAsync())!;
        var item = Assert.Single(page.Items, i => i.Name == "WiFi");

        Assert.Equal(MenuItemType.MenuItem, item.Type);
        Assert.Equal(ExpectedAction.Navigate, item.ExpectedAction);
        Assert.True(item.ExpectsPageChange);
        Assert.False(item.ExpectsStateChange);
    }

    [Fact(DisplayName = "§12-A 派生: button→Action/pageChange=true/stateChange=false")]
    public async Task Derive_Button_ActionPageChange()
    {
        var provider = new FakeVisionProvider(HappyPathJson());
        var analyzer = new PageAnalyzer(provider, MakePromptLibrary(), new FakeScreenCapture(new byte[] { 1 }));

        var page = (await analyzer.AnalyzeCurrentPageAsync())!;
        var item = Assert.Single(page.Items, i => i.Name == "OK");

        Assert.Equal(MenuItemType.Button, item.Type);
        Assert.Equal(ExpectedAction.Action, item.ExpectedAction);
        Assert.True(item.ExpectsPageChange);
        Assert.False(item.ExpectsStateChange);
    }

    [Fact(DisplayName = "§12-A 派生: text→None/both false")]
    public async Task Derive_Text_NoneNoChange()
    {
        var provider = new FakeVisionProvider(HappyPathJson());
        var analyzer = new PageAnalyzer(provider, MakePromptLibrary(), new FakeScreenCapture(new byte[] { 1 }));

        var page = (await analyzer.AnalyzeCurrentPageAsync())!;
        var item = Assert.Single(page.Items, i => i.Name == "Description");

        Assert.Equal(MenuItemType.Text, item.Type);
        Assert.Equal(ExpectedAction.None, item.ExpectedAction);
        Assert.False(item.ExpectsPageChange);
        Assert.False(item.ExpectsStateChange);
    }

    // ── 5. fail-fast (spec scenarios: invalid type / Direction / coordinate / Items null) ────

    /// <summary>
    /// ⚠️ spec §5 要求: 非法 type 抛 DomainValidationException。
    /// ElementTypeMapper.ToMenuItemType/ToExpectedAction 有回落值 (Item/None) 不会抛 —
    /// 因此 PageAnalyzer 必须用 ElementTypeMapper.IsValidType(dto.Type) 主动校验，false 则抛。
    /// 本测试断言 type="zzz_invalid" → DomainValidationException。请统筹校验生产侧确实这么做了。
    /// </summary>
    [Fact(DisplayName = "fail-fast: 非法 type (zzz_invalid) → DomainValidationException (需 IsValidType 主动校验)")]
    public async Task InvalidType_Throws()
    {
        var json = "{\"level1_dir\":\"left\",\"level1_menus\":[],\"level2_dir\":\"top\",\"level2_menus\":[],"
            + "\"current_path\":[],\"items\":[{\"name\":\"X\",\"type\":\"zzz_invalid\","
            + "\"coordinate\":{\"x\":0.5,\"y\":0.5},\"parent\":null}],"
            + "\"is_popup\":false,\"popup_info\":null,\"close_button\":null,\"back_button\":null,"
            + "\"has_scroll\":false,\"is_end_of_list\":false}";
        var provider = new FakeVisionProvider(json);
        var analyzer = new PageAnalyzer(provider, MakePromptLibrary(), new FakeScreenCapture(new byte[] { 1 }));

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => analyzer.AnalyzeCurrentPageAsync());

        Assert.Contains("zzz_invalid", ex.Message);
    }

    [Fact(DisplayName = "fail-fast: 非法 level1_dir (upside_down) → DomainValidationException")]
    public async Task InvalidDirection_Throws()
    {
        var json = "{\"level1_dir\":\"upside_down\",\"level1_menus\":[],\"level2_dir\":\"top\",\"level2_menus\":[],"
            + "\"current_path\":[],\"items\":[],\"is_popup\":false,\"popup_info\":null,"
            + "\"close_button\":null,\"back_button\":null,\"has_scroll\":false,\"is_end_of_list\":false}";
        var provider = new FakeVisionProvider(json);
        var analyzer = new PageAnalyzer(provider, MakePromptLibrary(), new FakeScreenCapture(new byte[] { 1 }));

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => analyzer.AnalyzeCurrentPageAsync());

        Assert.Contains("upside_down", ex.Message);
    }

    [Fact(DisplayName = "fail-fast: coordinate 越界 (x=1.5) → DomainValidationException")]
    public async Task OutOfRangeCoordinate_Throws()
    {
        var json = "{\"level1_dir\":\"left\",\"level1_menus\":[],\"level2_dir\":\"top\",\"level2_menus\":[],"
            + "\"current_path\":[],\"items\":[{\"name\":\"X\",\"type\":\"button\","
            + "\"coordinate\":{\"x\":1.5,\"y\":0.5},\"parent\":null}],"
            + "\"is_popup\":false,\"popup_info\":null,\"close_button\":null,\"back_button\":null,"
            + "\"has_scroll\":false,\"is_end_of_list\":false}";
        var provider = new FakeVisionProvider(json);
        var analyzer = new PageAnalyzer(provider, MakePromptLibrary(), new FakeScreenCapture(new byte[] { 1 }));

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => analyzer.AnalyzeCurrentPageAsync());

        // PageAnalyzer 把模型返回的像素/越界坐标归类为可重试的视觉坐标错误。
        Assert.Equal("coordinate", ex.FieldName);
        Assert.Contains("normalized range", ex.Message);
    }

    [Fact(DisplayName = "fail-fast: Items null → DomainValidationException")]
    public async Task NullItems_Throws()
    {
        // items 字段缺失 → DTO.Items 为 null → PageAnalyzer 映射期 fail-fast
        var json = "{\"level1_dir\":\"left\",\"level1_menus\":[],\"level2_dir\":\"top\",\"level2_menus\":[],"
            + "\"current_path\":[],\"is_popup\":false,\"popup_info\":null,"
            + "\"close_button\":null,\"back_button\":null,\"has_scroll\":false,\"is_end_of_list\":false}";
        var provider = new FakeVisionProvider(json);
        var analyzer = new PageAnalyzer(provider, MakePromptLibrary(), new FakeScreenCapture(new byte[] { 1 }));

        await Assert.ThrowsAsync<DomainValidationException>(
            () => analyzer.AnalyzeCurrentPageAsync());
    }

    // ── 6. 截图透传 (spec scenario: byte stream passed to CompleteVisionAsync equals CaptureAsync bytes) ────

    [Fact(DisplayName = "截图透传: CaptureAsync bytes → CompleteVisionAsync imageData 一致")]
    public async Task CaptureBytes_PassedToCompleteVisionAsync()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var provider = new FakeVisionProvider(HappyPathJson());
        var capture = new FakeScreenCapture(bytes);
        var analyzer = new PageAnalyzer(provider, MakePromptLibrary(), capture);

        await analyzer.AnalyzeCurrentPageAsync();

        Assert.Equal(1, capture.CaptureCallCount);
        Assert.Equal(1, provider.VisionCallCount);
        Assert.NotNull(provider.CapturedImage);
        Assert.Equal(bytes, provider.CapturedImage);
    }

    // ── 7. NIE (spec scenario: Other two interface methods not implemented) ────

    [Fact(DisplayName = "FindAppEntryAsync 抛 NotImplementedException (pending future slice)")]
    public async Task FindAppEntryAsync_ThrowsNotImplemented()
    {
        var analyzer = new PageAnalyzer(
            new FakeVisionProvider(HappyPathJson()),
            MakePromptLibrary(),
            new FakeScreenCapture(new byte[] { 1 }));

        var ex = await Assert.ThrowsAsync<NotImplementedException>(
            () => analyzer.FindAppEntryAsync("com.example.app"));

        Assert.Contains("pending", ex.Message);
        Assert.Contains("future slice", ex.Message);
    }

    [Fact(DisplayName = "VerifyPageTypeAsync 抛 NotImplementedException (pending future slice)")]
    public async Task VerifyPageTypeAsync_ThrowsNotImplemented()
    {
        var analyzer = new PageAnalyzer(
            new FakeVisionProvider(HappyPathJson()),
            MakePromptLibrary(),
            new FakeScreenCapture(new byte[] { 1 }));

        var page = new PageAnalysis(Direction.Left, Direction.Top);
        var ex = await Assert.ThrowsAsync<NotImplementedException>(
            () => analyzer.VerifyPageTypeAsync(page, "settings"));

        Assert.Contains("pending", ex.Message);
        Assert.Contains("future slice", ex.Message);
    }

    // ── 5.2 观测闭环 (spec scenario: Vision-mode observation record is produced) ────

    [Fact(DisplayName = "观测闭环: 装配期 router.Resolve(AnalyzeVisual) 套 ObservingModelProvider → AICallRecord mode=vision capability=analyze_visual")]
    public async Task AnalyzeCurrentPageAsync_ThroughRouter_RecordsVisionAICall()
    {
        // 共享 storage：InMemoryTraceRecorder 写入，用其 GetAICalls() 断言观测层记录
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);

        // 装配期 router：capability=analyze_visual 路由到 fake provider，套 ObservingModelProvider
        var bareProvider = new FakeVisionProvider(HappyPathJson());
        var routing = ImmutableDictionary.CreateRange(new[]
        {
            KeyValuePair.Create(ModelCapabilities.AnalyzeVisual, "mock"),
        });
        var providers = ImmutableDictionary.CreateRange<string, IModelProvider>(new[]
        {
            KeyValuePair.Create<string, IModelProvider>("mock", bareProvider),
        });
        var router = new ModelRouter(routing, providers, recorder, "mock");

        // D-8: 装配期 Resolve 产物（已套 ObservingModelProvider）注入 PageAnalyzer
        var observedProvider = router.Resolve(ModelCapabilities.AnalyzeVisual);
        var analyzer = new PageAnalyzer(
            observedProvider,
            MakePromptLibrary(),
            new FakeScreenCapture(new byte[] { 1, 2, 3 }));

        await analyzer.AnalyzeCurrentPageAsync();

        var calls = storage.GetAICalls();
        Assert.NotEmpty(calls);
        var record = Assert.Single(calls)!;
        Assert.Equal(ModelCapabilities.AnalyzeVisual, record.Capability);
        // ObservingModelProvider.BuildRecord 把 mode 写入 metadata["mode"]
        Assert.NotNull(record.Metadata);
        Assert.True(record.Metadata!.ContainsKey("mode"));
        Assert.Equal("vision", record.Metadata["mode"]?.ToString());
    }

    // ── 模型失败 fail-fast (spec scenario: Model call failure propagates) ────

    [Fact(DisplayName = "模型返回 Success=false → DomainValidationException 含 ErrorMessage (重试耗尽后)")]
    public async Task ModelFailure_ThrowsWithError()
    {
        var provider = new FakeVisionProvider("ignored", success: false, error: "vision boom");
        var analyzer = new PageAnalyzer(provider, MakePromptLibrary(), new FakeScreenCapture(new byte[] { 1 }));

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => analyzer.AnalyzeCurrentPageAsync());

        Assert.Contains("vision boom", ex.Message);
        // 重试会重发（一次抖动不杀死 run），全部失败后才抛
        Assert.Equal(2, provider.VisionCallCount);
    }

    // ── 视觉失败重试 (修复: 真实模型偶发截断/抖动不应杀死整个 run) ────

    /// <summary>
    /// Fake 提供方 — 前 N 次调用返回 invalid JSON（截断），之后返回 valid JSON。
    /// 用于验证 PageAnalyzer 在 JSON 解析失败后重试并最终成功。
    /// </summary>
    private sealed class FlakyVisionProvider : IModelProvider
    {
        private readonly string _invalidJson;
        private readonly string _validJson;
        private readonly int _failCount;
        public int VisionCallCount { get; private set; }
        public string ProviderId => "flaky-vision";

        public FlakyVisionProvider(int failCount, string validJson)
        {
            _failCount = failCount;
            _validJson = validJson;
            // 截断的 JSON：在 items 数组中间切开（未闭合）
            _invalidJson = validJson[..validJson.LastIndexOf("],", StringComparison.Ordinal)];
        }

        public Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
        {
            VisionCallCount++;
            var content = VisionCallCount <= _failCount ? _invalidJson : _validJson;
            return Task.FromResult(new ModelResponse(content, ProviderId, "vision", 50, 200, 15.0));
        }

        public Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    [Fact(DisplayName = "JSON 解析失败 (截断) → 重试一次后成功，重发新截图")]
    public async Task InvalidJson_RetriesAndSucceeds()
    {
        var provider = new FlakyVisionProvider(1, HappyPathJson());
        var capture = new FakeScreenCapture(new byte[] { 1 });
        var analyzer = new PageAnalyzer(provider, MakePromptLibrary(), capture);

        var page = await analyzer.AnalyzeCurrentPageAsync();

        Assert.NotNull(page);
        Assert.Equal(4, page!.Items.Length);
        Assert.Equal(2, provider.VisionCallCount);
        Assert.Equal(2, capture.CaptureCallCount); // 每次重试重新截屏
    }

    [Fact(DisplayName = "JSON 解析连续失败 → 重试耗尽后仍抛 DomainValidationException")]
    public async Task InvalidJson_Persistent_ThrowsAfterRetries()
    {
        var provider = new FlakyVisionProvider(5, HappyPathJson());
        var analyzer = new PageAnalyzer(provider, MakePromptLibrary(), new FakeScreenCapture(new byte[] { 1 }));

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => analyzer.AnalyzeCurrentPageAsync());

        Assert.Contains("not valid JSON", ex.Message);
        Assert.Equal(2, provider.VisionCallCount); // MaxAnalyzeAttempts
    }

    [Fact(DisplayName = "坐标越界 (像素坐标) → 重试一次后成功，重发新截图")]
    public async Task PixelCoordinate_RetriesAndSucceeds()
    {
        var provider = new PixelCoordinateVisionProvider(1, HappyPathJson());
        var capture = new FakeScreenCapture(new byte[] { 1 });
        var analyzer = new PageAnalyzer(provider, MakePromptLibrary(), capture);

        var page = await analyzer.AnalyzeCurrentPageAsync();

        Assert.NotNull(page);
        Assert.Equal(4, page!.Items.Length);
        Assert.Equal(2, provider.VisionCallCount);
        Assert.Equal(2, capture.CaptureCallCount); // 每次重试重新截屏
    }

    [Fact(DisplayName = "坐标越界连续发生 → 重试耗尽后抛 DomainValidationException")]
    public async Task PixelCoordinate_Persistent_ThrowsAfterRetries()
    {
        var provider = new PixelCoordinateVisionProvider(5, HappyPathJson());
        var analyzer = new PageAnalyzer(provider, MakePromptLibrary(), new FakeScreenCapture(new byte[] { 1 }));

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => analyzer.AnalyzeCurrentPageAsync());

        Assert.Contains("out of normalized range", ex.Message);
        Assert.Equal(2, provider.VisionCallCount); // MaxAnalyzeAttempts
    }

    /// <summary>返回像素坐标(非归一化)JSON 的 provider — 前 failCount 次失败, 之后返回有效 JSON。</summary>
    private sealed class PixelCoordinateVisionProvider : IModelProvider
    {
        private readonly string _pixelJson;
        private readonly string _validJson;
        private readonly int _failCount;
        public int VisionCallCount { get; private set; }
        public string ProviderId => "pixel-coordinate-vision";

        public PixelCoordinateVisionProvider(int failCount, string validJson)
        {
            _failCount = failCount;
            _validJson = validJson;
            _pixelJson = validJson.Replace("0.5", "500");
        }

        public Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
        {
            VisionCallCount++;
            var content = VisionCallCount <= _failCount ? _pixelJson : _validJson;
            return Task.FromResult(new ModelResponse(content, ProviderId, "vision", 50, 200, 15.0));
        }

        public Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
