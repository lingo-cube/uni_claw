using System.Text.RegularExpressions;
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
        "### I-13", "### I-14",
    };

    /// <summary>
    /// Guard 5：Trap 类型声明级匹配（TrapKind / TrapScope / Trap — HG-1：Phase 2 一等模型，
    /// 只允许存在于 Model/ 与 Recovery/）。
    /// 只匹配「声明关键字 + 类型名」模式（record/class/struct/interface/enum/delegate），
    /// 不匹配注释 / 正文里的讨论文字（如文档注释中的「Trap 一等模型」）。
    /// 长名优先（TrapKind / TrapScope 先于 Trap），避免正则交替短名吞前缀。
    /// </summary>
    private static readonly Regex TrapTypeDeclarationRegex = new(
        @"\b(?:record|class|struct|interface|enum|delegate)\s+(?:TrapKind|TrapScope|Trap)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Guard 5：RecoveryRequest 类型声明级匹配（恢复请求模型仍 DEFER — 全库禁止，含 Model/ 与 Recovery/）。</summary>
    private static readonly Regex RecoveryRequestTypeDeclarationRegex = new(
        @"\b(?:record|class|struct|interface|enum|delegate)\s+RecoveryRequest\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Guard 7：Recovery/ 禁止引用的组件 namespace（HG-1 冻结边界 — 恰好 2 项：Container / Traversal；无更多限制）。</summary>
    private static readonly string[] RecoveryForbiddenNamespaceReferences =
    {
        "UniClaw.Runtime.Container",
        "UniClaw.Runtime.Traversal",
    };

    /// <summary>Guard 6：Model 层 coordinate / hierarchy 类型声明级匹配（裁决 3 — coordinate/hierarchy grounding DEFER）。</summary>
    private static readonly Regex CoordinateTypeDeclarationRegex = new(
        @"\b(?:record|class|struct|interface|enum)\s+(?:Coordinate|Coordinates|Hierarchy|Hierarchical|BoundingBox|Bounds)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Guard 6：Model 层 coordinate 成员名声明级匹配（X / Y / Left / Top / CenterX / CenterY）。
    /// 要求「访问修饰符 + 类型 + 成员名 + { / = / ;」声明形状，且成员名以词边界匹配——
    /// 不匹配注释 / 字符串里的讨论文字（如文档注释「不新增 coordinate / hierarchy model」）。
    /// </summary>
    private static readonly Regex CoordinateMemberDeclarationRegex = new(
        @"\b(?:public|private|internal|protected)\s+(?:static\s+|readonly\s+|required\s+|sealed\s+|const\s+)*[\w<>\[\]?,.]+?\s+(?:X|Y|Left|Top|CenterX|CenterY)\b\s*(?:[{=;])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

    // ── Guard 5: Trap 类型只允许在 Model/ 与 Recovery/；RecoveryRequest 全库禁止（HG-1/HG-2）──────────

    [Fact]
    public void RuntimeSource_TrapTypesAllowedOnlyInModelOrRecovery()
    {
        var sourceDir = RepoRootPath(RuntimeSourceDir);
        Assert.True(Directory.Exists(sourceDir), BuildFileMissing(sourceDir));

        // Model/（数据定义）与 Recovery/（恢复语义 — 目录尚未创建；按路径前缀放行，不要求目录存在）之外禁止 Trap 类型
        var trapFiles = Directory.EnumerateFiles(sourceDir, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !IsUnderDirectory(p, "Model")
                     && !IsUnderDirectory(p, "Recovery"))
            .ToList();

        foreach (var file in trapFiles)
        {
            var content = File.ReadAllText(file);
            var violations = TrapTypeDeclarationRegex.Matches(content)
                .Cast<Match>()
                .Select(m => m.Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            Assert.True(
                violations.Count == 0,
                BuildGuardViolation(
                    $"违反了什么: {file} 声明了 Trap 类型「{string.Join(" / ", violations)}」"
                    + "（Trap 只允许存在于 Model/ 与 Recovery/）",
                    "为什么违反: HG-1 — Trap 一等模型是数据定义（Model）+ 恢复语义（Recovery）的产物；出现在 "
                    + "Agent/Container/Traversal/Startup/World/Environment 意味着把 Trap 决策 / 发射逻辑泄漏进执行层，"
                    + "破坏裁决 4 的组件边界。",
                    "应该读: " + ContractDoc + " I-8 / 裁决 4 + HG-1；Trap 类型只能落在 Model/ 或 Recovery/"));
        }
    }

    [Fact]
    public void RuntimeSource_RecoveryRequestType_BannedEverywhere()
    {
        var sourceDir = RepoRootPath(RuntimeSourceDir);
        Assert.True(Directory.Exists(sourceDir), BuildFileMissing(sourceDir));

        // RecoveryRequest 全库禁止（含 Model/ 与 Recovery/ — 恢复请求模型仍 DEFER）
        var allFiles = Directory.EnumerateFiles(sourceDir, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        foreach (var file in allFiles)
        {
            var content = File.ReadAllText(file);
            var violations = RecoveryRequestTypeDeclarationRegex.Matches(content)
                .Cast<Match>()
                .Select(m => m.Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            Assert.True(
                violations.Count == 0,
                BuildGuardViolation(
                    $"违反了什么: {file} 声明了「{string.Join(" / ", violations)}」",
                    "为什么违反: Phase 2 当前仅购买 Trap 发射语义（HG-1 / HG-2）；RecoveryRequest 恢复请求模型"
                    + "仍 DEFER（裁决 4 — recovery 半句未购买）——出现即把未批准的恢复模型偷渡进契约边界。",
                    "应该读: " + ContractDoc + " I-8 / 裁决 4 + HG-1 / HG-2；引入恢复请求模型必须走 " + OpenSpecChange));
        }
    }

    // ── Guard 6: 生产 Model 层不得声明 coordinate / hierarchy 类型与成员 ───────────────────────────────

    [Fact]
    public void RuntimeModel_NoCoordinateOrHierarchyModelDeclarations()
    {
        var modelDir = RepoRootPath(Path.Combine(RuntimeSourceDir, "Model"));
        Assert.True(Directory.Exists(modelDir), BuildFileMissing(modelDir));

        var modelFiles = Directory.EnumerateFiles(modelDir, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        foreach (var file in modelFiles)
        {
            var content = File.ReadAllText(file);
            var typeViolations = CoordinateTypeDeclarationRegex.Matches(content)
                .Cast<Match>().Select(m => m.Value).Distinct(StringComparer.Ordinal).ToList();
            var memberViolations = CoordinateMemberDeclarationRegex.Matches(content)
                .Cast<Match>().Select(m => m.Value).Distinct(StringComparer.Ordinal).ToList();
            Assert.True(
                typeViolations.Count == 0 && memberViolations.Count == 0,
                BuildGuardViolation(
                    $"违反了什么: {file} 声明了 coordinate / hierarchy 模型或成员"
                    + (typeViolations.Count > 0 ? $"【类型: {string.Join(" / ", typeViolations)}】" : "")
                    + (memberViolations.Count > 0 ? $"【成员: {string.Join(" / ", memberViolations)}】" : ""),
                    "为什么违反: 裁决 3 — grounding 仅使用 Text + SwitchState? 证据；coordinate-based 与 "
                    + "hierarchy-based grounding 均 DEFER 到未来场景购买（scenario-catalog SC-P1-005 架构断言："
                    + "生产 Model / 行为中无 coordinate / hierarchy 字段或模型）。",
                    "应该读: " + ContractDoc + " 裁决 3；引入坐标 / 层级模型必须走 " + OpenSpecChange));
        }
    }

    // ── Guard 7: Recovery/ 不得依赖 Container / Traversal（HG-1 冻结边界；Phase 2A 预置围栏）────────────

    [Fact]
    public void RuntimeSource_Recovery_HasNoContainerOrTraversalNamespaceReferences()
    {
        var recoveryDir = RepoRootPath(Path.Combine(RuntimeSourceDir, "Recovery"));
        Assert.True(Directory.Exists(recoveryDir), BuildFileMissing(recoveryDir));

        // 当前 Recovery/ 仅 .gitkeep（零 .cs）——围栏在 Phase 2B 写入代码前即生效，平凡通过
        var recoveryFiles = Directory.EnumerateFiles(recoveryDir, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        foreach (var file in recoveryFiles)
        {
            var content = File.ReadAllText(file);
            foreach (var banned in RecoveryForbiddenNamespaceReferences)
            {
                // 纯 Contains 扫描（Guard 2 模式）：alias import（using X = UniClaw.Runtime.Container.Container;）
                // 同样会命中完整 namespace 字符串
                Assert.False(
                    content.Contains(banned, StringComparison.Ordinal),
                    BuildGuardViolation(
                        $"违反了什么: {file} 引用了「{banned}」（Recovery/ 不得依赖 Container / Traversal）",
                        "为什么违反: HG-1 冻结边界 — Recovery → Container / Recovery → Traversal 双向禁止；"
                        + "I-1 依赖方向为 Agent → Container → Traversal → Environment，Recovery 不属于该执行链，"
                        + "依赖执行组件会把恢复机制耦合进 traversal 执行面（design §7 恢复机制独立于执行）。",
                        "应该读: " + ContractDoc + " I-1 + HG-1 + design.md §7"));
            }
        }
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

    /// <summary>判断文件路径是否位于指定目录下（目录名两侧带分隔符 — 精确前缀匹配，如 Model/、Recovery/；不要求目录存在）。</summary>
    private static bool IsUnderDirectory(string filePath, string directoryName)
        => filePath.Contains(
            $"{Path.DirectorySeparatorChar}{directoryName}{Path.DirectorySeparatorChar}",
            StringComparison.Ordinal);

    private static string BuildFileMissing(string path)
        => BuildGuardViolation(
            $"违反了什么: 预期文件/目录不存在「{path}」",
            "为什么违反: Greenfield 工程地基不完整 — 该文件是 Architecture Contract / Guard 机制的一部分。",
            "应该读: " + ContractDoc + " §3 相关入口；结构见 " + OpenSpecChange + " design.md");

    private static string BuildGuardViolation(string what, string why, string read)
        => $"\n\n[UniClaw.Runtime Architecture Guard 失败]\n{what}\n{why}\n{read}\n";
}
