using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Agent.Dsh;
using UniClaw.Agent.Profile;
using UniClaw.Host;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Host.Dsh.Tests;

[Collection("PlanLiveDevice")]
public sealed class SettingsDepthTraversalLiveTests(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions EvidenceJson = new() { WriteIndented = true };
    [Fact]
    public async Task FrozenPhase_RealAgent_UsesOnlyAuthorizedNavigation()
    {
        if (Environment.GetEnvironmentVariable("UNICLAW_PLAN006_LIVE") != "1") return;
        var root = FindRoot();
        var registrationPath = Environment.GetEnvironmentVariable("UNICLAW_PLAN006_REGISTRATION");
        Assert.False(string.IsNullOrWhiteSpace(registrationPath));
        using var registration = ReadFrozenRegistration(root, registrationPath!);
        var frozen = registration.RootElement;
        var phase = RequiredString(frozen, "phase");
        var depth = RequiredInt(frozen, "maxDepth");
        Assert.Equal(phase switch { "census" => 0, "depth1" => 1, "depth2" => 2, _ => -1 }, depth);
        var epoch = RequiredString(frozen, "epoch");
        Assert.Equal(epoch, Path.GetFileName(epoch));
        var input = frozen.GetProperty("input");
        var serial = RequiredString(input, "deviceSerial");
        var lease = RequiredString(input, "leaseDirectory");
        Assert.Equal(serial, Environment.GetEnvironmentVariable("UNICLAW_ANDROID_DEVICE"));
        Assert.Equal(lease, Environment.GetEnvironmentVariable("UNICLAW_ANDROID_RUN_DIR"));
        Assert.Contains("SERIAL=" + serial, File.ReadAllLines(Path.Combine(lease, ".state")));
        AssertRegisteredDevice(serial);
        var bindings = UniagentDshBindingsYaml.LoadDefault();
        Assert.Equal(3081, bindings.Service.BaseUri.Port);
        var profile = UniAgentProfileYaml.LoadDefault();
        Assert.Equal(26, profile.Revision);
        Assert.Single(profile.AllowedSkillRefs!, s => s.Name == "android-automotive-ui-testing" && s.Revision == 4);
        using (var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"product/prompt/uniagent-prod/manifest.json"))))
            Assert.Equal(25, manifest.RootElement.GetProperty("promptRevision").GetInt32());
        var model = DshModelManagement.ResolveDshModel(DshModelManagement.FromBindings(bindings,profile), LogicalProfileId.AgentDecision);
        AssertRegisteredModel(frozen,model);
        var stepsBudget = RequiredInt(input,"maxSteps");
        var consultBudget = RequiredInt(input,"maxConsultations");
        var localBudget = RequiredInt(input,"maxLocalOperations");
        foreach (var key in new[] { "maxTotalSteps", "authorizedTotalSteps" }) Assert.Equal(stepsBudget, RequiredInt(input,key));
        foreach (var key in new[] { "maxTotalConsultations", "authorizedTotalConsultations" }) Assert.Equal(consultBudget, RequiredInt(input,key));
        Assert.Equal(1, RequiredInt(frozen,"maximumPreAdoptionRepairsPerConsultation"));
        Assert.Equal(profile.Revision, RequiredInt(frozen,"profileRevision"));
        Assert.Equal(25, RequiredInt(frozen,"promptRevision")); Assert.Equal(4, RequiredInt(frozen,"skillRevision"));
        Assert.InRange(stepsBudget,1,256); Assert.InRange(consultBudget,1,24); Assert.InRange(localBudget,1,1024);
        var targets = input.GetProperty("rootTargets").EnumerateArray().Select(x=>x.GetString()!).ToArray();
        var navigation = input.GetProperty("navigationByRoute").EnumerateObject().ToDictionary(
            p=>p.Name,p=>(IReadOnlyList<string>)p.Value.EnumerateArray().Select(x=>x.GetString()!).ToArray(),StringComparer.Ordinal);
        var original=SettingsCoverageConfig.LoadDefault();
        var config=original with
        {
            Session=original.Session with {TaskTitle="PLAN006 "+phase+" "+epoch},
            Coverage=new CoverageRequirements(true,"all-visible",0,phase=="depth2"?1:0,phase!="census",0),
            TargetPages=targets,DynamicRootEntries=new(),DynamicNestedEntries=new(),DeepTraversal=new(),Slow=new(),LanguageInspection=null,
            NavigationScope=new SettingsNavigationScope(navigation,input.GetProperty("allowRootScroll").GetBoolean()),
            Bounds=original.Bounds with {MaxSteps=stepsBudget,MaxConsultRounds=consultBudget,
                MaxTotalSteps=stepsBudget,MaxTotalConsultRounds=consultBudget,
                AuthorizedTotalSteps=stepsBudget,AuthorizedTotalConsultRounds=consultBudget,
                GoalDirectedContinuationEnabled=false,RenewalThresholdSteps=0,RenewalThresholdRounds=0},
        };
        Assert.Equal(RequiredString(frozen,"objective"),ComposeObjective(config));
        var instruction=RequiredString(frozen,"taskInstruction");
        var phaseRoot=Path.Combine(root,"evidence/runtime-plan-006",phase,epoch);
        Assert.False(Directory.Exists(phaseRoot),"EVIDENCE_EXISTS: register a new epoch");
        Directory.CreateDirectory(phaseRoot); File.Copy(registrationPath!,Path.Combine(phaseRoot,"preregistration.json"));
        WriteJson(Path.Combine(phaseRoot,"actual-config.json"),config);
        WriteJson(Path.Combine(phaseRoot,"artifact-identities.json"),new {profileRevision=26,promptRevision=25,skillRevision=4,
            schemaHash=ProductProtocolSchema.Current.SchemaHash,model,sourceHashes=frozen.GetProperty("sourceHashes"),
            assemblies=new[]{typeof(SettingsDepthTraversalLiveTests).Assembly,typeof(SettingsCoverageRunner).Assembly,typeof(KernelRunDriver).Assembly}
                .ToDictionary(a=>a.GetName().Name!,a=>Hash(a.Location))});
        using var traffic=new RecordingMessageHandler();
        await using var peer=new DshOpenedHttpPeer(bindings.Service,handler:traffic,model:model,requestTimeout:TimeSpan.FromSeconds(210),expectedProfile:profile);
        DshAgentAdapter? agent=null; string? runId=null; var sessionId=Guid.NewGuid().ToString("N");
        var decisions=new List<AgentDecision>(); var snapshots=new List<PlanGenerationTrajectorySnapshot>();
        SettingsCoverageRunner.RunResult? result=null; Exception? failure=null; string? cleanupError=null;
        var timer=Stopwatch.StartNew();
        try
        {
            result=SettingsCoverageRunner.Run(phaseRoot,new SettingsCoverageRunner.Options(serial,
                new LivePerception.LiveAssets(serial,"wifi-settings",Path.Combine(root,"platforms/perception"),
                    Path.Combine(root,".perception/venv/bin/python"),Path.Combine(root,".perception/cache")),config,
                context=>
                {
                    runId??=context.RunId; Assert.Equal(runId,context.RunId);
                    agent??=new DshAgentAdapter(new DshOpenedDecisionChannel(peer),sessionId,context.RunId,turnTimeout:TimeSpan.FromSeconds(200));
                    Assert.Equal(ComposeObjective(config),context.Objective);
                    var sent=context with {Objective=context.Objective+"\n"+instruction};
                    var index=agent.SemanticConsultationsStarted+1;
                    var d=Path.Combine(phaseRoot,"consults",index.ToString("D2"));Directory.CreateDirectory(d);
                    File.WriteAllText(Path.Combine(d,"input-context.json"),JsonSerializer.Serialize(sent,ProductProtocolJson.CreateOptions()));
                    File.WriteAllText(Path.Combine(d,"base-goal.txt"),context.Objective);
                    var lineStart=traffic.Lines.Count;var requestStart=traffic.ConsultRequestIds.Count;
                    try
                    {
                        var decision=agent.Consult(sent);
                        File.WriteAllText(Path.Combine(d,"decision-codec-roundtrip.json"),decision is null?"null":PlanGenerationSemanticAssertions.EncodeRoundTrip(decision));
                        Assert.NotNull(decision);
                        ValidateDecision(decision!,phase,depth,localBudget,config);
                        decisions.Add(decision!);return decision;
                    }
                    catch(Exception e){File.WriteAllText(Path.Combine(d,"consult-error.txt"),e.ToString());throw;}
                    finally
                    {
                        File.WriteAllText(Path.Combine(d,"transport-raw.jsonl"),string.Join(Environment.NewLine,traffic.Lines.Skip(lineStart)));
                        var req=traffic.ConsultRequestIds.Skip(requestStart).LastOrDefault();
                        if(agent.DshSessionId is {Length:>0} sid && req is not null)
                        {
                            var snap=PlanGenerationTrajectoryCollector.CaptureAsync(bindings.Service,sid,req,Path.Combine(d,"trajectory")).GetAwaiter().GetResult();
                            WriteJson(Path.Combine(d,"trajectory-summary.json"),snap with {ConsultationRawJsonl="see trajectory/dsh-consultation.raw.jsonl"});
                            AssertMountedSources(root,snap);
                            Assert.True(snap.NoSubmitNudgeCount+Math.Max(0,snap.SubmitCalls.Count-1)<=1,"pre-adoption repairs exceed registered bound");
                            snapshots.Add(snap);
                        }
                    }
                },ViewportWidth:1080,ViewportHeight:1920,DshSessionIdAccessor:()=>agent?.DshSessionId,UseCoverageDirector:false));
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            timer.Stop();
            if(agent?.DshSessionId is {Length:>0})
                try{await peer.DetachAsync(CancellationToken.None);}catch(Exception e){cleanupError=e.ToString();}
            if(agent is not null)try{await agent.DisposeAsync();}catch(Exception e){cleanupError=(cleanupError??"")+e;}
            File.WriteAllText(Path.Combine(phaseRoot,"transport-raw.jsonl"),string.Join(Environment.NewLine,traffic.Lines));
            WriteJson(Path.Combine(phaseRoot,"trial-summary.json"),new {phase,depth,epoch,productSessionId=sessionId,runId,
                dshSessionId=agent?.DshSessionId,elapsedMilliseconds=timer.ElapsedMilliseconds,
                consultations=agent?.SemanticConsultationsStarted??0,capturedTrajectories=snapshots.Count,
                status=result?.Status.ToString(),result?.Reason,result?.RunDir,verifiedSteps=result?.Steps.Count(s=>s.Verified),
                error=failure?.ToString(),cleanupError});
        }
        Assert.Null(cleanupError);Assert.NotNull(result);Assert.NotNull(agent);
        Assert.True(agent!.SemanticConsultationsStarted<=consultBudget);
        Assert.Equal(agent.SemanticConsultationsStarted,snapshots.Count);
        Assert.NotEmpty(decisions);Assert.DoesNotContain(result!.Steps,s=>s.DesiredState is not null||s.TargetRole=="switch");
        Assert.True(result.Steps.Count<=stepsBudget);
        foreach(var s in result.Steps)
        {
            Assert.True(s.Verified,"Unverified Effect cannot prove traversal: "+s.FailureReason);
            if(s.EffectClass=="swipe-up")
                Assert.True(s.RouteBefore==config.RootRoute&&s.TargetRole=="scrollable"&&s.TargetDescriptor==config.ScrollContainerDescriptor);
            else
            {
                Assert.Equal("tap",s.EffectClass);Assert.Equal("ui.element",s.TargetRole);
                Assert.True(s.TargetDescriptor==config.BackDescriptor || s.RouteBefore is not null
                    && navigation.TryGetValue(s.RouteBefore,out var allowed)&&allowed.Contains(s.TargetDescriptor!,StringComparer.Ordinal));
            }
        }
        var traverses=decisions.OfType<AgentDecision.Plan>().SelectMany(p=>Flatten(p.Proposal.Items)).OfType<PlanItem.TraverseItem>().ToArray();
        if(phase=="census"){Assert.Empty(traverses);Assert.All(result.Steps,s=>Assert.Equal("swipe-up",s.EffectClass));}
        else Assert.NotEmpty(traverses);
        var entered = result.Steps.Where(s=>s.Verified && s.EffectClass=="tap" && s.TargetDescriptor!=config.BackDescriptor)
            .Select(s=>(Route:s.RouteBefore!,Target:s.TargetDescriptor!)).ToHashSet();
        var missingRoot=targets.Where(t=>!entered.Contains((config.RootRoute,t))).ToArray();
        var missingNested=phase=="depth2"?navigation.Where(p=>p.Key!=config.RootRoute)
            .SelectMany(p=>p.Value.Select(t=>(Route:p.Key,Target:t))).Where(x=>!entered.Contains(x)).ToArray():[];
        var actualDepth=0;var maximumActualDepth=0;var depthPathValid=true;
        foreach(var step in result.Steps.Where(s=>s.Verified && s.EffectClass=="tap"))
        {
            actualDepth+=step.TargetDescriptor==config.BackDescriptor?-1:1;
            if(actualDepth<0)depthPathValid=false;
            maximumActualDepth=Math.Max(maximumActualDepth,actualDepth);
        }
        var finalRoute=result.Steps.LastOrDefault()?.RouteAfter;
        var scrolls=result.Steps.Count(s=>s.Verified&&s.EffectClass=="swipe-up");
        var expected=frozen.GetProperty("expected");
        var finalAtRoot=finalRoute==config.RootRoute;
        var taskCoverageMet=phase=="census"?scrolls==RequiredInt(expected,"verifiedRootSwipes")&&entered.Count==0
            :missingRoot.Length==0&&missingNested.Length==0&&maximumActualDepth>=1;
        taskCoverageMet &= finalAtRoot && actualDepth==0 && depthPathValid && maximumActualDepth<=depth;
        WriteJson(Path.Combine(phaseRoot,"coverage-audit.json"),new{taskCoverageMet,finalAtRoot,finalRoute,
            actualDepth,maximumActualDepth,depthPathValid,scrolls,missingRoot,missingNested,
            entered=entered.Select(x=>new{x.Route,x.Target}).ToArray(),discoveryComplete=false});
        Assert.True(taskCoverageMet,"Registered scope is incomplete; see coverage-audit.json (not all Settings discovery)");
        Assert.NotEqual(RunDriveStatus.AgentDecisionFailed,result.Status);
        output.WriteLine($"phase={phase}, depth={depth}, status={result.Status}, verified={result.Steps.Count}, consultations={agent.SemanticConsultationsStarted}; coverage requires independent audit");
    }

    private static void ValidateDecision(AgentDecision d,string phase,int depth,int localBudget,SettingsCoverageConfig config)
    {
        if(d is AgentDecision.Plan p)
        {
            Assert.InRange(p.Proposal.MaxLocalOperations,1,localBudget);
            foreach (var act in Flatten(p.Proposal.Items).OfType<PlanItem.ActItem>())
                Assert.True(act.EffectClass == "swipe-up" || phase != "census" && act.EffectClass == "tap" && act.TargetDescriptor == config.BackDescriptor,
                    "Menu entry must be a Kernel Traverse operation; direct Act cannot bypass maxDepth");
            foreach(var item in Flatten(p.Proposal.Items).OfType<PlanItem.TraverseItem>())
            {
                Assert.NotEqual("census",phase);Assert.Equal(depth,item.MaxDepth);Assert.Equal(SettingsTraversalProjection.Source,item.Source);
                Assert.NotNull(item.ReturnStep);Assert.Equal("tap",item.ReturnStep!.EffectClass);
                Assert.Equal(config.BackDescriptor,item.ReturnStep.TargetDescriptor);
                Assert.All(item.Rules.Where(r=>r.Operation.Kind==TraversalOperationKind.ReadOnly),r=>Assert.Null(r.Operation.Step));
            }
        }
        Assert.False(d is AgentDecision.Policy,"This navigation-only task has no policy authority");
        if(d is AgentDecision.Act a) Assert.All(a.Proposal.Steps,s=>Assert.True(s.EffectClass=="swipe-up" || phase!="census" && s.EffectClass=="tap" && s.TargetDescriptor==config.BackDescriptor,
            "Direct Act may only scroll or safely return; menu entry must use Traverse"));
    }

    private static JsonDocument ReadFrozenRegistration(string root, string path)
    {
        var document = JsonDocument.Parse(File.ReadAllText(path));
        try
        {
            var value = document.RootElement;
            Assert.Equal("frozen-ready", RequiredString(value, "status"));
            var hashes = value.GetProperty("sourceHashes");
            Assert.Equal(JsonValueKind.Object, hashes.ValueKind);
            Assert.NotEmpty(hashes.EnumerateObject());
            foreach (var source in hashes.EnumerateObject())
            {
                Assert.False(Path.IsPathRooted(source.Name), $"sourceHashes path must be repository-relative: {source.Name}");
                var fullPath = Path.GetFullPath(Path.Combine(root, source.Name));
                Assert.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, fullPath, StringComparison.Ordinal);
                Assert.True(File.Exists(fullPath), $"registered source is missing: {source.Name}");
                Assert.Equal(source.Value.GetString(), Hash(fullPath));
            }
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static void AssertRegisteredModel(JsonElement registration, ModelConfiguration selected)
    {
        var model = registration.GetProperty("model");
        Assert.Equal(selected.Provider, model.GetProperty("provider").GetString());
        var nameProperty = model.TryGetProperty("name", out var name) ? name : model.GetProperty("model");
        Assert.Equal(selected.Name, nameProperty.GetString());
    }

    private static void AssertRegisteredDevice(string serial)
    {
        using var process = Process.Start(new ProcessStartInfo("adb")
        {
            ArgumentList = { "devices" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("ENVIRONMENT_UNAVAILABLE: adb could not start.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, "ENVIRONMENT_UNAVAILABLE: adb devices failed: " + stderr);
        Assert.Contains(stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries), line =>
        {
            var columns = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return columns.Length >= 2 && columns[0] == serial && columns[1] == "device";
        });
    }

    private static string ComposeObjective(SettingsCoverageConfig config) =>
        "Traverse Android Settings within the current device, account, permissions and system state. "
        + "Enter every configured first-level target: " + string.Join("; ", config.TargetPages) + ". "
        + $"Required evidence: rootPage={config.Coverage.RootPage}, "
        + $"scrollDiscoveredEntries={config.Coverage.ScrollDiscoveredEntries}, "
        + $"secondLevelPages={config.Coverage.SecondLevelPages}, "
        + $"backNavigation={config.Coverage.BackNavigation}, repeatedEntries={config.Coverage.RepeatedEntries}. "
        + "End on the observed Settings root. Select every action from the current semantic hierarchy. "
        + (config.DynamicRootEntriesSettings.AllVisible
            ? "Discover and inspect the safe dynamic root frontier; keep unobserved branches unknown. " : "")
        + (config.DynamicNestedEntriesSettings.IsActive
            ? "Inspect the configured dynamic nested frontier, keeping depth and scope limits explicit. " : "")
        + "A configured scope result never proves all possible UI states exhaustive.";

    private static void AssertMountedSources(string root, PlanGenerationTrajectorySnapshot snapshot)
    {
        var identity = File.ReadAllText(Path.Combine(root, "product/prompt/uniagent-prod/01-identity.txt")).TrimEnd();
        var guidance = File.ReadAllText(Path.Combine(root, "product/skills/android-automotive-ui-testing/guidance.md")).TrimEnd();
        Assert.NotNull(snapshot.SystemPrompt);
        Assert.Contains(identity, snapshot.SystemPrompt!, StringComparison.Ordinal);
        Assert.Contains(guidance, snapshot.SystemPrompt!, StringComparison.Ordinal);
        var expectedSections = new[]
        {
            "uniagent-prod:product-prompt",
            "uniagent-prod:skill:android-automotive-ui-testing",
        };
        Assert.Equal(expectedSections.Order(StringComparer.Ordinal), snapshot.Sections.Order(StringComparer.Ordinal));
    }

    private static IEnumerable<PlanItem> Flatten(IEnumerable<PlanItem> items)
    {
        foreach (var item in items)
        {
            yield return item;
            var children = item switch
            {
                PlanItem.ForEachItem loop => loop.Body,
                PlanItem.RepeatItem repeat => repeat.Body,
                PlanItem.IfItem branch => branch.Then.Concat(branch.Else).ToArray(),
                _ => Array.Empty<PlanItem>(),
            };
            foreach (var child in Flatten(children)) yield return child;
        }
    }

    private static string SettingsConfigPath(string root) => Path.Combine(root, "product/tasks/settings-coverage.yaml");
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string RequiredString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()!
                : throw new InvalidDataException($"Registration field '{name}' is required.");
    private static int RequiredInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result)
            ? result
            : throw new InvalidDataException($"Registration field '{name}' must be an integer.");
    private static void WriteJson(string path, object value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, EvidenceJson));

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
        throw new DirectoryNotFoundException("repository root unavailable");
    }

    private sealed class RecordingMessageHandler : HttpMessageHandler
    {
        private readonly HttpClientHandler _inner = new();
        public List<string> Lines { get; } = [];
        public List<string> ConsultRequestIds { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Lines.Add(JsonSerializer.Serialize(new
            {
                direction = "request", uri = request.RequestUri?.ToString(), body = requestBody,
            }));
            if (request.RequestUri?.AbsolutePath == "/api/uniclaw-agent/consult")
            {
                using var json = JsonDocument.Parse(requestBody);
                ConsultRequestIds.Add(json.RootElement.GetProperty("requestId").GetString()!);
            }
            using var client = new HttpMessageInvoker(_inner, disposeHandler: false);
            using var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json"),
            };
            foreach (var header in request.Headers) clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            var response = await client.SendAsync(clone, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            Lines.Add(JsonSerializer.Serialize(new
            {
                direction = "response", status = (int)response.StatusCode, body = responseBody,
            }));
            response.Content = new StringContent(responseBody, Encoding.UTF8,
                response.Content.Headers.ContentType?.MediaType ?? "application/json");
            return response;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
