using System.Text.Json;
using UniClaw.Runtime.Adapters;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;

namespace UniClaw.Runtime.ValidationHarness.SettingsCampaign.SlowShadow;

/// <summary>
/// Bounded SLOW_SEMANTIC_SHADOW experiment evaluator (Shadow-only stage).
/// Harness-local observation of what a Slow semantic advisor WOULD say about
/// each fresh frame — zero production Runtime effect: the production Agent
/// stays on the Disabled Slow path; nothing here feeds Action, GoalEvidence,
/// Graph authority, or Agent obligation. The output is one JSON ledger used
/// to answer "if Slow were adopted, would it rescue the current blocker".
///
/// Evidence binding per frame (revision-bound, mirroring the production
/// Agent conventions from Agent.ContainerReconciliation):
///   ObservationRef = "observation:{seq}", EvidenceRevision = seq,
///   destination node = agent-node:{seq}:{resolved page},
///   source node = the prior frame's node.
/// The Fast assessment is constructed from the observable Fast live outcome
/// (the same page resolution the runtime uses), bound to the same revision
/// and nodes.
/// Known harness-side gaps (recorded per entry, honest): TriggerOccurrence /
/// TransitionOccurrence / Graph candidates are Agent-internal and NOT
/// exposed on any read surface this tap can see — a real ASYNC_ADVISORY
/// consumption point would have to acquire them Runtime-side (already the
/// R5 deferred scope).
/// NEW_SYMBOL_JUSTIFICATION: no existing harness component owns Slow
/// experiment orchestration; this is the single Shadow-mode buyer for the
/// bounded experiment and lives harness-side so no production project gains
/// an experiment dependency.
/// </summary>
public sealed class SlowShadowEvaluator : IDisposable
{
    private readonly ISlowContainerSemanticAdvisor _advisor;
    private readonly IShadowFrameStore? _frameStore;
    private readonly Func<IReadOnlyList<SlowShadowMetric>>? _metricsProvider;
    private readonly SemaphoreSlim _queue = new(1, 1);
    private readonly List<Task> _pending = [];
    private readonly object _gate = new();
    private readonly List<Entry> _entries = [];
    private readonly string? _frameArchiveDir;
    private string? _priorPage;

    /// <summary>Port ctor with an optional per-frame PNG archive directory
    /// (post-hoc FalseCorrection review evidence).</summary>
    public SlowShadowEvaluator(
        ISlowContainerSemanticAdvisor advisor,
        IShadowFrameStore? frameStore,
        Func<IReadOnlyList<SlowShadowMetric>>? metricsProvider,
        string? frameArchiveDir)
        : this(advisor, frameStore, metricsProvider)
    {
        _frameArchiveDir = frameArchiveDir;
    }

    /// <summary>Production ctor: the concrete VLM advisor (frame store +
    /// latency/token metrics).</summary>
    public SlowShadowEvaluator(QwenVlSlowAdvisor advisor)
        : this(advisor, advisor, () => advisor.Metrics)
    {
    }

    /// <summary>Port ctor: any ISlowContainerSemanticAdvisor (Shadow-mode
    /// acquisition runs through the real purchased seam) + optional frame
    /// store and metric provider. Without a frame store the assessments fail
    /// closed to Insufficient (recorded, never guessed). The evaluator never
    /// disposes the advisor — the composition root owns it.</summary>
    public SlowShadowEvaluator(
        ISlowContainerSemanticAdvisor advisor,
        IShadowFrameStore? frameStore = null,
        Func<IReadOnlyList<SlowShadowMetric>>? metricsProvider = null)
    {
        _advisor = advisor ?? throw new ArgumentNullException(nameof(advisor));
        _frameStore = frameStore;
        _metricsProvider = metricsProvider;
    }

    /// <summary>Receives one frame's physical artifact (screenshot + structured
    /// candidates) from the adapter-local evidence tap. Evidence only. The
    /// artifact fires during ObserveAsync, BEFORE the observation tap sees the
    /// observation — the container context is merged into this registration by
    /// <see cref="OnObservation"/>.</summary>
    public void OnArtifact(PhysicalArtifactTap artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        if (_frameArchiveDir is { Length: > 0 } archiveDir)
        {
            try
            {
                Directory.CreateDirectory(archiveDir);
                File.WriteAllBytes(
                    Path.Combine(archiveDir, $"frame-{artifact.SequenceNumber}.png"),
                    artifact.PngBytes);
            }
            catch (IOException)
            {
                // Archival is diagnostic evidence only — never blocks the tap.
            }
        }
        _frameStore?.RegisterFrame(new ShadowFrameContext(
            artifact.SequenceNumber,
            artifact.PngBytes,
            artifact.Width,
            artifact.Height,
            CurrentContainer: null,
            PriorContainer: null,
            CandidatesJson: QwenVlSlowAdvisor.BuildCandidatesJson(artifact.Candidates)));
    }

    /// <summary>
    /// Receives one stabilized observation (the same observation the Runtime
    /// consumes) plus the observable Fast live resolution for it, merges the
    /// container context into the frame registration, builds the
    /// revision-bound Slow request, and queues the Shadow acquisition +
    /// projection off the run's critical path.
    /// </summary>
    public void OnObservation(Observation observation, string? resolvedPage)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var sequence = observation.SequenceNumber;
        var priorPage = _priorPage;
        _priorPage = resolvedPage;
        _frameStore?.MergeContainerContext(sequence, resolvedPage, priorPage);

        var destinationNode = resolvedPage is null
            ? (ContainerNodeRef?)null
            : new ContainerNodeRef($"agent-node:{sequence}:{resolvedPage}");
        var sourceNode = priorPage is null
            ? (ContainerNodeRef?)null
            : new ContainerNodeRef($"agent-node:prior:{priorPage}");
        var fastResolution = resolvedPage is null
            ? FastContainerResolutionKind.AMBIGUOUS
            : string.Equals(resolvedPage, priorPage, StringComparison.Ordinal)
                ? FastContainerResolutionKind.SAME_CONTAINER
                : FastContainerResolutionKind.NEW_CONTAINER;
        var fast = new FastContainerAssessment(
            fastResolution,
            new SemanticEvidenceRevision(sequence),
            sourceNode,
            destinationNode,
            resolvedPage,
            graphPriorNodeRef: null,
            independentBoundarySupport: fastResolution == FastContainerResolutionKind.NEW_CONTAINER,
            semanticSupport: resolvedPage is not null,
            triggerDestinationSemanticMatch: resolvedPage is not null && priorPage is not null
                && !string.Equals(resolvedPage, priorPage, StringComparison.Ordinal),
            hardConflict: false,
            isAbstained: resolvedPage is null,
            abstentionReason: resolvedPage is null ? "shadow: page unresolved" : null);
        var request = new SlowContainerSemanticRequest(
            $"observation:{sequence}",
            new SemanticEvidenceRevision(sequence),
            destinationNode,
            sourceNode,
            fastAssessment: fast);

        var task = Task.Run(async () =>
        {
            await _queue.WaitAsync().ConfigureAwait(false);
            try
            {
                var invocation = await SlowContainerSemanticConsumer.AcquireAsync(
                    SlowContainerSemanticMode.Shadow,
                    _advisor,
                    request).ConfigureAwait(false);
                var consumption = SlowContainerSemanticConsumer.Project(
                    invocation, request.EvidenceRevision);
                lock (_gate)
                {
                    _entries.Add(new Entry(
                        sequence,
                        request.ObservationRef,
                        request.EvidenceRevision.Value,
                        request.NodeRef?.Value,
                        request.SourceNodeRef?.Value,
                        resolvedPage,
                        priorPage,
                        fastResolution.ToString(),
                        invocation.AdvisorInvoked,
                        invocation.AcquisitionIssue,
                        consumption.Availability.ToString(),
                        consumption.IsCurrent,
                        consumption.ConflictsWithFast,
                        consumption.Assessment?.Kind.ToString(),
                        consumption.Assessment?.Scene.ToString(),
                        consumption.Assessment?.ContainerSemantic,
                        consumption.Assessment?.TriggerSemantic,
                        consumption.Assessment?.Details,
                        consumption.Assessment?.CorrectedIdentityCandidate,
                        consumption.Assessment?.EvidenceUsefulness.ToString(),
                        consumption.Assessment?.SuggestedDisposition.ToString(),
                        consumption.Assessment?.HasMismatch ?? false));
                }
            }
            finally
            {
                _queue.Release();
            }
        });
        lock (_gate)
        {
            _pending.Add(task);
        }
    }

    /// <summary>Waits (bounded) for all queued assessments, then writes the
    /// experiment ledger JSON (entries + summary metrics).</summary>
    public async Task<SlowShadowLedgerSummary> DrainAndWriteAsync(string path, TimeSpan bound)
    {
        Task[] pending;
        lock (_gate)
        {
            pending = _pending.ToArray();
        }
        if (pending.Length > 0)
        {
            var wait = Task.WhenAll(pending);
            await (await Task.WhenAny(wait, Task.Delay(bound)).ConfigureAwait(false)).ConfigureAwait(false);
        }

        List<Entry> entries;
        lock (_gate)
        {
            entries = _entries.ToList();
        }
        var metricsBySeq = (_metricsProvider?.Invoke() ?? [])
            .GroupBy(metric => metric.SequenceNumber)
            .ToDictionary(group => group.Key, group => group.First());

        var records = entries
            .OrderBy(entry => entry.SequenceNumber)
            .Select(entry => metricsBySeq.TryGetValue(entry.SequenceNumber, out var metric)
                ? (entry, (SlowShadowMetric?)metric)
                : (entry, (SlowShadowMetric?)null))
            .ToList();

        var latencyValues = records
            .Where(record => record.Item2 is not null)
            .Select(record => record.Item2!.LatencyMs)
            .ToList();
        var summary = new SlowShadowLedgerSummary(
            SlowInvocations: entries.Count,
            AdvisorInvoked: entries.Count(entry => entry.AdvisorInvoked),
            Confirm: CountKind(entries, nameof(SlowContainerSemanticAssessmentKind.Confirm)),
            Challenge: CountKind(entries, nameof(SlowContainerSemanticAssessmentKind.Challenge)),
            Correct: CountKind(entries, nameof(SlowContainerSemanticAssessmentKind.Correct)),
            Insufficient: CountKind(entries, nameof(SlowContainerSemanticAssessmentKind.Insufficient)),
            ConflictsWithFast: entries.Count(entry => entry.ConflictsWithFast),
            ParseFailures: records.Count(record => record.Item2 is { ParseSucceeded: false }),
            AverageLatencyMs: latencyValues.Count > 0 ? Math.Round(latencyValues.Average(), 1) : 0,
            MaxLatencyMs: latencyValues.Count > 0 ? Math.Round(latencyValues.Max(), 1) : 0,
            TotalPromptTokens: records.Sum(record => record.Item2?.PromptTokens ?? 0),
            TotalCompletionTokens: records.Sum(record => record.Item2?.CompletionTokens ?? 0));

        var document = new
        {
            format = "p26-slow-shadow-ledger.v1",
            generatedAtUtc = DateTimeOffset.UtcNow,
            mode = nameof(SlowContainerSemanticMode.Shadow),
            hasRuntimeEffect = false,
            summary,
            entries = records.Select(record => new
            {
                record.Item1.SequenceNumber,
                record.Item1.ObservationRef,
                evidenceRevision = record.Item1.EvidenceRevision,
                nodeRef = record.Item1.NodeRef,
                sourceNodeRef = record.Item1.SourceNodeRef,
                currentPage = record.Item1.CurrentPage,
                priorPage = record.Item1.PriorPage,
                fastResolution = record.Item1.FastResolution,
                advisorInvoked = record.Item1.AdvisorInvoked,
                acquisitionIssue = record.Item1.AcquisitionIssue,
                availability = record.Item1.Availability,
                isCurrent = record.Item1.IsCurrent,
                conflictsWithFast = record.Item1.ConflictsWithFast,
                kind = record.Item1.Kind,
                scene = record.Item1.Scene,
                containerSemantic = record.Item1.ContainerSemantic,
                triggerSemantic = record.Item1.TriggerSemantic,
                candidateInterpretation = record.Item1.CandidateInterpretation,
                correctedIdentity = record.Item1.CorrectedIdentity,
                evidenceUsefulness = record.Item1.EvidenceUsefulness,
                suggestedDisposition = record.Item1.SuggestedDisposition,
                hasMismatch = record.Item1.HasMismatch,
                latencyMs = record.Item2?.LatencyMs,
                promptTokens = record.Item2?.PromptTokens,
                completionTokens = record.Item2?.CompletionTokens,
                parseSucceeded = record.Item2?.ParseSucceeded,
                rawContent = record.Item2?.RawContent,
            }),
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(document,
            new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
        return summary;
    }

    private static int CountKind(IEnumerable<Entry> entries, string kind)
        => entries.Count(entry => string.Equals(entry.Kind, kind, StringComparison.Ordinal));

    private sealed record Entry(
        long SequenceNumber,
        string ObservationRef,
        long EvidenceRevision,
        string? NodeRef,
        string? SourceNodeRef,
        string? CurrentPage,
        string? PriorPage,
        string FastResolution,
        bool AdvisorInvoked,
        string? AcquisitionIssue,
        string Availability,
        bool IsCurrent,
        bool ConflictsWithFast,
        string? Kind,
        string? Scene,
        string? ContainerSemantic,
        string? TriggerSemantic,
        string? CandidateInterpretation,
        string? CorrectedIdentity,
        string? EvidenceUsefulness,
        string? SuggestedDisposition,
        bool HasMismatch);

    public void Dispose()
    {
        _queue.Dispose();
    }
}

/// <summary>Summary metrics of one run's Slow shadow ledger.</summary>
public sealed record SlowShadowLedgerSummary(
    int SlowInvocations,
    int AdvisorInvoked,
    int Confirm,
    int Challenge,
    int Correct,
    int Insufficient,
    int ConflictsWithFast,
    int ParseFailures,
    double AverageLatencyMs,
    double MaxLatencyMs,
    long TotalPromptTokens,
    long TotalCompletionTokens);
