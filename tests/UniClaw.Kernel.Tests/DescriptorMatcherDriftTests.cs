using UniClaw.Kernel.Assurance;
using UniClaw.Kernel.Control;
using UniClaw.Kernel.Effects;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Run;
using UniClaw.Kernel.Trace;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// RVR-002 F1 漂移回归 —— descriptor→occurrence 机械匹配的唯一实现
///（OccurrenceDescriptorMatcher）与全部三处消费点的一致性矩阵：
/// WorldModel.ResolveCurrent 与 DeriveOutcomeAssuranceView（经
/// DeriveEntityObligationFactKind，TargetEquality 模式），Control
/// DescriptorTargetPolicy（ScopeMembership 模式）。任何一处绕开共享
/// 匹配器私改谓词，本矩阵即 RED；匹配器自身语义由真值表测试钉住。
/// 确定性（level: DETERMINISTIC）。
/// </summary>
public sealed class DescriptorMatcherDriftTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 11, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 9, 11, 9, 5, 0, TimeSpan.Zero);

    /// <summary>判别景观（role × descriptor × container[null/ctr-a/ctr-b] ×
    /// state 有/无）。occurrence owner id 无需真实 minted——ResolveCurrent /
    /// OutcomeAssurance 只做过滤，不校验 container 存在性。</summary>
    private static readonly (string? Owner, string Role, string? Desc, string? State)[] Landscape =
    {
        ("ctr-a", "button", "x", "on"),
        ("ctr-b", "button", "x", "on"),
        (null, "button", "y", null),
        ("ctr-a", "label", "x", "off"),
        ("ctr-b", "toggle", "t", "off"),
    };

    // ---- 测试替身 ---------------------------------------------------------

    private sealed class LandscapeObservationStrategy : IUiObservationStrategy
    {
        public IReadOnlyList<ProposedOccurrence> Derive(EvidenceRecord record, WorldBeliefRevision? previous) =>
            Landscape.Select(o => new ProposedOccurrence(o.Owner, o.Role, o.Desc, o.State)).ToArray();
    }

    private sealed class NoopPolicy : IControlPolicy
    {
        public ControlDecision Decide(ControlInputs inputs) =>
            new(ControlIntentKind.Observe, EffectClass: null, TargetSubject: null);
    }

    private sealed class OkDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.DeliveryCompleted, "scripted:ok", T1);
    }

    private static WorldModel SeededWorld()
    {
        var world = new WorldModel(
            new HashSet<string> { UIWorldDoubles.Observed },
            new SeedContainerAssociationStrategy(),
            new LandscapeObservationStrategy());
        var kernel = new UniKernel(
            new EvidenceLedger(), world, DisabledRunTrace.Instance,
            run: new RunModel(),
            control: new ControlLoop(new NoopPolicy()),
            assurance: new RuntimeAssurance(new FreshnessDoubles.Satisfying()),
            effects: new EffectBoundary(new OkDriver()));
        Assert.True(kernel.AdmitContract(new ExecutionContract(
            Version: "v1",
            Objective: "matcher-drift-matrix",
            Scope: new HashSet<string> { UIWorldDoubles.Observed },
            AllowedEffects: new HashSet<string> { "tap" },
            ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "drift-matrix" })).Accepted);
        kernel.Process(UIWorldDoubles.Observation("page:seed:v1", T0)); // rev-1 铸 root
        kernel.Process(UIWorldDoubles.Observation("landscape:v1", T1)); // rev-2 occurrence 景观
        return world;
    }

    private static bool Matches(
        (string? Owner, string Role, string? Desc, string? State) occurrence,
        (string Role, string? Desc, string? Owner) target,
        ContainerMatchMode containerMode,
        IReadOnlySet<string>? scopeContainerIds = null) =>
        OccurrenceDescriptorMatcher.Matches(
            occurrence.Role, occurrence.Desc, occurrence.Owner,
            target.Role, target.Desc, target.Owner,
            containerMode, scopeContainerIds);

    // ---- 1：匹配器真值表（单一真相的语义钉） ------------------------------

    [Fact]
    public void MatcherTruthTablePinsBothContainerModes()
    {
        var occButtonA = Landscape[0]; // (ctr-a, button, x)
        var occButtonB = Landscape[1]; // (ctr-b, button, x)

        // role 维度：不相等永不匹配
        Assert.False(Matches(occButtonA, ("label", "x", null), ContainerMatchMode.TargetEquality));

        // descriptor 维度：target 未给 = 任意；给出 = 相等才匹配
        Assert.True(Matches(occButtonA, ("button", null, null), ContainerMatchMode.TargetEquality));
        Assert.False(Matches(occButtonA, ("button", "z", null), ContainerMatchMode.TargetEquality));

        // TargetEquality：target container 未给 = 任意；给出 = 相等才匹配
        Assert.True(Matches(occButtonA, ("button", "x", null), ContainerMatchMode.TargetEquality));
        Assert.True(Matches(occButtonA, ("button", "x", "ctr-a"), ContainerMatchMode.TargetEquality));
        Assert.False(Matches(occButtonA, ("button", "x", "ctr-b"), ContainerMatchMode.TargetEquality));
        Assert.True(Matches(occButtonB, ("button", "x", "ctr-b"), ContainerMatchMode.TargetEquality));

        // ScopeMembership：occurrence owner null = 通过；非 null 须 ∈ scope
        var scopeA = new HashSet<string> { "ctr-a" };
        Assert.True(Matches(Landscape[2], ("button", "y", null), ContainerMatchMode.ScopeMembership, scopeA));
        Assert.True(Matches(occButtonA, ("button", "x", null), ContainerMatchMode.ScopeMembership, scopeA));
        Assert.False(Matches(occButtonB, ("button", "x", null), ContainerMatchMode.ScopeMembership, scopeA));
        Assert.True(Matches(occButtonB, ("button", "x", null), ContainerMatchMode.ScopeMembership,
            new HashSet<string> { "ctr-a", "ctr-b" }));
        // scope 缺省 = 空 scope：非 null owner 一律不通过（fail-closed）
        Assert.False(Matches(occButtonA, ("button", "x", null), ContainerMatchMode.ScopeMembership));
    }

    // ---- 2：ResolveCurrent ≡ matcher（TargetEquality） ---------------------

    public static readonly TheoryData<string, string?, string?> TargetMatrix = new()
    {
        { "button", null, null },
        { "button", "x", null },
        { "button", "x", "ctr-a" },
        { "button", "x", "ctr-b" },
        { "button", null, "ctr-a" },
        { "button", "y", null },
        { "button", "z", null },
        { "label", null, null },
        { "label", "x", null },
        { "label", "x", "ctr-b" },
        { "toggle", "t", null },
        { "toggle", "t", "ctr-a" },
        { "toggle", "t", "ctr-b" },
        { "checkbox", null, null },
    };

    [Theory]
    [MemberData(nameof(TargetMatrix))]
    public void ResolveCurrentAgreesWithMatcherTargetEquality(string role, string? desc, string? owner)
    {
        var world = SeededWorld();
        var target = new TargetDescriptor(role, desc, owner);
        var expected = world.Current!.Occurrences!
            .Where(o => OccurrenceDescriptorMatcher.Matches(
                o.Role, o.SemanticDescriptor, o.OwningContainerId,
                role, desc, owner, ContainerMatchMode.TargetEquality))
            .Select(o => o.OccurrenceId)
            .ToHashSet();
        var view = world.ResolveCurrent(target);
        var actual = view.Candidates.Select(c => c.OccurrenceId).ToHashSet();
        Assert.True(expected.SetEquals(actual),
            $"ResolveCurrent ≠ matcher（{role}/{desc}/{owner}）：expected [{string.Join(",", expected)}] actual [{string.Join(",", actual)}]");
        Assert.Equal(
            actual.Count switch
            {
                0 => CurrentCandidateSetResultKind.NoCandidate,
                1 => CurrentCandidateSetResultKind.UniqueCandidate,
                _ => CurrentCandidateSetResultKind.MultipleCandidates,
            },
            view.Result);
    }

    // ---- 3：EntityObligation tri-state fact ≡ matcher（TargetEquality） ----

    public static readonly TheoryData<string, string?, string?, string> ObligationMatrix = new()
    {
        { "button", null, null, "on" },      // 3 候选 → Unknown
        { "button", "x", null, "on" },       // 2 候选 → Unknown
        { "button", "x", "ctr-a", "on" },    // 恰一 + state 命中 → Satisfied
        { "button", "x", "ctr-b", "on" },    // 恰一 + state 命中 → Satisfied
        { "button", "y", null, "on" },       // 恰一但 state null → Unknown
        { "label", "x", null, "on" },        // 恰一 + state "off" ≠ "on" → Unsatisfied
        { "label", "x", null, "off" },       // 恰一 + state 命中 → Satisfied
        { "toggle", "t", "ctr-a", "on" },    // 零候选 → Unknown
        { "checkbox", null, null, "on" },    // 零候选 → Unknown
    };

    [Theory]
    [MemberData(nameof(ObligationMatrix))]
    public void EntityObligationFactsAgreeWithMatcherTargetEquality(
        string role, string? desc, string? owner, string requiredState)
    {
        var world = SeededWorld();
        var candidates = world.Current!.Occurrences!
            .Where(o => OccurrenceDescriptorMatcher.Matches(
                o.Role, o.SemanticDescriptor, o.OwningContainerId,
                role, desc, owner, ContainerMatchMode.TargetEquality))
            .ToList();
        var expected = candidates.Count == 1 && candidates[0].State is not null
            ? (candidates[0].State == requiredState
                ? EntityObligationFactKind.Satisfied
                : EntityObligationFactKind.Unsatisfied)
            : EntityObligationFactKind.Unknown;

        var view = world.DeriveOutcomeAssuranceView(
            Array.Empty<string>(),
            new[] { ("obl-drift", new TargetDescriptor(role, desc, owner), requiredState) });
        var fact = Assert.Single(view.EntityFacts!);
        Assert.Equal(expected, fact.Kind);
    }

    // ---- 4：DescriptorTargetPolicy ≡ matcher（ScopeMembership） ------------

    private static (Slice Slice, ExecutionContractView View) LandscapeSlice()
    {
        var facts = Landscape
            .Select((o, i) => new OccurrenceFact($"occ-{i + 1}", o.Owner, o.Role, o.Desc, o.State))
            .ToArray();
        var slice = new Slice(
            "rev-drift", "ctr-root", new FreshnessBasis(T1),
            new[] { "ctr-a" }, facts, new Dictionary<string, string>());
        var view = new ExecutionContractView(
            "v1", "matcher-drift-matrix",
            new HashSet<string> { UIWorldDoubles.Observed },
            new HashSet<string> { "tap" },
            new HashSet<string>(),
            Array.Empty<string>());
        return (slice, view);
    }

    public static readonly TheoryData<string, string?> SpecMatrix = new()
    {
        { "button", "x" },   // occ-1 ∈ scope 命中；occ-2 owner ctr-b ∉ scope 不命中
        { "button", null },  // 首个 = occ-1
        { "button", "y" },   // occ-3 owner null → 通过
        { "label", "x" },    // occ-4 ∈ scope
        { "toggle", "t" },   // 仅 occ-5 owner ctr-b ∉ scope → 无匹配
        { "button", "z" },   // descriptor 无匹配
        { "checkbox", null },// role 无匹配
    };

    [Theory]
    [MemberData(nameof(SpecMatrix))]
    public void DescriptorTargetPolicyAgreesWithMatcherScopeMembership(string role, string? desc)
    {
        var (slice, view) = LandscapeSlice();
        var scope = new HashSet<string> { "ctr-a" };
        var spec = new TargetSpec(role, desc, "tap");
        var policy = new DescriptorTargetPolicy(new[] { spec });

        var decision = policy.Decide(new ControlInputs(view, slice));
        var expectedOccurrence = slice.Occurrences.FirstOrDefault(o =>
            OccurrenceDescriptorMatcher.Matches(
                o.Role, o.SemanticDescriptor, o.OwningContainerId,
                role, desc, targetOwningContainerId: null,
                ContainerMatchMode.ScopeMembership, scope));

        if (expectedOccurrence is null)
        {
            Assert.Equal(ControlIntentKind.Observe, decision.Kind);
            Assert.Null(decision.TargetSubject);
            Assert.Empty(policy.Visited);
        }
        else
        {
            Assert.Equal(ControlIntentKind.Act, decision.Kind);
            Assert.Equal(
                spec.SemanticDescriptor is null ? spec.Role : $"{spec.Role}:{spec.SemanticDescriptor}",
                decision.TargetSubject);
            // visited 簿记键 = (spec.Role, 被选中 occurrence 的 descriptor)
            Assert.Contains((spec.Role, expectedOccurrence.SemanticDescriptor), policy.Visited);
        }
    }

    [Fact]
    public void DescriptorTargetPolicyVisitedConsumesMatcherMatch()
    {
        var (slice, view) = LandscapeSlice();
        var policy = new DescriptorTargetPolicy(new[] { new TargetSpec("label", "x", "tap") });

        var first = policy.Decide(new ControlInputs(view, slice));
        Assert.Equal(ControlIntentKind.Act, first.Kind);
        // 唯一匹配 occurrence 已 visited：matcher 仍匹配，但 policy 簿记跳过 → Observe
        var second = policy.Decide(new ControlInputs(view, slice));
        Assert.Equal(ControlIntentKind.Observe, second.Kind);
    }
}
