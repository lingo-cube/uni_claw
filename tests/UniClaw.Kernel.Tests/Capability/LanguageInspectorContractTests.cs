using UniClaw.Kernel.Capability;
using Xunit;

namespace UniClaw.Kernel.Tests.Capability;

public sealed class LanguageInspectorContractTests
{
    private static CapabilityCorrelation Correlation() => new("run-1", captureId: "capture-1", observationCycleId: "cycle-1");
    private static ObservationTextItem Item() => new("occ-1", "ui://screen/button#1", "Continue", "Continue");

    [Fact]
    public void Request_preserves_declared_rendered_occurrence_and_correlation()
    {
        var request = new LanguageInspectorRequest(Correlation(), "rule-1", "en-US", InspectorCoverage.Full, InspectorInputState.Accepted, [Item()]);
        Assert.Equal("Continue", request.Items[0].DeclaredText);
        Assert.Equal("Continue", request.Items[0].RenderedText);
        Assert.Equal("occ-1", request.Items[0].OccurrenceId);
        Assert.Equal("capture-1", request.Correlation.CaptureId);
    }

    [Theory]
    [InlineData(FindingDisposition.Pass, CapabilityStatus.Completed)]
    [InlineData(FindingDisposition.Violation, CapabilityStatus.Completed)]
    [InlineData(FindingDisposition.Unknown, CapabilityStatus.Unknown)]
    [InlineData(FindingDisposition.NotApplicable, CapabilityStatus.Unavailable)]
    public void Request_creates_typed_finding_without_algorithm(FindingDisposition disposition, CapabilityStatus status)
    {
        var finding = new LanguageInspectorRequest(Correlation(), "rule-1", "en-US", InspectorCoverage.Full, InspectorInputState.Accepted, [Item()])
            .CreateFinding("finding-1", "language-inspector", disposition, status, "deterministic fixture result");
        Assert.Equal(disposition, finding.Disposition);
        Assert.Equal(status, finding.Status);
        Assert.Equal("cycle-1", finding.Correlation.ObservationCycleId);
    }

    [Theory]
    [InlineData(InspectorCoverage.Missing, InspectorInputState.MissingInput, FindingDisposition.Unknown, CapabilityStatus.Unknown)]
    [InlineData(InspectorCoverage.Unavailable, InspectorInputState.Unavailable, FindingDisposition.Unknown, CapabilityStatus.Unavailable)]
    [InlineData(InspectorCoverage.Partial, InspectorInputState.Accepted, FindingDisposition.Unknown, CapabilityStatus.Unknown)]
    [InlineData(InspectorCoverage.Mismatch, InspectorInputState.Accepted, FindingDisposition.Violation, CapabilityStatus.Failed)]
    [InlineData(InspectorCoverage.Full, InspectorInputState.Late, FindingDisposition.Unknown, CapabilityStatus.Late)]
    public void Input_coverage_and_lifecycle_are_explicit(InspectorCoverage coverage, InspectorInputState inputState, FindingDisposition disposition, CapabilityStatus status)
    {
        var request = new LanguageInspectorRequest(Correlation(), "rule-1", "en-US", coverage, inputState, coverage == InspectorCoverage.Missing ? null : [Item()]);
        var finding = request.CreateFinding("finding-1", "language-inspector", disposition, status, "input state is explicit");
        Assert.Equal(coverage, request.Coverage);
        Assert.Equal(inputState, request.InputState);
        Assert.Equal(status, finding.Status);
    }

    [Fact]
    public void Text_item_rejects_missing_both_projections()
        => Assert.Throws<ArgumentException>(() => new ObservationTextItem("occ-1", "source-1", null, null));
}
