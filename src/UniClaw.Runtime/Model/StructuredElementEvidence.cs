using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Model;

/// <summary>
/// Narrow raw structured UI facts for one interaction-capable node.
/// This is external-world evidence, not a semantic navigation claim.
/// </summary>
/// <param name="Class">Opaque source class identifier.</param>
/// <param name="ResourceId">Opaque source resource identifier, when supplied by the observation adapter.</param>
/// <param name="Clickable">Raw clickable flag.</param>
/// <param name="Checkable">Raw checkable flag.</param>
/// <param name="Checked">Raw checked flag when checkable.</param>
/// <param name="Enabled">Raw enabled flag.</param>
/// <param name="Focusable">Raw focusable flag.</param>
/// <param name="Bounds">Normalized bounds of the structured node.</param>
/// <param name="ContentDescription">Raw content-desc when present.</param>
/// <param name="SourceNodeIdentity">Deterministic node identity used for provenance/correlation.</param>
/// <param name="RawText">Raw text on this node only; no descendant or role inference.</param>
/// <param name="ParentSourceNodeIdentity">Optional primitive hierarchy parent identity.</param>
public sealed record StructuredElementEvidence(
    string? Class,
    string? ResourceId,
    bool? Clickable,
    bool? Checkable,
    bool? Checked,
    bool? Enabled,
    bool? Focusable,
    ElementBounds? Bounds,
    string? ContentDescription = null,
    string? SourceNodeIdentity = null,
    string? RawText = null,
    string? ParentSourceNodeIdentity = null);
