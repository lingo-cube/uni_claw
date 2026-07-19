using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Graph.Services;
using Xunit;

namespace UniClaw.Core.Tests.Graph;

// ===== EntryConfig Tests =====

public class EntryConfigTests
{
    [Fact(DisplayName = "EntryConfig: 默认值构造成功")]
    public void EntryConfig_DefaultValues_ConstructSuccessfully()
    {
        var config = new EntryConfig();
        Assert.Equal(WaitMode.Fast, config.WaitMode);
        Assert.Equal(10.0, config.WaitTimeoutSeconds);
        Assert.Equal(500, config.WaitIntervalMs);
        Assert.Equal(300, config.ActionDelayMs);
        Assert.Equal(TraceLevel.None, config.TraceLevel);
    }

    [Fact(DisplayName = "EntryConfig: WaitTimeoutSeconds=0 → DomainValidationException")]
    public void EntryConfig_RejectsZeroWaitTimeoutSeconds()
        => Assert.Throws<DomainValidationException>(() => new EntryConfig(WaitTimeoutSeconds: 0));

    [Fact(DisplayName = "EntryConfig: WaitTimeoutSeconds负值 → DomainValidationException")]
    public void EntryConfig_RejectsNegativeWaitTimeoutSeconds()
        => Assert.Throws<DomainValidationException>(() => new EntryConfig(WaitTimeoutSeconds: -1.0));

    [Fact(DisplayName = "EntryConfig: WaitIntervalMs=0 → DomainValidationException")]
    public void EntryConfig_RejectsZeroWaitIntervalMs()
        => Assert.Throws<DomainValidationException>(() => new EntryConfig(WaitIntervalMs: 0));

    [Fact(DisplayName = "EntryConfig: ActionDelayMs负值 → DomainValidationException")]
    public void EntryConfig_RejectsNegativeActionDelayMs()
        => Assert.Throws<DomainValidationException>(() => new EntryConfig(ActionDelayMs: -100));

    [Fact(DisplayName = "EntryConfig: 自定义合法值构造成功")]
    public void EntryConfig_ValidCustomValues_ConstructSuccessfully()
    {
        var config = new EntryConfig(WaitMode.Polling, 20.0, 1000, 500, TraceLevel.Detailed);
        Assert.Equal(WaitMode.Polling, config.WaitMode);
        Assert.Equal(20.0, config.WaitTimeoutSeconds);
        Assert.Equal(1000, config.WaitIntervalMs);
        Assert.Equal(500, config.ActionDelayMs);
        Assert.Equal(TraceLevel.Detailed, config.TraceLevel);
    }
}

// ===== TraversalPlan Tests =====

public class TraversalPlanTests
{
    [Fact(DisplayName = "TraversalPlan: 全部12字段构造成功")]
    public void TraversalPlan_WithAll12Fields_ConstructSuccessfully()
    {
        var plan = new TraversalPlan(
            EntryApp: "settings_app",
            EntryPolicy: new EntryPolicy(EntryStrategy.DirectDeeplink),
            PlanName: "test_plan",
            PlanId: "plan_001",
            EntryConfig: new EntryConfig(),
            RootNode: new TraversalNode("root", "root", NodeType.Screen,
                new Operation(OperationType.NoAction),
                new ChildrenStrategy(ChildrenStrategyType.DynamicMatch)),
            StaticNodes: new Dictionary<string, TraversalNode>(),
            TemplateRegistry: "full_interaction",
            Mode: TraversalMode.Hybrid,
            CompletionPolicy: new CompletionPolicy(),
            IntentSlots: new IntentSlots("settings_app", "full", ElementHandling: "full_interaction"));

        Assert.Equal("settings_app", plan.EntryApp);
        Assert.Equal("test_plan", plan.PlanName);
        Assert.Equal("plan_001", plan.PlanId);
        Assert.NotNull(plan.EntryConfig);
        Assert.Equal("full_interaction", plan.TemplateRegistry);
    }

    [Fact(DisplayName = "TraversalPlan: 空EntryApp → DomainValidationException")]
    public void TraversalPlan_RejectsEmptyEntryApp()
        => Assert.Throws<DomainValidationException>(() => new TraversalPlan(
            EntryApp: "", EntryPolicy: new EntryPolicy(EntryStrategy.ColdLaunch)));

    [Fact(DisplayName = "TraversalPlan: null EntryApp → DomainValidationException")]
    public void TraversalPlan_RejectsNullEntryApp()
        => Assert.Throws<DomainValidationException>(() => new TraversalPlan(
            EntryApp: null!, EntryPolicy: new EntryPolicy(EntryStrategy.ColdLaunch)));

    [Fact(DisplayName = "TraversalPlan: EntryConfig默认为null")]
    public void TraversalPlan_EntryConfig_DefaultsNull()
    {
        var plan = new TraversalPlan("app", new EntryPolicy(EntryStrategy.BindCurrentScreen));
        Assert.Null(plan.EntryConfig);
    }

    [Fact(DisplayName = "TraversalPlan: TemplateRegistry可为null")]
    public void TraversalPlan_TemplateRegistry_IsNullable()
    {
        var plan = new TraversalPlan("app", new EntryPolicy(EntryStrategy.BindCurrentScreen));
        Assert.Null(plan.TemplateRegistry);
    }

    [Fact(DisplayName = "TraversalPlan: 字段名StaticNodes而非Nodes")]
    public void TraversalPlan_StaticNodes_FieldNameCorrect()
    {
        Assert.NotNull(typeof(TraversalPlan).GetProperty("StaticNodes"));
        Assert.Null(typeof(TraversalPlan).GetProperty("Nodes"));
    }
}

// ===== PlanCompiler Tests =====

public class PlanCompilerTests
{
    [Fact(DisplayName = "PlanCompiler: TemplateSets含4个模板集")]
    public void TemplateSets_HasExactly4Values()
    {
        Assert.Equal(4, PlanCompiler.TemplateSets.Count);
        Assert.True(PlanCompiler.TemplateSets.ContainsKey("full_interaction"));
        Assert.True(PlanCompiler.TemplateSets.ContainsKey("menu_only"));
        Assert.True(PlanCompiler.TemplateSets.ContainsKey("safe_mode"));
        Assert.True(PlanCompiler.TemplateSets.ContainsKey("read_only"));
    }

    [Fact(DisplayName = "PlanCompiler: TemplateSets与Python源码匹配")]
    public void TemplateSets_MatchPythonSource()
    {
        Assert.Equal(ImmutableArray.Create("menu_container", "switch_leaf", "slider_leaf", "leaf_action"), PlanCompiler.TemplateSets["full_interaction"]);
        Assert.Equal(ImmutableArray.Create("menu_container"), PlanCompiler.TemplateSets["menu_only"]);
        Assert.Equal(ImmutableArray.Create("leaf_info"), PlanCompiler.TemplateSets["read_only"]);
    }

    [Fact(DisplayName = "PlanCompiler: MatchConditions与Python源码匹配")]
    public void MatchConditions_MatchPythonSource()
    {
        Assert.Equal("menu_item", PlanCompiler.MatchConditions["menu_container"].Type);
        Assert.Equal("switch", PlanCompiler.MatchConditions["switch_leaf"].Type);
        Assert.Equal("slider", PlanCompiler.MatchConditions["slider_leaf"].Type);
        Assert.Equal("button", PlanCompiler.MatchConditions["leaf_action"].Type);
        Assert.Null(PlanCompiler.MatchConditions["leaf_info"].Type);
    }

    // --- 3.1 调用点迁移 + 3.2 Scope 派生 Type ---

    [Fact(DisplayName = "PlanCompiler: target_only范围 → DynamicMatch + TargetFound(TargetName,Contains,MarkAndStop)")]
    public void Compile_TargetOnlyScope_DynamicMatchAndTargetFound()
    {
        var plan = new PlanCompiler().Compile(new IntentSlots("settings_app", "target_only", "wifi"));

        Assert.Equal(ChildrenStrategyType.DynamicMatch, plan.RootNode!.ChildrenStrategy.Type);
        Assert.Equal(CompletionPolicyType.TargetFound, plan.CompletionPolicy!.Type);
        Assert.Equal("wifi", plan.CompletionPolicy.TargetName);
        Assert.Equal(MatchMode.Contains, plan.CompletionPolicy.MatchMode);
        Assert.Equal(TargetFoundAction.MarkAndStop, plan.CompletionPolicy.ActionOnFound);
        Assert.Equal("settings_app", plan.EntryApp);
    }

    [Fact(DisplayName = "PlanCompiler: full范围 + full_interaction ElementHandling → DynamicMatch + Type=Exhaustive")]
    public void Compile_FullScope_FullInteraction_DynamicMatchAndExhaustive()
    {
        var plan = new PlanCompiler().Compile(new IntentSlots("settings_app", "full", ElementHandling: "full_interaction"));

        Assert.Equal(ChildrenStrategyType.DynamicMatch, plan.RootNode!.ChildrenStrategy.Type);
        Assert.Equal("full_interaction", plan.TemplateRegistry);
        Assert.Equal(CompletionPolicyType.Exhaustive, plan.CompletionPolicy!.Type);
    }

    [Fact(DisplayName = "PlanCompiler: full范围默认 ElementHandling=full_interaction")]
    public void Compile_FullScope_DefaultElementHandling_IsFullInteraction()
    {
        var plan = new PlanCompiler().Compile(new IntentSlots("settings_app", "full"));

        // 默认 ElementHandling=full_interaction → TemplateRegistry 反映之
        Assert.Equal("full_interaction", plan.TemplateRegistry);
        Assert.Equal(CompletionPolicyType.Exhaustive, plan.CompletionPolicy!.Type);
    }

    [Fact(DisplayName = "PlanCompiler: 合法 Scope {full, target_only} 均可编译")]
    public void Compile_ValidScopes_Accepted()
    {
        var compiler = new PlanCompiler();
        Assert.NotNull(compiler.Compile(new IntentSlots("app", "full")));
        Assert.NotNull(compiler.Compile(new IntentSlots("app", "target_only", "wifi")));
    }

    // --- 3.3 DynamicRules 来自 ElementHandling(非 Scope) ---

    [Fact(DisplayName = "PlanCompiler(P1): full + menu_only ElementHandling → 仅 menu_container 规则")]
    public void Compile_DynamicRules_FromElementHandling_NotScope()
    {
        var plan = new PlanCompiler().Compile(new IntentSlots("app", "full", ElementHandling: "menu_only"));
        var rules = plan.RootNode!.ChildrenStrategy.DynamicRules!;

        Assert.Single(rules);
        Assert.Contains("menu_container", rules.Keys);
    }

    [Fact(DisplayName = "PlanCompiler(P1): ElementHandling 变化驱动不同规则集")]
    public void Compile_DifferentElementHandling_DifferentRules()
    {
        var fullInteraction = new PlanCompiler()
            .Compile(new IntentSlots("app", "full", ElementHandling: "full_interaction"))
            .RootNode!.ChildrenStrategy.DynamicRules!;
        var readOnly = new PlanCompiler()
            .Compile(new IntentSlots("app", "full", ElementHandling: "read_only"))
            .RootNode!.ChildrenStrategy.DynamicRules!;

        Assert.Contains("switch_leaf", fullInteraction.Keys);
        Assert.DoesNotContain("switch_leaf", readOnly.Keys);
        Assert.Contains("leaf_info", readOnly.Keys);
    }

    // --- 3.4 Entry → RootNode 反射 + override 覆盖 Type ---

    [Fact(DisplayName = "PlanCompiler: Entry 反射到 RootNode.Name(默认 TargetApp)")]
    public void Compile_Entry_ReflectedInRootNodeName()
    {
        var defaultRoot = new PlanCompiler().Compile(new IntentSlots("app", "full"));
        Assert.Equal("app", defaultRoot.RootNode!.Name);

        var subMenuRoot = new PlanCompiler().Compile(new IntentSlots("app", "full", Entry: "network_subtree"));
        Assert.Equal("network_subtree", subMenuRoot.RootNode!.Name);
    }

    [Fact(DisplayName = "PlanCompiler(D2): full + max_steps override → Type=MaxSteps(覆盖 Type, 非 None)")]
    public void Compile_MaxStepsOverride_CoversType()
    {
        var plan = new PlanCompiler().Compile(new IntentSlots("app", "full", Completion: "max_steps"));

        Assert.Equal(CompletionPolicyType.MaxSteps, plan.CompletionPolicy!.Type);
        Assert.Equal(PlanCompiler.DefaultCompletionMaxSteps, plan.CompletionPolicy.MaxSteps);
    }

    [Fact(DisplayName = "PlanCompiler(D2): target_only + timeout override → Type=Timeout(覆盖 TargetFound)")]
    public void Compile_TimeoutOverride_CoversType()
    {
        var plan = new PlanCompiler().Compile(new IntentSlots("app", "target_only", "wifi", Completion: "timeout"));

        Assert.Equal(CompletionPolicyType.Timeout, plan.CompletionPolicy!.Type);
        Assert.Equal(PlanCompiler.DefaultCompletionTimeoutSeconds, plan.CompletionPolicy.TimeoutSeconds);
    }

    // --- 3.5 fail-fast ---

    [Fact(DisplayName = "PlanCompiler(P4): 未知 Completion override → DomainValidationException(非静默 None)")]
    public void Compile_UnknownCompletion_ThrowsDomainValidationException()
        => Assert.Throws<DomainValidationException>(() =>
            new PlanCompiler().Compile(new IntentSlots("app", "full", Completion: "bogus")));

    [Fact(DisplayName = "PlanCompiler(P2): target_path 作 Scope → DomainValidationException(词表已退役)")]
    public void Compile_TargetPathScope_ThrowsDomainValidationException()
        => Assert.Throws<DomainValidationException>(() =>
            new PlanCompiler().Compile(new IntentSlots("app", "target_path", "wifi")));

    [Fact(DisplayName = "PlanCompiler(P2): legacy element_handling 值作 Scope(full_interaction) → throw")]
    public void Compile_LegacyElementHandlingAsScope_ThrowsDomainValidationException()
        => Assert.Throws<DomainValidationException>(() =>
            new PlanCompiler().Compile(new IntentSlots("app", "full_interaction")));

    [Fact(DisplayName = "PlanCompiler: target_only 缺 Target → DomainValidationException")]
    public void Compile_TargetOnlyWithoutTarget_ThrowsDomainValidationException()
        => Assert.Throws<DomainValidationException>(() =>
            new PlanCompiler().Compile(new IntentSlots("app", "target_only")));

    [Fact(DisplayName = "PlanCompiler: 无效 ElementHandling → DomainValidationException")]
    public void Compile_InvalidElementHandling_ThrowsDomainValidationException()
        => Assert.Throws<DomainValidationException>(() =>
            new PlanCompiler().Compile(new IntentSlots("app", "full", ElementHandling: "bogus_handling")));

    [Fact(DisplayName = "PlanCompiler: 空targetApp → DomainValidationException")]
    public void Compile_RejectsEmptyTargetApp()
        => Assert.Throws<DomainValidationException>(() => new PlanCompiler().Compile(new IntentSlots("", "full")));

    [Fact(DisplayName = "PlanCompiler(H-4): 无效 Scope → DomainValidationException")]
    public void Compile_InvalidScope_ThrowsDomainValidationException()
        => Assert.Throws<DomainValidationException>(() => new PlanCompiler().Compile(new IntentSlots("app", "invalid_scope")));

    [Fact(DisplayName = "PlanCompiler: Depth负值 → DomainValidationException")]
    public void Compile_NegativeDepth_ThrowsDomainValidationException()
        => Assert.Throws<DomainValidationException>(() =>
            new PlanCompiler().Compile(new IntentSlots("app", "full", Depth: -1)));
}

// ===== DynamicMatcher Tests =====

public class DynamicMatcherTests
{
    private readonly DynamicMatcher _matcher = new();

    [Fact(DisplayName = "DynamicMatcher: 仅Type条件匹配MenuItemType成功")]
    public void Match_TypeOnly_MatchesMenuItemType() => Assert.True(_matcher.Match(new MatchCondition(Type: "switch"), new MatchableItem(MenuItemType: MenuItemType.Switch)).Matched);

    [Fact(DisplayName = "DynamicMatcher: Type不匹配 → 匹配失败")]
    public void Match_TypeMismatch_Fails() => Assert.False(_matcher.Match(new MatchCondition(Type: "switch"), new MatchableItem(MenuItemType: MenuItemType.Button)).Matched);

    [Fact(DisplayName = "DynamicMatcher: ExpectedAction条件匹配成功")]
    public void Match_ExpectedAction_Matches() => Assert.True(_matcher.Match(new MatchCondition(ExpectedAction: "toggle"), new MatchableItem(ExpectedAction: ExpectedAction.Toggle)).Matched);

    [Fact(DisplayName = "DynamicMatcher: Index范围条件约束项目成功")]
    public void Match_IndexRange_BoundsItem() => Assert.True(_matcher.Match(new MatchCondition(MinIndex: 2, MaxIndex: 5), new MatchableItem(Index: 3)).Matched);

    [Fact(DisplayName = "DynamicMatcher: Index越界 → 匹配失败")]
    public void Match_IndexRange_RejectsOutOfBounds() => Assert.False(_matcher.Match(new MatchCondition(MinIndex: 2, MaxIndex: 5), new MatchableItem(Index: 7)).Matched);

    [Fact(DisplayName = "DynamicMatcher: MinIndex=null允许任意下界")]
    public void Match_NullMinIndex_AllowsAnyLowerBound() => Assert.True(_matcher.Match(new MatchCondition(MinIndex: null, MaxIndex: 5), new MatchableItem(Index: 0)).Matched);

    [Fact(DisplayName = "DynamicMatcher: MaxIndex=null允许任意上界")]
    public void Match_NullMaxIndex_AllowsAnyUpperBound() => Assert.True(_matcher.Match(new MatchCondition(MinIndex: 2, MaxIndex: null), new MatchableItem(Index: 100)).Matched);

    [Fact(DisplayName = "DynamicMatcher: Custom字典键值对全匹配成功")]
    public void Match_CustomDict_MatchesAllKeyValuePairs()
    {
        var condition = new MatchCondition(Custom: new Dictionary<string, object> { ["role"] = "navigation", ["level"] = "1" });
        var item = new MatchableItem(Metadata: new Dictionary<string, string> { ["role"] = "navigation", ["level"] = "1" });
        Assert.True(_matcher.Match(condition, item).Matched);
    }

    [Fact(DisplayName = "DynamicMatcher: Custom字典值不匹配 → 失败")]
    public void Match_CustomDict_FailsOnMismatchedValue()
    {
        var condition = new MatchCondition(Custom: new Dictionary<string, object> { ["role"] = "navigation" });
        var item = new MatchableItem(Metadata: new Dictionary<string, string> { ["role"] = "content" });
        Assert.False(_matcher.Match(condition, item).Matched);
    }

    [Fact(DisplayName = "DynamicMatcher: 合取逻辑, 所有条件必须满足(部分不满足 → 失败)")]
    public void Match_ConjunctiveLogic_AllConditionsMustPass()
        => Assert.False(_matcher.Match(new MatchCondition(Type: "switch", ExpectedAction: "click"), new MatchableItem(MenuItemType: MenuItemType.Switch, ExpectedAction: ExpectedAction.Navigate)).Matched);

    [Fact(DisplayName = "DynamicMatcher: 空条件匹配任意项目")]
    public void Match_EmptyCondition_MatchesEverything()
        => Assert.True(_matcher.Match(new MatchCondition(), new MatchableItem(MenuItemType: MenuItemType.Item)).Matched);

    // M-9: TextMatchMode

    [Fact(DisplayName = "枚举守卫: TextMatchMode应有2个值")]
    public void TextMatchMode_HasExactly2Values() => Assert.Equal(2, Enum.GetValues<TextMatchMode>().Length);

    [Fact(DisplayName = "DynamicMatcher: Exact模式匹配完全相同字符串")]
    public void Match_ExactMode_MatchesIdenticalString()
        => Assert.True(_matcher.Match(new MatchCondition(TextPattern: "Settings", TextMatchMode: TextMatchMode.Exact), new MatchableItem(Text: "Settings")).Matched);

    [Fact(DisplayName = "DynamicMatcher: Exact模式拒绝子串匹配")]
    public void Match_ExactMode_RejectsSubstringMatch()
        => Assert.False(_matcher.Match(new MatchCondition(TextPattern: "Settings", TextMatchMode: TextMatchMode.Exact), new MatchableItem(Text: "Network Settings")).Matched);

    [Fact(DisplayName = "DynamicMatcher: Contains模式匹配子串")]
    public void Match_ContainsMode_MatchesSubstring()
        => Assert.True(_matcher.Match(new MatchCondition(TextPattern: "Settings", TextMatchMode: TextMatchMode.Contains), new MatchableItem(Text: "Network Settings")).Matched);

    [Fact(DisplayName = "DynamicMatcher: 默认TextMatchMode为Contains")]
    public void MatchCondition_DefaultTextMatchMode_IsContains()
        => Assert.Equal(TextMatchMode.Contains, new MatchCondition(TextPattern: "Settings").TextMatchMode);
}

// ===== TemplateInstantiator Tests =====

public class TemplateInstantiatorTests
{
    [Fact(DisplayName = "模板实例化: 解析占位符{{item_text}} → WiFi")]
    public void Instantiate_ResolvesPlaceholders()
    {
        var instantiator = new TemplateInstantiator();
        var template = new Template(TemplateId: "switch_leaf", NodeType: NodeType.LeafSwitch,
            Operation: new Dictionary<string, object> { ["action"] = "click", ["target"] = new Dictionary<string, object> { ["by"] = "text", ["value"] = "{{item_text}}" } });
        var result = instantiator.Instantiate(template, new Dictionary<string, object> { ["item_text"] = "WiFi" }, new List<string>());
        Assert.NotNull(result.Operation.Target);
        Assert.Equal("WiFi", result.Operation.Target.Value);
    }

    [Fact(DisplayName = "模板实例化: 构建完整TraversalNode(Operation+ChildrenStrategy+ErrorPolicy)")]
    public void Instantiate_ConstructsCompleteTraversalNode()
    {
        var result = new TemplateInstantiator().Instantiate(
            new Template(TemplateId: "menu_container", NodeType: NodeType.Container,
                Operation: new Dictionary<string, object> { ["action"] = "click" },
                ChildrenStrategy: new Dictionary<string, object> { ["type"] = "dynamic_match" },
                ErrorPolicy: new Dictionary<string, object> { ["on_error"] = "retry", ["max_retries"] = 2 }),
            new Dictionary<string, object>(), new List<string>());
        Assert.Equal(OperationType.Click, result.Operation.Action);
        Assert.Equal(ChildrenStrategyType.DynamicMatch, result.ChildrenStrategy.Type);
        Assert.Equal(ErrorPolicyType.Retry, result.ErrorPolicy!.OnError);
    }

    [Fact(DisplayName = "模板实例化: V69路径拼接(parentPath+templateId)")]
    public void Instantiate_V69PathConcatenation()
    {
        var result = new TemplateInstantiator().Instantiate(
            new Template(TemplateId: "wifi_switch", NodeType: NodeType.LeafSwitch,
                Operation: new Dictionary<string, object> { ["action"] = "click" }),
            new Dictionary<string, object>(), new List<string> { "home", "settings" });
        Assert.Equal(new List<string> { "home", "settings", "wifi_switch" }, result.Precondition!.Path);
    }

    [Fact(DisplayName = "模板实例化: 空父路径 → Precondition.Path仅含模板ID")]
    public void Instantiate_EmptyParentPath_SingleNodeName()
    {
        var result = new TemplateInstantiator().Instantiate(
            new Template(TemplateId: "root_menu", NodeType: NodeType.Screen,
                Operation: new Dictionary<string, object> { ["action"] = "no_action" }),
            new Dictionary<string, object>(), new List<string>());
        Assert.Equal(new List<string> { "root_menu" }, result.Precondition!.Path);
    }
}
