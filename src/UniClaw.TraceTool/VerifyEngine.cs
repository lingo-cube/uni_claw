namespace UniClaw.TraceTool;

/// <summary>
/// Rule engine shared by the verify command. Evaluates a list of
/// <see cref="IVerificationRule"/>s over a <see cref="VerificationContext"/>.
/// MVP: <see cref="LocateOneItemRule"/> (D-201 semantics).
/// </summary>
public static class VerifyEngine
{
    /// <summary>
    /// Evaluate all rules. The first rule that returns a non-null verdict wins
    /// (rules are evaluated in order). If no rule applies, returns evidence_missing.
    /// </summary>
    public static VerifyResult Verify(VerificationContext context)
    {
        var rules = new IVerificationRule[]
        {
            new LocateOneItemRule(),
        };

        foreach (var rule in rules)
        {
            var verdict = rule.Evaluate(context);
            if (verdict is null)
                continue;

            var status = verdict.Cause switch
            {
                "target_page_identity_verified" => "success",
                "evidence_missing" => "evidence_missing",
                _ => "failure",
            };

            var evidence = new List<VerifyEvidence>
            {
                new("final_identity",
                    null,
                    $"analysis.jsonl last row identity='{context.LastAnalysisRow?.Level1MenuNames.LastOrDefault() ?? "<none>"}'"),
                new("expected_identities",
                    null,
                    string.Join(" / ", context.ExpectedPageIdentities)),
                new("target_action_executed",
                    null,
                    context.TargetActionExecuted ? "Target action executed successfully." : "Target action did not execute."),
            };

            return new VerifyResult(
                context.RunId,
                status,
                verdict,
                evidence,
                new VerifyArtifactPaths([], null));
        }

        // No rule applied
        return new VerifyResult(
            context.RunId,
            "evidence_missing",
            new VerifyVerdict("evidence_missing", "high", null, "No verification rule could be applied."),
            [],
            new VerifyArtifactPaths([], null));
    }
}
