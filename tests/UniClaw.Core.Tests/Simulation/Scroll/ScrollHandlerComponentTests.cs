using System.Collections.Immutable;
using UniClaw.Core.Simulation.Scroll;
using UniClaw.Core.StateMachine.Scroll;
using Xunit;

namespace UniClaw.Core.Tests.Simulation.Scroll;

public class ScrollHandlerComponentTests
{
    #region ScrollabilityDetector Tests

    [Fact]
    public void ScrollabilityDetector_NotScrollable_WhenNoScrollData()
    {
        var config = ScrollHandlerConfig.Default();
        var result = ScrollabilityDetector.Detect(false, false, 0.0, config);

        Assert.Equal(Scrollability.NotScrollable, result);
    }

    [Fact]
    public void ScrollabilityDetector_CanScrollDown_WhenHasScrollDataAndNotAtBottom()
    {
        var config = ScrollHandlerConfig.Default();
        var result = ScrollabilityDetector.Detect(true, false, 0.3, config);

        Assert.Equal(Scrollability.CanScrollDown, result);
    }

    [Fact]
    public void ScrollabilityDetector_AtBottom_WhenIsEndOfList()
    {
        var config = ScrollHandlerConfig.Default();
        var result = ScrollabilityDetector.Detect(true, true, 0.9, config);

        Assert.Equal(Scrollability.AtBottom, result);
    }

    #endregion

    #region ScrollClassifier Tests

    [Fact]
    public void ScrollClassifier_Classify_ReturnsCorrectClassification()
    {
        var config = ScrollHandlerConfig.Default();
        var result = ScrollClassifier.Classify(0.3, 1.0, config);

        Assert.Equal(0.3, result.CurrentProgress);
        Assert.Equal(1.0, result.MaxProgress);
        Assert.Equal(0.3, result.RecommendedStep);
        Assert.Equal(0.7, result.RemainingDistance);
    }

    [Fact]
    public void ScrollClassifier_CalculateSafeStep_ClampsToRemainingDistance()
    {
        var safeStep = ScrollClassifier.CalculateSafeStep(0.5, 0.3);

        Assert.Equal(0.3, safeStep);
    }

    [Fact]
    public void ScrollClassifier_IsAtBottom_WithEpsilon_ReturnsTrue()
    {
        var result = ScrollClassifier.IsAtBottom(0.9995, 1.0, 0.001);

        Assert.True(result);
    }

    #endregion

    #region ScrollDecider Tests

    [Fact]
    public void ScrollDecider_Decide_CanScrollDown_ReturnsScrollDown()
    {
        var actionType = ScrollDecider.Decide(Scrollability.CanScrollDown);

        Assert.Equal(ScrollActionType.ScrollDown, actionType);
    }

    [Fact]
    public void ScrollDecider_Decide_AtBottom_ReturnsNone()
    {
        var actionType = ScrollDecider.Decide(Scrollability.AtBottom);

        Assert.Equal(ScrollActionType.None, actionType);
    }

    [Fact]
    public void ScrollDecider_Decide_NotScrollable_ReturnsNone()
    {
        var actionType = ScrollDecider.Decide(Scrollability.NotScrollable);

        Assert.Equal(ScrollActionType.None, actionType);
    }

    [Fact]
    public void ScrollDecider_ShouldScroll_ReturnsTrueForScrollActions()
    {
        Assert.True(ScrollDecider.ShouldScroll(ScrollActionType.ScrollDown));
        Assert.True(ScrollDecider.ShouldScroll(ScrollActionType.ScrollUp));
        Assert.False(ScrollDecider.ShouldScroll(ScrollActionType.None));
    }

    #endregion

    #region JumpDetector Tests

    [Fact]
    public void JumpDetector_HasOverlap_ReturnsHasOverlapStatus()
    {
        var before = ImmutableArray.Create("A", "B", "C");
        var after = ImmutableArray.Create("C", "D", "E");

        var result = JumpDetector.Detect(before, after);

        Assert.Equal(OverlapStatus.HasOverlap, result.Status);
        Assert.False(JumpDetector.IsJumpDetected(result));
    }

    [Fact]
    public void JumpDetector_NoOverlapBothHaveElements_ReturnsJumpDetected()
    {
        var before = ImmutableArray.Create("A", "B");
        var after = ImmutableArray.Create("C", "D");

        var result = JumpDetector.Detect(before, after);

        Assert.Equal(OverlapStatus.NoOverlap_BothHaveElements, result.Status);
        Assert.True(JumpDetector.IsJumpDetected(result));
    }

    [Fact]
    public void JumpDetector_BeforeEmpty_ReturnsSafeInitialState()
    {
        var before = ImmutableArray<string>.Empty;
        var after = ImmutableArray.Create("A", "B");

        var result = JumpDetector.Detect(before, after);

        Assert.Equal(OverlapStatus.NoOverlap_BeforeEmpty, result.Status);
        Assert.True(JumpDetector.IsSafeInitialState(result));
    }

    #endregion

    #region AdaptiveStepCalculator Tests

    [Fact]
    public void AdaptiveStepCalculator_CalculateNextStep_IncreasesWhenHighDuplicateRatio()
    {
        var config = new ScrollHandlerConfig(
            DefaultScrollStep: 0.3,
            AdaptiveStepIncreaseThreshold: 0.7,
            MinSampleSize: 3);

        var verifyResult = new ScrollVerifyResult(
            Status: OverlapStatus.HasOverlap,
            BeforeElementIds: ImmutableArray.Create("A", "B", "C"),
            AfterElementIds: ImmutableArray.Create("A", "B", "C", "D"),
            OverlapCount: 3,
            NewElementCount: 4,
            DuplicateElementCount: 3,
            DuplicateRatio: 0.75);

        var nextStep = AdaptiveStepCalculator.CalculateNextStep(0.3, verifyResult, config);

        Assert.InRange(nextStep, 0.44, 0.46); // 0.3 * 1.5, with tolerance range
    }

    [Fact]
    public void AdaptiveStepCalculator_CalculateNextStep_KeepsSameWhenLowDuplicateRatio()
    {
        var config = ScrollHandlerConfig.Default();

        var verifyResult = new ScrollVerifyResult(
            Status: OverlapStatus.HasOverlap,
            BeforeElementIds: ImmutableArray.Create("A", "B"),
            AfterElementIds: ImmutableArray.Create("A", "C", "D", "E"),
            OverlapCount: 1,
            NewElementCount: 4,
            DuplicateElementCount: 1,
            DuplicateRatio: 0.25);

        var nextStep = AdaptiveStepCalculator.CalculateNextStep(0.3, verifyResult, config);

        Assert.Equal(0.3, nextStep); // Unchanged
    }

    [Fact]
    public void AdaptiveStepCalculator_CalculateSafeStep_ClampsToRemainingDistance()
    {
        var safeStep = AdaptiveStepCalculator.CalculateSafeStep(0.5, 0.8, 1.0);

        Assert.InRange(safeStep, 0.19, 0.21);
    }

    [Fact]
    public void AdaptiveStepCalculator_Clamp_ClampsToRange()
    {
        var clamped = AdaptiveStepCalculator.Clamp(0.6, 0.1, 0.5);

        Assert.Equal(0.5, clamped);
    }

    #endregion

    #region ScrollStatisticsCollector Tests

    [Fact]
    public void ScrollStatisticsCollector_RecordScroll_IncrementsCountAndDistance()
    {
        var stats = new ScrollStatisticsCollector();

        stats.RecordScroll(0.3, 0.3);

        Assert.Equal(1, stats.ScrolledCount);
        Assert.Equal(0.3, stats.TotalDistance);
        Assert.Single(stats.StepHistory);
    }

    [Fact]
    public void ScrollStatisticsCollector_AverageStep_ReturnsCorrectAverage()
    {
        var stats = new ScrollStatisticsCollector();

        stats.RecordScroll(0.3, 0.3);
        stats.RecordScroll(0.3, 0.3);
        stats.RecordScroll(0.1, 0.1);

        Assert.Equal(0.23333, stats.AverageStep, 5);
    }

    [Fact]
    public void ScrollStatisticsCollector_Reset_ClearsAllStatistics()
    {
        var stats = new ScrollStatisticsCollector();

        stats.RecordScroll(0.3, 0.3);
        stats.Reset();

        Assert.Equal(0, stats.ScrolledCount);
        Assert.Equal(0.0, stats.TotalDistance);
        Assert.Empty(stats.StepHistory);
    }

    #endregion
}
