using System.Diagnostics;
using System.Text.Json;
using UniClaw.Agent.Dsh;
using UniClaw.Host;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Host.Dsh.Tests;

[CollectionDefinition("PlanLiveDevice", DisableParallelization = true)]
public sealed class PlanLiveDeviceCollection;

/// <summary>One explicitly enabled real-model/device comparison run. Every
/// proposal is produced by the formal DSH model channel; no scripted decisions.</summary>
[Collection("PlanLiveDevice")]
public sealed class RuntimePlanComparisonLiveTests(ITestOutputHelper output)
{
    [Fact]
    public async Task SameScopedGoal_RealModelUsesSelectedPlanningMode()
    {
        var mode = Environment.GetEnvironmentVariable("UNICLAW_PLAN_LIVE_MODE");
        if (string.IsNullOrWhiteSpace(mode))
        {
            output.WriteLine("未启用真实模型对照，不作为现场验收证据。");
            return;
        }
        Assert.Contains(mode, new[] { "Step", "ShortPlan", "RulePlan", "ComposedPlan" });
        var task = Environment.GetEnvironmentVariable("UNICLAW_PLAN_LIVE_TASK") ?? "candidates";
        var nestedScope = task == "nested-scope";
        var repeat = Environment.GetEnvironmentVariable("UNICLAW_PLAN_LIVE_REPEAT") ?? "1";
        var root = FindRoot();
        var runRoot = Path.Combine(root, "evidence", "runtime-plan-001", "comparison", task, mode, repeat);
        Directory.CreateDirectory(runRoot);
        var bindings = UniagentDshBindingsYaml.LoadDefault();
        Assert.Equal(3081, bindings.Service.BaseUri.Port);
        var profile = UniClaw.Agent.Profile.UniAgentProfileYaml.LoadDefault();
        var models = DshModelManagement.FromBindings(bindings, profile);
        var selectedModel = DshModelManagement.ResolveDshModel(models, UniClaw.Kernel.Capability.LogicalProfileId.AgentDecision);
        var deviceSerial = Environment.GetEnvironmentVariable("UNICLAW_ANDROID_DEVICE");
        Assert.False(string.IsNullOrWhiteSpace(deviceSerial), "ENVIRONMENT_UNAVAILABLE: a registered device lease serial is required");
        var providerArtifactPath = Environment.GetEnvironmentVariable("UNICLAW_DSH_PROVIDER_ARTIFACT");
        Assert.True(!string.IsNullOrWhiteSpace(providerArtifactPath) && File.Exists(providerArtifactPath),
            "ENVIRONMENT_UNAVAILABLE: the loaded DSH provider artifact is required for cost-comparison conditions");
        var transportEvidencePath = Environment.GetEnvironmentVariable("UNICLAW_PLAN_LIVE_TRANSPORT_EVIDENCE");
        Assert.True(!string.IsNullOrWhiteSpace(transportEvidencePath) && File.Exists(transportEvidencePath),
            "ENVIRONMENT_UNAVAILABLE: loaded service transport evidence is required for comparison conditions");
        Reset(deviceSerial!);
        var original = SettingsCoverageConfig.LoadDefault();
        var targets = task == "known-path"
            ? new[] { "Network & internet" }
            : new[] { "Network & internet", "Connected devices" };
        var config = original with
        {
            Session = original.Session with { TaskTitle = "ADR-0053 scoped comparison" },
            Bounds = original.Bounds with
            {
                MaxSteps = 12, MaxConsultRounds = 12,
                RenewalThresholdSteps = 1, RenewalThresholdRounds = 2,
                GoalDirectedContinuationEnabled = true,
                AuthorizedTotalSteps = 20, AuthorizedTotalConsultRounds = 20,
            },
            Coverage = new CoverageRequirements(true, "all-visible", 0, 0, true, 0),
            TargetPages = targets,
            DynamicRootEntries = new DynamicRootEntriesConfig(),
            DynamicNestedEntries = nestedScope ? new DynamicNestedEntriesConfig(true, 1, 1) : new DynamicNestedEntriesConfig(),
            DeepTraversal = new DeepTraversalConfig(),
            Slow = new SlowTriggerConfig(),
            LanguageInspection = null,
        };
        var scopedObjective = nestedScope
            ? "本次明确范围：进入Network & internet及Connected devices；在每个已访问一级页面，处理本次Owner配置范围内最多1个允许的动态子入口，深度为一级页之后再进入1层；观察控件、逐层返回，最终结束在根页。dynamic-nested-entries/frontier属于必须覆盖项，不能以一级入口已访问、根页当前无候选或子入口不在当前页为由忽略。仅记录开关，不切换；未知或被禁止的入口按原规则处理。"
            : "本次明确范围：进入Network & internet，观察该页并返回根页；不要求进入该页子入口，仅记录开关，不切换。";
        File.WriteAllText(Path.Combine(runRoot, "experiment.json"), JsonSerializer.Serialize(new
        {
            task, mode, repeat, targets, model = selectedModel, device = deviceSerial,
            bounds = config.Bounds, coverage = config.Coverage, actionPolicyDigest = config.ActionPolicy?.Digest,
            harnessAssemblySha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(typeof(RuntimePlanComparisonLiveTests).Assembly.Location))).ToLowerInvariant(),
            schemaHash = ProductProtocolSchema.Current.SchemaHash,
            providerArtifactPath,
            providerArtifactSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(providerArtifactPath!))).ToLowerInvariant(),
            transportEvidencePath,
            transportEvidenceSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(transportEvidencePath!))).ToLowerInvariant(),
            profileRevision = profile.Revision,
            promptHash = File.ReadAllText(Path.Combine(root, "product", "prompt", "uniagent-prod", "prompt-hash.txt")).Trim(),
            kernelAssemblySha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(typeof(KernelRunDriver).Assembly.Location))).ToLowerInvariant(),
            hostAssemblySha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(typeof(SettingsCoverageRunner).Assembly.Location))).ToLowerInvariant(),
            decisionChannelSourceSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(Path.Combine(root, "dsh", "uniclaw-decision-channel", "src", "index.js")))).ToLowerInvariant(),
            goal = scopedObjective,
            slowModelEnabled = false, observationAndVerification = "same live feed, per-action guard and Kernel chain",
        }));
        using var peer = new PeerLifetime(new DshOpenedHttpPeer(bindings.Service, model: selectedModel,
            requestTimeout: TimeSpan.FromSeconds(210)));
        DshAgentAdapter? agent = null;
        var decisions = new List<object>();
        var actualDecisions = new List<AgentDecision>();
        SettingsCoverageRunner.RunResult? result = null;
        var timer = Stopwatch.StartNew();
        Exception? failure = null;
        string? cleanupError = null;
        try
        {
            result = SettingsCoverageRunner.Run(runRoot, new SettingsCoverageRunner.Options(
                deviceSerial!, new LivePerception.LiveAssets(deviceSerial!, "wifi-settings",
                    Path.Combine(root, "platforms", "perception"),
                    Path.Combine(root, ".perception", "venv", "bin", "python"),
                    Path.Combine(root, ".perception", "cache")), config,
                context =>
                {
                    agent ??= new DshAgentAdapter(new DshOpenedDecisionChannel(peer.Peer),
                        "plan-comparison-" + Guid.NewGuid().ToString("N"), context.RunId,
                        turnTimeout: TimeSpan.FromSeconds(200));
                    var style = mode switch
                    {
                        "Step" => "本次采用逐步Agent方式；每次至多一个Act，不提交Plan/Policy。",
                        "ShortPlan" => "本组首次响应必须提交kind=plan；proposal.items只列当前已知有限路径的具体act/observe/control/checkpoint，不含forEach或policy，不以逐步Act代替。进入与返回均由Runtime逐动作观察验证；确有未知则诚实回报，不猜页面。",
                        "ComposedPlan" => "本组首次响应必须提交kind=plan，包含可执行的repeat与if组合，condition仅引用本次公布的availablePlanConditions。使用settings.current-navigation在运行中新发现允许入口，每次forEach最多选1项，下一轮读新页；有Ready入口则进入，否则根据当前是否根页选择返回或让出终局评估。不要预列未来页面名称，不用单层根页循环代替。页面控件只记录观察，不切换任何开关。按既有配置的一层nested-enter范围处理，结束须返回根页；上限可用repeat 16/maxLocalOperations 128，仍须服从实际资源复核。",
                        _ => "本组首次响应必须提交kind=plan且items实际包含一个使用已公开Source的单层forEach，不用线性步骤或逐步Act代替。每次body完整进入候选并返回根页，再选择下一候选；Runtime逐动作观察验证，欠证才额外Observe。确有未知则诚实回报。",
                    };
                    var actual = agent.Consult(context with
                    {
                        Objective = context.Objective + "\n" + scopedObjective + "\n" + style
                            + " 不扩大目标或权限；计划耗尽后检查同源overview。所有指定入口均有验证且已回根页、当前mandatory义务已满足时，提交不含completion字段的NoAction。intent ID不是EffectReceipt ID，不能编造成dispatch锚；只有仍需申请completion折抵时才使用当前可核验的真实锚。",
                    });
                    actualDecisions.Add(actual);
                    decisions.Add(new { context.DecisionId, decision = actual, context.PlanExecution });
                    return actual;
                }, ViewportWidth: 1080, ViewportHeight: 1920,
                DshSessionIdAccessor: () => agent?.DshSessionId, UseCoverageDirector: false));
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            timer.Stop();
            var sessionId = agent?.DshSessionId;
            if (agent is not null)
            {
                try
                {
                    if (sessionId is not null) await agent.RevokeAttachmentAsync();
                }
                catch (Exception error) { cleanupError = "revoke: " + error.Message; }
                try { await agent.DisposeAsync(); }
                catch (Exception error) { cleanupError = (cleanupError ?? "") + " dispose: " + error.Message; }
            }
            double? preflightMilliseconds = null;
            if (result is not null && File.Exists(Path.Combine(result.RunDir, "environment-preflight.json")))
            {
                using var preflight = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.RunDir, "environment-preflight.json")));
                if (preflight.RootElement.TryGetProperty("preflightElapsedMilliseconds", out var duration))
                    preflightMilliseconds = duration.GetDouble();
            }
            File.WriteAllText(Path.Combine(runRoot, "trial.json"), JsonSerializer.Serialize(new
            {
                task, mode, repeat, sessionId, elapsedMilliseconds = timer.ElapsedMilliseconds,
                preflightMilliseconds, formalElapsedMilliseconds = preflightMilliseconds is { } warmup
                    ? timer.Elapsed.TotalMilliseconds - warmup : (double?)null,
                consultations = agent?.SemanticConsultationsStarted ?? 0,
                slowModelCalls = 0, result?.RunDir, driveStatus = result?.Status.ToString(), result?.Reason,
                modeConformant = ConformsToMode(mode, actualDecisions),
                expectedVerifiedSteps = nestedScope ? (int?)null : targets.Length * 2,
                nestedScope,
                verifiedSteps = result?.Steps.Count(step => step.Verified) ?? 0,
                coverage = result?.Report.Status, error = failure?.Message, cleanupError,
            }));
            File.WriteAllText(Path.Combine(runRoot, "model-decisions.json"),
                JsonSerializer.Serialize(decisions, ProductProtocolJson.CreateOptions()));
        }
        Assert.NotNull(result);
        Assert.Null(cleanupError);
        output.WriteLine($"mode={mode}; task={task}; run={result.RunDir}; status={result.Status}; reason={result.Reason}");
        Assert.Equal(RunDriveStatus.Completed, result.Status);
        Assert.Equal("CoverageComplete", result.Report.Status);
        if (nestedScope)
        {
            Assert.True(result.Steps.Count(step => step.Verified) >= targets.Length * 2 + 2);
            Assert.Contains(result.Steps, step => step.Verified && step.RouteBefore != config.RootRoute
                && step.RouteAfter != config.RootRoute && step.RouteAfter != step.RouteBefore
                && step.TargetDescriptor != config.BackDescriptor);
        }
        else Assert.Equal(targets.Length * 2, result.Steps.Count(step => step.Verified));
        Assert.All(result.Steps, step => Assert.DoesNotContain("unattributed", step.DecisionId));
        Assert.True(ConformsToMode(mode, actualDecisions), "the model output did not use the assigned planning form");
    }

    private static bool ConformsToMode(string mode, IReadOnlyList<AgentDecision> decisions)
    {
        var plans = decisions.OfType<AgentDecision.Plan>().ToArray();
        var hasRule = plans.Any(plan => plan.Proposal.Items.Any(item => item is PlanItem.ForEachItem));
        if (decisions.Any(decision => decision is AgentDecision.Policy)) return false;
        return mode switch
        {
            "Step" => plans.Length == 0,
            "ShortPlan" => decisions.FirstOrDefault() is AgentDecision.Plan
                && plans.All(plan => Flatten(plan.Proposal.Items).All(item =>
                    item is not (PlanItem.ForEachItem or PlanItem.IfItem or PlanItem.RepeatItem))),
            "RulePlan" => hasRule,
            "ComposedPlan" => decisions.FirstOrDefault() is AgentDecision.Plan first
                && Flatten(first.Proposal.Items).Any(item => item is PlanItem.IfItem)
                && Flatten(first.Proposal.Items).Any(item => item is PlanItem.RepeatItem),
            _ => false,
        };
    }

    private static IEnumerable<PlanItem> Flatten(IReadOnlyList<PlanItem> items)
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

    private sealed class PeerLifetime(DshOpenedHttpPeer peer) : IDisposable
    {
        public DshOpenedHttpPeer Peer => peer;
        public void Dispose() => peer.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
    private static void Reset(string serial)
    {
        using var reset = Process.Start(new ProcessStartInfo("adb")
        {
            ArgumentList = { "-s", serial, "shell", "am", "start", "-S", "-a", "android.settings.SETTINGS" },
            RedirectStandardOutput = true,
        });
        Assert.NotNull(reset);
        reset.WaitForExit();
        Assert.Equal(0, reset.ExitCode);
        Thread.Sleep(2000);
    }
    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "AGENTS.md"))) return d.FullName;
        throw new InvalidOperationException("repository root unavailable");
    }
}
