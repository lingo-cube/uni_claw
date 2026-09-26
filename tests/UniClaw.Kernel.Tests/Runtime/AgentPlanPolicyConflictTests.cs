using UniClaw.Kernel.Control;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// PER-009 S5：冲突可见性 → 聚焦复查决策（mechanism.md ⑤）。
/// 悬案与已采纳目标相交 → Observe + TargetSubject；复用既有意图面，
/// 零 ControlIntentKind 变更。
/// </summary>
public sealed class AgentPlanPolicyConflictTests
{
    private static AgentPlanPolicy Policy(params TargetSpec[] specs)
    {
        var policy = new AgentPlanPolicy();
        if (specs.Length > 0)
            policy.Adopt(specs);
        return policy;
    }

    [Fact]
    public void Conflict_IntersectingAdoptedTarget_YieldsFocusedObserve()
    {
        var policy = Policy(new TargetSpec("switch", null, "tap", UniClaw.Kernel.Perception.UiHierarchy.CheckedState.Unchecked));
        policy.ConflictedSubjects = new[] { SharedSubjects.State("switch") };
        // 相交命中在触碰 inputs 之前返回——冲突分支自足
        var decision = policy.Decide(null!);
        Assert.Equal(ControlIntentKind.Observe, decision.Kind);
        Assert.Equal(SharedSubjects.State("switch"), decision.TargetSubject);
    }

    [Fact]
    public void Conflict_PrefixIntersect_CoversChildSubjects()
    {
        var policy = Policy(new TargetSpec("switch", "wifi", "tap", UniClaw.Kernel.Perception.UiHierarchy.CheckedState.Unchecked));
        // "switch:wifi.state" ⊂ "switch:wifi" 前缀相交；"switch.state" 不相交
        policy.ConflictedSubjects = new[] { "switch.state", SharedSubjects.State("switch:wifi") };
        var decision = policy.Decide(null!);
        Assert.Equal(SharedSubjects.State("switch:wifi"), decision.TargetSubject);
    }

    [Fact]
    public void Conflict_NotIntersecting_FallsThrough()
    {
        var policy = Policy(); // 未采纳 → 默认 Observe（无聚焦）
        policy.ConflictedSubjects = new[] { SharedSubjects.State("unrelated") };
        var decision = policy.Decide(null!);
        Assert.Equal(ControlIntentKind.Observe, decision.Kind);
        Assert.Null(decision.TargetSubject);
    }

    [Fact]
    public void Conflict_OnOtherTarget_DoesNotIntercept_FallsThroughToAdopted()
    {
        // 采纳 switch，悬案在 other.*——不相交 → 不聚焦，落到已采纳 policy
        //（其 Decide 消费 inputs；null inputs 必然 NRE = 证明未被冲突分支拦截）
        var policy = Policy(new TargetSpec("switch", null, "tap", UniClaw.Kernel.Perception.UiHierarchy.CheckedState.Unchecked));
        policy.ConflictedSubjects = new[] { SharedSubjects.State("other") };
        Assert.Throws<ArgumentNullException>(() => policy.Decide(null!));
    }

    [Fact]
    public void NoConflicts_BehaviorUnchanged()
    {
        var policy = Policy();
        var decision = policy.Decide(null!);
        Assert.Equal(ControlIntentKind.Observe, decision.Kind);
        Assert.Null(decision.TargetSubject);
    }

    [Fact]
    public void SharedSubjects_FrozenTrio_ValueDomain()
    {
        // S8 值域断言前置：共享三件套拼写与 {on,off,partial} 语义位
        Assert.Equal("ui.screen", SharedSubjects.Screen);
        Assert.Equal("screen.frame", SharedSubjects.Frame);
        Assert.Equal("switch.state", SharedSubjects.State("switch"));
    }
}
