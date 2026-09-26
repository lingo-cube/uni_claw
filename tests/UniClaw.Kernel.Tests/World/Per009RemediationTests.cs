using UniClaw.Kernel;
using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Diagnostics;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Runtime;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;
using Xunit;

namespace UniClaw.Kernel.Tests.World;

/// <summary>
/// PER-009 评审整改回归（C-1/C-2/C-3/P-1）：权威域冲突销案且不升档（D13）、
/// 无 XML 侧/过期 dump/checkable guard 三剥夺路径（D3/D14/D12）、Run 边界
/// 复位、冻结信任表载入、事后验证四门 XML 路由。
/// </summary>
public sealed class Per009RemediationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(3);

    private sealed class DeterministicDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.DeliveryCompleted, "test", T0);
    }

    private static UniKernel Compose(WorldModel world)
    {
        var traceScope = RunTraceFactory.BeginDisabled(new RunCorrelation("test:per009-fix"));
        return new UniKernel(
            new EvidenceLedger(), world, traceScope.Trace,
            new RunModel(), new ControlLoop(new AgentPlanPolicy()),
            new RuntimeAssurance(new ProductFreshnessEvaluator(() => T0, TimeSpan.FromMinutes(5))),
            new EffectBoundary(new DeterministicDriver()), new RuntimeStageMetrics());
    }

    private static ObservationProposal Claim(
        string subject, string value, string producer, DateTimeOffset capture,
        string[]? lineage = null) => new(
        new ObservationClaim(subject, value),
        IngressKind.Observation, ObservationContext.External,
        new Provenance(producer, capture, $"scope:{subject}", lineage ?? new[] { "test:claim" }));

    private static string SwitchState => SharedSubjects.State("switch");

    private static void Observe(UniKernel kernel, ObservationProposal claim) =>
        kernel.Process(claim);

    [Fact]
    public void Tier0Conflict_ResolvesClearsAndLogs_WithoutEscalation()
    {
        var world = new WorldModel(new HashSet<string> { SwitchState });
        var kernel = Compose(world);

        Observe(kernel, Claim(SwitchState, "on", "host.live", T0)); // 视觉/系统侧：开
        Observe(kernel, Claim(SwitchState, "off", ProducerTrust.XmlProducer, T0,
            new[] { "xml-map:switch_widget", "xml-checkable:true", "xml-unique:true" })); // XML 定案：关
        Assert.Contains(world.Current!.Conflicts, c => c.Subject == SwitchState);

        var resolved = kernel.ResolveAuthorityConflicts(Window);

        // D13：Tier 0 销案、confidence 盲、不升档（零观察调用——纯裁决）
        Assert.Equal(1, resolved);
        Assert.DoesNotContain(world.Current!.Conflicts, c => c.Subject == SwitchState);
        Assert.Equal("off", world.Current.WorldState[SwitchState].Value);
        var entry = Assert.Single(world.ConflictResolutionLog);
        Assert.Equal("category-authority", entry.Tier);
        Assert.Equal("host.live", entry.OverruledProducer); // 高低置信同等待遇
        // 演化留痕（销案走 Revise 语义痕迹链）
        Assert.Contains(world.ClaimEvolutionLog,
            l => l.Subject == SwitchState && l.Producer == "kernel.conflict-resolver");
    }

    [Fact]
    public void NonXmlConflict_Stays_ForFocusedEscalation()
    {
        var world = new WorldModel(new HashSet<string> { SwitchState });
        var kernel = Compose(world);

        Observe(kernel, Claim(SwitchState, "on", "host.live", T0, new[] { "t:a" }));
        Observe(kernel, Claim(SwitchState, "off", "host.live", T0, new[] { "t:b" }));

        var resolved = kernel.ResolveAuthorityConflicts(Window);

        Assert.Equal(0, resolved); // 无 XML 侧：缺席即数据，不参战 → 交 Control 聚焦
        Assert.Contains(world.Current!.Conflicts, c => c.Subject == SwitchState);
    }

    [Fact]
    public void StaleDump_DeprivesAuthority_ConflictStays()
    {
        var world = new WorldModel(new HashSet<string> { SwitchState });
        var kernel = Compose(world);

        Observe(kernel, Claim(SwitchState, "on", "host.live", T0));
        Observe(kernel, Claim(SwitchState, "off", ProducerTrust.XmlProducer, T0 - TimeSpan.FromSeconds(30),
            new[] { "xml-map:x", "xml-checkable:true", "xml-unique:true" })); // 过期 dump

        var resolved = kernel.ResolveAuthorityConflicts(Window);

        Assert.Equal(0, resolved); // D14 FreshEnough：两份证据可能描述两个时刻
        Assert.Contains(world.Current!.Conflicts, c => c.Subject == SwitchState);
    }

    [Fact]
    public void CheckableFalse_GuardBlocks_Resolution()
    {
        var world = new WorldModel(new HashSet<string> { SwitchState });
        var kernel = Compose(world);

        Observe(kernel, Claim(SwitchState, "on", "host.live", T0));
        Observe(kernel, Claim(SwitchState, "off", ProducerTrust.XmlProducer, T0,
            new[] { "xml-map:x", "xml-checkable:false", "xml-unique:true" })); // guard 不过

        var resolved = kernel.ResolveAuthorityConflicts(Window);

        Assert.Equal(0, resolved); // D12：checkable=false 时 checked 不具权威
        Assert.Contains(world.Current!.Conflicts, c => c.Subject == SwitchState);
    }

    [Fact]
    public void ResolutionCarriesOccurrences_ActCanContinueWithoutReobservation()
    {
        // 销案 revision 原样携带 occurrences——Act 链不需观察恢复（机制闭环前提）
        var world = new WorldModel(
            new HashSet<string> { SwitchState, SharedSubjects.Screen, SharedSubjects.Frame },
            new ProductAssociationStrategy(),
            new HostSeededOccurrenceStrategy());
        var kernel = Compose(world);

        Observe(kernel, Claim(SharedSubjects.Screen, "s1", "host.live", T0));
        Observe(kernel, Claim(SwitchState, "on", "host.live", T0));
        Observe(kernel, Claim(SwitchState, "off", ProducerTrust.XmlProducer, T0,
            new[] { "xml-map:x", "xml-checkable:true", "xml-unique:true" }));
        // 帧批尾（revision-local occurrence 语义：承载 occurrence 的必须是最后一条）
        Observe(kernel, Claim(SharedSubjects.Frame, "{\"role\":\"switch\",\"state\":\"on\",\"b\":[0.1,0.1,0.2,0.2]}", "host.live", T0));
        var occurrencesBefore = world.Current!.Occurrences!.Count;
        Assert.True(occurrencesBefore > 0);

        kernel.ResolveAuthorityConflicts(Window);

        Assert.Equal(occurrencesBefore, world.Current!.Occurrences!.Count); // 携带不丢
        Assert.DoesNotContain(world.Current.Conflicts, c => c.Subject == SwitchState);
    }

    [Fact]
    public void FrozenTrustTable_Loads_AndMatchesDefault()
    {
        var frozen = ProducerTrust.LoadFrozenTable();

        Assert.Equal(ProducerTrust.Default().Lookup(ProducerTrust.XmlProducer, "state"),
            frozen.Lookup(ProducerTrust.XmlProducer, "state"));
        Assert.Equal(ProducerTrust.Grade.A, frozen.Lookup(ProducerTrust.XmlProducer, "state"));
        Assert.Equal(ProducerTrust.Grade.B, frozen.Lookup(ProducerTrust.XmlProducer, "bounds"));
        Assert.Equal(ProducerTrust.Grade.C, frozen.Lookup(ProducerTrust.VisionProducer, "state"));
        Assert.Equal(ProducerTrust.Grade.C, frozen.Lookup("unknown.producer", "state")); // 级联兜底
    }

    [Fact]
    public void TypedRoute_FourGates_VerifiesViaSemanticChecked()
    {
        // PER-014 R2：typed 验证路由四门等价回归（替代 legacy xml-route）。
        var typedSubject = "ui.node.cap-t1#0.checked";
        var world = new WorldModel(
            new HashSet<string> { SharedSubjects.Screen, SharedSubjects.Frame, typedSubject },
            new ProductAssociationStrategy(),
            new HostSeededOccurrenceStrategy());
        var kernel = Compose(world);

        Observe(kernel, Claim(SharedSubjects.Screen, "s1", "host.live", T0));
        // typed claim 先于帧批尾（occurrence 为 revision-local——承载 revision
        // 的 record 必须最后处理，与 ResolutionCarriesOccurrences 同一约定）
        var post = kernel.Process(TypedCheckedClaim(typedSubject, "checked", T0.AddSeconds(1)));
        Observe(kernel, Claim(SharedSubjects.Frame,
            "{\"role\":\"switch\",\"state\":\"on\",\"b\":[0.1,0.1,0.2,0.2]}", "host.live", T0));
        var target = new TargetSpec("switch", null, "tap", CheckedState.Checked);

        var verification = kernel.VerifyPostActionEffect(
            target, new[] { post }, dispatchTime: T0);

        Assert.True(verification.IsVerified);
        Assert.Contains(verification.Checks, c => c.Name == "typed-route-four-gates");
    }

    [Fact]
    public void TypedRoute_TemporalGate_Fails_FallsBackToOccurrencePath()
    {
        var typedSubject = "ui.node.cap-t2#0.checked";
        var world = new WorldModel(
            new HashSet<string> { SharedSubjects.Screen, SharedSubjects.Frame, typedSubject },
            new ProductAssociationStrategy(),
            new HostSeededOccurrenceStrategy());
        var kernel = Compose(world);

        Observe(kernel, Claim(SharedSubjects.Screen, "s1", "host.live", T0));
        // typed claim 先于帧批尾（occurrence 为 revision-local——承载 revision
        // 的 record 必须最后处理，与 ResolutionCarriesOccurrences 同一约定）
        var post = kernel.Process(TypedCheckedClaim(typedSubject, "checked", T0.AddSeconds(1)));
        Observe(kernel, Claim(SharedSubjects.Frame,
            "{\"role\":\"switch\",\"state\":\"on\",\"b\":[0.1,0.1,0.2,0.2]}", "host.live", T0));
        var target = new TargetSpec("switch", null, "tap", CheckedState.Unchecked);

        // 门④：capture 不在 dispatch 之后 → typed 路由退位 → occurrence 视觉路径
        //（presentation "on" ≠ unchecked → 如实未验证，无 typed-route 检查项）
        var verification = kernel.VerifyPostActionEffect(
            target, new[] { post }, dispatchTime: T0 + TimeSpan.FromSeconds(1));

        Assert.False(verification.IsVerified);
        Assert.DoesNotContain(verification.Checks, c => c.Name == "typed-route-four-gates");
    }

    /// <summary>typed checked claim（PER-013 投影同构：occurrence-qualified
    /// subject + Hierarchy descriptor provenance，capture timestamp 可控）。</summary>
    private static ObservationProposal TypedCheckedClaim(
        string subject, string value, DateTimeOffset captureTime) => new(
        new ObservationClaim(subject, value),
        IngressKind.Observation, ObservationContext.PostActionEffectFlow,
        new Provenance(
            TypedHierarchyProposalProjector.Producer, captureTime, $"scope:{subject}",
            new[] { TypedHierarchyProposalProjector.LineageMarker },
            Hierarchy: new HierarchyCaptureDescriptor(
                CaptureId: "cap-x", AndroidApiLevel: 34,
                UiHierarchyAcquirerKind.LegacyUiAutomatorXml, "1.0",
                UiHierarchyFormat.UiAutomatorXml, "dev-1", "sess-1",
                ObservationCycleId: null, CaptureTimestamp: captureTime,
                CaptureDuration: null, HierarchyCapability.CheckedBooleanCollapsed,
                CoverageCompleteness.CompleteWithinDeclaredSurface,
                CoverageLimitation: null, NodeLocalIndex: 0, ParentLocalIndex: null,
                Field: "checked")));

    /// <summary>测试侧 occurrence 策略：从 screen.frame 帧派生 switch occurrence。</summary>
    private sealed class HostSeededOccurrenceStrategy : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous)
        {
            if (record.Claim.Subject != SharedSubjects.Frame)
                return Array.Empty<ProposedOccurrence>();
            var owner = previous?.Containers.Count == 1 ? previous.Containers[0].Identity.ContainerId : null;
            return new[]
            {
                new ProposedOccurrence(owner, "switch", null, "on",
                    new SpatialLocator(0.1, 0.1, 0.2, 0.2, "test.frame")),
            };
        }
    }
}
