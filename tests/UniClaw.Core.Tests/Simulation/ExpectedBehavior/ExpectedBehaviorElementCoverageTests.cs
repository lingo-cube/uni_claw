using System.Collections.Immutable;
using UniClaw.Core.Simulation.ExpectedBehavior;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.Simulation;

/// <summary>
/// ElementCoverageExpectation 验证语义单元测试 (simulation-test-quality-hardening §7)。
/// 直接构造 ExpectedBehavior + TraversalResult, 验证 exact/subset 两路的 guard 行为:
/// 精确 set-diff (missed/extra)、精确等值匹配 (非子串)、allowedMisses 豁免、
/// subset 过游走 guard、MarkAndStop (target_found 不 tap)。
/// 这些是完备性证明的「抓得到故障」回归护栏 —— 永久化 §7 的临时注入验证。
/// </summary>
public class ExpectedBehaviorElementCoverageTests
{
    // ── 构造 helper ──────────────────────────────────────

    private static ExpectedBehavior Build(ElementCoverageExpectation ec) => new(
        Scenario: "test", Description: "test",
        Completion: new CompletionExpectation(Success: true, Reason: TraversalResult.Reasons.AllVisited),
        PageCoverage: new PageCoverageExpectation(ImmutableArray<string>.Empty, ImmutableArray<string>.Empty),
        ElementCoverage: ec,
        CollisionProof: ImmutableArray<CollisionProof>.Empty,
        DfsProperties: new DfsPropertiesExpectation(RootFirst: false, ParentBeforeChild: false, BackAfterForward: false),
        NumericAnchor: new NumericAnchor(TotalSteps: 0, VisitedPagesCount: 0, ActionHistoryCount: 0, ElapsedSecondsMax: 9999));

    /// <summary>构造 TraversalResult, ActionHistory 为给定 element_id 序列的 tap (成功)。</summary>
    private static TraversalResult ResultWithTaps(string completionReason, params string[] tappedIds)
    {
        var history = tappedIds.Select(id => new ActionRecord(
            Action: "tap",
            Timestamp: DateTimeOffset.UtcNow,
            Parameters: new Dictionary<string, object> { ["element_id"] = id },
            Success: true)).ToImmutableArray();
        return new TraversalResult(
            Success: true, CompletionReason: completionReason,
            TotalSteps: tappedIds.Length, ElapsedSeconds: 0.0,
            ActionHistory: history,
            VisitedPages: ImmutableArray<string>.Empty,
            Trace: ImmutableArray<TraceRecord>.Empty,
            TraceId: null,
            FinalState: TraversalState.FrameComplete);
    }

    private static RuleResult EcRule(VerificationReport report)
        => report.Details.Single(r => r.RuleId == "element_coverage:completeness");

    private static ElementCoverageExpectation Exact(
        string[] required, ElementMiss[]? allowedMisses = null)
        => new(required.ToImmutableArray(),
            Mode: ElementCoverageMode.Exact,
            AllowedMisses: (allowedMisses ?? Array.Empty<ElementMiss>()).ToImmutableArray());

    // ── Exact: 精确 set-diff (D-4, D-7) ──────────────────

    /// <summary>§7.1 等价: 缺失 required 元素 → FAIL, 精确列出 missed (非百分比)。</summary>
    [Fact]
    public void Exact_MissingRequired_FailsEnumeratingMissed()
    {
        var eb = Build(Exact(new[] { "Network_0", "Network_1", "wifi_switch" }));
        var result = ResultWithTaps(TraversalResult.Reasons.AllVisited, "Network_0", "wifi_switch"); // 缺 Network_1

        var rule = EcRule(eb.Verify(result));

        Assert.False(rule.Passed);
        Assert.Contains("missed", rule.Message);
        Assert.Contains("Network_1", rule.Actual);
        Assert.DoesNotContain("Network_17", rule.Actual); // 未参与
    }

    /// <summary>§7.2: 幽灵 tap (全集外元素) → FAIL, 精确列出 extra。</summary>
    [Fact]
    public void Exact_PhantomTap_FailsEnumeratingExtra()
    {
        var eb = Build(Exact(new[] { "wifi_switch" }));
        var result = ResultWithTaps(TraversalResult.Reasons.AllVisited, "wifi_switch", "ghost_btn");

        var rule = EcRule(eb.Verify(result));

        Assert.False(rule.Passed);
        Assert.Contains("extra", rule.Message);
        Assert.Contains("ghost_btn", rule.Actual);
    }

    /// <summary>exact 全部命中且无 extra → PASS。</summary>
    [Fact]
    public void Exact_AllTappedNoExtra_Passes()
    {
        var eb = Build(Exact(new[] { "Network_0", "wifi_switch" }));
        var result = ResultWithTaps(TraversalResult.Reasons.AllVisited, "wifi_switch", "Network_0");

        Assert.True(EcRule(eb.Verify(result)).Passed);
    }

    /// <summary>missed 全在 AllowedMisses 内 → PASS (显式豁免, 非 ratio 放宽)。</summary>
    [Fact]
    public void Exact_MissedWithinAllowedMisses_Passes()
    {
        var eb = Build(Exact(
            new[] { "A", "B", "C", "D" },
            new[] { new ElementMiss("C", "duplicate-dedup at scroll boundary"),
                    new ElementMiss("D", "popup-blocked") }));
        var result = ResultWithTaps(TraversalResult.Reasons.AllVisited, "A", "B"); // 缺 C, D

        Assert.True(EcRule(eb.Verify(result)).Passed);
    }

    /// <summary>D-7 精确等值: "Network_1" 不得被子串匹配 "Network_17"。tap 了 17 但没 1 → missed=["Network_1"]。</summary>
    [Fact]
    public void Exact_SubstringDoesNotCountAsMatch_FailsWithMissed()
    {
        var eb = Build(Exact(new[] { "Network_1" }));
        var result = ResultWithTaps(TraversalResult.Reasons.AllVisited, "Network_17"); // 子串假匹配

        var rule = EcRule(eb.Verify(result));

        Assert.False(rule.Passed);
        Assert.Contains("Network_1", rule.Actual);       // 仍判为 missed
        Assert.Contains("Network_17", rule.Actual);      // 且作为 extra
    }

    // ── Subset: 过游走 guard (D-6) + MarkAndStop ─────────

    /// <summary>§7.3: TargetFound 命中 target 后又 tap 新元素 → FAIL (过游走)。</summary>
    [Fact]
    public void Subset_PostTargetTap_FailsOverTraversal()
    {
        var eb = Build(new ElementCoverageExpectation(
            Required: ImmutableArray<string>.Empty,
            Mode: ElementCoverageMode.Subset,
            TargetName: "App15"));
        // tap App_15 (target) 后又 tap App_22
        var result = ResultWithTaps(TraversalResult.Reasons.AllVisited, "App_15", "App_22");

        var rule = EcRule(eb.Verify(result));

        Assert.False(rule.Passed);
        Assert.Contains("App_22", rule.Message);
    }

    /// <summary>subset: target 命中后只 back/scroll (无新元素 tap) → PASS。</summary>
    [Fact]
    public void Subset_NoNewTapAfterTarget_Passes()
    {
        var eb = Build(new ElementCoverageExpectation(
            Required: ImmutableArray<string>.Empty,
            Mode: ElementCoverageMode.Subset,
            TargetName: "App15"));
        var result = ResultWithTaps(TraversalResult.Reasons.AllVisited, "App_15");

        Assert.True(EcRule(eb.Verify(result)).Passed);
    }

    /// <summary>MarkAndStop: target 经分析命中但未 tap (engine halt) + completion=target_found → PASS (无过游走可能)。</summary>
    [Fact]
    public void Subset_MarkAndStopTargetFound_Passes()
    {
        var eb = Build(new ElementCoverageExpectation(
            Required: ImmutableArray<string>.Empty,
            Mode: ElementCoverageMode.Subset,
            TargetName: "Dark mode"));
        // 未 tap target, 但 completion 确认 target_found (MarkAndStop)
        var result = ResultWithTaps(TraversalResult.Reasons.TargetFound, "menu_wifi", "menu_bluetooth");

        Assert.True(EcRule(eb.Verify(result)).Passed);
    }
}
