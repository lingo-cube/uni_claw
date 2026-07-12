using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;

namespace UniClaw.Core.StateMachine.Navigation;

/// <summary>
/// Navigation context read-only interface — DFS traversal state.
/// 只读属性暴露，mutation 方法只在 concrete class。
/// </summary>
public interface INavigationContext
{
    /// <summary>节点栈</summary>
    INodeStack NodeStack { get; }

    /// <summary>当前路径 (只读视图)</summary>
    IReadOnlyList<string> CurrentPath { get; }

    /// <summary>当前页面分析</summary>
    PageAnalysis? CurrentPageAnalysis { get; }

    /// <summary>当前指纹</summary>
    VisitFingerprint? CurrentFingerprint { get; }

    /// <summary>已访问的页面 (只读集合)</summary>
    IReadOnlySet<string> VisitedPages { get; }

    /// <summary>已访问的节点 (只读集合)</summary>
    IReadOnlySet<string> VisitedNodes { get; }

    /// <summary>已访问的子节点 (只读字典+只读嵌套集合)</summary>
    IReadOnlyDictionary<string, IReadOnlySet<string>> VisitedChildren { get; }

    /// <summary>已访问一级菜单</summary>
    IReadOnlySet<string> VisitedLevel1Menus { get; }

    /// <summary>已访问二级菜单</summary>
    IReadOnlySet<string> VisitedLevel2Menus { get; }

    /// <summary>页面树</summary>
    ContentNode? PageTree { get; }

    /// <summary>当前帧（节点）</summary>
    ITraversalNode? CurrentFrame { get; }
}
