using System.Collections.Immutable;
using UniClaw.Core.UniBrain;

namespace UniClaw.Core.Simulation;

/// <summary>
/// MockTextUnderstanding — 返回固定结果的 ITextUnderstanding 实现。
/// 对齐: PRD §9 mock 策略 — 返回 TextUnderstandingResult(Category="mock", Confidence=1.0, Entities=Empty)。
/// </summary>
public sealed class MockTextUnderstanding : ITextUnderstanding
{
    /// <inheritdoc />
    public Task<TextUnderstandingResult> UnderstandTextAsync(
        TextUnderstandingRequest request,
        CancellationToken ct = default)
    {
        return Task.FromResult(new TextUnderstandingResult(
            Category: "mock",
            Confidence: 1.0,
            Entities: ImmutableArray<string>.Empty));
    }
}
