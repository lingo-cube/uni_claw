using Xunit;

namespace UniClaw.Host.Tests.Integration;

/// <summary>
/// 显式、按 scope 启用的集成测试 Fact。默认基线始终跳过；只有
/// <c>UNICLAW_INTEGRATION_SCOPES</c> 显式包含当前 scope（逗号分隔，或 all）时运行。
/// Core.Tests 项目有同名的平行实现（测试项目间不互相引用）。
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute(string scope)
    {
        if (!IntegrationTestScopes.IsEnabled(scope))
        {
            Skip = $"显式集成测试 scope '{scope}' 默认跳过。设置 "
                + $"UNICLAW_INTEGRATION_SCOPES={scope} 后按需运行。";
        }
    }
}

public static class IntegrationTestScopes
{
    public const string AdbConnectivity = "adb-connectivity";
    public const string AdbReadOnly = "adb-read";
    public const string AdbAction = "adb-action";
    public const string AdbVisionAction = "adb-vision-action";
    public const string ScenarioLocate = "scenario-locate";
    public const string ScenarioEnumerate = "scenario-enumerate";

    public static bool IsEnabled(string scope)
    {
        var configured = Environment.GetEnvironmentVariable(
            "UNICLAW_INTEGRATION_SCOPES");
        if (string.IsNullOrWhiteSpace(configured))
            return false;

        return configured.Split(
                [',', ';'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(value => string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(value, scope, StringComparison.OrdinalIgnoreCase));
    }
}
