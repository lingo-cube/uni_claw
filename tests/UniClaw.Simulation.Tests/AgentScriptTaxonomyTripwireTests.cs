using System.Reflection;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// SIM-004 Step 14 — 架构 tripwire：防止 Product AgentDecision taxonomy 的
/// Simulation 镜像复活（AgentScriptKind / ScriptDecisionKind 形态）。变体名
/// 集合经反射取自 Product union——<b>不写死「只有四种决策」</b>：Product
/// 未来新增 AgentDecision.X 自动纳入保护；Simulation 使用 X 只需 Product
/// 类型载荷槽，不需要扩展自己的 semantic taxonomy。
/// </summary>
public sealed class AgentScriptTaxonomyTripwireTests
{
    /// <summary>Product AgentDecision 变体名（反射派生）。</summary>
    private static HashSet<string> ProductDecisionVariantNames() =>
        typeof(AgentDecision).GetNestedTypes(BindingFlags.Public)
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// 源头活性证明：变体名集合确实包含当前全部 Product 变体（nameof 编译期
    /// 引用——Product 新增变体时本断言随编译器强制更新；防止反射源空转导致
    /// 下一测试空洞通过）。
    /// </summary>
    [Fact]
    public void TripwireSource_IncludesAllCurrentProductVariants()
    {
        var variantNames = ProductDecisionVariantNames();
        Assert.Contains(nameof(AgentDecision.Act), variantNames);
        Assert.Contains(nameof(AgentDecision.NoAction), variantNames);
        Assert.Contains(nameof(AgentDecision.Defer), variantNames);
        Assert.Contains(nameof(AgentDecision.Policy), variantNames);
    }

    /// <summary>
    /// 本 test 程序集内任何枚举的任何成员名都不得是 Product AgentDecision
    /// 变体名——镜像 taxonomy（含部分镜像：Act/NoAction/…）复活即 RED。
    /// </summary>
    [Fact]
    public void SimulationTests_DeclareNoEnumMirroringProductDecisionVariants()
    {
        var variantNames = ProductDecisionVariantNames();
        var violations = new List<string>();
        foreach (var type in typeof(ScriptedUniAgent).Assembly.GetTypes().Where(t => t.IsEnum))
        {
            foreach (var name in Enum.GetNames(type))
                if (variantNames.Contains(name))
                    violations.Add($"{type.Name}.{name}");
        }

        Assert.True(violations.Count == 0,
            "Simulation 枚举镜像 Product AgentDecision 变体（SIM-004 ABS-003：禁止平行 taxonomy）: "
            + string.Join(",", violations));
    }

    /// <summary>
    /// ScriptedTurn 载荷槽类型必须来自 Product 程序集（ExpectedPhase /
    /// Act / Completion / Defer / Policy）——「返回什么」的语义类型不得是
    /// Simulation 自有类型（Behavior 为 double 行为枚举、Justification 为
    /// string，二者豁免——前者由上一测试执法其非镜像性）。
    /// </summary>
    [Fact]
    public void ScriptedTurn_PayloadSlots_AreProductTypes()
    {
        var product = typeof(AgentDecision).Assembly;
        var constructor = typeof(ScriptedTurn).GetConstructors().Single();
        foreach (var parameter in constructor.GetParameters())
        {
            if (parameter.Name is "Behavior" || parameter.ParameterType == typeof(string))
                continue;
            var effective = parameter.ParameterType.IsGenericType
                ? parameter.ParameterType.GetGenericArguments()[0]
                : parameter.ParameterType;
            Assert.Equal(product, effective.Assembly);
        }
    }
}
