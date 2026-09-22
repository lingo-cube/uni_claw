using System.Xml;
using System.Xml.Linq;
using UniClaw.Host;
using UniClaw.Kernel.Evidence;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// PER-009 S2：uiautomator dump 解析器（纯函数，fixture 测；设备拉取
/// 为 PENDING-ENV）。字段角色断言对齐 mechanism.md 冻结表。
/// </summary>
public sealed class UiAutomatorDumpTests
{
    private const string Fixture = """
        <hierarchy rotation="0">
          <node index="0" text="" resource-id="" class="android.widget.FrameLayout" package="com.android.settings" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" selected="false" bounds="[0,0][1080,1920]">
            <node index="0" text="" resource-id="com.android.settings:id/wifi_switch" class="android.widget.Switch" package="com.android.settings" checkable="true" checked="false" clickable="true" enabled="true" focusable="true" focused="false" scrollable="false" selected="false" bounds="[940,300][1040,360]"/>
            <node index="1" text="Network &amp; internet" resource-id="android:id/title" class="android.widget.TextView" package="com.android.settings" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" selected="false" bounds="[40,280][600,340]"/>
            <node index="2" text="" resource-id="com.android.settings:id/wifi_switch" class="android.widget.Switch" package="com.android.settings" checkable="true" checked="true" clickable="true" enabled="true" focusable="true" focused="false" scrollable="false" selected="false" bounds="[940,500][1040,560]"/>
          </node>
        </hierarchy>
        """;

    private static DateTimeOffset Capture => new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    private static UiAutomatorDump.DumpResult Parse(string xml = Fixture) =>
        UiAutomatorDump.Parse(xml, Capture, ObservationContext.External);

    private static string? Value(UiAutomatorDump.DumpResult dump, string subject) =>
        dump.Claims.FirstOrDefault(c => c.Claim.Subject == subject)?.Claim.Value;

    [Fact]
    public void Parse_MapsAllFieldRolesToFrozenTable()
    {
        var dump = Parse();
        // 状态权威字段
        Assert.Equal("android.widget.Switch", Value(dump, "ui.node.wifi_switch.class"));
        Assert.Equal("true", Value(dump, "ui.node.wifi_switch.checkable"));
        Assert.Equal("false", Value(dump, "ui.node.wifi_switch.checked"));
        // 能力属性同样落证（checkable 是能力字段，非状态——D12）
        Assert.Equal("true", Value(dump, "ui.node.wifi_switch.clickable"));
        Assert.Equal("true", Value(dump, "ui.node.wifi_switch.enabled"));
        // 空间证据：bounds 归一为 "x1,y1,x2,y2"
        Assert.Equal("940,300,1040,360", Value(dump, "ui.node.wifi_switch.bounds"));
        // 语义文本（XML 实体解引用）
        Assert.Equal("Network & internet", Value(dump, "ui.node.title.text"));
        // 身份证据：原始 resource-id 全文保留
        Assert.Equal("com.android.settings:id/wifi_switch", Value(dump, "ui.node.wifi_switch.resource_id"));
    }

    [Fact]
    public void Parse_CheckedTriStateForwardCompat()
    {
        // fixture 中 checked="true" 恰出现一次（第三个节点）→ 注入三态值
        var xml = Fixture.Replace("checked=\"true\"", "checked=\"partial\"");
        var dump = Parse(xml);
        Assert.Contains(dump.Claims, c => c.Claim.Subject.StartsWith("ui.node.wifi_switch.") && c.Claim.Value == "partial");
    }

    [Fact]
    public void Parse_EmptyTree_IsOkEmpty_NotFailure()
    {
        var dump = UiAutomatorDump.Parse("<hierarchy></hierarchy>", Capture, ObservationContext.External);
        Assert.Empty(dump.Nodes);
        Assert.Empty(dump.Claims); // A8：缺检测不产观察
    }

    [Fact]
    public void Parse_MalformedXml_ThrowsFailClosed()
    {
        Assert.Throws<XmlException>(() =>
            UiAutomatorDump.Parse("<hierarchy><node", Capture, ObservationContext.External));
    }

    [Fact]
    public void ResolveUnique_DuplicateResourceId_ReturnsNull()
    {
        var dump = Parse();
        // fixture 故意放两个同名 wifi_switch（防同名错配的回归面）
        Assert.Null(UiAutomatorDump.ResolveUniqueByResourceId(dump, "wifi_switch"));
    }

    [Fact]
    public void ResolveUnique_SingleResourceId_Resolves()
    {
        var dump = Parse();
        var node = UiAutomatorDump.ResolveUniqueByResourceId(dump, "title");
        Assert.NotNull(node);
        Assert.Equal("android.widget.TextView", node!.Class);
    }

    [Fact]
    public void Parse_CheckedEmittedEvenWhenCheckableFalse()
    {
        // guard 属消费方（D12）：解析层对非 checkable 节点仍落 checked 证据
        var dump = Parse();
        Assert.Equal("false", Value(dump, "ui.node.title.checked"));
        Assert.Equal("false", Value(dump, "ui.node.title.checkable"));
    }

    [Fact]
    public void Parse_AllClaimsCarryProducerProvenanceAndKind()
    {
        var dump = Parse();
        Assert.NotEmpty(dump.Claims);
        foreach (var claim in dump.Claims)
        {
            Assert.Equal(IngressKind.Observation, claim.Kind);
            Assert.Equal(UiAutomatorDump.Producer, claim.Provenance!.Producer);
            Assert.Equal(Capture, claim.Provenance.CaptureTime);
            Assert.Contains("uiautomator:dump", claim.Provenance.TransformationLineage);
        }
    }
}
