using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// Stage C1 task 3.2 capability coverage: the immutable LogicalItem
/// compositional model with explicit membership evidence, single optional
/// primary affordance, anchor integrity, fail-closed construction, and
/// strict semantic/authority isolation (container-runtime-v2-evidence-model,
/// spec: canonical-world "LogicalItem 为 LocalModel-scoped canonical 逻辑对象").
/// No producer/commit seam exists at 3.2 — the SemanticReconciler (3.3) owns
/// creation; these tests exercise the model contract only.
/// </summary>
public sealed class LogicalItemModelTests
{
    private static readonly LogicalItemRef ItemRef = new("item:1");
    private static readonly ViewportOccurrenceRef Occ1 = new("occ:1");
    private static readonly ViewportOccurrenceRef Occ2 = new("occ:2");
    private static readonly ContainerSliceRef Slice1 = new("slice:1");
    private static readonly ContainerSliceRef Slice2 = new("slice:2");

    // ── Buyer 1: 孪生文本 ──────────────────────────────────────────────

    [Fact]
    public void TwinTextEntitiesStaySeparateItemsWithExplicitEvidence()
    {
        // Two visual entities carry the SAME display text (twin-text buyer):
        // the model has no text field anywhere, so equal text cannot merge
        // them; each item keeps its own occurrence membership and evidence.
        var left = new LogicalItem(
            new LogicalItemRef("item:left"),
            LogicalStructure.ListItem,
            LogicalAffordanceKind.Navigate,
            memberships: [new LogicalMembership(Occ1, LogicalMemberRole.Primary, "evidence:left")],
            anchorSliceRefs: [Slice1]);

        var right = new LogicalItem(
            new LogicalItemRef("item:right"),
            LogicalStructure.ListItem,
            LogicalAffordanceKind.Navigate,
            memberships: [new LogicalMembership(Occ2, LogicalMemberRole.Primary, "evidence:right")],
            anchorSliceRefs: [Slice1]);

        Assert.NotEqual(left.ItemRef, right.ItemRef);
        Assert.Single(left.Memberships);
        Assert.Single(right.Memberships);
        // Text is not a model dimension: no text-bearing members exist.
        Assert.DoesNotContain(typeof(LogicalItem).GetProperties(), p => p.Name.Contains("Text", StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(LogicalMembership).GetProperties(), p => p.Name.Contains("Text", StringComparison.Ordinal));
    }

    [Fact]
    public void MembershipWithoutExplicitEvidenceIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new LogicalItem(
            ItemRef,
            LogicalStructure.ListItem,
            LogicalAffordanceKind.Navigate,
            memberships: [new LogicalMembership(Occ1, LogicalMemberRole.Primary, "   ")]));
    }

    // ── Buyer 2: 帧级翻转 ──────────────────────────────────────────────

    [Fact]
    public void FrameLevelClassificationFlipCannotSilentlyRewriteAnItem()
    {
        var firstFrame = new LogicalItem(
            ItemRef,
            LogicalStructure.ListItem,
            LogicalAffordanceKind.Navigate,
            memberships: [new LogicalMembership(Occ1, LogicalMemberRole.Primary, "frame:1:list-item")],
            anchorSliceRefs: [Slice1, Slice2]);

        // A later frame classifies the same occurrence differently. The only
        // representation is a NEW record with its own evidence; the original
        // record (identity, anchors, evidence) is untouched — the future
        // reconciler (3.3) judges between them explicitly.
        var flippedFrame = new LogicalItem(
            ItemRef,
            LogicalStructure.StaticContent,
            primaryAffordance: null,
            memberships: [new LogicalMembership(Occ1, LogicalMemberRole.Secondary, "frame:9:static")],
            anchorSliceRefs: [Slice1, Slice2]);

        Assert.Equal(LogicalStructure.ListItem, firstFrame.Structure);
        Assert.Equal(LogicalAffordanceKind.Navigate, firstFrame.PrimaryAffordance);
        Assert.Equal("frame:1:list-item", Assert.Single(firstFrame.Memberships).EvidenceRef);
        Assert.Equal(LogicalStructure.StaticContent, flippedFrame.Structure);
        Assert.Null(flippedFrame.PrimaryAffordance);
        // Anchors and identity are retained across interpretations.
        Assert.Equal(firstFrame.AnchorSliceRefs, flippedFrame.AnchorSliceRefs);
        Assert.Equal(firstFrame.ItemRef, flippedFrame.ItemRef);
    }

    [Fact]
    public void RecordsAreImmutableWithoutInitSettersOrMutableCollections()
    {
        var item = new LogicalItem(
            ItemRef,
            LogicalStructure.ListItem,
            LogicalAffordanceKind.Navigate,
            memberships: [new LogicalMembership(Occ1, LogicalMemberRole.Primary, "evidence:1")],
            anchorSliceRefs: [Slice1]);

        Assert.Equal(typeof(ImmutableArray<LogicalMembership>), typeof(LogicalItem).GetProperty("Memberships")!.PropertyType);
        Assert.Equal(typeof(ImmutableArray<ContainerSliceRef>), typeof(LogicalItem).GetProperty("AnchorSliceRefs")!.PropertyType);
        Assert.All(
            typeof(LogicalItem).GetProperties(),
            property => Assert.Null(property.SetMethod));
    }

    // ── Buyer 3: STATIC_CONTENT ─────────────────────────────────────────

    [Fact]
    public void StaticContentResolvesWithNoActionAffordance()
    {
        var item = new LogicalItem(
            new LogicalItemRef("item:title"),
            LogicalStructure.StaticContent,
            primaryAffordance: null,
            memberships: [new LogicalMembership(Occ1, LogicalMemberRole.Primary, "evidence:static")],
            anchorSliceRefs: [Slice1],
            semanticResolved: true);

        Assert.True(item.SemanticResolved);
        Assert.True(item.IsAffordanceDetermined);
        Assert.Null(item.PrimaryAffordance);
    }

    [Fact]
    public void ResolvedStaticContentCarriesNoAuthoritySurfaces()
    {
        var item = new LogicalItem(
            new LogicalItemRef("item:title"),
            LogicalStructure.StaticContent,
            primaryAffordance: null,
            memberships: [new LogicalMembership(Occ1, LogicalMemberRole.Primary, "evidence:static")],
            semanticResolved: true);

        // Structural authority isolation: no grounding geometry, no action /
        // obligation / coverage / completion / authorization semantics exist
        // on the canonical record.
        Assert.DoesNotContain(typeof(LogicalItem).GetProperties(), p =>
            p.PropertyType == typeof(ElementBounds));
        Assert.DoesNotContain(typeof(LogicalItem).GetProperties(), p =>
            p.Name.Contains("Groundable", StringComparison.Ordinal)
            || p.Name.Contains("Grounding", StringComparison.Ordinal)
            || p.Name.Contains("Authoriz", StringComparison.OrdinalIgnoreCase)
            || p.Name.Contains("Obligation", StringComparison.Ordinal)
            || p.Name.Contains("Coverage", StringComparison.Ordinal)
            || p.Name.Contains("Completion", StringComparison.Ordinal)
            || p.Name.Contains("Complete", StringComparison.Ordinal));
    }

    // ── 机械约束 ────────────────────────────────────────────────────────

    [Fact]
    public void PrimaryAffordanceIsAtMostOneOptionalValue()
    {
        Assert.Equal(
            typeof(LogicalAffordanceKind?),
            typeof(LogicalItem).GetProperty("PrimaryAffordance")!.PropertyType);

        var none = new LogicalItem(ItemRef, LogicalStructure.Group, primaryAffordance: null);
        Assert.Null(none.PrimaryAffordance);
    }

    [Fact]
    public void UnknownIsNeverCoercedIntoStaticContentOrNonInteractive()
    {
        // Unknown structure stays Unknown and cannot carry a resolved claim.
        var unresolved = new LogicalItem(ItemRef, LogicalStructure.Unknown, LogicalAffordanceKind.Unknown);
        Assert.Equal(LogicalStructure.Unknown, unresolved.Structure);
        Assert.Equal(LogicalAffordanceKind.Unknown, unresolved.PrimaryAffordance);
        // Unknown affordance stays genuinely unresolved.
        Assert.False(unresolved.IsAffordanceDetermined);

        // Missing affordance on an actionable structure is DETERMINED-NONE
        // (review ruling: determination looks only at the affordance value; a
        // definite non-operable list row is fully representable). No silent
        // default to a non-interactive STRUCTURE interpretation occurs — the
        // structure value is retained unchanged.
        var noAffordance = new LogicalItem(ItemRef, LogicalStructure.ListItem, primaryAffordance: null);
        Assert.Null(noAffordance.PrimaryAffordance);
        Assert.True(noAffordance.IsAffordanceDetermined);
        Assert.Equal(LogicalStructure.ListItem, noAffordance.Structure);
    }

    [Fact]
    public void ResolvedClaimWithoutDeterminedSemanticsFailsClosed()
    {
        // Unknown structure + resolved → rejected.
        Assert.Throws<ArgumentException>(() => new LogicalItem(
            ItemRef, LogicalStructure.Unknown, LogicalAffordanceKind.Navigate,
            memberships: [new LogicalMembership(Occ1, LogicalMemberRole.Primary, "evidence:1")],
            semanticResolved: true));

        // Unknown affordance + resolved → rejected (Unknown ≠ determined-none).
        Assert.Throws<ArgumentException>(() => new LogicalItem(
            ItemRef, LogicalStructure.ListItem, LogicalAffordanceKind.Unknown,
            memberships: [new LogicalMembership(Occ1, LogicalMemberRole.Primary, "evidence:1")],
            semanticResolved: true));

        // Resolved without membership evidence → rejected.
        Assert.Throws<ArgumentException>(() => new LogicalItem(
            ItemRef, LogicalStructure.StaticContent, primaryAffordance: null,
            semanticResolved: true));
    }

    [Fact]
    public void StructureAndAffordanceAreOrthogonalAtTheBaseModel()
    {
        // Review ruling (3.2 PASS_WITH_FIXES): determination looks ONLY at the
        // affordance value — Structure must not decide which affordance states
        // count as determined. A definite, non-operable list row resolves with
        // null affordance (the "Android version 15" buyer).
        var nonOperableRow = new LogicalItem(
            ItemRef, LogicalStructure.ListItem, primaryAffordance: null,
            memberships: [new LogicalMembership(Occ1, LogicalMemberRole.Primary, "evidence:row")],
            anchorSliceRefs: [Slice1],
            semanticResolved: true);

        Assert.True(nonOperableRow.SemanticResolved);
        Assert.True(nonOperableRow.IsAffordanceDetermined);
        Assert.Null(nonOperableRow.PrimaryAffordance);

        // Reverse direction is equally unvalidated at the base model: no
        // Structure→Affordance compatibility rules exist here (they belong to
        // the claim-specific EvidencePolicy, task 3.3), so unusual
        // combinations remain representable for the reconciler to judge.
        var unusualButRepresentable = new LogicalItem(
            ItemRef, LogicalStructure.StaticContent, LogicalAffordanceKind.Navigate,
            memberships: [new LogicalMembership(Occ1, LogicalMemberRole.Primary, "evidence:odd")]);
        Assert.Equal(LogicalStructure.StaticContent, unusualButRepresentable.Structure);
        Assert.Equal(LogicalAffordanceKind.Navigate, unusualButRepresentable.PrimaryAffordance);
    }

    [Fact]
    public void DefaultStructReferencesCannotBypassConstructors()
    {
        // default(LogicalItemRef) skips its ctor (Value == null) and must be
        // rejected by explicit value validation, not ThrowIfNull (meaningless
        // on a non-boxed struct).
        Assert.Throws<ArgumentException>(() => new LogicalItem(
            default, LogicalStructure.ListItem, LogicalAffordanceKind.Navigate));

        // Same bypass class at the membership consumption point.
        Assert.Throws<ArgumentException>(() => new LogicalItem(
            ItemRef, LogicalStructure.ListItem, LogicalAffordanceKind.Navigate,
            memberships: [new LogicalMembership(default(ViewportOccurrenceRef), LogicalMemberRole.Primary, "evidence:1")]));

        // Same bypass class at the B2 Occurrence consumption point.
        Assert.Throws<ArgumentException>(() => new Occurrence(
            default,
            Slice1,
            VisualPrimitiveKind.Text,
            new ElementBounds(0.1f, 0.2f, 0.9f, 0.3f),
            new OccurrenceRegionBinding(default, new SpatialRegionRef("primary"), 1d, false),
            "vision:default"));
    }

    [Fact]
    public void DuplicateMembershipsAndAnchorsFailClosed()
    {
        Assert.Throws<ArgumentException>(() => new LogicalItem(
            ItemRef, LogicalStructure.ListItem, LogicalAffordanceKind.Navigate,
            memberships:
            [
                new LogicalMembership(Occ1, LogicalMemberRole.Primary, "evidence:a"),
                new LogicalMembership(Occ1, LogicalMemberRole.Secondary, "evidence:b"),
            ]));

        Assert.Throws<ArgumentException>(() => new LogicalItem(
            ItemRef, LogicalStructure.ListItem, LogicalAffordanceKind.Navigate,
            memberships: [new LogicalMembership(Occ1, LogicalMemberRole.Primary, "evidence:1")],
            anchorSliceRefs: [Slice1, Slice1]));
    }

    [Fact]
    public void DanglingMembershipAndAnchorRefsAreDetectedFailClosed()
    {
        var model = new NodeLocalModel(
            new ContainerNodeRef("node:1"),
            activeSliceRefs: [Slice1],
            archivedSliceRefs: [Slice2],
            activeOccurrenceRefs: [Occ1]);

        var anchored = new LogicalItem(
            ItemRef, LogicalStructure.ListItem, LogicalAffordanceKind.Navigate,
            memberships: [new LogicalMembership(Occ1, LogicalMemberRole.Primary, "evidence:1")],
            anchorSliceRefs: [Slice1, Slice2]); // active + archived anchors both resolve

        Assert.True(LogicalItemIntegrity.ReferencesResolve(anchored, model, out var clean));
        Assert.Empty(clean);

        var dangling = new LogicalItem(
            ItemRef, LogicalStructure.ListItem, LogicalAffordanceKind.Navigate,
            memberships: [new LogicalMembership(Occ2, LogicalMemberRole.Primary, "evidence:2")],
            anchorSliceRefs: [new ContainerSliceRef("slice:missing")]);

        Assert.False(LogicalItemIntegrity.ReferencesResolve(dangling, model, out var violations));
        Assert.Equal(2, violations.Count);
        Assert.Contains(violations, v => v.Contains("occ:2", StringComparison.Ordinal));
        Assert.Contains(violations, v => v.Contains("slice:missing", StringComparison.Ordinal));
    }
}
