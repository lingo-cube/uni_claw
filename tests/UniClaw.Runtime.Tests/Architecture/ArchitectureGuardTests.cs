using Xunit;

namespace UniClaw.Runtime.Tests.Architecture;

/// <summary>
/// Greenfield 隔离约束 — 机械保证 docs/system/constitution/runtime-architecture-contract.md。
/// Guard 失败信息明确告知 Coding Agent:
///   违反了什么 / 为什么违反 / 应该读哪个文档。
/// 约束语义见 Contract §0/§1/§2 与 openspec/changes/greenfield-agent-runtime/。
/// </summary>
public class ArchitectureGuardTests
{
    private const string ContractRelativePath = "docs/system/constitution/runtime-architecture-contract.md";
    private const string ContractDoc = "docs/system/constitution/runtime-architecture-contract.md（New Runtime Architecture Contract）";
    private const string OpenSpecChange = "openspec/changes/greenfield-agent-runtime/（OpenSpec change: greenfield-agent-runtime）";
    private const string AgentsNavigationSection = "AGENTS.md「Agent Runtime（新）— Greenfield」段";
    private const string RuntimeCsprojRelativePath = "src/UniClaw.Runtime/UniClaw.Runtime.csproj";
    private const string RuntimeSourceDir = "src/UniClaw.Runtime";

    private static readonly string[] BannedNamespaces =
    {
        "UniClaw.Core.Traversal",
        "UniClaw.Core.StateMachine",
    };

    private static readonly string[] ContractInvariants =
    {
        "### I-1", "### I-2", "### I-3", "### I-4", "### I-5", "### I-6",
        "### I-7", "### I-8", "### I-9", "### I-10", "### I-11", "### I-12",
    };

    // ── Guard 1: UniClaw.Runtime.csproj 不得引用任何现有 project ──────────────

    [Fact]
    public void UniClawRuntime_Csproj_HasNoProjectReferences()
    {
        var path = RepoRootPath(RuntimeCsprojRelativePath);
        Assert.True(File.Exists(path), BuildFileMissing(path));

        var content = File.ReadAllText(path);
        Assert.False(
            content.Contains("<ProjectReference", StringComparison.Ordinal),
            BuildGuardViolation(
                $"违反了什么: {RuntimeCsprojRelativePath} 包含 ProjectReference（引用 UniClaw.Core 或其它现有 project）",
                "为什么违反: 第一阶段必须保持 Greenfield 隔离。Domain/Graph/Traversal/StateMachine 仍在同一 UniClaw.Core "
                + "assembly，引用它等于暴露全部旧控制结构，工程边界立即失效。",
                "应该读: " + ContractDoc + " §2 依赖边界；复用决策走 " + OpenSpecChange + "（Extract Foundation / Create Adapter / Reuse Contract）"));
    }

    // ── Guard 2: 源码不得出现旧 Runtime namespace 引用 ────────────────────────

    [Fact]
    public void UniClawRuntime_Source_HasNoLegacyRuntimeNamespaceReferences()
    {
        var sourceDir = RepoRootPath(RuntimeSourceDir);
        Assert.True(Directory.Exists(sourceDir), BuildFileMissing(sourceDir));

        var sourceFiles = Directory.EnumerateFiles(sourceDir, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            foreach (var banned in BannedNamespaces)
            {
                Assert.False(
                    content.Contains(banned, StringComparison.Ordinal),
                    BuildGuardViolation(
                        $"违反了什么: {file} 引用旧 Runtime namespace「{banned}」",
                        "为什么违反: Contract I-11 — 新 Runtime 不继承旧 TraversalEngine / StepOrchestrator / "
                        + "InterceptionHandler / TraversalRuntimeContext 的控制结构，引用其 namespace 会逐步滑向复制旧设计。",
                        "应该读: " + ContractDoc + " §0 定位 + I-11；如需复用能力走 " + OpenSpecChange));
            }
        }
    }

    // ── Guard 3: Contract 文档存在、12 invariants 齐全、AGENTS.md 导航有效 ────

    [Fact]
    public void RuntimeArchitectureContract_DocumentExists_WithAllInvariants()
    {
        var path = RepoRootPath(ContractRelativePath);
        Assert.True(File.Exists(path), BuildFileMissing(path));

        var content = File.ReadAllText(path);
        foreach (var invariant in ContractInvariants)
        {
            Assert.True(
                content.Contains(invariant, StringComparison.Ordinal),
                BuildGuardViolation(
                    $"违反了什么: {ContractRelativePath} 缺少 invariant 标题「{invariant}」",
                    "为什么违反: Contract 是新 Runtime 的边界契约，12 条 invariant 全部必须可机械核对，缺一条即失去约束力。",
                    "应该读: " + ContractDoc + " §1 Invariants；修改契约需走 " + OpenSpecChange));
        }
    }

    [Fact]
    public void AgentsNavigation_PointsToRuntimeContract()
    {
        var agentsPath = RepoRootPath("AGENTS.md");
        Assert.True(File.Exists(agentsPath), BuildFileMissing(agentsPath));

        var content = File.ReadAllText(agentsPath);
        var contractAnchor = "docs/system/constitution/runtime-architecture-contract.md";
        Assert.True(
            content.Contains(contractAnchor, StringComparison.Ordinal),
            BuildGuardViolation(
                $"违反了什么: AGENTS.md 缺少对「{contractAnchor}」的导航链接",
                "为什么违反: Harness Engineering — AGENTS.md 是导航地图；Coding Agent 找不到契约文档 = 契约不存在。",
                "应该读: " + AgentsNavigationSection + "（只加导航，不重构 AGENTS.md）"));
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    /// <summary>从测试输出目录向上找仓库根（含 AGENTS.md 的目录）。</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "无法定位仓库根目录（从测试输出目录向上未找到 AGENTS.md）。"
            + " Guard 失败是测试环境问题，不是 Runtime 违约。");
    }

    private static string RepoRootPath(string relative)
        => Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    private static string BuildFileMissing(string path)
        => BuildGuardViolation(
            $"违反了什么: 预期文件/目录不存在「{path}」",
            "为什么违反: Greenfield 工程地基不完整 — 该文件是 Architecture Contract / Guard 机制的一部分。",
            "应该读: " + ContractDoc + " §3 相关入口；结构见 " + OpenSpecChange + " design.md");

    private static string BuildGuardViolation(string what, string why, string read)
        => $"\n\n[UniClaw.Runtime Architecture Guard 失败]\n{what}\n{why}\n{read}\n";
}
