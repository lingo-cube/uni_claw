using System.Reflection;
using System.Text.RegularExpressions;
using UniClaw.Runtime.DriverHost;
using UniClaw.Runtime.Model;
using UniClaw.Runtime.Planning;
using Xunit;

namespace UniClaw.Runtime.Tests.Architecture;

public sealed class RuntimeExplorationSemanticAdmissionArchitectureGuardTests
{
    private static readonly string[] RuntimeFiles =
    [
        "src/UniClaw.Runtime/Agent/Agent.cs",
        "src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs",
        "src/UniClaw.Runtime/Agent/Agent.PreTerminalCycle.cs",
        "src/UniClaw.Runtime/Model/ExplorationLedger.cs",
        "src/UniClaw.Runtime/Model/ExplorationLedgerCompiler.cs",
        "src/UniClaw.Runtime/Planning/StrategyContract.cs",
        "src/UniClaw.Runtime/Planning/IntentExecution.cs"
    ];

    private static readonly string[] ForbiddenTypes =
        ["DeviceAction", "RunState", "GoalEvidence", "StateMachine", "Traversal", "Recovery", "TargetGroundingCriterion", "TargetGroundingEvidence", "TargetGrounder", "PlanStep", "Plan"];

    [Fact]
    public void StrategyDirective_WireShapeRemainsEightPropertiesAndEightParameters()
    {
        var expected = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["StrategyId"] = typeof(string), ["ContractVersion"] = typeof(int),
            ["Objective"] = typeof(StrategyObjective), ["Scope"] = typeof(StrategyScope),
            ["Exploration"] = typeof(ExplorationIntent), ["Constraints"] = typeof(StrategyConstraintSet),
            ["Completion"] = typeof(StrategyCompletionCriteria), ["Adaptation"] = typeof(StrategyAdaptationBoundary),
        };
        var properties = typeof(StrategyDirective).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(expected.Keys.OrderBy(x => x), properties.Select(x => x.Name).OrderBy(x => x));
        foreach (var property in properties) Assert.Equal(expected[property.Name], property.PropertyType);
        var constructor = Assert.Single(typeof(StrategyDirective).GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal(new[] { "strategyId", "contractVersion", "objective", "scope", "exploration", "constraints", "completion", "adaptation" }, constructor.GetParameters().Select(p => p.Name));
        Assert.Equal(new[] { typeof(string), typeof(int), typeof(StrategyObjective), typeof(StrategyScope), typeof(ExplorationIntent), typeof(StrategyConstraintSet), typeof(StrategyCompletionCriteria), typeof(StrategyAdaptationBoundary) }, constructor.GetParameters().Select(p => p.ParameterType));
        Assert.Equal(1, StrategyContractCompiler.SupportedContractVersion);
    }

    [Fact]
    public void StrategyRunStartWireShapeRemainsStable()
    {
        var request = typeof(StrategyRunStartRequest);
        var parameters = request.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Single().GetParameters();
        Assert.Equal(new[] { "strategy", "device" }, parameters.Select(p => p.Name));
        Assert.Equal(new[] { typeof(StrategyDirective), typeof(DeviceSelector) }, parameters.Select(p => p.ParameterType));
        Assert.Equal(new[] { ("Strategy", typeof(StrategyDirective)), ("Device", typeof(DeviceSelector)) }, request.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(p => (p.Name, p.PropertyType)));
        var dto = typeof(UniClawStrategyRunAdmissionDto);
        var dtoShape = new[] { ("Accepted", typeof(bool)), ("RunId", typeof(string)), ("RunState", typeof(string)), ("RejectionCode", typeof(string)), ("RejectionReason", typeof(string)) };
        Assert.Equal(dtoShape, dto.GetProperties().Select(p => (p.Name, Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType)));
        var dtoCtor = Assert.Single(dto.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal(new[] { "Accepted", "RunId", "RunState", "RejectionCode", "RejectionReason" }, dtoCtor.GetParameters().Select(p => p.Name));
        Assert.Equal(dtoShape.Select(x => x.Item2), dtoCtor.GetParameters().Select(p => Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType));
        Assert.Contains("case \"run.strategy.start\":", CodeOnly(File.ReadAllText(RepoPath("src/UniClaw.Runtime.DriverHost/Transport/UniClawDriverHostServer.cs"))), StringComparison.Ordinal);
        Assert.Equal(1, UniClawWireContract.ProtocolVersion);
        var wire = CodeOnly(File.ReadAllText(RepoPath("src/UniClaw.Runtime.DriverHost/Execution/StrategyRunStartWireContract.cs")));
        Assert.Contains("RequireOnly(root, \"strategy\", \"device\")", wire, StringComparison.Ordinal);
        var strategyKeys = new[] { "strategyId", "contractVersion", "objective", "scope", "exploration", "constraints", "completion", "adaptation" };
        var strategyBlock = Regex.Match(wire, @"RequireOnly\(\s*strategy,\s*(?<keys>.*?)\);", RegexOptions.Singleline).Groups["keys"].Value;
        Assert.NotEmpty(strategyBlock);
        var extractedKeys = Regex.Matches(strategyBlock, "\\\"([^\\\"]+)\\\"").Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(8, extractedKeys.Length);
        Assert.Equal(strategyKeys.OrderBy(key => key), extractedKeys.OrderBy(key => key));
        foreach (var key in strategyKeys) Assert.Equal(1, Regex.Matches(strategyBlock, $"\\\"{Regex.Escape(key)}\\\"").Count);
        Assert.Equal(1, StrategyContractCompiler.SupportedContractVersion);
    }

    [Fact]
    public void OptionATypes_AreInternalAndEvidenceSurfacesHaveNoAuthorityDependencies()
    {
        foreach (var type in new[] { typeof(ExplorationExecutionSemantics), typeof(AcceptedExplorationRunContext), typeof(ExplorationScopeEvidence) })
            Assert.False(type.IsPublic, $"{type.FullName} must remain internal.");
        foreach (var type in new[] { typeof(ExplorationExecutionSemantics), typeof(AcceptedExplorationRunContext), typeof(ExplorationScopeEvidence), typeof(ExplorationLedgerView), typeof(ExplorationScopeLedger), typeof(ExplorationLedgerCompiler) })
        {
            foreach (var member in DeclaredSurface(type))
            {
                var memberTypes = member switch
                {
                    PropertyInfo p => new[] { p.PropertyType },
                    FieldInfo f => new[] { f.FieldType },
                    MethodInfo m => new[] { m.ReturnType }.Concat(m.GetParameters().Select(parameter => parameter.ParameterType)),
                    ConstructorInfo c => c.GetParameters().Select(parameter => parameter.ParameterType),
                    _ => Enumerable.Empty<Type>()
                };
                foreach (var referenced in memberTypes.SelectMany(SurfaceTypes))
                    Assert.DoesNotContain(ForbiddenTypes, token => referenced.FullName?.Contains(token, StringComparison.OrdinalIgnoreCase) is true);
                if (member is MethodInfo method && !method.IsSpecialName)
                    Assert.DoesNotContain(new[] { "Authorize", "Dispatch", "Execute", "Transition", "Recover", "Complete", "Cancel", "Fail", "SelectTarget", "ChooseTarget", "GroundTarget" }, token => method.Name.Contains(token, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void ChangedRuntimeSources_AreScenarioNeutralAndUseTypedCategoryResolution()
    {
        var tokens = new[] { "Settings", "DeveloperOptions", "PreferenceRow", "NavigateUp", "Navigate up", "collapsing_toolbar", "wifi", "settings://" };
        var violations = RuntimeFiles.SelectMany(file => tokens.Where(token => CodeOnly(File.ReadAllText(RepoPath(file))).Contains(token, StringComparison.OrdinalIgnoreCase)).Select(token => $"{file}:{token}")).ToArray();
        Assert.Empty(violations);
        var ledger = CodeOnly(File.ReadAllText(RepoPath("src/UniClaw.Runtime/Model/ExplorationLedger.cs")));
        var resolver = Regex.Match(ledger, @"ExplorationRule\?\s+Resolve\(.*?\n\s*=>\s*category\s+switch\s*\{(?<body>.*?)\n\s*\};", RegexOptions.Singleline);
        Assert.True(resolver.Success, "ExplorationRuleResolver.Resolve typed switch body not found.");
        var body = resolver.Groups["body"].Value;
        Assert.Contains("TypeLevelElementCategory.NavigableContainer", body, StringComparison.Ordinal);
        Assert.Contains("TypeLevelElementCategory.StateChangingControl", body, StringComparison.Ordinal);
        Assert.DoesNotContain("ToString", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Parse", body, StringComparison.Ordinal);
        Assert.DoesNotContain("(int)", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentRemainsSoleOwnerAndCompilerRemainsPureStatic()
    {
        var all = Directory.GetFiles(RepoPath("src/UniClaw.Runtime"), "*.cs", SearchOption.AllDirectories).Select(file => (file, source: CodeOnly(File.ReadAllText(file)))).ToArray();
        foreach (var field in new[] { "_acceptedExplorationContext", "_recordOnlySatisfied", "_unknownFrontierIdentities", "_latestAcceptedStrategyExecutionEvidenceView" })
        {
            var declarations = all.Where(x => Regex.IsMatch(x.source, $@"\b(?:private|protected|internal)\s+[^;\r\n]*\b{Regex.Escape(field)}\b\s*(?:;|=)" )).Select(x => x.file).ToArray();
            Assert.Single(declarations);
            var assignments = all.Where(x => Regex.IsMatch(x.source, $@"\b{Regex.Escape(field)}\s*=" )).Select(x => x.file).ToArray();
            Assert.NotEmpty(assignments);
            Assert.All(assignments, file => Assert.Matches(@"Agent(?:\.OpenWorld|\.PreTerminalCycle)?\.cs$", file));
        }
        var calls = all.Where(x => x.source.Contains("ExplorationLedgerCompiler.Compile(", StringComparison.Ordinal)).Select(x => x.file).ToArray();
        Assert.NotEmpty(calls);
        Assert.All(calls, file => Assert.EndsWith("Agent.cs", file, StringComparison.Ordinal));
        Assert.True(typeof(ExplorationLedgerCompiler).IsAbstract && typeof(ExplorationLedgerCompiler).IsSealed);
    }

    private static IEnumerable<MemberInfo> DeclaredSurface(Type type)
        => type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(member => !member.Name.StartsWith("<", StringComparison.Ordinal));

    private static IEnumerable<Type> SurfaceTypes(Type? type)
    {
        if (type is null) yield break;
        yield return type;
        if (type.IsByRef || type.IsArray) foreach (var nested in SurfaceTypes(type.GetElementType())) yield return nested;
        if (type.IsGenericType) foreach (var argument in type.GetGenericArguments()) foreach (var nested in SurfaceTypes(argument)) yield return nested;
    }

    private static string CodeOnly(string source)
        => Regex.Replace(Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline), @"//[^\r\n]*", string.Empty);

    private static string RepoPath(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !(File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) && File.Exists(Path.Combine(directory.FullName, "src", "UniClaw.Runtime.sln")))) directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Repository root not found."), relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
