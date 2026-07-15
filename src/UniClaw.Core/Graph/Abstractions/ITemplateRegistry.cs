using UniClaw.Core.Graph.Models;

namespace UniClaw.Core.Graph.Abstractions;

/// <summary>
/// 模板注册表接口
/// </summary>
public interface ITemplateRegistry
{
    /// <summary>
    /// 获取模板
    /// </summary>
    Template? GetTemplate(string templateId);

    /// <summary>
    /// 检查模板是否存在
    /// </summary>
    bool HasTemplate(string templateId);

    /// <summary>
    /// 获取所有模板ID
    /// </summary>
    IEnumerable<string> GetTemplateIds();

    /// <summary>
    /// 从文件加载模板
    /// </summary>
    Task LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 实例化模板
    /// </summary>
    TraversalNode? Instantiate(string templateId, Dictionary<string, object> context);
}
