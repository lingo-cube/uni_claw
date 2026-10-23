using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World.UiRealization;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;

namespace UniClaw.Kernel.World;

public sealed record PageCaptureObservation(
    string CaptureId, string ObservationCycleId, string SessionCorrelation,
    DateTimeOffset CaptureTime, CoordinateSpace Space,
    IReadOnlyList<SemanticLayoutItem> Items)
{
    public IReadOnlyList<SemanticLayoutItem> Items { get; init; } = Array.AsReadOnly((Items ?? []).ToArray());
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HierarchyCoverage? Coverage { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DeviceId { get; init; }
}

public sealed record PageIdentityObservation(
    string CaptureId, string ObservationCycleId, string SessionCorrelation,
    DateTimeOffset CaptureTime, string Signature, string? ObservedTitle);

public sealed record PageObservationState(
    PageCaptureObservation Capture, string EvidenceId, string? ContainerId)
{
    /// <summary>Exact accepted identity interpretation; null when unconfirmed or contradicted.</summary>
    public string? IdentityEvidenceId { get; init; }
}

public sealed record ObservationNeed(
    string SourceRevisionId, string CaptureEvidenceId, PageCaptureObservation Capture,
    string Kind = "identity-disambiguation");

public static class PageObservationCodec
{
    public const string CaptureSubject = "ui.page.capture";
    public const string IdentitySubject = "ui.page.identity";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };

    public static string Encode(PageCaptureObservation payload) => EncodeCore(payload);
    public static string Encode(PageIdentityObservation payload) => EncodeCore(payload);
    public static bool TryReadCapture(string value, out PageCaptureObservation? payload) => TryRead(value, out payload);
    public static bool TryReadIdentity(string value, out PageIdentityObservation? payload) => TryRead(value, out payload);

    private static string EncodeCore<T>(T payload)
    {
        Validate(payload);
        return JsonSerializer.Serialize(payload, Json);
    }
    private static bool TryRead<T>(string value, out T? payload)
    {
        payload = default;
        try
        {
            payload = JsonSerializer.Deserialize<T>(value, Json);
            Validate(payload);
            if (payload is PageCaptureObservation capture)
                payload = (T)(object)(capture with { Items = Array.AsReadOnly(capture.Items.ToArray()) });
            return true;
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or NotSupportedException)
        { payload = default; return false; }
    }
    private static void Validate<T>(T? value)
    {
        if (value is PageCaptureObservation capture)
        {
            if (string.IsNullOrWhiteSpace(capture.CaptureId) || string.IsNullOrWhiteSpace(capture.ObservationCycleId) || string.IsNullOrWhiteSpace(capture.SessionCorrelation) || capture.CaptureTime == default || capture.Space is null || capture.Items is null
                || capture.Space.CaptureId is not null && capture.Space.CaptureId != capture.CaptureId || capture.Items.Count > 2048
                || capture.DeviceId is not null && string.IsNullOrWhiteSpace(capture.DeviceId))
                throw new ArgumentException("capture correlation is required");
            if (capture.Coverage is { } coverage && (!Enum.IsDefined(coverage.Completeness) || !coverage.IsValid))
                throw new ArgumentException("capture coverage must declare a valid bounded surface");
            if (capture.Items.Any(i => i is null || string.IsNullOrWhiteSpace(i.Id) || string.IsNullOrWhiteSpace(i.Kind)
                || i.Bounds is null || !i.Bounds.IsValid || i.Bounds.X1 < 0 || i.Bounds.Y1 < 0
                || i.Bounds.X2 > capture.Space.PixelWidth || i.Bounds.Y2 > capture.Space.PixelHeight
                || i.Confidence is { } confidence && (!double.IsFinite(confidence) || confidence < 0 || confidence > 1)) || capture.Items.Select(i => i.Id).Distinct().Count() != capture.Items.Count)
                throw new ArgumentException("capture items must be unique and bounded");
        }
        else if (value is PageIdentityObservation identity)
        {
            if (string.IsNullOrWhiteSpace(identity.CaptureId) || string.IsNullOrWhiteSpace(identity.ObservationCycleId) || string.IsNullOrWhiteSpace(identity.SessionCorrelation) || identity.CaptureTime == default || string.IsNullOrWhiteSpace(identity.Signature))
                throw new ArgumentException("identity correlation and signature are required");
        }
        else throw new ArgumentException("unsupported page observation");
    }
}

internal static class PageObservationEvidence
{
    internal static bool Matches(PageIdentityObservation identity, PageObservationState capture,
        EvidenceRecord record)
    {
        if (capture is null || record is null || capture.Capture.Items.Count < 2
            || identity.CaptureId != capture.Capture.CaptureId
            || identity.ObservationCycleId != capture.Capture.ObservationCycleId
            || identity.SessionCorrelation != capture.Capture.SessionCorrelation
            || identity.CaptureTime != capture.Capture.CaptureTime
            || record.Provenance.CaptureTime != capture.Capture.CaptureTime
            || !record.DerivedFromEvidenceIds.Contains(capture.EvidenceId)
                && !record.TransitiveEvidenceBasis.Contains(capture.EvidenceId)
            || string.IsNullOrWhiteSpace(identity.ObservedTitle)
            || !capture.Capture.Items.Any(i => string.Equals(i.Text?.Trim(), identity.ObservedTitle.Trim(), StringComparison.Ordinal)))
            return false;
        return true;
    }

    internal static IReadOnlyList<ProposedOccurrence> ToOwnerlessOccurrences(PageCaptureObservation capture)
    {
        var width = capture.Space.PixelWidth;
        var height = capture.Space.PixelHeight;
        return capture.Items.Select(item => new ProposedOccurrence(
            null, item.Kind, item.Text,
            null, new SpatialLocator(
                (double)item.Bounds.X1 / width, (double)item.Bounds.Y1 / height,
                (double)item.Bounds.X2 / width, (double)item.Bounds.Y2 / height,
                "device-viewport"), null, capture.Space)).ToArray();
    }

}
