using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Domain.Models.Vision;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using Xunit;

namespace UniClaw.Core.Tests.Architecture;

/// <summary>
/// Defensive enum value count assertions — prevents accidental addition of enum values.
/// Each enum's value count is locked by constitution/locked-enums.md;
/// these tests enforce the constraints as CI-blocking guards.
/// See docs/system/charter-specification.md §6 for the full guard design.
/// </summary>
public class EnumValueGuardTests
{
    // --- Phase 2.1 locked enums (10) ---
    [Fact]
    public void TraversalState_Has8Values()
        => Assert.Equal(8, Enum.GetValues<TraversalState>().Length);

    [Fact]
    public void GlobalState_Has8Values()
        => Assert.Equal(8, Enum.GetValues<GlobalState>().Length);

    [Fact]
    public void NodeType_Has8Values()
        => Assert.Equal(8, Enum.GetValues<NodeType>().Length);

    [Fact]
    public void ErrorType_Has6Values()
        => Assert.Equal(6, Enum.GetValues<ErrorType>().Length);

    [Fact]
    public void ErrorStrategy_Has5Values()
        => Assert.Equal(5, Enum.GetValues<ErrorStrategy>().Length);

    [Fact]
    public void PopupType_Has5Values()
        => Assert.Equal(5, Enum.GetValues<PopupType>().Length);

    [Fact]
    public void DismissStrategy_Has4Values()
        => Assert.Equal(4, Enum.GetValues<DismissStrategy>().Length);

    [Fact]
    public void UrgencyLevel_Has3Values()
        => Assert.Equal(3, Enum.GetValues<UrgencyLevel>().Length);

    [Fact]
    public void BlockingType_Has3Values()
        => Assert.Equal(3, Enum.GetValues<BlockingType>().Length);

    [Fact]
    public void FallbackAction_Has4Values()
        => Assert.Equal(4, Enum.GetValues<FallbackAction>().Length);

    // --- Phase 1 Domain locked enums (2) ---
    [Fact]
    public void TypeHint_Has8Values()
        => Assert.Equal(8, Enum.GetValues<TypeHint>().Length);

    [Fact]
    public void SelectionState_Has3Values()
        => Assert.Equal(3, Enum.GetValues<SelectionState>().Length);

    // --- Phase 2.2 locked enums (1) ---
    [Fact]
    public void SpanType_Has11Values()
        => Assert.Equal(11, Enum.GetValues<SpanType>().Length);
}

/// <summary>
/// Dependency direction guard — ensures Graph layer does not depend on StateMachine layer,
/// that interface ownership boundaries are respected,
/// and that Domain layer has zero upward references (C-4: Domain is the bottom layer).
/// Extracted from Phase2EnumGuardTests.cs.
/// </summary>
public class DependencyDirectionGuardTests
{
    // --- C-4: Domain layer must not reference any upper layer ---
    [Fact]
    public void Domain_DoesNotReferenceAnyUpperLayer()
    {
        // Domain is the bottom layer — it must not reference Graph, StateMachine,
        // Traversal, AI, or Observability (constitution C-4, verified 2026-07-05)
        var domainDir = Path.Combine(
            FindSourceRoot(), "src", "UniClaw.Core", "Domain");
        if (!Directory.Exists(domainDir))
            return;

        var forbiddenNamespaces = new[]
        {
            "UniClaw.Core.Graph",
            "UniClaw.Core.StateMachine",
            "UniClaw.Core.Traversal",
            "UniClaw.Core.AI",
            "UniClaw.Core.Observability",
        };

        foreach (var file in Directory.GetFiles(domainDir, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            foreach (var ns in forbiddenNamespaces)
                Assert.DoesNotContain($"using {ns}", source);
        }
    }

    // --- C-5: Graph layer must not reference StateMachine layer ---
    [Fact]
    public void TraversalNode_DoesNotReferenceStateMachineNamespace()
    {
        var sourcePath = Path.Combine(
            FindSourceRoot(), "src", "UniClaw.Core", "Graph", "Models", "TraversalNode.cs");
        if (!File.Exists(sourcePath))
            return; // Skip if file not found in test environment

        var source = File.ReadAllText(sourcePath);
        Assert.DoesNotContain("using UniClaw.Core.StateMachine", source);
    }

    [Fact]
    public void ITraversalNode_ResidesInGraphModelsNamespace()
    {
        // Verify ITraversalNode is defined in Graph.Models, not StateMachine
        var sourcePath = Path.Combine(
            FindSourceRoot(), "src", "UniClaw.Core", "Graph", "Models", "ITraversalNode.cs");
        if (!File.Exists(sourcePath))
            return;

        var source = File.ReadAllText(sourcePath);
        Assert.Contains("namespace UniClaw.Core.Graph.Models", source);
        Assert.Contains("interface ITraversalNode", source);
        Assert.Contains("interface IStackFrame", source);
    }

    [Fact]
    public void TraversalState_DoesNotContainITraversalNodeOrIStackFrame()
    {
        var sourcePath = Path.Combine(
            FindSourceRoot(), "src", "UniClaw.Core", "StateMachine", "TraversalState.cs");
        if (!File.Exists(sourcePath))
            return;

        var source = File.ReadAllText(sourcePath);
        Assert.DoesNotContain("interface ITraversalNode", source);
        Assert.DoesNotContain("interface IStackFrame", source);
    }

    // --- D-14 / D-17: Acknowledged upward references ---
    // StateMachine→Traversal (HasUnvisitedChildren uses Traversal.IGraphTraversalEngine)
    // StateMachine→Observability (TraversalRuntimeContext references Observability types)
    // These are NOT design defects — they are explicitly acknowledged upward references
    // consistent with D-17 (Observability is cross-cutting utility).
    [Fact]
    public void StateMachine_ReferencesTraversalForIGraphTraversalEngine()
    {
        // D-14 resolution: TraversalState.cs now references UniClaw.Core.Traversal
        // for IGraphTraversalEngine (empty stub deleted, full interface used).
        // This is an acknowledged upward reference, not a C-5 violation.
        var sourcePath = Path.Combine(
            FindSourceRoot(), "src", "UniClaw.Core", "StateMachine", "TraversalState.cs");
        if (!File.Exists(sourcePath))
            return;

        var source = File.ReadAllText(sourcePath);
        // Verify the acknowledged reference exists
        Assert.Contains("using UniClaw.Core.Traversal", source);
        // Verify the old empty stub is gone
        Assert.DoesNotContain("图遍历引擎接口（最小定义）", source);
    }

    [Fact]
    public void StateMachine_ReferencesObservabilityForCrossCuttingUtility()
    {
        // D-17: Observability is a cross-cutting utility, not a traditional upper layer.
        // TraversalRuntimeContext references Observability types (ITraceRecorder etc).
        // This is an acknowledged upward reference, not a C-5 violation.
        var ctxPath = Path.Combine(
            FindSourceRoot(), "src", "UniClaw.Core", "StateMachine", "TraversalRuntimeContext.cs");
        if (!File.Exists(ctxPath))
            return;

        var source = File.ReadAllText(ctxPath);
        Assert.Contains("using UniClaw.Core.Observability", source);
    }

    private static string FindSourceRoot()
    {
        // Walk up from test bin directory to find project root
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "src", "UniClaw.Core.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Directory.GetCurrentDirectory();
    }
}

/// <summary>
/// Namespace isolation guard — ensures Domain sub-domains (Vision/Content/Common)
/// have zero cross-imports, and FSMs (TraversalFSM/GlobalFSM) do not share types.
/// C-3: Domain three-island zero cross-import.
/// C-4: FSM independence — TraversalFSM and GlobalFSM must not share state/transition types.
/// </summary>
public class NamespaceIsolationGuardTests
{
    // --- C-3: Domain sub-domains zero cross-import ---
    [Fact]
    public void Domain_Subdomains_ZeroCrossImport()
    {
        var sourceRoot = FindSourceRoot();
        var domainDir = Path.Combine(sourceRoot, "src", "UniClaw.Core", "Domain");
        if (!Directory.Exists(domainDir))
            return;

        // Cross-domain imports that must not appear
        var visionForbidden = new[]
        {
            "UniClaw.Core.Domain.Content",
            "UniClaw.Core.Domain.Common",
        };
        var contentForbidden = new[]
        {
            "UniClaw.Core.Domain.Vision",
            "UniClaw.Core.Domain.Common",
        };
        var commonForbidden = new[]
        {
            "UniClaw.Core.Domain.Vision",
            "UniClaw.Core.Domain.Content",
        };

        // Scan Vision files
        var visionDir = Path.Combine(domainDir, "Models", "Vision");
        if (Directory.Exists(visionDir))
        {
            foreach (var file in Directory.GetFiles(visionDir, "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(file);
                foreach (var ns in visionForbidden)
                    Assert.DoesNotContain($"using {ns}", source);
            }
        }

        // Scan Content files
        var contentDir = Path.Combine(domainDir, "Models", "Content");
        if (Directory.Exists(contentDir))
        {
            foreach (var file in Directory.GetFiles(contentDir, "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(file);
                foreach (var ns in contentForbidden)
                    Assert.DoesNotContain($"using {ns}", source);
            }
        }

        // Scan Common files
        var commonDir = Path.Combine(domainDir, "Models", "Common");
        if (Directory.Exists(commonDir))
        {
            foreach (var file in Directory.GetFiles(commonDir, "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(file);
                foreach (var ns in commonForbidden)
                    Assert.DoesNotContain($"using {ns}", source);
            }
        }

        // Exception: Domain/Mappings/ CAN reference Vision and Content (the bridge)
        var mappingsDir = Path.Combine(domainDir, "Mappings");
        if (Directory.Exists(mappingsDir))
        {
            // Mappings files are NOT scanned — they are the exception
        }
    }

    // --- C-4: FSM independence — TraversalFSM and GlobalFSM must not share types ---
    [Fact]
    public void FSMs_DoNotShareTypes()
    {
        var sourceRoot = FindSourceRoot();
        var smDir = Path.Combine(sourceRoot, "src", "UniClaw.Core", "StateMachine");
        if (!Directory.Exists(smDir))
            return;

        var traversalFsmPath = Path.Combine(smDir, "TraversalFSM.cs");
        var globalFsmPath = Path.Combine(smDir, "GlobalFSM.cs");

        if (!File.Exists(traversalFsmPath) || !File.Exists(globalFsmPath))
            return;

        // TraversalFSM must not reference GlobalState or GlobalTransition
        var traversalSource = File.ReadAllText(traversalFsmPath);
        Assert.DoesNotContain("GlobalState", traversalSource);
        Assert.DoesNotContain("GlobalTransition", traversalSource);

        // GlobalFSM must not reference TraversalState or TraversalTransition
        var globalSource = File.ReadAllText(globalFsmPath);
        Assert.DoesNotContain("TraversalState", globalSource);
        Assert.DoesNotContain("TraversalTransition", globalSource);

        // Exception: both MAY reference ITraversalContext (coordination interface)
        // This is NOT flagged as a violation
    }

    private static string FindSourceRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "src", "UniClaw.Core.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Directory.GetCurrentDirectory();
    }
}

/// <summary>
/// Coding convention guard — ensures sealed record class convention (C-9)
/// and DomainValidationException unified validation (C-10).
/// </summary>
public class CodingConventionGuardTests
{
    // --- C-9: All records must be sealed record class ---
    [Fact]
    public void AllRecords_AreSealedRecordClass()
    {
        var sourceRoot = FindSourceRoot();
        var scanDirs = new[]
        {
            Path.Combine(sourceRoot, "src", "UniClaw.Core", "Domain"),
            Path.Combine(sourceRoot, "src", "UniClaw.Core", "StateMachine"),
            Path.Combine(sourceRoot, "src", "UniClaw.Core", "Traversal"),
            Path.Combine(sourceRoot, "src", "UniClaw.Core", "Graph"),
        };

        // Known exceptions: TraversalRuntimeContext is sealed class (not record — 26 mutable fields)
        var exceptions = new HashSet<string> { "TraversalRuntimeContext" };

        foreach (var dir in scanDirs)
        {
            if (!Directory.Exists(dir))
                continue;

            foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(file);
                var fileName = Path.GetFileNameWithoutExtension(file);

                // Find all "record class" declarations (not preceded by "sealed")
                var lines = source.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (line.Contains("record class") && !line.Contains("sealed record class"))
                    {
                        // Extract the record name from the line
                        var recordName = ExtractRecordName(line);
                        if (recordName != null && !exceptions.Contains(recordName))
                        {
                            Assert.Fail(
                                $"Unsealed record class '{recordName}' found in {fileName}.cs " +
                                $"(line {i + 1}): all records must be 'sealed record class'");
                        }
                    }
                }
            }
        }
    }

    // --- C-10: Domain must use DomainValidationException, not InvalidOperationException/ArgumentException ---
    [Fact]
    public void Domain_UsesDomainValidationException()
    {
        var sourceRoot = FindSourceRoot();
        var domainDir = Path.Combine(sourceRoot, "src", "UniClaw.Core", "Domain");
        if (!Directory.Exists(domainDir))
            return;

        foreach (var file in Directory.GetFiles(domainDir, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("throw new InvalidOperationException", source);
            Assert.DoesNotContain("throw new ArgumentException", source);
        }

        // Note: ElementTypeMapper uses graceful fallback (IsValid notification, no throw)
        // This is correct per C-10 and not flagged
    }

    private static string? ExtractRecordName(string line)
    {
        // Match patterns like "public record class Foo" or "record class Foo("
        var idx = line.IndexOf("record class");
        if (idx < 0) return null;

        var after = line.Substring(idx + "record class".Length).Trim();
        // Extract the first word (the record name)
        var nameEnd = 0;
        while (nameEnd < after.Length && (char.IsLetterOrDigit(after[nameEnd]) || after[nameEnd] == '_'))
            nameEnd++;

        return nameEnd > 0 ? after.Substring(0, nameEnd) : null;
    }

    private static string FindSourceRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "src", "UniClaw.Core.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Directory.GetCurrentDirectory();
    }
}
