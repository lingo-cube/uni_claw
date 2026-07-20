using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Common;
using UniClaw.Core.Graph.Models;
using Xunit;

namespace UniClaw.Core.Tests.Graph;

public class TraversalPlanSerializationTests
{
    private static readonly JsonSerializerOptions Options = DomainJsonOptions.Default;

    /// <summary>Helper: serialize then deserialize, assert equality via record Equals.</summary>
    private static T AssertRoundTrip<T>(T original)
    {
        var json = JsonSerializer.Serialize(original, Options);
        var result = JsonSerializer.Deserialize<T>(json, Options);
        Assert.NotNull(result);
        Assert.Equal(original, result);
        return result;
    }

    // === TraversalPlan round-trip tests (Task 7.2) ===

    [Fact]
    public void TraversalPlan_FullPlan_RoundTrips()
    {
        var plan = CreateFullPlan();
        AssertRoundTrip(plan);
    }

    [Fact]
    public void TraversalPlan_MinimalPlan_RoundTrips()
    {
        var plan = new TraversalPlan("SettingsApp", new EntryPolicy(EntryStrategy.ColdLaunch));
        var result = AssertRoundTrip(plan);
        Assert.Equal("SettingsApp", result.EntryApp);
        Assert.Null(result.RootNode);
        Assert.Null(result.CompletionPolicy);
        Assert.Null(result.Meta);
    }

    [Fact]
    public void TraversalPlan_WithMetaMixedTypes_RoundTrips()
    {
        var plan = new TraversalPlan("App", new EntryPolicy(EntryStrategy.ColdLaunch),
            Meta: new Dictionary<string, object>
            {
                ["version"] = 3L,
                ["name"] = "test",
                ["enabled"] = true,
                ["optional"] = null!,
            });
        var result = AssertRoundTrip(plan);
        Assert.Equal(3L, result.Meta!["version"]);
        Assert.Equal("test", result.Meta!["name"]);
        Assert.True((bool)result.Meta!["enabled"]);
        Assert.Null(result.Meta!["optional"]);
    }

    [Fact]
    public void TraversalPlan_WithStaticNodes_RoundTrips()
    {
        var node1 = CreateSampleNode("menu_1", "Menu 1");
        var node2 = CreateSampleNode("wifi_switch", "WiFi Switch");
        var plan = new TraversalPlan("App", new EntryPolicy(EntryStrategy.ColdLaunch),
            StaticNodes: new Dictionary<string, TraversalNode>
            {
                ["network_menu"] = node1,
                ["wifi_switch"] = node2,
            });
        AssertRoundTrip(plan);
    }

    [Fact]
    public void TraversalPlan_WithRootNode_RoundTrips()
    {
        var rootNode = new TraversalNode("root", "Root",
            NodeType.Screen,
            new Operation(OperationType.NoAction),
            new ChildrenStrategy(ChildrenStrategyType.DynamicMatch));
        var plan = new TraversalPlan("App", new EntryPolicy(EntryStrategy.ColdLaunch),
            RootNode: rootNode);
        AssertRoundTrip(plan);
    }

    // === Sub-type round-trip tests (Task 7.3) ===

    [Fact] public void EntryPolicy_RoundTrips() => AssertRoundTrip(new EntryPolicy(EntryStrategy.BindCurrentScreen, "fallback", TimeoutSeconds: 30));

    [Fact] public void EntryConfig_RoundTrips() => AssertRoundTrip(new EntryConfig(WaitMode.Polling, 15.0, 1000, 500, TraceLevel.Detailed));

    [Fact] public void CompletionPolicy_Exhaustive_RoundTrips() => AssertRoundTrip(new CompletionPolicy(CompletionPolicyType.Exhaustive));

    [Fact] public void CompletionPolicy_TargetFound_RoundTrips() => AssertRoundTrip(new CompletionPolicy(CompletionPolicyType.TargetFound, "wifi"));

    [Fact] public void CompletionPolicy_MaxSteps_RoundTrips() => AssertRoundTrip(new CompletionPolicy(CompletionPolicyType.MaxSteps, MaxSteps: 100));

    [Fact] public void CompletionPolicy_Timeout_RoundTrips() => AssertRoundTrip(new CompletionPolicy(CompletionPolicyType.Timeout, TimeoutSeconds: 60));

    [Fact] public void IntentSlots_RoundTrips() => AssertRoundTrip(new IntentSlots("Settings", "target_only", "WiFi", 3));

    [Fact] public void TraversalNode_RoundTrips() => AssertRoundTrip(CreateSampleNode("node1", "Test Node"));

    [Fact] public void Operation_RoundTrips() => AssertRoundTrip(new Operation(OperationType.Click, new Target(TargetType.Text, "button")));

    [Fact] public void Target_RoundTrips() => AssertRoundTrip(new Target(TargetType.Text, "WiFi switch"));

    [Fact] public void RestoreAction_RoundTrips() => AssertRoundTrip(new RestoreAction(OperationType.Back));

    [Fact] public void ChildrenStrategy_RoundTrips() => AssertRoundTrip(new ChildrenStrategy(ChildrenStrategyType.DynamicMatch, MaxChildren: 500));

    [Fact] public void DynamicRule_RoundTrips() => AssertRoundTrip(new DynamicRule("menu_rule", new MatchCondition("menu_item"), "menu_container", MatchAction.GenerateChild));

    [Fact] public void MatchCondition_RoundTrips() => AssertRoundTrip(new MatchCondition("switch", "click", "WiFi", TextMatchMode.Exact));

    [Fact] public void ErrorPolicy_RoundTrips() => AssertRoundTrip(new ErrorPolicy(ErrorPolicyType.Retry, 3, "home", true));

    [Fact] public void Precondition_RoundTrips() => AssertRoundTrip(new Precondition("settings", new List<string> { "home", "settings" }, "visible", 10));

    // === Fail-fast validation tests (Task 7.4) ===

    [Fact]
    public void Deserialize_EmptyEntryApp_ThrowsDomainValidationException()
    {
        var json = """{"entryApp":"","entryPolicy":{"strategy":"coldLaunch","timeoutSeconds":10}}""";
        Assert.Throws<DomainValidationException>(() =>
            JsonSerializer.Deserialize<TraversalPlan>(json, Options));
    }

    [Fact]
    public void Deserialize_MalformedRootNode_ThrowsDomainValidationException()
    {
        var json = """{"entryApp":"App","entryPolicy":{"strategy":"coldLaunch","timeoutSeconds":10},"rootNode":{"nodeId":"r","name":"R","nodeType":"leaf","operation":{"action":"click"},"childrenStrategy":{"type":"none"}}}""";
        Assert.Throws<DomainValidationException>(() =>
            JsonSerializer.Deserialize<TraversalPlan>(json, Options));
    }

    [Fact]
    public void Deserialize_CompletionPolicyTargetFoundWithoutTargetName_Throws()
    {
        var json = """{"type":"targetFound","targetName":null,"matchMode":"exact","actionOnFound":"markAndStop"}""";
        Assert.Throws<DomainValidationException>(() =>
            JsonSerializer.Deserialize<CompletionPolicy>(json, Options));
    }

    [Fact]
    public void Deserialize_EntryPolicyTimeoutSecondsZero_Throws()
    {
        var json = """{"strategy":"coldLaunch","timeoutSeconds":0}""";
        Assert.Throws<DomainValidationException>(() =>
            JsonSerializer.Deserialize<EntryPolicy>(json, Options));
    }

    [Fact]
    public void Deserialize_TraversalNodeEmptyNodeId_Throws()
    {
        var json = """{"nodeId":"","name":"X","nodeType":"screen","operation":{"action":"noAction"},"childrenStrategy":{"type":"none"}}}""";
        Assert.Throws<DomainValidationException>(() =>
            JsonSerializer.Deserialize<TraversalNode>(json, Options));
    }

    // === Null/missing fields test (Task 7.5) ===

    [Fact]
    public void Deserialize_OnlyRequiredFields_DefaultsPopulated()
    {
        var json = """{"entryApp":"SettingsApp","entryPolicy":{"strategy":"coldLaunch","timeoutSeconds":10}}""";
        var result = JsonSerializer.Deserialize<TraversalPlan>(json, Options)!;

        Assert.Equal("SettingsApp", result.EntryApp);
        Assert.Equal("", result.PlanName);
        Assert.Equal("", result.PlanId);
        Assert.Null(result.EntryConfig);
        Assert.Null(result.RootNode);
        Assert.Null(result.StaticNodes);
        Assert.Null(result.TemplateRegistry);
        Assert.Equal(TraversalMode.Hybrid, result.Mode);
        Assert.Null(result.CompletionPolicy);
        Assert.Null(result.IntentSlots);
        Assert.Null(result.Meta);
    }

    // === Extra fields tolerance test (Task 7.6) ===

    [Fact]
    public void Deserialize_ExtraUnknownField_Tolerated()
    {
        var json = """{"entryApp":"App","entryPolicy":{"strategy":"coldLaunch","timeoutSeconds":10},"futureField":"someValue"}""";
        var result = JsonSerializer.Deserialize<TraversalPlan>(json, Options)!;

        Assert.Equal("App", result.EntryApp);
    }

    // === StaticNodes key preservation test (Task 7.7) ===

    [Fact]
    public void StaticNodes_KeysPreserved_NotCamelCased()
    {
        var plan = new TraversalPlan("App", new EntryPolicy(EntryStrategy.ColdLaunch),
            StaticNodes: new Dictionary<string, TraversalNode>
            {
                ["network_menu"] = CreateSampleNode("network_menu", "Network"),
                ["wifi_switch"] = CreateSampleNode("wifi_switch", "WiFi"),
            });
        var json = JsonSerializer.Serialize(plan, Options);

        Assert.Contains("network_menu", json);
        Assert.Contains("wifi_switch", json);
        Assert.DoesNotContain("networkMenu", json);
        Assert.DoesNotContain("wifiSwitch", json);

        var result = JsonSerializer.Deserialize<TraversalPlan>(json, Options)!;
        Assert.True(result.StaticNodes!.ContainsKey("network_menu"));
        Assert.True(result.StaticNodes!.ContainsKey("wifi_switch"));
    }

    // === Computed properties test (Task 7.8) ===

    [Fact]
    public void TraversalNode_SerializedJson_OmitsComputedProperties()
    {
        var node = CreateSampleNode("n1", "Test");
        var json = JsonSerializer.Serialize(node, Options);

        Assert.DoesNotContain("isContainer", json);
        Assert.DoesNotContain("isLeaf", json);
        Assert.DoesNotContain("staticChildren", json);
    }

    [Fact]
    public void TraversalNode_Deserialized_WithComputedPropertiesInInput_Tolerated()
    {
        var node = CreateSampleNode("n1", "Test");
        var json = JsonSerializer.Serialize(node, Options);
        // Inject computed property into JSON
        var jsonDict = JsonSerializer.Deserialize<Dictionary<string, object>>(json, Options)!;
        jsonDict["isContainer"] = true;

        var modifiedJson = JsonSerializer.Serialize(jsonDict, Options);
        var result = JsonSerializer.Deserialize<TraversalNode>(modifiedJson, Options)!;

        Assert.Equal("n1", result.NodeId);
        Assert.Equal("Test", result.Name);
    }

    // === ToJson/FromJson convenience method tests ===

    [Fact]
    public void ToJson_ProducesValidCamelCaseJson()
    {
        var plan = new TraversalPlan("Settings", new EntryPolicy(EntryStrategy.ColdLaunch));
        var json = plan.ToJson();

        Assert.Contains("entryApp", json);
        Assert.Contains("Settings", json);
        Assert.Contains("entryPolicy", json);
        Assert.Contains("coldLaunch", json);
    }

    [Fact]
    public void FromJson_DeserializesToEqualPlan()
    {
        var plan = CreateFullPlan();
        var result = TraversalPlan.FromJson(plan.ToJson());
        Assert.Equal(plan, result);
    }

    [Fact]
    public void FromJson_NullInput_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => TraversalPlan.FromJson("null"));
    }

    [Fact]
    public void FromJson_EmptyString_ThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => TraversalPlan.FromJson(""));
    }

    // === Helpers ===

    private static TraversalPlan CreateFullPlan() =>
        new TraversalPlan("SettingsApp",
            new EntryPolicy(EntryStrategy.ColdLaunch, TimeoutSeconds: 15),
            PlanName: "Full Plan",
            PlanId: "plan-001",
            EntryConfig: new EntryConfig(WaitMode.Fast, 10.0, 500, 300, TraceLevel.Basic),
            RootNode: new TraversalNode("root", "Root Screen",
                NodeType.Screen,
                new Operation(OperationType.NoAction),
                new ChildrenStrategy(ChildrenStrategyType.DynamicMatch, MaxChildren: 500)),
            CompletionPolicy: new CompletionPolicy(CompletionPolicyType.Exhaustive),
            Mode: TraversalMode.Hybrid,
            IntentSlots: new IntentSlots("SettingsApp", "full", Depth: 3));

    private static TraversalNode CreateSampleNode(string id, string name) =>
        new TraversalNode(id, name, NodeType.Container,
            new Operation(OperationType.Click, new Target(TargetType.Text, name)),
            new ChildrenStrategy(ChildrenStrategyType.DynamicMatch, MaxChildren: 100),
            new Precondition(id, new List<string> { "home", id }, TimeoutSeconds: 5),
            new ErrorPolicy(ErrorPolicyType.Retry, 2));
}
