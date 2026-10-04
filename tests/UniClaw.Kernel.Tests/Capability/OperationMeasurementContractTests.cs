using UniClaw.Kernel.Capability;
using Xunit;

namespace UniClaw.Kernel.Tests.Capability;

public sealed class OperationMeasurementContractTests
{
    private static CapabilityCorrelation Correlation(string run = "run-1", string operation = "op-1", string? receipt = "receipt-1")
        => new(run, operationId: operation, receiptId: receipt, captureId: "capture-1");

    private static CapabilityEnvelope Envelope(CapabilityEventKind kind, CapabilityStatus status, CapabilityCorrelation? correlation = null)
        => new($"event-{Guid.NewGuid()}", correlation ?? Correlation(), kind == CapabilityEventKind.OperationTerminal ? 2 : 1, "runtime", "1.0", kind, status);

    [Theory]
    [InlineData(CapabilityStatus.Completed)]
    [InlineData(CapabilityStatus.Failed)]
    [InlineData(CapabilityStatus.Cancelled)]
    [InlineData(CapabilityStatus.TimedOut)]
    [InlineData(CapabilityStatus.Unknown)]
    [InlineData(CapabilityStatus.Unavailable)]
    public void Start_and_terminal_pair_preserves_stage_and_terminal_status(CapabilityStatus status)
    {
        var start = new OperationMeasurementStart(Envelope(CapabilityEventKind.OperationStarted, CapabilityStatus.Unknown), OperationMeasurementStage.Dispatch);
        var terminal = new OperationMeasurementTerminal(Envelope(CapabilityEventKind.OperationTerminal, status), OperationMeasurementStage.Dispatch);
        var pair = OperationMeasurementPair.Create(start, terminal);
        Assert.Equal(OperationMeasurementStage.Dispatch, pair.Start.Stage);
        Assert.Equal(status, pair.Terminal.Envelope.Status);
    }

    [Fact]
    public void Dispatch_receipt_and_verification_are_distinct_stages()
    {
        Assert.NotEqual(OperationMeasurementStage.Dispatch, OperationMeasurementStage.Receipt);
        Assert.NotEqual(OperationMeasurementStage.Receipt, OperationMeasurementStage.PostActionVerification);
    }

    [Fact]
    public void Pair_rejects_different_run_or_explicit_association()
    {
        var start = new OperationMeasurementStart(Envelope(CapabilityEventKind.OperationStarted, CapabilityStatus.Unknown), OperationMeasurementStage.Receipt);
        var differentRun = new OperationMeasurementTerminal(Envelope(CapabilityEventKind.OperationTerminal, CapabilityStatus.Completed, Correlation("run-2")), OperationMeasurementStage.Receipt);
        var differentOperation = new OperationMeasurementTerminal(Envelope(CapabilityEventKind.OperationTerminal, CapabilityStatus.Completed, Correlation(operation: "op-2")), OperationMeasurementStage.Receipt);
        Assert.Throws<ArgumentException>(() => OperationMeasurementPair.Create(start, differentRun));
        Assert.Throws<ArgumentException>(() => OperationMeasurementPair.Create(start, differentOperation));
    }

    [Fact]
    public void Pair_rejects_missing_association_and_mismatched_stage()
    {
        var missing = new CapabilityCorrelation("run-1");
        var start = new OperationMeasurementStart(Envelope(CapabilityEventKind.OperationStarted, CapabilityStatus.Unknown, missing), OperationMeasurementStage.Dispatch);
        var terminal = new OperationMeasurementTerminal(Envelope(CapabilityEventKind.OperationTerminal, CapabilityStatus.Completed, missing), OperationMeasurementStage.Dispatch);
        Assert.Throws<ArgumentException>(() => OperationMeasurementPair.Create(start, terminal));
        Assert.Throws<ArgumentException>(() => new MeasurementSample("s", missing, "runtime", "elapsed", "ms", "1.0", 2, CapabilityStatus.Completed));
    }

    [Fact]
    public void Pair_rejects_terminal_that_is_not_after_start()
    {
        var correlation = Correlation();
        var start = new OperationMeasurementStart(new CapabilityEnvelope("start", correlation, 2, "runtime", "1.0", CapabilityEventKind.OperationStarted, CapabilityStatus.Unknown), OperationMeasurementStage.Dispatch);
        var terminal = new OperationMeasurementTerminal(new CapabilityEnvelope("terminal", correlation, 2, "runtime", "1.0", CapabilityEventKind.OperationTerminal, CapabilityStatus.Completed), OperationMeasurementStage.Dispatch);
        Assert.Throws<ArgumentException>(() => OperationMeasurementPair.Create(start, terminal));
    }

    [Theory]
    [InlineData(CapabilityStatus.Late)]
    [InlineData(CapabilityStatus.Duplicate)]
    public void Late_and_duplicate_cannot_be_terminal_states(CapabilityStatus status)
        => Assert.Throws<ArgumentException>(() => new OperationMeasurementTerminal(Envelope(CapabilityEventKind.OperationTerminal, status), OperationMeasurementStage.Dispatch));

    [Fact]
    public void Independent_clock_fields_remain_separate()
    {
        var sample = new MeasurementSample("s", Correlation(), "sensor", "elapsed", "ms", "1.0", 12, CapabilityStatus.Completed,
            runtimeElapsed: TimeSpan.FromMilliseconds(12), hostMonotonicTime: 100, externalDeviceTime: 200, sensorTime: 300);
        Assert.Equal(12, sample.RuntimeElapsed.Value.TotalMilliseconds);
        Assert.Equal(100, sample.HostMonotonicTime);
        Assert.Equal(200, sample.ExternalDeviceTime);
        Assert.Equal(300, sample.SensorTime);
    }
}
