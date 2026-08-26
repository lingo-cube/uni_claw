using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Xunit;

namespace UniClaw.Runtime.Tests.ValidationHarness;

/// <summary>
/// WI-EVH-006 7.2 source-shape + dependency guards, reused from the accepted
/// ArchitectureGuardTests pattern (comment-stripped source scans). They assert
/// the harness DECLARES:
///   1. no Planner inference — no strategy-verb / prose-planning tokens in
///      harness sources (the DIRECTIVE_REQUIRED marker sentence is the single
///      carved-out cell: it states the negative of inference);
///   2. no mutation / FSM surface — no member named Authorize/Complete/
///      Transition/Dispatch declared on harness types, no StateMachine
///      reference, no DeviceAction construction;
///   3. no scenario knowledge beyond its own fixtures — UI feature tokens and
///      the fixture page-name vocabulary appear ONLY inside the explicitly
///      enumerated fixture whitelist (fixture world + fixture catalog + the
///      tests that drive them);
///   4. the frozen wire/DTO/strategy source remains byte-identical — each
///      listed file's SHA-256 must equal the baseline constant computed at
///      implementation time (post-WI-EVH-004, zero harness edits to them; the
///      pre-existing in-flight Phase-2 working-tree diff on StrategyContract.cs
///      is documented context, NOT a harness edit);
///   5. zero reverse references — no production project (Runtime, DriverHost,
///      Harness, PhysicalHost) references the harness; harness references only
///      Runtime/DriverHost/Harness (design D1), never PhysicalHost/Adapters/
///      Vision.Host (F1).
/// Scans are comment-stripped tripwires, per the accepted guard pattern; they
/// assert capability shape (declared surface, byte identity), never fixed
/// click counts, coordinates, page text, or UI paths.
/// </summary>
public sealed class HarnessSourceShapeGuardTests
{
    private const string HarnessSourceDir = "src/UniClaw.Runtime.ValidationHarness";
    private const string HarnessTestDir = "tests/UniClaw.Runtime.Tests/ValidationHarness";

    // ── 7.2a: no Planner inference (comment-stripped token scan) ───────────────

    /// <summary>The ONLY allowed occurrence of an inference-verb token in
    /// harness sources: the normative DIRECTIVE_REQUIRED marker sentence, which
    /// states that the driver never authors a strategy. Carved out for exactly
    /// that file cell (WI-EVH-006: DIRECTIVE_REQUIRED handling is allowed).</summary>
    private const string DirectiveRequiredCarveOutFile = "src/UniClaw.Runtime.ValidationHarness/Emulator/EmulatorCallLog.cs";
    private const string DirectiveRequiredMarkerLiteral = "DIRECTIVE_REQUIRED: only goal prose was supplied; the driver never synthesizes a strategy (design D2, spec 'No strategy inference').";

    private static readonly string[] PlannerInferenceTokens =
    {
        "synthesize",
        "infer strategy",
        "infer a strategy",
        "plan from prose",
        "derive a strategy from prose",
    };

    [Fact]
    public void HarnessSources_DeclareNoPlannerInferenceTokens()
    {
        foreach (var file in HarnessSourceFiles())
        {
            var content = File.ReadAllText(file);
            if (IsFile(file, DirectiveRequiredCarveOutFile))
            {
                // The carve-out must still target the real normative marker.
                Assert.True(
                    content.Contains(DirectiveRequiredMarkerLiteral, StringComparison.Ordinal),
                    GuardViolation(
                        $"违反了什么: {DirectiveRequiredCarveOutFile} 不再包含 DIRECTIVE_REQUIRED 规范句",
                        "为什么违反: 该句是 'No strategy inference' 的唯一规范表达（design D2），删除/改写使归档证据失真",
                        "应该读: openspec/changes/uniagent-emulator-validation-harness/design.md D2 + spec 'No strategy inference'"));
                content = content.Replace(DirectiveRequiredMarkerLiteral, "DIRECTIVE_REQUIRED_MARKER", StringComparison.Ordinal);
            }

            var stripped = StripComments(content);
            foreach (var token in PlannerInferenceTokens)
            {
                Assert.False(
                    stripped.Contains(token, StringComparison.OrdinalIgnoreCase),
                    GuardViolation(
                        $"违反了什么: {Relative(file)} 出现 Planner 推断 token「{token}」",
                        "为什么违反: harness 是验证工具，不是 Planner（design D2/D6 authority proof: Harness → Planner）——"
                        + "任何策略推断语言都宣告了推断代码路径",
                        "应该读: openspec/changes/uniagent-emulator-validation-harness/design.md D2 + D6 authority-proof 表"));
            }
        }
    }

    // ── 7.2b: no mutation / FSM surface declared on harness types ──────────────

    private static readonly Regex MutationSurfaceMethodRegex = new(
        @"\b(?:public|private|internal|protected)\b\s+(?:static\s+|readonly\s+|required\s+|sealed\s+|virtual\s+|abstract\s+|async\s+)*[\w<>\[\]?,.]+?\b\s+(?:Authorize|Complete|Transition|Dispatch)\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MutationSurfaceTypeRegex = new(
        @"\b(?:record|class|struct|interface|enum)\s+(?:Authorize|Complete|Transition|Dispatch)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex StateMachineReferenceRegex = new(
        @"\bStateMachine\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DeviceActionConstructionRegex = new(
        @"\bnew\s+DeviceAction\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void HarnessTypes_DeclareNoMutationOrFsmSurface()
    {
        foreach (var file in HarnessSourceFiles())
        {
            var stripped = StripComments(File.ReadAllText(file));
            var methodViolations = MutationSurfaceMethodRegex.Matches(stripped)
                .Select(m => m.Value).Distinct(StringComparer.Ordinal).ToArray();
            var typeViolations = MutationSurfaceTypeRegex.Matches(stripped)
                .Select(m => m.Value).Distinct(StringComparer.Ordinal).ToArray();
            var fsmViolations = StateMachineReferenceRegex.Matches(stripped)
                .Select(m => m.Value).Distinct(StringComparer.Ordinal).ToArray();
            var actionViolations = DeviceActionConstructionRegex.Matches(stripped)
                .Select(m => m.Value).Distinct(StringComparer.Ordinal).ToArray();

            Assert.True(
                methodViolations.Length == 0 && typeViolations.Length == 0
                && fsmViolations.Length == 0 && actionViolations.Length == 0,
                GuardViolation(
                    $"违反了什么: {Relative(file)} 声明了突变/FSM 表面"
                    + (methodViolations.Length > 0 ? $"【方法: {string.Join(" / ", methodViolations)}】" : "")
                    + (typeViolations.Length > 0 ? $"【类型: {string.Join(" / ", typeViolations)}】" : "")
                    + (fsmViolations.Length > 0 ? $"【FSM: {string.Join(" / ", fsmViolations)}】" : "")
                    + (actionViolations.Length > 0 ? $"【动作构造: {string.Join(" / ", actionViolations)}】" : ""),
                    "为什么违反: harness 不得拥有 Runtime 变异 / FSM 控制 / 动作注入能力（design D6 authority proof: "
                    + "Harness → Runtime mutation / Harness → FSM / Harness → Action injection 三条禁止边）",
                    "应该读: openspec/changes/uniagent-emulator-validation-harness/design.md D6 authority-proof 表"));
        }
    }

    // ── 7.2c: no scenario knowledge beyond the fixture whitelist ───────────────

    /// <summary>
    /// The minimal, explicitly enumerated scenario-token whitelist: the fixture
    /// world + fixture catalog sources, and the tests that drive them (the
    /// driver's forbidden-content cases deliberately carry UI-path tokens as
    /// INPUT data — that is the point of those tests). Nothing else may mention
    /// the fixture UI vocabulary.
    /// </summary>
    private static readonly string[] ScenarioTokenWhitelistedFiles =
    {
        "src/UniClaw.Runtime.ValidationHarness/Fixtures/DirectiveFixtureCatalog.cs",
        "src/UniClaw.Runtime.ValidationHarness/Fixtures/FixtureComposition.cs",
        "src/UniClaw.Runtime.ValidationHarness/Fixtures/FixtureSemanticEnvironment.cs",
        "src/UniClaw.Runtime.ValidationHarness/Fixtures/FixtureStrategyBinding.cs",
        "src/UniClaw.Runtime.ValidationHarness/Fixtures/ValidationFixtureWorld.cs",
        "tests/UniClaw.Runtime.Tests/ValidationHarness/EmulatorDriverTests.cs",
    };

    [Fact]
    public void ScenarioKnowledgeTokens_AppearOnlyInsideTheFixtureWhitelist()
    {
        // Tokens are assembled at runtime so this guard file itself cannot
        // trip its own scan (the whitelist must stay minimal and enumerated).
        var featureTokens = new[] { "wi" + "fi" };
        var fixturePageTokens = new[] { "settings" + "root", "connectivity" + "settings", "display" + "settings" };
        var scanSet = HarnessSourceFiles().Concat(HarnessTestFiles()).ToList();
        Assert.True(scanSet.Count >= 20,
            GuardViolation(
                "违反了什么: 守护扫描面为空或过小",
                "为什么违反: 空扫描面使 7.2c 守护平凡通过，失去守护意义",
                "应该读: WI-EVH-006 tasks.md 7.2"));

        foreach (var file in scanSet)
        {
            var relative = Relative(file);
            if (ScenarioTokenWhitelistedFiles.Contains(relative, StringComparer.Ordinal))
            {
                continue;
            }

            var stripped = StripComments(File.ReadAllText(file));
            foreach (var token in featureTokens.Concat(fixturePageTokens))
            {
                Assert.False(
                    stripped.Contains(token, StringComparison.OrdinalIgnoreCase),
                    GuardViolation(
                        $"违反了什么: {relative} 出现场景知识 token「{token}」（fixture 白名单之外）",
                        "为什么违反: harness 不得携带白名单外的场景知识——场景词汇只允许存在于 fixture 世界与其测试"
                        + "（design D2: directive authorship 在 Emulator/fixture；spec 'no scenario knowledge beyond fixtures'）",
                        "应该读: openspec/changes/uniagent-emulator-validation-harness/design.md D2 + spec 'Emulator driver boundary'"));
            }

            var coordinateMatches = Regex.Matches(stripped, @"\d+x\d+");
            Assert.Empty(coordinateMatches);
        }

        // Whitelist integrity: every enumerated file must exist (no stale entry).
        foreach (var relative in ScenarioTokenWhitelistedFiles)
        {
            Assert.True(File.Exists(RepoRootPath(relative)),
                GuardViolation(
                    $"违反了什么: 白名单文件不存在「{relative}」",
                    "为什么违反: 白名单必须指向真实存在的 fixture 世界/目录/测试文件",
                    "应该读: WI-EVH-006 tasks.md 7.2 白名单纪律"));
        }
    }

    // ── 7.2d: frozen wire/DTO/strategy source stays byte-identical ─────────────

    /// <summary>
    /// SHA-256 baselines computed AT IMPLEMENTATION TIME (WI-EVH-006, baseline
    /// taken post-WI-EVH-004 with zero harness edits to these files). They
    /// prove byte-identity going forward: any future harness-era change to
    /// these files trips this guard. The pre-existing in-flight Phase-2 diff
    /// in the working tree on StrategyContract.cs is documented context — it is
    /// NOT a harness edit and was already present when the baseline was taken.
    /// </summary>
    private static readonly (string File, string Sha256)[] FrozenSourceBaselines =
    {
        ("src/UniClaw.Runtime/Planning/StrategyContract.cs", "6a732a046026095384458e7ce63f95685623b8138c9c7811635455d2a89cc80e"),
        ("src/UniClaw.Runtime.DriverHost/Transport/UniClawDriverHostServer.cs", "24aedeaf27abcca8f7bb00c168300da37c8cc85568a056b5000a7936ca195647"),
        ("src/UniClaw.Runtime.DriverHost/Model/EvidenceRef.cs", "e8c9a0758d4fe3524d73c01e12750da9c87d1a80efb3f9b5520c3646c13e046b"),
        ("src/UniClaw.Runtime.DriverHost/Model/RunSnapshot.cs", "5ac056b2dcf41363c683446be0c59d869530a19f2728c2f69a11daa457f257cd"),
        ("src/UniClaw.Runtime.DriverHost/Model/RuntimeEventEnvelope.cs", "7345349c38f90038f62cdbf55923c4c4231390c161303c5956362ea711dca87a"),
        ("src/UniClaw.Runtime.DriverHost/Model/RuntimeEventKind.cs", "3ce5e6105a8bd92fee2c85826530cbf003a61736a32591b33af9556309185f88"),
        ("src/UniClaw.Runtime.DriverHost/Model/RuntimeEventKindTable.cs", "ff7ea595d131227d2dcd41d354e2b1f74e9c33a46e74c42ff4ab794ddd79aac2"),
    };

    [Fact]
    public void FrozenWireAndDtoSources_AreByteIdenticalToThePostApplyBaseline()
    {
        foreach (var (relative, expectedSha256) in FrozenSourceBaselines)
        {
            var path = RepoRootPath(relative);
            Assert.True(File.Exists(path), GuardViolation($"违反了什么: 冻结文件不存在「{relative}」", "", ""));
            var actualSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
            Assert.True(
                string.Equals(expectedSha256, actualSha256, StringComparison.Ordinal),
                GuardViolation(
                    $"违反了什么: 冻结 wire/DTO/协议源不再 byte-identical「{relative}」"
                    + $"\n基线:    {expectedSha256}\n当前:    {actualSha256}",
                    "为什么违反: harness 不得改动冻结 wire/DTO/协议源（design.md D6 authority proof: "
                    + "Harness → new wire/API 禁止；spec 'Contract-frozen surfaces untouched': byte-identical）",
                    "应该读: openspec/changes/uniagent-emulator-validation-harness/design.md D6 + spec 'Contract-frozen surfaces untouched'"));
        }
    }

    // ── 7.2e: zero reverse references — production never references harness ───

    [Fact]
    public void NoProductionProjectReferencesTheValidationHarness()
    {
        var productionProjects = new[]
        {
            "src/UniClaw.Runtime/UniClaw.Runtime.csproj",
            "src/UniClaw.Runtime.DriverHost/UniClaw.Runtime.DriverHost.csproj",
            "src/UniClaw.Runtime.Harness/UniClaw.Runtime.Harness.csproj",
            "src/UniClaw.Runtime.PhysicalHost/UniClaw.Runtime.PhysicalHost.csproj",
        };
        foreach (var relative in productionProjects)
        {
            var path = RepoRootPath(relative);
            Assert.True(File.Exists(path), GuardViolation($"违反了什么: 生产项目文件不存在「{relative}」", "", ""));
            var content = File.ReadAllText(path);
            Assert.False(
                content.Contains("UniClaw.Runtime.ValidationHarness", StringComparison.Ordinal),
                GuardViolation(
                    $"违反了什么: {relative} 引用了 ValidationHarness（禁止方向：生产 → harness）",
                    "为什么违反: design.md D1 — 依赖只允许 harness → Runtime/DriverHost/Harness；"
                    + "Runtime 生产保持 byte-identical，harness 仅被测试引用",
                    "应该读: openspec/changes/uniagent-emulator-validation-harness/design.md D1"));
        }

        // Allowed forward edges (design D1) + F1: the harness never references
        // PhysicalHost (production entry, fakes forbidden there); it owns its
        // fixture compositions. Tier B (Human-authorized 2026-08-26) composes
        // the REAL production pipeline — AdbScreenshotSource /
        // LocalVisionPerceptionSource / AdbDispatchTarget live in Adapters and
        // the managed vision host in Vision.Host — so those two forward
        // references are authorized FOR THE TIER B REAL-DEVICE COMPOSITION,
        // in read/client roles only; no fake ever enters PhysicalHost (F1).
        var harnessCsproj = RepoRootPath("src/UniClaw.Runtime.ValidationHarness/UniClaw.Runtime.ValidationHarness.csproj");
        var harnessContent = File.ReadAllText(harnessCsproj);
        foreach (var allowed in new[] { "UniClaw.Runtime.csproj", "UniClaw.Runtime.Harness.csproj", "UniClaw.Runtime.DriverHost.csproj", "UniClaw.Runtime.Adapters.csproj", "UniClaw.Vision.Host.csproj" })
        {
            Assert.True(
                harnessContent.Contains(allowed, StringComparison.Ordinal),
                GuardViolation(
                    $"违反了什么: harness csproj 未引用允许的「{allowed}」",
                    "为什么违反: design D1 — harness 引用 Runtime/Harness/DriverHost 是授权方向",
                    "应该读: openspec/changes/uniagent-emulator-validation-harness/design.md D1"));
        }
        foreach (var forbidden in new[] { "UniClaw.Runtime.PhysicalHost" })
        {
            Assert.False(
                harnessContent.Contains(forbidden, StringComparison.Ordinal),
                GuardViolation(
                    $"违反了什么: harness csproj 引用了「{forbidden}」",
                    "为什么违反: F1 — PhysicalHost 禁止 fakes；harness 拥有自己的 fixture 组合，不进入生产入口",
                    "应该读: openspec/changes/uniagent-emulator-validation-harness/design.md D1 + F1"));
        }
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    /// <summary>All harness project sources, excluding build artifacts.</summary>
    private static IEnumerable<string> HarnessSourceFiles()
        => EnumerateCs(RepoRootPath(HarnessSourceDir));

    /// <summary>All harness test sources.</summary>
    private static IEnumerable<string> HarnessTestFiles()
        => EnumerateCs(RepoRootPath(HarnessTestDir));

    private static IEnumerable<string> EnumerateCs(string directory)
        => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static bool IsFile(string fullPath, string relative)
        => string.Equals(Relative(fullPath), relative, StringComparison.Ordinal);

    private static string Relative(string fullPath)
        => Path.GetRelativePath(RepoRoot(), fullPath).Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>Comment-stripped source scan (accepted ArchitectureGuardTests
    /// pattern): removes block and line (incl. XML doc) comments. String
    /// literals are not token-probed — the guards are declarations tripwires.</summary>
    private static string StripComments(string source)
    {
        source = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        source = Regex.Replace(source, @"//[^\r\n]*", string.Empty);
        return source;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(dir.FullName, "src", "UniClaw.Runtime.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "无法定位仓库根目录（从测试输出目录向上未找到同时含 AGENTS.md 与 src/UniClaw.Runtime.sln 的目录）。"
            + " Guard 失败是测试环境问题，不是 Runtime 违约。");
    }

    private static string RepoRootPath(string relative)
        => Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    private static string GuardViolation(string what, string why, string read)
        => $"\n\n[UniClaw.Runtime ValidationHarness Source-Shape Guard 失败]\n{what}\n{why}\n{read}\n";
}