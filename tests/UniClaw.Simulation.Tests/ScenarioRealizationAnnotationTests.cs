using System.Text.Json;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// SIM-002 G4（S4 / 基线 C7 v0.2）：Agent 侧 realization 拆分标注执法。
/// 场景库每个条目必须标注两个独立面（simulation-baseline v0.2 C7）：
///   agentDecisionRealization —— 决策面（ConsultAgent seam 的实现）
///   goalEvaluationRealization —— 评估面（PrimaryGoal→GoalEvaluation）
/// 标注必须与 Simulation Host 的**实际构成**一致（当前 hybrid：
/// decision=double（ScriptedUniAgent），evaluation=真件（UniAgent——
/// ScenarioRunner.BuildReport 经 new UniAgent(BuildGoal(...)).Evaluate））。
/// 构成翻转时本测试的构成锚点必须同步改，从而强制场景库重标注。
/// </summary>
public sealed class ScenarioRealizationAnnotationTests
{
    /// <summary>当前 Simulation Host 的实际 Agent 侧构成（构成锚点）。</summary>
    private const string ActualDecisionRealization = "double";   // ScriptedUniAgent
    private const string ActualEvaluationRealization = "real";   // UniClaw.Agent.UniAgent

    private static readonly HashSet<string> LegalValues = new(StringComparer.Ordinal)
    {
        "real", "double",
    };

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "UniClaw.Kernel.slnx")))
                return directory.FullName;
            directory = directory.Parent!;
        }
        throw new InvalidOperationException("未定位到仓库根");
    }

    /// <summary>构成锚点的活性证明：ScriptedUniAgent 真在组合面上（非死常量）。</summary>
    [Fact]
    public void CompositionAnchor_ScriptedAgentIsTheComposedDecisionDouble()
    {
        // SimulationHost.ScriptedAgent 属性类型即 ScriptedUniAgent（编译期事实）；
        // 这里以一次真实组合确认它在组合产物上非空可达。
        var host = SimulationHost.Compose(GoldenScenarioBundles.WifiToggleOffToOn());
        Assert.NotNull(host.ScriptedAgent);
        Assert.IsType<ScriptedUniAgent>(host.ScriptedAgent);
    }

    [Fact]
    public void ScenarioLibrary_CarriesSplitRealizationAnnotationsMatchingComposition()
    {
        var repo = RepoRoot();
        var files = Directory.EnumerateFiles(Path.Combine(repo, "scenarios"), "SCN-*.json")
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
        Assert.NotEmpty(files);

        var violations = new List<string>();
        foreach (var file in files)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var root = document.RootElement;
            var name = Path.GetFileName(file);

            if (!root.TryGetProperty("agentDecisionRealization", out var decision)
                || decision.ValueKind != JsonValueKind.String)
                violations.Add($"{name}: 缺 agentDecisionRealization（C7 v0.2 拆分标注）");
            else if (!LegalValues.Contains(decision.GetString()))
                violations.Add($"{name}: agentDecisionRealization 非法值 '{decision.GetString()}'（legal: real|double）");
            else if (decision.GetString() != ActualDecisionRealization)
                violations.Add(
                    $"{name}: agentDecisionRealization='{decision.GetString()}' 与实际构成不符"
                    + $"（Simulation Host 现组合 {ActualDecisionRealization}）");

            if (!root.TryGetProperty("goalEvaluationRealization", out var evaluation)
                || evaluation.ValueKind != JsonValueKind.String)
                violations.Add($"{name}: 缺 goalEvaluationRealization（C7 v0.2 拆分标注）");
            else if (!LegalValues.Contains(evaluation.GetString()))
                violations.Add($"{name}: goalEvaluationRealization 非法值 '{evaluation.GetString()}'（legal: real|double）");
            else if (evaluation.GetString() != ActualEvaluationRealization)
                violations.Add(
                    $"{name}: goalEvaluationRealization='{evaluation.GetString()}' 与实际构成不符"
                    + $"（ScenarioRunner 评估现走 {ActualEvaluationRealization}）");
        }

        Assert.True(violations.Count == 0,
            "场景库 realization 标注违规（simulation-baseline v0.2 C7）：\n" + string.Join("\n", violations));
    }
}
