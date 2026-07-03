using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;
using Xunit;

namespace UniClaw.Core.Tests.Phase2;

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
    {
        Assert.Throws<DomainValidationException>(() => new EntryConfig(WaitTimeoutSeconds: 0));
    }

    [Fact]
    public void EntryConfig_RejectsNegativeWaitTimeoutSeconds()
    {
        Assert.Throws<DomainValidationException>(() => new EntryConfig(WaitTimeoutSeconds: -1.0));
    }

    [Fact]
    public void EntryConfig_RejectsZeroWaitIntervalMs()
    {
        Assert.Throws<DomainValidationException>(() => new EntryConfig(WaitIntervalMs: 0));
    }

    [Fact]
    public void EntryConfig_RejectsNegativeActionDelayMs()
    {
        Assert.Throws<DomainValidationException>(() => new EntryConfig(ActionDelayMs: -100));
    }

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
    {
        Assert.Throws<DomainValidationException>(() => new TraversalPlan(
            EntryApp: "",
            EntryPolicy: new EntryPolicy(EntryStrategy.ColdLaunch)));
    }

    [Fact]
    public void TraversalPlan_RejectsNullEntryApp()
    {
        Assert.Throws<DomainValidationException>(() => new TraversalPlan(
            EntryApp: null!,
            EntryPolicy: new EntryPolicy(EntryStrategy.ColdLaunch)));
    }

    [Fact]
    public void TraversalPlan_EntryConfig_DefaultsNull()
    {
        var plan = new TraversalPlan(
            EntryApp: "app",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen));
        Assert.Null(plan.EntryConfig);
    }

    [Fact]
    public void TraversalPlan_TemplateRegistry_IsNullable()
    {
        var plan = new TraversalPlan(
            EntryApp: "app",
            EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen));
        Assert.Null(plan.TemplateRegistry);
    }

    [Fact]
    public void TraversalPlan_StaticNodes_FieldNameCorrect()
    {
        var prop = typeof(TraversalPlan).GetProperty("StaticNodes");
        Assert.NotNull(prop);
        Assert.Null(typeof(TraversalPlan).GetProperty("Nodes"));
    }
}

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
        Assert.Equal(
            ImmutableArray.Create("menu_container", "switch_leaf", "slider_leaf", "leaf_action"),
            PlanCompiler.TemplateSets["full_interaction"]);
        Assert.Equal(
            ImmutableArray.Create("menu_container"),
            PlanCompiler.TemplateSets["menu_only"]);
        Assert.Equal(
            ImmutableArray.Create("leaf_info"),
            PlanCompiler.TemplateSets["read_only"]);
    }

    [Fact]
    public void Compile_TargetPathScope_StaticChildrenStrategy()
    {
        var compiler = new PlanCompiler();
        var slots = new IntentSlots("settings_app", "target_path", "wifi");
        var plan = compiler.Compile(slots);

        Assert.Equal(ChildrenStrategyType.Static, plan.RootNode!.ChildrenStrategy.Type);
        Assert.Equal("settings_app", plan.EntryApp);
    }

    [Fact]
    public void Compile_FullInteractionScope_DynamicMatchStrategy()
    {
        var compiler = new PlanCompiler();
        var slots = new IntentSlots("settings_app", "full_interaction");
        var plan = compiler.Compile(slots);

        Assert.Equal(ChildrenStrategyType.DynamicMatch, plan.RootNode!.ChildrenStrategy.Type);
        Assert.Equal("full_interaction", plan.TemplateRegistry);
    }

    [Fact]
    public void Compile_RejectsEmptyTargetApp()
    {
        var compiler = new PlanCompiler();
        Assert.Throws<DomainValidationException>(() =>
            compiler.Compile(new IntentSlots("", "full_interaction")));
    }

    [Fact]
    public void MatchConditions_MatchPythonSource()
    {
        Assert.Equal("menu_item", PlanCompiler.MatchConditions["menu_container"].Type);
        Assert.Equal("switch", PlanCompiler.MatchConditions["switch_leaf"].Type);
        Assert.Equal("slider", PlanCompiler.MatchConditions["slider_leaf"].Type);
        Assert.Equal("button", PlanCompiler.MatchConditions["leaf_action"].Type);
        Assert.Null(PlanCompiler.MatchConditions["leaf_info"].Type); // Empty condition
    }
}

public class DynamicMatcherTests
{
    private readonly DynamicMatcher _matcher = new();

    [Fact]
    public void Match_TypeOnly_MatchesMenuItemType()
    {
        var condition = new MatchCondition(Type: "switch");
        var item = new MatchableItem(MenuItemType: MenuItemType.Switch);
        var result = _matcher.Match(condition, item);
        Assert.True(result.Matched);
    }

    [Fact]
    public void Match_TypeMismatch_Fails()
    {
        var condition = new MatchCondition(Type: "switch");
        var item = new MatchableItem(MenuItemType: MenuItemType.Button);
        var result = _matcher.Match(condition, item);
        Assert.False(result.Matched);
    }

    [Fact]
    public void Match_ExpectedAction_Matches()
    {
        var condition = new MatchCondition(ExpectedAction: "toggle");
        var item = new MatchableItem(ExpectedAction: ExpectedAction.Toggle);
        var result = _matcher.Match(condition, item);
        Assert.True(result.Matched);
    }

    [Fact]
    public void Match_IndexRange_BoundsItem()
    {
        var condition = new MatchCondition(MinIndex: 2, MaxIndex: 5);
        var item = new MatchableItem(Index: 3);
        var result = _matcher.Match(condition, item);
        Assert.True(result.Matched);
    }

    [Fact]
    public void Match_IndexRange_RejectsOutOfBounds()
    {
        var condition = new MatchCondition(MinIndex: 2, MaxIndex: 5);
        var item = new MatchableItem(Index: 7);
        var result = _matcher.Match(condition, item);
        Assert.False(result.Matched);
    }

    [Fact]
    public void Match_NullMinIndex_AllowsAnyLowerBound()
    {
        var condition = new MatchCondition(MinIndex: null, MaxIndex: 5);
        var item = new MatchableItem(Index: 0);
        var result = _matcher.Match(condition, item);
        Assert.True(result.Matched);
    }

    [Fact]
    public void Match_NullMaxIndex_AllowsAnyUpperBound()
    {
        var condition = new MatchCondition(MinIndex: 2, MaxIndex: null);
        var item = new MatchableItem(Index: 100);
        var result = _matcher.Match(condition, item);
        Assert.True(result.Matched);
    }

    [Fact]
    public void Match_CustomDict_MatchesAllKeyValuePairs()
    {
        var condition = new MatchCondition(Custom: new Dictionary<string, object>
        {
            ["role"] = "navigation",
            ["level"] = "1"
        });
        var item = new MatchableItem(Metadata: new Dictionary<string, string>
        {
            ["role"] = "navigation",
            ["level"] = "1"
        });
        var result = _matcher.Match(condition, item);
        Assert.True(result.Matched);
    }

    [Fact]
    public void Match_CustomDict_FailsOnMismatchedValue()
    {
        var condition = new MatchCondition(Custom: new Dictionary<string, object>
        {
            ["role"] = "navigation"
        });
        var item = new MatchableItem(Metadata: new Dictionary<string, string>
        {
            ["role"] = "content"
        });
        var result = _matcher.Match(condition, item);
        Assert.False(result.Matched);
    }

    [Fact]
    public void Match_ConjunctiveLogic_AllConditionsMustPass()
    {
        var condition = new MatchCondition(Type: "switch", ExpectedAction: "click");
        var item = new MatchableItem(MenuItemType: MenuItemType.Switch, ExpectedAction: ExpectedAction.Navigate);
        var result = _matcher.Match(condition, item);
        Assert.False(result.Matched);
    }

    [Fact]
    public void Match_EmptyCondition_MatchesEverything()
    {
        var condition = new MatchCondition();
        var item = new MatchableItem(MenuItemType: MenuItemType.Item);
        var result = _matcher.Match(condition, item);
        Assert.True(result.Matched);
    }
}

public class TemplateInstantiatorTests
{
    [Fact]
    public void Instantiate_ResolvesPlaceholders()
    {
        var instantiator = new TemplateInstantiator();
        var template = new Template(
            TemplateId: "switch_leaf",
            NodeType: NodeType.LeafSwitch,
            Operation: new Dictionary<string, object>
            {
                ["action"] = "click",
                ["target"] = new Dictionary<string, object>
                {
                    ["by"] = "text",
                    ["value"] = "{{item_text}}"
                }
            });

        var context = new Dictionary<string, object> { ["item_text"] = "WiFi" };
        var result = instantiator.Instantiate(template, context, new List<string>());

        Assert.NotNull(result.Operation.Target);
        Assert.Equal("WiFi", result.Operation.Target.Value);
    }

    [Fact]
    public void Instantiate_ConstructsCompleteTraversalNode()
    {
        var instantiator = new TemplateInstantiator();
        var template = new Template(
            TemplateId: "menu_container",
            NodeType: NodeType.Container,
            Operation: new Dictionary<string, object> { ["action"] = "click" },
            ChildrenStrategy: new Dictionary<string, object> { ["type"] = "dynamic_match" },
            ErrorPolicy: new Dictionary<string, object> { ["on_error"] = "retry", ["max_retries"] = 2 });

        var context = new Dictionary<string, object>();
        var result = instantiator.Instantiate(template, context, new List<string>());

        Assert.Equal(OperationType.Click, result.Operation.Action);
        Assert.Equal(ChildrenStrategyType.DynamicMatch, result.ChildrenStrategy.Type);
        Assert.Equal(ErrorPolicyType.Retry, result.ErrorPolicy!.OnError);
        Assert.Equal(2, result.ErrorPolicy.MaxRetries);
    }

    [Fact]
    public void Instantiate_V69PathConcatenation()
    {
        var instantiator = new TemplateInstantiator();
        var template = new Template(
            TemplateId: "wifi_switch",
            NodeType: NodeType.LeafSwitch,
            Operation: new Dictionary<string, object> { ["action"] = "click" });

        var context = new Dictionary<string, object>();
        var parentPath = new List<string> { "home", "settings" };
        var result = instantiator.Instantiate(template, context, parentPath);

        Assert.NotNull(result.Precondition);
        Assert.Equal(new List<string> { "home", "settings", "wifi_switch" }, result.Precondition.Path);
    }

    [Fact]
    public void Instantiate_EmptyParentPath_SingleNodeName()
    {
        var instantiator = new TemplateInstantiator();
        var template = new Template(
            TemplateId: "root_menu",
            NodeType: NodeType.Screen,
            Operation: new Dictionary<string, object> { ["action"] = "no_action" });

        var context = new Dictionary<string, object>();
        var result = instantiator.Instantiate(template, context, new List<string>());

        Assert.NotNull(result.Precondition);
        Assert.Equal(new List<string> { "root_menu" }, result.Precondition.Path);
    }
}
