using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using Xunit;

namespace UniClaw.Core.Tests.Graph;

// ===== EntryConfig Tests =====

public class EntryConfigTests
{
    [Fact]
    public void EntryConfig_DefaultValues_ConstructSuccessfully()
    {
        var config = new EntryConfig();
        Assert.Equal(WaitMode.Fast, config.WaitMode);
        Assert.Equal(10.0, config.WaitTimeoutSeconds);
        Assert.Equal(500, config.WaitIntervalMs);
        Assert.Equal(300, config.ActionDelayMs);
        Assert.Equal(TraceLevel.None, config.TraceLevel);
    }

    [Fact]
    public void EntryConfig_RejectsZeroWaitTimeoutSeconds()
        => Assert.Throws<DomainValidationException>(() => new EntryConfig(WaitTimeoutSeconds: 0));

    [Fact]
    public void EntryConfig_RejectsNegativeWaitTimeoutSeconds()
        => Assert.Throws<DomainValidationException>(() => new EntryConfig(WaitTimeoutSeconds: -1.0));

    [Fact]
    public void EntryConfig_RejectsZeroWaitIntervalMs()
        => Assert.Throws<DomainValidationException>(() => new EntryConfig(WaitIntervalMs: 0));

    [Fact]
    public void EntryConfig_RejectsNegativeActionDelayMs()
        => Assert.Throws<DomainValidationException>(() => new EntryConfig(ActionDelayMs: -100));

    [Fact]
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
    [Fact]
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
            IntentSlots: new IntentSlots("settings_app", "full_interaction"));

        Assert.Equal("settings_app", plan.EntryApp);
        Assert.Equal("test_plan", plan.PlanName);
        Assert.Equal("plan_001", plan.PlanId);
        Assert.NotNull(plan.EntryConfig);
        Assert.Equal("full_interaction", plan.TemplateRegistry);
    }

    [Fact]
    public void TraversalPlan_RejectsEmptyEntryApp()
        => Assert.Throws<DomainValidationException>(() => new TraversalPlan(
            EntryApp: "", EntryPolicy: new EntryPolicy(EntryStrategy.ColdLaunch)));

    [Fact]
    public void TraversalPlan_RejectsNullEntryApp()
        => Assert.Throws<DomainValidationException>(() => new TraversalPlan(
            EntryApp: null!, EntryPolicy: new EntryPolicy(EntryStrategy.ColdLaunch)));

    [Fact]
    public void TraversalPlan_EntryConfig_DefaultsNull()
    {
        var plan = new TraversalPlan("app", new EntryPolicy(EntryStrategy.BindCurrentScreen));
        Assert.Null(plan.EntryConfig);
    }

    [Fact]
    public void TraversalPlan_TemplateRegistry_IsNullable()
    {
        var plan = new TraversalPlan("app", new EntryPolicy(EntryStrategy.BindCurrentScreen));
        Assert.Null(plan.TemplateRegistry);
    }

    [Fact]
    public void TraversalPlan_StaticNodes_FieldNameCorrect()
    {
        Assert.NotNull(typeof(TraversalPlan).GetProperty("StaticNodes"));
        Assert.Null(typeof(TraversalPlan).GetProperty("Nodes"));
    }
}

// ===== PlanCompiler Tests =====

public class PlanCompilerTests
{
    [Fact]
    public void TemplateSets_HasExactly4Values()
    {
        Assert.Equal(4, PlanCompiler.TemplateSets.Count);
        Assert.True(PlanCompiler.TemplateSets.ContainsKey("full_interaction"));
        Assert.True(PlanCompiler.TemplateSets.ContainsKey("menu_only"));
        Assert.True(PlanCompiler.TemplateSets.ContainsKey("safe_mode"));
        Assert.True(PlanCompiler.TemplateSets.ContainsKey("read_only"));
    }

    [Fact]
    public void TemplateSets_MatchPythonSource()
    {
        Assert.Equal(ImmutableArray.Create("menu_container", "switch_leaf", "slider_leaf", "leaf_action"), PlanCompiler.TemplateSets["full_interaction"]);
        Assert.Equal(ImmutableArray.Create("menu_container"), PlanCompiler.TemplateSets["menu_only"]);
        Assert.Equal(ImmutableArray.Create("leaf_info"), PlanCompiler.TemplateSets["read_only"]);
    }

    [Fact]
    public void Compile_TargetPathScope_StaticChildrenStrategy()
    {
        var compiler = new PlanCompiler();
        var plan = compiler.Compile(new IntentSlots("settings_app", "target_path", "wifi"));
        Assert.Equal(ChildrenStrategyType.Static, plan.RootNode!.ChildrenStrategy.Type);
    }

    [Fact]
    public void Compile_FullInteractionScope_DynamicMatchStrategy()
    {
        var compiler = new PlanCompiler();
        var plan = compiler.Compile(new IntentSlots("settings_app", "full_interaction"));
        Assert.Equal(ChildrenStrategyType.DynamicMatch, plan.RootNode!.ChildrenStrategy.Type);
        Assert.Equal("full_interaction", plan.TemplateRegistry);
    }

    [Fact]
    public void Compile_RejectsEmptyTargetApp()
        => Assert.Throws<DomainValidationException>(() => new PlanCompiler().Compile(new IntentSlots("", "full_interaction")));

    [Fact]
    public void MatchConditions_MatchPythonSource()
    {
        Assert.Equal("menu_item", PlanCompiler.MatchConditions["menu_container"].Type);
        Assert.Equal("switch", PlanCompiler.MatchConditions["switch_leaf"].Type);
        Assert.Equal("slider", PlanCompiler.MatchConditions["slider_leaf"].Type);
        Assert.Equal("button", PlanCompiler.MatchConditions["leaf_action"].Type);
        Assert.Null(PlanCompiler.MatchConditions["leaf_info"].Type);
    }

    // H-4: scope validation

    [Fact]
    public void Compile_InvalidScope_ThrowsDomainValidationException()
        => Assert.Throws<DomainValidationException>(() => new PlanCompiler().Compile(new IntentSlots("app", "invalid_scope")));

    [Fact]
    public void Compile_ValidScopes_Accepted()
    {
        var compiler = new PlanCompiler();
        foreach (var scope in new[] { "full_interaction", "menu_only", "safe_mode", "read_only", "target_path" })
        {
            var slots = scope == "target_path" ? new IntentSlots("app", scope, "wifi") : new IntentSlots("app", scope);
            Assert.NotNull(compiler.Compile(slots));
        }
    }
}

// ===== DynamicMatcher Tests =====

public class DynamicMatcherTests
{
    private readonly DynamicMatcher _matcher = new();

    [Fact] public void Match_TypeOnly_MatchesMenuItemType() => Assert.True(_matcher.Match(new MatchCondition(Type: "switch"), new MatchableItem(MenuItemType: MenuItemType.Switch)).Matched);
    [Fact] public void Match_TypeMismatch_Fails() => Assert.False(_matcher.Match(new MatchCondition(Type: "switch"), new MatchableItem(MenuItemType: MenuItemType.Button)).Matched);
    [Fact] public void Match_ExpectedAction_Matches() => Assert.True(_matcher.Match(new MatchCondition(ExpectedAction: "toggle"), new MatchableItem(ExpectedAction: ExpectedAction.Toggle)).Matched);
    [Fact] public void Match_IndexRange_BoundsItem() => Assert.True(_matcher.Match(new MatchCondition(MinIndex: 2, MaxIndex: 5), new MatchableItem(Index: 3)).Matched);
    [Fact] public void Match_IndexRange_RejectsOutOfBounds() => Assert.False(_matcher.Match(new MatchCondition(MinIndex: 2, MaxIndex: 5), new MatchableItem(Index: 7)).Matched);
    [Fact] public void Match_NullMinIndex_AllowsAnyLowerBound() => Assert.True(_matcher.Match(new MatchCondition(MinIndex: null, MaxIndex: 5), new MatchableItem(Index: 0)).Matched);
    [Fact] public void Match_NullMaxIndex_AllowsAnyUpperBound() => Assert.True(_matcher.Match(new MatchCondition(MinIndex: 2, MaxIndex: null), new MatchableItem(Index: 100)).Matched);

    [Fact]
    public void Match_CustomDict_MatchesAllKeyValuePairs()
    {
        var condition = new MatchCondition(Custom: new Dictionary<string, object> { ["role"] = "navigation", ["level"] = "1" });
        var item = new MatchableItem(Metadata: new Dictionary<string, string> { ["role"] = "navigation", ["level"] = "1" });
        Assert.True(_matcher.Match(condition, item).Matched);
    }

    [Fact]
    public void Match_CustomDict_FailsOnMismatchedValue()
    {
        var condition = new MatchCondition(Custom: new Dictionary<string, object> { ["role"] = "navigation" });
        var item = new MatchableItem(Metadata: new Dictionary<string, string> { ["role"] = "content" });
        Assert.False(_matcher.Match(condition, item).Matched);
    }

    [Fact]
    public void Match_ConjunctiveLogic_AllConditionsMustPass()
        => Assert.False(_matcher.Match(new MatchCondition(Type: "switch", ExpectedAction: "click"), new MatchableItem(MenuItemType: MenuItemType.Switch, ExpectedAction: ExpectedAction.Navigate)).Matched);

    [Fact]
    public void Match_EmptyCondition_MatchesEverything()
        => Assert.True(_matcher.Match(new MatchCondition(), new MatchableItem(MenuItemType: MenuItemType.Item)).Matched);

    // M-9: TextMatchMode

    [Fact] public void TextMatchMode_HasExactly2Values() => Assert.Equal(2, Enum.GetValues<TextMatchMode>().Length);

    [Fact]
    public void Match_ExactMode_MatchesIdenticalString()
        => Assert.True(_matcher.Match(new MatchCondition(TextPattern: "Settings", TextMatchMode: TextMatchMode.Exact), new MatchableItem(Text: "Settings")).Matched);

    [Fact]
    public void Match_ExactMode_RejectsSubstringMatch()
        => Assert.False(_matcher.Match(new MatchCondition(TextPattern: "Settings", TextMatchMode: TextMatchMode.Exact), new MatchableItem(Text: "Network Settings")).Matched);

    [Fact]
    public void Match_ContainsMode_MatchesSubstring()
        => Assert.True(_matcher.Match(new MatchCondition(TextPattern: "Settings", TextMatchMode: TextMatchMode.Contains), new MatchableItem(Text: "Network Settings")).Matched);

    [Fact]
    public void MatchCondition_DefaultTextMatchMode_IsContains()
        => Assert.Equal(TextMatchMode.Contains, new MatchCondition(TextPattern: "Settings").TextMatchMode);
}

// ===== TemplateInstantiator Tests =====

public class TemplateInstantiatorTests
{
    [Fact]
    public void Instantiate_ResolvesPlaceholders()
    {
        var instantiator = new TemplateInstantiator();
        var template = new Template(TemplateId: "switch_leaf", NodeType: NodeType.LeafSwitch,
            Operation: new Dictionary<string, object> { ["action"] = "click", ["target"] = new Dictionary<string, object> { ["by"] = "text", ["value"] = "{{item_text}}" } });
        var result = instantiator.Instantiate(template, new Dictionary<string, object> { ["item_text"] = "WiFi" }, new List<string>());
        Assert.NotNull(result.Operation.Target);
        Assert.Equal("WiFi", result.Operation.Target.Value);
    }

    [Fact]
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

    [Fact]
    public void Instantiate_V69PathConcatenation()
    {
        var result = new TemplateInstantiator().Instantiate(
            new Template(TemplateId: "wifi_switch", NodeType: NodeType.LeafSwitch,
                Operation: new Dictionary<string, object> { ["action"] = "click" }),
            new Dictionary<string, object>(), new List<string> { "home", "settings" });
        Assert.Equal(new List<string> { "home", "settings", "wifi_switch" }, result.Precondition!.Path);
    }

    [Fact]
    public void Instantiate_EmptyParentPath_SingleNodeName()
    {
        var result = new TemplateInstantiator().Instantiate(
            new Template(TemplateId: "root_menu", NodeType: NodeType.Screen,
                Operation: new Dictionary<string, object> { ["action"] = "no_action" }),
            new Dictionary<string, object>(), new List<string>());
        Assert.Equal(new List<string> { "root_menu" }, result.Precondition!.Path);
    }
}
