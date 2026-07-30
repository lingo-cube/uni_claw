using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
using UniClaw.Device;
using UniClaw.Host.Runner;
using Xunit;

namespace UniClaw.Host.Tests.Runner;

/// <summary>
/// M4 形状契约测试 —— 解决 OpenSpec change host-target-architecture 冲突 C4。
/// 断言 AI 路径 (PageAnalyzer) 与 UIAutomator 路径 (UiAutomatorPageAnalysis.Parse)
/// 在 PageAnalysis 的菜单列表字段上结构等价：Level1Menus / Level2Menus / Items /
/// CurrentPath / HasScroll / IsEndOfList。Direction 不在等价断言范围（两条路径都回落
/// Left，但语义来源不同——AI 路径来自 DTO 缺省，UIAutomator 路径来自无方向语义的显式
/// fallback；D4 决策只要求"同一回落规则"，不要求 Direction 值本身可派生对照）。
///
/// 负向测试断言：省略 Level1Menus 的旧式 PageAnalysis 会被契约捕获（mock green ⇒ real-path-shape green）。
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
    }

    /// <summary>
    /// AI 路径 JSON：Settings 首页，两个一级菜单 (Network / Display) 对齐 UIAutomator fixture。
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

    /// <summary>
    /// UIAutomator dump fixture：Settings 首页两个可交互项 (Network / Display)。
    /// bounds 选用 1080x2400 画布，坐标与 AI 路径 JSON 对齐
    /// (Network 中心 ≈ 540,288 → 0.5,0.12；Display 中心 ≈ 540,480 → 0.5,0.20)。
    /// </summary>
    private static string UiAutomatorXml() =>
        "<?xml version='1.0' encoding='UTF-8'?>"
        + "<hierarchy>"
        + "<node resource-id='com.android.settings:id/dashboard_container' "
        + "class='androidx.recyclerview.widget.RecyclerView' clickable='false' bounds='[0,200][1080,2400]'>"
        + "<node resource-id='' class='android.widget.LinearLayout' clickable='true' "
        + "bounds='[0,216][1080,360]'><node text='Network' class='android.widget.TextView' "
        + "bounds='[0,216][1080,360]' /></node>"
        + "<node resource-id='' class='android.widget.LinearLayout' clickable='true' "
        + "bounds='[0,408][1080,552]'><node text='Display' class='android.widget.TextView' "
        + "bounds='[0,408][1080,552]' /></node>"
        + "</node>"
        + "</hierarchy>";

    private static ScreenStateResult ScreenState(bool hasScroll, bool isEnd) =>
        new(
            Succeeded: true,
            Status: hasScroll ? "scrollable" : "no_scroll",
            HierarchyXml: UiAutomatorXml(),
            HierarchyFingerprint: "fixture-fp",
            HasScroll: hasScroll,
            IsEndOfList: isEnd,
            Failure: null);

    // ── 1. 双路径形状等价 ───────────────────────────────────────────────

    [Fact(DisplayName = "C4: AI 路径与 UIAutomator 路径 PageAnalysis 菜单列表形状等价")]
    public async Task BothPaths_ProduceShapeEquivalentPageAnalysis()
    {
        var ai = await BuildAiPathPageAnalysisAsync();
        var uia = UiAutomatorPageAnalysis.Parse(UiAutomatorXml(), ScreenState(hasScroll: true, isEnd: false));

        AssertShapeContract(ai, uia);
    }

    [Fact(DisplayName = "C4: UIAutomator 路径为含项页面填充 Level1Menus 非空")]
    public void UiAutomatorPath_FillsLevel1MenusForPageWithItems()
    {
        var uia = UiAutomatorPageAnalysis.Parse(UiAutomatorXml(), ScreenState(hasScroll: true, isEnd: false));

        Assert.NotEmpty(uia.Level1Menus);
        Assert.Equal(uia.Items.Length, uia.Level1Menus.Length);
        Assert.True(uia.Level1Menus.All(m => !string.IsNullOrWhiteSpace(m.Name)));
        Assert.True(uia.Level1Menus.All(m => m.Coordinate is not null));
    }

    [Fact(DisplayName = "C4: UIAutomator 路径 Level2Menus 为空 (dump 无二级层级 — 诚实值)")]
    public void UiAutomatorPath_Level2MenusEmptyWhenNoSecondLevel()
    {
        var uia = UiAutomatorPageAnalysis.Parse(UiAutomatorXml(), ScreenState(hasScroll: true, isEnd: false));

        Assert.Empty(uia.Level2Menus);
    }

    [Fact(DisplayName = "C4: 两条路径的 Direction 均回落 Left (D4 同一回落规则)")]
    public async Task BothPaths_DirectionFallsBackToLeft()
    {
        var ai = await BuildAiPathPageAnalysisAsync();
        var uia = UiAutomatorPageAnalysis.Parse(UiAutomatorXml(), ScreenState(hasScroll: true, isEnd: false));

        Assert.Equal(Direction.Left, ai.Level1Dir);
        Assert.Equal(Direction.Left, ai.Level2Dir);
        Assert.Equal(Direction.Left, uia.Level1Dir);
        Assert.Equal(Direction.Left, uia.Level2Dir);
    }

    // ── 2. 负向测试：省略契约字段的旧式 PageAnalysis 被捕获 ──────────────

    [Fact(DisplayName = "C4 负向: 旧式 PageAnalysis (Level1Menus=Empty) 在有 items 时违反契约")]
    public void LegacyPageAnalysis_WithoutLevel1Menus_FailsContract()
    {
        // 旧式构造：未填 Level1Menus (回落 Empty)，但有 items —— C4 修复前 UIAutomator 路径的形状。
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

    [Fact(DisplayName = "C4 负向: 修复后 UIAutomator PageAnalysis 通过同一契约")]
    public void FixedUiAutomatorPageAnalysis_SatisfiesContract()
    {
        var uia = UiAutomatorPageAnalysis.Parse(UiAutomatorXml(), ScreenState(hasScroll: true, isEnd: false));

        Assert.True(SatisfiesLevel1MenusContract(uia),
            "修复后的 UIAutomator 路径必须通过 Level1Menus 契约。");
    }

    // ── 契约实现 ───────────────────────────────────────────────────────

    /// <summary>
    /// 形状契约：当 Items 非空时，Level1Menus 必须非空且数量与 Items 一致
    /// (UIAutomator 路径把顶层 items 映射为 Level1Menus)。这是 C4 的实质性守卫。
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

    /// <summary>
    /// 断言两条路径的 PageAnalysis 在契约字段上结构等价。
    /// Direction 不在断言范围（D4：只要求同一回落规则，已在独立测试中验证）。
    /// </summary>
    private static void AssertShapeContract(PageAnalysis ai, PageAnalysis uia)
    {
        // Level1Menus: 名称集合等价 (顺序无关，因两条路径的 dedup/排序可能不同)
        var aiNames = ai.Level1Menus.Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var uiaNames = uia.Level1Menus.Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(aiNames);
        Assert.Equal(aiNames, uiaNames);

        // Level1Menus 坐标等价 (按名称配对，容差 1e-4)
        var aiByName = ai.Level1Menus.ToDictionary(m => m.Name, StringComparer.Ordinal);
        var uiaByName = uia.Level1Menus.ToDictionary(m => m.Name, StringComparer.Ordinal);
        foreach (var name in aiNames)
        {
            var a = aiByName[name];
            var u = uiaByName[name];
            AssertInRange(a.Coordinate.X, u.Coordinate.X, 1e-4);
            AssertInRange(a.Coordinate.Y, u.Coordinate.Y, 1e-4);
        }

        // Level2Menus: 均为空
        Assert.Empty(ai.Level2Menus);
        Assert.Empty(uia.Level2Menus);

        // Items: 名称 + 坐标等价
        var aiItems = ai.Items.OrderBy(i => i.Name, StringComparer.Ordinal).ToArray();
        var uiaItems = uia.Items.OrderBy(i => i.Name, StringComparer.Ordinal).ToArray();
        Assert.Equal(aiItems.Length, uiaItems.Length);
        for (int i = 0; i < aiItems.Length; i++)
        {
            Assert.Equal(aiItems[i].Name, uiaItems[i].Name);
            AssertInRange(aiItems[i].Coordinate.X, uiaItems[i].Coordinate.X, 1e-4);
            AssertInRange(aiItems[i].Coordinate.Y, uiaItems[i].Coordinate.Y, 1e-4);
        }

        // CurrentPath
        Assert.Equal(ai.CurrentPath.ToArray(), uia.CurrentPath.ToArray());

        // HasScroll / IsEndOfList
        Assert.Equal(ai.HasScroll, uia.HasScroll);
        Assert.Equal(ai.IsEndOfList, uia.IsEndOfList);
    }

    private static void AssertInRange(double expected, double actual, double tolerance)
    {
        Assert.InRange(actual, expected - tolerance, expected + tolerance);
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