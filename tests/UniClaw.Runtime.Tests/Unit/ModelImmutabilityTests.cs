using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Unit;

/// <summary>
    /// 模型层冒烟测试：契约类型存在（不可变 sealed record / abstract record union / enum）、
/// 字段契约精确、无 Deferred 类型 / 字段泄漏进 Model
/// （裁决 2/3/9；SC-P1-005 断言 4；Trap 一等模型已批准 — HG-1，RecoveryRequest / ElementKind 仍 DEFER；
/// RecoveryResult 已批准 — HG-5）。
/// </summary>
public class ModelImmutabilityTests
{
    private static readonly Type[] ContractTypes =
    {
        typeof(RunState),
        typeof(ObservedElement),
        typeof(Observation),
        typeof(WorldBelief),
        typeof(RecoveryAnchor),
        typeof(Goal),
        typeof(GoalEvidence),
        typeof(Plan),
        typeof(DeviceAction),
        typeof(ActionResult),
        typeof(StartupResult),
        typeof(TraversalStepResult),
        typeof(TraceEvent),
        typeof(RecoveryResult),
        typeof(CandidateAuthorizationEvidence),
        typeof(ViewportExplorationEvidence),
        typeof(BranchInventoryEvidence),
        typeof(BranchEffectCriterion),
        typeof(TargetGroundingEvidence),
        typeof(TargetGroundingCriterion),
    };

    [Fact]
    public void AllContractTypes_AreRecordsOrEnum()
    {
        foreach (var type in ContractTypes)
        {
            if (type.IsEnum)
                continue;

            Assert.True(type.IsClass, $"{type.Name} 应为 class（record）。");
            Assert.True(
                type.IsSealed || type.IsAbstract,
                $"{type.Name} 应为 sealed record 或 abstract record（discriminated union 基类）。");
            Assert.True(IsRecord(type), $"{type.Name} 不是 record（缺少合成成员）。");
        }
    }

    [Fact]
    public void UnionVariantRecords_AreSealed()
    {
        foreach (var variant in new[]
        {
            typeof(DeviceAction.LaunchApp), typeof(DeviceAction.Tap), typeof(DeviceAction.SetSwitch),
            typeof(StartupResult.Ready), typeof(StartupResult.NotReady),
            typeof(TraversalStepResult.Succeeded), typeof(TraversalStepResult.Failed),
            typeof(RecoveryResult.Verified), typeof(RecoveryResult.Failed),
        })
        {
            Assert.True(variant.IsSealed, $"{variant.Name} 应为 sealed record。");
        }
    }

    [Fact]
    public void AllModelTypes_AreImmutable()
    {
        foreach (var type in ModelTypes)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.True(
                    property.SetMethod is null || IsInitOnlySetter(property.SetMethod),
                    $"{type.Name}.{property.Name} 有可写 setter —— Model 必须不可变（I-2 跨 owner 快照）。");
            }
        }
    }

    [Fact]
    public void FieldContracts_AreExact()
    {
        AssertProperties(typeof(ObservedElement), "Bounds", "PerceptionType", "Text", "SwitchState", "Index");
        AssertProperties(typeof(Observation), "Elements", "ForegroundApplication", "SequenceNumber");
        AssertProperties(typeof(WorldBelief), "SemanticPage", "Confidence", "Evidence", "SourceObservationSequence");
        AssertProperties(typeof(RecoveryAnchor), "ApplicationIdentity", "ExpectedSemanticEntry", "VerificationCriteria", "RestoreRecipe", "EntryStrategy");
        AssertProperties(typeof(GoalEvidence), "Satisfied", "Reason", "SourceObservationSequence");
        AssertProperties(typeof(Goal), "EvidenceEvaluator", "CandidateAuthorizationEvaluator", "ViewportExplorationEvaluator", "BranchInventoryEvaluator", "DiscoveredBranchEffectCriterion", "CategoryClassifier");
        AssertProperties(typeof(BranchEffectCriterion), "BranchIdentity", "Evaluator");
        AssertProperties(typeof(TargetGroundingEvidence), "Supported", "Reason");
        AssertProperties(typeof(TargetGroundingCriterion), "CandidateEvaluator", "PostActionEvaluator");
        AssertProperties(typeof(CandidateAuthorizationEvidence), "Authorized", "Reason");
        AssertProperties(typeof(ViewportExplorationEvidence), "ContinueExploration", "Reason");
        AssertProperties(typeof(BranchInventoryEvidence), "RequiredBranchEvidence", "Reason");
        AssertProperties(typeof(Plan), "Steps");
        AssertProperties(
            typeof(PlanStep),
            "TargetDescription",
            "ActionDescription",
            "BranchEffectEvidenceEvaluator",
            "TargetGroundingCriterion");
        AssertProperties(typeof(ActionResult), "Outcome", "ActionDescription", "Info");
        AssertProperties(typeof(TraceEvent), "RunId", "ContainerId", "StepId", "ActionId", "Action", "Reason", "RunState", "TrapKind", "TrapScope", "RecoveryId");

        AssertProperties(typeof(DeviceAction.LaunchApp), "ApplicationId", "LaunchIntentAction");
        AssertProperties(typeof(DeviceAction.Tap), "TargetElementIndex", "TargetBounds");
        AssertProperties(typeof(DeviceAction.SetSwitch), "TargetElementIndex", "TargetState", "TargetBounds");
        AssertProperties(typeof(StartupResult.Ready), "Anchor");
        AssertProperties(typeof(StartupResult.NotReady), "Reason");
        AssertProperties(typeof(TraversalStepResult.Failed), "Reason");

        // ImmutableArray 集合约定
        Assert.Equal(typeof(ImmutableArray<ObservedElement>), typeof(Observation).GetProperty("Elements")!.PropertyType);
        Assert.Equal(typeof(ImmutableArray<PlanStep>), typeof(Plan).GetProperty("Steps")!.PropertyType);
        Assert.Equal(
            typeof(Func<Observation, bool?>),
            typeof(PlanStep).GetProperty("BranchEffectEvidenceEvaluator")!.PropertyType);
        Assert.Equal(
            typeof(TargetGroundingCriterion),
            typeof(PlanStep).GetProperty("TargetGroundingCriterion")!.PropertyType);
        Assert.Equal(
            typeof(Func<Observation, ObservedElement, CandidateAuthorizationEvidence>),
            typeof(Goal).GetProperty("CandidateAuthorizationEvaluator")!.PropertyType);
        Assert.Equal(
            typeof(Func<ImmutableArray<Observation>, ViewportExplorationEvidence>),
            typeof(Goal).GetProperty("ViewportExplorationEvaluator")!.PropertyType);
        Assert.Equal(
            typeof(Func<ImmutableArray<Observation>, int, BranchInventoryEvidence>),
            typeof(Goal).GetProperty("BranchInventoryEvaluator")!.PropertyType);

        // SC-P3-CAND-009 carrier field contract：exactly one optional immutable Goal association
        Assert.Equal(
            typeof(BranchEffectCriterion),
            typeof(Goal).GetProperty("DiscoveredBranchEffectCriterion")!.PropertyType);
        Assert.Equal(
            typeof(Func<Observation, bool?>),
            typeof(BranchEffectCriterion).GetProperty("Evaluator")!.PropertyType);
        Assert.Equal(typeof(string), typeof(BranchEffectCriterion).GetProperty("BranchIdentity")!.PropertyType);

        // 字段类型契约：WorldBelief / GoalEvidence / TraceEvent 的观测序号引用与可空性
        Assert.Equal(typeof(long?), typeof(WorldBelief).GetProperty("SourceObservationSequence")!.PropertyType);
        Assert.Equal(typeof(long?), typeof(GoalEvidence).GetProperty("SourceObservationSequence")!.PropertyType);
        Assert.Equal(typeof(bool?), typeof(ObservedElement).GetProperty("SwitchState")!.PropertyType);
        Assert.Equal(typeof(string), typeof(TraceEvent).GetProperty("RunId")!.PropertyType);
    }

    [Fact]
    public void NoDeferredTypesOrFields_LeakIntoModel()
    {
        // HG-1（A1+A6 原子修订）：Trap / TrapKind / TrapScope 已批准进入 Model（数据定义 — 裁决 4 Phase 2 购买）；
        // RecoveryRequest 仍 DEFER（恢复请求模型未购买）；裁决 9：无 ElementKind
        var bannedTypeNames = new[] { "RecoveryRequest", "ElementKind" };
        Assert.False(
            ModelTypes.Any(t => bannedTypeNames.Contains(t.Name)),
            "Model 层不应包含 Deferred 类型（RecoveryRequest / ElementKind）。");

        // 裁决 2：Observation 无 Fingerprint 字段（I-6 原则保留在宪章，字段 DEFER）
        Assert.Null(typeof(Observation).GetProperty("Fingerprint"));

        // 裁决 3 / SC-P1-005 断言 4：无 coordinate / hierarchy 字段
        // ElementBounds 是已购买的最小空间契约（docs/decisions/unified-spatial-evidence-challenge.md）—
        // 归一化 [0,1]×[0,1] bounds 作为空间证据，不是 coordinate-based grounding。
        // ObservedElement.Bounds 是该契约在 Observation 中的载体字段。
        var coordinateFieldNames = new[] { "X", "Y", "Rect", "Bounds", "Width", "Height", "Parent", "Children" };
        foreach (var type in ModelTypes)
        {
            if (type == typeof(ElementBounds))
                continue; // PURCHASED spatial evidence model

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (type == typeof(ObservedElement) && prop.Name == "Bounds")
                    continue; // PURCHASED spatial evidence carrier field

                Assert.False(
                    coordinateFieldNames.Contains(prop.Name),
                    $"{type.Name}.{prop.Name} 是 coordinate/hierarchy 字段 —— DEFER（裁决 3）。");
            }
        }

        // Model/Graph 保持空：无 Graph namespace 类型
        Assert.False(
            ModelTypes.Any(t => t.Namespace == "UniClaw.Runtime.Model.Graph"),
            "Model/Graph 应保持空（裁决：Graph 类型不在本切片）。");
    }

    [Fact]
    public void FailedAndNotReady_RejectEmptyReason()
    {
        Assert.Throws<ArgumentException>(() => new TraversalStepResult.Failed(""));
        Assert.Throws<ArgumentException>(() => new TraversalStepResult.Failed("   "));
        Assert.Throws<ArgumentException>(() => new StartupResult.NotReady(""));
        Assert.Throws<ArgumentException>(() => new RecoveryResult.Failed(""));
        Assert.Throws<ArgumentException>(() => new RecoveryResult.Failed("   "));
        Assert.Throws<ArgumentException>(() => new TraceEvent(""));
        Assert.Throws<ArgumentException>(() => new ViewportExplorationEvidence(true, ""));
        Assert.Throws<ArgumentException>(() => new ViewportExplorationEvidence(null, "   "));
    }

    private static IEnumerable<Type> ModelTypes =>
        typeof(RunState).Assembly.GetTypes()
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("UniClaw.Runtime.Model", StringComparison.Ordinal));

    private static bool IsRecord(Type type)
        => type.GetMethod("PrintMembers", BindingFlags.Instance | BindingFlags.NonPublic) is not null
        || type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.NonPublic) is not null;

    private static bool IsInitOnlySetter(MethodInfo setter)
        => setter.ReturnParameter.GetRequiredCustomModifiers()
            .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));

    private static void AssertProperties(Type type, params string[] expected)
    {
        var actual = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal).ToArray(), actual);
    }
}
