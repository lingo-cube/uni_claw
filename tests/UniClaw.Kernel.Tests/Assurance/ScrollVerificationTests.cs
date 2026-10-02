using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.Assurance;

/// <summary>
/// AGT-005 — swipe（滚动）步骤的 post-action 验证语义：
/// 通过 = 动作前目标唯一 ∧ 路由未变 ∧ 可见内容变化。
/// 路由变化（滚动误触导航）与内容未变（滚动未生效）都 fail closed。
/// </summary>
public sealed class ScrollVerificationTests
{
    private static readonly TargetSpec SwipeTarget =
        new("scrollable", "com.android.settings:id/main_content", "swipe-up");

    private static RuntimeAssurance NewAssurance() => new(
        new ProductFreshnessEvaluator(() => DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5)));

    private static Slice EmptySlice(string revision = "rev-1") => new(
        revision, "container-root",
        new FreshnessBasis(DateTimeOffset.UtcNow),
        new[] { "container-root" },
        new[]
        {
            new OccurrenceFact(
                "occ-scroll-1", "container-root", "scrollable",
                "com.android.settings:id/main_content"),
        },
        new Dictionary<string, string>());

    private static IReadOnlyList<KernelResult> AcceptedObservation() => new[]
    {
        new KernelResult(
            new AdmissionRecord(AdmissionDecision.Accepted, Array.Empty<AdmissionCheck>(),
                "ev-scroll-1", null),
            Relevance: null,
            ResultingRevision: new WorldBeliefRevision(
                "rev-2", "rev-1", 2,
                new Dictionary<string, WorldClaim>(),
                Array.Empty<string>(),
                new HashSet<string> { "ev-scroll-1" },
                new FreshnessBasis(DateTimeOffset.UtcNow),
                new Uncertainty(0),
                Array.Empty<Conflict>())),
    };

    [Fact]
    public void Swipe_RouteUnchanged_ContentChanged_Verifies()
    {
        var assurance = NewAssurance();
        var judgment = assurance.VerifyPostActionEffect(new PostActionEffectVerificationInput(
            SwipeTarget, EmptySlice("rev-2"), AcceptedObservation(),
            PriorRoute: "android.settings|route:Settings",
            CurrentRoute: "android.settings|route:Settings",
            ScrollContentChanged: true));

        Assert.True(judgment.IsVerified, judgment.RejectionReason);
        Assert.Contains(judgment.Checks, c => c.Name == "post-action-route-unchanged" && c.Passed);
        Assert.Contains(judgment.Checks, c => c.Name == "post-action-content-transition" && c.Passed);
    }

    [Fact]
    public void Swipe_RouteChanged_FailsClosed()
    {
        var assurance = NewAssurance();
        var judgment = assurance.VerifyPostActionEffect(new PostActionEffectVerificationInput(
            SwipeTarget, EmptySlice("rev-2"), AcceptedObservation(),
            PriorRoute: "android.settings|route:Settings",
            CurrentRoute: "android.settings|route:Internet",
            ScrollContentChanged: true));

        Assert.False(judgment.IsVerified);
        Assert.Equal("post-action-route-unchanged", judgment.RejectionReason);
    }

    [Fact]
    public void Swipe_ContentUnchanged_FailsClosed()
    {
        var assurance = NewAssurance();
        var judgment = assurance.VerifyPostActionEffect(new PostActionEffectVerificationInput(
            SwipeTarget, EmptySlice("rev-2"), AcceptedObservation(),
            PriorRoute: "android.settings|route:Settings",
            CurrentRoute: "android.settings|route:Settings",
            ScrollContentChanged: false));

        Assert.False(judgment.IsVerified);
        Assert.Equal("post-action-content-transition", judgment.RejectionReason);
    }

    [Fact]
    public void Swipe_MissingRouteEvidence_FailsClosed()
    {
        var assurance = NewAssurance();
        var judgment = assurance.VerifyPostActionEffect(new PostActionEffectVerificationInput(
            SwipeTarget, EmptySlice("rev-2"), AcceptedObservation(),
            PriorRoute: null,
            CurrentRoute: "android.settings|route:Settings",
            ScrollContentChanged: true));

        Assert.False(judgment.IsVerified);
        Assert.Equal("post-action-route-unchanged", judgment.RejectionReason);
    }

    [Fact]
    public void Swipe_ContentEvidenceMissing_FailsClosed()
    {
        var assurance = NewAssurance();
        var judgment = assurance.VerifyPostActionEffect(new PostActionEffectVerificationInput(
            SwipeTarget, EmptySlice("rev-2"), AcceptedObservation(),
            PriorRoute: "android.settings|route:Settings",
            CurrentRoute: "android.settings|route:Settings",
            ScrollContentChanged: null));

        Assert.False(judgment.IsVerified);
        Assert.Equal("post-action-content-transition", judgment.RejectionReason);
    }

    [Fact]
    public void Tap_DoesNotIncurScrollChecks()
    {
        var assurance = NewAssurance();
        var tapTarget = new TargetSpec("ui.element", "Network & internet", "tap");
        var tapSlice = new Slice(
            "rev-2", "container-root",
            new FreshnessBasis(DateTimeOffset.UtcNow),
            new[] { "container-root" },
            new[]
            {
                new OccurrenceFact(
                    "occ-tap-1", "container-root", "ui.element", "Network & internet"),
            },
            new Dictionary<string, string>());
        var judgment = assurance.VerifyPostActionEffect(new PostActionEffectVerificationInput(
            tapTarget, tapSlice, AcceptedObservation()));

        Assert.True(judgment.IsVerified, judgment.RejectionReason);
        Assert.DoesNotContain(judgment.Checks, c => c.Name.StartsWith("post-action-route-", StringComparison.Ordinal)
            || c.Name.StartsWith("post-action-content-", StringComparison.Ordinal));
    }
}
