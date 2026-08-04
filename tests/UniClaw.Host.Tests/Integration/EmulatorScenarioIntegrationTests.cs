using System.Text.Json;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Host.Commands;
using UniClaw.Host.Scenarios;
using UniClaw.TraceTool;
using Xunit;

namespace UniClaw.Host.Tests.Integration;

/// <summary>
/// Android Settings emulator integration tests — all scenarios drive through
/// the Core <see cref="TraversalEngine"/> / <see cref="TraversalFSM"/>.
/// Skipped by default; opt in via <c>UNICLAW_INTEGRATION_SCOPES</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class EmulatorScenarioIntegrationTests
{
    [Trait("IntegrationScope", IntegrationTestScopes.ScenarioLocate)]
    [IntegrationFact(IntegrationTestScopes.ScenarioLocate)]
    public Task LocateOneItem_ThroughCoreEngine_Completes() =>
        RunScenarioAsync(
            "locate-one-item.v1.json",
            IntegrationTestScopes.ScenarioLocate);

    [Trait("IntegrationScope", IntegrationTestScopes.ScenarioEnumerate)]
    [IntegrationFact(IntegrationTestScopes.ScenarioEnumerate)]
    public Task EnumerateSettings_ThroughCoreEngine_Completes() =>
        RunScenarioAsync(
            "enumerate-settings-safely.v1.json",
            IntegrationTestScopes.ScenarioEnumerate);

    /// <summary>
    /// 从配置 providers.{id} 注入该 provider 的 env (P2.4/P2.7)。
    /// 优先级: 环境变量已设 (CI/手设) &gt; 配置文件。yoloModel/labelMapping 为
    /// repo-root 相对路径, testhost CWD 是 bin 目录, 注入前解析为绝对路径。
    /// 按 provider 分发: local → visionServer env; sensenova → intentModel env。
    /// </summary>
    private static void ApplyProviderEnv(ScenarioConfig scenario)
    {
        switch (scenario.Provider)
        {
            case "local":
                ApplyVisionServerEnv(scenario);
                return;
            case "sensenova":
                // P2.7: 意图推理模型 config 管辖, 注入 SENSENOVA_MODEL (Host CreateIntentExtractor 读)
                SetEnvIfAbsent("SENSENOVA_MODEL", scenario.ProviderConfig?.IntentModel);
                return;
        }
    }

    private static void ApplyVisionServerEnv(ScenarioConfig scenario)
    {
        var vision = scenario.ProviderConfig?.VisionServer;
        if (vision is null)
            return;

        SetEnvIfAbsent("UNICLAW_VISION_SOCK", vision.Socket);
        SetEnvIfAbsent("UNICLAW_VISION_PORT", vision.Port?.ToString());
        SetEnvIfAbsent("UNICLAW_OMP_THREADS", vision.OmpThreads.ToString());
        SetEnvIfAbsent("UNICLAW_OCR_BACKEND", vision.OcrBackend);
        SetEnvIfAbsent("UNICLAW_OCR_TEXT_SCORE", vision.OcrTextScore.ToString("0.##"));
        SetEnvIfAbsent("UNICLAW_YOLO_MODEL", ResolveRepoPath(vision.YoloModel));
        SetEnvIfAbsent("UNICLAW_LABEL_MAPPING", ResolveRepoPath(vision.LabelMapping));
    }

    private static void SetEnvIfAbsent(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
            Environment.SetEnvironmentVariable(name, value);
    }

    private static string? ResolveRepoPath(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
            return null;
        return Path.IsPathRooted(relative)
            ? relative
            : Path.Combine(AdbTestContext.RepoRoot, relative);
    }

    /// <summary>
    /// 启动横幅 (3.3): 装配完成后、跑 Host 前, 打印生效配置 — "跑了什么"跑前可见。
    /// env 显示注入通道 (ApplyProviderEnv) 的生效值 — SetEnvIfAbsent 手设/CI 优先。
    /// </summary>
    private static void PrintStartupBanner(
        ScenarioConfig scenario,
        string serial,
        string outputRoot)
    {
        string[] injected =
        [
            "UNICLAW_REPO_ROOT",
            "UNICLAW_VISION_SOCK",
            "UNICLAW_VISION_PORT",
            "UNICLAW_OMP_THREADS",
            "UNICLAW_OCR_BACKEND",
            "UNICLAW_OCR_TEXT_SCORE",
            "UNICLAW_YOLO_MODEL",
            "UNICLAW_LABEL_MAPPING",
            "SENSENOVA_MODEL",
        ];
        var envSummary = string.Join(
            " · ",
            injected
                .Select(name => (name, value: Environment.GetEnvironmentVariable(name)))
                .Where(pair => !string.IsNullOrWhiteSpace(pair.value))
                .Select(pair => $"{pair.name}={pair.value}"));
        var model = string.IsNullOrWhiteSpace(scenario.Model)
            ? "<不消费>" // local/mock: 模型名无消费方 (D-205)
            : scenario.Model;

        Console.WriteLine(
            $"[integration-config] 生效配置: scenario={scenario.Id} scope={scenario.Scope} "
            + $"provider={scenario.Provider} model={model} mode={scenario.Mode} timeout={scenario.TimeoutSeconds}s");
        Console.WriteLine(
            $"[integration-config] serial={serial} outputRoot={outputRoot}");
        Console.WriteLine(
            $"[integration-config] env (手设/CI 优先): {(envSummary.Length > 0 ? envSummary : "<无注入>")}");
        Console.WriteLine(
            $"[integration-config] preflight OK → 启动 Host ({scenario.File})");
    }

    private static async Task RunScenarioAsync(
        string scenarioFile,
        string scope)
    {
        var config = IntegrationConfigLoader.Load();
        var scenario = IntegrationConfigLoader.ResolveScenarioByFile(
            config, scenarioFile, scope);

        var serial = config.Emulator.Serial == "auto"
            ? await AdbTestContext.ResolveSerialAsync()
            : config.Emulator.Serial;

        // D4: testhost CWD is the bin dir, not the repo root — anchor all
        // local-vision path resolution via UNICLAW_REPO_ROOT instead.
        Environment.SetEnvironmentVariable(
            "UNICLAW_REPO_ROOT", AdbTestContext.RepoRoot);
        ApplyProviderEnv(scenario);

        // P2.10: 选中 provider 的运行时前提 (凭据/本地路径) 装配期校验，
        // 缺什么当场 fail-fast，而不是跑完一遍 Host 才炸。
        ProviderPreflight.Check(scenario, AdbTestContext.RepoRoot);

        var scenarioPath = Path.Combine(
            AppContext.BaseDirectory,
            "Scenarios",
            scenarioFile);
        Assert.True(File.Exists(scenarioPath), $"场景资产不存在: {scenarioPath}");

        var runTimestamp = DateTime.UtcNow.ToString(config.Emulator.RunNaming);
        var outputRoot = Path.Combine(
            AdbTestContext.RepoRoot,
            config.Emulator.OutputRoot,
            scenario.Scope,
            scenario.Id,
            runTimestamp);

        PrintStartupBanner(scenario, serial, outputRoot);

        var outcome = await new HostCompositionFactory().RunScenarioAsync(
            new HostCommandOptions(
                "run",
                serial,
                outputRoot,
                scenario.Provider,
                scenario.Model,
                scenario.Mode,
                scenarioPath));

        // V2 (unified-asset-pipeline): locate 模式 Host 不再自判 — 状态写
        // pending_verification, 终判交 TraceTool verify (任务 4.1);
        // enumerate 模式验证仍在 Host (ScenarioCompletionVerifier), 状态保持 success。
        var expectedRunStatus = string.Equals(
                scope,
                IntegrationTestScopes.ScenarioLocate,
                StringComparison.Ordinal)
            ? "pending_verification"
            : "success";
        Assert.Equal(expectedRunStatus, outcome.Status);
        Assert.True(outcome.Steps > 0, "场景没有经过 Core/FSM step loop");
        Assert.True(outcome.ActionsAttempted > 0, "场景没有尝试任何安全门控动作");

        var resultFiles = Directory.GetFiles(
            outputRoot,
            "result.json",
            SearchOption.AllDirectories);
        var resultPath = Assert.Single(resultFiles);
        using var result = JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
        Assert.Equal(
            expectedRunStatus,
            result.RootElement.GetProperty("status").GetString());
        Assert.True(
            result.RootElement.GetProperty("stepsConsumed").GetInt32() > 0,
            "result.json 未记录 FSM steps");

        if (string.Equals(
                scope,
                IntegrationTestScopes.ScenarioLocate,
                StringComparison.Ordinal))
        {
            Assert.True(
                outcome.SuccessCriteriaSatisfied,
                "locate 只允许在目标动作成功且动作后页面身份匹配时通过");
            Assert.Contains(
                outcome.SuccessEvidence,
                evidence => evidence.StartsWith(
                    "target_action_executed:",
                    StringComparison.Ordinal));
            Assert.Contains(
                outcome.SuccessEvidence,
                evidence => evidence.StartsWith(
                    "target_page_identity:",
                    StringComparison.Ordinal));

            Assert.True(
                result.RootElement
                    .GetProperty("successCriteriaSatisfied")
                    .GetBoolean(),
                "result.json 未确认 locate 成功条件");
            var successEvidence = result.RootElement
                .GetProperty("successEvidence")
                .EnumerateArray()
                .Select(item => item.GetString())
                .Where(item => item is not null)
                .Cast<string>()
                .ToArray();
            Assert.NotEmpty(successEvidence);
            Assert.Contains(
                successEvidence,
                evidence => evidence.StartsWith(
                    "target_action_executed:",
                    StringComparison.Ordinal));
            Assert.Contains(
                successEvidence,
                evidence => evidence.StartsWith(
                    "target_page_identity:",
                    StringComparison.Ordinal));
        }

        if (string.Equals(
                scope,
                IntegrationTestScopes.ScenarioEnumerate,
                StringComparison.Ordinal))
        {
            // 5.3: enumerate completes with all entries sampled/skipped
            // and end-of-list detected (post-hoc via analyzer).
            var entriesHandled = outcome.DiscoveredEntries
                + outcome.VisitedEntries
                + outcome.SkippedEntries;
            Assert.True(
                entriesHandled > 0,
                "enumerate 必须至少采样/跳过/发现一条一级入口");

            Assert.Equal(
                "enumerated_all_first_level",
                outcome.CompletionReason);

            Assert.True(
                outcome.SuccessCriteriaSatisfied,
                "enumerate 成功条件必须在引擎完成、全部入口已记账、"
                + "尽头已证明且回到 Settings 首页时通过");

            Assert.Contains(
                outcome.SuccessEvidence,
                evidence => evidence.StartsWith(
                    "first_level_discovered:",
                    StringComparison.Ordinal));
            Assert.Contains(
                outcome.SuccessEvidence,
                evidence => evidence.StartsWith(
                    "first_level_visited:",
                    StringComparison.Ordinal));
            Assert.Contains(
                outcome.SuccessEvidence,
                evidence => evidence.StartsWith(
                    "first_level_skipped:",
                    StringComparison.Ordinal));
            Assert.Contains(
                outcome.SuccessEvidence,
                evidence => evidence.StartsWith(
                    "end_of_list:",
                    StringComparison.Ordinal));
            Assert.Contains(
                outcome.SuccessEvidence,
                evidence => evidence.StartsWith(
                    "return_page_identity:",
                    StringComparison.Ordinal));

            Assert.True(
                result.RootElement
                    .GetProperty("successCriteriaSatisfied")
                    .GetBoolean(),
                "result.json 未确认 enumerate 成功条件");
            Assert.Equal(
                "enumerated_all_first_level",
                result.RootElement
                    .GetProperty("completionReason")
                    .GetString());
            Assert.True(
                result.RootElement.GetProperty("discoveredEntries").GetInt32()
                + result.RootElement.GetProperty("visitedEntries").GetInt32()
                + result.RootElement.GetProperty("skippedEntries").GetInt32()
                > 0,
                "result.json 未记录一级入口的发现/访问/跳过");
        }

        // 任务 4.1: run 完成后用 TraceTool verify 引擎做终判 — 进程内调用,
        // 与 verify CLI 共享同一规则引擎 (VerifyEngine → LocateOneItemRule, D-201)。
        // runDir 取 result.json 所在目录 (Assert.Single 已保证全树唯一)。
        var runDir = Path.GetDirectoryName(resultPath)!;
        var verifyInput = await RunEvidenceLoader.LoadAsync(runDir);
        var verifyContext = new VerificationContext
        {
            RunId = verifyInput.Run.RunId,
            Criteria = verifyInput.Criteria,
            LastAnalysisRow = verifyInput.LastAnalysisRow,
            CompletionReason = verifyInput.CompletionReason,
            TargetActionExecuted = verifyInput.TargetActionExecuted,
            ExpectedPageIdentities = verifyInput.Criteria?.ExpectedPageIdentities ?? [],
            Trace = (ITraceEventQuery)verifyInput.Run.Trace,
            Issues = verifyInput.Run.Issues,
        };
        var verifyResult = VerifyEngine.Verify(verifyContext);

        // locate: 终判应成功 (末页身份匹配 + 目标动作已执行);
        // enumerate: 无适用规则 (无 expected identities) → evidence_missing, 不断言。
        if (string.Equals(
                scope,
                IntegrationTestScopes.ScenarioLocate,
                StringComparison.Ordinal))
        {
            Assert.Equal("success", verifyResult.Status);
        }
    }
}
