using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Kernel.Perception.UiHierarchy;

namespace UniClaw.Kernel.World;

/// <summary>Capture-bound observation candidate. It grants no effect authority.</summary>
/// <summary>Capture-scoped source observations, not canonical window identities.
/// Members are typed hierarchy node indices inside the observed dialog subtree.</summary>
public sealed record OverlayWindowEvidence(string BaseWindowId, string ActivityId, int DisplayId,
    string? OverlayWindowId, IReadOnlyList<int> OverlayNodeIndices)
{
    public IReadOnlyList<int> OverlayNodeIndices { get; init; } = Array.AsReadOnly((OverlayNodeIndices ?? []).ToArray());

    /// <summary>Coverage of the visible application-window set, independent of page-layout coverage.</summary>
    public HierarchyCoverage VisibleWindowCoverage { get; init; } = new(CoverageCompleteness.Unknown);
}

public sealed record OverlayObservation(string CaptureId, string ObservationCycleId,
    string SessionCorrelation, DateTimeOffset CaptureTime, string State, string Reason)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OverlayWindowEvidence? WindowEvidence { get; init; }
}

public static class OverlayObservationCodec
{
    public const string Subject = "ui.overlay.observation";
    public const string NeedKind = "overlay-evidence";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static string Encode(OverlayObservation value)
    {
        Validate(value);
        return JsonSerializer.Serialize(value, Json);
    }
    public static bool TryRead(string value, out OverlayObservation? observation)
    {
        observation = null;
        try { observation = JsonSerializer.Deserialize<OverlayObservation>(value, Json); Validate(observation!); return true; }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException)
        { observation = null; return false; }
    }
    internal static bool SameWindowEvidence(OverlayWindowEvidence first, OverlayWindowEvidence second) =>
        first.BaseWindowId == second.BaseWindowId && first.ActivityId == second.ActivityId
            && first.DisplayId == second.DisplayId && first.OverlayWindowId == second.OverlayWindowId
            && first.VisibleWindowCoverage == second.VisibleWindowCoverage
            && first.OverlayNodeIndices.Order().SequenceEqual(second.OverlayNodeIndices.Order());

    private static void Validate(OverlayObservation value)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.CaptureId)
            || string.IsNullOrWhiteSpace(value.ObservationCycleId) || string.IsNullOrWhiteSpace(value.SessionCorrelation)
            || value.CaptureTime == default || string.IsNullOrWhiteSpace(value.Reason)
            || value.State is not ("present" or "absent" or "unknown" or "conflict"))
            throw new ArgumentException("overlay correlation and supported state are required");
        if (value.WindowEvidence is { } windows &&
            (string.IsNullOrWhiteSpace(windows.BaseWindowId) || string.IsNullOrWhiteSpace(windows.ActivityId)
                || windows.DisplayId < 0 || windows.OverlayNodeIndices is null
                || windows.VisibleWindowCoverage is not { IsValid: true } coverage
                || !Enum.IsDefined(coverage.Completeness)
                || windows.OverlayNodeIndices.Any(index => index < 0)
                || windows.OverlayNodeIndices.Distinct().Count() != windows.OverlayNodeIndices.Count
                || value.State == "present" && (string.IsNullOrWhiteSpace(windows.OverlayWindowId)
                    || windows.OverlayWindowId == windows.BaseWindowId || windows.OverlayNodeIndices.Count == 0)
                || value.State == "absent" && (windows.OverlayWindowId is not null || windows.OverlayNodeIndices.Count != 0)
                || value.State is "unknown" or "conflict"))
            throw new ArgumentException("overlay window evidence inconsistent with state");
    }
}
