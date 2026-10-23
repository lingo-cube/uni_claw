using System.Collections.Immutable;
using System.Text.Json;

namespace UniClaw.Kernel.World;

/// <summary>A precisely targeted observation proposal, never a canonical Container assignment.</summary>
public sealed record PageInterpretationCorrection(
    string TargetCaptureEvidenceId, string SupersededIdentityEvidenceId,
    PageIdentityObservation ReplacementIdentity, IReadOnlyList<string> SupportingEvidenceIds,
    string ProcessingSourceRef);

/// <summary>World's immutable result, including unsuccessful attempts to reinterpret history.</summary>
public sealed record PageCorrectionDecision(
    string SourceRevisionId, string CorrectionEvidenceId, string TargetCaptureEvidenceId,
    string SupersededIdentityEvidenceId, string? PreviousContainerId, string? CorrectedContainerId,
    PageIdentityObservation? ReplacementIdentity, IReadOnlyList<string> SupportingEvidenceIds,
    string ProcessingSourceRef, bool Applied, string Reason)
{
    public IReadOnlyList<string> RevokedInterpretationEvidenceIds { get; init; } = ImmutableArray<string>.Empty;
}

public static class PageInterpretationCodec
{
    public const string Subject = "ui.page.interpretation.correction";
    public const string NativeContextProfile = "page-context-signature:native:v1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };

    public static string Encode(PageInterpretationCorrection correction)
    {
        Validate(correction);
        return JsonSerializer.Serialize(correction, Json);
    }

    public static bool TryRead(string value, out PageInterpretationCorrection? correction)
    {
        correction = null;
        try
        {
            var parsed = JsonSerializer.Deserialize<PageInterpretationCorrection>(value, Json);
            Validate(parsed!);
            correction = parsed! with { SupportingEvidenceIds = parsed.SupportingEvidenceIds.ToImmutableArray() };
            return true;
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or NotSupportedException)
        { return false; }
    }

    private static void Validate(PageInterpretationCorrection correction)
    {
        if (correction is null || string.IsNullOrWhiteSpace(correction.TargetCaptureEvidenceId)
            || string.IsNullOrWhiteSpace(correction.SupersededIdentityEvidenceId)
            || string.IsNullOrWhiteSpace(correction.ProcessingSourceRef)
            || correction.SupportingEvidenceIds is not { Count: > 0 and <= 32 }
            || correction.SupportingEvidenceIds.Any(string.IsNullOrWhiteSpace)
            || correction.SupportingEvidenceIds.Distinct(StringComparer.Ordinal).Count() != correction.SupportingEvidenceIds.Count)
            throw new ArgumentException("correction needs an exact original interpretation and bounded support");
        _ = PageObservationCodec.Encode(correction.ReplacementIdentity);
    }
}
