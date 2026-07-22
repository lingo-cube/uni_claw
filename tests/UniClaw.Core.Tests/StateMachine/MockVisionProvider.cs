using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.StateMachine;
using UniClaw.Core.UniBrain;

namespace UniClaw.Core.Tests.StateMachine;

/// <summary>
/// Mock IPageAnalyzer for handler testing.
/// Returns configurable NextResult; tracks CallCount.
/// </summary>
public sealed class MockVisionProvider : IPageAnalyzer
{
    /// <summary>Predefined PageAnalysis to return (null = no analysis available)</summary>
    public PageAnalysis? NextResult { get; set; }

    /// <summary>Number of times AnalyzeCurrentPageAsync was called</summary>
    public int CallCount { get; private set; }

    public Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(NextResult);
    }

    public Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default)
        => Task.FromResult<AppEntryPoint?>(new AppEntryPoint(targetApp, 0.5, 0.5));

    /// <inheritdoc />
    public Task<PageTypeVerification> VerifyPageTypeAsync(
        PageAnalysis pageAnalysis,
        string expectedType,
        string? expectedPageName = null,
        CancellationToken ct = default)
    {
        return Task.FromResult(new PageTypeVerification(
            IsMatch: false,
            Confidence: 0.0,
            ActualType: expectedType));
    }
}
