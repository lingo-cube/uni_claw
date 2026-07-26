using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.Simulation;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// analyze_visual 端到端测试 — OpenSpec change unibrain-analyzevisual-vertical-slice task 5.3.
/// 组装真实组件全链：
///   analyze_visual.mock.json (磁盘 fixture) → MockModelFixture.FromJson
///   → MockVisionProvider (从 fixture 取 content，经 CompleteVisionAsync 返回)
///   → ModelRouter（装配期套 ObservingModelProvider）
///   → router.Resolve(AnalyzeVisual) 产物注入 PageAnalyzer (D-8: 只收 IModelProvider)
///   → PromptLibrary(analyze_visual 模板 stub) + FakeScreenCapture
///   → CaptureAsync → CompleteVisionAsync(req, bytes) → Content → PageAnalysis。
/// 验证 D-8「router 降为装配期工厂」：PageAnalyzer ctor 不收 IModelRouter，方法体内不调 router.Resolve。
/// 结构镜像 DecideNextActionEndToEndTests。无网络/真机。
/// </summary>
public sealed class AnalyzeVisualEndToEndTests
{
    // ── prompt 模板（引自 PromptTemplateRegistry 单点真源） ──────────

    // ── fakes ──────────────────────────────────────────────────────

    /// <summary>
    /// Vision-capable mock IModelProvider — 从 MockModelFixture 取 analyze_visual 预设 content，
    /// 经 CompleteVisionAsync 返回。MockModelProvider.CompleteVisionAsync 在本切片抛 NIE
    /// （它只覆盖 CompleteTextAsync），故端到端 vision 路径需此测试侧 fake。
    /// </summary>
    private sealed class MockVisionProvider : IModelProvider
    {
        private readonly MockModelFixture _fixture;
        public string ProviderId => "mock-vision";

        public MockVisionProvider(MockModelFixture fixture) => _fixture = fixture;

        public Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
        {
            var entry = _fixture.Resolve(request.Capability ?? string.Empty)
                ?? throw new InvalidOperationException(
                    $"MockVisionProvider has no preset for capability '{request.Capability}'.");
            var resp = new ModelResponse(entry.Content, ProviderId, "vision", entry.InputTokens, entry.OutputTokens, entry.LatencyMs) with
            {
                Success = entry.Success,
                ErrorMessage = entry.ErrorMessage,
            };
            return Task.FromResult(resp);
        }

        public Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)
            => throw new NotImplementedException("MockVisionProvider is vision-only.");
        public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
            => throw new NotImplementedException("MockVisionProvider is vision-only.");
    }

    /// <summary>Fake IScreenCapture — 返固定 bytes (spec D5 接口形状)。</summary>
    private sealed class FakeScreenCapture : IScreenCapture
    {
        private readonly byte[] _bytes;
        public FakeScreenCapture(byte[] bytes) => _bytes = bytes;
        public Task<byte[]> CaptureAsync(CancellationToken ct = default) => Task.FromResult(_bytes);
    }

    // ── 端到端 ──────────────────────────────────────────────────────

    [Fact(DisplayName = "端到端: CaptureAsync → CompleteVisionAsync(req,bytes) → Content → PageAnalysis，且 D-8 router 降为装配期工厂")]
    public async Task AnalyzeVisual_FullStack_ReturnsPageAnalysisAndRecordsVisionAICall()
    {
        // 共享 storage：InMemoryTraceRecorder 写入，用其 GetAICalls() 断言观测层记录
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);

        // 从磁盘 fixture 加载预设响应表（csproj 已拷贝 Fixtures/**/*.json 到输出目录）
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "analyze_visual.mock.json");
        var fixture = MockModelFixture.FromJson(File.ReadAllText(fixturePath));

        var bareProvider = new MockVisionProvider(fixture);

        // ModelRouter 装配期为裸 provider 套 ObservingModelProvider → 调用必产生 AICallRecord
        var router = new ModelRouter(
            ImmutableDictionary.CreateRange(new[]
            {
                KeyValuePair.Create(ModelCapabilities.AnalyzeVisual, "mock"),
            }),
            ImmutableDictionary.CreateRange<string, IModelProvider>(new[]
            {
                KeyValuePair.Create<string, IModelProvider>("mock", bareProvider),
            }),
            recorder,
            "mock");

        // D-8: 装配期 Resolve 产物（已套 ObservingModelProvider）注入 PageAnalyzer
        // PageAnalyzer ctor 只收 IModelProvider，不收 IModelRouter — 验证 router 降为装配期工厂
        var observedProvider = router.Resolve(ModelCapabilities.AnalyzeVisual);
        var analyzer = new PageAnalyzer(
            observedProvider,
            new PromptLibrary(PromptTemplateRegistry.AnalyzeVisual),
            new FakeScreenCapture(new byte[] { 1, 2, 3, 4 }));

        // Act
        var page = await analyzer.AnalyzeCurrentPageAsync();

        // Assert: PageAnalysis 各字段
        Assert.NotNull(page);
        Assert.Equal(Direction.Left, page!.Level1Dir);
        Assert.Equal(Direction.Top, page.Level2Dir);
        var menu = Assert.Single(page.Level1Menus);
        Assert.Equal("Settings", menu.Name);
        Assert.True(menu.Active);
        Assert.Equal(new[] { "Settings" }, page.CurrentPath.ToArray());
        Assert.False(page.IsPopup);
        Assert.False(page.HasScroll);
        Assert.False(page.IsEndOfList);
        Assert.Equal(4, page.Items.Length);

        // Assert: §12-A 派生 4 分支
        var wifi = Assert.Single(page.Items, i => i.Name == "WiFi");
        Assert.Equal(MenuItemType.MenuItem, wifi.Type);
        Assert.Equal(ExpectedAction.Navigate, wifi.ExpectedAction);
        Assert.True(wifi.ExpectsPageChange);
        Assert.False(wifi.ExpectsStateChange);

        var ok = Assert.Single(page.Items, i => i.Name == "OK");
        Assert.Equal(MenuItemType.Button, ok.Type);
        Assert.Equal(ExpectedAction.Action, ok.ExpectedAction);
        Assert.True(ok.ExpectsPageChange);
        Assert.False(ok.ExpectsStateChange);

        var airplane = Assert.Single(page.Items, i => i.Name == "Airplane Mode");
        Assert.Equal(MenuItemType.Switch, airplane.Type);
        Assert.Equal(ExpectedAction.Toggle, airplane.ExpectedAction);
        Assert.False(airplane.ExpectsPageChange);
        Assert.True(airplane.ExpectsStateChange);

        var desc = Assert.Single(page.Items, i => i.Name == "Description");
        Assert.Equal(MenuItemType.Text, desc.Type);
        Assert.Equal(ExpectedAction.None, desc.ExpectedAction);
        Assert.False(desc.ExpectsPageChange);
        Assert.False(desc.ExpectsStateChange);

        // Assert: AICallRecord 被观测层记录（经 router 套的 ObservingModelProvider 产生）
        var record = Assert.Single(storage.GetAICalls())!;
        Assert.Equal(ModelCapabilities.AnalyzeVisual, record.Capability);
        Assert.Equal("mock-vision", record.ProviderId);
        Assert.True(record.Success);
        Assert.NotNull(record.Metadata);
        Assert.True(record.Metadata!.ContainsKey("mode"));
        Assert.Equal("vision", record.Metadata["mode"]?.ToString());
    }
}