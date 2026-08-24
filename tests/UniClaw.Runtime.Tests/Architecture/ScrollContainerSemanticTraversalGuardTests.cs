using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Architecture;

/// <summary>
/// PHYSICAL_SCROLL_CONTAINER_SEMANTIC_TRAVERSAL (BOUNDED_IMPLEMENTATION) 静态约束证明。
///
/// 机械保证该 change 未违反 ArchitectureDelta / AuthorityDelta / NewSemanticConcepts 禁令：
///   1. SemanticGoalInput 仍恰好 3 属性（ObjectIdentity / StateDimension / DesiredValue）——
///      运行时探索知识绝不混入「用户想要什么」的输入模型。
///   2. ViewportExplorationEvidence 仍是三值布尔判据 + Reason（无几何 / 无坐标 / 无计数）。
///   3. Runtime 生产源码不含任何场景专属（AutomaticSystemUpdates / DeveloperOptions / 页面锚点 / 滚动计数 /
///      顺序视口路由 / 滚动规划器）或目标专属滑动坐标。
///   4. 语义环滚动分支通过 RefreshContainerEvidence 逐观测 fresh 绑定；累积视口观测
///      （ViewportExplorationObservations）只由 Agent.EvaluateViewportExploration 消费为判据，
///      绝不直接作为当前动作几何。
/// </summary>
public sealed class ScrollContainerSemanticTraversalGuardTests
{
    private const string RuntimeSourceDir = "src/UniClaw.Runtime";

    /// <summary>场景专属 / 路由 / 规划器 / 计数 token（禁止出现在 Runtime 生产源码）。</summary>
    private static readonly string[] ForbiddenRuntimeTokens =
    {
        "AutomaticSystemUpdates",
        "DeveloperOptions",
        "Automatic system updates",
        "Developer options",
        "scrollCountForPage",
        "ScrollManager",
        "ScrollPlanner",
        "ViewportNavigator",
        "ScrollWorkflow",
        "ViewportRoute",
        "ScrollRoute",
        // 目标专属滑动坐标：Runtime 不出现任何物理 Swipe 表达式（机制翻译在 Adapters，非本 change 改动面）
        "Swipe(",
    };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            // 仓库根 = 同时含 AGENTS.md 与 src/UniClaw.Runtime.sln（子级区域地图只满足 AGENTS.md）。
            if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(dir.FullName, "src", "UniClaw.Runtime.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("无法定位仓库根目录（未找到 AGENTS.md）。");
    }

    private static string RepoRootPath(string relative)
        => Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void SemanticGoalInput_HasExactlyThreeProperties_NoEvaluatorField()
    {
        var properties = typeof(SemanticGoalInput)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(3, properties.Length);
        Assert.Contains(properties, p => p.Name == "ObjectIdentity");
        Assert.Contains(properties, p => p.Name == "StateDimension");
        Assert.Contains(properties, p => p.Name == "DesiredValue");
        // 运行时探索知识绝不进入「用户想要什么」输入模型
        Assert.DoesNotContain(properties, p => p.Name == "ViewportExplorationEvaluator");
    }

    [Fact]
    public void ViewportExplorationEvidence_IsBooleanVerdictOnly_NoGeometryOrCount()
    {
        var properties = typeof(ViewportExplorationEvidence)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(2, properties.Length);
        Assert.Contains(properties, p => p.Name == "ContinueExploration" && p.PropertyType == typeof(bool?));
        Assert.Contains(properties, p => p.Name == "Reason" && p.PropertyType == typeof(string));
        // 判据输出绝不携带几何 / 坐标 / 滚动计数 / 目标专属视口索引
        Assert.DoesNotContain(properties, p => p.Name is "Bounds" or "ElementIndex" or "ScrollCount" or "ViewportIndex");
    }

    [Fact]
    public void RuntimeSource_HasNoScenarioSpecificScrollLogic()
    {
        var runtimeSrc = RepoRootPath(RuntimeSourceDir);
        foreach (var file in Directory.GetFiles(runtimeSrc, "*.cs", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            var relative = Path.GetRelativePath(RepoRoot(), file);
            foreach (var token in ForbiddenRuntimeTokens)
            {
                Assert.False(
                    content.Contains(token, StringComparison.Ordinal),
                    $"[Scroll-Container Guard 失败] {relative} 含场景专属/路由/规划器 token「{token}」。"
                    + " Runtime 不得编码 AutomaticSystemUpdates/DeveloperOptions 场景知识、滚动计数、顺序视口路由或目标专属滑动坐标。");
            }
        }
    }

    [Fact]
    public void AgentSemanticRun_ScrollBranch_UsesFreshRebind_NotAccumulatedObservationsAsGeometry()
    {
        var source = File.ReadAllText(RepoRootPath("src/UniClaw.Runtime/Agent/Agent.SemanticRun.cs"));

        // 滚动后必须逐观测 fresh 绑定（RefreshContainerEvidence → RefreshSemanticSnapshot），
        // 旧视口 grounding 不得授权下一个动作。
        Assert.Contains("RefreshContainerEvidence(container, scrollObs)", source, StringComparison.Ordinal);

        // 语义环绝不直接读取累积视口观测作为动作几何；该字段只由 Agent.EvaluateViewportExploration 消费为判据。
        Assert.DoesNotContain("ViewportExplorationObservations", source, StringComparison.Ordinal);

        // 目标绑定后仍走既有毕业链（SetSwitch 由既有 lowering 产出），滚动分支不构造任何 SetSwitch / Tap。
        Assert.DoesNotContain("new DeviceAction.SetSwitch", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Agent_EvaluateViewportExploration_HasFuncOverload_ForSemanticLoop()
    {
        var methods = typeof(UniClaw.Runtime.Agent.Agent)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.Name == "EvaluateViewportExploration")
            .ToArray();

        // 语义环通过 Func 判据 overload 消费累积视口证据（Goal overload 保留给 Plan-run 路径）。
        Assert.Contains(methods, m =>
            m.GetParameters().Length == 4
            && m.GetParameters()[0].ParameterType == typeof(Func<ImmutableArray<Observation>, ViewportExplorationEvidence>));
    }
}
