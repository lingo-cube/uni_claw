using System.Text.Json;
using UniClaw.Host.Commands;
using Xunit;

namespace UniClaw.Host.Tests.Integration;

/// <summary>
/// 两个 Android Settings 实机场景的最终显式门禁。两项测试都通过生产
/// <see cref="HostCompositionFactory"/>，因此设备操作只能由 Host 组装的
/// Core TraversalEngine/TraversalFSM 链路驱动，并生成完整 run assets。
/// 默认基线不执行；仅在修改对应场景、设备边界、视觉或 FSM 链路时按 scope 运行。
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

    private static async Task RunScenarioAsync(
        string scenarioFile,
        string scope)
    {
        var serial = await AdbTestContext.ResolveSerialAsync();
        var scenarioPath = Path.Combine(
            AppContext.BaseDirectory,
            "Scenarios",
            scenarioFile);
        Assert.True(File.Exists(scenarioPath), $"场景资产不存在: {scenarioPath}");

        var provider = Environment.GetEnvironmentVariable(
                           "UNICLAW_INTEGRATION_PROVIDER")
                       ?? (string.Equals(
                               scope,
                               IntegrationTestScopes.ScenarioEnumerate,
                               StringComparison.Ordinal)
                           ? "mock"
                           : "sensenova");
        var model = Environment.GetEnvironmentVariable(
                        "UNICLAW_INTEGRATION_MODEL")
                    ?? (string.Equals(
                            provider,
                            "mock",
                            StringComparison.OrdinalIgnoreCase)
                        ? "deterministic-ui"
                        : "sensenova-6.7-flash-lite");
        var outputRoot = Path.Combine(
            AdbTestContext.RepoRoot,
            "artifacts",
            "runs",
            "integration",
            scope,
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));

        var outcome = await new HostCompositionFactory().RunScenarioAsync(
            new HostCommandOptions(
                "run",
                serial,
                outputRoot,
                provider,
                model,
                "direct",
                scenarioPath));

        Assert.Equal("success", outcome.Status);
        Assert.True(outcome.Steps > 0, "场景没有经过 Core/FSM step loop");
        Assert.True(outcome.ActionsAttempted > 0, "场景没有尝试任何安全门控动作");

        var resultFiles = Directory.GetFiles(
            outputRoot,
            "result.json",
            SearchOption.AllDirectories);
        var resultPath = Assert.Single(resultFiles);
        using var result = JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
        Assert.Equal(
            "success",
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
    }
}
