using UniClaw.Kernel.Capability;
using Xunit;

namespace UniClaw.Simulation.Tests;

public sealed class SimulatedCapabilityIntegrationTests
{
    [Fact]
    public void Hub_registers_and_resolves_distinct_runtime_integration_capabilities()
    {
        var host = SimulatedCapabilityHost.Create();

        Assert.Same(host.LanguageInspector, host.Hub.Resolve(SimulatedLanguageInspector.Id));
        Assert.Same(host.OperationMeasurement, host.Hub.Resolve(SimulatedOperationMeasurement.Id));
        Assert.Equal(TrustDomain.RuntimeIntegration, host.Hub.Domain);
        Assert.Equal(2, host.Hub.Facts.Count);
        Assert.All(host.Hub.Facts, fact => Assert.Equal(CapabilityLifecycle.Registered, fact.Lifecycle));
    }

    [Fact]
    public void Language_inspection_simulation_emits_correlated_pass_and_violation_findings()
    {
        var host = SimulatedCapabilityHost.Create();
        var correlation = new CapabilityCorrelation("run-language", captureId: "capture-language", observationCycleId: "cycle-1");
        var inspector = host.LanguageInspector;

        var pass = inspector.Inspect(new LanguageInspectorRequest(
            correlation, "rule-zh-1", "zh-CN", InspectorCoverage.Full, InspectorInputState.Accepted,
            [new ObservationTextItem("occ-1", "ui://settings/title", "设置", "设置")]));
        var violation = inspector.Inspect(new LanguageInspectorRequest(
            correlation, "rule-zh-1", "zh-CN", InspectorCoverage.Full, InspectorInputState.Accepted,
            [new ObservationTextItem("occ-2", "ui://settings/title", "Settings", "Settings")]));

        Assert.Equal(FindingDisposition.Pass, pass.Disposition);
        Assert.Equal(CapabilityStatus.Completed, pass.Status);
        Assert.Equal(FindingDisposition.Violation, violation.Disposition);
        Assert.Equal(correlation, violation.Correlation);
        Assert.Equal("simulation.language-inspector", violation.SourceOwner);
    }

    [Fact]
    public void Language_inspection_simulation_keeps_unavailable_input_explicit()
    {
        var host = SimulatedCapabilityHost.Create();
        var finding = host.LanguageInspector.Inspect(new LanguageInspectorRequest(
            new CapabilityCorrelation("run-language-missing", captureId: "capture-missing"),
            "rule-zh-1", "zh-CN", InspectorCoverage.Missing, InspectorInputState.MissingInput, null));

        Assert.Equal(FindingDisposition.Unknown, finding.Disposition);
        Assert.Equal(CapabilityStatus.Unknown, finding.Status);
    }

    [Fact]
    public void Operation_measurement_simulation_emits_explicit_pair_and_sample()
    {
        var host = SimulatedCapabilityHost.Create();
        var correlation = new CapabilityCorrelation("run-performance", operationId: "tap-settings", receiptId: "receipt-1", captureId: "capture-1");

        var measurement = host.OperationMeasurement.Measure(correlation, TimeSpan.FromMilliseconds(37));

        Assert.Equal(OperationMeasurementStage.Dispatch, measurement.Pair.Start.Stage);
        Assert.Equal(OperationMeasurementStage.Dispatch, measurement.Pair.Terminal.Stage);
        Assert.Equal(CapabilityStatus.Completed, measurement.Pair.Terminal.Envelope.Status);
        Assert.Equal(correlation, measurement.Sample.Correlation);
        Assert.Equal("response-time", measurement.Sample.MetricName);
        Assert.Equal("ms", measurement.Sample.Unit);
        Assert.Equal(37d, measurement.Sample.Value);
        Assert.Equal(TimeSpan.FromMilliseconds(37), measurement.Sample.RuntimeElapsed);
    }
}

internal sealed class SimulatedCapabilityHost
{
    private SimulatedCapabilityHost(CapabilityRegistry hub, SimulatedLanguageInspector languageInspector,
        SimulatedOperationMeasurement operationMeasurement)
    {
        Hub = hub;
        LanguageInspector = languageInspector;
        OperationMeasurement = operationMeasurement;
    }

    internal CapabilityRegistry Hub { get; }
    internal SimulatedLanguageInspector LanguageInspector { get; }
    internal SimulatedOperationMeasurement OperationMeasurement { get; }

    internal static SimulatedCapabilityHost Create()
    {
        var hub = new CapabilityRegistry(TrustDomain.RuntimeIntegration);
        var languageInspector = new SimulatedLanguageInspector();
        var operationMeasurement = new SimulatedOperationMeasurement();
        hub.Register(languageInspector, "simulation-fixture");
        hub.Register(operationMeasurement, "simulation-fixture");
        return new(hub, languageInspector, operationMeasurement);
    }
}

internal sealed class SimulatedLanguageInspector : ICapability
{
    internal const string Id = "simulation.language-inspector";

    public CapabilityDescription Description { get; } = new(
        Id,
        "1.0",
        CapabilityScope.RuntimeIntegration,
        [new CapabilityProtocol("Language Inspection", "1.0")],
        [],
        HealthStatus.Healthy,
        CapabilityCategory.Generic,
        [new CapabilityRole(CapabilityRoleKind.Realization, "deterministic-language-rule")],
        []);

    internal Finding Inspect(LanguageInspectorRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.InputState != InspectorInputState.Accepted || request.Coverage != InspectorCoverage.Full)
            return request.CreateFinding("finding-unknown", Id, FindingDisposition.Unknown, CapabilityStatus.Unknown,
                "simulation input is incomplete or unavailable");

        var rendered = request.Items.Select(item => item.RenderedText ?? item.DeclaredText ?? string.Empty).ToArray();
        var expectsChinese = request.ExpectedLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        var matches = rendered.Length > 0 && rendered.All(text => text.Any(IsChineseCharacter) == expectsChinese);
        return request.CreateFinding(
            matches ? "finding-pass" : "finding-violation",
            Id,
            matches ? FindingDisposition.Pass : FindingDisposition.Violation,
            CapabilityStatus.Completed,
            matches ? "deterministic language rule matched expected language" : "deterministic language rule found a mismatch");
    }

    private static bool IsChineseCharacter(char value) => value is >= '\u4e00' and <= '\u9fff';
}

internal sealed class SimulatedOperationMeasurement : ICapability
{
    internal const string Id = "simulation.operation-measurement";

    public CapabilityDescription Description { get; } = new(
        Id,
        "1.0",
        CapabilityScope.RuntimeIntegration,
        [new CapabilityProtocol("Operation Measurement", "1.0")],
        [],
        HealthStatus.Healthy,
        CapabilityCategory.Generic,
        [new CapabilityRole(CapabilityRoleKind.Realization, "deterministic-clock-pairing")],
        []);

    internal (OperationMeasurementPair Pair, MeasurementSample Sample) Measure(
        CapabilityCorrelation correlation,
        TimeSpan elapsed,
        OperationMeasurementStage stage = OperationMeasurementStage.Dispatch,
        CapabilityStatus terminalStatus = CapabilityStatus.Completed)
    {
        ArgumentNullException.ThrowIfNull(correlation);
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));

        var start = new OperationMeasurementStart(
            new CapabilityEnvelope("measurement-start", correlation, 1, Id, "1.0",
                CapabilityEventKind.OperationStarted, CapabilityStatus.Unknown), stage);
        var terminal = new OperationMeasurementTerminal(
            new CapabilityEnvelope("measurement-terminal", correlation, 2, Id, "1.0",
                CapabilityEventKind.OperationTerminal, terminalStatus, runtimeElapsed: elapsed), stage);
        var pair = OperationMeasurementPair.Create(start, terminal);
        var sample = new MeasurementSample("measurement-sample", correlation, Id, "response-time", "ms", "1.0",
            elapsed.TotalMilliseconds, terminalStatus, runtimeElapsed: elapsed);
        return (pair, sample);
    }
}
