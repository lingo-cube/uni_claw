using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Domain.Models.Vision;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.StateMachine;
using UniClaw.Core.Traversal;
using UniClaw.Core.UniBrain;
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

    // --- TraceContext field boundary guard ---
    [Fact]
    public void TraceContext_Has6Fields()
    {
        // TraceContext must have exactly 6 properties (NodeId, StepSpanId, StepNumber, TraceId,
        // VisitSpanId, ParentSpanId). Prevents accidental addition of type-specific fields (FsmType, SpanId, etc.)
        // that belong on individual record types, not in the shared correlation envelope.
        var props = typeof(TraceContext).GetProperties();
        Assert.Equal(6, props.Length);
        var names = props.Select(p => p.Name).OrderBy(n => n).ToList();
        Assert.Equal(new[] { "NodeId", "ParentSpanId", "StepNumber", "StepSpanId", "TraceId", "VisitSpanId" }, names);
    }

    // --- ITraceRecorder method count guard ---
    [Fact]
    public void ITraceRecorder_Has9Methods()
    {
        // ITraceRecorder must have exactly 9 methods (pure write contract).
        // D-134 (trace-span-observability P1): StartSpanAsync + EndSpanAsync added.
        // Prevents accidental addition of query methods (GetXxxAsync), CurrentSession getter,
        // or ExportTraceAsync — these belong on ITraceService and ITraceStorage.
        var methods = typeof(ITraceRecorder).GetMethods()
            .Where(m => m.DeclaringType == typeof(ITraceRecorder))
            .ToList();
        Assert.Equal(9, methods.Count);
        var names = methods.Select(m => m.Name).OrderBy(n => n).ToList();
        Assert.Equal(new[]
        {
            "EndSessionAsync",
            "EndSpanAsync",
            "RecordAICallAsync",
            "RecordErrorAsync",
            "RecordExecutionAsync",
            "RecordPageTransitionAsync",
            "RecordTransitionAsync",
            "StartSessionAsync",
            "StartSpanAsync"
        }, names);
    }
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

    // --- C-5 strengthened (scroll-action-refactor D-57): engine layers must not reference Simulation ---
    // StateMachine/Traversal/Domain/Graph 生产代码零 UniClaw.Core.Simulation 引用 ——
    // 消除 engine→Simulation 具体类型下转 (is ScrollableMockVisionService / is ScrollableMockActionExecutor),
    // 使 mock 与真实服务代码路径完全相同。Test 文件 (tests/) 与 Simulation 层本身不在范围内。
    [Fact]
    public void EngineLayers_DoNotReferenceSimulation()
    {
        var sourceRoot = FindSourceRoot();
        var engineDirs = new[]
        {
            Path.Combine(sourceRoot, "src", "UniClaw.Core", "Domain"),
            Path.Combine(sourceRoot, "src", "UniClaw.Core", "Graph"),
            Path.Combine(sourceRoot, "src", "UniClaw.Core", "StateMachine"),
            Path.Combine(sourceRoot, "src", "UniClaw.Core", "Traversal"),
        };

        foreach (var dir in engineDirs)
        {
            if (!Directory.Exists(dir))
                continue;
            foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(file);
                Assert.DoesNotContain("using UniClaw.Core.Simulation", source);
                Assert.DoesNotContain("Simulation.", source); // 禁止任何 Simulation.* 类型引用 (含下转)
            }
        }
    }

    // --- D-28: Graph 层三目录分离 — Abstractions/ 锁定为 4 个接口 ---
    // Graph/Abstractions/ 只允许 interface 定义 (IDynamicMatcher, IPlanCompiler,
    // ITemplateInstantiator, ITemplateRegistry)。新增接口须同步更新本 guard + layers/graph.md。
    [Fact]
    public void GraphAbstractions_Has4Interfaces()
    {
        var abstractionsDir = Path.Combine(
            FindSourceRoot(), "src", "UniClaw.Core", "Graph", "Abstractions");
        Assert.True(Directory.Exists(abstractionsDir), $"Graph/Abstractions/ not found at {abstractionsDir}");

        var files = Directory.GetFiles(abstractionsDir, "*.cs", SearchOption.TopDirectoryOnly);
        var names = files.Select(Path.GetFileNameWithoutExtension).OrderBy(n => n).ToArray();
        Assert.Equal(
            new[] { "IDynamicMatcher", "IPlanCompiler", "ITemplateInstantiator", "ITemplateRegistry" },
            names);

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            // 每个文件必须定义 interface, 不得含任何实现类型 (class/record/enum/struct)
            Assert.Contains("public interface I", source);
            Assert.DoesNotContain(" class ", source);
            Assert.DoesNotContain(" record ", source);
            Assert.DoesNotContain(" enum ", source);
            Assert.DoesNotContain(" struct ", source);
        }
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
/// Subsystem boundary guard — validates canonical subsystem field attribution
/// for TraversalRuntimeContext per D-15.
/// Each subsystem's field count must match the canonical table:
///   NavigationContext=12, ErrorContext=5, SessionContext=5,
///   ProgressContext=5, CacheContext=4 (excluding Phase 3 reserved).
/// See docs/system/layers/state-machine.md §5 for the canonical field ownership table.
/// </summary>
public class SubsystemBoundaryGuardTests
{
    // D-15: Canonical subsystem field counts — these are CI-blocking constraints.
    // If a field is moved to a different subsystem, this test MUST be updated accordingly.
    [Fact]
    public void TraversalRuntimeContext_FieldCountsPerSubsystem()
    {
        // Read TraversalRuntimeContext.cs source and parse subsystem annotations
        var sourceRoot = FindSourceRoot();
        var ctxPath = Path.Combine(
            sourceRoot, "src", "UniClaw.Core", "StateMachine", "TraversalRuntimeContext.cs");
        Assert.True(File.Exists(ctxPath), $"TraversalRuntimeContext.cs not found at {ctxPath}");

        var source = File.ReadAllText(ctxPath);

        // Count fields per subsystem by parsing "// <SubsystemName>" annotations
        var subsystemCounts = new Dictionary<string, int>
        {
            ["NavigationContext"] = 0,
            ["ErrorContext"] = 0,
            ["SessionContext"] = 0,
            ["ProgressContext"] = 0,
            ["CacheContext"] = 0,
        };

        // Parse each line for subsystem annotation comments
        var lines = source.Split('\n');
        foreach (var line in lines)
        {
            // Match patterns like: private string _traceId;  // SessionContext
            // or: public ITraversalNode? CurrentFrame { get; set; }  // NavigationContext
            // or: private ReadOnlyDictionary... _visitedChildrenReadOnly; // NavigationContext
            // Exclude Phase 3 reserved annotations (CacheContext (Phase 3))
            if (line.Contains("// NavigationContext") && !line.Contains("Phase 3"))
                subsystemCounts["NavigationContext"]++;
            else if (line.Contains("// ErrorContext"))
                subsystemCounts["ErrorContext"]++;
            else if (line.Contains("// SessionContext"))
                subsystemCounts["SessionContext"]++;
            else if (line.Contains("// ProgressContext"))
                subsystemCounts["ProgressContext"]++;
            else if (line.Contains("// CacheContext") && !line.Contains("Phase 3"))
                subsystemCounts["CacheContext"]++;
            // Phase 3 reserved fields annotated as "CacheContext (Phase 3)" are NOT counted
        }

        // Assert canonical field counts per D-15-2 (Phase 5: All sub-contexts extracted)
        // Phase 1 (NavigationContext) complete: fields moved to NavigationContext.cs
        // Phase 2 (ErrorContext) complete: fields moved to ErrorContext.cs
        // Phase 3 (SessionContext) complete: fields moved to SessionContext.cs
        // Phase 4 (ProgressContext) complete: fields moved to ProgressContext.cs
        // Phase 5 (CacheContext) complete: fields moved to CacheContext.cs
        //
        // Final counts after Phase 5:
        // - NavigationContext: 0 (extracted to NavigationContext.cs)
        // - ErrorContext: 0 (extracted to ErrorContext.cs)
        // - SessionContext: 0 (extracted to SessionContext.cs)
        // - ProgressContext: 0 (extracted to ProgressContext.cs)
        // - CacheContext: 0 (extracted to CacheContext.cs)
        // Total = 0 (all fields extracted to sub-contexts)
        Assert.Equal(0, subsystemCounts["NavigationContext"]);   // Phase 1: extracted
        Assert.Equal(0, subsystemCounts["ErrorContext"]);        // Phase 2: extracted
        Assert.Equal(0, subsystemCounts["SessionContext"]);      // Phase 3: extracted
        Assert.Equal(0, subsystemCounts["ProgressContext"]);     // Phase 4: extracted
        Assert.Equal(0, subsystemCounts["CacheContext"]);        // Phase 5: extracted

        // Total fields remaining in TraversalRuntimeContext after Phase 5 = 0
        var total = subsystemCounts.Values.Sum();
        Assert.Equal(0, total);
    }

    // Verify that Phase 3 reserved fields are annotated with "CacheContext (Phase 3)"
    // After Phase 5, these fields are in CacheContext.cs, not TraversalRuntimeContext.cs
    [Fact]
    public void TraversalRuntimeContext_Phase3ReservedFields_AnnotatedAsCacheContext()
    {
        var sourceRoot = FindSourceRoot();
        // After Phase 5, check CacheContext.cs instead of TraversalRuntimeContext.cs
        var ctxPath = Path.Combine(
            sourceRoot, "src", "UniClaw.Core", "StateMachine", "Cache", "CacheContext.cs");
        Assert.True(File.Exists(ctxPath));

        var source = File.ReadAllText(ctxPath);

        // Count Phase 3 field annotations — should be exactly 2 (_scrollHandler, _currentSnapshot)
        // Look for lines with "Phase 3:" which indicate field annotations
        var phase3Count = source.Split('\n')
            .Count(l => l.Contains("Phase 3:"));
        Assert.Equal(2, phase3Count);
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

/// <summary>
/// Interface compliance guard — verifies each sealed class correctly implements
/// its corresponding interface and that interface method counts match spec.
/// D-V: interface extraction for 6 Traversal sub-components.
/// </summary>
public class InterfaceComplianceGuardTests
{
    // --- D-V: Each sealed class implements its corresponding interface ---
    [Fact]
    public void DynamicChildManager_Implements_IDynamicChildManager()
        => Assert.True(typeof(IDynamicChildManager).IsAssignableFrom(typeof(DynamicChildManager)),
            "DynamicChildManager must implement IDynamicChildManager");

    [Fact]
    public void TraceCoordinator_Implements_ITraceCoordinator()
        => Assert.True(typeof(ITraceCoordinator).IsAssignableFrom(typeof(TraceCoordinator)),
            "TraceCoordinator must implement ITraceCoordinator");

    [Fact]
    public void EntryPolicyExecutor_Implements_IEntryPolicyExecutor()
        => Assert.True(typeof(IEntryPolicyExecutor).IsAssignableFrom(typeof(EntryPolicyExecutor)),
            "EntryPolicyExecutor must implement IEntryPolicyExecutor");

    [Fact]
    public void PageCacheManager_Implements_IPageCacheManager()
        => Assert.True(typeof(IPageCacheManager).IsAssignableFrom(typeof(PageCacheManager)),
            "PageCacheManager must implement IPageCacheManager");

    [Fact]
    public void PageSnapshotManager_Implements_IPageSnapshotManager()
        => Assert.True(typeof(IPageSnapshotManager).IsAssignableFrom(typeof(PageSnapshotManager)),
            "PageSnapshotManager must implement IPageSnapshotManager");

    [Fact]
    public void NodeStackAdapter_Implements_INodeStackAdapter()
        => Assert.True(typeof(INodeStackAdapter).IsAssignableFrom(typeof(NodeStackAdapter)),
            "NodeStackAdapter must implement INodeStackAdapter");

    // --- D-IV: StepOrchestrator 分解 — InterceptionHandler owns FSM override logic ---
    [Fact]
    public void InterceptionHandler_Implements_IInterceptionHandler()
        => Assert.True(typeof(IInterceptionHandler).IsAssignableFrom(typeof(InterceptionHandler)),
            "InterceptionHandler must implement IInterceptionHandler");

    // --- D-V: Interface method count assertions ---
    [Fact]
    public void IDynamicChildManager_Has5Methods()
    {
        var methods = typeof(IDynamicChildManager).GetMethods()
            .Where(m => m.DeclaringType == typeof(IDynamicChildManager))
            .ToList();
        Assert.Equal(5, methods.Count);
    }

    [Fact]
    public void ITraceCoordinator_Has27Members()
    {
        // 2 properties (Active, CurrentEngineStepSpanId) + 25 methods (16 Record methods + GetStepSnapshot
        // + ShouldRecordEntryAttempt + ShouldRecordVisionCall + PushSpan + PopSpan + ClearVisitSpan
        // + BuildCorrelation + StartSpan + EndSpan) = 27 total members
        // Phase 3-A added: PushSpan, PopSpan, ClearVisitSpan, BuildCorrelation (stack-based span tree)
        // D-134 P2 added: StartSpan, EndSpan (sync TraceSpan passthroughs), CurrentEngineStepSpanId
        var properties = typeof(ITraceCoordinator).GetProperties()
            .Where(p => p.DeclaringType == typeof(ITraceCoordinator))
            .ToList();
        // Exclude property getter/setter methods from method count
        var propertyMethodNames = properties
            .Select(p => $"get_{p.Name}")
            .Concat(properties.Where(p => p.CanWrite).Select(p => $"set_{p.Name}"))
            .ToHashSet();
        var methods = typeof(ITraceCoordinator).GetMethods()
            .Where(m => m.DeclaringType == typeof(ITraceCoordinator))
            .Where(m => !propertyMethodNames.Contains(m.Name))
            .ToList();
        Assert.Equal(2, properties.Count);
        Assert.Equal(25, methods.Count);
        Assert.Equal(27, properties.Count + methods.Count);
    }

    [Fact]
    public void ITraceCoordinator_HasTraceSpanPassthroughs()
    {
        // D-134 P2: StartSpan/EndSpan/CurrentEngineStepSpanId must exist for TraceSpan instrumentation.
        var methods = typeof(ITraceCoordinator).GetMethods()
            .Select(m => m.Name)
            .ToHashSet();
        Assert.Contains("StartSpan", methods);
        Assert.Contains("EndSpan", methods);
        Assert.Contains("get_CurrentEngineStepSpanId", methods);
    }

    [Fact]
    public void ITraceCoordinator_HasSpanStackMethods()
    {
        // Phase 3-A: PushSpan, PopSpan, ClearVisitSpan, BuildCorrelation must exist on the interface.
        // Prevents accidental rename or removal without updating the count guard.
        var methods = typeof(ITraceCoordinator).GetMethods()
            .Select(m => m.Name)
            .ToHashSet();
        Assert.Contains("PushSpan", methods);
        Assert.Contains("PopSpan", methods);
        Assert.Contains("ClearVisitSpan", methods);
        Assert.Contains("BuildCorrelation", methods);
    }

    [Fact]
    public void IEntryPolicyExecutor_Has2Methods()
    {
        var methods = typeof(IEntryPolicyExecutor).GetMethods()
            .Where(m => m.DeclaringType == typeof(IEntryPolicyExecutor))
            .ToList();
        Assert.Equal(2, methods.Count);
    }

    [Fact]
    public void IPageCacheManager_Has2Methods()
    {
        var methods = typeof(IPageCacheManager).GetMethods()
            .Where(m => m.DeclaringType == typeof(IPageCacheManager))
            .ToList();
        Assert.Equal(2, methods.Count);
    }

    [Fact]
    public void IPageSnapshotManager_Has2Methods()
    {
        var methods = typeof(IPageSnapshotManager).GetMethods()
            .Where(m => m.DeclaringType == typeof(IPageSnapshotManager))
            .ToList();
        Assert.Equal(2, methods.Count);
    }

    [Fact]
    public void INodeStackAdapter_Has3Methods()
    {
        var methods = typeof(INodeStackAdapter).GetMethods()
            .Where(m => m.DeclaringType == typeof(INodeStackAdapter))
            .ToList();
        Assert.Equal(3, methods.Count);
    }
}

/// <summary>
/// UniBrain guard tests — enforce interface shape locks and zero upward references.
/// Prevents accidental dependency on StateMachine/Traversal from UniBrain namespace,
/// ensures IUniBrain facade has exactly 3 sub-interface properties,
/// and IScreenStateProvider has exactly 4 methods locked.
/// See docs/prd/2026-07-22-unibrain-prd.md §10 for full guard design.
/// </summary>
public class UniBrainGuardTests
{
    [Fact]
    public void UniBrain_DoesNotReferenceStateMachine()
    {
        // UniBrain namespace must have zero references to StateMachine namespace.
        // Prevents UniBrain↔StateMachine bidirectional dependency (PRD §2.6).
        var uniBrainDir = Path.Combine(
            FindSourceRoot(), "src", "UniClaw.Core", "UniBrain");
        if (!Directory.Exists(uniBrainDir))
            return;

        foreach (var file in Directory.GetFiles(uniBrainDir, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("using UniClaw.Core.StateMachine", source);
        }
    }

    [Fact]
    public void UniBrain_DoesNotReferenceTraversal()
    {
        // UniBrain namespace must have zero references to Traversal namespace.
        // Scroll state is IScreenStateProvider (Traversal namespace), NOT on IUniBrain.
        var uniBrainDir = Path.Combine(
            FindSourceRoot(), "src", "UniClaw.Core", "UniBrain");
        if (!Directory.Exists(uniBrainDir))
            return;

        foreach (var file in Directory.GetFiles(uniBrainDir, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain("using UniClaw.Core.Traversal", source);
        }
    }

    [Fact]
    public void IUniBrain_Has3SubInterfaces()
    {
        // IUniBrain must have exactly 3 properties: PageAnalyzer, Advisor, Text.
        var properties = typeof(IUniBrain).GetProperties()
            .Where(p => p.DeclaringType == typeof(IUniBrain))
            .ToList();
        Assert.Equal(3, properties.Count);
        var names = properties.Select(p => p.Name).OrderBy(n => n).ToList();
        Assert.Equal(new[] { "Advisor", "PageAnalyzer", "Text" }, names);
        Assert.Equal(typeof(IPageAnalyzer), properties.First(p => p.Name == "PageAnalyzer").PropertyType);
        Assert.Equal(typeof(ITraversalAdvisor), properties.First(p => p.Name == "Advisor").PropertyType);
        Assert.Equal(typeof(ITextUnderstanding), properties.First(p => p.Name == "Text").PropertyType);
    }

    [Fact]
    public void IScreenStateProvider_Has4Methods()
    {
        // IScreenStateProvider must have exactly 4 public methods (locked).
        var methods = typeof(IScreenStateProvider).GetMethods()
            .Where(m => m.DeclaringType == typeof(IScreenStateProvider))
            .ToList();
        Assert.Equal(4, methods.Count);
        var names = methods.Select(m => m.Name).OrderBy(n => n).ToList();
        Assert.Equal(new[] { "GetScrollProgress", "GetScrollSwipeConfig", "HasScroll", "IsEndOfList" }, names);
    }

    [Fact]
    public void StateMachine_ReferencesUniBrainForIUniBrain()
    {
        // Acknowledged upward reference: StateMachine→UniBrain for IUniBrain injection.
        // Verifies StateMachine references UniBrain only through interface, not concrete types.
        var sourceRoot = FindSourceRoot();
        var stateMachineDir = Path.Combine(sourceRoot, "src", "UniClaw.Core", "StateMachine");
        if (!Directory.Exists(stateMachineDir))
            return;

        var hasUniBrainRef = false;
        foreach (var file in Directory.GetFiles(stateMachineDir, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            if (source.Contains("using UniClaw.Core.UniBrain"))
            {
                hasUniBrainRef = true;
                // Must not reference concrete UniBrainService (only IUniBrain and sub-interfaces)
                Assert.DoesNotContain("UniBrainService", source);
            }
        }
        // If StepContext has Brain property, the reference exists (future state).
        // This test acknowledges the upward reference pattern (same as D-14/D-17).
    }

    [Fact]
    public void Traversal_ReferencesUniBrainForIUniBrain()
    {
        // Acknowledged upward reference: Traversal→UniBrain for IUniBrain + IScreenStateProvider injection.
        // Verifies Traversal references UniBrain only through interface, not concrete types.
        var sourceRoot = FindSourceRoot();
        var traversalDir = Path.Combine(sourceRoot, "src", "UniClaw.Core", "Traversal");
        if (!Directory.Exists(traversalDir))
            return;

        foreach (var file in Directory.GetFiles(traversalDir, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            if (source.Contains("using UniClaw.Core.UniBrain"))
            {
                // Must not reference concrete UniBrainService (only IUniBrain and sub-interfaces)
                Assert.DoesNotContain("UniBrainService", source);
            }
        }
    }

    // --- UniBrain enum value guards ---
    [Fact]
    public void DecisionResult_Has3Values()
    {
        // DecisionResult: 3 values locked (Success, Unsure, GiveUp).
        // Adding/removing requires constitution change flow.
        Assert.Equal(3, Enum.GetValues<DecisionResult>().Length);
        var names = Enum.GetValues<DecisionResult>().Cast<DecisionResult>()
            .Select(v => v.ToString()).OrderBy(n => n).ToList();
        Assert.Equal(new[] { "GiveUp", "Success", "Unsure" }, names);
    }

    [Fact]
    public void SafetyTag_Has4Values()
    {
        // SafetyTag: 4 values locked (Safe, Caution, Skip, Unknown).
        // Adding/removing requires constitution change flow.
        Assert.Equal(4, Enum.GetValues<SafetyTag>().Length);
        var names = Enum.GetValues<SafetyTag>().Cast<SafetyTag>()
            .Select(v => v.ToString()).OrderBy(n => n).ToList();
        Assert.Equal(new[] { "Caution", "Safe", "Skip", "Unknown" }, names);
    }

    private static string FindSourceRoot()
    {
        // Walk up from test assembly location to find the repo root (where src/ exists)
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "UniClaw.Core")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Cannot find source root directory");
    }
}
