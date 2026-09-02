using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

public sealed class ContainerTransitionClassifierTests
{
    public static IEnumerable<object[]> AllKinds()
    {
        yield return [new ContainerTransitionClassificationInput
        {
            RunId = "kinds-same",
            FromObservedLocation = "Display", ToObservedLocation = "Display",
            ActiveExecutionContainer = "Display", FreshObservationRef = "observation:1",
        }, ContainerTransitionKind.SAME_CONTAINER, ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED];
        yield return [new ContainerTransitionClassificationInput
        {
            RunId = "kinds-child",
            FromObservedLocation = "SettingsRoot", ToObservedLocation = "Display",
            ActiveExecutionContainer = "SettingsRoot", ActiveParentAtObservation = "SettingsRoot",
            IsAuthorizedChildEntry = true, FreshObservationRef = "observation:2",
        }, ContainerTransitionKind.ENTER_CHILD, ContainerTransitionDisposition.OBSERVED_AND_EXECUTION_ADVANCED];
        yield return [new ContainerTransitionClassificationInput
        {
            RunId = "kinds-verified-return",
            FromObservedLocation = "Display", ToObservedLocation = "SettingsRoot",
            ActiveExecutionContainer = "Display", ActiveParentAtObservation = "SettingsRoot",
            IsVerifiedReturn = true, FreshObservationRef = "observation:3",
        }, ContainerTransitionKind.VERIFIED_RETURN_TO_ACTIVE_PARENT, ContainerTransitionDisposition.OBSERVED_AND_EXECUTION_RESUMED];
        yield return [new ContainerTransitionClassificationInput
        {
            RunId = "kinds-premature-return",
            FromObservedLocation = "Display", ToObservedLocation = "SettingsRoot",
            ActiveExecutionContainer = "Display", ActiveParentAtObservation = "SettingsRoot",
            CompletenessRef = "container:Display:incomplete", FreshObservationRef = "observation:4",
        }, ContainerTransitionKind.PREMATURE_RETURN_TO_ACTIVE_PARENT, ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED];
        yield return [new ContainerTransitionClassificationInput
        {
            RunId = "kinds-non-parent",
            FromObservedLocation = "Display", ToObservedLocation = "Network",
            ActiveExecutionContainer = "Display", ActiveParentAtObservation = "SettingsRoot",
            FreshObservationRef = "observation:5",
        }, ContainerTransitionKind.KNOWN_NON_PARENT_TRANSITION, ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED];
        yield return [new ContainerTransitionClassificationInput
        {
            RunId = "kinds-external",
            FromObservedLocation = "Display", ToObservedLocation = "Browser",
            ActiveExecutionContainer = "Display", IsExternalExit = true, FreshObservationRef = "observation:6",
        }, ContainerTransitionKind.EXTERNAL_EXIT, ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED];
        yield return [new ContainerTransitionClassificationInput
        {
            RunId = "kinds-unknown",
            FromObservedLocation = "Display", ToObservedLocation = null,
            ActiveExecutionContainer = "Display", FreshObservationRef = "observation:7",
        }, ContainerTransitionKind.UNKNOWN_TRANSITION, ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED];
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void Classify_CoversClosedKindsAndDispositions(
        ContainerTransitionClassificationInput input,
        ContainerTransitionKind expectedKind,
        ContainerTransitionDisposition expectedDisposition)
    {
        var first = ContainerTransitionClassifier.Classify(input);
        var second = ContainerTransitionClassifier.Classify(input);

        Assert.Equal(expectedKind, first.Kind);
        Assert.Equal(expectedDisposition, first.Disposition);
        Assert.Equal(first, second);
        Assert.Equal(input.RunId + ":container-transition:" + input.FreshObservationRef, first.TransitionRef);
    }

    [Fact]
    public void PrematureReturn_PreservesExecutionAndIncompleteReference()
    {
        var transition = ContainerTransitionClassifier.Classify(new ContainerTransitionClassificationInput
        {
            RunId = "r5",
            FromObservedLocation = "Display",
            ToObservedLocation = "SettingsRoot",
            ActiveExecutionContainer = "Display",
            ActiveParentAtObservation = "SettingsRoot",
            CompletenessRef = "container:Display:incomplete",
            FreshObservationRef = "observation:28",
        });

        Assert.Equal("Display", transition.FromObservedLocation);
        Assert.Equal("SettingsRoot", transition.ToObservedLocation);
        Assert.Equal("Display", transition.ActiveExecutionContainer);
        Assert.Equal("SettingsRoot", transition.ActiveParentAtObservation);
        Assert.Equal(ContainerTransitionKind.PREMATURE_RETURN_TO_ACTIVE_PARENT, transition.Kind);
        Assert.Equal(ContainerTransitionDisposition.OBSERVED_EXECUTION_PRESERVED, transition.Disposition);
        Assert.Equal("container:Display:incomplete", transition.CompletenessRef);
    }

    [Fact]
    public void ReadProjection_MissingAssetIsExplicitAndDoesNotParseReason()
    {
        var transition = ContainerTransitionClassifier.Classify(new ContainerTransitionClassificationInput
        {
            RunId = "r5", FromObservedLocation = "Display", ToObservedLocation = "SettingsRoot",
            ActiveExecutionContainer = "Display", ActiveParentAtObservation = "SettingsRoot",
            FreshObservationRef = "observation:28", EvidenceRef = "evidence:r5:28",
        });
        var record = new DecisionRecord("r5")
        {
            Reason = "unrelated free-form text that must not be interpreted",
            ContainerTransition = transition,
        };
        var projection = ContainerTransitionReadModel.From("SettingsRoot", "Display", [], [record]);

        Assert.Same(transition, projection.LatestTransition);
        Assert.Equal("evidence:r5:28", projection.EvidenceRef);
        Assert.Null(projection.AssetRef);
        Assert.True(projection.IsAssetMissing);
        Assert.Contains("MISSING_ASSET", projection.Diagnostics);
    }

    [Fact]
    public void ReadProjection_DistinguishesUnavailablePathFromKnownEmptyPath()
    {
        var transition = ContainerTransitionClassifier.Classify(new ContainerTransitionClassificationInput
        {
            RunId = "path",
            FromObservedLocation = "SettingsRoot",
            ToObservedLocation = "SettingsRoot",
            ActiveExecutionContainer = "SettingsRoot",
            FreshObservationRef = "observation:1",
        });

        var unavailable = ContainerTransitionReadModel.From("SettingsRoot", "SettingsRoot", null, [transition]);
        var knownEmpty = ContainerTransitionReadModel.From("SettingsRoot", "SettingsRoot", [], [transition]);

        Assert.True(unavailable.ActiveAncestorPath.IsDefault);
        Assert.Contains(unavailable.Diagnostics, d => d.Contains("path unavailable", StringComparison.OrdinalIgnoreCase));
        Assert.False(knownEmpty.ActiveAncestorPath.IsDefault);
        Assert.Empty(knownEmpty.ActiveAncestorPath);
        Assert.DoesNotContain(knownEmpty.Diagnostics, d => d.Contains("path unavailable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Prepare_RejectsBlankRunIdFailClosed()
    {
        AssertRejected(new ContainerTransitionClassificationInput
        {
            RunId = " ",
            ActiveExecutionContainer = "Display",
            FreshObservationRef = "observation:invalid-run",
        });
    }

    [Fact]
    public void Prepare_RejectsBlankFreshObservationFailClosed()
    {
        AssertRejected(new ContainerTransitionClassificationInput
        {
            RunId = "run:invalid-observation",
            ActiveExecutionContainer = "Display",
            FreshObservationRef = " ",
        });
    }

    [Fact]
    public void Prepare_RejectsBlankActiveExecutionContainerFailClosed()
    {
        AssertRejected(new ContainerTransitionClassificationInput
        {
            RunId = "run:invalid-active-container",
            ActiveExecutionContainer = " ",
            FreshObservationRef = "observation:invalid-active-container",
        });
    }

    [Fact]
    public void Prepare_RejectsContradictoryPolicyFlagsFailClosed()
    {
        AssertRejected(new ContainerTransitionClassificationInput
        {
            RunId = "run:contradictory-policy",
            ActiveExecutionContainer = "Display",
            FreshObservationRef = "observation:contradictory-policy",
            IsVerifiedReturn = true,
            IsExternalExit = true,
        });
    }

    [Fact]
    public void Prepare_RejectsVerifiedReturnWithoutExactActiveParentFailClosed()
    {
        AssertRejected(new ContainerTransitionClassificationInput
        {
            RunId = "run:verified-parent",
            ToObservedLocation = "SettingsRoot",
            ActiveExecutionContainer = "Display",
            ActiveParentAtObservation = "OtherParent",
            FreshObservationRef = "observation:verified-parent",
            IsVerifiedReturn = true,
        });
    }

    [Fact]
    public void Prepare_RejectsAuthorizedChildWithoutDistinctKnownDestinationFailClosed()
    {
        AssertRejected(new ContainerTransitionClassificationInput
        {
            RunId = "run:authorized-child",
            ToObservedLocation = "Display",
            ActiveExecutionContainer = "Display",
            FreshObservationRef = "observation:authorized-child",
            IsAuthorizedChildEntry = true,
        });
    }

    private static void AssertRejected(ContainerTransitionClassificationInput input)
    {
        var preparation = ContainerTransitionClassifier.Prepare(input);

        Assert.False(preparation.CanCommit);
        Assert.Equal(ContainerTransitionDisposition.NO_COMMIT_FAIL_CLOSED, preparation.Transition.Disposition);
    }
}
