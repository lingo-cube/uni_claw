using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.StateMachine;

namespace UniClaw.Core.Tests.StateMachine;

/// <summary>
/// Mock IVisionProvider for handler testing.
/// Returns configurable NextResult; tracks CallCount.
/// </summary>
public sealed class MockVisionProvider : IVisionProvider
{
    /// <summary>Predefined PageAnalysis to return (null = no analysis available)</summary>
    public PageAnalysis? NextResult { get; set; }

    /// <summary>Number of times GetCurrentPageAnalysisAsync was called</summary>
    public int CallCount { get; private set; }

    public Task<PageAnalysis?> GetCurrentPageAnalysisAsync(CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(NextResult);
    }
}
