using UniClaw.Host;
using UniClaw.Kernel.Perception.UiHierarchy;
using Xunit;

namespace UniClaw.Host.Tests;

/// <summary>
/// PER-013 Slice B：legacy XML → UiHierarchyObservation v1 typed 解析。
/// 执法点：missing≠false（A-5 typed 修正）、bounds 不造默认值、checked
/// collapsed 默认（M-02）、parent/child 保留（不再 Descendants 展平）、
/// password 脱敏（F-E2）、空串文本（F-B1）、未知属性忽略（F-E1）。
/// </summary>
public sealed class UiHierarchyTypedParseTests
{
    private const string Fixture = """
        <hierarchy rotation="0">
          <node index="0" text="" resource-id="" class="android.widget.FrameLayout" package="com.android.settings" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" selected="false" password="false" bounds="[0,0][1080,1920]">
            <node index="0" text="" resource-id="com.android.settings:id/wifi_switch" class="android.widget.Switch" package="com.android.settings" checkable="true" checked="true" clickable="true" enabled="true" focusable="true" focused="false" scrollable="false" selected="false" password="false" bounds="[940,300][1040,360]"/>
            <node index="1" text="Network &amp; internet" resource-id="android:id/title" class="android.widget.TextView" package="com.android.settings" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" selected="false" password="false" bounds="[40,280][600,340]"/>
          </node>
        </hierarchy>
        """;

    private static readonly DateTimeOffset Capture = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    private static UiAutomatorDump.UiHierarchyParseContext Context(
        HierarchyCapabilities? capabilities = null,
        CheckedExactProof? exactProof = null) =>
        new(
            CaptureId: "cap-typed-1",
            CaptureTimestamp: Capture,
            DeviceId: "emulator-5554",
            SessionCorrelation: "run-typed-1",
            AndroidApiLevel: 35,
            ObservationCycleId: "cycle-1",
            CaptureDuration: TimeSpan.FromSeconds(1.2),
            Capabilities: capabilities,
            ExactProof: exactProof);

    private static UiNodeObservation SingleNode(string nodeXml) =>
        UiAutomatorDump.ParseHierarchyObservation(
            $"<hierarchy>{nodeXml}</hierarchy>", Context()).Observation!.Nodes[0];

    [Fact]
    public void CompleteHierarchy_OutcomeComplete_MetadataComplete()
    {
        var result = UiAutomatorDump.ParseHierarchyObservation(Fixture, Context());

        Assert.Equal(UiHierarchyCaptureOutcome.Complete, result.Outcome);
        var metadata = result.Observation!.Metadata;
        Assert.Equal("cap-typed-1", metadata.CaptureId);
        Assert.Equal(35, metadata.AndroidApiLevel);
        Assert.Equal(UiHierarchyAcquirerKind.LegacyUiAutomatorXml, metadata.AcquirerKind);
        Assert.Equal(UiHierarchyFormat.UiAutomatorXml, metadata.HierarchyFormat);
        Assert.Equal(TimeSpan.FromSeconds(1.2), metadata.CaptureDuration);
        Assert.True(metadata.IsValid);
        Assert.Equal(3, result.Observation.Nodes.Count);
    }

    [Fact]
    public void ParentChildHierarchy_Preserved_NotFlattened()
    {
        var result = UiAutomatorDump.ParseHierarchyObservation(Fixture, Context());
        var nodes = result.Observation!.Nodes;

        // DFS pre-order：root=0，子节点 1、2；子节点 parent = root 的 OccurrenceRef
        Assert.Equal(new OccurrenceRef("cap-typed-1", 0), nodes[0].OccurrenceRef);
        Assert.Null(nodes[0].ParentOccurrenceRef);
        Assert.Equal(new OccurrenceRef("cap-typed-1", 0), nodes[1].ParentOccurrenceRef);
        Assert.Equal(new OccurrenceRef("cap-typed-1", 0), nodes[2].ParentOccurrenceRef);
        // SiblingOrder 来自 index 属性（occurrence 特征，非 identity）
        Assert.Equal(0, nodes[1].SiblingOrder);
        Assert.Equal(1, nodes[2].SiblingOrder);
    }

    [Fact]
    public void CheckedTrue_MapsChecked_CollapsedDefault()
    {
        var node = SingleNode("""
            <node index="0" text="" resource-id="id/switch" class="android.widget.Switch" package="p" checkable="true" checked="true" clickable="true" enabled="true" focusable="true" focused="false" scrollable="false" selected="false" password="false" bounds="[0,0][100,100]"/>
            """);

        Assert.Equal(FieldState.Observed, node.Checked.State);
        Assert.Equal(CheckedState.Checked, node.Checked.Value);
    }

    [Fact]
    public void CheckedFalse_CollapsedDefault_IsUnknownNeverUnchecked()
    {
        // PER-012 M-02 / PER-009 A-5 typed 修正：不再 false→off 折叠
        var node = SingleNode("""
            <node index="0" text="" resource-id="id/switch" class="android.widget.Switch" package="p" checkable="true" checked="false" clickable="true" enabled="true" focusable="true" focused="false" scrollable="false" selected="false" password="false" bounds="[0,0][100,100]"/>
            """);

        Assert.Equal(FieldState.Unknown, node.Checked.State);
        Assert.Equal(CheckedSemantics.PartialUnrepresentableReason, node.Checked.Reason);
        Assert.False(node.Checked.TryGetObserved(out _));
    }

    [Fact]
    public void CheckedFalse_ExactProof_MapsUnchecked()
    {
        var proof = new CheckedExactProof(
            "contract:fixture/two-state", "capability:fixture/exact", "fixture:two-state");
        var node = UiAutomatorDump.ParseHierarchyObservation(
            """
            <hierarchy><node index="0" text="" resource-id="id/s" class="android.widget.Switch" package="p" checkable="true" checked="false" clickable="true" enabled="true" focusable="true" focused="false" scrollable="false" selected="false" password="false" bounds="[0,0][100,100]"/></hierarchy>
            """,
            Context(capabilities: new HierarchyCapabilities(
                HierarchyCapability.SemanticText | HierarchyCapability.CheckedBooleanExact),
                exactProof: proof)).Observation!.Nodes[0];

        Assert.Equal(FieldState.Observed, node.Checked.State);
        Assert.Equal(CheckedState.Unchecked, node.Checked.Value);
    }

    [Fact]
    public void CheckedAttributeMissing_IsUnknownAttributeMissing_DistinctFromFalse()
    {
        var node = SingleNode("""
            <node index="0" text="" resource-id="id/s" class="android.widget.Switch" package="p" checkable="true" clickable="true" enabled="true" focusable="true" focused="false" scrollable="false" selected="false" password="false" bounds="[0,0][100,100]"/>
            """);

        Assert.Equal(FieldState.Unknown, node.Checked.State);
        Assert.Equal("attribute-missing", node.Checked.Reason);
        Assert.NotEqual(CheckedSemantics.PartialUnrepresentableReason, node.Checked.Reason);
    }

    [Fact]
    public void CheckedPartial_TriStateCapability_MapsPartial_CollapsedCannot()
    {
        var xml = """
            <hierarchy><node index="0" text="" resource-id="id/s" class="android.widget.Switch" package="p" checkable="true" checked="partial" clickable="true" enabled="true" focusable="true" focused="false" scrollable="false" selected="false" password="false" bounds="[0,0][100,100]"/></hierarchy>
            """;

        var collapsed = UiAutomatorDump.ParseHierarchyObservation(xml, Context()).Observation!.Nodes[0];
        Assert.Equal(FieldState.Unknown, collapsed.Checked.State);
        Assert.Equal(CheckedSemantics.PartialUnrepresentableReason, collapsed.Checked.Reason);

        var triState = UiAutomatorDump.ParseHierarchyObservation(
            xml,
            Context(capabilities: new HierarchyCapabilities(
                HierarchyCapability.SemanticText | HierarchyCapability.CheckedTriState))).Observation!.Nodes[0];
        Assert.Equal(FieldState.Observed, triState.Checked.State);
        Assert.Equal(CheckedState.Partial, triState.Checked.Value);
    }

    [Fact]
    public void MissingBooleanAttribute_IsUnknown_NotObservedFalse()
    {
        var node = SingleNode("""
            <node index="0" text="" resource-id="id/s" class="android.widget.TextView" package="p" checkable="false" checked="false" clickable="false" focusable="false" focused="false" scrollable="false" selected="false" password="false" bounds="[0,0][100,100]"/>
            """);

        // enabled 属性缺席 → Unknown，不是 false
        Assert.Equal(FieldState.Unknown, node.Enabled.State);
        Assert.Equal("attribute-missing", node.Enabled.Reason);
        Assert.False(node.Enabled.TryGetObserved(out _));
        // 在场的 false 仍是 Observed(false)
        Assert.Equal(FieldState.Observed, node.Checkable.State);
        Assert.False(node.Checkable.Value);
    }

    [Fact]
    public void MalformedBooleanValue_IsUnknownNotGuess()
    {
        var node = SingleNode("""
            <node index="0" text="" resource-id="id/s" class="android.widget.TextView" package="p" checkable="maybe" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" selected="false" password="false" bounds="[0,0][100,100]"/>
            """);

        Assert.Equal(FieldState.Unknown, node.Checkable.State);
        Assert.Equal("malformed-field", node.Checkable.Reason);
    }

    [Fact]
    public void MalformedBounds_IsUnknown_NotFabricatedZero()
    {
        var node = SingleNode("""
            <node index="0" text="" resource-id="id/s" class="android.widget.TextView" package="p" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" selected="false" password="false" bounds="garbage"/>
            """);

        Assert.Equal(FieldState.Unknown, node.Bounds.State);
        Assert.Equal("malformed-bounds", node.Bounds.Reason);
        Assert.False(node.Bounds.TryGetObserved(out _));
    }

    [Fact]
    public void InvertedBounds_IsUnknown()
    {
        var node = SingleNode("""
            <node index="0" text="" resource-id="id/s" class="android.widget.TextView" package="p" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" selected="false" password="false" bounds="[500,500][100,100]"/>
            """);

        Assert.Equal(FieldState.Unknown, node.Bounds.State);
        Assert.Equal("malformed-bounds", node.Bounds.Reason);
    }

    [Fact]
    public void PasswordFieldTrue_RedactsTextAndContentDesc_WithProvenance()
    {
        var node = SingleNode("""
            <node index="0" text="secret-password-value" resource-id="id/pwd" class="android.widget.EditText" package="p" content-desc="password field" checkable="false" checked="false" clickable="true" enabled="true" focusable="true" focused="true" scrollable="false" selected="false" password="true" bounds="[0,0][400,120]"/>
            """);

        // F-E2：password=true → Text/ContentDesc 脱敏，provenance 记 normalization
        Assert.True(node.Password.TryGetObserved(out var isPassword) && isPassword);
        Assert.Equal("[redacted:password]", node.Text.Value);
        Assert.Equal("redact:password", node.Text.Provenance!.Normalization);
        Assert.Equal("[redacted:password]", node.ContentDescription.Value);
        Assert.Equal("redact:password", node.ContentDescription.Provenance!.Normalization);
        // 非敏感字段不脱敏
        Assert.Equal("id/pwd", node.ResourceId.Value);
    }

    [Fact]
    public void EmptyText_IsObservedEmpty_NotUnknown()
    {
        var node = SingleNode("""
            <node index="0" text="" resource-id="id/s" class="android.widget.TextView" package="p" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" selected="false" password="false" bounds="[0,0][100,100]"/>
            """);

        Assert.Equal(FieldState.Observed, node.Text.State);
        Assert.Equal(string.Empty, node.Text.Value);
    }

    [Fact]
    public void TextEntityDecoded()
    {
        var node = SingleNode("""
            <node index="0" text="Network &amp; internet" resource-id="android:id/title" class="android.widget.TextView" package="p" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" selected="false" password="false" bounds="[0,0][100,100]"/>
            """);

        Assert.Equal("Network & internet", node.Text.Value);
    }

    [Fact]
    public void DuplicateResourceId_TwoDistinctOccurrences()
    {
        var result = UiAutomatorDump.ParseHierarchyObservation("""
            <hierarchy>
              <node index="0" text="" resource-id="id/dup" class="android.widget.Switch" package="p" checkable="true" checked="true" clickable="true" enabled="true" focusable="true" focused="false" scrollable="false" selected="false" password="false" bounds="[0,0][100,100]"/>
              <node index="1" text="" resource-id="id/dup" class="android.widget.Switch" package="p" checkable="true" checked="false" clickable="true" enabled="true" focusable="true" focused="false" scrollable="false" selected="false" password="false" bounds="[0,200][100,300]"/>
            </hierarchy>
            """, Context());
        var nodes = result.Observation!.Nodes;

        Assert.Equal(2, nodes.Count);
        Assert.Equal("id/dup", nodes[0].ResourceId.Value);
        Assert.Equal("id/dup", nodes[1].ResourceId.Value);
        Assert.NotEqual(nodes[0].OccurrenceRef, nodes[1].OccurrenceRef);
    }

    [Fact]
    public void UnknownAttribute_Ignored_NoValueNoError()
    {
        // F-E1：未知属性名不破 parser、不猜值、不进 ObservedValue
        var node = SingleNode("""
            <node index="0" oem-private-field="42" text="t" resource-id="id/s" class="android.widget.TextView" package="p" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" selected="false" password="false" bounds="[0,0][100,100]"/>
            """);

        Assert.Equal(FieldState.Observed, node.Text.State);
        Assert.Equal("t", node.Text.Value);
    }

    [Fact]
    public void CapabilitiesAbsentFields_AreUnsupported()
    {
        var node = SingleNode("""
            <node index="0" text="t" resource-id="id/s" class="android.widget.TextView" package="p" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" selected="false" password="false" bounds="[0,0][100,100]"/>
            """);

        // legacy XML 默认不声明 hint/visibility → Unsupported（≠ Unknown）
        Assert.Equal(FieldState.Unsupported, node.Hint.State);
        Assert.Equal(FieldState.Unsupported, node.VisibleToUser.State);
    }

    [Fact]
    public void EmptyHierarchy_EmptyOutcome_ZeroNodes()
    {
        var result = UiAutomatorDump.ParseHierarchyObservation("<hierarchy></hierarchy>", Context());

        Assert.Equal(UiHierarchyCaptureOutcome.Empty, result.Outcome);
        Assert.Empty(result.Observation!.Nodes);
        Assert.Empty(result.Observation.Windows);
    }

    [Fact]
    public void MalformedXml_MalformedOutcome_NoPartialNodes()
    {
        var result = UiAutomatorDump.ParseHierarchyObservation("<hierarchy><node", Context());

        Assert.Equal(UiHierarchyCaptureOutcome.Malformed, result.Outcome);
        Assert.Null(result.Observation);
        Assert.StartsWith("xml-structure-unparseable", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void RootNotHierarchy_MalformedOutcome()
    {
        var result = UiAutomatorDump.ParseHierarchyObservation("<root><node/></root>", Context());

        Assert.Equal(UiHierarchyCaptureOutcome.Malformed, result.Outcome);
        Assert.StartsWith("xml-structure-unparseable:root-not-hierarchy", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void FieldProvenance_CarriesCaptureAndSourceField()
    {
        var node = SingleNode("""
            <node index="0" text="t" resource-id="id/s" class="android.widget.TextView" package="p" checkable="false" checked="false" clickable="false" enabled="true" focusable="false" focused="false" scrollable="false" selected="false" password="false" bounds="[0,0][100,100]"/>
            """);

        Assert.Equal("cap-typed-1", node.Text.Provenance!.CaptureId);
        Assert.Equal("text", node.Text.Provenance.SourceField);
        Assert.Equal("bounds", node.Bounds.Provenance!.SourceField);
    }
}
