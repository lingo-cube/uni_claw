using System;
using System.Linq;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests.Assurance;

/// <summary>
/// PROTOCOL-FIX-002（ADR-0049）— ExpectedEffect 期望载荷锚定测试：
/// A3 空谓词集运行期拒绝；两族谓词求值驱动判定（A4：对照物 = 谓词求值，
/// 非身份连续——目标仍在场但期望不满足照样 fail closed）。
/// </summary>
public sealed class ExpectedEffectTests
{
    private static readonly TargetSpec Target =
        new("scrollable", null, "swipe-up");

    private static RuntimeAssurance NewAssurance() => new(
        new ProductFreshnessEvaluator(() => DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5)));

    private static Slice SliceWith(string role, string? state = null) => new(
        "rev-2", "container-root",
        new FreshnessBasis(DateTimeOffset.UtcNow),
        new[] { "container-root" },
        new[] { new OccurrenceFact("occ-1", "container-root", role, null, state) },
        new Dictionary<string, string>());

    private static IReadOnlyList<KernelResult> AcceptedObservation() => new[]
    {
        new KernelResult(
            new AdmissionRecord(AdmissionDecision.Accepted, Array.Empty<AdmissionCheck>(),
                "ev-1", null),
            Relevance: null,
            ResultingRevision: new WorldBeliefRevision(
                "rev-2", "rev-1", 2,
                new Dictionary<string, WorldClaim>(),
                Array.Empty<string>(),
                new HashSet<string> { "ev-1" },
                new FreshnessBasis(DateTimeOffset.UtcNow),
                new Uncertainty(0),
                Array.Empty<Conflict>())),
    };

    [Fact]
    public void EmptyPredicateSet_IsRejected_AtConstruction()
    {
        Assert.Throws<ArgumentException>(() => new ExpectedEffect([], []));
        Assert.Throws<ArgumentException>(() => new ExpectedEffect(
            Array.Empty<StateBecomes>(), Array.Empty<ChangePredicate>()));
        Assert.Throws<ArgumentException>(
            () => ExpectedEffect.OfChanges(Array.Empty<ChangePredicate>()));
    }

    [Fact]
    public void NullPredicateFamilies_AreRejected_AtConstruction()
    {
        Assert.Throws<ArgumentNullException>(() => new ExpectedEffect(null!, []));
        Assert.Throws<ArgumentNullException>(() => new ExpectedEffect([], null!));
    }

    [Fact]
    public void StateBecomes_Present_WithTargetObserved_Verifies()
    {
        var judgment = NewAssurance().VerifyPostActionEffect(new PostActionEffectVerificationInput(
            Target, SliceWith("scrollable"), AcceptedObservation(),
            ExpectedEffect.OfState("scrollable", "present")));

        Assert.True(judgment.IsVerified, judgment.RejectionReason);
        Assert.Contains(judgment.Checks, c => c.Name == "post-action-expected-state" && c.Passed);
    }

    [Fact]
    public void StateBecomes_ClaimSubject_MissingClaim_FailsClosed()
    {
        var judgment = NewAssurance().VerifyPostActionEffect(new PostActionEffectVerificationInput(
            Target, SliceWith("scrollable"), AcceptedObservation(),
            ExpectedEffect.OfState("screen.route", "expected-screen")));

        Assert.False(judgment.IsVerified);
        Assert.Equal("post-action-expected-state", judgment.RejectionReason);
        Assert.Contains(judgment.Checks, c => c.Name == "post-action-target-unique" && c.Passed);
    }

    [Fact]
    public void StateBecomes_ClaimSubject_MatchingClaim_Verifies()
    {
        var slice = new Slice(
            "rev-2", "container-root",
            new FreshnessBasis(DateTimeOffset.UtcNow),
            new[] { "container-root" },
            new[] { new OccurrenceFact("occ-1", "container-root", "scrollable", null) },
            new Dictionary<string, string> { ["screen.route"] = "expected-screen" });
        var judgment = NewAssurance().VerifyPostActionEffect(new PostActionEffectVerificationInput(
            Target, slice, AcceptedObservation(),
            ExpectedEffect.OfState("screen.route", "expected-screen")));

        Assert.True(judgment.IsVerified, judgment.RejectionReason);
    }

    [Fact]
    public void StateBecomes_Present_NavigationTransition_Waived()
    {
        var judgment = NewAssurance().VerifyPostActionEffect(new PostActionEffectVerificationInput(
            Target, SliceWith("other-role"), AcceptedObservation(),
            ExpectedEffect.OfState("scrollable", "present"),
            TargetDisappearedAfterRouteChange: true));

        Assert.True(judgment.IsVerified, judgment.RejectionReason);
    }

    [Fact]
    public void StateBecomes_TypedValue_MatchesOnlyExactCheckedState()
    {
        var assurance = NewAssurance();
        var verified = assurance.VerifyPostActionEffect(new PostActionEffectVerificationInput(
            new TargetSpec("switch", null, "tap", CheckedState.Checked),
            SliceWith("switch", "on"), AcceptedObservation(),
            ExpectedEffect.OfState("switch", "Checked")));
        var mismatched = assurance.VerifyPostActionEffect(new PostActionEffectVerificationInput(
            new TargetSpec("switch", null, "tap", CheckedState.Checked),
            SliceWith("switch", "off"), AcceptedObservation(),
            ExpectedEffect.OfState("switch", "Checked")));

        Assert.True(verified.IsVerified, verified.RejectionReason);
        Assert.False(mismatched.IsVerified);
    }

    /// <summary>
    /// A4 锚定：目标仍在场（身份连续成立），但期望谓词（RouteStable）求值
    /// 不通过 → fail closed。对照物是谓词，不是 prior 染色的身份连续。
    /// </summary>
    [Fact]
    public void IdentityContinuity_DoesNotSubstituteForPredicateEvaluation()
    {
        var judgment = NewAssurance().VerifyPostActionEffect(new PostActionEffectVerificationInput(
            Target, SliceWith("scrollable"), AcceptedObservation(),
            ExpectedEffect.OfChanges(ChangePredicate.RouteStable),
            Change: new ChangeEvidence(
                "android.settings|route:Settings",
                "android.settings|route:Internet",
                VisibleContentChanged: null)));

        Assert.False(judgment.IsVerified);
        Assert.Equal("post-action-route-unchanged", judgment.RejectionReason);
        Assert.Contains(judgment.Checks, c => c.Name == "post-action-target-unique" && c.Passed);
    }

    [Fact]
    public void RouteStable_MissingRouteEvidence_FailsClosed()
    {
        var judgment = NewAssurance().VerifyPostActionEffect(new PostActionEffectVerificationInput(
            Target, SliceWith("scrollable"), AcceptedObservation(),
            ExpectedEffect.OfChanges(ChangePredicate.RouteStable),
            Change: new ChangeEvidence(null, "android.settings|route:Settings", null)));

        Assert.False(judgment.IsVerified);
        Assert.Equal("post-action-route-unchanged", judgment.RejectionReason);
    }

    [Fact]
    public void ContentChanged_EvidenceTrue_Verifies()
    {
        var judgment = NewAssurance().VerifyPostActionEffect(new PostActionEffectVerificationInput(
            Target, SliceWith("scrollable"), AcceptedObservation(),
            ExpectedEffect.OfChanges(ChangePredicate.ContentChanged),
            Change: new ChangeEvidence(null, null, VisibleContentChanged: true)));

        Assert.True(judgment.IsVerified, judgment.RejectionReason);
        Assert.Contains(judgment.Checks, c => c.Name == "post-action-content-transition" && c.Passed);
    }
}
