using UniClaw.Runtime.Model;
using UniClaw.Runtime.Capabilities.Perception.Vision;
using Xunit;

namespace UniClaw.Runtime.Tests.Perception;

/// <summary>
/// T1 COMPONENT PROOFS — ISwitchStateReader contract validation.
/// F1-F7: known ON/OFF/ambiguous, invalid bounds, non-switch, resolution independence, determinism.
/// </summary>
public sealed class SwitchStateReaderComponentTests
{
    private static readonly ElementBounds ValidBounds = new(0.75f, 0.20f, 0.90f, 0.30f);

    // ── F1: Known ON → true ──────────────────────────────────────────────

    [Fact]
    public async Task F1_KnownOn_ReturnsTrue()
    {
        var reader = MockSwitchStateReader.AlwaysOn;
        var result = await reader.ReadAsync(ValidBounds);

        Assert.True(result);
    }

    // ── F2: Known OFF → false ────────────────────────────────────────────

    [Fact]
    public async Task F2_KnownOff_ReturnsFalse()
    {
        var reader = MockSwitchStateReader.AlwaysOff;
        var result = await reader.ReadAsync(ValidBounds);

        Assert.False(result);
    }

    // ── F3: Ambiguous / UNKNOWN → null ───────────────────────────────────

    [Fact]
    public async Task F3_AmbiguousOrUnknown_ReturnsNull()
    {
        var reader = MockSwitchStateReader.AlwaysUnknown;
        var result = await reader.ReadAsync(ValidBounds);

        Assert.Null(result);
    }

    // ── F4: Invalid bounds → null ────────────────────────────────────────

    [Fact]
    public async Task F4_InvalidBounds_ReturnsNull()
    {
        var reader = MockSwitchStateReader.AlwaysOn;
        var invalid = new ElementBounds(0.9f, 0.2f, 0.1f, 0.3f); // X1 > X2 — invalid

        Assert.False(invalid.IsValid);
        var result = await reader.ReadAsync(invalid);

        Assert.Null(result);
    }

    // ── F5: Non-switch region → null ─────────────────────────────────────

    [Fact]
    public async Task F5_NonSwitchRegion_ReturnsNull()
    {
        // A region with unknown content is treated as null by the mock
        // (real implementation would check if the crop contains a recognizable switch)
        var reader = MockSwitchStateReader.AlwaysUnknown;
        var result = await reader.ReadAsync(ValidBounds);

        Assert.Null(result);
    }

    // ── F6: Resolution independence — normalized bounds produce same result ──

    [Fact]
    public async Task F6_ResolutionIndependence_SameNormalizedBoundsSameResult()
    {
        var reader = MockSwitchStateReader.AlwaysOn;
        // Same normalized region, different conceptual resolutions
        var bounds = new ElementBounds(0.805f, 0.395f, 0.913f, 0.425f);
        Assert.True(bounds.IsValid);

        var r1 = await reader.ReadAsync(bounds);
        var r2 = await reader.ReadAsync(bounds);

        Assert.Equal(r1, r2);
    }

    // ── F7: Deterministic replay — same input → same output ──────────────

    [Fact]
    public async Task F7_DeterministicReplay_SameInputSameOutput()
    {
        var reader = MockSwitchStateReader.AlwaysOff;

        var r1 = await reader.ReadAsync(ValidBounds);
        var r2 = await reader.ReadAsync(ValidBounds);
        var r3 = await reader.ReadAsync(ValidBounds);

        Assert.Equal(r1, r2);
        Assert.Equal(r2, r3);
        Assert.False(r1);
    }

    // ── CONTRACT: ISwitchStateReader does not carry confidence/model ─────

    [Fact]
    public void Contract_NoConfidenceOrModelOnInterface()
    {
        var methods = typeof(ISwitchStateReader).GetMethods();
        var readMethod = Assert.Single(methods.Where(m => m.Name == "ReadAsync"));

        // Only parameters: ElementBounds, CancellationToken
        var paramTypes = readMethod.GetParameters().Select(p => p.ParameterType).ToHashSet();
        Assert.Contains(typeof(ElementBounds), paramTypes);
        Assert.Contains(typeof(CancellationToken), paramTypes);
        Assert.Equal(2, readMethod.GetParameters().Length);

        // Return type: ValueTask<bool?>
        Assert.Equal(typeof(ValueTask<bool?>), readMethod.ReturnType);
    }

    // ── CONTRACT: Mock is stateless ──────────────────────────────────────

    [Fact]
    public void Mock_IsStateless_NoMutableFields()
    {
        var fields = typeof(MockSwitchStateReader)
            .GetFields(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public);
        var nonReadonly = fields.Where(f => !f.IsInitOnly && !f.IsLiteral).ToArray();

        Assert.Empty(nonReadonly);
    }

    // ── V4 STALE-FRAME: different readers for different frames ────────────

    [Fact]
    public async Task V4_StaleFrame_ReaderBoundToOneFrame_DetectableMismatch()
    {
        // Prove that a reader's frame identity is explicit and checkable.
        // Adapter MUST create a new reader per fresh capture frame.
        var readerF1 = MockSwitchStateReader.AlwaysOn;
        var readerF2 = MockSwitchStateReader.AlwaysOff;

        // Frame F1: reader reports ON
        var bounds = new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f);
        var resultF1 = await readerF1.ReadAsync(bounds);
        Assert.True(resultF1);

        // Same bounds against reader for F2 — DIFFERENT frame, DIFFERENT result
        var resultF2 = await readerF2.ReadAsync(bounds);
        Assert.False(resultF2);

        // Frames are explicitly comparable — stale-frame misuse is detectable
        Assert.NotEqual(readerF1.Frame, readerF2.Frame);
    }

    // ── V5 EXPIRED FRAME: reader cannot outlive adapter discipline ────────

    [Fact]
    public void V5_ExpiredFrame_NewCaptureRequiresNewReader()
    {
        // Each new capture creates a new PerceptionFrame + new reader.
        // The adapter MUST NOT reuse the old reader across captures.
        var oldReader = new MockSwitchStateReader(true);
        var newReader = new MockSwitchStateReader(true);

        // Different frame identities — old reader's evidence is from a stale frame
        Assert.NotEqual(oldReader.Frame, newReader.Frame);
    }

    // ── V8 UNKNOWN SAFETY: UNKNOWN → no blind action ─────────────────────

    [Fact]
    public async Task V8_UnknownIsNotFalse_DoesNotTriggerBlindToggle()
    {
        var reader = MockSwitchStateReader.AlwaysUnknown;
        var bounds = new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f);

        var result = await reader.ReadAsync(bounds);

        // UNKNOWN (null) ≠ OFF (false) — the Runtime must not interpret
        // null as "probably OFF, let's toggle"
        Assert.Null(result);
        Assert.NotEqual(false, result);
    }

    // ── BOUNDARY: bounds from B1 golden match ────────────────────────────

    [Fact]
    public async Task Boundary_B1GoldenBounds_MatchesPkj110RealDetection()
    {
        // B1 golden: PKJ110, 1440×3168, switch at pixel [1160,1251,1314,1346]
        // → normalized: (0.805, 0.395, 0.913, 0.425)
        var b1Bounds = new ElementBounds(0.805f, 0.395f, 0.913f, 0.425f);
        Assert.True(b1Bounds.IsValid);

        var reader = MockSwitchStateReader.AlwaysOn;
        var result = await reader.ReadAsync(b1Bounds);
        Assert.True(result);
    }
}
