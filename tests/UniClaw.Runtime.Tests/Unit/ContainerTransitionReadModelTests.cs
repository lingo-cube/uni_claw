using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Runtime.Agent;
using UniClaw.Runtime.Model;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// Capability tests for the ContainerTransitionReadModel public read seam
/// (WI-CRV2-R8 validation half).  They prove the model is a pure, exact
/// projection of the sole immutable V2 state: no V2 field is ever inferred
/// from Belief/legacy evidence, entry-null is distinct from state-unavailable,
/// observed current stays separate from active execution, multi-entry evidence
/// stays distinct, and V2 occurrences are never substituted for legacy
/// transitions.
/// </summary>
public sealed class ContainerTransitionReadModelTests
{
    /// <summary>A null V2 state stays explicitly unavailable and never infers V2 fields from legacy values.</summary>
    [Fact]
    public void NullV2StateProjectsLegacyFieldsOnlyAndInfersNoV2Fields()
    {
        var legacy = Legacy("SettingsRoot", "Display");
        var model = ContainerTransitionReadModel.From(
            observedLocation: "SettingsRoot",
            activeExecutionContainer: "Display",
            activeAncestorPath: ["SettingsMain"],
            transitions: new[] { legacy },
            v2State: null);

        Assert.False(model.IsV2StateAvailable);
        Assert.Equal(ContainerFastAssessmentAvailability.Unavailable, model.FastAssessmentAvailability);
        Assert.Null(model.CurrentNodeRef);
        Assert.Null(model.CurrentSliceRef);
        Assert.Null(model.EntrySourceNodeRef);
        Assert.Null(model.EntryTransitionOccurrenceRef);
        Assert.Null(model.EntryRelationRef);
        Assert.Null(model.LatestTransitionOccurrence);
        Assert.Null(model.EvidenceRevision);

        // Legacy evidence stays intact and is the only populated channel.
        Assert.Equal("SettingsRoot", model.CurrentObservedLocation);
        Assert.Equal("Display", model.ActiveExecutionContainer);
        Assert.Equal(["SettingsMain"], model.ActiveAncestorPath);
        Assert.Same(legacy, model.LatestTransition);
        Assert.Equal("evidence:legacy", model.EvidenceRef);
        Assert.Contains(model.Diagnostics, diagnostic => diagnostic.Contains("MISSING_ASSET", StringComparison.Ordinal));
    }

    /// <summary>A present current Container without entry context keeps current/revision known and entry known-null.</summary>
    [Fact]
    public void CurrentContainerWithoutEntryContextKeepsCurrentKnownAndEntryKnownNull()
    {
        var destination = new ContainerNodeRef("node:settings-root");
        var slice = new ContainerSliceRef("slice:5");
        var occurrence = Occ("occ:entry", 5, new ContainerNodeRef("node:settings-main"), destination);
        var state = new ContainerRuntimeV2State(
            currentContainer: new CurrentContainer(destination, slice, entryContext: null),
            transitionOccurrences: new[] { occurrence },
            evidenceRevision: new SemanticEvidenceRevision(5));

        var model = ContainerTransitionReadModel.From(
            observedLocation: "SettingsRoot",
            activeExecutionContainer: "Display",
            activeAncestorPath: ["SettingsMain"],
            transitions: null,
            v2State: state);

        Assert.True(model.IsV2StateAvailable);
        Assert.Equal(ContainerFastAssessmentAvailability.NotRetained, model.FastAssessmentAvailability);
        Assert.Equal(destination, model.CurrentNodeRef);
        Assert.Equal(slice, model.CurrentSliceRef);
        Assert.Equal(new SemanticEvidenceRevision(5), model.EvidenceRevision);
        Assert.Equal(occurrence, model.LatestTransitionOccurrence);
        // Known-null entry fields are distinct from whole-state unavailability.
        Assert.Null(model.EntrySourceNodeRef);
        Assert.Null(model.EntryTransitionOccurrenceRef);
        Assert.Null(model.EntryRelationRef);
    }

    /// <summary>A full V2 state projects every V2 field as an exact match of the immutable state.</summary>
    [Fact]
    public void FullV2StateProjectsExactImmutableValuesAndFastAssessmentNotRetained()
    {
        var source = new ContainerNodeRef("node:settings-main");
        var destination = new ContainerNodeRef("node:settings-root");
        var first = Occ("occ:1", 1, new ContainerNodeRef("node:launcher"), source);
        var second = Occ("occ:2", 2, source, destination);
        var slice = new ContainerSliceRef("slice:2");
        var relation = new ContainerRelationRef("relation:occ:2");
        var entry = new ContainerEntryContext(source, second.OccurrenceRef, relation);
        var state = new ContainerRuntimeV2State(
            graph: new ContainerGraphSnapshot(
                nodes: new[] { new ContainerGraphNode(source, "SettingsMain"), new ContainerGraphNode(destination, "SettingsRoot") },
                relations: new[] { new ContainerGraphRelation(relation, source, destination, "affordance:occ:2") },
                occurrenceRefs: new[] { first.OccurrenceRef, second.OccurrenceRef }),
            currentContainer: new CurrentContainer(destination, slice, entry),
            transitionOccurrences: new[] { first, second },
            evidenceRevision: new SemanticEvidenceRevision(2));

        var model = ContainerTransitionReadModel.From(
            observedLocation: "SettingsRoot",
            activeExecutionContainer: "Display",
            activeAncestorPath: ["SettingsMain"],
            transitions: null,
            v2State: state);

        Assert.True(model.IsV2StateAvailable);
        Assert.Equal(ContainerFastAssessmentAvailability.NotRetained, model.FastAssessmentAvailability);
        Assert.Equal(destination, model.CurrentNodeRef);
        Assert.Equal(slice, model.CurrentSliceRef);
        Assert.Equal(source, model.EntrySourceNodeRef);
        Assert.Equal(second.OccurrenceRef, model.EntryTransitionOccurrenceRef);
        Assert.Equal(relation, model.EntryRelationRef);
        Assert.Equal(state.TransitionOccurrences.LastOrDefault(), model.LatestTransitionOccurrence);
        Assert.Equal(new SemanticEvidenceRevision(2), model.EvidenceRevision);
    }

    /// <summary>Observed V2 current and active execution are separate readable channels that never merge.</summary>
    [Fact]
    public void ObservedV2CurrentAndActiveExecutionAreSeparateReadableChannels()
    {
        var observed = new ContainerNodeRef("node:settings-root");
        var slice = new ContainerSliceRef("slice:5");
        var occurrence = Occ("occ:5", 5, new ContainerNodeRef("node:settings-main"), observed);
        var state = new ContainerRuntimeV2State(
            currentContainer: new CurrentContainer(observed, slice),
            transitionOccurrences: new[] { occurrence },
            evidenceRevision: new SemanticEvidenceRevision(5));

        var model = ContainerTransitionReadModel.From(
            observedLocation: "SettingsRoot",
            activeExecutionContainer: "Display",
            activeAncestorPath: ["SettingsMain"],
            transitions: null,
            v2State: state);

        // V2 observed-current channel is present and exact.
        Assert.Equal(observed, model.CurrentNodeRef);
        Assert.Equal(slice, model.CurrentSliceRef);
        // The independent execution channel keeps its own value.
        Assert.Equal("Display", model.ActiveExecutionContainer);
        // Legacy observed location is not overwritten by V2 current presence.
        Assert.Equal("SettingsRoot", model.CurrentObservedLocation);
    }

    /// <summary>Same destination with different entry evidence keeps source/occurrence refs exact and distinct.</summary>
    [Fact]
    public void SameDestinationWithDifferentEntryContextsProjectsDistinctExactEntryRefs()
    {
        var destination = new ContainerNodeRef("node:settings-root");
        var firstEntry = new ContainerEntryContext(
            new ContainerNodeRef("node:settings-main"),
            new TransitionOccurrenceRef("occ:enter-main"));
        var firstState = new ContainerRuntimeV2State(
            currentContainer: new CurrentContainer(destination, new ContainerSliceRef("slice:1"), firstEntry),
            transitionOccurrences: new[] { Occ("occ:enter-main", 1, new ContainerNodeRef("node:settings-main"), destination) },
            evidenceRevision: new SemanticEvidenceRevision(1));
        var secondEntry = new ContainerEntryContext(
            new ContainerNodeRef("node:display-root"),
            new TransitionOccurrenceRef("occ:return-verified"));
        var secondState = new ContainerRuntimeV2State(
            currentContainer: new CurrentContainer(destination, new ContainerSliceRef("slice:2"), secondEntry),
            transitionOccurrences: new[]
            {
                Occ("occ:enter-main", 1, new ContainerNodeRef("node:settings-main"), destination),
                Occ("occ:return-verified", 2, new ContainerNodeRef("node:display-root"), destination),
            },
            evidenceRevision: new SemanticEvidenceRevision(2));

        var firstModel = Project(destination, firstState);
        var secondModel = Project(destination, secondState);

        Assert.Equal(destination, firstModel.CurrentNodeRef);
        Assert.Equal(destination, secondModel.CurrentNodeRef);
        Assert.Equal(firstEntry.SourceNodeRef, firstModel.EntrySourceNodeRef);
        Assert.Equal(firstEntry.EntryTransitionOccurrenceRef, firstModel.EntryTransitionOccurrenceRef);
        Assert.Equal(secondEntry.SourceNodeRef, secondModel.EntrySourceNodeRef);
        Assert.Equal(secondEntry.EntryTransitionOccurrenceRef, secondModel.EntryTransitionOccurrenceRef);
        Assert.NotEqual(firstModel.EntrySourceNodeRef, secondModel.EntrySourceNodeRef);
        Assert.NotEqual(firstModel.EntryTransitionOccurrenceRef, secondModel.EntryTransitionOccurrenceRef);
        Assert.Equal(firstState.TransitionOccurrences[^1], firstModel.LatestTransitionOccurrence);
        Assert.Equal(secondState.TransitionOccurrences[^1], secondModel.LatestTransitionOccurrence);
        // No canonical parent or reverse-edge vocabulary exists on the read seam.
        Assert.DoesNotContain(
            typeof(ContainerTransitionReadModel).GetProperties(),
            property => property.Name.Contains("CanonicalParent", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("ReverseEdge", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Latest V2 occurrence and latest legacy transition are independently readable, never substituted.</summary>
    [Fact]
    public void LatestV2OccurrenceAndLatestLegacyTransitionAreIndependentlyReadable()
    {
        var source = new ContainerNodeRef("node:settings-main");
        var destination = new ContainerNodeRef("node:settings-root");
        var first = Occ("occ:1", 1, source, destination);
        var second = Occ("occ:2", 2, source, destination);
        var legacy = Legacy("SettingsRoot", "Display");
        Assert.NotEqual(legacy.TransitionRef, second.OccurrenceRef.Value);
        var state = new ContainerRuntimeV2State(
            currentContainer: new CurrentContainer(destination, new ContainerSliceRef("slice:2")),
            transitionOccurrences: new[] { first, second },
            evidenceRevision: new SemanticEvidenceRevision(2));

        var model = ContainerTransitionReadModel.From(
            observedLocation: "SettingsRoot",
            activeExecutionContainer: "Display",
            activeAncestorPath: ["SettingsMain"],
            transitions: new[] { legacy },
            v2State: state);

        Assert.Equal(second, model.LatestTransitionOccurrence);
        Assert.Same(legacy, model.LatestTransition);
        Assert.Equal(new TransitionOccurrenceRef("occ:2"), model.LatestTransitionOccurrence!.OccurrenceRef);
        Assert.Equal("run-r5:container-transition:legacy", model.LatestTransition!.TransitionRef);
    }

    /// <summary>Agent.ContainerContext V2 fields exactly equal the sole live V2 state values.</summary>
    [Fact]
    public async Task AgentContainerContextProjectsExactlyTheSoleV2StateValues()
    {
        var harness = UniClaw.Runtime.Tests.Scenario.ScenarioHarness.Create("happy");

        await harness.RunAsync();

        var state = ReadV2State(harness.Agent);
        Assert.NotNull(state.CurrentContainer);
        var activeExecution = ReadActiveExecutionSemantic(harness.Agent);
        var context = harness.Agent.ContainerContext;

        Assert.True(context.IsV2StateAvailable);
        Assert.Equal(ContainerFastAssessmentAvailability.NotRetained, context.FastAssessmentAvailability);
        Assert.Equal(state.CurrentContainer!.NodeRef, context.CurrentNodeRef);
        Assert.Equal(state.CurrentContainer.CurrentSliceRef, context.CurrentSliceRef);
        Assert.Equal(state.CurrentContainer.EntryContext?.SourceNodeRef, context.EntrySourceNodeRef);
        Assert.Equal(state.CurrentContainer.EntryContext?.EntryTransitionOccurrenceRef, context.EntryTransitionOccurrenceRef);
        Assert.Equal(state.CurrentContainer.EntryContext?.EntryRelationRef, context.EntryRelationRef);
        Assert.Equal(state.TransitionOccurrences.LastOrDefault(), context.LatestTransitionOccurrence);
        Assert.Equal(state.EvidenceRevision, context.EvidenceRevision);
        // Independent channels from the same live Agent: V2 current ref and active execution.
        Assert.Equal(activeExecution, context.ActiveExecutionContainer);
    }

    /// <summary>Repeated ContainerContext reads are pure: no state, context, or trace mutation.</summary>
    [Fact]
    public async Task AgentContainerContextRepeatedReadIsPureWithoutStateMutation()
    {
        var harness = UniClaw.Runtime.Tests.Scenario.ScenarioHarness.Create("happy");

        await harness.RunAsync();

        var stateField = typeof(RuntimeAgent).GetField("_containerRuntimeV2State", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Agent._containerRuntimeV2State field is missing.");
        var activeContextField = typeof(RuntimeAgent).GetField("_activeContainerContext", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Agent._activeContainerContext field is missing.");
        var state = stateField.GetValue(harness.Agent);
        Assert.NotNull(state);
        var activeContext = activeContextField.GetValue(harness.Agent);
        var traceCount = harness.Agent.Trace.Count;

        var first = harness.Agent.ContainerContext;
        var second = harness.Agent.ContainerContext;

        // Note: record equality is not used here — ImmutableArray<string> fields
        // make record equality reference-based; field-wise comparison with
        // sequence-aware array assertions is the deterministic check.
        AssertSameProjection(first, second);
        Assert.Same(state, stateField.GetValue(harness.Agent));
        Assert.Same(activeContext, activeContextField.GetValue(harness.Agent));
        Assert.Equal(traceCount, harness.Agent.Trace.Count);
    }

    private static void AssertSameProjection(
        ContainerTransitionReadModel expected,
        ContainerTransitionReadModel actual)
    {
        Assert.Equal(expected.CurrentObservedLocation, actual.CurrentObservedLocation);
        Assert.Equal(expected.ActiveExecutionContainer, actual.ActiveExecutionContainer);
        Assert.Equal(expected.ActiveAncestorPath.ToArray(), actual.ActiveAncestorPath.ToArray());
        Assert.Equal(expected.LatestTransition, actual.LatestTransition);
        Assert.Equal(expected.CurrentNodeRef, actual.CurrentNodeRef);
        Assert.Equal(expected.CurrentSliceRef, actual.CurrentSliceRef);
        Assert.Equal(expected.EntrySourceNodeRef, actual.EntrySourceNodeRef);
        Assert.Equal(expected.EntryTransitionOccurrenceRef, actual.EntryTransitionOccurrenceRef);
        Assert.Equal(expected.EntryRelationRef, actual.EntryRelationRef);
        Assert.Equal(expected.LatestTransitionOccurrence, actual.LatestTransitionOccurrence);
        Assert.Equal(expected.EvidenceRevision, actual.EvidenceRevision);
        Assert.Equal(expected.IsV2StateAvailable, actual.IsV2StateAvailable);
        Assert.Equal(expected.FastAssessmentAvailability, actual.FastAssessmentAvailability);
        Assert.Equal(expected.CompletenessRef, actual.CompletenessRef);
        Assert.Equal(expected.EvidenceRef, actual.EvidenceRef);
        Assert.Equal(expected.AssetRef, actual.AssetRef);
        Assert.Equal(expected.IsAssetMissing, actual.IsAssetMissing);
        Assert.Equal(expected.Diagnostics.ToArray(), actual.Diagnostics.ToArray());
    }

    private static ContainerTransitionReadModel Project(ContainerNodeRef current, ContainerRuntimeV2State state)
        => ContainerTransitionReadModel.From(
            observedLocation: "SettingsRoot",
            activeExecutionContainer: "Display",
            activeAncestorPath: ["SettingsMain"],
            transitions: null,
            v2State: state);

    private static ContainerRuntimeV2State ReadV2State(RuntimeAgent agent)
    {
        var field = Assert.Single(
            typeof(RuntimeAgent).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(candidate => candidate.FieldType == typeof(ContainerRuntimeV2State)));
        return Assert.IsType<ContainerRuntimeV2State>(field.GetValue(agent));
    }

    private static string? ReadActiveExecutionSemantic(RuntimeAgent agent)
    {
        var context = typeof(RuntimeAgent)
            .GetField("_activeContainerContext", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(agent) as ActiveContainerContext;
        return context?.ActiveExecutionContainer.SemanticPageName;
    }

    private static ContainerTransitionOccurrence Occ(
        string id,
        long revision,
        ContainerNodeRef source,
        ContainerNodeRef destination)
        => new(
            new TransitionOccurrenceRef(id),
            "observation:" + revision,
            new SemanticEvidenceRevision(revision),
            ContainerTransitionBoundary.NEW_CONTAINER,
            isCompleted: true,
            source,
            "trigger:" + id,
            destination,
            ["evidence:" + id]);

    private static ContainerTransition Legacy(string observedLocation, string execution)
        => new(
            "run-r5:container-transition:legacy",
            "SettingsRoot",
            observedLocation,
            execution,
            null,
            "observation:legacy",
            "container:" + execution + ":local-completeness",
            ContainerTransitionKind.PREMATURE_RETURN_TO_ACTIVE_PARENT,
            ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED,
            "evidence:legacy");
}