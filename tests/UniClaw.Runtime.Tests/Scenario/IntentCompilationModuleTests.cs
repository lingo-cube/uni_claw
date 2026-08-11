using System.Collections.Immutable;
using System.Reflection;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using UniClaw.Runtime.Tests.Scenario.Fakes;
using UniClaw.Runtime.World;
using Xunit;
using RuntimeAgent = UniClaw.Runtime.Agent.Agent;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeRecovery = UniClaw.Runtime.Recovery.Recovery;
using RuntimeStartup = UniClaw.Runtime.Startup.Startup;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>Phase 6 production-shaped proofs for bounded BusinessIntent compilation.</summary>
public sealed class IntentCompilationModuleTests
{
    private static readonly SemanticObject Wifi = SemanticObject.Define("WifiConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static readonly SemanticObject Bluetooth = SemanticObject.Define("BluetoothConnectivity", "ConnectivitySetting", ["Enabled"]);
    private static readonly Capability SetEnabled = Capability.Define("SetEnabled", "ConnectivitySetting", "Enabled");
    private static readonly ImmutableArray<SemanticObject> Objects = [Wifi, Bluetooth];
    private static readonly ImmutableDictionary<string, string> Aliases = ImmutableDictionary<string, string>.Empty
        .Add("WifiConnectivity", "Wi-Fi")
        .Add("BluetoothConnectivity", "Bluetooth");

    [Fact]
    public void P1_EnableWifi_CompilesEnabledTrue()
    {
        var result = Compile("Enable Wi-Fi");
        var resolved = Assert.IsType<IntentCompilationResult.Resolved>(result);
        Assert.Equal(new SemanticGoalInput("WifiConnectivity", "Enabled", true), resolved.Goal);
    }

    [Fact]
    public async Task P2_DisableWifi_CompilesAndRunsOnToOff()
    {
        var resolved = Assert.IsType<IntentCompilationResult.Resolved>(Compile("Turn off Wi-Fi"));
        var (agent, environment) = Build("Wi-Fi", initial: true, desired: false);
        var result = await IntentExecution.RunResolvedAsync(agent, resolved, Objects, [SetEnabled], "p2");

        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Equal(RunState.Completed, agent.State);
        Assert.False(Assert.Single(environment.ActionHistory.OfType<DeviceAction.SetSwitch>()).TargetState);
    }

    [Fact]
    public void P3_EnableBluetooth_CompilesEnabledTrue()
    {
        var resolved = Assert.IsType<IntentCompilationResult.Resolved>(Compile("Turn on Bluetooth"));
        Assert.Equal(new SemanticGoalInput("BluetoothConnectivity", "Enabled", true), resolved.Goal);
    }

    [Fact]
    public void P4_GoalHasNoUiOrExecutionFields()
    {
        var resolved = Assert.IsType<IntentCompilationResult.Resolved>(Compile("打开 Wi-Fi"));
        var names = typeof(SemanticGoalInput).GetProperties().Select(property => property.Name).ToArray();
        Assert.Equal<string>(["ObjectIdentity", "StateDimension", "DesiredValue"], names);
        Assert.Equal("WifiConnectivity", resolved.Goal.ObjectIdentity);
    }

    [Theory]
    [InlineData("Enable Airplane Mode")]
    [InlineData("打开它")]
    [InlineData("Inspect Wi-Fi")]
    [InlineData("Enable Wi-Fi and Bluetooth")]
    [InlineData("Enable and disable Wi-Fi")]
    public void P5_InsufficientInputsHaveNoExecutableGoal(string expression)
    {
        var result = Compile(expression);
        var insufficient = Assert.IsType<IntentCompilationResult.Insufficient>(result);
        Assert.NotNull(insufficient.Intent);
        Assert.DoesNotContain(typeof(IntentCompilationResult.Insufficient)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.PropertyType == typeof(SemanticGoalInput));
    }

    [Fact]
    public void P6_OneGenericCompilerPathHandlesWifiAndBluetooth()
    {
        var wifi = Assert.IsType<IntentCompilationResult.Resolved>(Compile("Enable Wi-Fi"));
        var bluetooth = Assert.IsType<IntentCompilationResult.Resolved>(Compile("Enable Bluetooth"));

        Assert.Equal("WifiConnectivity", wifi.Goal.ObjectIdentity);
        Assert.Equal("BluetoothConnectivity", bluetooth.Goal.ObjectIdentity);
    }

    [Fact]
    public async Task P7_ResolvedWifi_UsesExistingAgentClosedLoop()
    {
        var resolved = Assert.IsType<IntentCompilationResult.Resolved>(Compile("开启 Wi-Fi"));
        var (agent, environment) = Build("Wi-Fi", initial: false, desired: true);
        var result = await IntentExecution.RunResolvedAsync(agent, resolved, Objects, [SetEnabled], "p7");

        var satisfied = Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.True(satisfied.Evidence.Satisfied);
        Assert.Equal(RunState.Completed, agent.State);
        Assert.True(Assert.Single(environment.ActionHistory.OfType<DeviceAction.SetSwitch>()).TargetState);
    }

    [Fact]
    public void P8_CompileAndRunResolvedRequireNoPlanStepOrDeviceAction()
    {
        var compileParameters = typeof(IntentCompiler).GetMethod(nameof(IntentCompiler.Compile))!.GetParameters();
        var runParameters = typeof(IntentExecution).GetMethod(nameof(IntentExecution.RunResolvedAsync))!.GetParameters();

        Assert.DoesNotContain(compileParameters.Concat(runParameters), parameter =>
            parameter.ParameterType == typeof(PlanStep) || parameter.ParameterType == typeof(DeviceAction));
    }

    [Fact]
    public void P9_CompilationIsWorldIndependentAndHasNoObservationInput()
    {
        var intent = new BusinessIntent("Enable Wi-Fi");
        var first = IntentCompiler.Compile(intent, Objects, Aliases);
        var second = IntentCompiler.Compile(intent, Objects, Aliases);
        Assert.Equal(first, second);
        Assert.DoesNotContain(typeof(IntentCompiler).GetMethod(nameof(IntentCompiler.Compile))!.GetParameters(),
            parameter => parameter.ParameterType == typeof(Observation));
    }

    [Fact]
    public async Task P10_DirectSemanticGoalInputPathRemainsUsable()
    {
        var (agent, _) = Build("Bluetooth", initial: false, desired: true);
        var result = await agent.RunSemanticGoalAsync(
            new SemanticGoalInput("BluetoothConnectivity", "Enabled", true),
            Objects,
            [SetEnabled],
            "p10");

        Assert.IsType<SemanticRunResult.Satisfied>(result);
        Assert.Equal(RunState.Completed, agent.State);
    }

    [Fact]
    public void P11_BusinessIntentRejectsBlankExpression()
    {
        Assert.Throws<ArgumentException>(() => new BusinessIntent(" "));
    }

    [Theory]
    [InlineData("Enable Wi-Fi.")]
    [InlineData("请开启 Wi-Fi。")]
    public void P12_PunctuationAndChineseWordingCompileDeterministically(string expression)
    {
        var first = Compile(expression);
        var second = Compile(expression);

        var resolved = Assert.IsType<IntentCompilationResult.Resolved>(first);
        Assert.Equal(new SemanticGoalInput("WifiConnectivity", "Enabled", true), resolved.Goal);
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("Reenable Wi-Fi")]
    [InlineData("Enable Bluetoothable")]
    [InlineData("Enable xBluetooth")]
    public void P13_EmbeddedEnglishTermsDoNotFabricateAGoal(string expression)
    {
        var result = Compile(expression);

        Assert.IsType<IntentCompilationResult.Insufficient>(result);
    }

    [Fact]
    public void P14_ObjectWithoutEnabledDimensionRemainsInsufficient()
    {
        var display = SemanticObject.Define("Display", "Setting", ["Brightness"]);
        var result = IntentCompiler.Compile(
            new BusinessIntent("Enable Display"),
            [display],
            ImmutableDictionary<string, string>.Empty.Add("Display", "Display"));

        var insufficient = Assert.IsType<IntentCompilationResult.Insufficient>(result);
        Assert.Contains("does not declare the Enabled dimension", insufficient.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void P15_AliasWithMultipleValidObjectsRemainsInsufficient()
    {
        var ambiguousAliases = ImmutableDictionary<string, string>.Empty
            .Add("WifiConnectivity", "Connectivity")
            .Add("BluetoothConnectivity", "Connectivity");
        var result = IntentCompiler.Compile(
            new BusinessIntent("Enable Connectivity"),
            Objects,
            ambiguousAliases);

        var insufficient = Assert.IsType<IntentCompilationResult.Insufficient>(result);
        Assert.Contains("More than one", insufficient.Reason, StringComparison.Ordinal);
    }

    private static IntentCompilationResult Compile(string expression)
        => IntentCompiler.Compile(new BusinessIntent(expression), Objects, Aliases);

    private static (RuntimeAgent Agent, ScriptedEnvironment Environment) Build(string label, bool initial, bool desired)
    {
        var identity = label == "Bluetooth" ? "BluetoothConnectivity" : "WifiConnectivity";
        var obj = identity == "BluetoothConnectivity" ? Bluetooth : Wifi;
        var bounds = new ElementBounds(0.75f, 0.20f, 0.90f, 0.30f);
        var criteria = new ElementBindingCriteria(
            [obj],
            ImmutableDictionary<string, string>.Empty.Add(identity, label),
            ImmutableDictionary<string, string>.Empty.Add(identity, "toggle"));
        var pages = new PageAnalysisCriteria(
            "settings",
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("Settings", [label]));
        var transition = new TransitionConfig(ScreenTransitionAction.SetSwitch, "Result", desired);
        var environment = new ScriptedEnvironment(
            "Settings",
            "Settings",
            [
                Screen("Settings", label, initial, transition, bounds),
                Screen("Result", label, desired, null, bounds),
            ]);
        var traversal = new RuntimeTraversal(environment);
        var startup = new RuntimeStartup(environment, "settings", _ => "Settings");
        var recovery = new RuntimeRecovery(environment, _ => [], (_, _) => null, (_, _) => true);
        RuntimeContainer Factory(string page) => new(page, observation => observation.ForegroundApplication == "settings", traversal.ExecuteStep);
        return (new RuntimeAgent(startup, traversal, token => environment.ObserveAsync(token), _ => "Settings", Factory, recovery, pages, criteria), environment);
    }

    private static ScreenConfig Screen(string name, string label, bool value, TransitionConfig? transition, ElementBounds bounds)
        => new(name, "settings", [
            new ElementConfig(label, null, null, new ElementBounds(0.05f, 0.20f, 0.50f, 0.30f), "menuItem"),
            new ElementConfig("", value, transition, bounds, "toggle"),
        ]);
}
