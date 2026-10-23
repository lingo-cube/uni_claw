using System.Text.Json;

namespace UniClaw.Kernel.World;

/// <summary>Independent native-profile comparison, not a model's self-assessment.</summary>
public sealed record SemanticVerificationObservation(string SemanticEvidenceId, string SourceCaptureEvidenceId,
    string SourceIdentityEvidenceId, string NativeContextEvidenceId, string ProfileFingerprint);

public sealed record SemanticVerificationDecision(string SourceRevisionId, string EvidenceId,
    SemanticVerificationObservation? Observation, bool Applied, string Reason);

/// <summary>World-derived stable meaning with original validation and new correspondence;
/// neither a recaptured observation nor a current interaction handle.</summary>
public sealed record WorldSemanticKnowledge(string ContainerId, string CurrentCaptureEvidenceId,
    string CaptureId, string ObservationCycleId, string SessionCorrelation,
    string PageKey, string PageName, string PagePurpose,
    string OriginalSemanticEvidenceId, string VerificationEvidenceId,
    IReadOnlyList<string> CorrespondenceEvidenceIds, string SourceRevisionId, string ApplicableRevisionId, bool Reused);

public static class SemanticVerificationCodec
{
    public const string Subject = "ui.semantic.verification";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    public static string Encode(SemanticVerificationObservation value)
    {
        Validate(value);
        return JsonSerializer.Serialize(value, Json);
    }
    public static bool TryRead(string value, out SemanticVerificationObservation? observation)
    {
        observation = null;
        try { observation = JsonSerializer.Deserialize<SemanticVerificationObservation>(value, Json); Validate(observation!); return true; }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException)
        { observation = null; return false; }
    }
    private static void Validate(SemanticVerificationObservation value)
    {
        if (value is null || new[] { value.SemanticEvidenceId, value.SourceCaptureEvidenceId,
            value.SourceIdentityEvidenceId, value.NativeContextEvidenceId, value.ProfileFingerprint }.Any(string.IsNullOrWhiteSpace)
            || value.ProfileFingerprint.Length != 64 || !value.ProfileFingerprint.All(Uri.IsHexDigit))
            throw new ArgumentException("semantic validation requires exact sources and profile fingerprint");
    }
}
