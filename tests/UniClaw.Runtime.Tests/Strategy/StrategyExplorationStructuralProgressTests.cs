using System.Collections.Immutable;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Strategy;

public sealed class StrategyExplorationStructuralProgressTests
{
    private static AcceptedExplorationRunContext Context()
        => new("run-1", new ExplorationExecutionSemantics(
            "strategy-1", "intent-1", ExplorationRule.ExpandContainer, ExplorationRule.RecordOnly,
            ExplorationDepthSemantics.BoundedRecursive, ExplorationBoundaryDisposition.FailClosed, 2));

    private static ImmutableArray<ExplorationScopeEvidence> Scopes()
        => ImmutableArray.Create(new ExplorationScopeEvidence(
            new BranchProgressEvidence("scope", ImmutableDictionary<string, long>.Empty
                .Add("a", 10).Add("b", 10), ImmutableDictionary<string, long>.Empty.Add("a", 20))));

    private static StrategyExecutionEvidenceView View(
        IReadOnlyList<StrategyStructuralProgressFact>? facts = null,
        string runId = "run-1",
        string intent = "intent-1")
        => new(runId, intent, 7, 3, "belief", 2, facts, ["coverage-b"], ["contradiction-a"], ["trace-a"], "trace-digest");

    [Fact]
    public void StructuralEvidenceChangesOnlyCorrelation_NotAccounting()
    {
        var context = Context();
        var absent = ExplorationLedgerCompiler.Compile(context, Scopes());
        var empty = ExplorationLedgerCompiler.Compile(context, Scopes(), View(Array.Empty<StrategyStructuralProgressFact>()));
        var valid = ExplorationLedgerCompiler.Compile(context, Scopes(), View([
            new StrategyStructuralProgressFact(StrategyStructuralProgressKind.BoundedScopeEntered, 1, "scope-entry")
        ]));

        Assert.Equal(absent.Scopes, empty.Scopes);
        Assert.Equal(absent.Scopes, valid.Scopes);
        Assert.Equal(absent.Scopes.Select(s => (s.Discovered, s.Visited, s.Pending, s.Unresolved, s.UnknownFrontier, s.SourceObservationSequence)),
            valid.Scopes.Select(s => (s.Discovered, s.Visited, s.Pending, s.Unresolved, s.UnknownFrontier, s.SourceObservationSequence)));
        Assert.NotEqual(absent.LedgerDigest, empty.LedgerDigest);
        Assert.NotEqual(empty.LedgerDigest, valid.LedgerDigest);
    }

    [Fact]
    public void StructuralFactsAreCanonicalAcrossInputOrder()
    {
        var first = View([
            new StrategyStructuralProgressFact(StrategyStructuralProgressKind.BoundedScopeEntered, 1, "b"),
            new StrategyStructuralProgressFact(StrategyStructuralProgressKind.ChildObligationDiscovered, 1, "a")]);
        var second = View([
            new StrategyStructuralProgressFact(StrategyStructuralProgressKind.ChildObligationDiscovered, 1, "a"),
            new StrategyStructuralProgressFact(StrategyStructuralProgressKind.BoundedScopeEntered, 1, "b")]);

        var left = ExplorationLedgerCompiler.Compile(Context(), Scopes(), first);
        var right = ExplorationLedgerCompiler.Compile(Context(), Scopes(), second);
        Assert.Equal(left.LedgerDigest, right.LedgerDigest);
        Assert.Equal(left, right);
        Assert.Equal(left.StructuralCorrelationDigestMaterial, right.StructuralCorrelationDigestMaterial);
        Assert.NotEqual(left.StructuralCorrelationMaterial, right.StructuralCorrelationMaterial);
    }

    [Fact]
    public void StructuralEvidenceBindingAndRevisionAreFailClosed()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ExplorationLedgerCompiler.Compile(Context(), Scopes(), View(runId: "other-run")));
        Assert.Throws<InvalidOperationException>(() =>
            ExplorationLedgerCompiler.Compile(Context(), Scopes(), View(intent: "other-intent")));
        var nonMonotonic = View([
            new StrategyStructuralProgressFact(StrategyStructuralProgressKind.BoundedScopeEntered, 2, "two"),
            new StrategyStructuralProgressFact(StrategyStructuralProgressKind.ContinuityVerified, 1, "one")]);
        Assert.Throws<InvalidOperationException>(() =>
            ExplorationLedgerCompiler.Compile(Context(), Scopes(), nonMonotonic));
    }

    [Fact]
    public void ExistingEvidenceConstructorsRejectInvalidStructuralFacts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StrategyStructuralProgressFact((StrategyStructuralProgressKind)999, 1, "ref"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StrategyStructuralProgressFact(StrategyStructuralProgressKind.BoundedScopeEntered, -1, "ref"));
        Assert.Throws<ArgumentException>(() => new StrategyStructuralProgressFact(StrategyStructuralProgressKind.BoundedScopeEntered, 1, " "));
        Assert.Throws<ArgumentException>(() => new StrategyExecutionEvidenceView("run-1", "intent-1", 1, 1, "belief", 1,
            [new StrategyStructuralProgressFact(StrategyStructuralProgressKind.BoundedScopeEntered, 2, "ahead")], [], [], [], "trace"));
    }

    [Fact]
    public void DifferentFactKindRevisionAndReferenceChangeDigest()
    {
        var baseline = ExplorationLedgerCompiler.Compile(Context(), Scopes(), View([
            new StrategyStructuralProgressFact(StrategyStructuralProgressKind.BoundedScopeEntered, 1, "ref")])).LedgerDigest;
        var kind = ExplorationLedgerCompiler.Compile(Context(), Scopes(), View([
            new StrategyStructuralProgressFact(StrategyStructuralProgressKind.ChildObligationDiscovered, 1, "ref")])).LedgerDigest;
        var revision = ExplorationLedgerCompiler.Compile(Context(), Scopes(), View([
            new StrategyStructuralProgressFact(StrategyStructuralProgressKind.BoundedScopeEntered, 2, "ref")])).LedgerDigest;
        var reference = ExplorationLedgerCompiler.Compile(Context(), Scopes(), View([
            new StrategyStructuralProgressFact(StrategyStructuralProgressKind.BoundedScopeEntered, 1, "other")])).LedgerDigest;
        Assert.All(new[] { kind, revision, reference }, digest => Assert.NotEqual(baseline, digest));
    }
}
