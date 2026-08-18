using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Model;

/// <summary>
/// Narrow raw structured Android UI facts for one interaction-capable node.
/// This is external-world evidence, not a semantic navigation claim.
/// </summary>
/// <param name="Class">Android view/widget class, e.g. android.widget.LinearLayout.</param>
/// <param name="ResourceId">Android resource-id, e.g. android:id/title or com.android.settings:id/switchWidget.</param>
/// <param name="Clickable">Raw clickable flag.</param>
/// <param name="Checkable">Raw checkable flag.</param>
/// <param name="Checked">Raw checked flag when checkable.</param>
/// <param name="Enabled">Raw enabled flag.</param>
/// <param name="Focusable">Raw focusable flag.</param>
/// <param name="Bounds">Normalized bounds of the structured node.</param>
/// <param name="TitleText">Title text if this node or a directly relevant child carries android:id/title.</param>
/// <param name="SummaryText">Summary text if this node or a directly relevant child carries android:id/summary.</param>
/// <param name="HasSwitchChild">True when the interaction container contains a Switch/checkable control child.</param>
/// <param name="ContentDescription">Raw content-desc when present.</param>
/// <param name="SourceNodeIdentity">Deterministic node identity used for provenance/correlation.</param>
public sealed record StructuredElementEvidence(
    string? Class,
    string? ResourceId,
    bool? Clickable,
    bool? Checkable,
    bool? Checked,
    bool? Enabled,
    bool? Focusable,
    ElementBounds? Bounds,
    string? TitleText = null,
    string? SummaryText = null,
    bool? HasSwitchChild = null,
    string? ContentDescription = null,
    string? SourceNodeIdentity = null);
