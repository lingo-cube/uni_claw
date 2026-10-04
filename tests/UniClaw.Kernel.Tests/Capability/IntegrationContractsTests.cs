using UniClaw.Kernel.Capability;
using Xunit;

namespace UniClaw.Kernel.Tests.Capability;

public sealed class IntegrationContractsTests
{
    private static CapabilityCorrelation Correlation() => new("run-1", operationId: "op-1", receiptId: "receipt-1");

    [Fact]
    public void Envelope_distinguishes_point_and_terminal_success()
    {
        var point = new CapabilityEnvelope("event-1", Correlation(), 1, "runtime", "1.0", CapabilityEventKind.PointEvent, CapabilityStatus.Completed);
        var terminal = new CapabilityEnvelope("event-2", Correlation(), 2, "runtime", "1.0", CapabilityEventKind.OperationTerminal, CapabilityStatus.Completed, TimeSpan.FromMilliseconds(12), 44);
        Assert.Equal(CapabilityEventKind.PointEvent, point.EventKind);
        Assert.Equal(CapabilityEventKind.OperationTerminal, terminal.EventKind);
        Assert.Equal(44, terminal.HostMonotonicTime);
        Assert.Null(terminal.ExternalDeviceTime);
    }

    [Fact]
    public void Measurement_requires_explicit_correlation()
    {
        var missing = new CapabilityCorrelation("run-1");
        Assert.Throws<ArgumentException>(() => new MeasurementSample("sample-1", missing, "sensor", "response-time", "ms", "1.0", 3, CapabilityStatus.Completed));
        var sample = new MeasurementSample("sample-2", Correlation(), "sensor", "response-time", "ms", "1.0", 3, CapabilityStatus.Completed, externalDeviceTime: 900, sensorTime: 901);
        Assert.Equal(900, sample.ExternalDeviceTime);
        Assert.Equal(901, sample.SensorTime);
        Assert.Null(sample.HostMonotonicTime);
    }

    [Fact]
    public void Late_and_duplicate_are_explicit_dispositions()
    {
        var late = new MeasurementSample("late", Correlation(), "sensor", "response-time", "ms", "1.0", null, CapabilityStatus.Late, AssociationDisposition.Late, diagnostic: "terminal already published");
        var duplicate = new MeasurementSample("duplicate", Correlation(), "sensor", "response-time", "ms", "1.0", null, CapabilityStatus.Duplicate, AssociationDisposition.Duplicate);
        Assert.Equal(AssociationDisposition.Late, late.Association);
        Assert.Equal(AssociationDisposition.Duplicate, duplicate.Association);
    }

    [Fact]
    public void Terminal_failure_states_and_fixture_states_are_typed()
    {
        var statuses = new[] { CapabilityStatus.Failed, CapabilityStatus.Cancelled, CapabilityStatus.TimedOut, CapabilityStatus.Unknown, CapabilityStatus.Unavailable };
        Assert.All(statuses, status => Assert.Equal(status, new CapabilityEnvelope($"{status}", Correlation(), 1, "harness", "1.0", CapabilityEventKind.OperationTerminal, status).Status));
        var fixture = new FixtureLifecycleFact("fixture-1", "harness", FixtureLifecycleState.Failed, "1.0", "setup unavailable");
        Assert.Equal(FixtureLifecycleState.Failed, fixture.State);
    }

    [Fact]
    public void Artifact_and_finding_keep_independent_source_and_version()
    {
        var finding = new Finding("finding-1", Correlation(), "rule-2", "inspector", FindingDisposition.Pass, CapabilityStatus.Completed, "check passed");
        var artifact = new ArtifactReference("artifact-1", Correlation(), "capture", "schema-3", "sha256:abc", "image/png");
        Assert.Equal(FindingDisposition.Pass, finding.Disposition);
        Assert.Equal("response-time", new MeasurementSample("sample-3", Correlation(), "sensor", "response-time", "ms", "1.0", 4, CapabilityStatus.Completed).MetricName);
        Assert.Equal("rule-2", finding.RuleVersion);
        Assert.Equal("schema-3", artifact.ArtifactVersion);
        Assert.Equal("image/png", artifact.MediaType);
    }
}
