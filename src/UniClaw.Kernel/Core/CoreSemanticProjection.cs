using System.Globalization;
using UniClaw.Core;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.World;

using CoreEvidence = UniClaw.Core.EvidenceRecord;
using CoreSlice = UniClaw.Core.Slice;
using KernelEvidence = UniClaw.Kernel.Evidence.EvidenceRecord;
using KernelSlice = UniClaw.Kernel.World.Slice;

namespace UniClaw.Kernel.Core;

/// <summary>
/// CORE-002/CORE-007 realization→Core 投影缝：把现有手机/UI 滚动—点击 realization
/// （admitted evidence、container segment、DeriveSlice、WorldState claim、
/// accepted contract、CanonicalBinding、EffectReceipt）投影为 UniClaw.Core
/// 候选语义记录。UI 专用字段保留在 Kernel；本缝只投影当前 UI 用途实际需要的
/// 最小语义，不把所有 vNext 字段复制进 Core。
/// 纯函数、零状态、不回写 realization。Core 只承载领域无关值；UI occurrence、
/// locator、DOM/ADB/截图等域载荷以不透明字符串值进入 Core。
/// </summary>
public static class CoreSemanticProjection
{
    /// <summary>
    /// 将已接受的 UI execution contract 中明确给出的目标和证明判据投影为 Clause。
    /// Allowed/forbidden effects 仍属于 Runtime contract；没有独立授权范围与来源时不自动
    /// 制造 Permission。
    /// </summary>
    public static IReadOnlyList<Clause> ProjectClauses(
        ExecutionContractView contract,
        DateTimeOffset establishedAt)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var clauses = new List<Clause>
        {
            new(
                new CoreId($"clause:{contract.Version}:objective"),
                ClauseKind.Requirement,
                contract.Objective,
                establishedAt),
        };

        clauses.AddRange(contract.ProofCriteria.Select((criterion, index) =>
            new Clause(
                new CoreId($"clause:{contract.Version}:criterion:{index}"),
                ClauseKind.Criterion,
                criterion,
                establishedAt)));

        // Allowed/forbidden effects remain Runtime contract facts in this UI profile.
        // Without an explicit authority/scope contract, projecting them as Permission
        // would manufacture authorization from a shortened string.
        return clauses;
    }

    public static CoreEvidence ProjectEvidence(KernelEvidence record) =>
        new(
            new CoreId(record.EvidenceId),
            new CoreId(record.Claim.Subject),
            record.Provenance.CaptureTime,
            record.Provenance.Producer,
            record.Provenance.Scope,
            record.Provenance.TransformationLineage);

    public static Segment ProjectSegment(string containerId, string semanticKind) =>
        new(new CoreId(containerId), semanticKind);

    public static CoreId SliceId(KernelSlice slice) =>
        new($"slice:{slice.SourceRevisionId}:{slice.RootContainerId}");

    public static CoreSlice ProjectSlice(KernelSlice slice, WorldBeliefRevision source)
    {
        ArgumentNullException.ThrowIfNull(slice);
        ArgumentNullException.ThrowIfNull(source);
        if (slice.SourceRevisionId != source.RevisionId)
            throw new ArgumentException(
                $"slice 来源 revision '{slice.SourceRevisionId}' 与传入 revision '{source.RevisionId}' 不一致（fail-closed）",
                nameof(source));
        return new CoreSlice(
            SliceId(slice),
            new CoreId(slice.RootContainerId),
            source.EvidenceBasis
                .Select(id => new CoreId(id))
                .OrderBy(id => id.Value, StringComparer.Ordinal)
                .ToArray(),
            slice.FreshnessBasis.AsOf,
            string.Join("+", slice.InScopeContainerIds),
            slice.Occurrences.Select(o => new CoreId(o.OccurrenceId)).ToArray(),
            Coverage: "partial");
    }

    public static Claim ProjectClaim(string subject, WorldClaim claim, bool conflicted)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return new Claim(
            new CoreId($"claim:{subject}:{claim.EvidenceId}"),
            new CoreId(subject),
            Predicate: "observed-value",
            claim.Value,
            conflicted ? ClaimDisposition.Conflict : ClaimDisposition.Accepted,
            new[] { claim.EvidenceId }
                .Concat(claim.SupersededEvidenceIds ?? Array.Empty<string>())
                .Select(id => new CoreId(id))
                .ToArray());
    }

    public static Effect ProjectEffect(CanonicalBinding binding) =>
        new(
            new CoreId($"effect:{binding.IntentId}"),
            binding.EffectClass,
            new CoreId(binding.TargetSubject));

    public static DeliveryOutcome MapDelivery(DispatchOutcome outcome) => outcome switch
    {
        DispatchOutcome.DeliveryCompleted => DeliveryOutcome.Completed,
        DispatchOutcome.DeliveryFailed => DeliveryOutcome.Failed,
        _ => DeliveryOutcome.Unknown,
    };

    public static Attempt ProjectAttempt(EffectReceipt receipt, CoreId effectId, CoreId? deliveryEvidenceId = null)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return new Attempt(
            new CoreId($"attempt:{receipt.ReceiptId}"),
            effectId,
            new CoreId(receipt.BindingId),
            receipt.DispatchedAt,
            MapDelivery(receipt.Outcome),
            deliveryEvidenceId);
    }

    public static TargetBinding ProjectTargetBinding(
        CanonicalBinding binding,
        CoreId basisSliceId,
        CoreId targetSegmentId,
        string currentRevisionId,
        GateDecision? gate = null)
    {
        ArgumentNullException.ThrowIfNull(binding);
        var disposition =
            gate is { Allowed: false }
                ? BindingDisposition.Unauthorized
                : binding.RevisionId != currentRevisionId
                    ? BindingDisposition.Stale
                    : BindingDisposition.Canonical;
        var (locatorKind, locatorKey) = ProjectLocator(binding);
        return new TargetBinding(
            new CoreId(binding.BindingId),
            targetSegmentId,
            basisSliceId,
            locatorKind,
            locatorKey,
            disposition);
    }

    private static (string Kind, string Key) ProjectLocator(CanonicalBinding binding) =>
        binding.TargetNative is { } native
            ? (native.Kind, native.Value)
            : binding.TargetLocator is { } spatial
                ? ($"spatial:{spatial.SpatialFrameId}",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{spatial.X1:F4},{spatial.Y1:F4},{spatial.X2:F4},{spatial.Y2:F4}"))
                : ("none", "");
}
