using System.Text.Json;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Core;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Tests.World;

/// <summary>Optional, read-only snapshots of real Owner outputs for the visualizer.</summary>
internal static class WorldVisualizationExport
{
    private const string EnvironmentVariable = "UNICLAW_WORLD_VIS_EXPORT";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    internal static object Frame(string key, string label, UniKernel kernel, WorldModel world,
        IReadOnlyList<string>? containerIds = null, WorldKnowledgeView? knowledge = null,
        WorldRecoveryView? recovery = null, IReadOnlyList<EffectCorrectionReview>? effectImpacts = null,
        bool? terminal = null, object? outcome = null, PageCaptureObservation? capture = null,
        object? actualConsultation = null,
        params Slice[] capturedSlices)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)))
            return new { key, label, collectionStatus = "disabled" };
        var revision = world.Current;
        var ids = (containerIds ?? (revision?.CurrentPageObservation?.ContainerId is { } current
                ? new[] { current } : Array.Empty<string>()))
            .Distinct(StringComparer.Ordinal).ToArray();
        var views = revision is null ? Array.Empty<WorldPageView>() : ids
            .Where(id => revision.Containers?.Any(c => c.Identity.ContainerId == id) == true)
            .Select(id => world.DerivePageView(id, revision.RevisionId)).ToArray();
        var slices = new List<Slice>();
        if (revision is not null)
        {
            foreach (var id in ids.Where(id => revision.Containers?.Any(c => c.Identity.ContainerId == id) == true))
                if (!capturedSlices.Any(slice => slice.SourceRevisionId == revision.RevisionId
                    && slice.RootContainerId == id))
                    slices.Add(kernel.DeriveSlice(id, ids.Where(inScope => revision.Containers!
                        .Any(c => c.Identity.ContainerId == inScope)).ToArray()));
        }
        slices.AddRange(capturedSlices);
        var projectedSlices = slices.Select(slice =>
        {
            var source = world.RevisionHistory.FirstOrDefault(r => r.RevisionId == slice.SourceRevisionId);
            var presentations = PresentationProjections(slice);
            return new
            {
                sliceId = CoreSemanticProjection.SliceId(slice).Value,
                slice,
                coreSlice = source is null ? null : CoreSemanticProjection.ProjectSlice(slice, source),
                presentation = presentations
            };
        }).ToArray();

        return new
        {
            key,
            label,
            revision = revision,
            knowledge = knowledge ?? kernel.ProjectWorldKnowledge(),
            recovery,
            effectImpacts = effectImpacts?.ToArray(),
            terminal,
            outcome,
            actualConsultation,
            receipts = kernel.EffectReceipts.ToArray(),
            verifications = kernel.RunView is null ? null : kernel.PostActionVerifications.ToArray(),
            decisionUses = kernel.WorldDecisionUses.ToArray(),
            capture = capture ?? revision?.CurrentPageObservation?.Capture,
            pages = views,
            slices = projectedSlices
        };
    }

    internal static void Write(string family, params object[] frames)
    {
        var directory = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(directory)) return;
        if (!Path.IsPathFullyQualified(directory))
            throw new InvalidOperationException(EnvironmentVariable + " must be an absolute directory");
        Directory.CreateDirectory(directory);
        var payload = new
        {
            schemaVersion = "world-visualization.v1",
            id = "world-visualization:" + family,
            title = family switch
            {
                "reuse" => "Verified page meaning reuse",
                "context" => "Current context knowledge gaps",
                "recovery" => "Bounded observation recovery review",
                "terminal" => "Terminal effect correction review",
                "correction" => "错误归属、真实消费与历史纠正",
                _ => family
            },
            provenance = new
            {
                coreCommit = "56a75fd7",
                sourceTest = family switch
                {
                    "reuse" => "WorldSemanticReuseTests.VerifiedMeaning_IsReadableAsHistoryBeforeCurrentAnchor_ThenReusedOnlyWithFreshCorrespondence",
                    "context" => "WorldKnowledgeGapTests.FourGaps_AreProducedHeldThenResolvedOnlyByCurrentEvidence",
                    "recovery" => "WorldRecoveryReviewTests.Driver_ReconsultsForNewResultVersion_WithoutRenewingSupplierRequestQuota",
                    "terminal" => "WorldCorrectionImpactTests.CorrectionAfterRecordedTerminal_AppendsReviewWithoutChangingOutcomeOrDelivery",
                    "correction" => "WorldCorrectionImpactTests.CorrectedPlanBasis_ReassessesVerifiedReceipt_AndBlocksNextActAtStepBoundary",
                    _ => family
                },
                inputClass = "受控来源/真实核心",
                limitations = new[] { "controlled fixture/provider inputs; no live-device or live-model evidence" }
            },
            frames
        };
        File.WriteAllText(Path.Combine(directory, family + ".json"), JsonSerializer.Serialize(payload, Json));
    }

    private static IReadOnlyList<WorldPresentation> PresentationProjections(Slice slice)
    {
        var located = slice.Occurrences.Where(o => o.Locator is not null).ToArray();
        return located.GroupBy(o => o.Locator!.SpatialFrameId, StringComparer.Ordinal).Select(group =>
        {
            var boxes = group.Select(o => o.Locator!).ToArray();
            var x1 = boxes.Min(b => b.X1); var y1 = boxes.Min(b => b.Y1);
            var x2 = boxes.Max(b => b.X2); var y2 = boxes.Max(b => b.Y2);
            if (x1 >= x2 || y1 >= y2) return null;
            var region = new SpatialLocator(x1, y1, x2, y2, group.Key);
            return SliceContentPresentation.Project(slice, group.Key, region);
        }).Where(presentation => presentation is not null).Cast<WorldPresentation>().ToArray();
    }
}
