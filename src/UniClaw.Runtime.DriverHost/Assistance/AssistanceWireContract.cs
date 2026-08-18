using System.Collections.Immutable;
using System.Text.Json.Nodes;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Wire contract for the two ADDITIVE assistance methods
/// (dsh-assistance-provider-adapter): assistance.pending (read poll) and
/// assistance.resolve (submit). Transport DTOs are wire copies, not authority;
/// the frozen 9-method table keeps exact semantics.
/// </summary>
public static class UniClawAssistanceWire
{
    /// <summary>Serialize the pending digest page for assistance.pending.</summary>
    public static UniClawAssistancePendingDto ToPendingDto(ImmutableArray<AssistanceRequestDigest> pending)
        => new([.. pending.Select(ToDto)]);

    /// <summary>Serialize one pending digest (observation = capability-gap summary).</summary>
    public static UniClawAssistanceRequestDto ToDto(AssistanceRequestDigest digest)
        => new(
            digest.RequestId,
            digest.RunId,
            digest.SemanticPage,
            digest.BeliefState.ToString(),
            digest.WorldVersion,
            new UniClawObservationDigestDto(
                digest.ObservationSequence,
                digest.ForegroundApplication,
                digest.ElementCount,
                digest.ElementTexts));

    /// <summary>Serialize a resolve attempt result (business result, not an RPC error).</summary>
    public static UniClawAssistanceResolveDto ToResolveDto(AssistanceResolveResult result)
        => new(result.Resolved, result.Diagnostic);

    /// <summary>Parse assistance.resolve params into the resolve request.</summary>
    public static AssistanceResolveRequest ParseResolve(JsonObject? parameters)
    {
        if (parameters is null)
        {
            throw new ArgumentException("missing 'params' object");
        }

        var requestId = RequireString(parameters, "requestId");
        if (!UniClawWireCodec.TryGetLong(parameters, "worldVersion", out var worldVersion))
        {
            throw new ArgumentException("missing or non-integer 'worldVersion'");
        }

        string? recommendation = null;
        string? additionalEvidence = null;
        string? reason = null;
        if (parameters["recommendation"] is JsonValue rec
            && rec.GetValueKind() == System.Text.Json.JsonValueKind.String)
        {
            recommendation = rec.GetValue<string>();
        }

        if (parameters["additionalEvidence"] is JsonValue ev
            && ev.GetValueKind() == System.Text.Json.JsonValueKind.String)
        {
            additionalEvidence = ev.GetValue<string>();
        }

        if (parameters["reason"] is JsonValue rs
            && rs.GetValueKind() == System.Text.Json.JsonValueKind.String)
        {
            reason = rs.GetValue<string>();
        }

        return new AssistanceResolveRequest(requestId, worldVersion, recommendation, additionalEvidence, reason);
    }

    private static string RequireString(JsonObject obj, string key)
    {
        if (obj[key] is not JsonValue value || value.GetValueKind() != System.Text.Json.JsonValueKind.String)
        {
            throw new ArgumentException($"missing or non-string '{key}'");
        }

        var text = value.GetValue<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException($"'{key}' must not be empty");
        }

        return text;
    }
}

/// <summary>Wire copy of one pending assistance request (digest only).</summary>
public sealed record UniClawAssistanceRequestDto(
    string RequestId,
    string RunId,
    string SemanticPage,
    string BeliefState,
    long WorldVersion,
    UniClawObservationDigestDto Observation);

/// <summary>Capability-gap observation summary (never a model prompt, never raw pixels).</summary>
public sealed record UniClawObservationDigestDto(
    long Sequence,
    string? ForegroundApplication,
    int ElementCount,
    ImmutableArray<string> ElementTexts);

/// <summary>assistance.pending result page.</summary>
public sealed record UniClawAssistancePendingDto(ImmutableArray<UniClawAssistanceRequestDto> Requests);

/// <summary>assistance.resolve result (business result; resolved:false + diagnostic
/// when the request is unknown/terminal/stale/invalid — never an RPC error).</summary>
public sealed record UniClawAssistanceResolveDto(bool Resolved, string? Diagnostic);
