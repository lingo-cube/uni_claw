using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using UniClaw.Runtime.Adapters;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;

namespace UniClaw.Runtime.ValidationHarness.SettingsCampaign.SlowShadow;

/// <summary>
/// One observed frame's shadow context, captured harness-side at observation
/// time and consumed by the advisor when the queued assessment runs. Pure
/// correlation data; never a Runtime input.
/// </summary>
public sealed record ShadowFrameContext(
    long SequenceNumber,
    byte[] PngBytes,
    int Width,
    int Height,
    string? CurrentContainer,
    string? PriorContainer,
    string CandidatesJson);

/// <summary>
/// One recorded Slow shadow invocation with its full metric payload
/// (SLOW_SEMANTIC_SHADOW experiment ledger entry).
/// </summary>
public sealed record SlowShadowMetric(
    long SequenceNumber,
    string ObservationRef,
    long EvidenceRevision,
    string? NodeRef,
    string? SourceNodeRef,
    bool AdvisorInvoked,
    string? AcquisitionIssue,
    double LatencyMs,
    long? PromptTokens,
    long? CompletionTokens,
    string? RawContent,
    bool ParseSucceeded,
    string AssessmentKind,
    string Scene,
    string? ContainerSemantic,
    string? TriggerSemantic,
    string? CandidateInterpretation,
    string? CorrectedIdentity,
    string EvidenceUsefulness,
    string SuggestedDisposition,
    bool ConflictsWithFast,
    string Availability);

/// <summary>
/// Frame-context registration seam for the shadow evaluator (the artifact /
/// observation taps register what the advisor will need per frame).
/// Harness-experiment-local; not part of the purchased Runtime port.
/// </summary>
public interface IShadowFrameStore
{
    /// <summary>Registers one frame's shadow context (harness tap).</summary>
    void RegisterFrame(ShadowFrameContext context);

    /// <summary>Merges the observable container context into an existing
    /// registration.</summary>
    void MergeContainerContext(long sequenceNumber, string? currentContainer, string? priorContainer);
}

/// <summary>
/// Concrete ISlowContainerSemanticAdvisor implementation for the bounded
/// SLOW_SEMANTIC_SHADOW experiment: a local Qwen2.5-VL UI-reasoning model
/// served by llama-server (OpenAI-compatible /v1/chat/completions with the
/// frame's PNG screenshot). EXPERIMENT ONLY — Shadow mode; never composed
/// into the production Agent (which stays Disabled), never consumed by any
/// Action / GoalEvidence / Graph / obligation path.
/// NEW_SYMBOL_JUSTIFICATION: the purchased R5 port
/// (ISlowContainerSemanticAdvisor) had no concrete provider; this harness
/// local implementation is the experiment's provider seam and lives outside
/// src/UniClaw.Runtime so no production project gains a model dependency.
/// </summary>
public sealed class QwenVlSlowAdvisor : ISlowContainerSemanticAdvisor, IShadowFrameStore, IDisposable
{
    private const string ModelAlias = "qwen2.5-vl-3b-ui-r1";
    private const int MaxCandidatesInPrompt = 40;

    // Instrument v2 (SLOW_SEMANTIC_SHADOW round 2+): v1 measured container
    // identity agreement only — the model concurred with the fast identity on
    // every frame but expressed it through the "correct" channel (identity-
    // concurring corrections), and volunteered NO candidate-level
    // interpretation, which is exactly the channel the campaign's actual
    // blocker class (affordance-level Unknown) hinges on.  v2 asks the
    // candidate-role question explicitly.
    private static readonly string SystemPrompt =
        """
        You are a slow semantic advisor for a mobile UI runtime. You receive one
        fresh screenshot, the structured perception candidates extracted from it
        (text, kind, normalized bounds), the runtime's current container identity
        (the fast resolution for this frame), and the prior container identity.
        You have two jobs:
        1. Judge the fast container identity.
        2. Judge the INTERACTION ROLE of the text candidates: which are
           interactive targets (a tappable list row or control) and which are
           non-interactive elements (section label, group header, description,
           subtitle, decoration). Judge from the visual layout: a small-font
           line directly above a list row is that row's group label, not a row;
           a short line directly below a row is its description.
        Reply with ONE JSON object and no other text, with exactly these fields:
        kind: "confirm" | "challenge" | "correct" | "insufficient"
          confirm = the fast current-container identity is correct;
          challenge = it is wrong but you cannot supply the right one;
          correct = it is wrong and corrected_identity holds the right one;
          insufficient = the evidence does not support a semantic judgment.
        scene: "unknown" | "normal" | "advertisement" | "transient" | "loading"
               | "overlay" | "unrelated" | "off_path" | "wrong_child"
        container_semantic: one short sentence naming or describing the page
        corrected_identity: the corrected container identity, or null
        evidence_usefulness: "useful" | "not_useful" | "unknown"
        suggested_disposition: "none" | "retain_evidence" | "reassess_fresh_evidence"
        candidate_interpretation: a JSON list, one object per text candidate you
          can judge: {"text": "...", "role": "interactive" | "non_interactive"
          | "unclear", "reason": "one short phrase"}
        """;

    private readonly HttpClient _client;

    private readonly ConcurrentDictionary<long, ShadowFrameContext> _frames = new();
    private readonly ConcurrentQueue<SlowShadowMetric> _metrics = new();

    /// <summary>Production ctor: the local llama-server provider endpoint.</summary>
    public QwenVlSlowAdvisor(string baseUrl, TimeSpan timeout)
        : this(new HttpClientHandler(), baseUrl, timeout)
    {
    }

    /// <summary>Test ctor: scripted HTTP handler injection (the handler owns
    /// the response; the base URL is nominal).</summary>
    public QwenVlSlowAdvisor(HttpMessageHandler handler, string baseUrl, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentNullException.ThrowIfNull(handler);
        _client = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = timeout,
        };
    }

    /// <summary>All recorded invocation metrics, in completion order.</summary>
    public IReadOnlyList<SlowShadowMetric> Metrics => _metrics.ToList();

    /// <summary>Registers one frame's shadow context (harness tap, before the
    /// queued assessment for that frame runs).</summary>
    public void RegisterFrame(ShadowFrameContext context) => _frames[context.SequenceNumber] = context;

    /// <summary>Merges the observable container context into an existing frame
    /// registration (the artifact tap registered the PNG/candidates first; the
    /// observation tap then adds the resolved/prior container identities). A
    /// missing registration (artifact tap absent) creates a context without
    /// pixels — the assessment for it fails closed to Insufficient.</summary>
    public void MergeContainerContext(long sequenceNumber, string? currentContainer, string? priorContainer)
    {
        _frames.AddOrUpdate(
            sequenceNumber,
            _ => new ShadowFrameContext(
                sequenceNumber, [], 0, 0, currentContainer, priorContainer, "{}"),
            (_, existing) => existing with
            {
                CurrentContainer = currentContainer,
                PriorContainer = priorContainer,
            });
    }

    /// <summary>
    /// Assesses one exact evidence binding. The advisor ALWAYS echoes the
    /// request binding (revision-bound correlation), derives its bounded
    /// interpretation from the frame's screenshot + structured candidates,
    /// and fails closed to an Insufficient assessment on any error — a
    /// provider fault never escapes into the harness run.
    /// </summary>
    public async Task<SlowContainerSemanticAssessment> AssessAsync(
        SlowContainerSemanticRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sequence = ParseSequence(request.ObservationRef);
        var started = System.Diagnostics.Stopwatch.StartNew();
        string? raw = null;
        long? promptTokens = null;
        long? completionTokens = null;
        try
        {
            if (sequence is null
                || !_frames.TryGetValue(sequence.Value, out var frame)
                || frame.PngBytes.Length == 0)
                throw new InvalidOperationException(
                    $"shadow frame context unavailable for {request.ObservationRef}");

            var payload = BuildPayload(frame, request);
            using var response = await _client.PostAsync(
                "v1/chat/completions",
                new StringContent(payload, Encoding.UTF8, "application/json"),
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            raw = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            if (document.RootElement.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var pt) && pt.ValueKind == JsonValueKind.Number)
                    promptTokens = pt.GetInt64();
                if (usage.TryGetProperty("completion_tokens", out var ct) && ct.ValueKind == JsonValueKind.Number)
                    completionTokens = ct.GetInt64();
            }

            var parsed = ParseAssessment(raw)
                ?? throw new InvalidOperationException(
                    "advisor output is not a bounded JSON assessment");
            started.Stop();
            Record(
                request, sequence ?? -1, started.Elapsed.TotalMilliseconds,
                promptTokens, completionTokens, raw, parseSucceeded: true,
                acquisitionIssue: null,
                kind: parsed.Kind.ToString(), scene: parsed.Scene.ToString(),
                containerSemantic: parsed.ContainerSemantic,
                triggerSemantic: parsed.TriggerSemantic,
                candidateInterpretation: parsed.CandidateInterpretation,
                correctedIdentity: parsed.CorrectedIdentity,
                usefulness: parsed.Usefulness.ToString(),
                disposition: parsed.Disposition.ToString(),
                conflictsWithFast: false,
                availability: "raw");
            return BuildAssessment(request, parsed);
        }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            started.Stop();
            var issue = $"advisor fail-closed: {error.GetType().Name}: {error.Message}";
            Record(
                request, sequence ?? -1, started.Elapsed.TotalMilliseconds,
                promptTokens, completionTokens, raw, parseSucceeded: false,
                acquisitionIssue: issue,
                kind: "Insufficient", scene: "Unknown",
                containerSemantic: null, triggerSemantic: null,
                candidateInterpretation: null, correctedIdentity: null,
                usefulness: "Unknown", disposition: "None",
                conflictsWithFast: false,
                availability: "raw");
            return new SlowContainerSemanticAssessment(
                request.ObservationRef,
                request.EvidenceRevision,
                SlowContainerSemanticAssessmentKind.Insufficient,
                SlowContainerSceneKind.Unknown,
                request.NodeRef,
                request.SourceNodeRef,
                request.TriggerOccurrenceRef,
                request.TransitionOccurrenceRef,
                details: issue);
        }
    }

    private string BuildPayload(ShadowFrameContext frame, SlowContainerSemanticRequest request)
    {
        var contextBlock = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["observation_ref"] = request.ObservationRef,
            ["evidence_revision"] = request.EvidenceRevision.Value,
            ["fast_current_container"] = frame.CurrentContainer,
            ["prior_container"] = frame.PriorContainer,
            ["candidates"] = JsonDocument.Parse(frame.CandidatesJson).RootElement.Clone(),
        });
        var message = new Dictionary<string, object?>
        {
            ["model"] = ModelAlias,
            ["temperature"] = 0.1,
            ["max_tokens"] = 700,
            ["messages"] = new object[]
            {
                new Dictionary<string, object?> { ["role"] = "system", ["content"] = SystemPrompt },
                new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["content"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["type"] = "image_url",
                            ["image_url"] = new Dictionary<string, string?>
                            {
                                ["url"] = $"data:image/png;base64,{Convert.ToBase64String(frame.PngBytes)}",
                            },
                        },
                        new Dictionary<string, object?>
                        {
                            ["type"] = "text",
                            ["text"] = "Screenshot evidence for this frame:\n" + contextBlock,
                        },
                    },
                },
            },
        };
        return JsonSerializer.Serialize(message);
    }

    private static long? ParseSequence(string observationRef)
    {
        var separator = observationRef.LastIndexOf(':');
        return separator >= 0 && long.TryParse(observationRef[(separator + 1)..], out var sequence)
            ? sequence
            : null;
    }

    /// <summary>Parses the bounded JSON assessment; null when the model
    /// output is not parseable (the caller fails closed — unparseable model
    /// output is never a semantic claim).</summary>
    private static ParsedAssessment? ParseAssessment(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;
        try
        {
            using var document = JsonDocument.Parse(
                content.Substring(start, end - start + 1));
            var root = document.RootElement;
            string? Field(string name)
                => root.TryGetProperty(name, out var value)
                    && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
            // candidate_interpretation: a JSON list of per-candidate role
            // judgments (v2 instrument) — retained compactly; a bare string
            // (v1 instrument) is kept verbatim.
            string? candidateInterpretation = null;
            if (root.TryGetProperty("candidate_interpretation", out var interpretation))
            {
                candidateInterpretation = interpretation.ValueKind switch
                {
                    JsonValueKind.String => interpretation.GetString(),
                    JsonValueKind.Array => JsonSerializer.Serialize(interpretation),
                    _ => null,
                };
            }
            return new ParsedAssessment(
                MapKind(Field("kind")),
                MapScene(Field("scene")),
                Field("container_semantic"),
                Field("trigger_semantic"),
                candidateInterpretation,
                Field("corrected_identity"),
                MapUsefulness(Field("evidence_usefulness")),
                MapDisposition(Field("suggested_disposition")));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static SlowContainerSemanticAssessment BuildAssessment(
        SlowContainerSemanticRequest request, ParsedAssessment parsed)
        => new(
            request.ObservationRef,
            request.EvidenceRevision,
            parsed.Kind,
            parsed.Scene,
            request.NodeRef,
            request.SourceNodeRef,
            request.TriggerOccurrenceRef,
            request.TransitionOccurrenceRef,
            parsed.CorrectedIdentity,
            parsed.Disposition,
            parsed.CandidateInterpretation,
            parsed.ContainerSemantic,
            parsed.TriggerSemantic,
            relationSemantic: null,
            parsed.Usefulness);

    private static SlowContainerSemanticAssessmentKind MapKind(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "confirm" => SlowContainerSemanticAssessmentKind.Confirm,
        "challenge" => SlowContainerSemanticAssessmentKind.Challenge,
        "correct" => SlowContainerSemanticAssessmentKind.Correct,
        _ => SlowContainerSemanticAssessmentKind.Insufficient,
    };

    private static SlowContainerSceneKind MapScene(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "normal" => SlowContainerSceneKind.Normal,
        "advertisement" => SlowContainerSceneKind.Advertisement,
        "transient" => SlowContainerSceneKind.Transient,
        "loading" => SlowContainerSceneKind.Loading,
        "overlay" => SlowContainerSceneKind.Overlay,
        "unrelated" => SlowContainerSceneKind.Unrelated,
        "off_path" or "offpath" => SlowContainerSceneKind.OffPath,
        "wrong_child" or "wrongchild" => SlowContainerSceneKind.WrongChild,
        _ => SlowContainerSceneKind.Unknown,
    };

    private static SlowContainerEvidenceUsefulness MapUsefulness(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "useful" => SlowContainerEvidenceUsefulness.Useful,
        "not_useful" or "notuseful" => SlowContainerEvidenceUsefulness.NotUseful,
        _ => SlowContainerEvidenceUsefulness.Unknown,
    };

    private static SlowContainerSemanticDisposition MapDisposition(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "retain_evidence" or "retainevidence" => SlowContainerSemanticDisposition.RetainEvidence,
        "reassess_fresh_evidence" or "reassessfreshevidence" => SlowContainerSemanticDisposition.ReassessFreshEvidence,
        _ => SlowContainerSemanticDisposition.None,
    };

    private void Record(
        SlowContainerSemanticRequest request,
        long sequence,
        double latencyMs,
        long? promptTokens,
        long? completionTokens,
        string? rawContent,
        bool parseSucceeded,
        string? acquisitionIssue,
        string kind,
        string scene,
        string? containerSemantic,
        string? triggerSemantic,
        string? candidateInterpretation,
        string? correctedIdentity,
        string usefulness,
        string disposition,
        bool conflictsWithFast,
        string availability)
    {
        var truncated = rawContent is null
            ? null
            : rawContent.Length > 2000 ? rawContent[..2000] : rawContent;
        _metrics.Enqueue(new SlowShadowMetric(
            sequence,
            request.ObservationRef,
            request.EvidenceRevision.Value,
            request.NodeRef?.Value,
            request.SourceNodeRef?.Value,
            AdvisorInvoked: true,
            acquisitionIssue,
            Math.Round(latencyMs, 1),
            promptTokens,
            completionTokens,
            truncated,
            parseSucceeded,
            kind,
            scene,
            containerSemantic,
            triggerSemantic,
            candidateInterpretation,
            correctedIdentity,
            usefulness,
            disposition,
            conflictsWithFast,
            availability));
    }

    /// <summary>Builds the compact prompt candidates block from one artifact tap.</summary>
    public static string BuildCandidatesJson(ImmutableArray<PerceptionCandidate> candidates)
    {
        var list = candidates
            .Take(MaxCandidatesInPrompt)
            .Select(candidate => new Dictionary<string, object?>
            {
                ["t"] = candidate.Text,
                ["k"] = candidate.Type,
                ["b"] = candidate.Bounds is { } bounds
                    ? new[] { bounds.X1, bounds.Y1, bounds.X2, bounds.Y2 }
                    : null,
            })
            .ToList();
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["count"] = candidates.Length,
            ["included"] = list.Count,
            ["items"] = list,
        });
    }

    private sealed record ParsedAssessment(
        SlowContainerSemanticAssessmentKind Kind,
        SlowContainerSceneKind Scene,
        string? ContainerSemantic,
        string? TriggerSemantic,
        string? CandidateInterpretation,
        string? CorrectedIdentity,
        SlowContainerEvidenceUsefulness Usefulness,
        SlowContainerSemanticDisposition Disposition);

    public void Dispose() => _client.Dispose();
}
