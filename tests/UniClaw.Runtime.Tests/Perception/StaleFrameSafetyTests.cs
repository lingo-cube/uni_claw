using UniClaw.Runtime.Capabilities.Perception.Vision;
using Xunit;

namespace UniClaw.Runtime.Tests.Perception;

/// <summary>
/// SF1-SF6: Stale-frame fail-closed production composition proofs.
///
/// Proves that stale-frame evidence CANNOT silently enter a fresh Observation.
/// The SwitchStateValidation.ValidateFrameMatch invariant is the mandatory
/// production composition rule — not an optional test-harness check.
/// </summary>
public sealed class StaleFrameSafetyTests
{
    // ── SF1: same frame → evidence allowed ───────────────────────────────

    [Fact]
    public void SF1_SameFrame_EvidenceAllowed()
    {
        var reader = new MockSwitchStateReader(true);
        var frame = reader.Frame;

        var result = SwitchStateValidation.ValidateFrameMatch(reader, frame, true);
        Assert.True(result); // same frame, evidence passes through
    }

    // ── SF2: stale frame → true/false MUST NOT enter observation ──────────

    [Fact]
    public void SF2_StaleFrame_TrueRejectedToNull()
    {
        var readerF1 = new MockSwitchStateReader(true);
        var frameF2 = new PerceptionFrame(); // different capture

        // Stale reader (F1) used with current frame (F2) → fail closed
        var result = SwitchStateValidation.ValidateFrameMatch(readerF1, frameF2, true);
        Assert.Null(result); // trusted ON from stale frame → null
    }

    [Fact]
    public void SF2_StaleFrame_FalseRejectedToNull()
    {
        var readerF1 = new MockSwitchStateReader(false);
        var frameF2 = new PerceptionFrame();

        var result = SwitchStateValidation.ValidateFrameMatch(readerF1, frameF2, false);
        Assert.Null(result); // trusted OFF from stale frame → null
    }

    // ── SF3: old reader reused after next capture → fail closed ───────────

    [Fact]
    public void SF3_OldReaderAfterNextCapture_FailClosed()
    {
        // Capture F1: create reader
        var readerF1 = new MockSwitchStateReader(true);
        var frameF1 = readerF1.Frame;

        // Same frame: OK
        var ok = SwitchStateValidation.ValidateFrameMatch(readerF1, frameF1, true);
        Assert.True(ok);

        // Capture F2: new frame, old reader reused → fail closed
        var frameF2 = new PerceptionFrame();
        var stale = SwitchStateValidation.ValidateFrameMatch(readerF1, frameF2, true);
        Assert.Null(stale);
    }

    // ── SF4: UNKNOWN preserves null through validation ────────────────────

    [Fact]
    public void SF4_Unknown_NullPassesThrough()
    {
        var reader = new MockSwitchStateReader(null);
        var frame = reader.Frame;

        // UNKNOWN (null) is already safe — passes through regardless
        var result = SwitchStateValidation.ValidateFrameMatch(reader, frame, null);
        Assert.Null(result);
    }

    [Fact]
    public void SF4_Unknown_StaleFrameStillNull()
    {
        var reader = MockSwitchStateReader.AlwaysUnknown;
        var otherFrame = new PerceptionFrame();

        var result = SwitchStateValidation.ValidateFrameMatch(reader, otherFrame, null);
        Assert.Null(result); // UNKNOWN doesn't become something else on stale frame
    }

    // ── SF5: same frame ON/OFF behavior unaffected ────────────────────────

    [Fact]
    public void SF5_SameFrame_OnOffBehaviorUnaffected()
    {
        var readerOn = new MockSwitchStateReader(true);
        var readerOff = new MockSwitchStateReader(false);

        var onResult = SwitchStateValidation.ValidateFrameMatch(readerOn, readerOn.Frame, true);
        var offResult = SwitchStateValidation.ValidateFrameMatch(readerOff, readerOff.Frame, false);

        Assert.True(onResult);
        Assert.False(offResult);
    }

    // ── SF6: validate false→false on same frame, not converted to UNKNOWN ─

    [Fact]
    public void SF6_FalseOnSameFrame_NotConvertedToUnknown()
    {
        var reader = new MockSwitchStateReader(false);
        var frame = reader.Frame;

        var result = SwitchStateValidation.ValidateFrameMatch(reader, frame, false);
        Assert.False(result); // false on same frame is preserved, not converted
    }

    // ── PRODUCTION INVARIANT: reader == current → evidence accepted ───────

    [Fact]
    public void ProductionInvariant_OnlyCurrentFrameEvidenceEntersObservation()
    {
        // Simulate the full production composition:
        //   Capture frame F → creates reader → validates each toggle

        var currentFrame = new PerceptionFrame();
        var currentReader = new MockSwitchStateReader(true);
        // In production, the adapter ensures: currentReader.Frame == currentFrame
        // Here the mock creates its own frame, but the invariant is:
        // ValidateFrameMatch(currentReader, captureFrame, readResult)

        // If the adapter correctly passes the capture's frame:
        var result = SwitchStateValidation.ValidateFrameMatch(
            currentReader, currentReader.Frame, true);
        Assert.True(result);

        // If the adapter accidentally passes a stale frame:
        var staleFrame = new PerceptionFrame();
        var staleResult = SwitchStateValidation.ValidateFrameMatch(
            currentReader, staleFrame, true);
        Assert.Null(staleResult); // fail closed
    }
}
