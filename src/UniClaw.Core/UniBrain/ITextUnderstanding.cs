namespace UniClaw.Core.UniBrain;

/// <summary>
/// ITextUnderstanding — 文本理解能力。
/// 单一职责: "这段文本/指令的含义是什么？"
/// 对齐 Python: parse_instruction capability。
/// </summary>
public interface ITextUnderstanding
{
    /// <summary>理解文本 — 分类、置信度、实体提取</summary>
    Task<TextUnderstandingResult> UnderstandTextAsync(
        TextUnderstandingRequest request,
        CancellationToken ct = default);
}
