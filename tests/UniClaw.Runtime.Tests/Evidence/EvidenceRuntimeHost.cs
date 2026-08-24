using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.World;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Evidence;

/// <summary>
/// Generic Runtime test host for the Evidence + Specification driven model.
///
/// Wires an <see cref="EvidenceFixture"/> into the real Runtime (Startup +
/// Traversal + Container factory + Agent + Goal + type-level specification)
/// with the shared fixture semantic capability, runs open-world execution,
/// and returns the produced evidence surfaces for the generic evaluator.
///
/// The host is scenario-neutral: it derives all wiring from the fixture's
/// declared screens/relations and the specification — it contains no scenario
/// knowledge and no expected action sequence.
/// </summary>
public sealed class EvidenceRuntimeHost
{
    private readonly EvidenceFixture _fixture;
    private readonly ExpectedSpecification _spec;
    private readonly List<GoalEvidence> _evidence = [];

    private EvidenceRuntimeHost(EvidenceFixture fixture, ExpectedSpecification spec)
    {
        _fixture = fixture;
        _spec = spec;
    }

    /// <summary>Agent trace produced by the run (evidence surface).</summary>
    public RuntimeAgent? Agent { get; private set; }

    /// <summary>Scripted environment used for the run (observation/action history surface).</summary>
    public ScriptedEnvironment? Environment { get; private set; }

    /// <summary>GoalEvidence receipts produced by the run.</summary>
    public IReadOnlyList<GoalEvidence> EvidenceReceipts => _evidence;

    public static EvidenceRuntimeHost Create(EvidenceFixture fixture, ExpectedSpecification spec)
        => new(fixture, spec);

    /// <summary>
    /// Runs open-world execution over the fixture with the given semantic
    /// role classifier, then returns the produced evidence for evaluation.
    /// </summary>
    /// <param name="classifier">Fixture semantic role classifier (element → role);
    /// null = default scenario-neutral classifier: an element whose text equals a
    /// parent container identity of the current container is a PARENT-RETURN
    /// control; every other text-bearing element is a navigation candidate.</param>
    public async Task<EvaluationResult> RunAndEvaluateAsync(
        Func<ObservedElement, FixtureSemanticRole?>? classifier = null,
        string? runId = null,
        CancellationToken cancellationToken = default)
    {
        var env = _fixture.ToScriptedEnvironment();
        Environment = env;

        var semanticEnv = classifier is null
            ? new SemanticCapabilityTestEnvironment(env, DefaultContextClassifier)
            : new SemanticCapabilityTestEnvironment(env, classifier);

        var traversal = new RuntimeTraversal(semanticEnv);

        // Scenario-neutral page resolver: identity by declared container list
        // (a screen is "mine" when all its declared element texts are present).
        string? Resolve(Observation o) => ResolveContainerIdentity(o, _fixture);

        RuntimeContainer Factory(string page) => new(page,
            o => string.Equals(Resolve(o), page, StringComparison.Ordinal),
            traversal.ExecuteStep);

        var startup = new RuntimeStartup(semanticEnv, _spec.ApplicationIdentity, Resolve);
        var recovery = new RuntimeRecovery(semanticEnv,
            _ => ImmutableArray<DeviceAction>.Empty, (_, _) => null, (_, _) => true);

        var goal = new Goal(
            EvidenceEvaluator: observation =>
            {
                var signal = GoalSignalFor(observation, _fixture, _spec);
                var evidence = new GoalEvidence(
                    signal,
                    signal ? "goal signal observed." : "goal signal not observed.",
                    observation.SequenceNumber);
                _evidence.Add(evidence);
                return evidence;
            },
            CandidateAuthorizationEvaluator: (_, candidate) =>
                new CandidateAuthorizationEvidence(true, $"authorized: {candidate.Text}"),
            BranchInventoryEvaluator: (observations, _) =>
                BuildInventory(observations, _fixture),
            CategoryClassifier: _spec.IncludeStateChangingControls
                ? element => element.SwitchState is not null
                    ? TypeLevelElementCategory.StateChangingControl
                    : string.IsNullOrEmpty(element.Text)
                        ? (TypeLevelElementCategory?)null
                        : TypeLevelElementCategory.NavigableContainer
                : null);

        var agent = new RuntimeAgent(
            startup, traversal, token => semanticEnv.ObserveAsync(token),
            Resolve, Factory, recovery);
        Agent = agent;

        var spec = _spec.ToTypeLevelSpecification();
        var envelope = IntentSemanticEnvelope.Project(
            "evidence-driven open-world run", goal,
            new IntentExecutionRepresentation.OpenWorldTypeLevel(spec));

        var state = await IntentExecution.RunOpenWorldAsync(
            agent, envelope, runId ?? "evidence-run-1", cancellationToken);

        return EvidenceEvaluator.Evaluate(_spec, agent, _evidence);
    }

    /// <summary>Scenario-neutral container identity: a screen is the container
    /// whose declared element texts are all present in the observation. When
    /// several variants match (OFF/ON of the same container), the variant with
    /// the most matching declared texts wins (an ON variant carrying an extra
    /// status element is the more specific match). All variants share the
    /// container's semantic identity.</summary>
    private static string? ResolveContainerIdentity(Observation o, EvidenceFixture fixture)
    {
        EvidenceScreen? best = null;
        var bestCount = -1;
        foreach (var screen in fixture.Screens)
        {
            var texts = screen.Elements.Select(e => e.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
            if (texts.Length == 0)
                continue;
            if (texts.All(t => o.Elements.Any(e => string.Equals(e.Text, t, StringComparison.Ordinal)))
                && texts.Length > bestCount)
            {
                best = screen;
                bestCount = texts.Length;
            }
        }
        return best?.SemanticIdentity;
    }

    /// <summary>
    /// Default scenario-neutral context classifier:
    ///   - an element whose text equals the CURRENT container's declared parent
    ///     (from the fixture's child relations) is a PARENT-RETURN control;
    ///   - a switch-state element is a LOCAL control (never navigation);
    ///   - an element whose text equals one of the current container's declared
    ///     children is a NAVIGATION candidate;
    ///   - everything else text-bearing is a navigation candidate (leaf signals
    ///     are not navigation targets, but generic world elements are).
    /// Roles come from the fixture graph, never from scenario vocabulary.
    /// </summary>
    private FixtureSemanticRole? DefaultContextClassifier(Observation observation, ObservedElement element, int index)
    {
        if (string.IsNullOrWhiteSpace(element.Text))
            return null;

        if (element.SwitchState is not null)
            return FixtureSemanticRole.LocalControl;

        var currentContainer = ResolveContainerIdentity(observation, _fixture);
        if (currentContainer is null)
            return FixtureSemanticRole.NavigationCandidate;

        // Parent-return: element text equals a container that declares this one as a child.
        foreach (var relation in _fixture.ChildRelations)
        {
            if (relation.Children.Contains(currentContainer)
                && string.Equals(relation.ContainerIdentity, element.Text, StringComparison.Ordinal))
            {
                return FixtureSemanticRole.ParentReturnControl;
            }
        }

        return FixtureSemanticRole.NavigationCandidate;
    }

    /// <summary>Goal signal: the declared signal element of any screen is present
    /// in the observation (goal evidence must come from observation, not actions).
    /// A switch-state signal additionally requires the observed SwitchState to be
    /// true — the goal is a state outcome, proven by observation evidence.</summary>
    private static bool GoalSignalFor(Observation o, EvidenceFixture fixture, ExpectedSpecification spec)
    {
        foreach (var signal in fixture.GoalSignals)
        {
            if (o.Elements.Any(e => string.Equals(e.Text, signal.ElementText, StringComparison.Ordinal)
                && (e.SwitchState is null || e.SwitchState is true)))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Generic branch inventory: children of the observed container that
    /// have a matching navigable element in the observation. The expected
    /// inventory comes from the SPECIFICATION (required coverage) intersected
    /// with the fixture's declared child relations; containers the specification
    /// requires but the fixture cannot expose become ghost branches that must
    /// fail closed when their evidence is absent.</summary>
    private BranchInventoryEvidence BuildInventory(
        ImmutableArray<Observation> observations, EvidenceFixture fixture)
    {
        if (observations.IsDefaultOrEmpty)
            return new BranchInventoryEvidence(null, "no observations; inventory unresolved.");

        var latest = observations[^1];
        var container = ResolveContainerIdentity(latest, fixture);
        if (container is null)
            return new BranchInventoryEvidence(null, "container identity unresolved from observation.");

        var expectedChildren = ExpectedChildrenOf(container, latest);
        if (expectedChildren.Count == 0)
            return new BranchInventoryEvidence(ImmutableDictionary<string, long>.Empty, "bounded leaf container.");

        var missing = expectedChildren.Where(c => !latest.Elements.Any(e => string.Equals(e.Text, c, StringComparison.Ordinal))).ToArray();
        if (missing.Length > 0)
            return new BranchInventoryEvidence(null, $"inventory incomplete: missing {string.Join(",", missing)}");

        // EXPLICIT SOURCE PROVENANCE GROUNDING (parent-change contract): each
        // required branch must be grounded to a fresh primary-source occurrence
        // reference, or the Agent refuses dispatch (fail closed).
        var occurrences = SourceEquivalenceNormalizer.OccurrencesOf(latest);
        var grounding = ImmutableDictionary.CreateBuilder<string, NavigationSourceOccurrenceReference>(StringComparer.Ordinal);
        foreach (var child in expectedChildren)
        {
            var occurrence = occurrences.FirstOrDefault(o =>
                o.CanonicalOccurrence.Reference.ElementIndex < latest.Elements.Length
                && string.Equals(latest.Elements[o.CanonicalOccurrence.Reference.ElementIndex].Text, child, StringComparison.Ordinal));
            grounding[child] = new NavigationSourceOccurrenceReference(
                latest.SequenceNumber,
                occurrence?.OccurrenceIdentity ?? $"missing-grounding:{child}");
        }

        var required = expectedChildren.ToImmutableDictionary(c => c, _ => latest.SequenceNumber, StringComparer.Ordinal);
        return new BranchInventoryEvidence(required, $"inventory complete: {expectedChildren.Count} children of '{container}'.", grounding.ToImmutable());
    }

    /// <summary>Children the specification requires for a container: the
    /// fixture-declared relations, plus (when state-changing controls are in
    /// scope) any switch-state control element present in the current
    /// observation, plus any spec-required container that the fixture does not
    /// declare at all (ghost — must fail closed on absent evidence).</summary>
    private ImmutableHashSet<string> ExpectedChildrenOf(string containerIdentity, Observation latest)
    {
        var declared = _fixture.ChildrenOf(containerIdentity).ToImmutableHashSet(StringComparer.Ordinal);
        var controls = _spec.IncludeStateChangingControls
            ? latest.Elements
                .Where(e => e.SwitchState is not null && !string.IsNullOrWhiteSpace(e.Text))
                .Select(e => e.Text!)
                .ToImmutableHashSet(StringComparer.Ordinal)
            : ImmutableHashSet<string>.Empty;
        var ghost = _spec.RequiredCoverage
            .Where(c => !_fixture.ContainerIdentities.Contains(c))
            .ToImmutableHashSet(StringComparer.Ordinal);
        var result = declared.Union(controls);
        return ghost.IsEmpty ? result : result.Union(ghost);
    }
}
