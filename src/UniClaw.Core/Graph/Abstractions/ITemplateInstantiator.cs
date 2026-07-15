using UniClaw.Core.Graph.Models;

namespace UniClaw.Core.Graph.Abstractions;

/// <summary>
/// 模板实例化器接口 — 从模板定义生成 TraversalNode。
/// </summary>
public interface ITemplateInstantiator
{
    /// <summary>
    /// instantiate — 从模板 + 上下文 + 父路径生成 TraversalNode。
    /// </summary>
    TraversalNode Instantiate(
        Template template,
        Dictionary<string, object> context,
        List<string> parentPath);
}
