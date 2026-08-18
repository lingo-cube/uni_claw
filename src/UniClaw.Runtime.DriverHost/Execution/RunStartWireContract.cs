using System.Collections.Immutable;
using System.Text.Json.Nodes;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.DriverHost;

/// <summary>
/// Wire contract for the ADDITIVE run.start method (dsh-runtime-agent-subagent-run-entry).
/// The frozen 8 read-only methods keep their exact semantics; run.start is new.
///
/// Transport DTOs are NOT semantic authority: they are deterministic wire copies
/// mapped into the existing Runtime domain model (<see cref="RunStartRequest"/>
/// → <see cref="SemanticGoalInput"/> / <see cref="SemanticObject"/> /
/// <see cref="Capability"/>) at the DriverHost boundary.
/// </summary>
public static class UniClawRunStartWire
{
    /// <summary>Typed error code for deterministic start rejection (REQUEST_REJECTED).</summary>
    public const string ErrorRequestRejected = "request_rejected";

    /// <summary>Serialize an accepted result to its wire copy.</summary>
    public static UniClawRunAcceptedDto ToDto(RunAccepted accepted)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        return new UniClawRunAcceptedDto(Accepted: true, accepted.RunId, accepted.RunState.ToString());
    }

    /// <summary>Parse the run.start params object into the domain request.
    /// Malformed JSON structure → ArgumentException (bad_request); device
    /// selector parse failure → ArgumentException. Semantic rejection (unknown
    /// object/device/busy) is raised later by the execution seam.</summary>
    public static RunStartRequest ParseRunStartRequest(JsonObject? parameters)
    {
        if (parameters is null)
        {
            throw new ArgumentException("missing 'params' object");
        }

        if (parameters["goal"] is not JsonObject goal)
        {
            throw new ArgumentException("missing 'goal' object");
        }

        var goalDto = new UniClawSemanticGoalDto(
            RequireString(goal, "objectIdentity"),
            RequireString(goal, "stateDimension"),
            RequireBool(goal, "desiredValue"));

        var objects = new List<UniClawSemanticObjectDto>();
        if (parameters["objects"] is not JsonArray objectsArray)
        {
            throw new ArgumentException("missing 'objects' array");
        }

        foreach (var item in objectsArray)
        {
            if (item is not JsonObject obj)
            {
                throw new ArgumentException("each 'objects' entry must be an object");
            }

            var dimensions = new List<string>();
            if (obj["stateDimensions"] is JsonArray dims)
            {
                foreach (var dim in dims)
                {
                    if (dim is not JsonValue dimValue || dimValue.GetValueKind() != System.Text.Json.JsonValueKind.String)
                    {
                        throw new ArgumentException("each 'stateDimensions' entry must be a string");
                    }

                    dimensions.Add(dimValue.GetValue<string>());
                }
            }

            objects.Add(new UniClawSemanticObjectDto(
                RequireString(obj, "identity"),
                RequireString(obj, "category"),
                [.. dimensions]));
        }

        var capabilities = new List<UniClawCapabilityDto>();
        if (parameters["capabilities"] is not JsonArray capabilitiesArray)
        {
            throw new ArgumentException("missing 'capabilities' array");
        }

        foreach (var item in capabilitiesArray)
        {
            if (item is not JsonObject cap)
            {
                throw new ArgumentException("each 'capabilities' entry must be an object");
            }

            capabilities.Add(new UniClawCapabilityDto(
                RequireString(cap, "name"),
                RequireString(cap, "applicableToCategory"),
                RequireString(cap, "stateDimension")));
        }

        var deviceText = RequireString(parameters, "device");
        if (!DeviceSelector.TryParse(deviceText, out var selector))
        {
            throw new ArgumentException($"invalid device selector '{deviceText}'");
        }

        return Map(new UniClawRunStartRequestDto(goalDto, [.. objects], [.. capabilities], deviceText), selector);
    }

    /// <summary>Deterministic DTO → domain mapping (wire copy ≠ semantic authority).</summary>
    private static RunStartRequest Map(UniClawRunStartRequestDto dto, DeviceSelector selector)
    {
        var goal = new SemanticGoalInput(dto.Goal.ObjectIdentity, dto.Goal.StateDimension, dto.Goal.DesiredValue);
        var objects = dto.Objects
            .Select(o => SemanticObject.Define(o.Identity, o.Category, o.StateDimensions))
            .ToImmutableArray();
        var capabilities = dto.Capabilities
            .Select(c => Capability.Define(c.Name, c.ApplicableToCategory, c.StateDimension))
            .ToImmutableArray();
        return new RunStartRequest(goal, objects, capabilities, selector);
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

    private static bool RequireBool(JsonObject obj, string key)
    {
        if (obj[key] is not JsonValue value || value.GetValueKind() != System.Text.Json.JsonValueKind.True
            && value.GetValueKind() != System.Text.Json.JsonValueKind.False)
        {
            throw new ArgumentException($"missing or non-boolean '{key}'");
        }

        return value.GetValue<bool>();
    }
}

/// <summary>Transport DTO for the semantic goal (wire copy of SemanticGoalInput).</summary>
public sealed record UniClawSemanticGoalDto(
    string ObjectIdentity,
    string StateDimension,
    bool DesiredValue);

/// <summary>Transport DTO for one SemanticObject (wire copy).</summary>
public sealed record UniClawSemanticObjectDto(
    string Identity,
    string Category,
    ImmutableArray<string> StateDimensions);

/// <summary>Transport DTO for one Capability (wire copy).</summary>
public sealed record UniClawCapabilityDto(
    string Name,
    string ApplicableToCategory,
    string StateDimension);

/// <summary>Transport DTO for the run.start request (wire copy, not authority).</summary>
public sealed record UniClawRunStartRequestDto(
    UniClawSemanticGoalDto Goal,
    ImmutableArray<UniClawSemanticObjectDto> Objects,
    ImmutableArray<UniClawCapabilityDto> Capabilities,
    string Device);

/// <summary>Transport DTO for the accepted result (RunAccepted wire copy).</summary>
public sealed record UniClawRunAcceptedDto(
    bool Accepted,
    string RunId,
    string RunState);
