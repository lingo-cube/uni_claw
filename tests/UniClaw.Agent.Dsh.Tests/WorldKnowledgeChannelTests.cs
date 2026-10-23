using System.Net;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UniClaw.Agent.Dsh;
using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Agent.Dsh.Tests;

/// <summary>
/// External-provider double over the real Ledger → Kernel → World → Driver →
/// DSH adapter/channel/HTTP transport path. This is not a model or live-provider
/// regression; the handler records and answers the actual HTTP request bodies.
/// </summary>
public sealed class WorldKnowledgeChannelTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly JsonSerializerOptions _wire = ProductProtocolJson.CreateOptions();
        private int _consults;

        public List<(string Path, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Requests.Add((path, body));
            using var json = JsonDocument.Parse(body);

            if (path.EndsWith("/handshake", StringComparison.Ordinal))
            {
                var root = json.RootElement;
                var capabilities = root.GetProperty("expectedCapabilities").Clone();
                return Json(new
                {
                    accepted = true,
                    protocol = root.GetProperty("protocol").Clone(),
                    reportedCapabilities = capabilities,
                    runtimeCapabilities = capabilities.GetProperty("capabilities").Clone(),
                    artifacts = root.GetProperty("expectedArtifacts").Clone(),
                    dshSessionId = "fixture-dsh-session",
                });
            }

            if (!path.EndsWith("/consult", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            var requestId = json.RootElement.GetProperty("requestId").GetString()!;
            var generation = json.RootElement.GetProperty("generation").GetInt64();
            var context = json.RootElement.GetProperty("context");
            var decisionId = context.GetProperty("decisionId").GetString()!;
            var decision = ++_consults == 1
                ? (AgentDecision)new AgentDecision.Defer(decisionId, new ObserveSpec(null, 1))
                : new AgentDecision.Blocked(decisionId, "controlled-provider-stop");
            var payload = JsonSerializer.SerializeToElement(decision, _wire);
            return Json(new
            {
                requestId,
                generation,
                task = new { initialization = new { }, payload },
                diagnostics = new { source = "controlled-provider-double" },
            });
        }

        private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json"),
        };
    }

    [Fact]
    public async Task DriverWorldKnowledge_IsSerializedOnBothRealConsultRequests_AndEnumCodecIsStrict()
    {
        var time = new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);
        var capture = new PageCaptureObservation("channel-c1", "channel-cycle-c1", "session-gaps", time,
            CoordinateSpace.DeviceViewport(100, 100, captureId: "channel-c1"),
            [new("title", "text", "Settings", new UiBounds(0, 0, 90, 20)),
             new("button", "button", "Wi-Fi", new UiBounds(0, 20, 90, 50))]);
        var captureProposal = new ObservationProposal(new(PageObservationCodec.CaptureSubject, PageObservationCodec.Encode(capture)),
            IngressKind.Observation, ObservationContext.External,
            new("controlled-layout", time, "viewport", ["fixture:c1"]));
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string>
            { PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject }, new ProductAssociationStrategy());
        var plan = new AgentPlanPolicy();
        var effects = new EffectBoundary(new ForbiddenDriver());
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, new RunModel(), new ControlLoop(plan),
            new RuntimeAssurance(new Fresh()), effects);
        kernel.Process(captureProposal);

        Assert.True(kernel.AdmitContract(new("v1", "inspect settings",
            new HashSet<string> { PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject },
            new HashSet<string> { "tap" }, new HashSet<string>(), ["objective"],
            [new("pending", RunObligationKind.MaterialEffect, "test.pending", "done", true)],
            TaskInstanceId: "task-gaps", ProductSessionId: "session-gaps", ContractRef: "contract-gaps")).Accepted);

        var handler = new CapturingHandler();
        var peer = new DshOpenedHttpPeer(new DshServiceEndpoint(new Uri("http://127.0.0.1:3080/")),
            credential: new DshWebCredential("dsh-auth-test", "v1.test.test", DateTimeOffset.UtcNow.AddDays(1)),
            handler: handler);
        var channel = new DshOpenedDecisionChannel(peer);
        await using var adapter = new DshAgentAdapter(channel, "session-gaps", kernel.RunId, TimeSpan.FromSeconds(5));

        var reads = 0;
        var inputs = new RunDriverInputs
        {
            NextInput = _ =>
            {
                if (++reads == 1) return new RunDriverInput.Interruption(new("gap-review", "world-review", 1));
                if (reads > 2) return null;
                var source = world.Current!.CurrentPageObservation!.EvidenceId;
                var identity = new ObservationProposal(new(PageObservationCodec.IdentitySubject,
                    PageObservationCodec.Encode(new PageIdentityObservation(capture.CaptureId, capture.ObservationCycleId,
                        capture.SessionCorrelation, capture.CaptureTime, "settings", "Settings"))),
                    IngressKind.Observation, ObservationContext.External,
                    new("controlled-identity", time, "viewport", ["fixture:identity"]))
                {
                    DerivedFromEvidenceIds = [source],
                    TransitiveEvidenceBasis = new HashSet<string> { source },
                    FusionRule = "controlled-page-identity",
                    FusionVersion = "1",
                };
                return new RunDriverInput.Observation([identity]);
            },
            ConsultAgent = adapter.Consult,
        };
        var driver = new KernelRunDriver(kernel, plan, inputs);
        Assert.True(driver.Activate().Accepted);
        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        var consults = handler.Requests.Where(r => r.Path.EndsWith("/consult", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, consults.Length);
        var first = ConsultContext(consults[0].Body);
        var second = ConsultContext(consults[1].Body);
        var firstKnowledge = first.GetProperty("worldKnowledge");
        var secondKnowledge = second.GetProperty("worldKnowledge");
        Assert.Equal(3, firstKnowledge.GetProperty("gaps").GetArrayLength());
        Assert.Single(secondKnowledge.GetProperty("gaps").EnumerateArray());
        Assert.NotEqual(firstKnowledge.GetProperty("sourceRevisionId").GetString(),
            secondKnowledge.GetProperty("sourceRevisionId").GetString());
        Assert.All(firstKnowledge.GetProperty("gaps").EnumerateArray(), gap =>
            Assert.IsType<string>(gap.GetProperty("kind").GetString()));

        var options = ProductProtocolJson.CreateOptions();
        var firstContext = JsonSerializer.Deserialize<AgentDecisionContext>(first.GetRawText(), options)!;
        Assert.NotNull(firstContext.WorldKnowledge);
        var legacy = JsonNode.Parse(first.GetRawText())!.AsObject();
        legacy.Remove("worldKnowledge");
        Assert.Null(JsonSerializer.Deserialize<AgentDecisionContext>(legacy.ToJsonString(), options)!.WorldKnowledge);

        var numericEnum = JsonNode.Parse(first.GetRawText())!.AsObject();
        numericEnum["worldKnowledge"]!["gaps"]![0]!["kind"] = 0;
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentDecisionContext>(numericEnum.ToJsonString(), options));
        var unknownEnum = JsonNode.Parse(first.GetRawText())!.AsObject();
        unknownEnum["worldKnowledge"]!["gaps"]![0]!["kind"] = "not-a-gap";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentDecisionContext>(unknownEnum.ToJsonString(), options));

        Assert.Empty(effects.ReceiptLog);
    }

    [Fact]
    public async Task RecordedSettingsLayout_FlowsThroughWorldDriverAndDshConsults_AndExportsFormalViewsWhenRequested()
    {
        var repository = FindRepositoryRoot();
        var sourcePath = Path.Combine(repository, "platforms", "perception", "evaluation", "validation",
            "fastscreen-v1", "uia", "7e41f85e06e4.xml");
        var sourceBytes = await File.ReadAllBytesAsync(sourcePath);
        var sourceSha256 = Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant();
        var document = XDocument.Parse(Encoding.UTF8.GetString(sourceBytes));
        var nodes = document.Descendants("node").ToArray();
        Assert.Contains(nodes, node => string.Equals(ObservedText(node), "Settings", StringComparison.Ordinal));
        var items = nodes.Select((node, index) => LayoutItem(node, index, 1080, 2400))
            .Where(item => item is not null).Select(item => item!).ToArray();
        Assert.True(items.Length >= 2);
        Assert.Contains(items, item => item.Text == "Settings");

        // Capture identity/time/session are controlled fixture correlation; node text and bounds come from the recording.
        var time = new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);
        var capture = new PageCaptureObservation("recorded-settings-c1", "recorded-settings-cycle1",
            "controlled-recording-session", time,
            CoordinateSpace.DeviceViewport(1080, 2400, captureId: "recorded-settings-c1"), items)
        { DeviceId = "recorded-source:fastscreen-v1" };
        var captureProposal = new ObservationProposal(new(PageObservationCodec.CaptureSubject,
                PageObservationCodec.Encode(capture)), IngressKind.Observation, ObservationContext.External,
            new("controlled-recording-import", time, "recording:7e41f85e06e4.xml", ["recording-sha256:" + sourceSha256]));
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string>
            { PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject }, new ProductAssociationStrategy());
        var plan = new AgentPlanPolicy();
        var effects = new EffectBoundary(new ForbiddenDriver());
        var kernel = new UniKernel(ledger, world, DisabledRunTrace.Instance, new RunModel(), new ControlLoop(plan),
            new RuntimeAssurance(new Fresh()), effects);
        kernel.Process(captureProposal);

        Assert.True(kernel.AdmitContract(new("recording-v1", "inspect recorded Settings page",
            new HashSet<string> { PageObservationCodec.CaptureSubject, PageObservationCodec.IdentitySubject },
            new HashSet<string> { "tap" }, new HashSet<string>(), ["objective"],
            [new("pending", RunObligationKind.MaterialEffect, "recording.pending", "done", true)],
            TaskInstanceId: "recording-task", ProductSessionId: capture.SessionCorrelation,
            ContractRef: "recording-contract")).Accepted);

        var handler = new CapturingHandler();
        var peer = new DshOpenedHttpPeer(new DshServiceEndpoint(new Uri("http://127.0.0.1:3080/")),
            credential: new DshWebCredential("dsh-auth-test", "v1.test.test", DateTimeOffset.UtcNow.AddDays(1)),
            handler: handler);
        var channel = new DshOpenedDecisionChannel(peer);
        await using var adapter = new DshAgentAdapter(channel, capture.SessionCorrelation, kernel.RunId,
            TimeSpan.FromSeconds(5));

        var reads = 0;
        var inputs = new RunDriverInputs
        {
            NextInput = _ =>
            {
                if (++reads == 1) return new RunDriverInput.Interruption(new("recording-gap-review", "world-review", 1));
                if (reads > 2) return null;
                var source = world.Current!.CurrentPageObservation!.EvidenceId;
                var identity = new ObservationProposal(new(PageObservationCodec.IdentitySubject,
                        PageObservationCodec.Encode(new PageIdentityObservation(capture.CaptureId,
                            capture.ObservationCycleId, capture.SessionCorrelation, capture.CaptureTime,
                            "recorded-settings", "Settings"))),
                    IngressKind.Observation, ObservationContext.External,
                    new("controlled-recording-identity", time, "recording:identity", ["recorded-title:Settings"]))
                {
                    DerivedFromEvidenceIds = [source],
                    TransitiveEvidenceBasis = new HashSet<string> { source },
                    FusionRule = "controlled-recording-page-identity",
                    FusionVersion = "1",
                };
                return new RunDriverInput.Observation([identity]);
            },
            ConsultAgent = adapter.Consult,
        };
        var driver = new KernelRunDriver(kernel, plan, inputs);
        Assert.True(driver.Activate().Accepted);
        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        var consults = handler.Requests.Where(r => r.Path.EndsWith("/consult", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, consults.Length);
        var first = ConsultContext(consults[0].Body).GetProperty("worldKnowledge");
        var second = ConsultContext(consults[1].Body).GetProperty("worldKnowledge");
        Assert.Equal(3, first.GetProperty("gaps").GetArrayLength());
        Assert.Single(second.GetProperty("gaps").EnumerateArray());
        Assert.Empty(effects.ReceiptLog);

        var containerId = world.Current!.CurrentPageObservation!.ContainerId!;
        var page = kernel.ProjectWorldPage(containerId);
        var slice = kernel.DeriveSlice(containerId);
        var frameId = capture.Space.CoordinateSpaceId;
        var presentation = SliceContentPresentation.Project(slice, frameId,
            new SpatialLocator(0, 0, 1, 1, frameId));
        var outputPath = Environment.GetEnvironmentVariable("UNICLAW_WORLD_EXAMPLE_EXPORT");
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            if (!Path.IsPathFullyQualified(outputPath))
                throw new InvalidOperationException("UNICLAW_WORLD_EXAMPLE_EXPORT must be an absolute path");
            var directory = Path.GetDirectoryName(outputPath)!;
            Directory.CreateDirectory(directory);
            var example = new
            {
                schemaVersion = "uniclaw.world-channel-recorded-example.v1",
                evidenceClass = "real-core-controlled-recording-metadata-external-provider-double",
                finalProviderEnvelopeProven = false,
                source = new
                {
                    file = Path.GetRelativePath(repository, sourcePath),
                    sha256 = sourceSha256,
                    titleSettingsPresentInSource = true,
                    layoutItemExtraction = "XDocument text/content-desc + strict legal bounds within 1080x2400",
                    layoutItemCount = items.Length,
                    titleItemIncluded = items.Any(item => item.Text == "Settings"),
                    deviceId = capture.DeviceId,
                    deviceIdMeaning = "recording-source-label; not current device identity",
                    sessionAndCaptureTime = "controlled fixture correlation",
                },
                consultationGapCounts = new[] { first.GetProperty("gaps").GetArrayLength(), second.GetProperty("gaps").GetArrayLength() },
                worldPage = page,
                slice,
                slicePresentation = presentation,
                httpConsultBodies = consults.Select(r => r.Body).ToArray(),
            };
            File.WriteAllText(outputPath, JsonSerializer.Serialize(example,
                new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(directory, "channel-consults.json"),
                JsonSerializer.Serialize(consults.Select(r => r.Body).ToArray()), new UTF8Encoding(false));
        }
    }

    private static JsonElement ConsultContext(string body)
    {
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("context").Clone();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "UniClaw.Kernel.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root was not found from test output directory");
    }

    private static string? ObservedText(XElement node)
    {
        var text = ((string?)node.Attribute("text"))?.Trim();
        if (string.IsNullOrWhiteSpace(text)) text = ((string?)node.Attribute("content-desc"))?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static SemanticLayoutItem? LayoutItem(XElement node, int index, int width, int height)
    {
        var text = ObservedText(node);
        var bounds = ((string?)node.Attribute("bounds"))?.Trim();
        if (text is null || bounds is null) return null;
        var match = Regex.Match(bounds, @"^\[(\d+),(\d+)\]\[(\d+),(\d+)\]$",
            RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        var values = match.Groups.Cast<Group>().Skip(1)
            .Select(group => int.TryParse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                ? (int?)value : null).ToArray();
        if (values.Length != 4 || values.Any(value => value is null)) return null;
        var (x1, y1, x2, y2) = (values[0]!.Value, values[1]!.Value, values[2]!.Value, values[3]!.Value);
        if (x1 < 0 || y1 < 0 || x2 <= x1 || y2 <= y1 || x2 > width || y2 > height) return null;
        var clickable = string.Equals((string?)node.Attribute("clickable"), "true", StringComparison.OrdinalIgnoreCase);
        return new("recording-node-" + index.ToString(CultureInfo.InvariantCulture),
            clickable ? "ui.element" : "text", text, new UiBounds(x1, y1, x2, y2));
    }

    private sealed class Fresh : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) => new(FreshnessSufficiency.Sufficient, "fixture");
    }

    private sealed class ForbiddenDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) => throw new InvalidOperationException("zero Effect expected");
    }
}
