using System;
using System.Collections.Immutable;

namespace UniClaw.Core.Domain.Models.Content;

/// <summary>
/// 访问指纹，用于追踪已访问元素。PRD §5.2 ported from content_models.py VisitFingerprint(BaseModel)。
/// </summary>
public sealed record class VisitFingerprint
{
    /// <summary>一级菜单名</summary>
    public string Level1 { get; init; }

    /// <summary>二级菜单名</summary>
    public string Level2 { get; init; }

    /// <summary>项名称</summary>
    public string ItemName { get; init; }

    /// <param name="Level1">一级菜单名</param>
    /// <param name="Level2">二级菜单名</param>
    /// <param name="ItemName">项名称</param>
    public VisitFingerprint(string Level1, string Level2, string ItemName)
    {
        this.Level1 = Level1 ?? string.Empty;
        this.Level2 = Level2 ?? string.Empty;
        this.ItemName = ItemName ?? string.Empty;
    }

    /// <summary>字符串表示（用于集合成员判断）</summary>
    public override string ToString() => $"{Level1}|{Level2}|{ItemName}";

    /// <summary>从字符串创建指纹</summary>
    public static VisitFingerprint FromString(string value)
    {
        var parts = value.Split('|');
        if (parts.Length != 3)
            throw new DomainValidationException(nameof(VisitFingerprint), value);
        return new VisitFingerprint(Level1: parts[0], Level2: parts[1], ItemName: parts[2]);
    }
}

/// <summary>
/// 内容树节点。PRD §5.2 ported from content_models.py ContentNode(BaseModel)。
/// </summary>
public sealed record class ContentNode
{
    /// <summary>节点ID</summary>
    public string Id { get; init; }

    /// <summary>标题</summary>
    public string Title { get; init; }

    /// <summary>层级</summary>
    public int Level { get; init; }

    /// <summary>父节点ID</summary>
    public string? ParentId { get; init; }

    /// <summary>子节点ID列表</summary>
    public ImmutableArray<string> Children { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>坐标</summary>
    public Coordinate? Coordinate { get; init; }

    /// <summary>节点类型 (item/popup/jump/no_feedback)</summary>
    public string NodeType { get; init; }

    /// <summary>描述</summary>
    public string? Description { get; init; }

    /// <summary>是否已访问</summary>
    public bool Visited { get; init; }

    /// <param name="Id">节点ID</param>
    /// <param name="Title">标题</param>
    /// <param name="Level">层级</param>
    /// <param name="ParentId">父节点ID</param>
    /// <param name="Children">子节点ID列表</param>
    /// <param name="Coordinate">坐标</param>
    /// <param name="NodeType">节点类型</param>
    /// <param name="Description">描述</param>
    /// <param name="Visited">是否已访问</param>
    public ContentNode(
        string Id,
        string Title,
        int Level,
        string? ParentId = null,
        ImmutableArray<string> Children = default,
        Coordinate? Coordinate = null,
        string NodeType = "item",
        string? Description = null,
        bool Visited = false)
    {
        this.Id = Id ?? string.Empty;
        this.Title = Title ?? string.Empty;
        this.Level = Level;
        this.ParentId = ParentId;
        this.Children = Children.IsDefault ? ImmutableArray<string>.Empty : Children;
        this.Coordinate = Coordinate;
        this.NodeType = NodeType ?? "item";
        this.Description = Description;
        this.Visited = Visited;
    }

    /// <summary>
    /// 转为 markdown 表示（对齐 Python ContentNode.to_markdown）。
    /// 按层级缩进输出 "{id}. {title}({node_type})"，node_type=="item" 时省略后缀。
    /// includeChildren 仅为 API 对齐——子节点为 ID 列表，其渲染由树遍历处理（同 Python）。
    /// </summary>
    public string ToMarkdown(bool includeChildren = true)
    {
        var indent = new string(' ', Math.Max(0, 2 * (Level - 1)));
        var typeSuffix = NodeType != "item" ? $" ({NodeType})" : string.Empty;
        return $"{indent}{Id}. {Title}{typeSuffix}\n";
    }
}
