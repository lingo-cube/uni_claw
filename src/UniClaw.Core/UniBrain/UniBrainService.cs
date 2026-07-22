namespace UniClaw.Core.UniBrain;

/// <summary>
/// UniBrainService — 纯组合容器 (sealed class, 非 record)。
/// 不做路由、不持有 IModelProvider、不持有配置。
/// 子接口实现通过构造器注入，组合由配置/DI 决定。
/// </summary>
public sealed class UniBrainService : IUniBrain
{
    /// <inheritdoc />
    public IPageAnalyzer PageAnalyzer { get; }

    /// <inheritdoc />
    public ITraversalAdvisor Advisor { get; }

    /// <inheritdoc />
    public ITextUnderstanding Text { get; }

    /// <summary>
    /// 构造 UniBrainService — 纯组合，无默认值。
    /// </summary>
    /// <param name="pageAnalyzer">页面感知+验证实现</param>
    /// <param name="advisor">遍历决策实现</param>
    /// <param name="text">文本理解实现</param>
    public UniBrainService(
        IPageAnalyzer pageAnalyzer,
        ITraversalAdvisor advisor,
        ITextUnderstanding text)
    {
        PageAnalyzer = pageAnalyzer ?? throw new ArgumentNullException(nameof(pageAnalyzer));
        Advisor = advisor ?? throw new ArgumentNullException(nameof(advisor));
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }
}
