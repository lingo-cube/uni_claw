using System.Text.RegularExpressions;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// AGT-010 §2 验收 1-2 — e1 实录语料（23 份 XML）离线区分度验证：
/// 撞名页与根页 RouteKey 不同；同页相邻观察（含滚动前后）key 一致；
/// 无标题页回退身份；ViewportDigest 同视口稳定、滚动变化、与 key 独立。
/// 语料 = evidence/real-settings-coverage-negative-20261001/run-e1-with-evidence/
/// （撞名现场 step 20 同源；分析记录见 changes/AGT-010 Decisions 1-3）。
/// </summary>
public sealed class SettingsRouteKeyTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return directory.FullName;
            directory = directory.Parent!;
        }
        throw new InvalidOperationException("未定位到仓库根");
    }

    private static string Corpus(string capture) => Path.Combine(RepoRoot(),
        "evidence", "real-settings-coverage-negative-20261001", "run-e1-with-evidence",
        "evidence", capture + ".xml");

    /// <summary>根页采样（12 份：初始视口 2 + 滚动后 10；三视口态成组）。</summary>
    private static readonly string[] RootCaptures =
    {
        "capture-21d9968674a343f4b75787b6fb3af044",
        "capture-4a7726969f964b1494297485d0b0d986",
        "capture-0d1006df470e4e0391fb8bb8b7f97719",
        "capture-15e81b6ab12c4752b9ca835f36de8e50",
        "capture-50483045782c406b9c67d483be26e220",
        "capture-5c45ac093fa34ae4ae640ba13d6653c6",
        "capture-76d80cdb364d4cf490eb7933ddb3e476",
        "capture-c71c0772da0647b5bea3a441d038f578",
        "capture-d2348a1e6d2a4742ad880317a1dc74c0",
        "capture-f066af4c27df402ba5ff898008e0fb35",
        "capture-fce639580fb24ebfa7c30bf1a492eaef",
        "capture-fd9ab6156e0c446ca9e34b58c4d7ff00",
    };

    private const string RootKey =
        "android.settings|rk1:Settings|src=homepage_title|up=0";

    [Fact]
    public void CollidingPage_RouteKey_DiffersFromRoot()
    {
        // 撞名现场：Security & privacy 页标题 = "Settings"（android:id/title），
        // 与根页同标题——多信号（src/up/sc）必须区分
        var colliding = SettingsTraversalLiveFeed.DeriveRouteKey(
            File.ReadAllText(Corpus("capture-7323abdbfd054a2288c276092a25f7e8")));
        var root = SettingsTraversalLiveFeed.DeriveRouteKey(
            File.ReadAllText(Corpus("capture-4a7726969f964b1494297485d0b0d986")));

        Assert.Equal("Settings", ExtractTitle(colliding));
        Assert.Equal("Settings", ExtractTitle(root)); // 标题相同（撞名成立）
        Assert.NotEqual(root, colliding); // 身份不同（去撞名）
        Assert.Equal("android.settings|rk1:Settings|src=title|up=1", colliding);
    }

    [Fact]
    public void RootPage_AcrossScrollStates_RouteKeyStable()
    {
        // 同页相邻观察（含初始/滚动后视口）key 一致 = 身份滚动不变
        var keys = RootCaptures
            .Select(c => SettingsTraversalLiveFeed.DeriveRouteKey(File.ReadAllText(Corpus(c))))
            .Distinct()
            .ToList();
        Assert.Equal(new[] { RootKey }, keys);
    }

    [Fact]
    public void TitlelessPage_FallsBackToUnknownIdentity()
    {
        var key = SettingsTraversalLiveFeed.DeriveRouteKey(
            File.ReadAllText(Corpus("capture-3e3809ea136e408aaa72f75513953086")));
        Assert.Equal("android.settings", key);
        Assert.Null(SettingsTraversalLiveFeed.DeriveViewportDigest(null));
    }

    [Fact]
    public void ViewportDigest_StableForSameViewport_ChangesOnScroll()
    {
        // 同视口重复观察：稳定
        var scrolled1 = SettingsTraversalLiveFeed.DeriveViewportDigest(
            File.ReadAllText(Corpus("capture-0d1006df470e4e0391fb8bb8b7f97719")));
        var scrolled2 = SettingsTraversalLiveFeed.DeriveViewportDigest(
            File.ReadAllText(Corpus("capture-15e81b6ab12c4752b9ca835f36de8e50")));
        Assert.Equal(scrolled1, scrolled2);
        Assert.StartsWith("vd1:", scrolled1);

        // 滚动后内容变化：digest 变
        var initial = SettingsTraversalLiveFeed.DeriveViewportDigest(
            File.ReadAllText(Corpus("capture-4a7726969f964b1494297485d0b0d986")));
        Assert.NotEqual(initial, scrolled1);
    }

    [Fact]
    public void RouteKey_And_ViewportDigest_AreIndependent()
    {
        // 独立性 1：同 key（根页）不同 digest（滚动前后视口）
        var rootInitialKey = SettingsTraversalLiveFeed.DeriveRouteKey(
            File.ReadAllText(Corpus("capture-4a7726969f964b1494297485d0b0d986")));
        var rootScrolledKey = SettingsTraversalLiveFeed.DeriveRouteKey(
            File.ReadAllText(Corpus("capture-0d1006df470e4e0391fb8bb8b7f97719")));
        var rootInitialDigest = SettingsTraversalLiveFeed.DeriveViewportDigest(
            File.ReadAllText(Corpus("capture-4a7726969f964b1494297485d0b0d986")));
        var rootScrolledDigest = SettingsTraversalLiveFeed.DeriveViewportDigest(
            File.ReadAllText(Corpus("capture-0d1006df470e4e0391fb8bb8b7f97719")));
        Assert.Equal(rootInitialKey, rootScrolledKey);
        Assert.NotEqual(rootInitialDigest, rootScrolledDigest);

        // 独立性 2：同 digest 不同 key（语料实证：Display 与 Internet 页
        // clickable 景观同摘要、标题不同 → key 不同）
        var displayKey = SettingsTraversalLiveFeed.DeriveRouteKey(
            File.ReadAllText(Corpus("capture-1f44ad9365084c3c923ff8470eadfbb5")));
        var internetKey = SettingsTraversalLiveFeed.DeriveRouteKey(
            File.ReadAllText(Corpus("capture-8357c152d8a84516bcd27fb459e3ab8b")));
        var displayDigest = SettingsTraversalLiveFeed.DeriveViewportDigest(
            File.ReadAllText(Corpus("capture-1f44ad9365084c3c923ff8470eadfbb5")));
        var internetDigest = SettingsTraversalLiveFeed.DeriveViewportDigest(
            File.ReadAllText(Corpus("capture-8357c152d8a84516bcd27fb459e3ab8b")));
        Assert.Equal(displayDigest, internetDigest);
        Assert.NotEqual(displayKey, internetKey);
    }

    private static string ExtractTitle(string routeKey)
    {
        var match = Regex.Match(routeKey, @"\|rk1:([^|]+)\|");
        return match.Success ? match.Groups[1].Value : "";
    }
}
