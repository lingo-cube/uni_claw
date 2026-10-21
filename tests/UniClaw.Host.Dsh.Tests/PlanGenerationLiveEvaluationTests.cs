using System.Diagnostics;
using System.Text;
using System.Text.Json;
using UniClaw.Agent.Dsh;
using UniClaw.Agent.Profile;
using UniClaw.Host;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Host.Dsh.Tests;

[CollectionDefinition("PlanGenerationLive", DisableParallelization = true)]
public sealed class PlanGenerationLiveCollection;

/// <summary>Explicitly enabled model-only evaluation; expected behavior is independently frozen.</summary>
[Collection("PlanGenerationLive")]
public sealed class PlanGenerationLiveEvaluationTests(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions EvidenceJson = new() { WriteIndented = true };

    [Fact]
    public async Task Preflight_RealModelAndRawTrajectoryBeforeFormalBatch()
    {
        if (Environment.GetEnvironmentVariable("UNICLAW_PLAN004_PREFLIGHT") != "1") return;
        var root = RuntimeToolHostTests.RepoRoot();
        using var registration = ReadFrozenRegistration(root);
        using var corpus = ReadCorpus(root);
        var item = corpus.RootElement.GetProperty("cases").EnumerateArray()
            .Single(c => c.GetProperty("id").GetString() == "already-satisfied");
        var directory = Path.Combine(root, "evidence/runtime-plan-004/generation",
            registration.RootElement.GetProperty("epochDirectory").GetString()! + "-preflight");
        Assert.False(Directory.Exists(directory), "Do not overwrite preflight evidence; register a new epoch.");
        Directory.CreateDirectory(directory);
        File.Copy(RegistrationPath(root), Path.Combine(directory, "preregistration.json"));
        var result = await RunTrial(root, registration.RootElement, item, 0, directory);
        var passed = result.FinalPass && result.FirstPass;
        var record = new
        {
            passed, recordedUtc = DateTimeOffset.UtcNow, modelCalls = 1,
            preregistrationSha256 = Hash(RegistrationPath(root)),
            formalFlowStartedAfterPreflight = true, deviceRequired = false, visualWarmupRequired = false,
            scope = "actual model consult plus formal codec and raw DSH session export; UI facts are explicit fixtures",
            trial = result,
        };
        WriteJson(Path.Combine(directory, "environment-preflight.json"), record);
        Assert.True(passed, string.Join("; ", result.Errors));
    }

    [Fact]
    public async Task PreregisteredCases_UseOfficialDshChannelAndPreserveRawTraffic()
    {
        if (Environment.GetEnvironmentVariable("UNICLAW_PLAN004_LIVE") != "1") return;
        var root = RuntimeToolHostTests.RepoRoot();
        using var registration = ReadFrozenRegistration(root);
        var epoch = registration.RootElement.GetProperty("epochDirectory").GetString()!;
        var preflightPath = Path.Combine(root, "evidence/runtime-plan-004/generation", epoch + "-preflight/environment-preflight.json");
        using var preflight = JsonDocument.Parse(File.ReadAllText(preflightPath));
        Assert.True(preflight.RootElement.GetProperty("passed").GetBoolean(), "ENVIRONMENT_UNAVAILABLE: preflight failed");
        Assert.Equal(Hash(RegistrationPath(root)), preflight.RootElement.GetProperty("preregistrationSha256").GetString());
        using var corpus = ReadCorpus(root);
        var cases = corpus.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(7, cases.Length);
        var epochRoot = Path.Combine(root, "evidence/runtime-plan-004/generation", epoch);
        Assert.False(Directory.Exists(epochRoot), "Do not overwrite a formal batch; freeze a new epoch.");
        Directory.CreateDirectory(epochRoot);
        File.Copy(RegistrationPath(root), Path.Combine(epochRoot, "preregistration.json"));
        var results = new List<TrialAssessment>();
        foreach (var item in cases)
        {
            var caseId = item.GetProperty("id").GetString()!;
            for (var repeat = 1; repeat <= 3; repeat++)
            {
                var result = await RunTrial(root, registration.RootElement, item, repeat,
                    Path.Combine(epochRoot, caseId, repeat.ToString("D2")));
                results.Add(result);
                // Persist after each trial: interruption and failures never erase the partial batch.
                WriteJson(Path.Combine(epochRoot, "batch-progress.json"), results);
                output.WriteLine($"{caseId}/{repeat}: first={result.FirstPass} final={result.FinalPass}; {string.Join("; ", result.Errors)}");
            }
        }
        var firstPass = results.Count(r => r.FirstPass);
        var finalPass = results.Count(r => r.FinalPass);
        var perCase = results.GroupBy(r => r.CaseId).ToDictionary(g => g.Key, g => g.Count(r => r.FinalPass));
        WriteJson(Path.Combine(epochRoot, "batch-assessment.json"), new
        {
            firstPass, finalPass, total = results.Count, perCase, results,
            cost = "unknown", kernelConsumerStatus = "separate mandatory consumption check; not inferred from these model predicates",
        });
        Assert.Equal(21, results.Count);
        Assert.InRange(firstPass, 19, 21);
        Assert.Equal(21, finalPass);
        Assert.All(perCase, pair => Assert.Equal(3, pair.Value));
    }

    [Fact]
    public void RejectionFeedback_ComesFromFormalCodecWithoutChangingObjective()
    {
        using var item = JsonDocument.Parse("""
            {"id":"rejection-repair","task":"打开当前网络入口并确认进入","facts":{"target":"网络","allowedEffects":["tap"]}}
            """);
        var original = BuildContext(item.RootElement, "local-session", "local-run");
        var directory = Directory.CreateTempSubdirectory("plan004-rejection-").FullName;
        try
        {
            var revised = AddRealRejectionSeed(original, directory);
            Assert.Equal(original.Objective, revised.Objective);
            Assert.Equal(original.AllowedEffects, revised.AllowedEffects);
            Assert.Contains("targetRole", revised.CurrentWorldClaims["fixture.actualCodecRejection"].Value);
            var bad = revised.CurrentWorldClaims["fixture.rejectedCandidate"].Value;
            Assert.Throws<JsonException>(() => PlanGenerationSemanticAssertions.DecodeOfficially(bad));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static async Task<TrialAssessment> RunTrial(
        string root, JsonElement registration, JsonElement item, int repeat, string directory)
    {
        Directory.CreateDirectory(directory);
        var bindings = UniagentDshBindingsYaml.LoadDefault();
        Assert.Equal(3081, bindings.Service.BaseUri.Port);
        var profile = UniAgentProfileYaml.LoadDefault();
        var selected = DshModelManagement.ResolveDshModel(DshModelManagement.FromBindings(bindings, profile), LogicalProfileId.AgentDecision);
        Assert.Equal(registration.GetProperty("model").GetProperty("provider").GetString(), selected.Provider);
        Assert.Equal(registration.GetProperty("model").GetProperty("name").GetString(), selected.Name);
        var caseId = item.GetProperty("id").GetString()!;
        var sessionId = "plan004-" + caseId + "-" + repeat + "-" + Guid.NewGuid().ToString("N");
        var runId = registration.GetProperty("epochDirectory").GetString()! + "-" + caseId + "-" + repeat;
        var context = BuildContext(item, sessionId, runId);
        if (caseId == "rejection-repair") context = AddRealRejectionSeed(context, directory);
        WriteJson(Path.Combine(directory, "task-expectation-identities.json"), new
        {
            taskSha256 = HashText(context.Objective),
            expectationSha256 = HashText(item.GetProperty("must").GetRawText() + item.GetProperty("forbidden").GetRawText()),
            mutationPolicy = "Goal and independent expectations are frozen; only candidate decisions may be repaired",
        });
        File.WriteAllText(Path.Combine(directory, "input-context.json"), JsonSerializer.Serialize(context, ProductProtocolJson.CreateOptions()));
        WriteJson(Path.Combine(directory, "artifact-identities.json"), new
        {
            profileRevision = profile.Revision, profileSha256 = Hash(Path.Combine(root, "product/profiles/uniagent-prod.yaml")),
            promptHash = profile.Assembly!.PromptManifest!.Hash, skill = Assert.Single(profile.AllowedSkillRefs!),
            schemaHash = ProductProtocolSchema.Current.SchemaHash, model = selected, productSessionId = sessionId, runId,
            assemblies = new[] { typeof(PlanGenerationLiveEvaluationTests).Assembly, typeof(DshAgentAdapter).Assembly,
                typeof(UniAgentProfile).Assembly, typeof(KernelRunDriver).Assembly }.ToDictionary(a => a.GetName().Name!, a => Hash(a.Location)),
        });
        var errors = new List<string>();
        var firstErrors = new List<string>();
        AgentDecision? decision = null;
        PlanGenerationTrajectorySnapshot? snapshot = null;
        var watch = Stopwatch.StartNew();
        using var traffic = new RecordingMessageHandler();
        await using var peer = new DshOpenedHttpPeer(bindings.Service, handler: traffic,
            model: selected, requestTimeout: TimeSpan.FromSeconds(210), expectedProfile: profile);
        await using var agent = new DshAgentAdapter(new DshOpenedDecisionChannel(peer), sessionId, runId,
            turnTimeout: TimeSpan.FromSeconds(200));
        try
        {
            decision = await agent.ConsultAsync(context);
            watch.Stop();
            if (decision is null)
                errors.Add("DSH returned no decision: " + string.Join(";", agent.Diagnostics.Select(d => d.Code + ":" + d.Message)));
            else
            {
                File.WriteAllText(Path.Combine(directory, "final-decision.json"), PlanGenerationSemanticAssertions.EncodeRoundTrip(decision));
                errors.AddRange(PlanGenerationSemanticAssertions.Assess(caseId, decision));
            }
        }
        catch (Exception ex) { errors.Add(ex.ToString()); }
        finally { watch.Stop(); }
        try
        {
            if (agent.DshSessionId is null || traffic.RequestId is null)
                throw new InvalidOperationException("ENVIRONMENT_UNAVAILABLE: no exact DSH session/request for trajectory");
            snapshot = await PlanGenerationTrajectoryCollector.CaptureAsync(bindings.Service,
                agent.DshSessionId, traffic.RequestId, Path.Combine(directory, "trajectory"));
            WriteJson(Path.Combine(directory, "trajectory-summary.json"), snapshot with { ConsultationRawJsonl = "see trajectory/dsh-consultation.raw.jsonl" });
            AssertMountedSources(root, snapshot);
            if (snapshot.SubmitCalls.Count == 0) errors.Add("no raw submit_decision call");
            if (Math.Max(0, snapshot.SubmitCalls.Count - 1) + snapshot.NoSubmitNudgeCount > 1)
                errors.Add("exceeded one pre-adoption revision");
            foreach (var call in snapshot.SubmitCalls.Skip(1))
                File.WriteAllText(Path.Combine(directory, "repair-draft-" + call.Seq + ".json"), call.RawArguments);
            WriteJson(Path.Combine(directory, "rejection-feedback.json"), snapshot.Rejections);
            if (snapshot.SubmitCalls.FirstOrDefault() is { } first)
            {
                File.WriteAllText(Path.Combine(directory, "first-draft.json"), first.RawArguments);
                if (first.Accepted != true) firstErrors.Add("first submission was not accepted");
                if (snapshot.NoSubmitNudgeCount != 0) firstErrors.Add("initial turn needed a no-submit nudge");
                try
                {
                    using var raw = JsonDocument.Parse(first.RawArguments);
                    var firstDecision = PlanGenerationSemanticAssertions.DecodeOfficially(raw.RootElement.GetProperty("task").GetProperty("payload").GetRawText());
                    firstErrors.AddRange(PlanGenerationSemanticAssertions.Assess(caseId, firstDecision));
                }
                catch (Exception ex) { firstErrors.Add(ex.Message); }
            }
            else firstErrors.Add("no observable first draft");
        }
        catch (Exception ex) { errors.Add("trajectory: " + ex); firstErrors.Add("trajectory unavailable"); }
        finally
        {
            // Each trial owns an independent mapping. Dispose only closes local
            // transport; release our successful attachment after preserving its log.
            if (agent.DshSessionId is not null)
            {
                try { await peer.DetachAsync(CancellationToken.None); }
                catch (Exception ex) { errors.Add("owned-session-detach: " + ex); }
            }
        }
        File.WriteAllLines(Path.Combine(directory, "dsh-consult-request-response.raw.jsonl"), traffic.Lines);
        WriteJson(Path.Combine(directory, "elapsed-usage.json"), new
        {
            formalConsultElapsedMilliseconds = watch.ElapsedMilliseconds,
            usageStatus = snapshot?.UsageStatus ?? "unknown", usage = snapshot?.UsageAttempts,
            inputTokens = snapshot?.InputTokenTotal, outputTokens = snapshot?.OutputTokenTotal,
            totalTokens = snapshot?.TotalTokenTotal, cost = "unknown",
            note = "session export/assessment timing is excluded from formal consultation latency",
        });
        var result = new TrialAssessment(caseId, repeat, errors.Count == 0 && firstErrors.Count == 0,
            errors.Count == 0, firstErrors.ToArray(), errors.ToArray(), snapshot?.SubmitCalls.Count,
            snapshot?.Rejections.Count, snapshot?.NoSubmitNudgeCount);
        WriteJson(Path.Combine(directory, "assessment.json"), result);
        return result;
    }

    private static AgentDecisionContext AddRealRejectionSeed(AgentDecisionContext context, string directory)
    {
        // Deliberately malformed pre-adoption fixture, not a claimed model draft or executed action.
        var bad = JsonSerializer.Serialize(new
        {
            kind = "act", decisionId = context.DecisionId,
            proposal = new { decisionId = context.DecisionId, steps = new[] { new { targetDescriptor = "网络", effectClass = "tap" } } },
        });
        string rejection;
        try
        {
            PlanGenerationSemanticAssertions.DecodeOfficially(bad);
            throw new InvalidOperationException("rejection fixture unexpectedly passed the formal codec");
        }
        catch (JsonException ex) { rejection = ex.ToString(); }
        WriteJson(Path.Combine(directory, "pre-adoption-codec-rejection.json"), new
        {
            source = "explicit fixture rejected by actual ProductProtocolJson; not an earlier model output",
            badCandidate = JsonDocument.Parse(bad).RootElement.Clone(), rejection,
            goalSha256Before = HashText(context.Objective), goalSha256After = HashText(context.Objective), effects = 0,
        });
        var claims = new Dictionary<string, ClaimSummary>(context.CurrentWorldClaims, StringComparer.Ordinal)
        {
            ["fixture.rejectedCandidate"] = new(bad, "Observed", false),
            ["fixture.actualCodecRejection"] = new(rejection, "Observed", false),
        };
        return context with { CurrentWorldClaims = claims };
    }

    private static void AssertMountedSources(string root, PlanGenerationTrajectorySnapshot snapshot)
    {
        var identity = File.ReadAllText(Path.Combine(root, "product/prompt/uniagent-prod/01-identity.txt")).TrimEnd();
        var guidance = File.ReadAllText(Path.Combine(root, "product/skills/android-automotive-ui-testing/guidance.md")).TrimEnd();
        if (snapshot.SystemPrompt is null || !snapshot.SystemPrompt.Contains(identity, StringComparison.Ordinal)
            || !snapshot.SystemPrompt.Contains(guidance, StringComparison.Ordinal))
            throw new InvalidDataException("actual rendered system prompt does not contain the pinned identity and Skill guidance");
        var expected = new[] { "uniagent-prod:product-prompt", "uniagent-prod:skill:android-automotive-ui-testing" };
        if (!snapshot.Sections.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("actual session sections differ from the fixed Product assembly");
    }

    private static JsonDocument ReadFrozenRegistration(string root)
    {
        var document = JsonDocument.Parse(File.ReadAllText(RegistrationPath(root)));
        Assert.Equal("frozen-ready", document.RootElement.GetProperty("status").GetString());
        foreach (var source in document.RootElement.GetProperty("sourceHashes").EnumerateObject())
            Assert.Equal(source.Value.GetString(), Hash(Path.Combine(root, source.Name)));
        return document;
    }

    private static JsonDocument ReadCorpus(string root) => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(root, "evidence/runtime-plan-004/generation/cases.json")));
    private static string RegistrationPath(string root) => Path.Combine(root, "evidence/runtime-plan-004/generation/preregistration.json");
    private static string Hash(string path) => HashBytes(File.ReadAllBytes(path));
    private static string HashText(string text) => HashBytes(Encoding.UTF8.GetBytes(text));
    private static string HashBytes(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
    private static void WriteJson(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, EvidenceJson));

    private sealed record TrialAssessment(string CaseId, int Repeat, bool FirstPass, bool FinalPass,
        string[] FirstErrors, string[] Errors, int? SubmitCalls, int? Rejections, int? Nudges);
    private static AgentDecisionContext BuildContext(JsonElement item, string sessionId, string runId)
    {
        var task = item.GetProperty("task").GetString()!;
        var facts = item.GetProperty("facts");
        var caseId = item.GetProperty("id").GetString();
        var claims = facts.EnumerateObject().ToDictionary(property => "fixture." + property.Name,
            property => new ClaimSummary(property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()! : property.Value.GetRawText(), "Observed", false),
                StringComparer.Ordinal);
        var availableEffects = facts.TryGetProperty("allowedEffects", out var effects)
            ? effects.EnumerateArray().Select(effect => effect.GetString()!).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var elements = new List<ElementSummary>();
        if (facts.TryGetProperty("visibleEntries", out var entries))
            elements.AddRange(entries.EnumerateArray().Select(entry => new ElementSummary("menuItem", entry.GetString(), null,
                true, false, true, ElementEpistemic.Observed)));
        if (facts.TryGetProperty("readOnlyEntries", out var readOnlyEntries))
            elements.AddRange(readOnlyEntries.EnumerateArray().Select(entry => new ElementSummary("switch", entry.GetString(), null,
                false, true, true, ElementEpistemic.Observed, "关闭")));
        if (facts.TryGetProperty("target", out var target))
            elements.Add(new ElementSummary(caseId == "switch-readonly" ? "switch" : "menuItem", target.GetString(), null,
                caseId == "switch-readonly" ? false : true, caseId == "switch-readonly", true,
                ElementEpistemic.Observed, facts.TryGetProperty("observedState", out var state) ? state.GetString() : null));
        if (facts.TryGetProperty("node", out var node))
            elements.Add(new ElementSummary("unknown", node.GetString(), null, false, false, true,
                ElementEpistemic.Observed));
        IReadOnlyDictionary<string, string>? sources = caseId is "dynamic-menu" or "switch-readonly"
            ? new Dictionary<string, string>(StringComparer.Ordinal)
                { ["sim.menu"] = caseId == "switch-readonly" ? "Fixture projection only of the observed read-only switch; the sole condition is readOnly and no Effect is authorized." : "Fixture projection of the current World menu. Root exposes menuItem entries 网络 and 蓝牙 plus read-only switch 飞行模式. Entering 网络 reveals one child that is not listed initially. MaxDepth is 2; traversal return action is button Back, declared as a current action contract and only grounded when currently visible. Candidates are resolved by Kernel against the current World during traversal." }
            : null;
        IReadOnlyDictionary<string, string>? conditions = caseId == "dynamic-menu"
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["navigation"] = "current candidate is an observed traversable menu entry",
                ["readOnly"] = "current candidate is an observed read-only control; inspect without dispatch",
            }
            : null;
        if (caseId == "switch-readonly")
            conditions = new Dictionary<string, string> { ["readOnly"] = "current observed switch; inspect without dispatch" };
        var capabilities = new Dictionary<string, CapabilityProfileReport>(StringComparer.Ordinal);
        if (caseId == "dynamic-menu")
            claims["fixture.maxTraversalDepth"] = new ClaimSummary("2", "DeclaredBudget", false);
        return new AgentDecisionContext("generation-" + caseId, runId, "1", task,
            availableEffects, claims,
            Array.Empty<AgentObligationView>(), AgentDecisionPhase.InitialPlanning,
            Screen: new ScreenSummary("fixture-root", "RUNTIME-PLAN-004:" + caseId),
            Elements: elements,
            BudgetRemaining: new ConsultationBudget(5, 16),
            AvailableCapabilities: capabilities,
            ProductSessionId: sessionId,
            AvailablePlanSources: sources,
            AvailablePlanConditions: conditions);
    }


    private sealed class RecordingMessageHandler : HttpMessageHandler
    {
        private readonly HttpClientHandler _inner = new();
        public List<string> Lines { get; } = new();
        public string? RequestId { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Lines.Add(JsonSerializer.Serialize(new { direction = "request", uri = request.RequestUri?.ToString(), body = requestBody }));
            if (request.RequestUri?.AbsolutePath == "/api/uniclaw-agent/consult")
            {
                using var json = JsonDocument.Parse(requestBody);
                RequestId = json.RootElement.GetProperty("requestId").GetString();
            }
            using var client = new HttpMessageInvoker(_inner, disposeHandler: false);
            using var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            { Content = new StringContent(requestBody, Encoding.UTF8, "application/json") };
            foreach (var header in request.Headers) clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            var response = await client.SendAsync(clone, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            Lines.Add(JsonSerializer.Serialize(new { direction = "response", status = (int)response.StatusCode, body }));
            response.Content = new StringContent(body, Encoding.UTF8, response.Content.Headers.ContentType?.MediaType ?? "application/json");
            return response;
        }
        protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
    }
}
