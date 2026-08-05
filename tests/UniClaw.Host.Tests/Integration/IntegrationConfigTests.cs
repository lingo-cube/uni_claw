using System.Text.Json;
using Microsoft.Extensions.Logging;
using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Host.Tests.Integration;

/// <summary>
/// integration.config.json 加载/校验/解析测试 (P2.x 配置规范)。
/// 使用临时目录写非法配置验证 fail-fast；默认配置从测试输出目录加载。
/// </summary>
public sealed class IntegrationConfigTests
{
    private static readonly string DefaultConfigPath = Path.Combine(
        AppContext.BaseDirectory,
        "Integration",
        IntegrationConfigLoader.DefaultFileName);

    [Fact]
    public void DefaultConfig_LoadsAndValidates()
    {
        Assert.True(
            File.Exists(DefaultConfigPath),
            $"默认配置文件不存在: {DefaultConfigPath}");

        var config = IntegrationConfigLoader.Load(DefaultConfigPath);

        Assert.Equal(IntegrationConfigLoader.SchemaVersion, config.Schema);
        Assert.True(config.Emulator.KeepRuns >= 0);
        Assert.NotEmpty(config.Emulator.OutputRoot);
        Assert.NotEmpty(config.Providers);
        Assert.NotEmpty(config.Scenarios);
    }

    [Fact]
    public void DefaultConfig_ProvidersCoverLocalAndMockWithVisionServerOnlyOnLocal()
    {
        var config = IntegrationConfigLoader.Load(DefaultConfigPath);

        Assert.Contains("local", config.Providers.Keys);
        Assert.Contains("mock", config.Providers.Keys);
        Assert.NotNull(config.Providers["local"].VisionServer);
        Assert.Null(config.Providers["mock"].VisionServer);
        Assert.Equal("rapidocr", config.Providers["local"].VisionServer!.OcrBackend);
        Assert.Equal(4, config.Providers["local"].VisionServer!.OmpThreads);
        // P2.10: local/mock 不消费 model —— 死值已删, 不强制
        Assert.Empty(config.Providers["local"].Model);
        Assert.Empty(config.Providers["mock"].Model);
        // P2.7: sensenova 意图推理模型入 config 管辖
        Assert.Equal("deepseek-v4-flash", config.Providers["sensenova"].IntentModel);
    }

    [Fact]
    public void DefaultConfig_ScenariosReferenceExistingProviders()
    {
        var config = IntegrationConfigLoader.Load(DefaultConfigPath);

        foreach (var scenario in config.Scenarios)
        {
            Assert.Contains(scenario.Provider, config.Providers.Keys);
            Assert.False(string.IsNullOrWhiteSpace(scenario.File));
            Assert.False(string.IsNullOrWhiteSpace(scenario.Scope));
        }

        var locate = Assert.Single(config.Scenarios, s => s.Id == "locate-one-item");
        Assert.Equal("scenario-locate", locate.Scope);
        Assert.Equal("local", locate.Provider);
    }

    [Fact]
    public void ResolveScenario_FileFallbackToScope_AndEnvOverrideWins()
    {
        var config = IntegrationConfigLoader.Load(DefaultConfigPath);

        var resolved = IntegrationConfigLoader.ResolveScenarioByFile(
            config, "locate-one-item.v1.json", "scenario-locate");
        Assert.Equal("local", resolved.Provider);
        // P2.10: local 不消费 model —— 解析结果为空串 (Host local 分支忽略 model)
        Assert.Empty(resolved.Model);
        Assert.NotNull(resolved.ProviderConfig?.VisionServer);

        // env 覆盖 (CI 选择器) — provider 换 sensenova, model 跟随其 provider 段
        Environment.SetEnvironmentVariable(
            IntegrationConfigLoader.EnvProviderOverride, "sensenova");
        try
        {
            var overridden = IntegrationConfigLoader.ResolveScenario(
                config, "locate-one-item");
            Assert.Equal("sensenova", overridden.Provider);
            Assert.Equal("sensenova-6.7-flash-lite", overridden.Model);
            Assert.Null(overridden.ProviderConfig?.VisionServer);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                IntegrationConfigLoader.EnvProviderOverride, null);
        }
    }

    [Fact]
    public void DefaultConfig_LoggingSection_ParsesToWarning()
    {
        // 5.4: logging.level 为合法 LogLevelConfig 值, 装配期注入 UNICLAW_LOG_LEVEL
        var config = IntegrationConfigLoader.Load(DefaultConfigPath);

        Assert.NotNull(config.Logging);
        Assert.Equal("warning", config.Logging!.Level);
        Assert.Equal(
            LogLevel.Warning,
            LogLevelConfig.ParseLevelStrict(config.Logging.Level));
    }

    [Fact]
    public void Load_InvalidLoggingLevel_ThrowsFailFast()
    {
        using var dir = TempConfigDir(
            """{"schema":"uniclaw.integrationConfig.v1","emulator":{"outputRoot":"x"},"logging":{"level":"loud"},"providers":{"local":{}},"scenarios":[]}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => IntegrationConfigLoader.Load(Path.Combine(dir.Path, IntegrationConfigLoader.DefaultFileName)));
        Assert.Contains("logging.level", ex.Message);
        Assert.Contains("loud", ex.Message);
    }

    [Fact]
    public void Load_LoggingSection_ValidLevel_Loads()
    {
        using var dir = TempConfigDir(
            """{"schema":"uniclaw.integrationConfig.v1","emulator":{"outputRoot":"x"},"logging":{"level":"trace"},"providers":{"local":{}},"scenarios":[{"id":"s","file":"s.json","scope":"sc","provider":"local","mode":"direct"}]}""");

        var config = IntegrationConfigLoader.Load(
            Path.Combine(dir.Path, IntegrationConfigLoader.DefaultFileName));
        Assert.Equal("trace", config.Logging?.Level);
    }

    [Fact]
    public void Load_UnknownProviderReference_ThrowsFailFast()
    {
        using var dir = TempConfigDir(
            """{"schema":"uniclaw.integrationConfig.v1","emulator":{"outputRoot":"x"},"providers":{"local":{"model":"m"}},"scenarios":[{"id":"s","file":"s.json","scope":"sc","provider":"ghost","mode":"direct"}]}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => IntegrationConfigLoader.Load(Path.Combine(dir.Path, IntegrationConfigLoader.DefaultFileName)));
        Assert.Contains("ghost", ex.Message);
    }

    [Fact]
    public void Load_VisionServerOnNonLocalProvider_ThrowsFailFast()
    {
        using var dir = TempConfigDir(
            """{"schema":"uniclaw.integrationConfig.v1","emulator":{"outputRoot":"x"},"providers":{"sensenova":{"model":"m","visionServer":{"ocrBackend":"rapidocr"}}},"scenarios":[]}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => IntegrationConfigLoader.Load(Path.Combine(dir.Path, IntegrationConfigLoader.DefaultFileName)));
        Assert.Contains("visionServer", ex.Message);
    }

    [Fact]
    public void Load_SchemaMismatch_ThrowsFailFast()
    {
        using var dir = TempConfigDir(
            """{"schema":"uniclaw.integrationConfig.v9","emulator":{"outputRoot":"x"},"providers":{},"scenarios":[]}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => IntegrationConfigLoader.Load(Path.Combine(dir.Path, IntegrationConfigLoader.DefaultFileName)));
        Assert.Contains("schema", ex.Message);
    }

    [Fact]
    public void Load_InvalidOcrBackend_ThrowsFailFast()
    {
        using var dir = TempConfigDir(
            """{"schema":"uniclaw.integrationConfig.v1","emulator":{"outputRoot":"x"},"providers":{"local":{"model":"m","visionServer":{"ocrBackend":"tesseract"}}},"scenarios":[]}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => IntegrationConfigLoader.Load(Path.Combine(dir.Path, IntegrationConfigLoader.DefaultFileName)));
        Assert.Contains("ocrBackend", ex.Message);
    }

    [Fact]
    public void Load_CloudProviderWithoutModel_ThrowsFailFast()
    {
        // P2.10: 云端 provider 的 model 是构造参数 (Host 必填) — 缺了就 fail-fast
        using var dir = TempConfigDir(
            """{"schema":"uniclaw.integrationConfig.v1","emulator":{"outputRoot":"x"},"providers":{"sensenova":{}},"scenarios":[]}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => IntegrationConfigLoader.Load(Path.Combine(dir.Path, IntegrationConfigLoader.DefaultFileName)));
        Assert.Contains("sensenova", ex.Message);
    }

    [Fact]
    public void Load_LocalAndMockWithoutModel_Loads()
    {
        // P2.10: local/mock 不消费 model —— 死值删除后配置仍然合法
        using var dir = TempConfigDir(
            """{"schema":"uniclaw.integrationConfig.v1","emulator":{"outputRoot":"x"},"providers":{"local":{"visionServer":{"ocrBackend":"rapidocr"}},"mock":{}},"scenarios":[{"id":"s","file":"s.json","scope":"sc","provider":"mock","mode":"direct"}]}""");

        var config = IntegrationConfigLoader.Load(
            Path.Combine(dir.Path, IntegrationConfigLoader.DefaultFileName));
        Assert.Empty(config.Providers["local"].Model);
        Assert.Empty(config.Providers["mock"].Model);
    }

    [Fact]
    public void Load_IntentModelOnNonSensenova_ThrowsFailFast()
    {
        // P2.7: intentModel 只对 sensenova 有意义 (意图推理走 sensenova 端点)
        using var dir = TempConfigDir(
            """{"schema":"uniclaw.integrationConfig.v1","emulator":{"outputRoot":"x"},"providers":{"qwen":{"model":"m","intentModel":"deepseek-v4-flash"}},"scenarios":[]}""");

        var ex = Assert.Throws<InvalidOperationException>(
            () => IntegrationConfigLoader.Load(Path.Combine(dir.Path, IntegrationConfigLoader.DefaultFileName)));
        Assert.Contains("intentModel", ex.Message);
    }

    [Fact]
    public void ResolveScenario_CloudProviderWithEmptyModel_FailsFast()
    {
        // 按实际生效配置校验: 覆盖成云端 provider + model 空 → fail-fast (文件原样检查发现不了)
        var config = IntegrationConfigLoader.Load(DefaultConfigPath);
        Environment.SetEnvironmentVariable(
            IntegrationConfigLoader.EnvModelOverride, "");
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => IntegrationConfigLoader.ResolveScenario(
                    config, "locate-one-item", providerOverride: "sensenova"));
            Assert.Contains("model", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                IntegrationConfigLoader.EnvModelOverride, null);
        }
    }

    [Fact]
    public void ResolveScenario_NonCloudProviderWithEmptyModel_Loads()
    {
        // 实际配置校验不影响不消费 model 的 provider (mock/local)
        var config = IntegrationConfigLoader.Load(DefaultConfigPath);
        Environment.SetEnvironmentVariable(
            IntegrationConfigLoader.EnvModelOverride, "");
        try
        {
            var resolved = IntegrationConfigLoader.ResolveScenario(
                config, "enumerate-settings-safely");
            Assert.Equal("mock", resolved.Provider);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                IntegrationConfigLoader.EnvModelOverride, null);
        }
    }

    [Fact]
    public void Load_MissingFile_ThrowsFailFast()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => IntegrationConfigLoader.Load("/nonexistent/integration.config.json"));
        Assert.Contains("不存在", ex.Message);
    }

    private static TempDir TempConfigDir(string json)
    {
        var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, IntegrationConfigLoader.DefaultFileName), json);
        return dir;
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "uniclaw-int-config-" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // 清理失败不影响测试结果
            }
        }
    }
}
