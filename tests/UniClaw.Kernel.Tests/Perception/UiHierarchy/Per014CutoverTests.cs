using System.Reflection;
using System.Text.RegularExpressions;
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

namespace UniClaw.Kernel.Tests.Perception.UiHierarchy;

/// <summary>
/// PER-014 Slice E（rulings R3-R6）：cutover 守卫测试。
/// T6 两轴并存（G3）：rendered/appearance claim 与 semantic checked Unknown
/// 并存，无跨轴推导、无自动 Conflict。
/// T7 迁移后 consumer 行为证明：belief 只有 typed claims 时验证与 obligation
/// 判定照常工作；追加 legacy switch.state claim 不改变任何结果（零 dual-read）。
/// T8 Policy 零新 authority（G4/R6）：PolicyRuntime 不引用新缝或 WorldModel 写 API。
/// T9 Assurance 零新 authority（G4）：RuntimeAssurance 仍只 read + evaluate。
/// T10 legacy reader inventory 可复算（G5）：justified 集之外零 legacy-state reader。
/// </summary>
public sealed class Per014CutoverTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);
    private static readonly TargetDescriptor Switch = new("switch");

    // ---- T6：两轴并存（G3 / R：semantic.checked != rendered.toggleAppearance）----

    [Fact]
    public void T6_RenderedOff_Alone_DoesNotDeriveSemanticChecked()
    {
        // rendered 轴 claim（PER-010 冻结词汇）在场、typed checked claim 缺席
        // → semantic 轴如实 Unknown（不跨轴推导 rendered OFF → Unchecked）。
        var belief = Belief(new Dictionary<string, WorldClaim>
        {
            ["rendered.toggleAppearance"] = PlainClaim("off", "ev-rendered-0001"),
        }, Occ());

        var resolution = SemanticCheckedResolver.Resolve(belief, Switch);

        Assert.Equal(FieldState.Unknown, resolution.State);
        Assert.Equal("no-checked-claim", resolution.Reason);
    }

    [Fact]
    public void T6_RenderedOff_And_TypedChecked_Coexist_NoCrossAxisDerivation()
    {
        // 两轴并存：semantic 轴由 typed claim 定案，rendered claim 被无视
        //（既不参与推导也不产生 Conflict——不同 claim domain）。
        var belief = Belief(new Dictionary<string, WorldClaim>
        {
            ["ui.node.cap-1#0.checked"] = TypedClaim("checked", "ev-typed-0000001"),
            ["rendered.toggleAppearance"] = PlainClaim("off", "ev-rendered-0001"),
        }, Occ());

        var resolution = SemanticCheckedResolver.ResolveDetailed(belief, Switch);

        Assert.True(resolution.IsObserved);
        Assert.Equal(CheckedState.Checked, resolution.Value.Value);
    }

    [Fact]
    public void T6_TwoAxes_NoAutoConflict_InWorldModel()
    {
        var typedSubject = "ui.node.cap-t6#0.checked";
        var renderedSubject = "rendered.toggleAppearance";
        var world = new WorldModel(new HashSet<string> { typedSubject, renderedSubject });
        var kernel = Compose(world);

        kernel.Process(TypedCheckedProposal(typedSubject, "checked", T0.AddSeconds(1)));
        kernel.Process(PlainProposal(renderedSubject, "off", "host.live", T0));

        // 不同 claim domain：零自动 Conflict；两轴 claim 并存于同一 WorldState。
        Assert.Empty(world.Current!.Conflicts);
        Assert.Equal("checked", world.Current.WorldState[typedSubject].Value);
        Assert.Equal("off", world.Current.WorldState[renderedSubject].Value);
    }

    // ---- T7：迁移后 consumer 只认 typed（零 dual-read 行为证明）----

    [Fact]
    public void T7_TypedOnlyBelief_ObligationAndVerificationWork()
    {
        var typedSubject = "ui.node.cap-t7#0.checked";
        var kernel = ComposeTypedKernel(typedSubject, includeLegacySubject: false);

        kernel.Process(TypedCheckedProposal(typedSubject, "checked", T0.AddSeconds(1)));
        kernel.Process(FrameProposal()); // 帧批尾：occurrence 载体 revision

        // verification：typed 唯一证据源即通过（typed route 四门全过、值相等）。
        var verification = kernel.VerifyPostActionEffect(
            new TargetSpec("switch", null, "tap", CheckedState.Checked),
            Array.Empty<KernelResult>(), dispatchTime: T0);
        Assert.True(verification.IsVerified);
        Assert.Contains(verification.Checks, c => c.Name == "typed-route-four-gates");

        // obligation：typed 唯一证据源即满足
        Assert.Empty(kernel.UnsatisfiedMandatoryObligations());
    }

    [Fact]
    public void T7_AddingLegacySwitchStateClaim_DoesNotChangeOutcomes()
    {
        var typedSubject = "ui.node.cap-t7b#0.checked";
        var legacySubject = SharedSubjects.State("switch");
        var kernel = ComposeTypedKernel(typedSubject, includeLegacySubject: true);

        kernel.Process(TypedCheckedProposal(typedSubject, "unchecked", T0.AddSeconds(1)));
        kernel.Process(FrameProposal());
        var before = kernel.UnsatisfiedMandatoryObligations();
        var semanticBefore = SemanticCheckedResolver.Resolve(
            kernel.CurrentBelief!, Switch);

        // 追加 legacy switch.state=on claim（若存在 dual-read，会误判满足）；
        // 帧批尾跟随（revision-local occurrence 语义）。
        kernel.Process(PlainProposal(legacySubject, "on", "host.live", T0));
        kernel.Process(FrameProposal());

        var after = kernel.UnsatisfiedMandatoryObligations();

        // 结果不变：obligation 判定逐条不变（legacy "on" 不折叠不代判）；
        // legacy claim 确实入世（egress 表面仍在），但 R1 缝只读 ui.node.*.checked
        // subjects（T6 已锁该 subject 过滤面）→ 零 dual-read。
        Assert.NotEmpty(before);
        Assert.NotEmpty(after);
        Assert.Equal(
            before.Select(o => o.ObligationId).Order(),
            after.Select(o => o.ObligationId).Order());
        Assert.Equal(FieldState.Observed, semanticBefore.State);
        Assert.Equal(CheckedState.Unchecked, semanticBefore.Value);
        Assert.Equal("on", kernel.CurrentBelief!.WorldState[legacySubject].Value);
    }

    // ---- T8：Policy 零新 authority（G4 / R6）----

    [Fact]
    public void T8_PolicyRuntime_ReferencesNoResolver_NoWorldModelWriteApi()
    {
        // 签名级 reflection：Policy* 类型（PolicyRuntime.cs 全部类型 +
        // AgentPlanPolicy）无任何成员签名触及 SemanticCheckedResolver /
        // WorldModel（resolver 是 R1 只读缝；Policy 依 RUN-005/AGT-001 冻结，
        // subject-parametric，零新 authority）。
        var kernelAssembly = typeof(UniKernel).Assembly;
        var policyTypes = kernelAssembly.GetTypes()
            .Where(t => t.Namespace == "UniClaw.Kernel.Runtime"
                && t.Name.StartsWith("Policy", StringComparison.Ordinal))
            .ToArray();
        Assert.Contains(kernelAssembly.GetTypes(), t => t.Name == "PolicyEvaluation"); // 名单非空哨兵
        Assert.NotEmpty(policyTypes);

        var forbidden = new[]
        {
            typeof(WorldModel),
            kernelAssembly.GetTypes().Single(t => t.Name == "SemanticCheckedResolver"),
        };
        foreach (var type in policyTypes)
        {
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (var member in members)
            {
                var touched = MemberSignatureTypes(member);
                Assert.True(
                    !touched.Any(forbidden.Contains),
                    $"Policy 类型 {type.Name} 的成员 {member.Name} 触及禁区类型"
                    + $"（PER-014 R6：Policy 零新 authority）");
            }
        }

        // 源码级（方法体 reflection 不可见）：PolicyRuntime.cs 不出现
        // WorldModel / SemanticCheckedResolver 触点。
        var source = File.ReadAllText(RepoPath("src/UniClaw.Kernel/Runtime/PolicyRuntime.cs"));
        Assert.DoesNotContain("WorldModel", source);
        Assert.DoesNotContain("SemanticCheckedResolver", source);
    }

    // ---- T9：Assurance 零新 authority（G4）----

    [Fact]
    public void T9_RuntimeAssurance_StillReadOnlyEvaluate()
    {
        var assuranceType = typeof(RuntimeAssurance);
        var kernelAssembly = typeof(UniKernel).Assembly;

        // 签名级：公开成员不触及 WorldModel / EvidenceLedger（评估输入全是
        // view / receipt / contract view；Assurance 不持有也不写世界）。
        foreach (var member in assuranceType.GetMembers(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var touched = MemberSignatureTypes(member);
            Assert.True(
                !touched.Any(t => t == typeof(WorldModel)
                    || t.Name == "SemanticCheckedResolver"
                    || t == typeof(EvidenceLedger)),
                $"RuntimeAssurance 成员 {member.Name} 触及世界/账本类型（T9：只 read + evaluate）");
        }

        // 源码级：无 WorldModel 引用、无世界写 API（Revise / Establish）调用。
        var source = File.ReadAllText(RepoPath("src/UniClaw.Kernel/Assurance/RuntimeAssurance.cs"));
        Assert.DoesNotContain("WorldModel", source);
        Assert.DoesNotContain(".Revise", source);
        Assert.DoesNotContain("EstablishClaim", source);
    }

    // ---- T10：legacy reader inventory 可复算（G5 / M-10）----

    /// <summary>
    /// 2026-09-26 post-migration 复算基线：src 内 `*.state` 触点完整清单
    /// （file → line 集合，Regex <c>\.state\b</c> 大小写敏感）。处置：
    /// UiAutomatorDump / LivePerception = egress writer（R5 旗门控 / wifi 探针
    /// 单一 producer egress）；HostRunner = 回滚旗 + egress scope；SharedSubjects
    /// = 常量；LegacyStateProjection = egress 投影；AgentPlanPolicy = 通用
    /// subject 前缀聚焦（subject-parametric）；ConflictResolver = legacy-only
    /// 裁决 reader（R4 休眠 rollback 面，唯一保留 reader）。生产 reader 合计
    /// = 1 处（ConflictResolver.cs:104-105），其余全为 writer/注释/常量。
    /// </summary>
    private static readonly IReadOnlyDictionary<string, int[]> FrozenInventory =
        new Dictionary<string, int[]>
        {
            ["src/UniClaw.Host/UiAutomatorDump.cs"] = new[] { 19, 191, 231, 649 },
            ["src/UniClaw.Host/HostRunner.cs"] = new[] { 39, 41 },
            ["src/UniClaw.Host/LivePerception.cs"] = new[] { 154, 185, 187, 204 },
            ["src/UniClaw.Kernel/Compatibility/LegacyStateProjection.cs"] = new[] { 23 },
            ["src/UniClaw.Kernel/Evidence/SharedSubjects.cs"] = new[] { 6, 17, 18 },
            ["src/UniClaw.Kernel/Runtime/AgentPlanPolicy.cs"] = new[] { 49 },
            ["src/UniClaw.Kernel/World/ConflictResolver.cs"] = new[] { 90, 101, 104, 105 },
        };

    private static readonly Regex StateSurface = new(@"\.state\b", RegexOptions.Compiled);

    /// <summary>功能性 reader 触点模式（读 *.state claim 值做判定）。</summary>
    private static readonly Regex ReaderPattern =
        new(@"EndsWith\(""\.state""|TryGetValue\([^)]*\.state|WorldState\[[^)]*\.state", RegexOptions.Compiled);

    [Fact]
    public void T10_LegacyStateInventory_Recomputable_ZeroUnjustifiedReaders()
    {
        var root = RepoRoot();
        var actual = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
        foreach (var project in new[] { "src/UniClaw.Host", "src/UniClaw.Kernel" })
        {
            foreach (var file in Directory.EnumerateFiles(
                Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.Contains("/obj/") || relative.Contains("/bin/"))
                    continue;
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (StateSurface.IsMatch(lines[i]))
                    {
                        (actual.TryGetValue(relative, out var list)
                            ? list
                            : actual[relative] = new List<int>()).Add(i + 1);
                    }
                }
            }
        }

        var actualFrozen = actual.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
        Assert.True(
            actualFrozen.Count == FrozenInventory.Count
                && FrozenInventory.All(kv =>
                    actualFrozen.TryGetValue(kv.Key, out var lines)
                    && lines.SequenceEqual(kv.Value)),
            "legacy *.state inventory 漂移 = RED（PER-014 R5 冻结）。"
            + "实际：[" + string.Join(", ", actualFrozen.Select(kv => $"{kv.Key}:{string.Join('/', kv.Value)}"))
            + "]；期望见 FrozenInventory");

        // 零 UNJUSTIFIED readers：功能性 reader 触点只允许出现在
        // ConflictResolver.cs（R4 显式 legacy-only rollback 路径）。
        foreach (var (file, lines) in actualFrozen)
        {
            var source = File.ReadAllLines(RepoPath(file));
            foreach (var line in lines)
            {
                if (ReaderPattern.IsMatch(source[line - 1]))
                    Assert.Equal("src/UniClaw.Kernel/World/ConflictResolver.cs", file);
            }
        }
    }

    // ---- 公共 fixture ----

    private static UniKernel Compose(WorldModel world)
    {
        var traceScope = RunTraceFactory.BeginDisabled(new RunCorrelation("test:per014-cutover"));
        return new UniKernel(
            new EvidenceLedger(), world, traceScope.Trace,
            new RunModel(), new ControlLoop(new AgentPlanPolicy()),
            new RuntimeAssurance(new ProductFreshnessEvaluator(() => T0, TimeSpan.FromMinutes(5))),
            new EffectBoundary(new DeterministicDriver()), new RuntimeStageMetrics());
    }

    /// <summary>typed-obligation 组合根（R3 词汇：Subject = ui.role.switch.checked，
    /// RequiredValue = checked；EntityScope = switch）。</summary>
    private static UniKernel ComposeTypedKernel(string typedSubject, bool includeLegacySubject)
    {
        var scope = new HashSet<string>
        {
            SharedSubjects.Screen, SharedSubjects.Frame, typedSubject,
        };
        if (includeLegacySubject)
            scope.Add(SharedSubjects.State("switch"));
        var world = new WorldModel(scope, new ProductAssociationStrategy(), new FrameSeededOccurrenceStrategy());
        var kernel = Compose(world);
        var admission = kernel.AdmitContract(new ExecutionContract(
            Version: "v0", Objective: "per014-t7", Scope: scope,
            AllowedEffects: new HashSet<string> { "tap" }, ForbiddenEffects: new HashSet<string>(),
            ProofCriteria: new[] { "switch-state-checked" },
            Obligations: new[]
            {
                new RunObligation("obj-switch-checked", RunObligationKind.Objective,
                    Subject: "ui.role.switch.checked", RequiredValue: "checked",
                    Mandatory: true, EntityScope: new TargetDescriptor("switch")),
            }));
        Assert.True(admission.Accepted, admission.RejectionReason);
        return kernel;
    }

    private sealed class DeterministicDriver : IEffectDriver
    {
        public DispatchResult Deliver(DispatchRequest request) =>
            new(DispatchOutcome.DeliveryCompleted, "test", T0);
    }

    private static OccurrenceBelief Occ(string role = "switch") =>
        new("occ-1", null, role, null, Array.Empty<string>());

    private static WorldBeliefRevision Belief(
        IReadOnlyDictionary<string, WorldClaim> worldState,
        params OccurrenceBelief[] occurrences) =>
        new(
            RevisionId: "rev-1", ParentRevisionId: null, RevisionNumber: 1,
            WorldState: worldState, WorldGraph: Array.Empty<string>(),
            EvidenceBasis: (IReadOnlySet<string>)new HashSet<string>(
                worldState.Values.Select(c => c.EvidenceId)),
            FreshnessBasis: new FreshnessBasis(T0), Uncertainty: new Uncertainty(0),
            Conflicts: Array.Empty<Conflict>(),
            Occurrences: occurrences);

    private static WorldClaim TypedClaim(string value, string evidenceId) =>
        new(value, evidenceId, TypedHierarchyProposalProjector.Producer);

    private static WorldClaim PlainClaim(string value, string evidenceId) =>
        new(value, evidenceId, "host.live");

    /// <summary>typed checked claim 投影（PER-013 同构：occurrence-qualified
    /// subject + Hierarchy descriptor provenance）。</summary>
    private static ObservationProposal TypedCheckedProposal(
        string subject, string value, DateTimeOffset capture) => new(
        new ObservationClaim(subject, value),
        IngressKind.Observation, ObservationContext.PostActionEffectFlow,
        new Provenance(
            TypedHierarchyProposalProjector.Producer, capture, $"scope:{subject}",
            new[] { TypedHierarchyProposalProjector.LineageMarker },
            Hierarchy: new HierarchyCaptureDescriptor(
                CaptureId: subject.Split('#')[0]["ui.node.".Length..], AndroidApiLevel: 34,
                UiHierarchyAcquirerKind.LegacyUiAutomatorXml, "1.0",
                UiHierarchyFormat.UiAutomatorXml, "dev-1", "sess-1",
                ObservationCycleId: null, CaptureTimestamp: capture,
                CaptureDuration: null, HierarchyCapability.CheckedBooleanCollapsed,
                CoverageCompleteness.CompleteWithinDeclaredSurface,
                CoverageLimitation: null, NodeLocalIndex: 0, ParentLocalIndex: null,
                Field: "checked")));

    private static ObservationProposal PlainProposal(
        string subject, string value, string producer, DateTimeOffset capture) => new(
        new ObservationClaim(subject, value),
        IngressKind.Observation, ObservationContext.External,
        new Provenance(producer, capture, $"scope:{subject}", new[] { "test:claim" }));

    private static ObservationProposal FrameProposal() => new(
        new ObservationClaim(SharedSubjects.Frame,
            "{\"role\":\"switch\",\"state\":\"on\",\"b\":[0.1,0.1,0.2,0.2]}"),
        IngressKind.Observation, ObservationContext.External,
        new Provenance("host.live", T0, "scope:screen.frame", new[] { "test:frame" }));

    /// <summary>测试侧 occurrence 策略：从 screen.frame 帧派生 switch occurrence
    ///（与 Per009RemediationTests 同一机械）。</summary>
    private sealed class FrameSeededOccurrenceStrategy : IUiObservationStrategy
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

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "UniClaw.Kernel.slnx")))
                return directory.FullName;
            directory = directory.Parent!;
        }
        throw new InvalidOperationException("未定位到仓库根");
    }

    private static string RepoPath(string relative) => Path.Combine(RepoRoot(), relative);

    private static IReadOnlyList<Type> MemberSignatureTypes(MemberInfo member) => member switch
    {
        MethodInfo method => method.GetParameters().Select(p => p.ParameterType)
            .Append(method.ReturnType)
            .Where(t => t.Assembly == typeof(UniKernel).Assembly || t == typeof(WorldModel))
            .ToList(),
        ConstructorInfo ctor => ctor.GetParameters().Select(p => p.ParameterType).ToList(),
        PropertyInfo prop => new[] { prop.PropertyType },
        FieldInfo field => new[] { field.FieldType },
        EventInfo evt => new[] { evt.EventHandlerType! },
        _ => Array.Empty<Type>(),
    };
}
