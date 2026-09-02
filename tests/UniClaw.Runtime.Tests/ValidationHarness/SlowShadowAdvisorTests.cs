using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.Json;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.World;
using UniClaw.Runtime.ValidationHarness.SettingsCampaign.SlowShadow;
using Xunit;

namespace UniClaw.Runtime.Tests.ValidationHarness;

/// <summary>
/// SLOW_SEMANTIC_SHADOW (bounded experiment) — advisor mapping and evaluator
/// coverage. These tests verify CAPABILITY: revision-bound request
/// construction, bounded output mapping, fail-closed provider behavior, and
/// Shadow-mode acquisition/projection through the real purchased seam —
/// never fixed scripts.
/// </summary>
public sealed class SlowShadowAdvisorTests
{
    private static byte[] DummyPng => [1, 2, 3, 4];

    private sealed class ScriptedHandler(Func<string> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(respond(), Encoding.UTF8, "application/json"),
            };
        }
    }

    private static string OpenAiResponse(string content) => JsonSerializer.Serialize(new
    {
        choices = new[] { new { message = new { content } } },
        usage = new { prompt_tokens = 521, completion_tokens = 42 },
    });

    private static SlowContainerSemanticRequest Request(
        long sequence, string? current = "ChildPage", string? prior = null)
    {
        var destination = current is null ? (ContainerNodeRef?)null : new ContainerNodeRef($"agent-node:{sequence}:{current}");
        var source = prior is null ? (ContainerNodeRef?)null : new ContainerNodeRef($"agent-node:prior:{prior}");
        var fast = new FastContainerAssessment(
            FastContainerResolutionKind.NEW_CONTAINER,
            new SemanticEvidenceRevision(sequence),
            source,
            destination,
            current,
            null,
            independentBoundarySupport: true,
            semanticSupport: true,
            triggerDestinationSemanticMatch: true,
            hardConflict: false,
            isAbstained: false);
        return new SlowContainerSemanticRequest(
            $"observation:{sequence}",
            new SemanticEvidenceRevision(sequence),
            destination,
            source,
            fastAssessment: fast);
    }

    private static void RegisterFrame(QwenVlSlowAdvisor advisor, long sequence, string? current, string? prior)
        => advisor.RegisterFrame(new ShadowFrameContext(
            sequence, DummyPng, 1080, 1920, current, prior, "{}"));

    [Fact]
    public async Task Advisor_MapsBoundedOutput_AndEchoesBinding()
    {
        using var advisor = new QwenVlSlowAdvisor(
            new ScriptedHandler(() => OpenAiResponse("""
            {"kind":"correct","scene":"wrong_child","container_semantic":"the page is the accessibility list",
             "trigger_semantic":null,"candidate_interpretation":"the top rows are category headers",
             "corrected_identity":"AccessibilityPage","evidence_usefulness":"useful",
             "suggested_disposition":"retain_evidence"}
            """)),
            "http://provider.test", TimeSpan.FromSeconds(10));
        RegisterFrame(advisor, 12, "WrongPage", "RootPage");

        var request = Request(12);
        var assessment = await advisor.AssessAsync(request);

        Assert.Equal(request.ObservationRef, assessment.ObservationRef);
        Assert.Equal(request.EvidenceRevision, assessment.EvidenceRevision);
        Assert.Equal(request.NodeRef, assessment.NodeRef);
        Assert.Equal(request.SourceNodeRef, assessment.SourceNodeRef);
        Assert.Equal(SlowContainerSemanticAssessmentKind.Correct, assessment.Kind);
        Assert.Equal(SlowContainerSceneKind.WrongChild, assessment.Scene);
        Assert.Equal("AccessibilityPage", assessment.CorrectedIdentityCandidate);
        Assert.Equal("the page is the accessibility list", assessment.ContainerSemantic);
        Assert.Equal("the top rows are category headers", assessment.Details);
        Assert.Equal(SlowContainerEvidenceUsefulness.Useful, assessment.EvidenceUsefulness);
        Assert.Equal(SlowContainerSemanticDisposition.RetainEvidence, assessment.SuggestedDisposition);
        Assert.True(assessment.HasMismatch);

        var metric = Assert.Single(advisor.Metrics);
        Assert.Equal(12, metric.SequenceNumber);
        Assert.True(metric.ParseSucceeded);
        Assert.Equal("Correct", metric.AssessmentKind);
        Assert.Equal(521, metric.PromptTokens);
        Assert.Equal(42, metric.CompletionTokens);
        Assert.True(metric.LatencyMs >= 0);
    }

    [Fact]
    public async Task Advisor_UnparseableOutput_FailsClosedToInsufficient()
    {
        using var advisor = new QwenVlSlowAdvisor(
            new ScriptedHandler(() => OpenAiResponse("I think this is a page about colors.")),
            "http://provider.test", TimeSpan.FromSeconds(10));
        RegisterFrame(advisor, 5, "P", null);

        var assessment = await advisor.AssessAsync(Request(5));

        Assert.Equal(SlowContainerSemanticAssessmentKind.Insufficient, assessment.Kind);
        Assert.Equal(SlowContainerSceneKind.Unknown, assessment.Scene);
        Assert.Contains("fail-closed", assessment.Details);
        // The raw model output is retained as FalseCorrection-review evidence.
        var metric = Assert.Single(advisor.Metrics);
        Assert.False(metric.ParseSucceeded);
        Assert.Contains("page about colors", metric.RawContent);
    }

    [Fact]
    public async Task Advisor_MissingFrameContext_FailsClosed()
    {
        using var advisor = new QwenVlSlowAdvisor(
            new ScriptedHandler(() => OpenAiResponse("{}")),
            "http://provider.test", TimeSpan.FromSeconds(10));

        var assessment = await advisor.AssessAsync(Request(99));

        Assert.Equal(SlowContainerSemanticAssessmentKind.Insufficient, assessment.Kind);
        Assert.Contains("unavailable", assessment.Details);
    }

    [Fact]
    public async Task Advisor_ProviderFault_NeverEscapes()
    {
        using var advisor = new QwenVlSlowAdvisor(
            new ScriptedHandler(() => throw new HttpRequestException("provider down")),
            "http://provider.test", TimeSpan.FromSeconds(5));
        RegisterFrame(advisor, 3, "P", null);

        var assessment = await advisor.AssessAsync(Request(3));

        Assert.Equal(SlowContainerSemanticAssessmentKind.Insufficient, assessment.Kind);
        Assert.Contains("fail-closed", assessment.Details);
    }

    [Fact]
    public async Task MergeContainerContext_KeepsArtifactPixels()
    {
        using var handler = new ScriptedHandler(() => OpenAiResponse("""
            {"kind":"confirm","scene":"normal","container_semantic":"ok",
             "trigger_semantic":null,"candidate_interpretation":null,
             "corrected_identity":null,"evidence_usefulness":"useful",
             "suggested_disposition":"none"}
            """));
        using var advisor = new QwenVlSlowAdvisor(handler, "http://provider.test", TimeSpan.FromSeconds(5));
        RegisterFrame(advisor, 7, null, null);
        advisor.MergeContainerContext(7, "RootPage", null);

        var assessment = await advisor.AssessAsync(Request(7, "RootPage"));

        Assert.Equal(SlowContainerSemanticAssessmentKind.Confirm, assessment.Kind);
        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("image/png;base64,", handler.LastRequestBody);
        // The merged registration kept the artifact pixels (non-trivial
        // image data reached the provider) AND the container context (the
        // context block is JSON-escaped inside the user text).
        Assert.Contains("fast_current_container", handler.LastRequestBody);
        Assert.Contains("RootPage", handler.LastRequestBody);
    }
}

/// <summary>
/// Evaluator coverage: revision-bound request construction through the REAL
/// purchased seam (Shadow-mode acquisition + projection) with a scripted
/// advisor, plus ledger summary accounting.
/// </summary>
public sealed class SlowShadowEvaluatorTests
{
    private sealed class ScriptedAdvisor : ISlowContainerSemanticAdvisor
    {
        public List<SlowContainerSemanticRequest> Requests { get; } = [];

        public Task<SlowContainerSemanticAssessment> AssessAsync(
            SlowContainerSemanticRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            // Challenge when the fast resolution claims a NEW container — the
            // scripted pattern exercises ConflictsWithFast projection.
            var kind = request.FastAssessment?.Resolution == FastContainerResolutionKind.NEW_CONTAINER
                && request.FastAssessment?.CurrentNodeRef is not null
                ? SlowContainerSemanticAssessmentKind.Challenge
                : SlowContainerSemanticAssessmentKind.Confirm;
            return Task.FromResult(new SlowContainerSemanticAssessment(
                request.ObservationRef,
                request.EvidenceRevision,
                kind,
                SlowContainerSceneKind.Normal,
                request.NodeRef,
                request.SourceNodeRef,
                request.TriggerOccurrenceRef,
                request.TransitionOccurrenceRef,
                details: "scripted"));
        }
    }

    private static Observation Frame(long sequence) => new(
        ImmutableArray.Create(new ObservedElement(
            "Row A", null, 0, new ElementBounds(0f, 0.1f, 1f, 0.12f), "menu_item")),
        "app",
        sequence);

    [Fact]
    public async Task Evaluator_AcquiresInShadowMode_AndWritesLedger()
    {
        var scripted = new ScriptedAdvisor();
        using var evaluator = new SlowShadowEvaluator(scripted);

        evaluator.OnObservation(Frame(7), "RootPage");
        evaluator.OnObservation(Frame(8), "RootPage");
        evaluator.OnObservation(Frame(9), "ChildPage");

        var path = Path.Combine(Path.GetTempPath(), $"p26-slow-shadow-test-{Guid.NewGuid():N}.json");
        var summary = await evaluator.DrainAndWriteAsync(path, TimeSpan.FromSeconds(10));

        Assert.Equal(3, summary.SlowInvocations);
        // Frame 9 resolves a NEW container with a prior node ⇒ the scripted
        // advisor challenges it ⇒ ConflictsWithFast counts once.
        Assert.Equal(2, summary.Confirm);
        Assert.Equal(1, summary.Challenge);
        Assert.Equal(1, summary.ConflictsWithFast);

        Assert.True(File.Exists(path));
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("Shadow", document.RootElement.GetProperty("mode").GetString());
        Assert.False(document.RootElement.GetProperty("hasRuntimeEffect").GetBoolean());
        Assert.Equal(3, document.RootElement.GetProperty("entries").GetArrayLength());
        // Every entry is revision-bound to its own observation sequence.
        foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
        {
            Assert.Equal(
                entry.GetProperty("SequenceNumber").GetInt64(),
                entry.GetProperty("evidenceRevision").GetInt64());
            Assert.Equal(
                $"observation:{entry.GetProperty("SequenceNumber").GetInt64()}",
                entry.GetProperty("ObservationRef").GetString());
        }
        File.Delete(path);
    }

    [Fact]
    public async Task Evaluator_MergeContainerContext_GapRecording()
    {
        // TriggerOccurrence / TransitionOccurrence stay null at the harness
        // read surface — the recorded gap is itself experimental evidence.
        var scripted = new ScriptedAdvisor();
        using var evaluator = new SlowShadowEvaluator(scripted);

        evaluator.OnObservation(Frame(1), "RootPage");
        var path = Path.Combine(Path.GetTempPath(), $"p26-slow-shadow-test-{Guid.NewGuid():N}.json");
        await evaluator.DrainAndWriteAsync(path, TimeSpan.FromSeconds(10));

        var request = Assert.Single(scripted.Requests);
        Assert.Null(request.TriggerOccurrenceRef);
        Assert.Null(request.TransitionOccurrenceRef);
        File.Delete(path);
    }
}
