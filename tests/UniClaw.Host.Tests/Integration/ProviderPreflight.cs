namespace UniClaw.Host.Tests.Integration;

/// <summary>
/// 各 provider 的运行时可用性预检 (P2.10 补全)。
/// IntegrationConfigLoader 只查配置文件结构；本类查运行时前提：
/// 凭据 env / secrets 文件存在性 + local 本地路径存在性。
/// 在 RunScenarioAsync 装配期调用 —— 缺什么当场 fail-fast，
/// 而不是跑完一遍 Host 才在 CreateProviders 里炸。
/// 每个 provider 自己检查自己的前提，保证选它时真的可用。
/// </summary>
public static class ProviderPreflight
{
    /// <summary>qwen/sensenova 的备用凭据文件 (~/.litellm/secrets.json, Host 侧 LoadXxxApiKey 支持)。</summary>
    private static readonly string LitellmSecretsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".litellm",
        "secrets.json");

    /// <summary>
    /// 检查 scenario 选中的 provider 是否可用。
    /// 失败抛 <see cref="InvalidOperationException"/>，信息带"缺什么 + 怎么设"。
    /// </summary>
    public static void Check(ScenarioConfig scenario, string repoRoot)
    {
        switch (scenario.Provider)
        {
            case "mock":
                return; // 确定性 provider，无外部依赖

            case "local":
                RequireEnv(
                    "DEEPSEEK_API_KEY",
                    "local 的文本能力 (decide_next_action/parse_instruction/安全审查) 走 deepseek text 路由");
                CheckVisionFiles(scenario, repoRoot);
                return;

            case "claude":
                RequireEnv("ANTHROPIC_API_KEY", "claude provider");
                return;

            case "sensenova":
                RequireEnvOrSecrets("SENSENOVA_API_KEY", "sensenova provider");
                return;

            case "qwen":
                RequireEnvOrSecrets("QWEN_API_KEY", "qwen provider");
                return;

            default:
                return; // 未知 provider 已被 loader 拦截
        }
    }

    private static void CheckVisionFiles(ScenarioConfig scenario, string repoRoot)
    {
        var vision = scenario.ProviderConfig?.VisionServer;
        if (vision is null)
            throw new InvalidOperationException(
                "local provider 缺少 visionServer 段 (integration.config.json providers.local.visionServer)。");

        RequireFile(vision.YoloModel, repoRoot, "yoloModel (UNICLAW_YOLO_MODEL)");
        RequireFile(vision.LabelMapping, repoRoot, "labelMapping (UNICLAW_LABEL_MAPPING)");
    }

    private static void RequireFile(string? relative, string repoRoot, string what)
    {
        if (string.IsNullOrWhiteSpace(relative))
            throw new InvalidOperationException(
                $"local provider 的 {what} 未配置。");
        var path = Path.IsPathRooted(relative) ? relative : Path.Combine(repoRoot, relative);
        if (!File.Exists(path))
            throw new InvalidOperationException(
                $"local provider 的 {what} 指向不存在的文件: '{path}'。"
                + "请确认模型/映射资产已下载到仓库内 (见 docs/testing/integration-config.md §1 推荐配置)。");
    }

    private static void RequireEnv(string name, string what)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
            throw new InvalidOperationException(
                $"{what} 需要环境变量 {name}。"
                + $"设置 {name}=<key> 后重跑。");
    }

    private static void RequireEnvOrSecrets(string name, string what)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
            return;
        if (File.Exists(LitellmSecretsPath))
            return; // Host 侧 LoadXxxApiKey 会从 secrets 文件读，文件存在即视为可用
        throw new InvalidOperationException(
            $"{what} 需要环境变量 {name} 或凭据文件 {LitellmSecretsPath}。"
            + $"二选一配置后重跑。");
    }
}
