using Xunit;

namespace UniClaw.Host.Tests.Integration;

/// <summary>
/// ProviderPreflight 测试 (P2.10) — 各 provider 的运行时前提预检。
/// 只测确定性分支 (mock/local/claude)；sensenova/qwen 依赖用户 home 的
/// secrets 文件存在性, 测试环境不可控, 不测。
/// </summary>
public sealed class ProviderPreflightTests
{
    private static readonly string TempRoot =
        Path.Combine(Path.GetTempPath(), "uniclaw-preflight-" + Guid.NewGuid().ToString("N"));

    public ProviderPreflightTests()
    {
        Directory.CreateDirectory(TempRoot);
    }

    [Fact]
    public void Mock_AlwaysPasses()
    {
        // mock 无外部依赖 — 任何配置都不炸
        var scenario = Scenario("mock", providerConfig: null);
        ProviderPreflight.Check(scenario, TempRoot);
    }

    [Fact]
    public void Local_MissingDeepSeekKey_FailsFast()
    {
        var key = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        Environment.SetEnvironmentVariable("DEEPSEEK_API_KEY", null);
        try
        {
            var scenario = Scenario("local", LocalVisionConfig(
                yoloModel: Path.Combine(TempRoot, "best.pt"),
                labelMapping: Path.Combine(TempRoot, "label-mapping.json")));
            File.WriteAllText(Path.Combine(TempRoot, "best.pt"), "x");
            File.WriteAllText(Path.Combine(TempRoot, "label-mapping.json"), "{}");

            var ex = Assert.Throws<InvalidOperationException>(
                () => ProviderPreflight.Check(scenario, TempRoot));
            Assert.Contains("DEEPSEEK_API_KEY", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEEPSEEK_API_KEY", key);
        }
    }

    [Fact]
    public void Local_MissingVisionFiles_FailsFast()
    {
        var key = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        Environment.SetEnvironmentVariable("DEEPSEEK_API_KEY", "test-key");
        try
        {
            // yoloModel 指向不存在的文件
            var scenario = Scenario("local", LocalVisionConfig(
                yoloModel: "models/missing-best.pt",
                labelMapping: "mappings/missing.json"));

            var ex = Assert.Throws<InvalidOperationException>(
                () => ProviderPreflight.Check(scenario, TempRoot));
            Assert.Contains("yoloModel", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEEPSEEK_API_KEY", key);
        }
    }

    [Fact]
    public void Local_Ready_DoesNotThrow()
    {
        var key = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        Environment.SetEnvironmentVariable("DEEPSEEK_API_KEY", "test-key");
        try
        {
            var scenario = Scenario("local", LocalVisionConfig(
                yoloModel: Path.Combine(TempRoot, "best.pt"),
                labelMapping: Path.Combine(TempRoot, "label-mapping.json")));
            File.WriteAllText(Path.Combine(TempRoot, "best.pt"), "x");
            File.WriteAllText(Path.Combine(TempRoot, "label-mapping.json"), "{}");

            ProviderPreflight.Check(scenario, TempRoot); // 不炸即通过
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEEPSEEK_API_KEY", key);
        }
    }

    [Fact]
    public void Local_NoVisionServer_FailsFast()
    {
        var key = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        Environment.SetEnvironmentVariable("DEEPSEEK_API_KEY", "test-key");
        try
        {
            var scenario = Scenario("local", providerConfig: null);

            var ex = Assert.Throws<InvalidOperationException>(
                () => ProviderPreflight.Check(scenario, TempRoot));
            Assert.Contains("visionServer", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEEPSEEK_API_KEY", key);
        }
    }

    [Fact]
    public void Claude_MissingApiKey_FailsFast()
    {
        var key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        try
        {
            // claude 是云端 provider: 模型必填 (实际配置校验), 预检只管凭据
            var scenario = Scenario(
                "claude",
                new ProviderConfig { Model = "claude-sonnet-5" });

            var ex = Assert.Throws<InvalidOperationException>(
                () => ProviderPreflight.Check(scenario, TempRoot));
            Assert.Contains("ANTHROPIC_API_KEY", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", key);
        }
    }

    private static ScenarioConfig Scenario(string provider, ProviderConfig? providerConfig)
    {
        var config = new IntegrationConfig
        {
            Schema = IntegrationConfigLoader.SchemaVersion,
            Emulator = new EmulatorConfig(),
            Providers = new Dictionary<string, ProviderConfig>(StringComparer.Ordinal)
            {
                [provider] = providerConfig ?? new ProviderConfig(),
            },
            Scenarios = new List<ScenarioConfig>
            {
                new()
                {
                    Id = "s",
                    File = "s.json",
                    Scope = "sc",
                    Provider = provider,
                    Mode = "direct",
                },
            },
        };
        return IntegrationConfigLoader.ResolveScenario(config, "s");
    }

    private static ProviderConfig LocalVisionConfig(string yoloModel, string labelMapping) =>
        new()
        {
            VisionServer = new VisionServerConfig
            {
                Socket = "/tmp/uniclaw-vision.sock",
                YoloModel = yoloModel,
                LabelMapping = labelMapping,
            },
        };
}
