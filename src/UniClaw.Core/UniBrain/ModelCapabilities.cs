namespace UniClaw.Core.UniBrain;

/// <summary>
/// ModelCapabilities — ModelRequest.Capability 与 IModelRouter 路由键的权威 capability 词汇表。
/// 对齐 Python capability 名；排除 verify_page_with_vision（C# IPageAnalyzer YAGNI）。
/// </summary>
public static class ModelCapabilities
{
    /// <summary>ITextUnderstanding — 将用户指令解析为结构化意图。</summary>
    public const string ParseInstruction = "parse_instruction";

    /// <summary>IPageAnalyzer.VerifyPageTypeAsync — 校验当前页面类型。</summary>
    public const string VerifyPageType = "verify_page_type";

    /// <summary>ITraversalAdvisor — 决策下一步动作。</summary>
    public const string DecideNextAction = "decide_next_action";

    /// <summary>ITraversalAdvisor — 评估屏幕安全性。</summary>
    public const string ScreenSafety = "screen_safety";

    /// <summary>IPageAnalyzer — 分析视觉布局。</summary>
    public const string AnalyzeVisual = "analyze_visual";
}
