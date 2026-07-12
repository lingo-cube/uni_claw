using System.Collections.Immutable;
using UniClaw.Core.Simulation.Scroll;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Content;
using Coordinate = UniClaw.Core.Domain.Models.Content.Coordinate;
using Xunit;

namespace UniClaw.Core.Tests.Simulation.Scroll;

public class ScrollDataModelTests
{
    #region ScrollSegment Tests

    [Fact]
    public void ScrollSegment_Creation_ValidThreshold_Succeeds()
    {
        var elements = ImmutableArray.Create(
            new MenuItem("Item1", new Coordinate(0.5, 0.5)),
            new MenuItem("Item2", new Coordinate(0.5, 0.6))
        );

        var segment = new ScrollSegment(0.5, elements);

        Assert.Equal(0.5, segment.Threshold);
        Assert.Equal(2, segment.Elements.Length);
    }

    [Fact]
    public void ScrollSegment_ThresholdBelowZero_ThrowsDomainValidationException()
    {
        var elements = ImmutableArray<MenuItem>.Empty;
        Assert.Throws<DomainValidationException>(() => new ScrollSegment(-0.1, elements));
    }

    [Fact]
    public void ScrollSegment_ThresholdAboveOne_ThrowsDomainValidationException()
    {
        var elements = ImmutableArray<MenuItem>.Empty;
        Assert.Throws<DomainValidationException>(() => new ScrollSegment(1.1, elements));
    }

    [Fact]
    public void ScrollSegment_Empty_CreatesEmptySegment()
    {
        var segment = ScrollSegment.Empty(0.5);
        Assert.Equal(0.5, segment.Threshold);
        Assert.True(segment.Elements.IsEmpty);
    }

    #endregion

    #region ScrollState Tests

    [Fact]
    public void ScrollState_Initial_HasZeroProgressAndCount()
    {
        var state = ScrollState.Initial();

        Assert.Equal(0.0, state.CurrentProgress);
        Assert.Equal(0, state.ScrollCount);
        Assert.True(state.ScrollHistory.IsEmpty);
    }

    [Fact]
    public void ScrollState_ApplyDelta_IncreasesProgressAndCount()
    {
        var state = ScrollState.Initial();
        var newState = state.ApplyDelta(0.3);

        Assert.Equal(0.3, newState.CurrentProgress);
        Assert.Equal(1, newState.ScrollCount);
        Assert.Single(newState.ScrollHistory);
        Assert.Equal(0.0, newState.ScrollHistory[0]);
    }

    [Fact]
    public void ScrollState_ApplyDelta_ClampsAboveOne()
    {
        var state = ScrollState.Initial();
        var newState = state.ApplyDelta(1.5);

        Assert.Equal(1.0, newState.CurrentProgress);
    }

    [Fact]
    public void ScrollState_ApplyNegativeDelta_ClampsBelowZero()
    {
        var state = new ScrollState(CurrentProgress: 0.3);
        var newState = state.ApplyDelta(-0.5);

        Assert.Equal(0.0, newState.CurrentProgress);
    }

    [Fact]
    public void ScrollState_SetProgress_SetsProgressDirectly()
    {
        var state = ScrollState.Initial();
        var newState = state.SetProgress(0.7);

        Assert.Equal(0.7, newState.CurrentProgress);
    }

    #endregion

    #region ScrollDataStore Tests

    [Fact]
    public void ScrollDataStore_Empty_ReturnsEmptySegmentsForAnyPage()
    {
        var store = ScrollDataStore.Empty();
        var segments = store.GetSegments("non_existent_page");

        Assert.True(segments.IsEmpty);
    }

    [Fact]
    public void ScrollDataStore_AddSegments_StoresSegments()
    {
        var store = ScrollDataStore.Empty();
        var segments = ImmutableArray.Create(
            ScrollSegment.Empty(0.0),
            ScrollSegment.Empty(0.5)
        );

        store = store.AddSegments("test_page", segments);

        var retrieved = store.GetSegments("test_page");
        Assert.Equal(2, retrieved.Length);
    }

    [Fact]
    public void ScrollDataStore_HasScrollData_ReturnsTrueForExistingPage()
    {
        var store = ScrollDataStore.Empty();
        var segments = ImmutableArray.Create(ScrollSegment.Empty(0.0));

        store = store.AddSegments("test_page", segments);

        Assert.True(store.HasScrollData("test_page"));
    }

    [Fact]
    public void ScrollDataStore_GetMaxThreshold_ReturnsMaximumThreshold()
    {
        var store = ScrollDataStore.Empty();
        var segments = ImmutableArray.Create(
            ScrollSegment.Empty(0.0),
            ScrollSegment.Empty(0.5),
            ScrollSegment.Empty(1.0)
        );

        store = store.AddSegments("test_page", segments);

        Assert.Equal(1.0, store.GetMaxThreshold("test_page"));
    }

    [Fact]
    public void ScrollDataStore_Builder_BuildsStore()
    {
        var store = ScrollDataStore.CreateBuilder()
            .Add("page1", ScrollSegment.Empty(0.0), ScrollSegment.Empty(0.5))
            .Add("page2", ScrollSegment.Empty(0.0), ScrollSegment.Empty(1.0))
            .Build();

        Assert.True(store.HasScrollData("page1"));
        Assert.True(store.HasScrollData("page2"));
        Assert.Equal(2, store.GetSegments("page1").Length);
    }

    #endregion

    #region OverlapStatus Tests

    [Fact]
    public void OverlapStatus_HasOverlap_WhenBothSetsShareElements()
    {
        var before = ImmutableArray.Create("A", "B", "C");
        var after = ImmutableArray.Create("C", "D", "E");

        var result = ScrollVerifyResult.Compute(before, after);

        Assert.Equal(OverlapStatus.HasOverlap, result.Status);
        Assert.Equal(1, result.OverlapCount);
        Assert.Equal(2, result.NewElementCount);
    }

    [Fact]
    public void OverlapStatus_NoOverlapBothHaveElements_WhenNoSharedElements()
    {
        var before = ImmutableArray.Create("A", "B");
        var after = ImmutableArray.Create("C", "D");

        var result = ScrollVerifyResult.Compute(before, after);

        Assert.Equal(OverlapStatus.NoOverlap_BothHaveElements, result.Status);
        Assert.True(result.IsJumpDetected);
    }

    [Fact]
    public void OverlapStatus_NoOverlapBeforeEmpty_WhenBeforeIsEmpty()
    {
        var before = ImmutableArray<string>.Empty;
        var after = ImmutableArray.Create("A", "B");

        var result = ScrollVerifyResult.Compute(before, after);

        Assert.Equal(OverlapStatus.NoOverlap_BeforeEmpty, result.Status);
        Assert.False(result.IsJumpDetected);
    }

    [Fact]
    public void OverlapStatus_NoOverlapAfterEmpty_WhenAfterIsEmpty()
    {
        var before = ImmutableArray.Create("A", "B");
        var after = ImmutableArray<string>.Empty;

        var result = ScrollVerifyResult.Compute(before, after);

        Assert.Equal(OverlapStatus.NoOverlap_AfterEmpty, result.Status);
    }

    [Fact]
    public void OverlapStatus_BothEmpty_WhenBothAreEmpty()
    {
        var before = ImmutableArray<string>.Empty;
        var after = ImmutableArray<string>.Empty;

        var result = ScrollVerifyResult.Compute(before, after);

        Assert.Equal(OverlapStatus.BothEmpty, result.Status);
    }

    #endregion

    #region ScrollHandlerConfig Tests

    [Fact]
    public void ScrollHandlerConfig_Default_HasCorrectDefaults()
    {
        var config = ScrollHandlerConfig.Default();

        Assert.Equal(0.3, config.DefaultScrollStep);
        Assert.Equal(0.01, config.MinScrollStep);
        Assert.Equal(0.5, config.MaxScrollStep);
        Assert.Equal(3, config.MaxJumpRetryCount);
        Assert.Equal(0.5, config.JumpRecoveryFactor);
        Assert.Equal(0.001, config.ProgressEpsilon);
        Assert.True(config.EnableAdaptiveStep);
    }

    [Fact]
    public void ScrollHandlerConfig_InvalidDefaultStep_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => new ScrollHandlerConfig(DefaultScrollStep: 1.5));
    }

    [Fact]
    public void ScrollHandlerConfig_MinGreaterThanMax_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() =>
            new ScrollHandlerConfig(MinScrollStep: 0.5, MaxScrollStep: 0.3));
    }

    #endregion

    #region ScrollContext Tests

    [Fact]
    public void ScrollContext_NoScroll_ReturnsNoScrollContext()
    {
        var context = ScrollContext.NoScroll();

        Assert.Equal(ScrollActionType.None, context.ActionType);
        Assert.Equal(0.0, context.StepPercent);
        Assert.False(context.HasScroll);
    }

    [Fact]
    public void ScrollContext_ScrollDown_ReturnsCorrectContext()
    {
        var context = ScrollContext.ScrollDown(0.3, 0.0, 1.0);

        Assert.Equal(ScrollActionType.ScrollDown, context.ActionType);
        Assert.Equal(0.3, context.StepPercent);
        Assert.True(context.HasScroll);
    }

    #endregion

    #region ScrollActionResult Tests

    [Fact]
    public void ScrollActionResult_Succeeded_ReturnsSuccessResult()
    {
        var result = ScrollActionResult.Succeeded(ScrollActionType.ScrollDown, 0.3, "Success");

        Assert.True(result.Success);
        Assert.Equal(ScrollActionType.ScrollDown, result.Action);
        Assert.Equal(0.3, result.NewProgress);
    }

    [Fact]
    public void ScrollActionResult_Failed_ReturnsFailureResult()
    {
        var result = ScrollActionResult.Failed(ScrollActionType.ScrollDown, "Error");

        Assert.False(result.Success);
        Assert.Contains("Error", result.Description);
    }

    [Fact]
    public void ScrollActionResult_Skipped_ReturnsSkipResult()
    {
        var result = ScrollActionResult.Skipped("At bottom");

        Assert.True(result.Success);
        Assert.Equal(ScrollActionType.None, result.Action);
        Assert.Contains("skipped", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
