using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// IUniBrain — 统一 AI 服务 facade。
/// 引擎和 Handler 注入此接口，通过子接口访问各能力。
/// Hybrid facade + ISP: 对外统一注入点，内部 3 子接口各自独立。
/// </summary>
public interface IUniBrain
{
    /// <summary>页面感知+验证能力: "当前屏幕是什么？是期望页面吗？"</summary>
    IPageAnalyzer PageAnalyzer { get; }

    /// <summary>遍历决策能力: "遍历引擎该怎么做？"</summary>
    ITraversalAdvisor Advisor { get; }

    /// <summary>文本理解能力: "这段文本的含义是什么？"</summary>
    ITextUnderstanding Text { get; }
}
