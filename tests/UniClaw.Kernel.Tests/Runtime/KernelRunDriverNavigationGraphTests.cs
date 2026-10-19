using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.Runtime;

public sealed class KernelRunDriverNavigationGraphTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 18, 10, 0, 0, TimeSpan.Zero);

    private sealed class AlwaysFresh : IFreshnessEvaluator
    {
        public FreshnessJudgment Evaluate(FreshnessEvaluationInput input) =>
            new(FreshnessSufficiency.Sufficient, "test:fresh");
    }

    private sealed class OkDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.DeliveryCompleted, "test:delivered", T0);
    }

    private sealed class PageOccurrenceStrategy : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous)
        {
            string? owner = previous?.WorldState.TryGetValue(WorldModel.CurrentContainerSubject, out var claim) == true
                ? claim.Value
                : null;

            if (record.Claim.Subject == ProductAssociationStrategy.ScreenIdentitySubject)
            {
                owner = "ctr-" + record.EvidenceId[3..15];
                return ItemsForPage(record.Claim.Value, owner);
            }

            if (record.Claim.Subject == "ui.page.items")
                return ParseItems(record.Claim.Value, owner);

            return previous?.Occurrences?.Select(occurrence => new ProposedOccurrence(
                owner, occurrence.Role, occurrence.SemanticDescriptor, occurrence.State,
                occurrence.Locator, occurrence.Native, occurrence.Space)).ToArray()
                ?? Array.Empty<ProposedOccurrence>();
        }

        private static IReadOnlyList<ProposedOccurrence> ItemsForPage(string page, string owner) =>
            page switch
            {
                "home" => ParseItems("entry:settings", owner),
                "settings" => ParseItems("back:home", owner),
                _ => Array.Empty<ProposedOccurrence>(),
            };

        private static IReadOnlyList<ProposedOccurrence> ParseItems(string value, string? owner) =>
            value.Split('+', StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => segment.Split(':', 2))
                .Select(parts => new ProposedOccurrence(
                    owner,
                    parts[0],
                    parts.Length == 2 ? parts[1] : null,
                    Native: parts.Length == 2
                        ? new NativeLocator("test.selector", $"{parts[0]}:{parts[1]}")
                        : null))
                .ToArray();
    }

    private static ExecutionContract Contract() => new(
        Version: "navigation-v1",
        Objective: "visit-settings-and-return",
        Scope: new HashSet<string> { "ui.screen", "ui.screen.route", "ui.page.items" },
        AllowedEffects: new HashSet<string> { "tap" },
        ForbiddenEffects: new HashSet<string>(),
        ProofCriteria: new[] { "observed-navigation" },
        Obligations: new[] { new RunObligation("visit-settings", RunObligationKind.Objective,
            "ui.screen", "settings", Mandatory: true) },
        TaskInstanceId: "task-navigation-graph",
        ProductSessionId: "session-navigation-graph",
        ContractRef: "contract-navigation-graph");

    private static ObservationProposal Claim(
        string subject, string value, int cycle, ObservationContext context) => new(
        new ObservationClaim(subject, value),
        IngressKind.Observation,
        context,
        new Provenance("settings-feed", T0.AddMinutes(cycle),
            $"scope:{subject}:{cycle}", new[] { $"capture:{cycle}:{subject}" }));

    private static RunDriverInput.Observation ObservePage(
        string page, int cycle, ObservationContext context, string? items = null) =>
        new(new[]
        {
            Claim("ui.screen", page, cycle, context),
            Claim("ui.screen.route", page, cycle, context),
            Claim("ui.page.items", items ?? (page == "home" ? "entry:settings" : "back:home"), cycle, context),
        });

    private static string CurrentContainer(WorldBeliefRevision revision) =>
        revision.WorldState[WorldModel.CurrentContainerSubject].Value;

    [Fact]
    public void VerifiedEntryAndReturn_AddGraphEdges_WhileGroundingUsesOnlyCurrentPage()
    {
        UniKernel? kernel = null;
        var postCycle = 1;
        var consult = 0;
        AgentDecisionContext? childContext = null;
        var plan = new AgentPlanPolicy();
        var effects = new EffectBoundary(new OkDriver());
        var inputs = new RunDriverInputs
        {
            NextInput = directive => directive.Context == ObservationContext.External
                ? ObservePage("home", 1, ObservationContext.External)
                : ObservePage(
                    effects.ReceiptLog.Count == 1 ? "settings" : "home",
                    ++postCycle,
                    ObservationContext.PostActionEffectFlow),
            ConsultAgent = context =>
            {
                consult++;
                if (consult == 2)
                {
                    var belief = kernel!.CurrentBelief!;
                    var anchor = CurrentContainer(belief);
                    Assert.Equal(2, belief.Containers!.Count);
                    childContext = context;
                    Assert.All(belief.Occurrences!, occurrence => Assert.Equal(anchor, occurrence.OwningContainerId));
                    Assert.DoesNotContain(belief.Occurrences!, occurrence =>
                        occurrence.Role == "entry" && occurrence.SemanticDescriptor == "settings");
                    var projection = kernel.ProjectWorldGraph($"{kernel.RunId}:{kernel.RunView!.Version}")!;
                    Assert.Equal(anchor, projection.CurrentContainerId);
                    Assert.Contains(projection.Nodes, node => node.Identity.ContainerId != anchor);
                    return new AgentDecision.Act(new AgentActionProposal(
                        context.DecisionId,
                        new[] { new AgentActionStep("back", "home", "tap", null) },
                        "return-to-home"));
                }

                if (consult == 1)
                    return new AgentDecision.Act(new AgentActionProposal(
                        context.DecisionId,
                        new[] { new AgentActionStep("entry", "settings", "tap", null) },
                        "open-settings"));

                return new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId, "graph-checked"));
            },
        };
        kernel = new UniKernel(
            new EvidenceLedger(),
            new WorldModel(new HashSet<string> { "ui.screen", "ui.screen.route", "ui.page.items" },
                new ProductAssociationStrategy(), new PageOccurrenceStrategy()),
            DisabledRunTrace.Instance,
            new RunModel(),
            new ControlLoop(plan),
            new RuntimeAssurance(new AlwaysFresh()),
            effects);
        Assert.True(kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(kernel, plan, inputs);
        Assert.True(driver.Activate().Accepted);

        var result = driver.Drive();

        Assert.NotNull(childContext);
        Assert.Equal(childContext.CurrentWorldClaims[WorldModel.CurrentContainerSubject].Value,
            childContext.Screen!.ContainerId);
        Assert.Contains(childContext.Elements!, element => element.Role == "back" && element.Text == "home");
        Assert.DoesNotContain(childContext.Elements!, element => element.Role == "entry");
        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        Assert.Equal(2, effects.ReceiptLog.Count);
        var projectionAfterReturn = kernel.ProjectWorldGraph($"{kernel.RunId}:{kernel.RunView!.Version}")!;
        Assert.Equal(2, projectionAfterReturn.Nodes.Count);
        Assert.Equal(2, projectionAfterReturn.Edges.Count);
        Assert.Equal(CurrentContainer(kernel.CurrentBelief!), projectionAfterReturn.CurrentContainerId);
        Assert.Contains(projectionAfterReturn.Edges, edge => edge.SourceContainerId != edge.TargetContainerId);
        Assert.All(projectionAfterReturn.Edges, edge =>
        {
            Assert.NotEmpty(edge.EvidenceBasis);
            Assert.NotNull(edge.VerificationSourceRef);
            Assert.NotNull(edge.SourceRevisionId);
            Assert.NotNull(edge.TargetRevisionId);
        });
    }

    [Fact]
    public void UnverifiedNavigationAttempt_DoesNotCreateSuccessEdge()
    {
        var consult = 0;
        var postCycle = 1;
        var plan = new AgentPlanPolicy();
        var effects = new EffectBoundary(new OkDriver());
        var inputs = new RunDriverInputs
        {
            NextInput = directive => directive.Context == ObservationContext.External
                ? ObservePage("home", 1, ObservationContext.External)
                // The attempted tap leaves both page identity and route unchanged.
                : ObservePage("home", ++postCycle,
                    ObservationContext.PostActionEffectFlow, "other:status"),
            ConsultAgent = context =>
            {
                consult++;
                return consult == 1
                    ? new AgentDecision.Act(new AgentActionProposal(
                        context.DecisionId,
                        new[] { new AgentActionStep("entry", "settings", "tap", null) },
                        "attempt-but-no-transition"))
                    : new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId, "stop-after-failure"));
            },
        };
        var kernel = new UniKernel(
            new EvidenceLedger(),
            new WorldModel(new HashSet<string> { "ui.screen", "ui.screen.route", "ui.page.items" },
                new ProductAssociationStrategy(), new PageOccurrenceStrategy()),
            DisabledRunTrace.Instance,
            new RunModel(),
            new ControlLoop(plan),
            new RuntimeAssurance(new AlwaysFresh()),
            effects);
        Assert.True(kernel.AdmitContract(Contract()).Accepted);
        var driver = new KernelRunDriver(kernel, plan, inputs);
        Assert.True(driver.Activate().Accepted);

        var result = driver.Drive();

        Assert.Equal(RunDriveStatus.TerminalNotProven, result.Status);
        var projection = kernel.ProjectWorldGraph($"{kernel.RunId}:{kernel.RunView!.Version}")!;
        Assert.Empty(projection.Edges);
        Assert.Single(projection.Nodes);
        Assert.Contains("no-navigation-edge-observed-in-scope", projection.Unknown);
    }
}
