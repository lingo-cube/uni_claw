using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
/// Deterministic contract tests for the Exploration Ledger projection and
/// depth-control derivation (OpenSpec runtime-exploration-ledger-and-depth-control).
/// Tests capability: unified evidence-derived accounting, Visited != Clicked,
/// deterministic digest, closed rule derivation, bounded depth semantics, and
/// zero authority members on ledger types.
/// </summary>
public sealed class ExplorationLedgerTests
{
    private static BranchProgressEvidence Progress(
        string parent = "scope://root",
        string[]? approved = null,
        string[]? completed = null,
        string[]? authorized = null)
    {
        approved ??= Array.Empty<string>();
        completed ??= Array.Empty<string>();
        authorized ??= Array.Empty<string>();
        return new BranchProgressEvidence(
            parent,
            approved.ToImmutableDictionary(k => k, _ => 10L),
            completed.ToImmutableDictionary(k => k, _ => 20L),
            authorized.ToImmutableDictionary(k => k, _ => 15L));
    }

    private static ExplorationScopeEvidence Evidence(
        BranchProgressEvidence progress,
        string[]? unresolved = null,
        string[]? recordOnly = null,
        string[]? frontier = null,
        string[]? revisit = null)
        => new(progress, unresolved, (recordOnly ?? []).Select(identity => new KeyValuePair<string, long>(identity, progress.ApprovedSiblingEvidence[identity])), frontier, revisit);

    [Fact]
    public void CompileScope_ReportsUnifiedAccountingFromEvidence()
    {
        var progress = Progress(
            approved: new[] { "a", "b", "c" },
            completed: new[] { "a" },
            authorized: new[] { "a", "b" });

        var scope = ExplorationLedgerCompiler.CompileScope(Evidence(progress));

        Assert.Equal(3, scope.Discovered);
        Assert.Equal(1, scope.Visited);           // only completed-with-evidence
        Assert.Equal(2, scope.Pending);           // b/c remain unsatisfied
        Assert.Equal(0, scope.Unresolved);
        Assert.Equal(0, scope.UnknownFrontier);   // c: discovered-but-unauthorized remainder
    }

    [Fact]
    public void Visited_RequiresCompletionEvidence_NotAuthorizationOrClick()
    {
        // Authorized (= dispatched) but no completion evidence → pending, not visited.
        var progress = Progress(
            approved: new[] { "a" },
            completed: Array.Empty<string>(),
            authorized: new[] { "a" });

        var scope = ExplorationLedgerCompiler.CompileScope(Evidence(progress));

        Assert.Equal(1, scope.Discovered);
        Assert.Equal(0, scope.Visited);
        Assert.Equal(1, scope.Pending);
    }

    [Fact]
    public void UnclassifiableNode_FailsClosedToUnresolved_NeverGuessed()
    {
        var progress = Progress(approved: new[] { "a" });

        var scope = ExplorationLedgerCompiler.CompileScope(Evidence(progress, unresolved: ["a"]));

        Assert.Equal(1, scope.Unresolved);
        // No rule is inferred: the ledger only records the unresolved state.
    }

    [Fact]
    public void BoundedRecordBoundary_RecordsUnknownFrontier_AndCountsRecordVisited()
    {
        // Spec R3 ("Record-only node visited by observation": a RecordOnly node
        // recorded in a fresh accepted observation counts as visited without any
        // dispatch) + R4 ("Bounded-record boundary is recorded, not failed"):
        // the boundary container is BOTH record-visited and unknown frontier —
        // overlapping annotations, not exclusive dispositions.
        var progress = Progress(approved: new[] { "a", "deep-container" }, completed: new[] { "a" });

        var scope = ExplorationLedgerCompiler.CompileScope(Evidence(progress, recordOnly: ["deep-container"], frontier: ["deep-container"]));

        Assert.Equal(1, scope.UnknownFrontier);
        Assert.Equal(2, scope.Visited);           // 'a' completed + 'deep-container' record-visited (R3)
        Assert.Equal(2, scope.Discovered);
    }

    [Fact]
    public void IdenticalEvidence_ProducesIdenticalLedgerAndDigest()
    {
        var progress = Progress(approved: new[] { "a", "b" }, completed: new[] { "a" });
        var context = new AcceptedExplorationRunContext("run-1", new ExplorationExecutionSemantics(
            "strategy-1", "intent-1", ExplorationRule.ExpandContainer, ExplorationRule.RecordOnly,
            ExplorationDepthSemantics.BoundedRecursive, ExplorationBoundaryDisposition.FailClosed, 2));
        var scopes = ImmutableArray.Create(Evidence(progress));
        var first = ExplorationLedgerCompiler.Compile(context, scopes);
        var second = ExplorationLedgerCompiler.Compile(context, scopes);

        Assert.Equal(first.LedgerDigest, second.LedgerDigest);
        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentEvidence_ProducesDifferentDigest()
    {
        var context = new AcceptedExplorationRunContext("run-1", new ExplorationExecutionSemantics(
            "strategy-1", "intent-1", ExplorationRule.ExpandContainer, ExplorationRule.RecordOnly,
            ExplorationDepthSemantics.BoundedRecursive, ExplorationBoundaryDisposition.FailClosed, 2));
        var a = ExplorationLedgerCompiler.Compile(context, ImmutableArray.Create(
            Evidence(Progress(approved: new[] { "a" }, completed: new[] { "a" }))));
        var b = ExplorationLedgerCompiler.Compile(context, ImmutableArray.Create(
            Evidence(Progress(approved: new[] { "a", "deep-container" }, completed: new[] { "a" }), recordOnly: ["deep-container"], frontier: ["deep-container"])));

        Assert.NotEqual(a.LedgerDigest, b.LedgerDigest);
    }

    [Theory]
    [InlineData(ExplorationIntent.ExhaustiveWithinScope)]
    [InlineData(ExplorationIntent.InspectMatchesWithinScope)]
    public void DeriveRules_ClosedVocabulary_ContainersExpand_LeavesRecord(ExplorationIntent intent)
    {
        var (container, leaf) = ExplorationLedgerCompiler.DeriveRules(intent);
        Assert.Equal(ExplorationRule.ExpandContainer, container);
        Assert.Equal(ExplorationRule.RecordOnly, leaf);
    }

    [Theory]
    [InlineData(0, ExplorationDepthSemantics.RootRecordOnly)]
    [InlineData(1, ExplorationDepthSemantics.RootAndDirectChildren)]
    [InlineData(2, ExplorationDepthSemantics.BoundedRecursive)]
    [InlineData(64, ExplorationDepthSemantics.BoundedRecursive)]
    public void DeriveDepthSemantics_MapsDeclaredDepth(int declared, ExplorationDepthSemantics expected)
    {
        Assert.Equal(expected, ExplorationLedgerCompiler.DeriveDepthSemantics(declared));
    }

    [Fact]
    public void LedgerTypes_CarryNoAuthorityMembers()
    {
        var forbidden = new[]
        {
            "Authorize", "Complete", "Transition", "Dispatch", "Execute",
            "StartRun", "Recover", "Fail", "Cancel",
        };
        foreach (var type in new[] { typeof(ExplorationLedgerView), typeof(ExplorationScopeLedger), typeof(ExplorationRule), typeof(ExplorationDepthSemantics) })
        {
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                Assert.DoesNotContain(member.Name, forbidden);
            }
        }
    }

    [Fact]
    public void LedgerTypes_DoNotReferenceMutableWorldOrActionTypes()
    {
        var forbiddenTypeNames = new[] { "DeviceAction", "RunState", "GoalEvidence", "Traversal", "StateMachine" };
        foreach (var type in new[] { typeof(ExplorationLedgerView), typeof(ExplorationScopeLedger) })
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.DoesNotContain(property.PropertyType.Name, forbiddenTypeNames);
            }
        }
    }

    [Fact]
    public void ScopeLedger_RejectsDispositionOverCount()
    {
        // Spec-aligned invariants (R3+R4): unknown frontier is an overlapping
        // visited annotation, NOT a disposition population — so the disposition
        // sum is visited + pending + unresolved (frontier excluded), and frontier
        // is separately bounded by discovered.
        // visited + pending + unresolved > discovered → reject.
        Assert.Throws<ArgumentException>(() =>
            new ExplorationScopeLedger("scope://root", discovered: 2, visited: 2, pending: 1, unresolved: 0, unknownFrontier: 0, 1));
        // unknownFrontier > discovered → reject.
        Assert.Throws<ArgumentException>(() =>
            new ExplorationScopeLedger("scope://root", discovered: 1, visited: 1, pending: 0, unresolved: 0, unknownFrontier: 2, 1));
    }

    [Fact]
    public void ScopeLedger_AllowsFrontierOverlappingVisited()
    {
        // R3+R4 overlap is legal: the same two record-only boundary nodes count
        // as visited (fresh-observation record, zero dispatch) AND as unknown
        // frontier (containers beyond the declared depth boundary).
        var scope = new ExplorationScopeLedger(
            "scope://root", discovered: 2, visited: 2, pending: 0, unresolved: 0, unknownFrontier: 2, 1);
        Assert.Equal(2, scope.Visited);
        Assert.Equal(2, scope.UnknownFrontier);
        Assert.Equal(2, scope.Discovered);
    }

    [Fact]
    public void Compiler_IsPure_NoStatefulDependencies()
    {
        // The compiler type must expose only static members (no instance state, no persistence surface).
        Assert.True(typeof(ExplorationLedgerCompiler)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Length == 0);
    }

    [Fact]
    public void RevisitCoverage_IdentityOutsideApprovedInventory_FailsClosed()
    {
        // Fail-closed consistency cross-check: a coverage record referencing a
        // branch absent from the scope's approved sibling inventory is evidence
        // corruption — compilation must throw, never produce a cleaner ledger.
        var progress = Progress(approved: new[] { "a" }, completed: new[] { "a" });

        Assert.Throws<InvalidOperationException>(() =>
            ExplorationLedgerCompiler.CompileScope(Evidence(progress, revisit: ["a", "ghost-branch"])));
    }

    [Fact]
    public void ValidRevisitCoverage_DoesNotChangeTheFiveCounts()
    {
        // Coverage concerns still-pending branches: a valid coverage input
        // (every identity inside the approved inventory) must leave all five
        // counts identical to the no-coverage compilation.
        var progress = Progress(
            approved: new[] { "a", "b", "c" },
            completed: new[] { "a" },
            authorized: new[] { "a", "b" });

        var withoutCoverage = ExplorationLedgerCompiler.CompileScope(Evidence(progress));
        var withCoverage = ExplorationLedgerCompiler.CompileScope(Evidence(progress, revisit: ["b", "c"]));

        Assert.Equal(withoutCoverage.Discovered, withCoverage.Discovered);
        Assert.Equal(withoutCoverage.Visited, withCoverage.Visited);
        Assert.Equal(withoutCoverage.Pending, withCoverage.Pending);
        Assert.Equal(withoutCoverage.Unresolved, withCoverage.Unresolved);
        Assert.Equal(withoutCoverage.UnknownFrontier, withCoverage.UnknownFrontier);
    }

    [Fact]
    public void IdentityPartition_CompletedAndUnresolvedIsExact()
    {
        var progress = Progress(approved: ["a", "b"], completed: ["a"]);
        var scope = ExplorationLedgerCompiler.CompileScope(Evidence(progress, unresolved: ["b"]));
        Assert.Equal((2, 1, 0, 1), (scope.Discovered, scope.Visited, scope.Pending, scope.Unresolved));
    }

    [Fact]
    public void VerifiedBoundaryDisposition_IsVisited()
    {
        var relation = new BoundaryRelation("scope", "boundary@occ", "app", "external", "scope", 10);
        var progress = new BranchProgressEvidence("scope",
            ImmutableDictionary<string, long>.Empty.Add("boundary", 10),
            ImmutableDictionary<string, long>.Empty) with
        {
            RequiredBoundaryObligations = ImmutableArray.Create(new BoundaryObligation(relation).WithVerified()),
            VerifiedBoundaryDispositions = ImmutableArray.Create(new VerifiedBoundaryDisposition(relation, "scope", 11)),
        };
        var scope = ExplorationLedgerCompiler.CompileScope(Evidence(progress));
        Assert.Equal(1, scope.Visited);
        Assert.Equal(11, scope.SourceObservationSequence);
    }

    [Theory]
    [InlineData("unresolved")]
    [InlineData("record-only")]
    [InlineData("frontier")]
    [InlineData("revisit")]
    public void OutOfInventoryIdentity_FailsClosed(string kind)
    {
        var progress = Progress(approved: ["a"]);
        var evidence = kind switch
        {
            "unresolved" => Evidence(progress, unresolved: ["ghost"]),
            "record-only" => new ExplorationScopeEvidence(progress, recordOnlyIds: [new KeyValuePair<string, long>("ghost", 10)]),
            "frontier" => Evidence(progress, frontier: ["ghost"]),
            _ => Evidence(progress, revisit: ["ghost"]),
        };
        Assert.Throws<InvalidOperationException>(() => ExplorationLedgerCompiler.CompileScope(evidence));
    }

    [Fact]
    public void OutOfInventoryVerifiedBoundary_FailsClosed()
    {
        var relation = new BoundaryRelation("scope", "ghost@occ", "app", "external", "scope", 10);
        var progress = new BranchProgressEvidence("scope",
            ImmutableDictionary<string, long>.Empty.Add("a", 10),
            ImmutableDictionary<string, long>.Empty) with
        {
            VerifiedBoundaryDispositions = ImmutableArray.Create(new VerifiedBoundaryDisposition(relation, "scope", 11)),
        };
        Assert.Throws<InvalidOperationException>(() => ExplorationLedgerCompiler.CompileScope(Evidence(progress)));
    }

    [Fact]
    public void ContradictoryIdentityEvidence_FailsClosed()
    {
        var progress = Progress(approved: ["a"], completed: ["a"]);
        Assert.Throws<InvalidOperationException>(() => ExplorationLedgerCompiler.CompileScope(Evidence(progress, unresolved: ["a"])));
        Assert.Throws<InvalidOperationException>(() => ExplorationLedgerCompiler.CompileScope(Evidence(progress, recordOnly: ["a"])));
        Assert.Throws<InvalidOperationException>(() => ExplorationLedgerCompiler.CompileScope(Evidence(progress, frontier: ["a"])));
    }

    [Fact]
    public void UnresolvedAndRecordOnlyOverlap_FailsClosed()
    {
        var progress = Progress(approved: ["a"]);
        Assert.Throws<InvalidOperationException>(() => ExplorationLedgerCompiler.CompileScope(
            Evidence(progress, unresolved: ["a"], recordOnly: ["a"])));
    }

    [Fact]
    public void UnresolvedAndVerifiedBoundaryOverlap_FailsClosed()
    {
        var relation = new BoundaryRelation("scope://root", "a@occ", "app", "external", "scope://root", 10);
        var progress = new BranchProgressEvidence("scope://root",
            ImmutableDictionary<string, long>.Empty.Add("a", 10),
            ImmutableDictionary<string, long>.Empty) with
        {
            VerifiedBoundaryDispositions = ImmutableArray.Create(new VerifiedBoundaryDisposition(relation, "scope://root", 11)),
        };
        Assert.Throws<InvalidOperationException>(() => ExplorationLedgerCompiler.CompileScope(Evidence(progress, unresolved: ["a"])));
    }

    [Fact]
    public void RecordOnlySequenceMismatch_FailsClosed()
    {
        var progress = Progress(approved: ["a"]);
        var evidence = new ExplorationScopeEvidence(progress,
            recordOnlyIds: [new KeyValuePair<string, long>("a", 99)]);
        Assert.Throws<InvalidOperationException>(() => ExplorationLedgerCompiler.CompileScope(evidence));
    }

    [Fact]
    public void IdentityAndSequenceCorrelationChangeDigest()
    {
        var context = new AcceptedExplorationRunContext("run", new ExplorationExecutionSemantics(
            "strategy", "intent", ExplorationRule.ExpandContainer, ExplorationRule.RecordOnly,
            ExplorationDepthSemantics.BoundedRecursive, ExplorationBoundaryDisposition.FailClosed, 2));
        var a = ExplorationLedgerCompiler.Compile(context, ImmutableArray.Create(Evidence(Progress(approved: ["a"], completed: ["a"]))));
        var b = ExplorationLedgerCompiler.Compile(context, ImmutableArray.Create(Evidence(Progress(approved: ["b"], completed: ["b"]))));
        var c = ExplorationLedgerCompiler.Compile(context, ImmutableArray.Create(Evidence(
            new BranchProgressEvidence("scope", ImmutableDictionary<string, long>.Empty.Add("a", 11), ImmutableDictionary<string, long>.Empty.Add("a", 20)))));
        Assert.NotEqual(a.LedgerDigest, b.LedgerDigest);
        Assert.NotEqual(a.LedgerDigest, c.LedgerDigest);
    }

    [Fact]
    public void EquivalentEvidenceEnumerationOrderProducesSameDigest()
    {
        var context = new AcceptedExplorationRunContext("run", new ExplorationExecutionSemantics(
            "strategy", "intent", ExplorationRule.ExpandContainer, ExplorationRule.RecordOnly,
            ExplorationDepthSemantics.BoundedRecursive, ExplorationBoundaryDisposition.FailClosed, 2));
        var first = Progress(approved: ["a", "b"], completed: ["a"], authorized: ["a"]);
        var second = new BranchProgressEvidence("scope://root",
            ImmutableDictionary<string, long>.Empty.Add("b", 10).Add("a", 10),
            ImmutableDictionary<string, long>.Empty.Add("a", 20),
            ImmutableDictionary<string, long>.Empty.Add("a", 15));
        var a = ExplorationLedgerCompiler.Compile(context, ImmutableArray.Create(Evidence(first)));
        var b = ExplorationLedgerCompiler.Compile(context, ImmutableArray.Create(Evidence(second)));
        Assert.Equal(a.LedgerDigest, b.LedgerDigest);
    }
}
