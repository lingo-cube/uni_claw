using UniClaw.Kernel.Perception.UiHierarchy;
using Xunit;

namespace UniClaw.Kernel.Tests.Perception.UiHierarchy;

/// <summary>
/// PER-013 Gate A / PER-012 fixture M-01–M-03：checked capability 语义与
/// exact-proof 执法（PER-010 §Checked + checkedBooleanExact proof rule）。
/// </summary>
public class CheckedSemanticsTests
{
    private static readonly CheckedExactProof CompleteProof = new(
        TwoStateContractId: "contract:settings.wifi-toggle/two-state",
        AdapterCapabilityDeclaration: "capability:legacy-xml/declares-exact",
        TraceableEvidenceRef: "fixture:two-state-contract/evidence-2026-09");

    // ---- M-01 Exact checked false ----

    [Fact]
    public void M01_ExactCheckedFalse_WithCompleteProof_MapsUnchecked()
    {
        var value = CheckedSemantics.MapBoolean(
            rawChecked: false,
            declaredCapability: CheckedCapability.CheckedBooleanExact,
            exactProof: CompleteProof);

        Assert.Equal(FieldState.Observed, value.State);
        Assert.Equal(CheckedState.Unchecked, value.Value);
        Assert.Null(value.Reason);
    }

    [Fact]
    public void M01_ExactCheckedTrue_MapsChecked()
    {
        var value = CheckedSemantics.MapBoolean(true, CheckedCapability.CheckedBooleanExact, CompleteProof);

        Assert.Equal(FieldState.Observed, value.State);
        Assert.Equal(CheckedState.Checked, value.Value);
    }

    // ---- M-02 Collapsed checked false ----

    [Fact]
    public void M02_CollapsedCheckedFalse_MapsUnknownPartialUnrepresentable_NeverUnchecked()
    {
        var value = CheckedSemantics.MapBoolean(
            rawChecked: false,
            declaredCapability: CheckedCapability.CheckedBooleanCollapsed,
            exactProof: null);

        Assert.Equal(FieldState.Unknown, value.State);
        Assert.Equal(CheckedSemantics.PartialUnrepresentableReason, value.Reason);
        Assert.False(value.TryGetObserved(out _));
        Assert.NotEqual(CheckedState.Unchecked, value.Value);
    }

    [Fact]
    public void M02_CollapsedCheckedTrue_MapsChecked()
    {
        var asTrue = CheckedSemantics.MapBoolean(true, CheckedCapability.CheckedBooleanCollapsed, null);

        Assert.Equal(FieldState.Observed, asTrue.State);
        Assert.Equal(CheckedState.Checked, asTrue.Value);
    }

    [Theory]
    [InlineData("", "capability:decl", "fixture:ev")]
    [InlineData("contract:two-state", "", "fixture:ev")]
    [InlineData("contract:two-state", "capability:decl", "")]
    [InlineData(" ", "capability:decl", "fixture:ev")]
    public void M02_ExactCapabilityWithoutCompleteProof_AlsoUnknown(string contract, string declaration, string evidence)
    {
        var incomplete = new CheckedExactProof(contract, declaration, evidence);
        Assert.False(incomplete.IsComplete);

        var value = CheckedSemantics.MapBoolean(
            rawChecked: false,
            declaredCapability: CheckedCapability.CheckedBooleanExact,
            exactProof: incomplete);

        Assert.Equal(FieldState.Unknown, value.State);
        Assert.Equal(CheckedSemantics.PartialUnrepresentableReason, value.Reason);
        Assert.False(value.TryGetObserved(out _));
    }

    [Fact]
    public void M02_ExactCapabilityWithNullProof_AlsoUnknown()
    {
        var value = CheckedSemantics.MapBoolean(false, CheckedCapability.CheckedBooleanExact, exactProof: null);

        Assert.Equal(FieldState.Unknown, value.State);
        Assert.Equal(CheckedSemantics.PartialUnrepresentableReason, value.Reason);
    }

    // ---- M-03 Tri-state rich source ----

    [Theory]
    [InlineData(CheckedState.Checked)]
    [InlineData(CheckedState.Unchecked)]
    [InlineData(CheckedState.Partial)]
    public void M03_TriState_MapsLosslessly(CheckedState source)
    {
        var value = CheckedSemantics.MapTriState(source);

        Assert.Equal(FieldState.Observed, value.State);
        Assert.Equal(source, value.Value);
        Assert.Null(value.Reason);
    }

    // ---- exact-proof enforcement（PER-010 禁止清单的 API 面执法）----

    [Fact]
    public void ExactProofSatisfied_RequiresExactCapability_AndCompleteProof()
    {
        Assert.True(CheckedSemantics.ExactProofSatisfied(CheckedCapability.CheckedBooleanExact, CompleteProof));
        Assert.False(CheckedSemantics.ExactProofSatisfied(CheckedCapability.CheckedBooleanCollapsed, CompleteProof));
        Assert.False(CheckedSemantics.ExactProofSatisfied(CheckedCapability.CheckedTriState, CompleteProof));
        Assert.False(CheckedSemantics.ExactProofSatisfied(CheckedCapability.CheckedBooleanExact, null));
    }

    [Fact]
    public void MapBoolean_TriStateCapability_IsContractViolation_FailClosed()
    {
        // tri-state 源必须走 MapTriState；boolean 通道无法表达 Partial，parser 不猜值。
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CheckedSemantics.MapBoolean(false, CheckedCapability.CheckedTriState, exactProof: null));
    }

    [Fact]
    public void CheckedApiSurface_Frozen_NoVersionOrClassInferencePath()
    {
        // 公开面冻结：MapBoolean/MapTriState/ExactProofSatisfied 的参数只有
        // (bool, CheckedCapability, CheckedExactProof?, FieldProvenance?) 等语义参数；
        // 不存在 AndroidApiLevel / class / 历史样本参数——Exact 推导无非法通道。
        var mapBoolean = typeof(CheckedSemantics)
            .GetMethod(nameof(CheckedSemantics.MapBoolean))!
            .GetParameters()
            .Select(p => p.ParameterType.Name)
            .ToArray();
        Assert.Equal(new[] { "Boolean", nameof(CheckedCapability), nameof(CheckedExactProof), nameof(FieldProvenance) }, mapBoolean);

        var mapTriState = typeof(CheckedSemantics)
            .GetMethod(nameof(CheckedSemantics.MapTriState))!
            .GetParameters()
            .Select(p => p.ParameterType.Name)
            .ToArray();
        Assert.Equal(new[] { nameof(CheckedState), nameof(FieldProvenance) }, mapTriState);

        var proofProps = typeof(CheckedExactProof).GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(
            new[] { nameof(CheckedExactProof.AdapterCapabilityDeclaration), nameof(CheckedExactProof.IsComplete), nameof(CheckedExactProof.TraceableEvidenceRef), nameof(CheckedExactProof.TwoStateContractId) },
            proofProps);
    }
}
