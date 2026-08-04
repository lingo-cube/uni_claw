using UniClaw.TraceTool;
using Xunit;

namespace UniClaw.TraceTool.Tests;

/// <summary>
/// Task 3.6 — verification rule engine tests. Exercises VerifyEngine.Verify and
/// LocateOneItemRule (D-201 semantics) over in-memory VerificationContexts:
/// identity match success, Level1MenuNames-empty fallback to Items[].Name,
/// identity mismatch, target action not executed, missing evidence, and the
/// no-criteria not-applicable case (rule returns null).
/// </summary>
public sealed class VerifyEngineTests
{
    [Fact]
    public void Verify_Success_WhenIdentityMatchesAndTargetActionExecuted()
    {
        var context = new VerificationContext
        {
            RunId = "test-run-1",
            ExpectedPageIdentities = new[] { "About device" },
            LastAnalysisRow = new AnalysisRow
            {
                AnalyzedAt = "2026-08-04T00:00:00.000Z",
                ItemCount = 1,
                Items = new[]
                {
                    new AnalysisItemDto { Name = "About device", Type = "text", X = 0.5, Y = 0.3, ExpectedAction = "click" }
                },
                Level1MenuNames = new[] { "About device" }
            },
            TargetActionExecuted = true,
            CompletionReason = "target_found",
        };

        var result = VerifyEngine.Verify(context);

        Assert.Equal("success", result.Status);
        Assert.Equal("target_page_identity_verified", result.Verdict.Cause);
        Assert.Equal("high", result.Verdict.Confidence);
    }

    [Fact]
    public void Verify_IdentityFallback_WhenLevel1MenuNamesEmptyButItemsMatch()
    {
        var context = new VerificationContext
        {
            RunId = "test-run-2",
            ExpectedPageIdentities = new[] { "About device" },
            LastAnalysisRow = new AnalysisRow
            {
                AnalyzedAt = "2026-08-04T00:00:00.000Z",
                ItemCount = 1,
                Items = new[]
                {
                    new AnalysisItemDto { Name = "About device", Type = "text", X = 0.5, Y = 0.3, ExpectedAction = "click" }
                },
                Level1MenuNames = Array.Empty<string>()  // empty → fallback to Items[].Name
            },
            TargetActionExecuted = true,
            CompletionReason = "target_found",
        };

        var result = VerifyEngine.Verify(context);

        Assert.Equal("success", result.Status);
        Assert.Equal("target_page_identity_verified", result.Verdict.Cause);
    }

    [Fact]
    public void Verify_NotVerified_WhenIdentityDoesNotMatch()
    {
        var context = new VerificationContext
        {
            RunId = "test-run-3",
            ExpectedPageIdentities = new[] { "About device" },
            LastAnalysisRow = new AnalysisRow
            {
                AnalyzedAt = "2026-08-04T00:00:00.000Z",
                ItemCount = 1,
                Items = new[]
                {
                    new AnalysisItemDto { Name = "Wi-Fi", Type = "text", X = 0.5, Y = 0.3, ExpectedAction = "click" }
                },
                Level1MenuNames = new[] { "Settings" }
            },
            TargetActionExecuted = true,
            CompletionReason = "target_found",
        };

        var result = VerifyEngine.Verify(context);

        Assert.Equal("failure", result.Status);
        Assert.Equal("target_page_identity_not_verified", result.Verdict.Cause);
    }

    [Fact]
    public void Verify_TargetActionNotExecuted()
    {
        var context = new VerificationContext
        {
            RunId = "test-run-4",
            ExpectedPageIdentities = new[] { "About device" },
            LastAnalysisRow = new AnalysisRow
            {
                AnalyzedAt = "2026-08-04T00:00:00.000Z",
                ItemCount = 1,
                Items = new[]
                {
                    new AnalysisItemDto { Name = "About device", Type = "text", X = 0.5, Y = 0.3, ExpectedAction = "click" }
                },
                Level1MenuNames = new[] { "About device" }
            },
            TargetActionExecuted = false,
            CompletionReason = "max_steps",
        };

        var result = VerifyEngine.Verify(context);

        Assert.Equal("failure", result.Status);
        Assert.Equal("target_action_not_executed", result.Verdict.Cause);
    }

    [Fact]
    public void Verify_EvidenceMissing_WhenNoAnalysisRow()
    {
        var context = new VerificationContext
        {
            RunId = "test-run-5",
            ExpectedPageIdentities = new[] { "About device" },
            LastAnalysisRow = null,
            TargetActionExecuted = false,
            CompletionReason = "max_steps",
        };

        var result = VerifyEngine.Verify(context);

        Assert.Equal("evidence_missing", result.Status);
        Assert.Equal("evidence_missing", result.Verdict.Cause);
    }

    [Fact]
    public void LocateOneItemRule_ReturnsNull_WhenNoExpectedIdentities()
    {
        var rule = new LocateOneItemRule();
        var context = new VerificationContext
        {
            RunId = "test-run-6",
            ExpectedPageIdentities = Array.Empty<string>(),
            LastAnalysisRow = new AnalysisRow(),
        };

        var verdict = rule.Evaluate(context);

        Assert.Null(verdict);
    }
}
