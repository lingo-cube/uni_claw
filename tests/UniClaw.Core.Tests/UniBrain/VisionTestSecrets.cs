using System.Text.Json;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>视觉集成测试共享的凭据/配置加载（不打印明文）。</summary>
public static class VisionTestSecrets
{
    /// <summary>从环境变量或 ~/.litellm/secrets.json 读取 SENSENOVA_API_KEY。</summary>
    public static string? LoadSensenovaApiKey()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(
            "SENSENOVA_API_KEY");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment;

        var home = Environment.GetEnvironmentVariable("HOME") ?? "";
        var secretsPath = Path.Combine(home, ".litellm", "secrets.json");
        if (!File.Exists(secretsPath)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(secretsPath));
            return doc.RootElement.TryGetProperty("SENSENOVA_API_KEY", out var v)
                ? v.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>组合 baseUrl / model / apiKey。缺少 key → InvalidOperationException。</summary>
    public static SensenovaConfig LoadSensenovaConfig()
    {
        var apiKey = Environment.GetEnvironmentVariable("SENSENOVA_API_KEY")
                     ?? LoadSensenovaApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "SENSENOVA_API_KEY not found in env or ~/.litellm/secrets.json");
        }

        return new SensenovaConfig(
            apiKey,
            Environment.GetEnvironmentVariable("SENSENOVA_BASE_URL")
            ?? "https://token.sensenova.cn",
            Environment.GetEnvironmentVariable("SENSENOVA_MODEL")
            ?? "sensenova-6.7-flash-lite");
    }

    public sealed record class SensenovaConfig(
        string ApiKey,
        string BaseUrl,
        string Model);
}
