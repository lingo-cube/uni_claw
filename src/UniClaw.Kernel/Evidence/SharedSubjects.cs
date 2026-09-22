namespace UniClaw.Kernel.Evidence;

/// <summary>
/// 共享 subject 层（PER-009 D7 / ADR-0027）：跨 producer 共用的 claim key，
/// 仅语义三件套；统一拼写由常量类承载，元素级 subject 永不共享（防假合并）。
/// 值域约定（半页语义纸的代码面）：*.state ∈ {on, off, partial}；
/// screen.frame 为 {role,state,b,f} JSON；ui.screen 为屏幕身份标识。
/// </summary>
public static class SharedSubjects
{
    /// <summary>屏幕身份（association 判别锚，ProductAssociationStrategy 消费）。</summary>
    public const string Screen = "ui.screen";

    /// <summary>执行帧（SpatialLocator 载荷）。</summary>
    public const string Frame = "screen.frame";

    /// <summary>状态 claim：{name}.state，值域 {on, off, partial}。</summary>
    public static string State(string name) => $"{name}.state";
}
