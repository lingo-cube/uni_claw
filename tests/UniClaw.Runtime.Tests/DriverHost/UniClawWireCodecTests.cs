using System.Collections.Immutable;
using System.Text.Json.Nodes;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Harness;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Tests.Observability;
using Xunit;

namespace UniClaw.Runtime.Tests.DriverHost;

/// <summary>
/// Wire codec tests (PLUG-F8/F9/F10/F11/F14 gate coverage).
/// The codec maps DriverHost read models to immutable wire DTOs, preserving
/// field classification, unavailable-ness, cursor semantics, and stable event
/// identity/sequence/observationSequence. Pure, deterministic, side-effect free.
/// </summary>
public sealed class UniClawWireCodecTests
{
    private static DriverHostObservability BuildObservability()
    {
        var observability = new DriverHostObservability();
        observability.RegisterRun(ReadOnlyObservabilityFixtures.RunId, ReadOnlyObservabilityFixtures.CompletedTrace(), ReadOnlyObservabilityFixtures.CompletedRun());
        return observability;
    }

    [Fact]
    public void SnapshotDto_PreservesClassificationStrings()
    {
        var dto = UniClawWireCodec.ToDto(BuildObservability().GetRunSnapshot(ReadOnlyObservabilityFixtures.RunId));

        Assert.Equal("directPublicProjection", dto.RunState.Classification);
        Assert.Equal("derivedReadModel", dto.CurrentGoal.Classification);
        Assert.Equal("notCurrentlyAvailable", dto.CurrentObservationSequence.Classification);
    }

    [Fact]
    public void SnapshotDto_UnavailableField_HasNullValue_AndIsPartialFlag()
    {
        var dto = UniClawWireCodec.ToDto(BuildObservability().GetRunSnapshot(ReadOnlyObservabilityFixtures.RunId));

        Assert.Null(dto.CurrentObservationSequence.Value);
        Assert.Equal("notCurrentlyAvailable", dto.CurrentObservationSequence.Classification);
        Assert.True(dto.LatestGoalEvidence.IsPartial);
        Assert.Equal("notCurrentlyAvailable", dto.LatestGoalEvidence.Classification);
    }

    [Fact]
    public void SnapshotDto_SerializesAndRoundTrips_ThroughJson()
    {
        var dto = UniClawWireCodec.ToDto(BuildObservability().GetRunSnapshot(ReadOnlyObservabilityFixtures.RunId));
        var json = UniClawWireCodec.Serialize(dto);
        var parsed = UniClawWireCodec.ParseObject(json);

        Assert.Equal(ReadOnlyObservabilityFixtures.RunId, parsed["runId"]?.GetValue<string>());
        Assert.Equal("completed", parsed["runState"]?["value"]?.GetValue<string>());
        Assert.Equal("directPublicProjection", parsed["runState"]?["classification"]?.GetValue<string>());
        Assert.Null(parsed["currentObservationSequence"]?["value"]);
        Assert.Equal("notCurrentlyAvailable", parsed["currentObservationSequence"]?["classification"]?.GetValue<string>());
        Assert.NotNull(parsed["latestGoalEvidence"]?["value"]);
        Assert.True(parsed["latestGoalEvidence"]?["isPartial"]?.GetValue<bool>());
    }

    [Fact]
    public void EventPageDto_PreservesCursorAndEventIdentity()
    {
        var observability = BuildObservability();
        var page = observability.GetRuntimeEvents(ReadOnlyObservabilityFixtures.RunId);
        var dto = UniClawWireCodec.ToDto(page);

        Assert.Equal(ReadOnlyObservabilityFixtures.RunId, dto.RunId);
        Assert.Equal(page.Events.Length, dto.Events.Length);
        Assert.NotNull(dto.NextCursor);
        Assert.Equal(page.NextCursor!.RunId, dto.NextCursor.RunId);
        Assert.Equal(page.NextCursor.LastSequence, dto.NextCursor.LastSequence);

        var first = dto.Events[0];
        Assert.Equal(page.Events[0].EventId, first.EventId);
        Assert.Equal(page.Events[0].Sequence, first.Sequence);
        Assert.Equal(page.Events[0].ObservationSequence, first.ObservationSequence);
        Assert.Equal(page.Events[0].Kind.ToString(), first.Kind);
        Assert.Equal(page.Events[0].EvidenceRefs.Length, first.EvidenceRefs.Length);
        Assert.NotNull(first.Payload);
    }

    [Fact]
    public void EventPageDto_EventIdsAreStableAndRunScoped()
    {
        var observability = BuildObservability();
        var page = observability.GetRuntimeEvents(ReadOnlyObservabilityFixtures.RunId);
        var dto = UniClawWireCodec.ToDto(page);

        foreach (var (index, evt) in dto.Events.Index())
        {
            Assert.StartsWith($"evt-{ReadOnlyObservabilityFixtures.RunId}-", evt.EventId, StringComparison.Ordinal);
            Assert.Equal(evt.Sequence, index + 1);
        }
    }

    [Fact]
    public void SerializeResponse_EchoesId_Verbatim()
    {
        var json = UniClawWireCodec.SerializeResponse(42, new UniClawPingDto("svc", 1, "chg"));
        var parsed = UniClawWireCodec.ParseObject(json);

        Assert.Equal(42, parsed["id"]?.GetValue<int>());
        Assert.Equal("2.0", parsed["jsonrpc"]?.GetValue<string>());
        Assert.Equal("svc", parsed["result"]?["service"]?.GetValue<string>());
    }

    [Fact]
    public void SerializeError_UsesTypedCode_AndEchoesNullId()
    {
        var json = UniClawWireCodec.SerializeError(null, UniClawWireContract.ErrorUnknownMethod, "nope");
        var parsed = UniClawWireCodec.ParseObject(json);

        Assert.Null(parsed["id"]);
        Assert.Equal("unknown_method", parsed["error"]?["code"]?.GetValue<string>());
        Assert.Equal("nope", parsed["error"]?["message"]?.GetValue<string>());
    }

    [Fact]
    public void ParseCursor_AbsentOrMalformed_ReturnsNull()
    {
        Assert.Null(UniClawWireCodec.ParseCursor(null));
        Assert.Null(UniClawWireCodec.ParseCursor(new JsonObject()));
        Assert.Null(UniClawWireCodec.ParseCursor(new JsonObject { ["runId"] = "run-1" }));
    }

    [Fact]
    public void ParseCursor_RoundTripsRunIdAndLastSequence()
    {
        var cursor = UniClawWireCodec.ParseCursor(new JsonObject { ["runId"] = "run-1", ["lastSequence"] = 3 });
        Assert.NotNull(cursor);
        Assert.Equal("run-1", cursor.RunId);
        Assert.Equal(3, cursor.LastSequence);
    }

    [Fact]
    public void ParseEvidenceRef_RequiresLocatorAndRunId()
    {
        var reference = UniClawWireCodec.ParseEvidenceRef(new JsonObject { ["locator"] = "capture:s:record:1", ["runId"] = "run-1" });
        Assert.Equal("capture:s:record:1", reference.Locator);
        Assert.Equal("run-1", reference.RunId);

        Assert.Throws<ArgumentException>(() => UniClawWireCodec.ParseEvidenceRef(new JsonObject { ["locator"] = "x" }));
    }

    [Fact]
    public void TrapDto_RoundTripsWithLastActionDescription()
    {
        var trap = new Trap(
            TrapKind.StateMismatch,
            TrapScope.Agent,
            expected: 3,
            observed: 7,
            "agent",
            "observed=false expected=true",
            new DeviceAction.SetSwitch(1, true));
        var dto = UniClawWireCodec.ToDto(trap);

        Assert.Equal("StateMismatch", dto.Kind);
        Assert.Equal("Agent", dto.Scope);
        Assert.Equal(3, dto.Expected);
        Assert.Equal(7, dto.Observed);
        Assert.Equal("agent", dto.Source);
        Assert.Equal("observed=false expected=true", dto.Evidence);
        Assert.NotNull(dto.LastActionDescription);
        Assert.Contains("SetSwitch", dto.LastActionDescription, StringComparison.Ordinal);
    }

    [Fact]
    public void ControlSupportDto_RoundTrips()
    {
        var result = ControlSupportAudit.Audit("pause");
        var dto = UniClawWireCodec.ToDto(result);

        Assert.Equal("pause", dto.Operation);
        Assert.False(dto.Supported);
        Assert.Equal(ControlSupportAudit.DeferredNoKernelControlBuyer, dto.Reason);
        Assert.False(dto.ReadOnly);
        Assert.Equal(result.Evidence, dto.Evidence);
    }
}
