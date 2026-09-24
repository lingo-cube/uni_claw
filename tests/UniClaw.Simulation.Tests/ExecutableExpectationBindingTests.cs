using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace UniClaw.Simulation.Tests;

/// <summary>
/// SIM-003 G9/G10 — Executable Expectation Tripwire + M1–M7 mutation 证明。
///
/// 本测试族**不是第二真源**：期望的 authoring truth 只在 scenarios/*.json。
/// 它证明的是——
///   tripwire：projection 没有被未来代码绕过（bundle.Expected 的 canonical
///             digest == certification.expectationsDigest，carrier/options
///             == certified execution 绑定）；
///   M1–M7：  改期望不重认证 / 绕过投影 / 改运行时源 / LF↔CRLF / 改
///             Type-B 断言 / 改 carrier-options 不重认证，各自的失败面。
/// </summary>
public sealed class ExecutableExpectationBindingTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "UniClaw.Kernel.slnx")))
                return directory.FullName;
            directory = directory.Parent!;
        }
        throw new InvalidOperationException("未定位到仓库根");
    }

    private static string ScenariosDir() => Path.Combine(RepoRoot(), "scenarios");

    /// <summary>temp 副本目录（单个场景文件；mutation 测试不触碰仓库文件）。</summary>
    private static string TempScenarioCopy(string scenarioId)
    {
        var dir = Path.Combine(Path.GetTempPath(), "uniclaw-sim003-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.Copy(
            Path.Combine(ScenariosDir(), scenarioId + ".json"),
            Path.Combine(dir, scenarioId + ".json"));
        return dir;
    }

    private static JsonDocument ReadScenarioJson(string scenariosDir, string scenarioId) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(scenariosDir, scenarioId + ".json")));

    private static string CertifiedExpectationDigest(string scenariosDir, string scenarioId)
    {
        using var document = ReadScenarioJson(scenariosDir, scenarioId);
        return document.RootElement.GetProperty("certification").GetProperty("expectationsDigest").GetString()!;
    }

    /// <summary>跨语言对拍辅助：python --print-source-hash（fail-closed）。</summary>
    private static string PythonSourceHash()
    {
        Exception? failure = null;
        foreach (var candidate in new[] { "python", "python3" })
        {
            try
            {
                var info = new ProcessStartInfo(candidate, "tools/scenario_certify.py --print-source-hash")
                {
                    WorkingDirectory = RepoRoot(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                using var process = Process.Start(info)!;
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(30_000);
                if (process.ExitCode == 0 && output.Length == 64)
                    return output;
                failure = new InvalidOperationException(
                    $"{candidate} 退出码 {process.ExitCode}: {output[..Math.Min(120, output.Length)]}");
            }
            catch (Exception e)
            {
                failure = e;
            }
        }
        throw new InvalidOperationException(
            "python 不可用或 --print-source-hash 失败（M5 跨语言对拍无法执行；"
            + "安装 python3 并确保 tools/scenario_certify.py 可运行）", failure);
    }

    // =====================================================================
    // Tripwire（G9）：全库 golden-bundle 场景的 certified 绑定证明
    // =====================================================================

    /// <summary>
    /// 对每个 execution.kind=golden-bundle 条目：bundle.Expected 的
    /// canonical digest 必须 == certification.expectationsDigest；
    /// 实际 carrier/options 必须 == certified execution 绑定。
    /// 本测试是绕过投影的执法报警（M3），不拥有任何期望值。
    /// </summary>
    [Fact]
    public void Tripwire_EveryGoldenBundleScenario_ExpectationDigestAndCarrierMatchCertification()
    {
        var violations = new List<string>();
        var carriersInUse = new HashSet<string>(StringComparer.Ordinal);
        var noneKind = new List<string>();

        foreach (var file in Directory.EnumerateFiles(ScenariosDir(), "SCN-*.json")
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            var scenarioId = Path.GetFileNameWithoutExtension(file);
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var root = document.RootElement;
            var kind = root.GetProperty("execution").GetProperty("kind").GetString();

            if (kind == "none")
            {
                noneKind.Add(scenarioId);
                // fail-closed：kind=none 显式拒绝 bundle 解析（保证范围声明机械执法）
                Assert.Throws<ScenarioLibraryException>(() => ScenarioLibrary.Load(scenarioId));
                continue;
            }

            LibraryScenario scenario;
            try
            {
                scenario = ScenarioLibrary.Load(scenarioId);
            }
            catch (Exception e)
            {
                violations.Add($"{scenarioId}: ScenarioLibrary.Load 失败——{e.Message}");
                continue;
            }

            // 绑定核心：certified expectation digest == executable digest
            var executableDigest = ScenarioCertification.DigestOf(
                ScenarioCertification.SnapshotOf(scenario.Bundle.Expected));
            var certifiedDigest = root.GetProperty("certification").GetProperty("expectationsDigest").GetString();
            if (executableDigest != certifiedDigest)
                violations.Add(
                    $"{scenarioId}: executable expectation digest {executableDigest[..12]}…"
                    + $" != certified {(certifiedDigest is { } cd ? cd[..Math.Min(12, cd.Length)] : "-")}…（投影被绕过或认证失配）");

            // carrier 绑定：实际 carrier == certified execution.carrier
            var certifiedCarrier = root.GetProperty("execution").GetProperty("carrier").GetString();
            if (scenario.Carrier != certifiedCarrier)
                violations.Add($"{scenarioId}: carrier '{scenario.Carrier}' != certified '{certifiedCarrier}'");

            // options 绑定：RunOptions 投影 == certified execution.options
            var expectedOptions = new RunOptions();
            if (root.GetProperty("execution").TryGetProperty("options", out var optionsElement))
            {
                foreach (var option in optionsElement.EnumerateObject())
                {
                    var value = option.Value.GetBoolean();
                    expectedOptions = option.Name switch
                    {
                        "duplicateActivation" => expectedOptions with { DuplicateActivation = value },
                        "phased" => expectedOptions with { Phased = value },
                        _ => expectedOptions,
                    };
                }
            }
            if (scenario.Options.DuplicateActivation != expectedOptions.DuplicateActivation
                || scenario.Options.Phased != expectedOptions.Phased)
                violations.Add($"{scenarioId}: RunOptions 投影 != certified execution.options");

            carriersInUse.Add(scenario.Carrier);
        }

        // 注册表一致性：不存在无 scenario 消费者的死 carrier（注册表腐化检查）
        var deadCarriers = ScenarioLibrary.RegisteredCarriers
            .Where(key => !carriersInUse.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
        if (deadCarriers.Count > 0)
            violations.Add("死 carrier（无 certified scenario 消费）: " + string.Join(", ", deadCarriers));

        Assert.True(violations.Count == 0,
            "SIM-003 tripwire 违规：\n" + string.Join("\n", violations));
    }

    // =====================================================================
    // M1 — 改 JSON expectation，不重认证：certification + projection 双失败
    // =====================================================================

    [Fact]
    public void M1_TamperedExpectations_RefusedAtProjection_AndFailsCertification()
    {
        var dir = TempScenarioCopy("SCN-WIFI-001");
        try
        {
            var path = Path.Combine(dir, "SCN-WIFI-001.json");
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"effects\": 1", "\"effects\": 2"));

            // 投影门：拒绝投影（certified digest ≠ tampered 内容 digest）
            var projection = Assert.Throws<ExpectationProjectionException>(
                () => ScenarioExpectations.Load("SCN-WIFI-001", dir));
            Assert.Contains("expectations 摘要不匹配", projection.Message);

            // 认证门：VerifyFile 报违规
            var violations = ScenarioCertification.VerifyFile(path, RepoRoot());
            Assert.Contains(violations, v => v.Contains("expectations 摘要不匹配"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // =====================================================================
    // M2 — 改 JSON expectation + 合法重认证：executable expectation 自动变化
    // =====================================================================

    [Fact]
    public void M2_RecertifiedExpectation_FlowsIntoBundle_WithoutCSharpEdits()
    {
        var dir = TempScenarioCopy("SCN-WIFI-001");
        try
        {
            // 模拟 --change SIM-003 合法重认证：改期望值 + 重盖 expectationsDigest
            // （certify 工具写入的就是「当前内容」的 digest）
            using (var document = ReadScenarioJson(dir, "SCN-WIFI-001"))
            {
                var root = JsonSerializer.SerializeToNode(document.RootElement)!;
                root["expectations"]!["effects"] = 2;
                var snapshot = new ScenarioCertification.ExpectationsSnapshot(
                    root["expectations"]!["status"]!.GetValue<string>(),
                    root["expectations"]!["classification"]?.GetValue<string>(),
                    root["expectations"]!["effects"]!.GetValue<int>(),
                    root["expectations"]!["agentConsultations"]!.GetValue<int>(),
                    root["expectations"]!["unconsumedStimuli"]!.GetValue<int>(),
                    root["expectations"]!["goalSatisfaction"]?.GetValue<string>());
                root["certification"]!["expectationsDigest"] = ScenarioCertification.DigestOf(snapshot);
                File.WriteAllText(
                    Path.Combine(dir, "SCN-WIFI-001.json"),
                    root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
            }

            // 零 C# 修改：投影自动携带新期望（bundle 接线
            // factory(scenarioId) → Load(scenarioId) 由 tripwire 在库级证明；
            // 此处验证认证后的期望值无需任何 C# 改动即可变化）
            var expectation = ScenarioExpectations.Load("SCN-WIFI-001", dir);
            Assert.Equal(2, expectation.ExpectedEffects);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // =====================================================================
    // M3 — 绕过 projection（手改 executable Expected）：tripwire 必然检出
    // =====================================================================

    [Fact]
    public void M3_BypassedProjection_TripwireDigestMismatch()
    {
        var scenario = ScenarioLibrary.Load("SCN-WIFI-001");
        var certified = CertifiedExpectationDigest(ScenariosDir(), "SCN-WIFI-001");

        // 正向对照：未篡改 bundle 的 digest == certified digest
        Assert.Equal(certified, ScenarioCertification.DigestOf(
            ScenarioCertification.SnapshotOf(scenario.Bundle.Expected)));

        // 绕过模拟：在投影之后再手改 executable Expected（重封 digest——
        // integrity 层无从发现，唯一能抓到的是 tripwire 的 certified 比对）
        var tampered = ScenarioBundleDigest.Sealed(scenario.Bundle with
        {
            Expected = scenario.Bundle.Expected with { ExpectedEffects = 99 },
        });
        Assert.NotEqual(certified, ScenarioCertification.DigestOf(
            ScenarioCertification.SnapshotOf(tampered.Expected)));
    }

    // =====================================================================
    // M5 — LF/CRLF checkout 不改变 runtimeSourceHash；python/C# 逐字节一致
    // =====================================================================

    [Fact]
    public void M5_RuntimeSourceHash_IsCheckoutInvariant()
    {
        // 同内容两份源树（LF vs CRLF）→ 同 digest
        string MakeRoot(bool crlf)
        {
            var root = Path.Combine(Path.GetTempPath(), "uniclaw-sim003-src-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "src", "UniClaw.Kernel"));
            Directory.CreateDirectory(Path.Combine(root, "src", "UniClaw.Agent"));
            var content = "// probe\nclass K { }\n";
            var bytes = crlf
                ? content.Replace("\n", "\r\n")
                : content;
            File.WriteAllText(Path.Combine(root, "src", "UniClaw.Kernel", "KernelProbe.cs"), bytes);
            File.WriteAllText(Path.Combine(root, "src", "UniClaw.Agent", "AgentProbe.cs"), bytes);
            return root;
        }
        string lf, crlf;
        try
        {
            var lfRoot = MakeRoot(crlf: false);
            var crlfRoot = MakeRoot(crlf: true);
            try
            {
                lf = ScenarioCertification.RuntimeSourceHash(lfRoot);
                crlf = ScenarioCertification.RuntimeSourceHash(crlfRoot);
            }
            finally
            {
                Directory.Delete(lfRoot, recursive: true);
                Directory.Delete(crlfRoot, recursive: true);
            }
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"temp 源树构建/哈希失败: {e.Message}", e);
        }
        Assert.Equal(lf, crlf);
    }

    [Fact]
    public void M5_RuntimeSourceHash_CSharpMatchesPythonByteForByte()
    {
        // 跨语言对拍：python --print-source-hash 与 C# RuntimeSourceHash 同值
        // （两侧 canonical 逐字节一致是认证真值链的前提；python 缺失 = 环境失败，fail-closed）
        Assert.Equal(PythonSourceHash(), ScenarioCertification.RuntimeSourceHash(RepoRoot()));
    }

    // =====================================================================
    // M6 — 只改 Type-B 断言（tests/ 源）：不影响 certification
    // =====================================================================

    [Fact]
    public void M6_TestOnlyEdit_DoesNotTouchRuntimeSourceHash()
    {
        // runtimeSourceHash 的输入集只有 src/UniClaw.Kernel 与 src/UniClaw.Agent；
        // tests/ 源（Type-B 断言所在地）的改动哈希不可见 → 认证不受影响
        string MakeRoot(string extraTestFile)
        {
            var root = Path.Combine(Path.GetTempPath(), "uniclaw-sim003-m6-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "src", "UniClaw.Kernel"));
            Directory.CreateDirectory(Path.Combine(root, "src", "UniClaw.Agent"));
            File.WriteAllText(Path.Combine(root, "src", "UniClaw.Kernel", "Probe.cs"), "class K { }\n");
            if (extraTestFile is not null)
            {
                Directory.CreateDirectory(Path.Combine(root, "tests", "UniClaw.Simulation.Tests"));
                File.WriteAllText(Path.Combine(root, "tests", "UniClaw.Simulation.Tests", extraTestFile), "// Type-B edit\n");
            }
            return root;
        }
        var without = MakeRoot(null);
        var with = MakeRoot("DeterministicScenarioTests.cs");
        try
        {
            Assert.Equal(
                ScenarioCertification.RuntimeSourceHash(without),
                ScenarioCertification.RuntimeSourceHash(with));
        }
        finally
        {
            Directory.Delete(without, recursive: true);
            Directory.Delete(with, recursive: true);
        }
    }

    // =====================================================================
    // M7 — 改 execution.carrier / options 不重认证：executionDigest 失败
    // =====================================================================

    [Fact]
    public void M7_TamperedCarrierOrOptions_RefusedAtProjection_AndFailsCertification()
    {
        var dir = TempScenarioCopy("SCN-WIFI-003"); // carrier + duplicateActivation:true
        try
        {
            var path = Path.Combine(dir, "SCN-WIFI-003.json");

            // carrier 篡改：wifi-off-to-on → wifi-already-on（不重认证）
            File.WriteAllText(path, File.ReadAllText(path).Replace("wifi-off-to-on", "wifi-already-on"));
            var projection = Assert.Throws<ExpectationProjectionException>(
                () => ScenarioLibrary.Load("SCN-WIFI-003", dir));
            Assert.Contains("execution 摘要不匹配", projection.Message);
            Assert.Contains("execution 摘要不匹配",
                Assert.Throws<ExpectationProjectionException>(
                    () => ScenarioExpectations.Load("SCN-WIFI-003", dir)).Message);
            Assert.Contains(ScenarioCertification.VerifyFile(path, RepoRoot()),
                v => v.Contains("execution 摘要不匹配"));

            // options 篡改：duplicateActivation true → false（不重认证）
            File.WriteAllText(path, File.ReadAllText(path)
                .Replace("\"duplicateActivation\": true", "\"duplicateActivation\": false"));
            Assert.Contains("execution 摘要不匹配",
                Assert.Throws<ExpectationProjectionException>(
                    () => ScenarioLibrary.Load("SCN-WIFI-003", dir)).Message);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // =====================================================================
    // v1 legacy 认证 fail-closed（G2：不偷偷兼容无法验证的旧认证）
    // =====================================================================

    [Fact]
    public void LegacyV1Certification_FailsClosed_AtProjectionAndVerify()
    {
        var dir = TempScenarioCopy("SCN-WIFI-001");
        try
        {
            var path = Path.Combine(dir, "SCN-WIFI-001.json");
            File.WriteAllText(path, File.ReadAllText(path)
                .Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1"));

            var projection = Assert.Throws<ExpectationProjectionException>(
                () => ScenarioExpectations.Load("SCN-WIFI-001", dir));
            Assert.Contains("schemaVersion", projection.Message);

            Assert.Contains(ScenarioCertification.VerifyFile(path, RepoRoot()),
                v => v.Contains("schemaVersion"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // =====================================================================
    // M4 的机制面由 ScenarioCertificationTests.StaleRuntimeSourceHash 覆盖；
    // 此处补投影侧范围声明：projection 刻意不校验 runtimeSourceHash
    // （源码哈希执法属认证层，见该测试）。
    // =====================================================================
}
