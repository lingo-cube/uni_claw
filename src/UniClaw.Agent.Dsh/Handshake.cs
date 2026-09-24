namespace UniClaw.Agent.Dsh;

public sealed record HandshakeValidation(bool Accepted, string? FailureReason)
{
    public static HandshakeValidation Accept() => new(true, null);
    public static HandshakeValidation Reject(string reason) => new(false, reason);
}

public static class ProductHandshake
{
    public static HandshakeRequest CreateRequest(string productSessionId, string productRunId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productSessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(productRunId);
        var schema = ProductProtocolSchema.Current;
        return new HandshakeRequest(
            new ProtocolStamp(schema.ProtocolVersion, schema.SchemaVersion, schema.SchemaHash),
            CapabilityManifest.ProductHeadless,
            productSessionId,
            productRunId);
    }

    public static HandshakeValidation Validate(
        HandshakeRequest expected,
        HandshakeResponse reported)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(reported);
        if (!reported.Accepted)
            return HandshakeValidation.Reject(reported.FailureReason ?? "sidecar rejected handshake");
        if (!string.Equals(expected.Protocol.ProtocolVersion, reported.Protocol.ProtocolVersion,
                StringComparison.Ordinal))
            return HandshakeValidation.Reject("protocol-version-mismatch");
        if (!string.Equals(expected.Protocol.SchemaVersion, reported.Protocol.SchemaVersion,
                StringComparison.Ordinal))
            return HandshakeValidation.Reject("schema-version-mismatch");
        if (!string.Equals(expected.Protocol.SchemaHash, reported.Protocol.SchemaHash,
                StringComparison.Ordinal))
            return HandshakeValidation.Reject("schema-hash-mismatch");

        if (expected.ExpectedCapabilities.HasDuplicates || reported.ReportedCapabilities.HasDuplicates)
            return HandshakeValidation.Reject("capability-manifest-duplicate");

        var expectedCapabilities = expected.ExpectedCapabilities.NormalizedCapabilities;
        var reportedCapabilities = reported.ReportedCapabilities.NormalizedCapabilities;
        if (!expectedCapabilities.SequenceEqual(reportedCapabilities, StringComparer.Ordinal))
            return HandshakeValidation.Reject("capability-manifest-mismatch");
        if (reportedCapabilities.Any(ProductCapabilities.ForbiddenProductCapabilities.Contains))
            return HandshakeValidation.Reject("forbidden-capability-reported");
        if (!reportedCapabilities.Contains(ProductCapabilities.SubmitDecision,
                StringComparer.Ordinal))
            return HandshakeValidation.Reject("submit_decision-capability-missing");
        if (string.IsNullOrWhiteSpace(reported.DshSessionId))
            return HandshakeValidation.Reject("dsh-session-id-missing");
        return HandshakeValidation.Accept();
    }
}
