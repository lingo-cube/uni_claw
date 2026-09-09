using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// ESO-001 phase 1 验收 E1–E5 —— EntityScopedObligation 物理入口
/// （producer ②：contract entity-scoped obligation → standing
/// ContinuityDemand → 首次充分观察后 identity 绑定；ADR-0014 / P23）。
/// 全部确定性（level: DETERMINISTIC）；observation / continuity 复用
/// UIW-003 的 deterministic seam doubles。
/// </summary>
public sealed class EntityScopedObligationTests
{
    private const string ObligationId = "obl-entity-1";
    private static readonly TargetDescriptor DefaultScope = new("save-button", "primary");

    /// <summary>带 entity-scoped obligation 的合法 contract（admission 全过）。</summary>
    private static ExecutionContract ScopedContract(TargetDescriptor? scope = null) => new(
        Version: "v1",
        Objective: "verify-save-button",
        Scope: new HashSet<string> { UIWorldDoubles.Observed },
        AllowedEffects: new HashSet<string> { "none" },
        ForbiddenEffects: new HashSet<string>(),
        ProofCriteria: new[] { "save-button-present" },
        Obligations: new[]
        {
            new RunObligation(
                ObligationId, RunObligationKind.Objective,
                Subject: "save-button", RequiredValue: "present", Mandatory: true,
                EntityScope: scope),
        });

    /// <summary>observation + continuity + Run 三面组合 kernel（ESO 组合缝）。</summary>
    private static (UniKernel Kernel, WorldModel World) NewKernel(
        IUiObservationStrategy observation,
        IContinuityStrategy continuity)
    {
        var world = new WorldModel(
            new HashSet<string> { UIWorldDoubles.Observed },
            associationStrategy: null, observation, continuity);
        return (new UniKernel(
            new EvidenceLedger(), world, DisabledRunTrace.Instance,
            run: new RunModel()), world);
    }

    // ---- E1：accepted → 恰含该 standing demand ------------------------------

    [Fact]
    public void E1_AcceptedEntityScopedObligation_RegistersDescriptorScopedStandingDemand()
    {
        var (kernel, world) = NewKernel(new RoleObservationStrategy(), new RoleContinuityStrategy());

        var admission = kernel.AdmitContract(ScopedContract(new TargetDescriptor(
            "save-button", "primary", "ctr-1")));

        Assert.True(admission.Accepted);
        var demand = Assert.Single(world.ContinuityDemands);
        Assert.Equal($"demand-obl-{ObligationId}", demand.DemandId);
        Assert.Equal(ContinuityDemandSourceKind.EntityScopedObligation, demand.SourceKind);
        Assert.Equal("save-button", demand.Role);
        Assert.Equal("primary", demand.SemanticDescriptor);
        Assert.Equal("ctr-1", demand.OwningContainerId);
        Assert.Null(demand.AnchorOccurrenceId);
        Assert.Null(demand.AnchorRevisionId);
        Assert.Null(demand.LogicalItemId);
    }

    // ---- E2：同 contract 重复 admit → demand 不重复 --------------------------

    [Fact]
    public void E2_ReAdmitSameContract_IsIdempotent_DemandNotDuplicated()
    {
        var (kernel, world) = NewKernel(new RoleObservationStrategy(), new RoleContinuityStrategy());

        kernel.AdmitContract(ScopedContract(DefaultScope));
        var second = kernel.AdmitContract(ScopedContract(DefaultScope));

        Assert.True(second.Accepted);
        Assert.Single(world.ContinuityDemands);
    }

    // ---- E3：rejected → 零登记 -----------------------------------------------

    [Fact]
    public void E3_RejectedAdmission_RegistersNoDemand()
    {
        var (kernel, world) = NewKernel(new RoleObservationStrategy(), new RoleContinuityStrategy());
        var invalid = ScopedContract(DefaultScope) with { Version = "" }; // version-present fail-closed

        var admission = kernel.AdmitContract(invalid);

        Assert.False(admission.Accepted);
        Assert.Empty(world.ContinuityDemands);
    }

    // ---- E4：无 EntityScope → 零登记（既有行为不变）--------------------------

    [Fact]
    public void E4_ObligationWithoutEntityScope_RegistersNoDemand()
    {
        var (kernel, world) = NewKernel(new RoleObservationStrategy(), new RoleContinuityStrategy());

        var admission = kernel.AdmitContract(ScopedContract(scope: null));

        Assert.True(admission.Accepted);
        Assert.Empty(world.ContinuityDemands);
    }

    // ---- E5：standing demand → 首帧观察 → ResolveContinuity → 绑定 -----------

    [Fact]
    public void E5_StandingDemand_AfterFirstObservation_ResolvesReferenceAndBindsLogicalItem()
    {
        var (kernel, world) = NewKernel(new RoleObservationStrategy(), new RoleContinuityStrategy());
        kernel.AdmitContract(ScopedContract(DefaultScope));
        var demandId = $"demand-obl-{ObligationId}";
        Assert.Null(world.ContinuityDemands.Single(d => d.DemandId == demandId).LogicalItemId);

        // 首帧充分观察（demand 早于观察登记——Q1 场景 2 全链）
        kernel.Process(UIWorldDoubles.Observation("save-button:primary", UIWorldDoubles.T0));

        var result = world.ResolveContinuity(new DemandHandle(demandId));

        Assert.Equal(ContinuityResolutionOutcomeKind.ReferenceEstablished, result.Outcome.Kind);
        Assert.NotNull(result.LogicalItemId);
        var bound = world.ContinuityDemands.Single(d => d.DemandId == demandId);
        Assert.Equal(result.LogicalItemId, bound.LogicalItemId);
        Assert.True(world.IsHotItem(result.LogicalItemId!));
    }
}
