using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json.Serialization;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Domain.Models.Vision;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Graph.Services;
using UniClaw.Core.StateMachine;
using Xunit;

namespace UniClaw.Core.Tests.Graph;

// ===== Fail-Fast Validation Baseline (C-1 ~ C-4 + C-8) =====
// Spec scenarios: openspec/changes/fail-fast-validation-baseline/specs/

public class FailFastValidationBaselineTests
{
    // ── C-1: Graph model constructor validation ──

    [Fact(DisplayName = "C-1: Precondition.TimeoutSeconds=0 → DomainValidationException")]
    public void Precondition_RejectsZeroTimeout()
        => Assert.Throws<DomainValidationException>(() => new Precondition(TimeoutSeconds: 0));

    [Fact(DisplayName = "C-1: Precondition.TimeoutSeconds=300 构造成功（闭上界）")]
    public void Precondition_AcceptsUpperBound()
        => Assert.Equal(300, new Precondition(TimeoutSeconds: 300).TimeoutSeconds);

    [Fact(DisplayName = "C-1: DynamicRule 空 RuleId → DomainValidationException")]
    public void DynamicRule_RejectsEmptyRuleId()
        => Assert.Throws<DomainValidationException>(() => new DynamicRule(
            "", new MatchCondition(), "tpl", MatchAction.GenerateChild));

    [Fact(DisplayName = "C-1: DynamicRule 空 ChildTemplate → DomainValidationException")]
    public void DynamicRule_RejectsEmptyChildTemplate()
        => Assert.Throws<DomainValidationException>(() => new DynamicRule(
            "r1", new MatchCondition(), " ", MatchAction.GenerateChild));

    [Fact(DisplayName = "C-1: ChildrenStrategy.MaxChildren=-1 → DomainValidationException")]
    public void ChildrenStrategy_RejectsNegativeMaxChildren()
        => Assert.Throws<DomainValidationException>(() =>
            new ChildrenStrategy(ChildrenStrategyType.None, MaxChildren: -1));

    [Fact(DisplayName = "C-1: ChildrenStrategy.MaxChildren=10000 构造成功（闭上界）")]
    public void ChildrenStrategy_AcceptsUpperBound()
        => Assert.Equal(10000, new ChildrenStrategy(ChildrenStrategyType.None, MaxChildren: 10000).MaxChildren);

    [Fact(DisplayName = "C-1: ErrorPolicy.MaxRetries=200 → DomainValidationException")]
    public void ErrorPolicy_RejectsExcessiveMaxRetries()
        => Assert.Throws<DomainValidationException>(() =>
            new ErrorPolicy(ErrorPolicyType.Retry, MaxRetries: 200));


    [Fact(DisplayName = "C-1: CompletionPolicy TargetFound 空 TargetName → DomainValidationException")]
    public void CompletionPolicy_RejectsTargetFoundWithoutTargetName()
        => Assert.Throws<DomainValidationException>(() =>
            new CompletionPolicy(CompletionPolicyType.TargetFound, TargetName: " "));

    [Fact(DisplayName = "C-1: CompletionPolicy.MaxSteps=0 → DomainValidationException")]
    public void CompletionPolicy_RejectsZeroMaxSteps()
        => Assert.Throws<DomainValidationException>(() =>
            new CompletionPolicy(CompletionPolicyType.MaxSteps, MaxSteps: 0));

    [Fact(DisplayName = "C-1: EntryPolicy.TimeoutSeconds=500 → DomainValidationException")]
    public void EntryPolicy_RejectsExcessiveTimeout()
        => Assert.Throws<DomainValidationException>(() =>
            new EntryPolicy(EntryStrategy.ColdLaunch, TimeoutSeconds: 500));

    [Fact(DisplayName = "C-1: TraversalNode 空 NodeId → DomainValidationException")]
    public void TraversalNode_RejectsEmptyNodeId()
        => Assert.Throws<DomainValidationException>(() => new TraversalNode(
            "", "n", NodeType.Screen, new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.None)));

    [Fact(DisplayName = "C-1: TraversalNode 空 Name → DomainValidationException")]
    public void TraversalNode_RejectsEmptyName()
        => Assert.Throws<DomainValidationException>(() => new TraversalNode(
            "id", null!, NodeType.Screen, new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.None)));

    // ── C-4: TraversalPlan root node validation ──

    [Fact(DisplayName = "C-4: TraversalPlan 非容器根节点 → DomainValidationException")]
    public void TraversalPlan_RejectsNonContainerRoot()
        => Assert.Throws<DomainValidationException>(() => new TraversalPlan(
            "app", new EntryPolicy(EntryStrategy.BindCurrentScreen),
            RootNode: new TraversalNode("root", "root", NodeType.LeafSwitch,
                new Operation(OperationType.NoAction), new ChildrenStrategy(ChildrenStrategyType.None))));

    [Fact(DisplayName = "C-4: TraversalPlan 根节点操作非 NoAction → DomainValidationException")]
    public void TraversalPlan_RejectsNonNoActionRoot()
        => Assert.Throws<DomainValidationException>(() => new TraversalPlan(
            "app", new EntryPolicy(EntryStrategy.BindCurrentScreen),
            RootNode: new TraversalNode("root", "root", NodeType.Screen,
                new Operation(OperationType.Click), new ChildrenStrategy(ChildrenStrategyType.None))));

    [Fact(DisplayName = "C-4: TraversalPlan null RootNode 允许（引擎 BuildDefaultRoot 兜底）")]
    public void TraversalPlan_AllowsNullRoot()
    {
        var plan = new TraversalPlan("app", new EntryPolicy(EntryStrategy.BindCurrentScreen));
        Assert.Null(plan.RootNode);
    }

    // ── C-2: PlaceholderResolver fail-fast ──

    [Fact(DisplayName = "C-2: PlaceholderResolver 未解析占位符 → DomainValidationException")]
    public void PlaceholderResolver_RejectsUnresolvedPlaceholder()
        => Assert.Throws<DomainValidationException>(() =>
            PlaceholderResolver.Resolve("click {{unknown}}", new Dictionary<string, object>()));

    [Fact(DisplayName = "C-2: PlaceholderResolver 已解析占位符正常返回")]
    public void PlaceholderResolver_ResolvesKnownPlaceholder()
        => Assert.Equal("click WiFi",
            PlaceholderResolver.Resolve("click {{item_text}}",
                new Dictionary<string, object> { ["item_text"] = "WiFi" }));

    // ── C-3: ErrorPolicy wiring ──

    [Fact(DisplayName = "C-3: ErrorPolicy.MaxRetries 覆盖默认 → Retry 在 RetryCount=4 时仍可选")]
    public void ErrorPolicy_MaxRetriesOverridesDefault()
    {
        var selector = new ErrorStrategySelector();
        var ctx = new StrategySelectionContext(4, 3, true, 5, true,
            ErrorPolicy: new ErrorPolicy(ErrorPolicyType.Retry, MaxRetries: 5));
        // RetryCount=4 >= 默认 3 (会跳过 Retry)，但 policy MaxRetries=5 → 仍选 Retry
        Assert.Equal(ErrorStrategy.Retry, selector.SelectStrategy(ErrorType.Timeout, ctx));
    }

    [Fact(DisplayName = "C-3: null ErrorPolicy 保留默认硬编码行为")]
    public void ErrorPolicy_NullPreservesDefault()
    {
        var selector = new ErrorStrategySelector();
        var ctx = new StrategySelectionContext(0, 3, true, 5, true); // 无 ErrorPolicy
        Assert.Equal(ErrorStrategy.Retry, selector.SelectStrategy(ErrorType.Timeout, ctx));
    }

    [Fact(DisplayName = "C-3: ErrorPolicy.OnError=Abort → 直接 Abort")]
    public void ErrorPolicy_OnErrorAbortMapsToAbort()
    {
        var selector = new ErrorStrategySelector();
        var ctx = new StrategySelectionContext(0, 3, true, 5, true,
            ErrorPolicy: new ErrorPolicy(ErrorPolicyType.Abort));
        Assert.Equal(ErrorStrategy.Abort, selector.SelectStrategy(ErrorType.Timeout, ctx));
    }

    // ── C-8: Domain P3 items ──

    [Fact(DisplayName = "C-8: Region.Id 空 → DomainValidationException")]
    public void Region_RejectsEmptyId()
        => Assert.Throws<DomainValidationException>(() =>
            new Region("", new BoundingBox(0, 0, 1, 1), RegionRole.Content));

    [Fact(DisplayName = "C-8: IsCanonical 精确规范名 true，别名/未知 false")]
    public void IsCanonical_DistinguishesExactFromAlias()
    {
        Assert.True(TypeHintExtensions.IsCanonical("clickable_text"));
        Assert.True(TypeHintExtensions.IsCanonical("input_field"));
        Assert.False(TypeHintExtensions.IsCanonical("clickable"));  // 别名
        Assert.False(TypeHintExtensions.IsCanonical("toggle"));     // 别名
        Assert.False(TypeHintExtensions.IsCanonical("unknown"));    // 未知
    }

    [Fact(DisplayName = "C-8: ContentNode.ToMarkdown 按层级缩进 + 类型后缀")]
    public void ContentNode_ToMarkdown_RendersLevelIndentAndTypeSuffix()
    {
        var root = new ContentNode("n0", "Root", 1, NodeType: "item");
        var child = new ContentNode("n1", "Popup", 2, NodeType: "popup");
        Assert.Equal("n0. Root\n", root.ToMarkdown());
        Assert.Equal("  n1. Popup (popup)\n", child.ToMarkdown());
    }

    [Fact(DisplayName = "C-8: TypeHint 8 值均有 [JsonPropertyName] 元数据（与其他 Domain enum 一致）")]
    public void TypeHint_HasJsonPropertyNameAttributes()
    {
        // 注: JsonStringEnumConverter(CamelCase) 忽略 enum 成员的 [JsonPropertyName]，序列化仍为 camelCase；
        // 此 attribute 作为反射元数据（与 MenuItemType/Direction 一致），供字符串集构建等使用。
        foreach (TypeHint value in System.Enum.GetValues(typeof(TypeHint)))
        {
            var attr = typeof(TypeHint).GetField(value.ToString())
                ?.GetCustomAttribute<JsonPropertyNameAttribute>();
            Assert.True(attr is not null, $"{value} 缺 [JsonPropertyName]");
        }
    }
}
