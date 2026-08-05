using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Host.Tests.Runner;

/// <summary>
/// M4 形状契约测试 —— 解决 OpenSpec change host-target-architecture 冲突 C4。
/// UIAutomator 路径已移除 (delete-uia)：契约由 AI 路径 (PageAnalyzer) 单独满足。
/// 负向测试断言：省略 Level1Menus 的旧式 PageAnalysis 会被契约捕获
/// (mock green ⇒ real-path-shape green)。
/// </summary>
public sealed class PageAnalysisShapeContractTests
{
    // ── AI 路径 fakes (与 PageAnalyzerTests 同源模式) ──────────────────

    private static PromptLibrary MakePromptLibrary() =>
        new(PromptTemplateRegistry.AnalyzeVisual);

    private sealed class FakeVisionProvider : IModelProvider
    {
        private readonly string _content;
        public string ProviderId => "fake-vision";

        public FakeVisionProvider(string content) => _content = content;

        public Task<ModelResponse> CompleteVisionAsync(
            ModelRequest request, byte[] imageData, CancellationToken ct = default)
        {
            var resp = new ModelResponse(_content, ProviderId, "vision", 50, 200, 15.0);
            return Task.FromResult(resp);
        }

        public Task<ModelResponse> CompleteTextAsync(
            ModelRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ModelResponse> CompleteMultimodalAsync(
            ModelRequest request, byte[] imageData, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeScreenCapture : IScreenCapture
    {
        public Task<byte[]> CaptureAsync(CancellationToken ct = default)
            => Task.FromResult(new byte[] { 1, 2, 3 });

        public Task<RawScreenBuffer> CaptureRawAsync(CancellationToken ct = default)
            => throw new NotSupportedException("Raw capture not supported in test fake");
    }

    /// <summary>
    /// AI 路径 JSON：Settings 首页，两个一级菜单 (Network / Display)。
    /// level1_dir/level2_dir 缺省 → AI 路径回落 Direction.Left (PageAnalyzer.cs:141-142)。
    /// </summary>
    private static string AiPathJson() =>
        "{\"level1_menus\":["
        + "{\"name\":\"Network\",\"coordinate\":{\"x\":0.5,\"y\":0.12},\"active\":false},"
        + "{\"name\":\"Display\",\"coordinate\":{\"x\":0.5,\"y\":0.20},\"active\":false}"
        + "],\"level2_menus\":[],"
        + "\"current_path\":[\"Settings\"],"
        + "\"items\":["
        + "{\"name\":\"Network\",\"type\":\"menu_item\",\"coordinate\":{\"x\":0.5,\"y\":0.12},\"parent\":null},"
        + "{\"name\":\"Display\",\"type\":\"menu_item\",\"coordinate\":{\"x\":0.5,\"y\":0.20},\"parent\":null}"
        + "],\"is_popup\":false,\"popup_info\":null,\"close_button\":null,\"back_button\":null,"
        + "\"has_scroll\":true,\"is_end_of_list\":false}";

    // ── 1. 负向测试：省略契约字段的旧式 PageAnalysis 被捕获 ──────────────

    [Fact(DisplayName = "C4 负向: 旧式 PageAnalysis (Level1Menus=Empty) 在有 items 时违反契约")]
    public void LegacyPageAnalysis_WithoutLevel1Menus_FailsContract()
    {
        // 旧式构造：未填 Level1Menus (回落 Empty)，但有 items —— C4 修复前的形状。
        var legacy = new PageAnalysis(
            Direction.Left,
            Direction.Left,
            CurrentPath: ["Settings"],
            Items: ImmutableArray.Create(
                new MenuItem("Network", new Coordinate(0.5, 0.12), MenuItemType.MenuItem,
                    ExpectedAction: ExpectedAction.Navigate, ExpectsPageChange: true),
                new MenuItem("Display", new Coordinate(0.5, 0.20), MenuItemType.MenuItem,
                    ExpectedAction: ExpectedAction.Navigate, ExpectsPageChange: true)),
            HasScroll: true,
            IsEndOfList: false);

        // 契约：有 items 的页面必须 Level1Menus 非空。旧式违反 → 契约应捕获。
        var contractOk = SatisfiesLevel1MenusContract(legacy);
        Assert.False(contractOk,
            "旧式 PageAnalysis (Level1Menus=Empty 但 Items 非空) 必须被契约判为违反；"
            + "若此断言失败，说明契约形同虚设，C4 修复失去守卫。");
    }

    [Fact(DisplayName = "C4 正向: 修复后 AI 路径 PageAnalysis 通过同一契约")]
    public async Task FixedAiPathPageAnalysis_SatisfiesContract()
    {
        var ai = await BuildAiPathPageAnalysisAsync();

        Assert.True(SatisfiesLevel1MenusContract(ai),
            "修复后的 AI 路径必须通过 Level1Menus 契约。");
    }

    // ── 契约实现 ───────────────────────────────────────────────────────

    /// <summary>
    /// 形状契约：当 Items 非空时，Level1Menus 必须非空且数量与 Items 一致。
    /// 这是 C4 的实质性守卫。
    /// </summary>
    private static bool SatisfiesLevel1MenusContract(PageAnalysis pa)
    {
        if (!pa.Items.IsDefault && pa.Items.Length > 0)
        {
            return !pa.Level1Menus.IsDefault
                   && pa.Level1Menus.Length == pa.Items.Length;
        }
        return pa.Level1Menus.IsDefault || pa.Level1Menus.Length == 0;
    }

    private static async Task<PageAnalysis> BuildAiPathPageAnalysisAsync()
    {
        var provider = new FakeVisionProvider(AiPathJson());
        var analyzer = new PageAnalyzer(
            provider,
            MakePromptLibrary(),
            new FakeScreenCapture());
        var page = await analyzer.AnalyzeCurrentPageAsync();
        Assert.NotNull(page);
        return page!;
    }
}
