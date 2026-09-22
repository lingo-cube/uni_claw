using System.Xml.Linq;
using UniClaw.Kernel.Evidence;

namespace UniClaw.Host;

/// <summary>
/// uiautomator XML 树 → Observation claims（PER-009，producer=platform.uiautomator）。
/// 解析为纯函数（fixture 可测，replay 确定）；设备拉取（adb）ENVIRONMENT-gated。
/// 字段角色遵循 changes/PER-009/mechanism.md 冻结表：
///   状态权威 checked(∧checkable=true)/enabled/selected/focused；text=语义文本
///   （像素文字不在树里）；checkable/clickable/scrollable/focusable=能力属性
///   （checkable 是 checked 的 validity guard，非状态）；bounds=空间证据；
///   resource-id/class/package=身份证据。
/// 全部字段以 ui.node.* 私有命名空间落 claim（证据细节层）；共享层
/// （*.state 等）由消费方按 IdentityMatched 解析后映射，解析器不做裁决。
/// </summary>
public static class UiAutomatorDump
{
    public const string Producer = "platform.uiautomator";

    public sealed record NodeInfo(
        string LocalId,
        string? ResourceId,
        string Class,
        string? Package,
        string? Text,
        bool Checkable,
        string Checked, // "false" | "true" | "partial"（三态前向兼容，api35 实发布尔）
        bool Clickable,
        bool Enabled,
        bool Focusable,
        bool Focused,
        bool Scrollable,
        bool Selected,
        string Bounds); // "x1,y1,x2,y2"（px）

    public sealed record DumpResult(
        IReadOnlyList<NodeInfo> Nodes,
        IReadOnlyList<ObservationProposal> Claims);

    /// <summary>
    /// 解析 dump XML（纯函数）。空树 = OK_EMPTY：零 claims、非失败
    /// （A8：缺检测不产观察）。非法 XML → XmlException 显式抛出
    /// （fail-closed；transport/执行层应先分类超时与失败）。
    /// </summary>
    public static DumpResult Parse(string xml, DateTimeOffset captureTime, ObservationContext context)
    {
        ArgumentNullException.ThrowIfNull(xml);
        var root = XDocument.Parse(xml).Root ?? throw new InvalidOperationException("dump XML 无根节点（SchemaFailure，fail-closed）");
        var nodes = new List<NodeInfo>();
        var claims = new List<ObservationProposal>();
        var index = 0;
        foreach (var element in root.Descendants("node"))
        {
            var node = ToNodeInfo(element, index++);
            nodes.Add(node);
            AddNodeClaims(claims, node, captureTime, context);
        }
        return new DumpResult(nodes, claims);
    }

    /// <summary>
    /// IdentityMatched 辅助（D3）：按净化 resource-id 唯一解析节点。
    /// 零命中或多命中 → null（= 剥夺 XML 权威，非错误）。
    /// </summary>
    public static NodeInfo? ResolveUniqueByResourceId(DumpResult dump, string localId)
    {
        ArgumentNullException.ThrowIfNull(dump);
        NodeInfo? hit = null;
        var count = 0;
        foreach (var n in dump.Nodes)
        {
            if (n.ResourceId is null || Sanitize(n.ResourceId) != localId)
                continue;
            hit = n;
            count++;
            if (count > 1)
                return null;
        }
        return count == 1 ? hit : null;
    }

    internal static string Sanitize(string resourceId)
    {
        var tail = resourceId;
        var lastColon = tail.LastIndexOf(':');
        if (lastColon >= 0)
            tail = tail[(lastColon + 1)..];
        var lastSlash = tail.LastIndexOf('/');
        if (lastSlash >= 0)
            tail = tail[(lastSlash + 1)..];
        return tail;
    }

    private static NodeInfo ToNodeInfo(XElement e, int index)
    {
        var resourceId = (string?)e.Attribute("resource-id");
        var localId = string.IsNullOrEmpty(resourceId) ? $"node{index}" : Sanitize(resourceId);
        return new NodeInfo(
            LocalId: localId,
            ResourceId: resourceId,
            Class: (string?)e.Attribute("class") ?? string.Empty,
            Package: (string?)e.Attribute("package"),
            Text: (string?)e.Attribute("text"),
            Checkable: Bool(e, "checkable"),
            Checked: CheckedValue(e),
            Clickable: Bool(e, "clickable"),
            Enabled: Bool(e, "enabled"),
            Focusable: Bool(e, "focusable"),
            Focused: Bool(e, "focused"),
            Scrollable: Bool(e, "scrollable"),
            Selected: Bool(e, "selected"),
            Bounds: ParseBounds((string?)e.Attribute("bounds")));
    }

    private static string CheckedValue(XElement e)
    {
        var raw = (string?)e.Attribute("checked");
        return raw switch
        {
            "true" => "true",
            "partial" => "partial", // API 36+ 前向兼容通道（api35 实发布尔）
            _ => "false",
        };
    }

    private static bool Bool(XElement e, string name) =>
        (string?)e.Attribute(name) == "true";

    private static string ParseBounds(string? raw)
    {
        // uiautomator 格式："[x1,y1][x2,y2]" → "x1,y1,x2,y2"
        if (raw is null)
            return "0,0,0,0";
        var parts = raw.Replace("][", ",").Replace("[", "").Replace("]", "").Split(',');
        return parts.Length == 4 ? string.Join(",", parts) : "0,0,0,0";
    }

    private static void AddNodeClaims(
        List<ObservationProposal> claims, NodeInfo node, DateTimeOffset captureTime, ObservationContext context)
    {
        void Add(string field, string value)
        {
            var subject = $"ui.node.{node.LocalId}.{field}";
            claims.Add(new ObservationProposal(
                new ObservationClaim(subject, value),
                IngressKind.Observation,
                context,
                new Provenance(Producer, captureTime, $"scope:{subject}", new[] { "uiautomator:dump" })));
        }

        Add("class", node.Class);
        if (node.ResourceId is { Length: > 0 })
            Add("resource_id", node.ResourceId);
        if (node.Package is { Length: > 0 })
            Add("package", node.Package);
        if (node.Text is { Length: > 0 })
            Add("text", node.Text);
        // 状态权威字段（checked 的有效性 guard=checkable 由消费方判定——解析层全量落证）
        Add("checked", node.Checked);
        Add("checkable", node.Checkable ? "true" : "false");
        Add("clickable", node.Clickable ? "true" : "false");
        Add("enabled", node.Enabled ? "true" : "false");
        Add("focusable", node.Focusable ? "true" : "false");
        Add("focused", node.Focused ? "true" : "false");
        Add("scrollable", node.Scrollable ? "true" : "false");
        Add("selected", node.Selected ? "true" : "false");
        Add("bounds", node.Bounds);
    }
}
