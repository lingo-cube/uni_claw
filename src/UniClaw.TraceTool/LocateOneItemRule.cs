namespace UniClaw.TraceTool;

/// <summary>
/// LocateOneItemRule — D-201 identity-fallback semantics ported from
/// ScenarioCompletionVerifier. Evaluates whether the final page identity
/// matches one of the expected identities after the target action executed.
/// </summary>
public sealed class LocateOneItemRule : IVerificationRule
{
    /// <summary>
    /// Evaluate the rule over the run context. Returns null when the scenario
    /// carries no expected identities (rule not applicable), otherwise a
    /// verdict of evidence_missing, target_page_identity_verified,
    /// target_action_not_executed or target_page_identity_not_verified.
    /// </summary>
    public VerifyVerdict? Evaluate(VerificationContext context)
    {
        var expected = context.ExpectedPageIdentities;
        if (expected.Count == 0)
            return null;  // Not applicable — no identities to check

        if (context.LastAnalysisRow is null)
        {
            // Evidence missing — check if pipeline failure caused it
            var attribution = context.Issues.Any(i =>
                i.Summary.Contains("asset_write_failed", StringComparison.OrdinalIgnoreCase))
                ? "pipeline failure"
                : "no analysis output";
            return new VerifyVerdict(
                "evidence_missing",
                "high",
                null,
                $"No analysis.jsonl rows found. Attribution: {attribution}.");
        }

        // D-201 identity fallback: CurrentPath may be empty (LocalVisionProvider quirk).
        // Primary identity = last level-1 menu name; fallback = first Items[].Name matching expected.
        var finalIdentity = context.LastAnalysisRow.Level1MenuNames.LastOrDefault();
        if (string.IsNullOrWhiteSpace(finalIdentity))
        {
            // Fallback: match any expected identity text against the item names in the last row
            finalIdentity = context.LastAnalysisRow.Items
                .Select(item => item.Name)
                .FirstOrDefault(name => expected.Any(
                    candidate => IdentityMatches(name, candidate)));
        }

        var identityMatched = !string.IsNullOrWhiteSpace(finalIdentity)
            && expected.Any(candidate => IdentityMatches(finalIdentity, candidate));

        if (context.TargetActionExecuted && identityMatched)
        {
            return new VerifyVerdict(
                "target_page_identity_verified",
                "high",
                null,
                $"Final identity '{finalIdentity}' matches expected. Target action executed successfully.");
        }

        if (!context.TargetActionExecuted)
        {
            return new VerifyVerdict(
                "target_action_not_executed",
                "high",
                null,
                $"The target action did not execute successfully. Completion reason: {context.CompletionReason ?? "unknown"}.");
        }

        return new VerifyVerdict(
            "target_page_identity_not_verified",
            "high",
            null,
            $"Post-action page identity '{finalIdentity ?? "<empty>"}' did not match any expected identity.");
    }

    /// <summary>
    /// Whitespace- and case-insensitive identity match: exact equality, or one
    /// side contained in the other (D-201 containment fallback).
    /// </summary>
    private static bool IdentityMatches(string actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(expected))
            return false;

        var normalizedActual = Normalize(actual);
        var normalizedExpected = Normalize(expected);
        return string.Equals(normalizedActual, normalizedExpected, StringComparison.Ordinal)
               || normalizedActual.Contains(normalizedExpected, StringComparison.Ordinal)
               || normalizedExpected.Contains(normalizedActual, StringComparison.Ordinal);
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Trim().ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
