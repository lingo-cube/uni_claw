using System.Collections.Frozen;
using System.Collections.Immutable;
using UniClaw.Kernel.Run;
using Xunit;

namespace UniClaw.Kernel.Tests.Run;

public sealed class ContractOwnershipTests
{
    [Fact]
    public void AdmitContract_SnapshotsCallerCollectionsAndPreservesComparers()
    {
        var scope = new HashSet<string> { "ui.node.*" };
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "tap" };
        var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "toggle:NFC" };
        var proofCriteria = new List<string> { "switch-state-checked" };
        var contract = Contract(scope, allowed, forbidden, proofCriteria);
        var run = new RunModel();

        var admission = run.AdmitContract(contract);

        Assert.True(admission.Accepted);
        var view = run.View!;
        var runId = run.RunId;

        scope.Clear();
        scope.Add("changed.scope");
        allowed.Clear();
        allowed.Add("swipe");
        forbidden.Clear();
        forbidden.Add("other");
        proofCriteria[0] = "changed-criterion";
        proofCriteria.Add("second-criterion");

        Assert.Contains("ui.node.*", view.Scope);
        Assert.DoesNotContain("UI.NODE.*", view.Scope);
        Assert.DoesNotContain("changed.scope", view.Scope);
        Assert.True(view.AllowedEffects.Contains("tap"));
        Assert.False(view.AllowedEffects.Contains("TAP"));
        Assert.False(view.AllowedEffects.Contains("swipe"));
        Assert.True(view.ForbiddenEffects.Contains("TOGGLE:nfc"));
        Assert.False(view.ForbiddenEffects.Contains("other"));
        Assert.Equal(new[] { "switch-state-checked" }, view.ProofCriteria);
        Assert.Equal(runId, run.RunId);

        var readmission = run.AdmitContract(contract);
        Assert.True(readmission.Accepted);
        Assert.Same(view, run.View);
        Assert.Equal(runId, run.RunId);
    }

    [Fact]
    public void PublishedCollectionsDoNotExposeMutableCallerStorage()
    {
        var run = new RunModel();
        var contract = Contract(
            new HashSet<string> { "ui.node.*" },
            new HashSet<string> { "tap" },
            new HashSet<string> { "toggle" },
            new List<string> { "switch-state-checked" });

        Assert.True(run.AdmitContract(contract).Accepted);
        var view = run.View!;

        AssertReadOnly(view.Scope, collection => collection.Add("changed.scope"));
        AssertReadOnly(view.AllowedEffects, collection => collection.Clear());
        AssertReadOnly(view.ForbiddenEffects, collection => collection.Remove("toggle"));
        AssertReadOnly(view.ProofCriteria, collection => collection.Add("changed-criterion"));
        Assert.Equal(new[] { "switch-state-checked" }, view.ProofCriteria);
    }

    [Fact]
    public void AdmitContract_PreservesAlreadyFrozenSetSemantics()
    {
        var scope = new[] { "ui.node.*" }.ToFrozenSet(StringComparer.Ordinal);
        var allowed = new[] { "tap" }.ToFrozenSet(StringComparer.Ordinal);
        var forbidden = new[] { "toggle:NFC" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        var run = new RunModel();

        Assert.True(run.AdmitContract(Contract(
            scope, allowed, forbidden, new[] { "switch-state-checked" })).Accepted);

        Assert.Contains("ui.node.*", run.View!.Scope);
        Assert.DoesNotContain("UI.NODE.*", run.View.Scope);
        Assert.Contains("tap", run.View.AllowedEffects);
        Assert.DoesNotContain("TAP", run.View.AllowedEffects);
        Assert.Contains("TOGGLE:nfc", run.View.ForbiddenEffects);
        Assert.DoesNotContain("other", run.View.ForbiddenEffects);
    }

    [Fact]
    public void AdmitContract_PreservesImmutableSetAndSortedSetComparers()
    {
        var allowed = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "tap");
        var forbidden = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "toggle:NFC" };
        var run = new RunModel();
        Assert.True(run.AdmitContract(Contract(
            ImmutableSortedSet.Create(StringComparer.OrdinalIgnoreCase, "ui.node.*"),
            allowed, forbidden, new[] { "switch-state-checked" })).Accepted);

        forbidden.Clear();

        Assert.True(run.View!.Scope.Contains("UI.NODE.*"));
        Assert.True(run.View.AllowedEffects.Contains("TAP"));
        Assert.True(run.View.ForbiddenEffects.Contains("TOGGLE:nfc"));
        AssertReadOnly(run.View.ForbiddenEffects, collection => collection.Clear());
    }

    private static void AssertReadOnly<T>(IEnumerable<T> values, Action<ICollection<T>> mutate)
    {
        if (values is not ICollection<T> collection) return;

        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutate(collection));
    }

    private static ExecutionContract Contract(
        IReadOnlySet<string> scope,
        IReadOnlySet<string> allowed,
        IReadOnlySet<string> forbidden,
        IReadOnlyList<string> proofCriteria) =>
        new(
            Version: "v1",
            Objective: "confirm switch state",
            Scope: scope,
            AllowedEffects: allowed,
            ForbiddenEffects: forbidden,
            ProofCriteria: proofCriteria,
            TaskInstanceId: "task-ownership",
            ProductSessionId: "session-ownership",
            ContractRef: "contract-ownership");
}
