using UniClaw.Kernel.Capability;
using Xunit;

namespace UniClaw.Kernel.Tests.Capability;

public sealed class ExternalArtifactMeasurementContractTests
{
    private static CapabilityCorrelation Correlation(string run = "run-1", string capture = "capture-1", string? receipt = "receipt-1", string? operation = "op-1")
        => new(run, receiptId: receipt, captureId: capture, operationId: operation);

    private static ArtifactReference Artifact(CapabilityCorrelation? correlation = null)
        => new("artifact-1", correlation ?? Correlation(), "camera", "1.0", "sha256:abc", "video/mp4");

    private static MeasurementSample Measurement(CapabilityCorrelation? correlation = null, CapabilityStatus status = CapabilityStatus.Completed)
        => new("sample-1", correlation ?? Correlation(), "sensor", "frame-latency", "ms", "1.0", null, status,
            externalDeviceTime: 200, sensorTime: 300);

    [Fact]
    public void Binding_requires_explicit_matching_correlation()
    {
        var binding = ArtifactMeasurementBinding.Create(Artifact(), Measurement());
        Assert.Equal("capture-1", binding.Artifact.Correlation.CaptureId);
        Assert.Equal(200, binding.Measurement.ExternalDeviceTime);
    }

    [Theory]
    [InlineData("run-2", "capture-1", "receipt-1", "op-1")]
    [InlineData("run-1", "capture-2", "receipt-1", "op-1")]
    [InlineData("run-1", "capture-1", "receipt-2", "op-1")]
    [InlineData("run-1", "capture-1", "receipt-1", "op-2")]
    public void Binding_rejects_different_run_or_capture_receipt_operation(string run, string capture, string receipt, string operation)
    {
        var mismatch = Correlation(run, capture, receipt, operation);
        Assert.Throws<ArgumentException>(() => ArtifactMeasurementBinding.Create(Artifact(), Measurement(mismatch)));
    }

    [Fact]
    public void Binding_rejects_missing_artifact_or_measurement()
    {
        Assert.Throws<ArgumentNullException>(() => ArtifactMeasurementBinding.Create(null!, Measurement()));
        Assert.Throws<ArgumentNullException>(() => ArtifactMeasurementBinding.Create(Artifact(), null!));
    }

    [Theory]
    [InlineData(CapabilityStatus.Late)]
    [InlineData(CapabilityStatus.Duplicate)]
    [InlineData(CapabilityStatus.Unavailable)]
    public void Binding_preserves_external_sample_status(CapabilityStatus status)
    {
        var binding = ArtifactMeasurementBinding.Create(Artifact(), Measurement(status: status));
        Assert.Equal(status, binding.Measurement.Status);
    }

    [Theory]
    [InlineData(FixtureLifecycleState.Setup)]
    [InlineData(FixtureLifecycleState.Ready)]
    [InlineData(FixtureLifecycleState.Failed)]
    [InlineData(FixtureLifecycleState.Teardown)]
    [InlineData(FixtureLifecycleState.Closed)]
    [InlineData(FixtureLifecycleState.Unknown)]
    [InlineData(FixtureLifecycleState.Unavailable)]
    public void Fixture_states_are_independent_facts(FixtureLifecycleState state)
    {
        var fact = new FixtureLifecycleFact("camera-1", "harness", state, "1.0");
        Assert.Equal(state, fact.State);
    }
}
