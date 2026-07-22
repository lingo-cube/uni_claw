using System.Collections.Immutable;
using UniClaw.Core.Domain;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// TextUnderstandingRequest — 文本理解请求。
/// 对齐 Python: parse_instruction capability input。
/// </summary>
public sealed record class TextUnderstandingRequest
{
    /// <summary>待理解的文本 (非空)</summary>
    public string Text { get; init; }

    /// <summary>可选上下文 (页面名、场景描述等)</summary>
    public string? Context { get; init; }

    /// <summary>
    /// 构造 TextUnderstandingRequest — Text 非空校验 fail-fast。
    /// </summary>
    public TextUnderstandingRequest(string Text, string? Context = null)
    {
        if (string.IsNullOrWhiteSpace(Text))
            throw new DomainValidationException(nameof(Text), Text);
        this.Text = Text;
        this.Context = Context;
    }
}

/// <summary>
/// TextUnderstandingResult — 文本理解结果。
/// 对齐 Python: parse_instruction capability output。
/// Confidence 0-1 范围校验 fail-fast。
/// </summary>
public sealed record class TextUnderstandingResult
{
    /// <summary>文本分类</summary>
    public string Category { get; init; }

    /// <summary>置信度 (0-1)</summary>
    public double Confidence { get; init; }

    /// <summary>提取的实体列表</summary>
    public ImmutableArray<string> Entities { get; init; }

    /// <summary>可选摘要</summary>
    public string? Summary { get; init; }

    /// <summary>
    /// 构造 TextUnderstandingResult — Confidence 0-1 校验 fail-fast。
    /// </summary>
    public TextUnderstandingResult(
        string Category,
        double Confidence,
        ImmutableArray<string> Entities,
        string? Summary = null)
    {
        if (Confidence < 0.0 || Confidence > 1.0)
            throw new DomainValidationException(nameof(Confidence), Confidence);
        this.Category = Category;
        this.Confidence = Confidence;
        this.Entities = Entities;
        this.Summary = Summary;
    }
}
