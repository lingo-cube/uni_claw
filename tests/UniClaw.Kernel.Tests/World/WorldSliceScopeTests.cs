using UniClaw.Kernel.World;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World.UiRealization;
using UniClaw.Kernel.Core;
using Xunit;

namespace UniClaw.Kernel.Tests.World;

public sealed class WorldSliceScopeTests
{
    [Fact]
    public void HistoricalBindingProjection_PreservesLegacyBasisBytes_AndRemainsStale()
    {
        var legacy = new UniClaw.Core.CoreId("slice:rev-1:ctr-history");
        var binding = new UniClaw.Kernel.Effects.CanonicalBinding("binding:old", "intent:old", "tap",
            "target", "observed", "rev-1", 1);
        var projected = CoreSemanticProjection.ProjectTargetBinding(binding, legacy,
            new UniClaw.Core.CoreId("ctr-history"), "rev-2");
        Assert.Equal("slice:rev-1:ctr-history", projected.BasisSliceId!.Value.Value);
        Assert.Equal(UniClaw.Core.BindingDisposition.Stale, projected.Disposition);
        Assert.False(UniClaw.Core.CoreInvariants.CanDispatch(projected));
    }

    [Fact]
    public void CoreSliceReferences_DistinguishScopesOnSameRevisionAndRoot()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "ui.screen" }, new ProductAssociationStrategy());
        string Observe(string page, int t)
        {
            var (_, record) = ledger.Admit(new ObservationProposal(new("ui.screen", page), IngressKind.Observation,
                ObservationContext.External, new("page-observer", DateTimeOffset.UnixEpoch.AddSeconds(t), $"capture:{t}", ["fixture"])));
            world.Reconcile(record!, world.JudgeRelevance(record!));
            return world.Current!.WorldState[WorldModel.CurrentContainerSubject].Value;
        }
        var root = Observe("root", 1);
        var child = Observe("child", 2);
        var kernel = new UniKernel(ledger, world, UniClaw.Kernel.Trace.DisabledRunTrace.Instance);

        var a = kernel.DeriveSlice(root, [root]);
        var b = kernel.DeriveSlice(root, [root, child]);
        var revision = world.Current!;

        Assert.NotEqual(CoreSemanticProjection.SliceId(a).Value, CoreSemanticProjection.SliceId(b).Value);
        Assert.NotEqual(CoreSemanticProjection.ProjectSlice(a, revision).Scope,
            CoreSemanticProjection.ProjectSlice(b, revision).Scope);
    }

    [Fact]
    public void EquivalentScopes_AreOrdinalCanonicalAndDifferentRevisionsRemainDistinct()
    {
        var ledger = new EvidenceLedger();
        var world = new WorldModel(new HashSet<string> { "ui.screen" }, new ProductAssociationStrategy());
        string Observe(string page, int t)
        {
            var (_, record) = ledger.Admit(new ObservationProposal(new("ui.screen", page), IngressKind.Observation,
                ObservationContext.External, new("page-observer", DateTimeOffset.UnixEpoch.AddSeconds(t), $"capture:{t}", ["fixture"])));
            world.Reconcile(record!, world.JudgeRelevance(record!));
            return world.Current!.WorldState[WorldModel.CurrentContainerSubject].Value;
        }
        var a = Observe("A", 1); var b = Observe("B", 2);
        var kernel = new UniKernel(ledger, world, UniClaw.Kernel.Trace.DisabledRunTrace.Instance);
        var x = kernel.DeriveSlice(a, [a, b]);
        var y = kernel.DeriveSlice(a, [b, a, b]);
        Assert.Equal(x.InScopeContainerIds, y.InScopeContainerIds);
        Assert.Equal(CoreSemanticProjection.SliceId(x), CoreSemanticProjection.SliceId(y));
        Observe("A", 3);
        Assert.NotEqual(CoreSemanticProjection.SliceId(x), CoreSemanticProjection.SliceId(kernel.DeriveSlice(a, [a, b])));
        Assert.NotEqual(CoreSemanticProjection.SliceId(x), CoreSemanticProjection.SliceId(kernel.DeriveSlice(b)));
    }
}
