using System.Text.Json;
using UniClaw.Kernel;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// Explicitly simulated traversal inputs: the golden bundle still runs through
/// perception/evidence/world/kernel, while page/menu observations and mechanical
/// effect delivery are deterministic external fixtures.
/// </summary>
internal static class TraversalScenarioFixture
{
    internal const string Source = "sim.menu";
    private static readonly DateTimeOffset Start = new(2026, 10, 19, 2, 0, 0, TimeSpan.Zero);

    internal sealed record MenuEntry(string Role, string Descriptor, bool Traverse, bool ReadOnly = false,
        string? State = null, PlanCandidateDisposition Disposition = PlanCandidateDisposition.Ready,
        string? Reason = null);
    internal sealed record Page(string Id, IReadOnlyList<MenuEntry> Entries, bool Complete = true);
    internal sealed record DeliveredEffect(string Page, string Descriptor, string EffectClass);

    internal sealed class EffectCapture
    {
        private readonly Dictionary<string, (string Page, string Descriptor)> _targets = new(StringComparer.Ordinal);
        private UniKernel? _kernel;
        public List<DeliveredEffect> Effects { get; } = new();
        internal UniKernel? CurrentKernel => _kernel;

        internal TraversalObservation? Project(UniKernel kernel, string source)
        {
            _kernel = kernel;
            if (source != Source || kernel.CurrentBelief is not { } belief) return null;
            var graph = kernel.ProjectWorldGraph($"{kernel.RunId}:{kernel.RunView?.Version}");
            if (graph?.CurrentContainerId is not { } current) return null;

            var menuClaim = belief.WorldState.GetValueOrDefault("menu.ready");
            if (menuClaim is null)
                return new TraversalObservation(graph, Array.Empty<TraversalCandidate>(), false,
                    belief.EvidenceBasis.ToArray());
            var menu = JsonSerializer.Deserialize<MenuDocument>(menuClaim.Value)
                ?? throw new InvalidOperationException("simulation menu claim is malformed");
            var currentOccurrences = belief.Occurrences?.Where(item => item.OwningContainerId == current).ToArray() ?? [];
            var candidates = new List<TraversalCandidate>();
            foreach (var entry in menu.Items.Where(item => item.Traverse || item.ReadOnly))
            {
                var occurrence = currentOccurrences.SingleOrDefault(item => item.Role == entry.Role
                    && item.SemanticDescriptor == entry.Descriptor);
                if (occurrence is null) continue;
                _targets[occurrence.OccurrenceId] = (PageOf(belief), occurrence.SemanticDescriptor!);
                candidates.Add(new TraversalCandidate(
                    new PlanCandidate($"{current}:{entry.Role}:{entry.Descriptor}", entry.Role,
                        entry.Descriptor, entry.Disposition, occurrence.EvidenceBasis, entry.Reason),
                    new Dictionary<string, PolicyTruth>(StringComparer.Ordinal)
                    {
                        ["navigation"] = entry.Traverse ? PolicyTruth.Satisfied : PolicyTruth.Violated,
                        ["readOnly"] = entry.ReadOnly ? PolicyTruth.Satisfied : PolicyTruth.Violated,
                    }));
            }
            foreach (var occurrence in currentOccurrences)
                if (occurrence.SemanticDescriptor is not null)
                    _targets[occurrence.OccurrenceId] = (PageOf(belief), occurrence.SemanticDescriptor);

            var evidence = belief.EvidenceBasis.Contains(menuClaim.EvidenceId)
                ? new[] { menuClaim.EvidenceId }
                : Array.Empty<string>();
            return new TraversalObservation(graph, candidates, menu.Complete, evidence);
        }

        internal IEffectDriver Driver() => new SimulatedEffectDriver(request =>
        {
            var occurrence = request.Target.OccurrenceReference;
            if (!_targets.TryGetValue(occurrence, out var target))
                throw new InvalidOperationException($"effect target was not in the current projected World: {occurrence}");
            Effects.Add(new DeliveredEffect(target.Page, target.Descriptor, request.EffectClass));
        });

        private static string PageOf(WorldBeliefRevision belief) =>
            belief.WorldState.TryGetValue("ui.screen", out var page) ? page.Value : "<unknown-page>";
    }

    private sealed record MenuDocument(bool Complete, IReadOnlyList<MenuEntry> Items);

    private sealed class SimulatedEffectDriver(Action<DispatchRequest> onDispatch) : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request)
        {
            onDispatch(request);
            return new DispatchResult(DispatchOutcome.DeliveryCompleted, "simulated-mechanical-delivery", Start);
        }
    }

    private sealed class MenuObservationStrategy : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous)
        {
            if (record.Claim.Subject != "menu.ready") return Carry(previous);
            var owner = previous?.WorldState.GetValueOrDefault(WorldModel.CurrentContainerSubject)?.Value;
            var menu = JsonSerializer.Deserialize<MenuDocument>(record.Claim.Value)
                ?? throw new InvalidOperationException("simulation menu claim is malformed");
            return menu.Items.Select(item => new ProposedOccurrence(owner, item.Role,
                item.Descriptor, State: item.State ?? (item.Traverse ? "navigation" : item.ReadOnly ? "unchecked" : "return"))).ToArray();
        }

        private static IReadOnlyList<ProposedOccurrence> Carry(WorldBeliefRevision? previous) =>
            previous?.Occurrences?.Select(item => new ProposedOccurrence(item.OwningContainerId,
                item.Role, item.SemanticDescriptor, item.State, item.Locator, item.Native, item.Space)).ToArray()
            ?? Array.Empty<ProposedOccurrence>();
    }

    internal sealed record Scenario(MinimalScenarioBundle Bundle, RunOptions Options, EffectCapture Effects);

    internal static Scenario DeepThenShallow()
    {
        var golden = GoldenScenarioBundles.AlreadyOnZeroEffect();
        var initial = (ScenarioStimulus.ObservationFrame)golden.Stimuli[0];
        var pages = new[]
        {
            new Page("root", new[] { Link("A"), Link("C") }),
            new Page("A", new[] { Link("C"), Back() }),
            new Page("C", new[] { Link("D"), Back() }),
            new Page("A", new[] { Link("C"), Back() }),
            new Page("root", new[] { Link("A"), Link("C") }),
            new Page("C", new[] { Link("D"), Back() }),
            new Page("D", new[] { Back() }),
            new Page("C", new[] { Link("D"), Back() }),
            new Page("root", new[] { Link("A"), Link("C") }),
        };
        var stimuli = pages.Select((page, index) => Frame(initial, page, index,
            index == 0 ? ObservationContext.External : ObservationContext.PostActionEffectFlow)).ToArray();
        var bundle = ScenarioBundleDigest.Sealed(golden with
        {
            BundleId = "sim-traversal-depth-two-then-one",
            ScenarioId = "sim-traversal-depth-two-then-one",
            ScenarioVersion = "v1",
            Stimuli = stimuli,
            PhaseScript = new PhaseAwareAgentScript(Array.Empty<ScriptedTurn>()),
            Expected = new ScenarioExpectation("TerminalNotProven", null, 8, 2, 0, null),
            Contract = Contract(),
            Goal = new GoalSpec("goal-traversal-fixture", "exercise the traversal owner",
                RequiredClassification: null, RequiredObligationIds: Array.Empty<string>()),
        });
        var effects = new EffectCapture();
        var inspector = new CapabilityRegistry(TrustDomain.RuntimeIntegration);
        inspector.Register(new LanguageFormatInspector(), "simulation-traversal-fixture");
        var options = new RunOptions
        {
            Seams = new SeamOverrides
            {
                Agent = CreateAgent(NavigationRules(NavigationOperation("visit-link")), maxDepth: 2),
                Association = new ProductAssociationStrategy(),
                Observation = new MenuObservationStrategy(),
                Driver = effects.Driver(),
                TraversalProjection = effects.Project,
                CanBindPlanLanguage = handler => CanBind(inspector, handler),
                InspectPlanLanguage = (handler, input) => Inspect(inspector, handler, input, effects),
                ObserveTraversalRecovery = _ => { },
            },
        };
        return new Scenario(bundle, options, effects);
    }

    internal static AgentDecision Decide(AgentDecisionContext context, IReadOnlyList<TraversalRule> rules,
        int maxDepth = 2, int maxOperations = 128, AgentActionStep? returnStep = null)
    {
        if (context.Phase == AgentDecisionPhase.InitialPlanning)
            return new AgentDecision.Plan(context.DecisionId, new AgentPlanProposal(
                new PlanItem[]
                {
                    new PlanItem.TraverseItem(Source, rules, maxDepth,
                        returnStep ?? new AgentActionStep("button", "Back", "tap", null)),
                }, "run one bounded traversal through current World evidence", "sim-traversal-plan",
                MaxLocalOperations: maxOperations));
        if (context.PlanExecution?.Status == PlanExecutionStatus.Paused)
            return new AgentDecision.ContinuePlan(context.DecisionId, context.PlanExecution.Position);
        return new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId,
            "traversal fixture has no additional goal-directed action"));
    }

    private static Func<AgentDecisionContext, AgentDecision?> CreateAgent(
        IReadOnlyList<TraversalRule> rules, int maxDepth = 2, int maxOperations = 128,
        AgentActionStep? returnStep = null, Func<AgentDecisionContext, bool>? continuePaused = null)
    {
        var issuedPlan = false;
        return context =>
        {
            if (!issuedPlan && context.Phase == AgentDecisionPhase.InitialPlanning)
            {
                issuedPlan = true;
                return Decide(context, rules, maxDepth, maxOperations, returnStep);
            }
            if (context.PlanExecution?.Status == PlanExecutionStatus.Paused
                && (continuePaused?.Invoke(context) ?? true))
                return new AgentDecision.ContinuePlan(context.DecisionId, context.PlanExecution.Position);
            return new AgentDecision.NoAction(new AgentNoActionProposal(context.DecisionId,
                "traversal fixture has no additional goal-directed action"));
        };
    }

    internal static IReadOnlyList<TraversalRule> NavigationRules(params TraversalOperation[] operations) =>
        operations.Select((operation, index) => new TraversalRule($"navigate-{index + 1}", "navigation", operation)).ToArray();

    internal static TraversalOperation NavigationOperation(string operationId,
        IReadOnlyList<PlanLanguageHandler>? before = null, IReadOnlyList<PlanLanguageHandler>? after = null) =>
        new(operationId, TraversalOperationKind.Navigate,
            new AgentActionStep("$candidate", "$candidate", "tap", null),
            before ?? Array.Empty<PlanLanguageHandler>(), after ?? Array.Empty<PlanLanguageHandler>());

    internal static TraversalOperation ReadOnlyOperation(string operationId,
        IReadOnlyList<PlanLanguageHandler>? before = null, IReadOnlyList<PlanLanguageHandler>? after = null) =>
        new(operationId, TraversalOperationKind.ReadOnly, null,
            before ?? Array.Empty<PlanLanguageHandler>(), after ?? Array.Empty<PlanLanguageHandler>());

    internal static PlanLanguageHandler EnglishHandler(string id, bool required = true, bool requirePass = false) =>
        new(id, LanguageInspectionProtocol.CapabilityId, LanguageInspectionProtocol.Version,
            "en", required, requirePass);

    internal static Scenario Create(Page[] pages, IReadOnlyList<TraversalRule> rules,
        int maxDepth = 2, int maxOperations = 128, AgentActionStep? returnStep = null,
        TimeSpan? handlerTimeout = null, Func<PlanLanguageHandler, PlanHandlerInput, Finding?>? inspectOverride = null,
        Func<PlanLanguageHandler, bool>? canBindOverride = null,
        Action<TraversalRecoveryFacts>? recoveryObserver = null,
        Func<AgentDecisionContext, AgentDecision?>? agentOverride = null,
        Func<UniKernel, string, TraversalObservation?>? projectionOverride = null,
        ExecutionContract? contractOverride = null,
        ScenarioExpectation? expectedOverride = null,
        GoalSpec? goalOverride = null,
        Func<AgentDecisionContext, bool>? continuePaused = null,
        Action<PlanLanguageHandler, PlanHandlerInput>? inspectObserver = null)
    {
        var golden = GoldenScenarioBundles.AlreadyOnZeroEffect();
        var initial = (ScenarioStimulus.ObservationFrame)golden.Stimuli[0];
        var stimuli = pages.Select((page, index) => Frame(initial, page, index,
            index == 0 ? ObservationContext.External : ObservationContext.PostActionEffectFlow)).ToArray();
        var bundle = ScenarioBundleDigest.Sealed(golden with
        {
            BundleId = "sim-traversal-" + string.Join('-', pages.Select(page => page.Id)),
            ScenarioId = "sim-traversal-" + string.Join('-', pages.Select(page => page.Id)),
            ScenarioVersion = "v1",
            Stimuli = stimuli,
            PhaseScript = new PhaseAwareAgentScript(Array.Empty<ScriptedTurn>()),
            Expected = expectedOverride ?? new ScenarioExpectation("TerminalNotProven", null, 0, 2, 0, null),
            Contract = contractOverride ?? Contract(),
            Goal = goalOverride ?? new GoalSpec("goal-traversal-fixture", "exercise the traversal owner",
                RequiredClassification: null, RequiredObligationIds: Array.Empty<string>()),
        });
        var effects = new EffectCapture();
        var registry = new CapabilityRegistry(TrustDomain.RuntimeIntegration);
        registry.Register(new LanguageFormatInspector(), "simulation-traversal-fixture");
        var options = new RunOptions
        {
            Seams = new SeamOverrides
            {
                Agent = agentOverride ?? CreateAgent(rules, maxDepth, maxOperations, returnStep, continuePaused),
                Association = new ProductAssociationStrategy(),
                Observation = new MenuObservationStrategy(),
                Driver = effects.Driver(),
                TraversalProjection = projectionOverride ?? effects.Project,
                CanBindPlanLanguage = canBindOverride ?? (handler => CanBind(registry, handler)),
                InspectPlanLanguage = (handler, input) =>
                {
                    inspectObserver?.Invoke(handler, input);
                    return inspectOverride is null
                        ? Inspect(registry, handler, input, effects)
                        : inspectOverride(handler, input);
                },
                ObserveTraversalRecovery = facts => recoveryObserver?.Invoke(facts),
                PlanHandlerTimeout = handlerTimeout,
            },
        };
        return new Scenario(bundle, options, effects);
    }

    private static bool CanBind(CapabilityRegistry registry, PlanLanguageHandler handler) =>
        registry.Resolve(handler.CapabilityId) is ILanguageInspector
        && handler.CapabilityId == LanguageInspectionProtocol.CapabilityId
        && handler.ProtocolVersion == LanguageInspectionProtocol.Version
        && handler.ExpectedLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase);

    private static Finding? Inspect(CapabilityRegistry registry, PlanLanguageHandler handler,
        PlanHandlerInput input, EffectCapture effects)
    {
        if (registry.Resolve(handler.CapabilityId) is not ILanguageInspector inspector) return null;
        var current = effects.CurrentKernel?.CurrentBelief;
        var targetContainerId = input.TargetContainerId ?? input.ContainerId;
        var currentContainerId = current?.WorldState.GetValueOrDefault(WorldModel.CurrentContainerSubject)?.Value;
        var occurrence = currentContainerId == targetContainerId
            ? current?.Occurrences?.SingleOrDefault(item => item.OwningContainerId == targetContainerId
                && item.Role == input.Target.TargetRole && item.SemanticDescriptor == input.Target.TargetDescriptor)
            : null;
        var correlation = new CapabilityCorrelation(input.RunId, operationId: input.OperationId,
            domainCorrelation: input.SourceRevisionId);
        var items = occurrence is null ? Array.Empty<ObservationTextItem>()
            : new[] { new ObservationTextItem(occurrence.OccurrenceId, occurrence.EvidenceBasis[0],
                occurrence.SemanticDescriptor, null) };
        return inspector.Inspect(new LanguageInspectorRequest(correlation, handler.ProtocolVersion,
            handler.ExpectedLanguage, items.Length == 0 ? InspectorCoverage.Missing : InspectorCoverage.Full,
            items.Length == 0 ? InspectorInputState.MissingInput : InspectorInputState.Accepted, items));
    }

    private static ScenarioStimulus.ObservationFrame Frame(ScenarioStimulus.ObservationFrame template,
        Page page, int index, ObservationContext context)
    {
        var entries = page.Entries.Select(entry => new MenuEntry(entry.Role, entry.Descriptor,
            entry.Traverse, entry.ReadOnly, entry.State, entry.Disposition, entry.Reason)).ToArray();
        var menu = JsonSerializer.Serialize(new MenuDocument(page.Complete, entries));
        var claims = new[]
        {
            new ReviewedStateClaim("ui.screen", page.Id, "screen:" + page.Id),
            new ReviewedStateClaim("ui.screen.route", page.Id, "route:" + page.Id),
            new ReviewedStateClaim("menu.ready", menu, $"menu:{page.Id}:{index}"),
        };
        return template with
        {
            ReviewedElements = Array.Empty<ReviewedElement>(),
            ReviewedStateClaims = claims,
            Context = context,
            StimulusId = $"traversal-page-{index}-{page.Id}",
            VirtualTime = Start.AddSeconds(index * 10),
        };
    }

    private static ExecutionContract Contract() => new(
        "sim-traversal-v1", "exercise a bounded UI traversal",
        new HashSet<string>(StringComparer.Ordinal)
        {
            "live.frame", "ui.screen", "ui.screen.route", "menu.ready",
        },
        new HashSet<string>(StringComparer.Ordinal) { "tap" },
        new HashSet<string>(StringComparer.Ordinal),
        new[] { "traversal-evidence" },
        MaxConsultations: 16, MaxTotalSteps: 64,
        TaskInstanceId: "task-sim-traversal", ProductSessionId: "session-sim-traversal",
        ContractRef: "contract-sim-traversal", AcceptedContractViewRef: "view-sim-traversal",
        ContractGeneration: 1);

    internal static MenuEntry Link(string descriptor) => new("menuItem", descriptor, true);
    internal static MenuEntry ReadOnlyTarget(string role, string descriptor, string state = "unchecked") =>
        new(role, descriptor, false, true, state);
    internal static MenuEntry StateTarget(string role, string descriptor, string state) => new(role, descriptor, false, false, state);
    internal static MenuEntry BlockedLink(string descriptor, string reason = "simulated-policy-block") =>
        new("menuItem", descriptor, true, false, null, PlanCandidateDisposition.Blocked, reason);
    internal static MenuEntry Back() => new("button", "Back", false);
}
