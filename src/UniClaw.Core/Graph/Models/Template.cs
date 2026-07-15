using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.Graph.Models;

/// <summary>
/// 模板 - 可重用的节点模式
/// </summary>
/// <param name="TemplateId">模板ID</param>
/// <param name="NodeType">节点类型</param>
/// <param name="Operation">操作配置</param>
/// <param name="Precondition">前置条件配置</param>
/// <param name="ChildrenStrategy">子节点策略配置</param>
/// <param name="ErrorPolicy">错误策略配置</param>
/// <param name="ExitCondition">退出条件配置</param>
/// <param name="Meta">元数据</param>
public sealed record class Template(
    string TemplateId,
    NodeType NodeType,
    Dictionary<string, object> Operation,
    Dictionary<string, object>? Precondition = null,
    Dictionary<string, object>? ChildrenStrategy = null,
    Dictionary<string, object>? ErrorPolicy = null,
    Dictionary<string, object>? ExitCondition = null,
    Dictionary<string, object>? Meta = null);
