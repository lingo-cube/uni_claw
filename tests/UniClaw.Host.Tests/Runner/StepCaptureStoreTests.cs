using UniClaw.Core.Traversal;
using UniClaw.Host.Runner;
using Xunit;

namespace UniClaw.Host.Tests.Runner;

public sealed class StepCaptureStoreTests
{
    private static ScreenStateResult State(string fingerprint = "fp-1") =>
        new(
            Succeeded: true,
            Status: "ok",
            HierarchyXml: "<hierarchy />",
            HierarchyFingerprint: fingerprint,
            HasScroll: false,
            IsEndOfList: false,
            Failure: null);

    [Fact]
    public void TryGetBefore_AfterSet_ReturnsSameState()
    {
        var store = new StepCaptureStore();
        var state = State();

        store.SetBefore(state);

        Assert.True(store.TryGetBefore(out var actual));
        Assert.Same(state, actual);
    }

    [Fact]
    public void TryGetBefore_BeforeAnySet_ReturnsFalse()
    {
        var store = new StepCaptureStore();

        Assert.False(store.TryGetBefore(out var state));
        Assert.Null(state);
    }

    [Fact]
    public void Invalidate_MarksStoredStateStale()
    {
        var store = new StepCaptureStore();
        store.SetBefore(State());

        store.Invalidate();

        Assert.False(store.TryGetBefore(out _));
    }

    [Fact]
    public void SetBefore_OverwritesPreviousStep()
    {
        var store = new StepCaptureStore();
        store.SetBefore(State("fp-old"));

        store.SetBefore(State("fp-new"));

        Assert.True(store.TryGetBefore(out var actual));
        Assert.Equal("fp-new", actual!.HierarchyFingerprint);
    }
}
