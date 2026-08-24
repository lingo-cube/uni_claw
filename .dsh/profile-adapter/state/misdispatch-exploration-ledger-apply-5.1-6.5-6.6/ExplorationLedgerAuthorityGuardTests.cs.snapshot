using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
using UniClaw.Runtime.Model;
using Xunit;

namespace UniClaw.Runtime.Tests.Architecture;

/// <summary>
/// Architecture-level authority guards for the Exploration Ledger projection
/// (OpenSpec runtime-exploration-ledger-and-depth-control, tasks 5.2/5.3).
/// The ledger is an evidence-derived projection only: it must carry no
/// authorize/complete/transition/dispatch/execute/run/recovery authority and
/// remain scenario neutral. This is the guard (alarm) layer — any violation
/// fails; there is no whitelist.
/// </summary>
public sealed class ExplorationLedgerAuthorityGuardTests
{
    private static readonly string[] LedgerSourceFiles =
    {
        "src/UniClaw.Runtime/Model/ExplorationLedger.cs",
        "src/UniClaw.Runtime/Model/ExplorationLedgerCompiler.cs",
    };

    private static readonly string[] AuthorityMemberNames =
    {
        "Authorize", "Authorized", "Complete", "Completed", "Transition",
        "Dispatch", "Execute", "StartRun", "Recover", "Fail", "Cancel",
    };

    private static readonly string[] ForbiddenReferencedTypeNames =
    {
        "DeviceAction", "RunState", "GoalEvidence",
        "Traversal", "StateMachine", "Recovery", "Operator", "Brain",
    };

    private static readonly string[] ScenarioTokens =
    {
        "Settings", "DeveloperOptions", "PreferenceRow", "NavigateUp",
        "Navigate up", "collapsing_toolbar", "wifi", "settings://",
    };

    private static readonly Type[] LedgerTypes =
    {
        typeof(ExplorationLedgerView),
        typeof(ExplorationScopeLedger),
        typeof(ExplorationRule),
        typeof(ExplorationDepthSemantics),
        typeof(ExplorationLedgerCompiler),
    };

    [Fact]
    public void LedgerSurface_ExposesNoAuthorityMembers()
    {
        foreach (var type in LedgerTypes)
        {
            foreach (var member in type.GetMembers(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                         | BindingFlags.DeclaredOnly))
            {
                foreach (var forbidden in AuthorityMemberNames)
                    Assert.False(
                        member.Name.Contains(forbidden, StringComparison.Ordinal),
                        $"{type.Name}.{member.Name} carries authority-shaped name '{forbidden}'. "
                        + "The exploration ledger is a read-only projection and must not expose "
                        + "authorize/complete/transition/dispatch/execute/run/recovery authority.");
            }
        }
    }

    [Fact]
    public void LedgerProperties_DoNotReferenceMutableWorldActionOrAuthorityTypes()
    {
        foreach (var type in new[] { typeof(ExplorationLedgerView), typeof(ExplorationScopeLedger) })
        {
            foreach (var property in type.GetProperties(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                         | BindingFlags.DeclaredOnly))
            {
                foreach (var referenced in ReferencedTypeNames(property.PropertyType))
                {
                    foreach (var forbidden in ForbiddenReferencedTypeNames)
                        Assert.False(
                            referenced.Contains(forbidden, StringComparison.Ordinal),
                            $"{type.Name}.{property.Name} references forbidden type '{referenced}'. "
                            + "The ledger must not carry DeviceAction/RunState/GoalEvidence/"
                            + "Traversal/StateMachine references.");
                }
            }
        }
    }

    [Fact]
    public void LedgerCompiler_DependsOnlyOnExistingEvidenceRecords()
    {
        var allowed = new[]
        {
            "BranchProgressEvidence", "ExplorationIntent", "ExplorationRule",
            "ExplorationDepthSemantics", "ExplorationScopeLedger", "ExplorationLedgerView",
            "ImmutableArray", "ImmutableDictionary", "String", "Int32", "Int64", "Boolean",
        };

        foreach (var method in typeof(ExplorationLedgerCompiler).GetMethods(
                     BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            var referenced = ReferencedTypeNames(method.ReturnType)
                .Concat(method.GetParameters().SelectMany(p => ReferencedTypeNames(p.ParameterType)))
                .Where(name => !IsPrimitiveOrSystem(name))
                .ToArray();

            foreach (var name in referenced)
                Assert.Contains(name, allowed);
        }
    }

    [Fact]
    public void LedgerCompiler_IsPureStatic_WithNoInstanceSurface()
    {
        Assert.True(typeof(ExplorationLedgerCompiler).IsAbstract && typeof(ExplorationLedgerCompiler).IsSealed);

        var instanceMembers = typeof(ExplorationLedgerCompiler)
            .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.MemberType != MemberTypes.Constructor)
            // Skip compiler-generated closure/display types ("<>"-prefixed), not source surface.
            .Where(m => !m.Name.StartsWith("<", StringComparison.Ordinal))
            .ToArray();

        Assert.True(instanceMembers.Length == 0,
            "ExplorationLedgerCompiler must be a pure static compiler with no instance members. Found: "
            + string.Join(", ", instanceMembers.Select(m => m.Name)));
    }

    [Fact]
    public void LedgerSources_AreScenarioNeutral()
    {
        var violations = LedgerSourceFiles
            .SelectMany(file => ScenarioTokens
                .Where(token => CodeOnly(File.ReadAllText(RepoPath(file)))
                    .Contains(token, StringComparison.OrdinalIgnoreCase))
                .Select(token => file + " contains scenario token '" + token + "'"))
            .ToArray();

        Assert.True(violations.Length == 0,
            "Exploration ledger sources must remain scenario neutral — scenario knowledge "
            + "belongs to external semantic capability bindings, never to the Model layer.\n"
            + string.Join("\n", violations));
    }

    private static IEnumerable<string> ReferencedTypeNames(Type? type)
    {
        while (type is not null)
        {
            if (type.IsArray || type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                    foreach (var name in ReferencedTypeNames(argument))
                        yield return name;

                yield return type.Name.Split('`')[0];
                if (type.GetElementType() is { } element)
                    foreach (var name in ReferencedTypeNames(element))
                        yield return name;
                yield break;
            }

            yield return type.Name;
            type = null;
        }
    }

    private static bool IsPrimitiveOrSystem(string name)
        => name is "Void" or "ValueTuple" or "String" or "Int32" or "Int64" or "Boolean"
            or "IEnumerable" or "ICollection" or "IList" or "IEnumerable`1" or "KeyValuePair`2";

    // Guards must inspect executable source shape, not XML documentation or comments.
    private static string CodeOnly(string source)
        => Regex.Replace(
            Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline),
            @"//[^\r\n]*", string.Empty);

    private static string RepoPath(string relative)
        => Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            // 仓库根 = 同时含 AGENTS.md 与 src/UniClaw.Runtime.sln（子级区域地图只满足 AGENTS.md）。
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "src", "UniClaw.Runtime.sln")))
                return directory.FullName;
            directory = directory.Parent!;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }
}
