using System.Collections.Immutable;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Harness;
using UniClaw.Runtime.Harness.Capture;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Observability;
using Xunit;

namespace UniClaw.Runtime.Tests.DriverHost;

/// <summary>
/// Control-surface behavior tests (PLUG-F8/F12/F13/F15 gate coverage).
/// The surface is the DriverHost-internal facade over the observability read
/// model: read-only, fail-open, deterministic diagnostics.
/// </summary>
public sealed class ControlSurfaceTests
{
    private static (DriverHostObservability Observability, UniClawControlSurface Surface) BuildSurface()
    {
        var observability = new DriverHostObservability();
        var surface = new UniClawControlSurface(observability);
        return (observability, surface);
    }

    [Fact]
    public void Ping_ReturnsServiceName()
    {
        var (_, surface) = BuildSurface();
        Assert.Equal("dsh-uniclaw-driverhost", surface.Ping());
    }

    [Fact]
    public void ListRuns_Empty_WhenNothingRegistered()
    {
        var (_, surface) = BuildSurface();
        Assert.Empty(surface.ListRuns());
    }

    [Fact]
    public void ListRuns_ReturnsRegisteredRuns_Sorted()
    {
        var (observability, surface) = BuildSurface();
        observability.RegisterRun("run-b", ReadOnlyObservabilityFixtures.CompletedTrace(), ReadOnlyObservabilityFixtures.CompletedRun());
        observability.RegisterRun("run-a", ReadOnlyObservabilityFixtures.CompletedTrace(), ReadOnlyObservabilityFixtures.CompletedRun());

        var runs = surface.ListRuns();
        Assert.Equal(["run-a", "run-b"], runs);
    }

    [Fact]
    public void InspectRun_KnownRun_HasDirectRunState()
    {
        var (observability, surface) = BuildSurface();
        observability.RegisterRun(ReadOnlyObservabilityFixtures.RunId, ReadOnlyObservabilityFixtures.CompletedTrace(), ReadOnlyObservabilityFixtures.CompletedRun());

        var snapshot = surface.InspectRun(ReadOnlyObservabilityFixtures.RunId);
        Assert.Equal(ReadOnlyObservabilityFixtures.RunId, snapshot.RunId);
        Assert.Equal(SnapshotFieldClassification.DirectPublicProjection, snapshot.RunState.Classification);
        Assert.Equal(RunState.Completed, snapshot.RunState.Value);
    }

    [Fact]
    public void InspectRun_UnknownRun_ReturnsUnknownSnapshot_WithDiagnostic()
    {
        var (_, surface) = BuildSurface();
        var snapshot = surface.InspectRun("no-such-run");
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, snapshot.RunState.Classification);
        Assert.Contains(snapshot.Diagnostics, d => d.Contains("No registered run", StringComparison.Ordinal));
        // On the wire an unknown run's RunState value must stay absent (never
        // invented — the codec nulls plainly unavailable fields).
        var dto = UniClawWireCodec.ToDto(snapshot);
        Assert.Null(dto.RunState.Value);
        Assert.Equal("notCurrentlyAvailable", dto.RunState.Classification);
    }

    [Fact]
    public void InspectRun_UnavailableFields_StayUnavailable()
    {
        var (observability, surface) = BuildSurface();
        observability.RegisterRun(ReadOnlyObservabilityFixtures.RunId, ReadOnlyObservabilityFixtures.CompletedTrace(), ReadOnlyObservabilityFixtures.CompletedRun());

        var snapshot = surface.InspectRun(ReadOnlyObservabilityFixtures.RunId);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, snapshot.CurrentObservationSequence.Classification);
        Assert.Null(snapshot.CurrentObservationSequence.Value);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, snapshot.CurrentContainerSummary.Classification);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, snapshot.BindingsSummary.Classification);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, snapshot.StateBeliefsSummary.Classification);
    }

    [Fact]
    public void InspectRun_DerivedFields_AreClassifiedDerived()
    {
        var (observability, surface) = BuildSurface();
        observability.RegisterRun(ReadOnlyObservabilityFixtures.RunId, ReadOnlyObservabilityFixtures.CompletedTrace(), ReadOnlyObservabilityFixtures.CompletedRun());

        var snapshot = surface.InspectRun(ReadOnlyObservabilityFixtures.RunId);
        Assert.Equal(SnapshotFieldClassification.DerivedReadModel, snapshot.CurrentGoal.Classification);
        Assert.Equal("WifiConnectivity.Enabled=true", snapshot.CurrentGoal.Value?.Goal);
        Assert.Equal(SnapshotFieldClassification.DerivedReadModel, snapshot.LastDecision.Classification);
        Assert.Equal(SnapshotFieldClassification.DerivedReadModel, snapshot.LastAction.Classification);
        Assert.Equal("Action-1", snapshot.LastAction.Value?.ActionId);
    }

    [Fact]
    public void InspectTrap_NoTrap_ReturnsFoundFalse_WithoutDiagnostic()
    {
        var (observability, surface) = BuildSurface();
        observability.RegisterRun(ReadOnlyObservabilityFixtures.RunId, ReadOnlyObservabilityFixtures.CompletedTrace(), ReadOnlyObservabilityFixtures.CompletedRun());

        var result = surface.InspectTrap(ReadOnlyObservabilityFixtures.RunId);
        Assert.False(result.Found);
        Assert.Null(result.Diagnostic);
        Assert.Equal(SnapshotFieldClassification.DirectPublicProjection, result.Trap?.Classification);
    }

    [Fact]
    public void InspectTrap_WithTrap_ReturnsFoundTrue_AndTrapDetails()
    {
        var (observability, surface) = BuildSurface();
        observability.RegisterRun(ReadOnlyObservabilityFixtures.RunId, ReadOnlyObservabilityFixtures.CompletedTrace(), ReadOnlyObservabilityFixtures.FailedRunWithTrapAndRecovery());

        var result = surface.InspectTrap(ReadOnlyObservabilityFixtures.RunId);
        Assert.True(result.Found);
        Assert.Null(result.Diagnostic);
        Assert.NotNull(result.Trap?.Value);
        Assert.Equal(TrapKind.StateMismatch, result.Trap!.Value!.Kind);
        Assert.Equal(TrapScope.Agent, result.Trap.Value.Scope);
        Assert.Equal(7, result.Trap.Value.Observed);
    }

    [Fact]
    public void InspectTrap_UnknownRun_ReturnsFoundFalse_WithDiagnostic()
    {
        var (_, surface) = BuildSurface();
        var result = surface.InspectTrap("no-such-run");
        Assert.False(result.Found);
        Assert.NotNull(result.Diagnostic);
        Assert.Contains("not registered", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void ControlSupport_AllCandidateControls_AreDeferredNoKernelControlBuyer()
    {
        var (_, surface) = BuildSurface();
        foreach (var operation in ControlSupportAudit.CandidateOperations)
        {
            var result = surface.ControlSupport(operation);
            Assert.Equal(operation, result.Operation);
            Assert.False(result.Supported);
            Assert.Equal(ControlSupportAudit.DeferredNoKernelControlBuyer, result.Reason);
            Assert.False(result.ReadOnly);
            Assert.NotEmpty(result.Evidence);
        }
    }

    [Fact]
    public void ControlSupport_ReadOnlyOperations_AreSupportedAndReadOnly()
    {
        var (_, surface) = BuildSurface();
        foreach (var operation in ControlSupportAudit.ReadOnlyOperations)
        {
            var result = surface.ControlSupport(operation);
            Assert.True(result.Supported, $"expected '{operation}' supported");
            Assert.Equal(ControlSupportAudit.ReadOnlyInspect, result.Reason);
            Assert.True(result.ReadOnly);
        }
    }

    [Fact]
    public void ControlSupport_UnknownOperation_ResolvesToUnknownOperation()
    {
        var (_, surface) = BuildSurface();
        var result = surface.ControlSupport("explode-the-kernel");
        Assert.False(result.Supported);
        Assert.Equal(ControlSupportAudit.UnknownOperation, result.Reason);
        Assert.False(result.ReadOnly);
    }

    [Fact]
    public void OpenEvidence_NoCatalog_ReturnsFoundFalse_WithDiagnostic()
    {
        var (observability, surface) = BuildSurface();
        observability.RegisterRun(ReadOnlyObservabilityFixtures.RunId, ReadOnlyObservabilityFixtures.CompletedTrace(), ReadOnlyObservabilityFixtures.CompletedRun());

        var reference = new EvidenceRef
        {
            EvidenceId = "capture:session-1:record:1",
            Kind = EvidenceKind.TraceFragment,
            RunId = ReadOnlyObservabilityFixtures.RunId,
            Locator = "capture:session-1:record:1",
        };

        var resolution = surface.OpenEvidence(reference);
        Assert.False(resolution.Found);
        Assert.Contains("No evidence catalog registered", resolution.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenEvidence_ResolvesByLogicalLocatorOnly_WithRealBundle()
    {
        var (observability, surface) = BuildSurface();
        var bundle = new TraceCaptureBundle
        {
            CaptureSessionId = "session-1",
            Provenance = "Synthetic",
            FinalState = CaptureState.Persisted,
            Records =
            [
                new CaptureRecord
                {
                    Order = 1,
                    Kind = CaptureRecordKind.Observation,
                    SequenceNumber = 7,
                    FrameId = "frame-1",
                },
                new CaptureRecord
                {
                    Order = 2,
                    Kind = CaptureRecordKind.ActionDispatch,
                    ActionId = "Action-1",
                },
            ],
            Artifacts =
            [
                new CaptureArtifact { ArtifactId = "artifact-0001", FrameId = "frame-1", FileName = "shot.png", ByteCount = 128 },
            ],
        };
        observability.RegisterRun(ReadOnlyObservabilityFixtures.RunId, ReadOnlyObservabilityFixtures.CompletedTrace(), ReadOnlyObservabilityFixtures.CompletedRun(), bundle);

        var reference = new EvidenceRef
        {
            EvidenceId = "capture:session-1:record:1",
            Kind = EvidenceKind.PerceptionOutput,
            RunId = ReadOnlyObservabilityFixtures.RunId,
            Locator = "capture:session-1:record:1",
        };

        var resolution = surface.OpenEvidence(reference);
        Assert.True(resolution.Found);
        Assert.Equal("session-1", resolution.CaptureSessionId);
        Assert.NotNull(resolution.Record);
        Assert.Equal(CaptureRecordKind.Observation, resolution.Record!.Kind);
        Assert.Equal(7, resolution.Record.SequenceNumber);
        Assert.Null(resolution.Artifact);
    }

    [Fact]
    public void OpenEvidence_PathLookingLocator_IsNotResolved()
    {
        var (observability, surface) = BuildSurface();
        var bundle = new TraceCaptureBundle
        {
            CaptureSessionId = "session-1",
            Provenance = "Synthetic",
            FinalState = CaptureState.Persisted,
            Records =
            [
                new CaptureRecord { Order = 1, Kind = CaptureRecordKind.Observation, SequenceNumber = 7 },
            ],
        };
        observability.RegisterRun(ReadOnlyObservabilityFixtures.RunId, ReadOnlyObservabilityFixtures.CompletedTrace(), ReadOnlyObservabilityFixtures.CompletedRun(), bundle);

        var reference = new EvidenceRef
        {
            EvidenceId = "/etc/passwd",
            Kind = EvidenceKind.TraceFragment,
            RunId = ReadOnlyObservabilityFixtures.RunId,
            Locator = "/etc/passwd",
        };

        var resolution = surface.OpenEvidence(reference);
        Assert.False(resolution.Found);
        Assert.Contains("not found in catalog", resolution.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void GetRuntimeEvents_UnknownRun_ReturnsEmptyPageWithDiagnostics()
    {
        var (_, surface) = BuildSurface();
        var page = surface.GetRuntimeEvents("no-such-run");
        Assert.Equal("no-such-run", page.RunId);
        Assert.Empty(page.Events);
        Assert.False(page.HasMore);
    }
}
