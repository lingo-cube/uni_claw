using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Harness;
using UniClaw.Runtime.Harness.Capture;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Environment;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;
using Xunit;

namespace UniClaw.Runtime.Tests.DriverHost;

public sealed class ContainerContextReadModelTests
{
    private static readonly ContainerTransition Transition = new(
        "run-r5:container-transition:observation:5", "ObservedPage", "ObservedPage", "ExecutionPage", null,
        "observation:5", "container:ExecutionPage:local-completeness",
        ContainerTransitionKind.PREMATURE_RETURN_TO_ACTIVE_PARENT,
        ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED, "observation:5", null);

    private static AgentStateSnapshot Snapshot(ContainerTransitionReadModel context) => new()
    {
        RunId = "run-r5", State = RunState.Running,
        Belief = new WorldBelief("SettingsRoot", 1, "fresh", 5),
        ContainerContext = context,
    };

    private static TraceRun Trace() => new() { RunId = "run-r5", TraceRunId = "trace-r5" };

    [Fact]
    public void R5ProjectionKeepsObservedAndExecutionSeparateAndImmutable()
    {
        var context = new ContainerTransitionReadModel
        {
            CurrentObservedLocation = "ObservedPage", ActiveExecutionContainer = "ExecutionPage",
            ActiveAncestorPath = ["ObservedPage"], LatestTransition = Transition,
            CompletenessRef = Transition.CompletenessRef, EvidenceRef = Transition.EvidenceRef,
            AssetRef = Transition.AssetRef,
        };
        var snapshot = RunSnapshotProjector.Project("run-r5", Trace(), Snapshot(context));
        Assert.Equal("ObservedPage", snapshot.CurrentObservedLocation.Value);
        Assert.Equal("ExecutionPage", snapshot.ActiveExecutionContainer.Value);
        Assert.Equal(new[] { "ObservedPage" }, snapshot.ActiveAncestorPath.Value);
        Assert.Equal(ContainerTransitionKind.PREMATURE_RETURN_TO_ACTIVE_PARENT, snapshot.LatestObservedTransition.Value!.Kind);
        Assert.Equal("container:ExecutionPage:local-completeness", snapshot.CompletenessRef.Value);
        Assert.Equal("observation:5", snapshot.EvidenceRef.Value);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, snapshot.AssetRef.Classification);
        Assert.Contains(snapshot.Diagnostics, d => d.Contains("MISSING_ASSET"));
    }

    [Fact]
    public void DefaultContextIsUnavailableButKnownEmptyPathIsDirect()
    {
        var unavailable = RunSnapshotProjector.Project("run-r5", Trace(), new AgentStateSnapshot());
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, unavailable.CurrentObservedLocation.Classification);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, unavailable.ActiveExecutionContainer.Classification);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, unavailable.ActiveAncestorPath.Classification);
        var empty = RunSnapshotProjector.Project("run-r5", Trace(), Snapshot(new ContainerTransitionReadModel
        {
            CurrentObservedLocation = "Page", ActiveExecutionContainer = "Page", ActiveAncestorPath = []
        }));
        Assert.Equal(SnapshotFieldClassification.DirectPublicProjection, empty.ActiveAncestorPath.Classification);
    }

    [Fact]
    public void OlderRunWithoutTransitionMarksAllTransitionFieldsUnavailable()
    {
        var snapshot = RunSnapshotProjector.Project("old", Trace() with { RunId = "old" }, new AgentStateSnapshot
        {
            RunId = "old", ContainerContext = new ContainerTransitionReadModel
            {
                CurrentObservedLocation = "Root", ActiveExecutionContainer = "Root", ActiveAncestorPath = []
            }
        });
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, snapshot.LatestObservedTransition.Classification);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, snapshot.CompletenessRef.Classification);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, snapshot.EvidenceRef.Classification);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, snapshot.AssetRef.Classification);
    }

    [Fact]
    public void CatalogCorrelatesObservationAndAllFrameAssetsWithoutBodies()
    {
        var bundle = new TraceCaptureBundle
        {
            CaptureSessionId = "cap-r5", Records = [new CaptureRecord { Kind = CaptureRecordKind.Observation, SequenceNumber = 5, FrameId = "frame-5" }],
            Artifacts = [new CaptureArtifact { ArtifactId = "z", FrameId = "frame-5", Content = [1] }, new CaptureArtifact { ArtifactId = "a", FrameId = "frame-5", Content = [2] }]
        };
        var link = EvidenceCatalog.FromBundle(bundle, "run-r5").ResolveTransition(Transition);
        Assert.Equal("capture:cap-r5:record:0", link.EvidenceRef!.Locator);
        Assert.Equal(new[] { "capture:cap-r5:artifact:a", "capture:cap-r5:artifact:z" }, link.AssetRefs.Select(a => a.Locator));
        Assert.All(link.AssetRefs, a => Assert.DoesNotContain("/", a.Locator));
    }

    [Fact]
    public void MissingCatalogAndLinksAreExplicitAndHistoryIsPreserved()
    {
        var noCatalog = RunSnapshotProjector.Project("run-r5", Trace(), Snapshot(new ContainerTransitionReadModel { LatestTransition = Transition }));
        Assert.Contains(noCatalog.Diagnostics, d => d.Contains("MISSING_EVIDENCE"));
        Assert.Contains(noCatalog.Diagnostics, d => d.Contains("MISSING_ASSET"));
        var bundle = new TraceCaptureBundle { CaptureSessionId = "cap", Records = [new CaptureRecord { Kind = CaptureRecordKind.Observation, SequenceNumber = 5, FrameId = "missing" }] };
        var projected = RunSnapshotProjector.Project("run-r5", Trace(), Snapshot(new ContainerTransitionReadModel { LatestTransition = Transition }), EvidenceCatalog.FromBundle(bundle, "run-r5"));
        Assert.Equal("observation:5", projected.LatestObservedTransition.Value!.EvidenceRef);
        Assert.Contains(projected.Diagnostics, d => d.Contains("MISSING_ASSET"));
    }

    [Fact]
    public void ObservabilityRepeatedReadIsDeterministic()
    {
        var o = new DriverHostObservability();
        o.RegisterRun("run-r5", Trace(), Snapshot(new ContainerTransitionReadModel { LatestTransition = Transition }));
        var first = o.GetRunSnapshot("run-r5");
        var second = o.GetRunSnapshot("run-r5");
        Assert.Equal(first.Diagnostics, second.Diagnostics);
        Assert.Equal(first.LatestObservedTransition.Value, second.LatestObservedTransition.Value);
    }

    [Fact]
    public async Task RealAgentSnapshotUsesKnownEmptyContextAndNoLiveHandle()
    {
        var environment = new SnapshotEnvironment();
        var traversal = new RuntimeTraversal(environment);
        var resolve = (Observation observation) => "Root";
        var startup = new RuntimeStartup(environment, "test.app", resolve);
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        var agent = new RuntimeAgent(startup, traversal, environment.ObserveAsync, resolve,
            page => new RuntimeContainer(page, _ => true, traversal.ExecuteStep), recovery);

        await agent.RunAsync(new Goal(_ => new GoalEvidence(true, "initial evidence", 2)), new Plan([]), "real-agent", default);
        var snapshot = AgentStateSnapshot.From(agent);
        Assert.Equal("Root", snapshot.ContainerContext.CurrentObservedLocation);
        Assert.Equal("Root", snapshot.ContainerContext.ActiveExecutionContainer);
        Assert.True(snapshot.ContainerContext.ActiveAncestorPath.IsEmpty);
        Assert.Null(snapshot.ContainerContext.LatestTransition);
        Assert.Equal(SnapshotFieldClassification.DirectPublicProjection,
            RunSnapshotProjector.Project("real-agent", new TraceRun { RunId = "real-agent", TraceRunId = "trace" }, snapshot).ActiveAncestorPath.Classification);
    }

    [Fact]
    public void MissingCatalogIsClassifiedWithoutReplacingCommittedHistory()
    {
        var result = RunSnapshotProjector.Project("run-r5", Trace(), Snapshot(new ContainerTransitionReadModel
        {
            LatestTransition = Transition, EvidenceRef = "committed-history", AssetRef = "committed-asset"
        }));
        Assert.Equal("committed-history", result.ContainerEvidenceRef.Value);
        Assert.Equal("committed-asset", result.ContainerAssetRef.Value);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, result.ContainerCatalogEvidenceRef.Classification);
        Assert.Contains(result.Diagnostics, d => d.Contains("MISSING_EVIDENCE"));
    }

    [Fact]
    public void MissingObservationRecordAndFrameIdAreExplicit()
    {
        var catalog = EvidenceCatalog.FromBundle(new TraceCaptureBundle
        {
            CaptureSessionId = "cap",
            Records = [new CaptureRecord { Kind = CaptureRecordKind.CaptureFault, SequenceNumber = 99, FrameId = null }],
            Artifacts = []
        }, "run-r5");
        var result = RunSnapshotProjector.Project("run-r5", Trace(), Snapshot(new ContainerTransitionReadModel
        {
            LatestTransition = Transition
        }), catalog);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, result.ContainerCatalogEvidenceRef.Classification);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, result.ContainerAssetRef.Classification);
        Assert.Contains(result.Diagnostics, d => d.Contains("MISSING_EVIDENCE"));
        Assert.Contains(result.Diagnostics, d => d.Contains("MISSING_ASSET"));
    }

    [Fact]
    public void MatchingRefsHaveNoMismatchAndMismatchingRefsPreserveHistory()
    {
        var matching = RunSnapshotProjector.Project("run-r5", Trace(), Snapshot(new ContainerTransitionReadModel
        {
            LatestTransition = Transition
        }));
        Assert.DoesNotContain(matching.Diagnostics, d => d.Contains("does not match", StringComparison.Ordinal));

        var mismatching = RunSnapshotProjector.Project("run-r5", Trace(), Snapshot(new ContainerTransitionReadModel
        {
            LatestTransition = new ContainerTransition(
                "run-r5:container-transition:observation:5", "SettingsRoot", "SettingsRoot", "Display", null,
                "observation:5", "container:Display:local-completeness", ContainerTransitionKind.PREMATURE_RETURN_TO_ACTIVE_PARENT,
                ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED, "historical:4", null),
            EvidenceRef = "historical:4"
        }));
        Assert.Equal("historical:4", mismatching.ContainerEvidenceRef.Value);
        Assert.Contains(mismatching.Diagnostics, d => d.Contains("does not match", StringComparison.Ordinal));
    }

    [Fact]
    public void MultiAssetProjectionSortsIdsAndKeepsAllLogicalRefs()
    {
        var bundle = new TraceCaptureBundle
        {
            CaptureSessionId = "cap-r5",
            Records = [new CaptureRecord { Kind = CaptureRecordKind.Observation, SequenceNumber = 5, FrameId = "frame-5" }],
            Artifacts = [
                new CaptureArtifact { ArtifactId = "z", FrameId = "frame-5", Content = [1] },
                new CaptureArtifact { ArtifactId = "a", FrameId = "frame-5", Content = [2] },
                new CaptureArtifact { ArtifactId = "crop", FrameId = "frame-5", DerivedFromArtifactId = "a", Content = [3] }]
        };
        var result = RunSnapshotProjector.Project("run-r5", Trace(), Snapshot(new ContainerTransitionReadModel { LatestTransition = Transition }),
            EvidenceCatalog.FromBundle(bundle, "run-r5"));
        Assert.Equal("capture:cap-r5:artifact:a", result.ContainerAssetRef.Value);
        Assert.Equal(["capture:cap-r5:artifact:a", "capture:cap-r5:artifact:crop", "capture:cap-r5:artifact:z"], result.ContainerAssetRefs.Value);
        Assert.All(result.ContainerAssetRefs.Value, locator => Assert.DoesNotContain("/", locator));
    }

    [Fact]
    public void ReflectionSurfaceDoesNotExposeRuntimeContainersOrMutableCollections()
    {
        foreach (var type in new[] { typeof(AgentStateSnapshot), typeof(RunSnapshot) })
        foreach (var property in type.GetProperties())
        {
            var propertyType = property.PropertyType;
            Assert.DoesNotContain("ActiveContainerContext", propertyType.FullName ?? propertyType.Name);
            Assert.DoesNotContain("Runtime.Container.Container", propertyType.FullName ?? propertyType.Name);
            var immutable = propertyType.Namespace == "System.Collections.Immutable";
            Assert.True(immutable || !typeof(System.Collections.IList).IsAssignableFrom(propertyType), $"{type.Name}.{property.Name} exposes {propertyType}");
            Assert.True(immutable || !typeof(System.Collections.IDictionary).IsAssignableFrom(propertyType), $"{type.Name}.{property.Name} exposes {propertyType}");
        }
    }

    /// <summary>DriverHost keeps the sole V2 aggregate out of every member: no V2 mutable state, cache, or live Container/authority handle is exposed.</summary>
    [Fact]
    public void DriverHostHoldsNoV2MutableStateCacheOrLiveHandle()
    {
        var driverHostTypes = typeof(DriverHostObservability).Assembly.GetTypes();
        foreach (var type in driverHostTypes)
        foreach (var field in type.GetFields(
                     BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            Assert.NotEqual(typeof(ContainerRuntimeV2State), field.FieldType);
            Assert.DoesNotContain("FastAssessment", field.FieldType.Name, StringComparison.Ordinal);
        }

        foreach (var type in new[] { typeof(AgentStateSnapshot), typeof(RunSnapshot) })
        foreach (var property in type.GetProperties())
        {
            var full = property.PropertyType.FullName ?? property.PropertyType.Name;
            Assert.DoesNotContain("ActiveContainerContext", full);
            Assert.DoesNotContain("UniClaw.Runtime.Container.", full);
            Assert.DoesNotContain("Provider", full);
            Assert.DoesNotContain("UniClaw.Runtime.Recovery.", full);
        }
    }

    /// <summary>r5-style projection shows the V2 physical current (SettingsRoot node/slice/revision) and keeps the execution obligation (Display) separate — Observed != Execution is never merged.</summary>
    [Fact]
    public void R5V2ProjectionShowsV2CurrentAndKeepsExecutionSeparate()
    {
        var source = new ContainerNodeRef("node:settings-main");
        var current = new ContainerNodeRef("node:settings-root");
        var slice = new ContainerSliceRef("slice:5");
        var occurrence = V2Occurrence("occ:5", 5, source, current);
        var context = new ContainerTransitionReadModel
        {
            CurrentObservedLocation = "SettingsRoot",
            ActiveExecutionContainer = "Display",
            ActiveAncestorPath = ["SettingsMain"],
            CurrentNodeRef = current,
            CurrentSliceRef = slice,
            LatestTransitionOccurrence = occurrence,
            EvidenceRevision = new SemanticEvidenceRevision(5),
            IsV2StateAvailable = true,
            FastAssessmentAvailability = ContainerFastAssessmentAvailability.NotRetained,
        };
        var snapshot = RunSnapshotProjector.Project("run-r5", Trace(), Snapshot(context));

        Assert.Equal(SnapshotFieldClassification.DirectPublicProjection, snapshot.CurrentContainerNodeRef.Classification);
        Assert.Equal(current, snapshot.CurrentContainerNodeRef.Value);
        Assert.Equal(SnapshotFieldClassification.DirectPublicProjection, snapshot.CurrentSliceRef.Classification);
        Assert.Equal(slice, snapshot.CurrentSliceRef.Value);
        Assert.Equal(SnapshotFieldClassification.DirectPublicProjection, snapshot.EvidenceRevision.Classification);
        Assert.Equal(new SemanticEvidenceRevision(5), snapshot.EvidenceRevision.Value);

        // Observed semantic current and V2 physical current stay aligned; execution obligation stays Display.
        Assert.Equal("SettingsRoot", snapshot.CurrentSemanticPage.Value);
        Assert.Equal("Display", snapshot.ActiveExecutionContainer.Value);
        Assert.NotEqual(snapshot.CurrentSemanticPage.Value, snapshot.ActiveExecutionContainer.Value);
    }

    /// <summary>Multi-entry snapshots keep path-relative EntryContext refs exact and distinct per entry; the same destination never collapses entries.</summary>
    [Fact]
    public void MultiEntrySnapshotPreservesPathRelativeEntryRefs()
    {
        var destination = new ContainerNodeRef("node:settings-root");
        var firstEntry = new ContainerEntryContext(
            new ContainerNodeRef("node:settings-main"),
            new TransitionOccurrenceRef("occ:enter-main"),
            new ContainerRelationRef("relation:enter-main"));
        var secondEntry = new ContainerEntryContext(
            new ContainerNodeRef("node:display-root"),
            new TransitionOccurrenceRef("occ:return-verified"));
        var first = RunSnapshotProjector.Project("run-r5", Trace(), Snapshot(V2Context(destination, 1, firstEntry)));
        var second = RunSnapshotProjector.Project("run-r5", Trace(), Snapshot(V2Context(destination, 2, secondEntry)));

        Assert.Equal(destination, first.CurrentContainerNodeRef.Value);
        Assert.Equal(destination, second.CurrentContainerNodeRef.Value);
        Assert.Equal(firstEntry.SourceNodeRef, first.EntrySourceNodeRef.Value);
        Assert.Equal(firstEntry.EntryTransitionOccurrenceRef, first.EntryTransitionOccurrenceRef.Value);
        Assert.Equal(firstEntry.EntryRelationRef, first.EntryRelationRef.Value);
        Assert.Equal(secondEntry.SourceNodeRef, second.EntrySourceNodeRef.Value);
        Assert.Equal(secondEntry.EntryTransitionOccurrenceRef, second.EntryTransitionOccurrenceRef.Value);
        Assert.Null(second.EntryRelationRef.Value);
        Assert.NotEqual(first.EntrySourceNodeRef.Value, second.EntrySourceNodeRef.Value);
        Assert.NotEqual(first.EntryTransitionOccurrenceRef.Value, second.EntryTransitionOccurrenceRef.Value);
    }

    /// <summary>A root entry (V2 current present, no EntryContext) projects known-null entry refs as direct values; an older/manual snapshot without any V2 state stays NotCurrentlyAvailable.</summary>
    [Fact]
    public void RootEntryKnownNullIsDirectWhileNoV2StateIsUnavailable()
    {
        var current = new ContainerNodeRef("node:settings-root");
        var slice = new ContainerSliceRef("slice:5");
        var root = RunSnapshotProjector.Project("run-r5", Trace(), Snapshot(V2Context(current, 5, entryContext: null)));

        Assert.Equal(SnapshotFieldClassification.DirectPublicProjection, root.CurrentContainerNodeRef.Classification);
        Assert.Equal(SnapshotFieldClassification.DirectPublicProjection, root.CurrentSliceRef.Classification);
        Assert.Equal(SnapshotFieldClassification.DirectPublicProjection, root.EvidenceRevision.Classification);
        // Entry fields are known-null direct values — never reported as whole-V2 unavailable.
        Assert.Equal(SnapshotFieldClassification.DirectPublicProjection, root.EntrySourceNodeRef.Classification);
        Assert.Null(root.EntrySourceNodeRef.Value);
        Assert.Equal(SnapshotFieldClassification.DirectPublicProjection, root.EntryTransitionOccurrenceRef.Classification);
        Assert.Null(root.EntryTransitionOccurrenceRef.Value);
        Assert.Equal(SnapshotFieldClassification.DirectPublicProjection, root.EntryRelationRef.Classification);
        Assert.Null(root.EntryRelationRef.Value);

        var older = RunSnapshotProjector.Project("run-r5", Trace(), new AgentStateSnapshot
        {
            RunId = "run-r5",
            ContainerContext = new ContainerTransitionReadModel
            {
                CurrentObservedLocation = "Root", ActiveExecutionContainer = "Root", ActiveAncestorPath = []
            }
        });
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, older.CurrentContainerNodeRef.Classification);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, older.CurrentSliceRef.Classification);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, older.EvidenceRevision.Classification);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, older.EntrySourceNodeRef.Classification);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, older.EntryTransitionOccurrenceRef.Classification);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, older.EntryRelationRef.Classification);
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, older.LatestTransitionOccurrence.Classification);
    }

    /// <summary>The latest V2 occurrence and the latest legacy transition are projected separately with exact refs/revision and distinct classification — legacy never becomes the occurrence authority.</summary>
    [Fact]
    public void LatestV2OccurrenceAndLegacyTransitionProjectedSeparatelyWithDistinctClassification()
    {
        var source = new ContainerNodeRef("node:settings-main");
        var destination = new ContainerNodeRef("node:settings-root");
        var occurrence = V2Occurrence("occ:2", 2, source, destination);
        var context = new ContainerTransitionReadModel
        {
            CurrentObservedLocation = "SettingsRoot",
            ActiveExecutionContainer = "Display",
            ActiveAncestorPath = ["SettingsMain"],
            LatestTransition = Transition,
            LatestTransitionOccurrence = occurrence,
            EvidenceRevision = new SemanticEvidenceRevision(2),
            IsV2StateAvailable = true,
            FastAssessmentAvailability = ContainerFastAssessmentAvailability.NotRetained,
        };
        var snapshot = RunSnapshotProjector.Project("run-r5", Trace(), Snapshot(context));

        Assert.Equal(SnapshotFieldClassification.DerivedReadModel, snapshot.LatestObservedTransition.Classification);
        Assert.Equal("run-r5:container-transition:observation:5", snapshot.LatestObservedTransition.Value!.TransitionRef);
        Assert.Equal(SnapshotFieldClassification.DirectPublicProjection, snapshot.LatestTransitionOccurrence.Classification);
        Assert.Equal(new TransitionOccurrenceRef("occ:2"), snapshot.LatestTransitionOccurrence.Value!.OccurrenceRef);
        Assert.Equal(new SemanticEvidenceRevision(2), snapshot.LatestTransitionOccurrence.Value!.EvidenceRevision);
        Assert.NotEqual(snapshot.LatestObservedTransition.Value.TransitionRef, snapshot.LatestTransitionOccurrence.Value.OccurrenceRef.Value);
    }

    /// <summary>Fast assessment availability is explicitly NotCurrentlyAvailable with the no-latest-slot explanation — never inferred from Graph/Belief/legacy.</summary>
    [Fact]
    public void FastAssessmentAvailabilityIsExplicitlyNotCurrentlyAvailableWithNoLatestSlotExplanation()
    {
        var current = new ContainerNodeRef("node:settings-root");
        var withV2 = RunSnapshotProjector.Project("run-r5", Trace(), Snapshot(V2Context(current, 5, entryContext: null)));
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, withV2.FastAssessmentAvailability.Classification);
        Assert.True(withV2.FastAssessmentAvailability.IsPartial);
        Assert.Equal(ContainerFastAssessmentAvailability.NotRetained, withV2.FastAssessmentAvailability.Value);
        Assert.Contains("no mutable latest Fast assessment slot", withV2.FastAssessmentAvailability.TruthSource, StringComparison.Ordinal);
        Assert.Contains("never inferred from Graph/Belief/legacy", withV2.FastAssessmentAvailability.TruthSource, StringComparison.Ordinal);

        var older = RunSnapshotProjector.Project("run-r5", Trace(), new AgentStateSnapshot());
        Assert.Equal(SnapshotFieldClassification.NotCurrentlyAvailable, older.FastAssessmentAvailability.Classification);
        Assert.Equal(ContainerFastAssessmentAvailability.Unavailable, older.FastAssessmentAvailability.Value);
    }

    /// <summary>Reading a real Agent snapshot never mutates the Agent: V2 state ref, active context ref, Belief, trace length, and BranchProgress stay identical.</summary>
    [Fact]
    public async Task ReadingRealAgentSnapshotNeverMutatesAgentState()
    {
        var harness = UniClaw.Runtime.Tests.Scenario.ScenarioHarness.Create("happy");
        await harness.RunAsync();
        var agent = harness.Agent;

        var stateField = typeof(RuntimeAgent).GetField("_containerRuntimeV2State", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Agent._containerRuntimeV2State field is missing.");
        var activeContextField = typeof(RuntimeAgent).GetField("_activeContainerContext", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Agent._activeContainerContext field is missing.");

        var stateBefore = stateField.GetValue(agent);
        var activeContextBefore = activeContextField.GetValue(agent);
        var beliefBefore = agent.Belief;
        var traceBefore = agent.Trace.Count;
        var progressBefore = agent.BranchProgress.Count;

        var snapshot = AgentStateSnapshot.From(agent);
        var context = agent.ContainerContext;
        var first = RunSnapshotProjector.Project("run-1", new TraceRun { RunId = "run-1", TraceRunId = "trace" }, snapshot);
        var second = RunSnapshotProjector.Project("run-1", new TraceRun { RunId = "run-1", TraceRunId = "trace" }, snapshot);

        // The projection is an exact copy of the Agent's ContainerContext V2 fields.
        Assert.Equal(context.CurrentNodeRef, first.CurrentContainerNodeRef.Value);
        Assert.Equal(context.CurrentSliceRef, first.CurrentSliceRef.Value);
        Assert.Equal(context.EntrySourceNodeRef, first.EntrySourceNodeRef.Value);
        Assert.Equal(context.EntryTransitionOccurrenceRef, first.EntryTransitionOccurrenceRef.Value);
        Assert.Equal(context.EntryRelationRef, first.EntryRelationRef.Value);
        Assert.Equal(context.LatestTransitionOccurrence, first.LatestTransitionOccurrence.Value);
        Assert.Equal(context.EvidenceRevision, first.EvidenceRevision.Value);
        Assert.Equal(context.FastAssessmentAvailability, first.FastAssessmentAvailability.Value);
        Assert.Equal(context.ActiveExecutionContainer, first.ActiveExecutionContainer.Value);

        // Reading never mutates the Agent: V2 state ref, active context ref, trace, BranchProgress.
        Assert.Same(stateBefore, stateField.GetValue(agent));
        Assert.Same(activeContextBefore, activeContextField.GetValue(agent));
        Assert.Equal(beliefBefore, agent.Belief);
        Assert.Equal(traceBefore, agent.Trace.Count);
        Assert.Equal(progressBefore, agent.BranchProgress.Count);

        // Deterministic: two projections from the same snapshot agree on every V2 field
        // and on diagnostics; ImmutableArray record equality is reference-based, so
        // compare field-wise (as the Runtime read-seam tests do).
        Assert.Equal(first.CurrentContainerNodeRef, second.CurrentContainerNodeRef);
        Assert.Equal(first.CurrentSliceRef, second.CurrentSliceRef);
        Assert.Equal(first.EntrySourceNodeRef, second.EntrySourceNodeRef);
        Assert.Equal(first.EntryTransitionOccurrenceRef, second.EntryTransitionOccurrenceRef);
        Assert.Equal(first.EntryRelationRef, second.EntryRelationRef);
        Assert.Equal(first.LatestTransitionOccurrence, second.LatestTransitionOccurrence);
        Assert.Equal(first.EvidenceRevision, second.EvidenceRevision);
        Assert.Equal(first.FastAssessmentAvailability, second.FastAssessmentAvailability);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
    }

    private static ContainerTransitionReadModel V2Context(
        ContainerNodeRef current,
        long revision,
        ContainerEntryContext? entryContext)
        => new()
        {
            CurrentObservedLocation = "SettingsRoot",
            ActiveExecutionContainer = "Display",
            ActiveAncestorPath = ["SettingsMain"],
            CurrentNodeRef = current,
            CurrentSliceRef = new ContainerSliceRef("slice:" + revision),
            EntrySourceNodeRef = entryContext?.SourceNodeRef,
            EntryTransitionOccurrenceRef = entryContext?.EntryTransitionOccurrenceRef,
            EntryRelationRef = entryContext?.EntryRelationRef,
            LatestTransitionOccurrence = V2Occurrence("occ:" + revision, revision, new ContainerNodeRef("node:settings-main"), current),
            EvidenceRevision = new SemanticEvidenceRevision(revision),
            IsV2StateAvailable = true,
            FastAssessmentAvailability = ContainerFastAssessmentAvailability.NotRetained,
        };

    private static ContainerTransitionOccurrence V2Occurrence(
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

    private sealed class SnapshotEnvironment : IEnvironment
    {
        private int _observations;
        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _observations++;
            return Task.FromResult(new Observation([new ObservedElement("Root", null, 0, null, "text")], "test.app", _observations));
        }

        public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
            => Task.FromResult(new ActionResult(ActionResultOutcome.Dispatched, "snapshot", "accepted"));
    }
}
