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

    [Fact]
    public void CompileScope_ReportsUnifiedAccountingFromEvidence()
    {
        var progress = Progress(
            approved: new[] { "a", "b", "c" },
            completed: new[] { "a" },
            authorized: new[] { "a", "b" });

        var scope = ExplorationLedgerCompiler.CompileScope(progress, unresolvedCount: 0, unknownFrontierCount: 0);

        Assert.Equal(3, scope.Discovered);
        Assert.Equal(1, scope.Visited);           // only completed-with-evidence
        Assert.Equal(1, scope.Pending);           // b authorized, not completed
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

        var scope = ExplorationLedgerCompiler.CompileScope(progress, 0, 0);

        Assert.Equal(1, scope.Discovered);
        Assert.Equal(0, scope.Visited);
        Assert.Equal(1, scope.Pending);
    }

    [Fact]
    public void UnclassifiableNode_FailsClosedToUnresolved_NeverGuessed()
    {
        var progress = Progress(approved: new[] { "a" }, completed: new[] { "a" });

        var scope = ExplorationLedgerCompiler.CompileScope(progress, unresolvedCount: 1, unknownFrontierCount: 0);

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

        var scope = ExplorationLedgerCompiler.CompileScope(progress, 0, unknownFrontierCount: 1);

        Assert.Equal(1, scope.UnknownFrontier);
        Assert.Equal(2, scope.Visited);           // 'a' completed + 'deep-container' record-visited (R3)
        Assert.Equal(2, scope.Discovered);
    }

    [Fact]
    public void IdenticalEvidence_ProducesIdenticalLedgerAndDigest()
    {
        var scopes = ImmutableArray.Create(
            (Progress(approved: new[] { "a", "b" }, completed: new[] { "a" }), 0, 0, ImmutableArray<string>.Empty));

        var first = ExplorationLedgerCompiler.Compile(
            "run-1", "intent-1", ExplorationIntent.ExhaustiveWithinScope, 2, scopes);
        var second = ExplorationLedgerCompiler.Compile(
            "run-1", "intent-1", ExplorationIntent.ExhaustiveWithinScope, 2, scopes);

        Assert.Equal(first.LedgerDigest, second.LedgerDigest);
        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentEvidence_ProducesDifferentDigest()
    {
        var a = ExplorationLedgerCompiler.Compile(
            "run-1", "intent-1", ExplorationIntent.ExhaustiveWithinScope, 2,
            ImmutableArray.Create((Progress(approved: new[] { "a" }, completed: new[] { "a" }), 0, 0, ImmutableArray<string>.Empty)));
        var b = ExplorationLedgerCompiler.Compile(
            "run-1", "intent-1", ExplorationIntent.ExhaustiveWithinScope, 2,
            ImmutableArray.Create((Progress(approved: new[] { "a", "deep-container" }, completed: new[] { "a" }), 0, 1, ImmutableArray<string>.Empty)));

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
            ExplorationLedgerCompiler.CompileScope(
                progress, unresolvedCount: 0, unknownFrontierCount: 0,
                revisitCoveredIdentities: new[] { "a", "ghost-branch" }));
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

        var withoutCoverage = ExplorationLedgerCompiler.CompileScope(progress, 0, 0);
        var withCoverage = ExplorationLedgerCompiler.CompileScope(
            progress, 0, 0, revisitCoveredIdentities: new[] { "b", "c" });

        Assert.Equal(withoutCoverage.Discovered, withCoverage.Discovered);
        Assert.Equal(withoutCoverage.Visited, withCoverage.Visited);
        Assert.Equal(withoutCoverage.Pending, withCoverage.Pending);
        Assert.Equal(withoutCoverage.Unresolved, withCoverage.Unresolved);
        Assert.Equal(withoutCoverage.UnknownFrontier, withCoverage.UnknownFrontier);
    }
}
