using System.Text.Json;
using UniClaw.Agent.Dsh;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Runtime;
using UniClaw.Simulation.Tests;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Host.Dsh.Tests;

/// <summary>Consumes actual saved DSH decisions through the official codec and existing Kernel simulation seam.</summary>
public sealed class PlanGenerationKernelConsumerTests(ITestOutputHelper output)
{
    [Fact]
    public void GeneratedDynamicTraversal_OfficialCodecThenKernelSimulation()
    {
        if (!ConsumerRequested()) return;
        var path = GeneratedDecisionPath("dynamic-menu");
        if (!ReadyToConsume(path)) return;

        var decision = Decode(path);
        var plan = Assert.IsType<AgentDecision.Plan>(decision);
        var traversal = Assert.Single(plan.Proposal.Items.OfType<PlanItem.TraverseItem>());
        Assert.Equal("sim.menu", traversal.Source);
        Assert.InRange(traversal.MaxDepth, 1, 2);

        var scenario = BuildTraversalScenario(plan);
        var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);

        Assert.False(execution.Report.RunDriveStatus == "AgentDecisionFailed",
            ScenarioReport.DescribeAcceptance(scenario.Bundle, execution.Report));
        Assert.Equal(new[] { "网络", "未知子入口", "Back", "Back", "蓝牙", "Back" },
            scenario.Effects.Effects.Select(effect => effect.Descriptor));
        Assert.Equal(6, execution.Host.Facts.EffectReceipts.Count);
        Assert.Equal("root", execution.Host.Facts.CurrentWorldClaims.GetValueOrDefault("ui.screen"));
    }

    [Fact]
    public void GeneratedSimpleAct_OfficialCodecThenKernelSimulation()
    {
        if (!ConsumerRequested()) return;
        var path = GeneratedDecisionPath("simple-act");
        if (!ReadyToConsume(path)) return;

        var decision = Decode(path);
        var actions = ExtractActions(decision).ToArray();
        Assert.Single(actions);
        Assert.Equal("网络", actions[0].TargetDescriptor);
        Assert.Equal("tap", actions[0].EffectClass);
        var scenario = BuildActionScenario(decision);
        var driver = new RecordingEffectDriver();
        var options = scenario.Options with { Seams = scenario.Options.Seams! with { Driver = driver } };
        var execution = ScenarioRunner.Run(scenario.Bundle, options);

        Assert.Single(driver.Requests);
        Assert.Single(execution.Host.Facts.EffectReceipts);
        Assert.Equal("network-page", execution.Host.Facts.CurrentWorldClaims.GetValueOrDefault("ui.screen"));
    }

    [Fact]
    public void RegisteredGeneratedBatch_All21FinalDecisionsPassSemanticAndKernelConsumerChecks()
    {
        if (!ConsumerRequested()) return;

        var root = FindRoot();
        AssertFrozenBatchRegistration(root);
        using var corpus = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "evidence/runtime-plan-004/generation/cases.json")));
        var cases = corpus.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        var results = new List<object>();
        foreach (var item in cases)
        {
            var caseId = item.GetProperty("id").GetString()!;
            for (var repeat = 1; repeat <= 3; repeat++)
            {
                var directory = Path.Combine(RegisteredEpochRoot(root), caseId, repeat.ToString("D2"));
                var decisionPath = Path.Combine(directory, "final-decision.json");
                Assert.True(File.Exists(decisionPath), $"registered batch is incomplete: {decisionPath}");
                Assert.True(File.Exists(Path.Combine(directory, "artifact-identities.json")), $"missing registered artifact identities: {directory}");
                var decision = Decode(decisionPath);
                Assert.Empty(PlanGenerationSemanticAssertions.Assess(caseId, decision));
                var effects = ConsumeFinalDecision(caseId, decision);
                results.Add(new { caseId, repeat, semanticPass = true, kernelPass = true, effects });
            }
        }

        var report = JsonSerializer.Serialize(new
        {
            registration = Path.GetFileName(RegisteredEpochRoot(root)),
            trials = results.Count,
            pass = results.Count == 21,
            results,
            cost = "unknown",
        }, new JsonSerializerOptions { WriteIndented = true });
        var reportPath = Path.Combine(RegisteredEpochRoot(root), "kernel-consumer-assessment.json");
        using (var stream = new FileStream(reportPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream)) writer.Write(report);
        Assert.Equal(21, results.Count);
    }

    [Fact]
    public void LocalLiteralTracers_OfficialCodecThenKernelSimulation()
    {
        // Deterministic evaluator tracers exercise the exact official wire codec and the
        // same Kernel/World/effect fixture used by saved model decisions.
        AgentDecision dynamic = new AgentDecision.Plan("literal", new AgentPlanProposal(
            [new PlanItem.TraverseItem(TraversalScenarioFixture.Source,
                [
                    new TraversalRule("read", "readOnly", new TraversalOperation("read", TraversalOperationKind.ReadOnly,
                        null, [], [])),
                    TraversalScenarioFixture.NavigationRules(TraversalScenarioFixture.NavigationOperation("navigate"))[0],
                ], 2, new AgentActionStep("button", "Back", "tap", null))],
            "Inspect current entries, discover nested entries, then return.", "literal-plan", MaxLocalOperations: 128));
        dynamic = CodecRoundTrip(dynamic);
        var dynamicScenario = BuildTraversalScenario(Assert.IsType<AgentDecision.Plan>(dynamic));
        var dynamicRun = ScenarioRunner.Run(dynamicScenario.Bundle, dynamicScenario.Options);
        var traversalView = Assert.IsType<TraversalExecutionView>(dynamicRun.Host.KernelCore.TraversalExecution);
        output.WriteLine($"status={dynamicRun.Report.RunDriveStatus}; effects={string.Join(",", dynamicScenario.Effects.Effects.Select(effect => $"{effect.Page}:{effect.Descriptor}"))}; screen={dynamicRun.Host.Facts.CurrentWorldClaims.GetValueOrDefault("ui.screen")}; traversal={traversalView.Status}/depth={traversalView.Depth}/gaps={traversalView.Gaps.Count}/operations={traversalView.OperationsCompleted}");
        Assert.False(dynamicRun.Report.RunDriveStatus == "AgentDecisionFailed",
            ScenarioReport.DescribeAcceptance(dynamicScenario.Bundle, dynamicRun.Report));
        Assert.Equal(new[] { "网络", "未知子入口", "Back", "Back", "蓝牙", "Back" },
            dynamicScenario.Effects.Effects.Select(effect => effect.Descriptor));
        Assert.Equal("root", dynamicRun.Host.Facts.CurrentWorldClaims.GetValueOrDefault("ui.screen"));

        AgentDecision act = new AgentDecision.Act(new AgentActionProposal("literal-act",
            [new AgentActionStep("menuItem", "网络", "tap", null)], "Open the visible network entry."));
        act = CodecRoundTrip(act);
        var actScenario = BuildActionScenario(act);
        var driver = new RecordingEffectDriver();
        var actOptions = actScenario.Options with
        {
            Seams = actScenario.Options.Seams! with { Driver = driver },
        };
        var actRun = ScenarioRunner.Run(actScenario.Bundle, actOptions);
        Assert.Equal("network-page", actRun.Host.Facts.CurrentWorldClaims.GetValueOrDefault("ui.screen"));
        Assert.Single(actRun.Host.Facts.EffectReceipts);
        Assert.Equal("tap", Assert.Single(driver.Requests).EffectClass);
    }

    [Fact]
    public void LocalSafetyTracers_OfficialCodecThenKernelSimulationHaveZeroEffects()
    {
        AgentDecision switchRead = CodecRoundTrip(new AgentDecision.Plan("literal-switch",
            new AgentPlanProposal(
                [new PlanItem.TraverseItem(TraversalScenarioFixture.Source,
                    [new TraversalRule("read-switch", "readOnly",
                        TraversalScenarioFixture.ReadOnlyOperation("read-switch"))],
                    2, new AgentActionStep("button", "Back", "tap", null))],
                "Observe Airplane mode; the current observed value is 关闭.", "literal-switch-plan", MaxLocalOperations: 32)));
        var readScenario = BuildTraversalScenario(Assert.IsType<AgentDecision.Plan>(switchRead));
        var readRun = ScenarioRunner.Run(readScenario.Bundle, readScenario.Options);
        Assert.Empty(readRun.Host.Facts.EffectReceipts);
        Assert.Empty(readScenario.Effects.Effects);
        var observed = Assert.Single(Assert.IsType<TraversalExecutionView>(readRun.Host.KernelCore.TraversalExecution)
            .Operations.Where(operation => operation.TemplateId == "read-switch"));
        Assert.Equal("关闭", observed.ObservedState);

        var decisions = new AgentDecision[]
        {
            CodecRoundTrip(new AgentDecision.NoAction(new AgentNoActionProposal("literal-satisfied", "Wi-Fi is already connected."))),
            CodecRoundTrip(new AgentDecision.Blocked("literal-unsafe", "Unknown target is unauthorized; refusing to act.")),
            CodecRoundTrip(new AgentDecision.Blocked("literal-missing-capability", "Cannot complete: required capability is unavailable.")),
        };
        foreach (var decision in decisions)
        {
            var scenario = BuildNoEffectScenario(decision);
            var execution = ScenarioRunner.Run(scenario.Bundle, scenario.Options);
            Assert.Empty(execution.Host.Facts.EffectReceipts);
            Assert.Empty(scenario.Effects.Effects);
        }
    }

    private static AgentDecision Decode(string path)
    {
        var decision = JsonSerializer.Deserialize<AgentDecision>(File.ReadAllText(path), ProductProtocolJson.CreateOptions());
        return decision ?? throw new JsonException("formal ProductProtocolJson returned null");
    }

    private bool ReadyToConsume(string path)
    {
        if (File.Exists(path)) return true;
        var requested = Environment.GetEnvironmentVariable("UNICLAW_PLAN004_CONSUME") == "1";
        if (requested) Assert.Fail("consumer flag is set but required generated decision is missing: " + path);
        output.WriteLine("CONSUMER_SKIPPED_NO_REAL_ARTIFACT: " + path);
        return false;
    }

    private bool ConsumerRequested()
    {
        if (Environment.GetEnvironmentVariable("UNICLAW_PLAN004_CONSUME") == "1") return true;
        output.WriteLine("CONSUMER_SKIPPED_NOT_ENABLED: 真实模型链未运行，不能记为验收通过。设置 UNICLAW_PLAN004_CONSUME=1 才会 fail-closed 消费已冻结的21个final-decision。");
        return false;
    }

    private static void AssertFrozenBatchRegistration(string root)
    {
        using var prereg = ReadRegistration(root);
        var registration = prereg.RootElement;
        Assert.Equal("frozen-ready", registration.GetProperty("status").GetString());
        Assert.Equal(21, registration.GetProperty("trialCount").GetInt32());
        Assert.False(registration.GetProperty("modelCallsAuthorizedByThisFile").GetBoolean());
        _ = RegisteredEpochRoot(root); // validates the registered epoch directory before consuming artifacts

        var sourceHashes = registration.GetProperty("sourceHashes");
        Assert.Equal(JsonValueKind.Object, sourceHashes.ValueKind);
        Assert.NotEmpty(sourceHashes.EnumerateObject());
        foreach (var entry in sourceHashes.EnumerateObject())
        {
            Assert.False(Path.IsPathRooted(entry.Name), $"registered source path must be repo-relative: {entry.Name}");
            var sourcePath = Path.GetFullPath(Path.Combine(root, entry.Name.Replace('/', Path.DirectorySeparatorChar)));
            var rootPath = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
            Assert.StartsWith(rootPath, sourcePath, StringComparison.Ordinal);
            Assert.True(File.Exists(sourcePath), $"registered source is missing: {entry.Name}");
            Assert.Matches("^[0-9a-f]{64}$", entry.Value.GetString() ?? string.Empty);
            Assert.Equal(entry.Value.GetString(), Hash(sourcePath));
        }
    }

    private static JsonDocument ReadRegistration(string root) => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(root, "evidence/runtime-plan-004/generation/preregistration.json")));

    private static string RegisteredEpochRoot(string root)
    {
        using var registration = ReadRegistration(root);
        var directory = registration.RootElement.GetProperty("epochDirectory").GetString();
        Assert.False(string.IsNullOrWhiteSpace(directory), "frozen preregistration must declare epochDirectory");
        Assert.Equal(Path.GetFileName(directory), directory);
        Assert.StartsWith("epoch-", directory, StringComparison.Ordinal);
        var rootPath = Path.GetFullPath(Path.Combine(root, "evidence/runtime-plan-004/generation"));
        var epochPath = Path.GetFullPath(Path.Combine(rootPath, directory!));
        Assert.StartsWith(rootPath + Path.DirectorySeparatorChar, epochPath, StringComparison.Ordinal);
        return epochPath;
    }

    private static object ConsumeFinalDecision(string caseId, AgentDecision decision)
    {
        if (caseId == "dynamic-menu")
        {
            var traversal = Assert.IsType<AgentDecision.Plan>(decision);
            var scenario = BuildTraversalScenario(traversal);
            var run = ScenarioRunner.Run(scenario.Bundle, scenario.Options);
            Assert.Equal(new[] { "网络", "未知子入口", "Back", "Back", "蓝牙", "Back" },
                scenario.Effects.Effects.Select(effect => effect.Descriptor));
            Assert.DoesNotContain(scenario.Effects.Effects, effect => effect.Descriptor == "飞行模式");
            Assert.Equal("root", run.Host.Facts.CurrentWorldClaims.GetValueOrDefault("ui.screen"));
            var view = Assert.IsType<TraversalExecutionView>(run.Host.KernelCore.TraversalExecution);
            Assert.Equal(PlanExecutionStatus.Completed, view.Status);
            Assert.Equal(0, view.Depth);
            Assert.Empty(view.Gaps);
            Assert.Equal(6, run.Host.Facts.EffectReceipts.Count);
            return new { receipts = run.Host.Facts.EffectReceipts.Count, switchEffects = 0, finalScreen = "root", traversal = "completed" };
        }

        if (caseId is "simple-act" or "rejection-repair")
        {
            var actions = ExtractActions(decision).ToArray();
            Assert.Single(actions);
            Assert.Equal("menuItem", actions[0].TargetRole);
            Assert.Equal("网络", actions[0].TargetDescriptor);
            Assert.Equal("tap", actions[0].EffectClass);
            var scenario = BuildActionScenario(decision);
            var driver = new RecordingEffectDriver();
            var options = scenario.Options with { Seams = scenario.Options.Seams! with { Driver = driver } };
            var run = ScenarioRunner.Run(scenario.Bundle, options);
            Assert.Single(driver.Requests);
            Assert.Single(run.Host.Facts.EffectReceipts);
            Assert.Equal("network-page", run.Host.Facts.CurrentWorldClaims.GetValueOrDefault("ui.screen"));
            return new { receipts = run.Host.Facts.EffectReceipts.Count, dispatched = driver.Requests.Count, finalScreen = "network-page" };
        }

        if (caseId == "switch-readonly" && decision is AgentDecision.Plan readPlan
            && readPlan.Proposal.Items.OfType<PlanItem.TraverseItem>().Any())
        {
            var scenario = BuildTraversalScenario(readPlan);
            var run = ScenarioRunner.Run(scenario.Bundle, scenario.Options);
            Assert.Empty(run.Host.Facts.EffectReceipts);
            var observation = Assert.Single(Assert.IsType<TraversalExecutionView>(run.Host.KernelCore.TraversalExecution)
                .Operations.Where(operation => operation.ObservedState == "关闭"));
            Assert.Equal("关闭", observation.ObservedState);
            return new { receipts = run.Host.Facts.EffectReceipts.Count, observedState = observation.ObservedState };
        }

        var noEffectScenario = BuildNoEffectScenario(decision);
        var noEffectRun = ScenarioRunner.Run(noEffectScenario.Bundle, noEffectScenario.Options);
        Assert.Empty(noEffectRun.Host.Facts.EffectReceipts);
        Assert.Empty(noEffectScenario.Effects.Effects);
        if (caseId == "switch-readonly")
        {
            var switchOccurrence = Assert.Single(noEffectRun.Host.KernelCore.CurrentBelief!.Occurrences!
                .Where(occurrence => occurrence.SemanticDescriptor == "飞行模式"));
            Assert.Equal("关闭", switchOccurrence.State);
            var reportedState = decision switch
            {
                AgentDecision.NoAction noAction => noAction.Proposal.Justification,
                AgentDecision.Plan plan => plan.Proposal.Justification,
                _ => null,
            };
            Assert.Contains("关闭", reportedState ?? string.Empty, StringComparison.Ordinal);
        }
        return new { receipts = noEffectRun.Host.Facts.EffectReceipts.Count, outcome = decision.GetType().Name };
    }

    private static string Hash(string path) => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static AgentDecision CodecRoundTrip(AgentDecision literal)
    {
        var codec = ProductProtocolJson.CreateOptions();
        var wire = JsonSerializer.Serialize<AgentDecision>(literal, codec);
        var decision = JsonSerializer.Deserialize<AgentDecision>(wire, codec);
        return decision ?? throw new JsonException("formal ProductProtocolJson returned null");
    }

    private static string GeneratedDecisionPath(string caseId)
    {
        var root = FindRoot();
        return Path.Combine(RegisteredEpochRoot(root), caseId, "01", "final-decision.json");
    }

    private static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("repository root was not found");
    }

    private static IEnumerable<AgentActionStep> ExtractActions(AgentDecision decision) => decision switch
    {
        AgentDecision.Act act => act.Proposal.Steps,
        AgentDecision.Plan plan => plan.Proposal.Items.OfType<PlanItem.ActItem>()
            .Select(item => new AgentActionStep(item.TargetRole, item.TargetDescriptor, item.EffectClass, item.DesiredState)),
        _ => Array.Empty<AgentActionStep>(),
    };

    private static TraversalScenarioFixture.Scenario BuildTraversalScenario(AgentDecision.Plan generated)
    {
        var generatedTraverse = Assert.Single(generated.Proposal.Items.OfType<PlanItem.TraverseItem>());
        var issued = false;
        AgentDecision? Agent(AgentDecisionContext context)
        {
            if (!issued && context.Phase == AgentDecisionPhase.InitialPlanning)
            {
                issued = true;
                return generated with { DecisionId = context.DecisionId };
            }
            if (context.PlanExecution?.Status == PlanExecutionStatus.Paused)
                return new AgentDecision.ContinuePlan(context.DecisionId, context.PlanExecution.Position);
            return new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId, "fixture traversal completed"));
        }

        return TraversalScenarioFixture.Create(
            [
                new TraversalScenarioFixture.Page("root", [
                    TraversalScenarioFixture.Link("网络"),
                    TraversalScenarioFixture.Link("蓝牙"),
                    TraversalScenarioFixture.ReadOnlyTarget("switch", "飞行模式", "关闭"),
                ]),
                new TraversalScenarioFixture.Page("network-page", [
                    TraversalScenarioFixture.Link("未知子入口"),
                    TraversalScenarioFixture.ReadOnlyTarget("switch", "飞行模式", "关闭"),
                    TraversalScenarioFixture.Back(),
                ]),
                new TraversalScenarioFixture.Page("nested-child", [TraversalScenarioFixture.Back()]),
                new TraversalScenarioFixture.Page("network-page", [
                    TraversalScenarioFixture.Link("未知子入口"),
                    TraversalScenarioFixture.ReadOnlyTarget("switch", "飞行模式", "关闭"),
                    TraversalScenarioFixture.Back(),
                ]),
                new TraversalScenarioFixture.Page("root", [
                    TraversalScenarioFixture.Link("网络"),
                    TraversalScenarioFixture.Link("蓝牙"),
                    TraversalScenarioFixture.ReadOnlyTarget("switch", "飞行模式", "关闭"),
                ]),
                new TraversalScenarioFixture.Page("bluetooth-page", [TraversalScenarioFixture.Back()]),
                new TraversalScenarioFixture.Page("root", [
                    TraversalScenarioFixture.Link("网络"),
                    TraversalScenarioFixture.Link("蓝牙"),
                    TraversalScenarioFixture.ReadOnlyTarget("switch", "飞行模式", "关闭"),
                ]),
            ], generatedTraverse.Rules, maxDepth: 2,
            maxOperations: generated.Proposal.MaxLocalOperations, returnStep: generatedTraverse.ReturnStep,
            agentOverride: Agent);
    }

    private static TraversalScenarioFixture.Scenario BuildActionScenario(AgentDecision generated)
    {
        var issued = false;
        AgentDecision? Agent(AgentDecisionContext context)
        {
            if (!issued && context.Phase == AgentDecisionPhase.InitialPlanning)
            {
                issued = true;
                return generated switch
                {
                    AgentDecision.Act act => act with
                    {
                        Proposal = act.Proposal with { DecisionId = context.DecisionId },
                    },
                    AgentDecision.Plan plan => plan with { DecisionId = context.DecisionId },
                    _ => new AgentDecision.Blocked(context.DecisionId, "decision was not an action"),
                };
            }
            return new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId, "single action completed"));
        }

        return TraversalScenarioFixture.Create(
            [
                new TraversalScenarioFixture.Page("root", [TraversalScenarioFixture.Link("网络")]),
                new TraversalScenarioFixture.Page("network-page", [TraversalScenarioFixture.Back()]),
            ],
            TraversalScenarioFixture.NavigationRules(TraversalScenarioFixture.NavigationOperation("generation-network")),
            agentOverride: Agent);
    }

    private sealed class RecordingEffectDriver : IEffectDriver
    {
        public List<DispatchRequest> Requests { get; } = new();

        public DispatchResult Deliver(DispatchRequest request)
        {
            Requests.Add(request);
            return new DispatchResult(DispatchOutcome.DeliveryCompleted, "literal-kernel-simulation", DateTimeOffset.UtcNow);
        }
    }

    private static TraversalScenarioFixture.Scenario BuildNoEffectScenario(AgentDecision decision)
    {
        AgentDecision? Agent(AgentDecisionContext context) => context.Phase == AgentDecisionPhase.InitialPlanning
            ? decision switch
            {
                AgentDecision.Blocked blocked => blocked with { DecisionId = context.DecisionId },
                AgentDecision.NoAction noAction => noAction with
                {
                    Proposal = noAction.Proposal with { DecisionId = context.DecisionId },
                },
                AgentDecision.Plan plan => plan with { DecisionId = context.DecisionId },
                AgentDecision.Skip skip => skip with { DecisionId = context.DecisionId },
                _ => new AgentDecision.Blocked(context.DecisionId, "unexpected literal safety decision"),
            }
            : new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId, "safety tracer terminal"));

        var entries = new List<TraversalScenarioFixture.MenuEntry>
        {
            TraversalScenarioFixture.StateTarget("switch", "飞行模式", "关闭"),
            TraversalScenarioFixture.StateTarget("setting", "Wi-Fi", "已连接"),
        };
        if (decision is AgentDecision.Blocked blocked
            && blocked.Reason.Contains("unknown", StringComparison.OrdinalIgnoreCase))
            entries.Add(TraversalScenarioFixture.BlockedLink("未知来源安装", "current target is unauthorized"));

        return TraversalScenarioFixture.Create(
            [new TraversalScenarioFixture.Page("root", entries)],
            TraversalScenarioFixture.NavigationRules(TraversalScenarioFixture.NavigationOperation("unused")),
            agentOverride: Agent);
    }
}
