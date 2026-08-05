using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Core.Observability;

namespace UniClaw.Host.Tests.Integration;

/// <summary>
/// 集成测试配置单点真源 (integration.config.json) — provider/model/outputRoot/
/// 视觉服务参数集中管理，替代散落在测试代码里的硬编码与环境变量 (P2.x)。
/// 加载即校验 (fail-fast)：schema 版本、provider 引用存在性、visionServer 只允许
/// 挂在 local 下。优先级: 配置文件 &lt; 环境变量覆盖 &lt; 显式参数。
/// 对齐 label-mapping.json 模式 (schema 版本 + 绝对路径解析 + 构造期校验)。
/// </summary>
public static class IntegrationConfigLoader
{
    public const string SchemaVersion = "uniclaw.integrationConfig.v1";
    public const string DefaultFileName = "integration.config.json";
    public const string EnvPathOverride = "UNICLAW_INTEGRATION_CONFIG";
    public const string EnvProviderOverride = "UNICLAW_INTEGRATION_PROVIDER";
    public const string EnvModelOverride = "UNICLAW_INTEGRATION_MODEL";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>已知 provider 集合 — 与 Host 侧 CreateUniBrainConfig 支持集合对齐。</summary>
    private static readonly string[] KnownProviders =
        ["local", "sensenova", "claude", "qwen", "mock"];

    /// <summary>已批准的 host 执行模式 (HostCommandOptions.Mode)。</summary>
    private static readonly string[] KnownModes = ["direct", "legacy", "interactive"];

    /// <summary>允许挂 visionServer 的 provider (本地视觉服务专属)。</summary>
    private const string VisionServerOnlyProvider = "local";

    /// <summary>
    /// 消费 model 字段的 provider (P2.10)：cloud 三件套的 model 是构造参数 (Host 侧
    /// sensenova/claude 缺 model 直接抛异常)；local 的 text 模型走 DEEPSEEK_MODEL、
    /// local-vision 无模型参数；mock 不消费 —— 它们不强制 model。
    /// </summary>
    private static bool RequiresModel(string providerId) =>
        providerId is "sensenova" or "claude" or "qwen";

    /// <summary>
    /// 加载配置。路径解析顺序: 显式 path → UNICLAW_INTEGRATION_CONFIG →
    /// 测试输出目录 Integration/integration.config.json (csproj Content 拷贝)。
    /// 校验失败抛 InvalidOperationException (fail-fast)。
    /// </summary>
    public static IntegrationConfig Load(string? path = null)
    {
        var resolved = ResolvePath(path);
        if (!File.Exists(resolved))
            throw new InvalidOperationException(
                $"integration.config.json 不存在: '{resolved}' (设置 {EnvPathOverride} 覆盖)。");

        IntegrationConfig config;
        try
        {
            config = JsonSerializer.Deserialize<IntegrationConfig>(
                File.ReadAllText(resolved),
                JsonOptions)
                ?? throw new InvalidOperationException(
                    $"integration.config.json at '{resolved}' deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"integration.config.json at '{resolved}' 不是合法 JSON: {ex.Message}");
        }

        Validate(config, resolved);
        return config;
    }

    /// <summary>
    /// 解析某 scenario 的生效配置：文件值为默认，环境变量覆盖 (UNICLAW_INTEGRATION_PROVIDER /
    /// UNICLAW_INTEGRATION_MODEL)。env 覆盖是 CI 每 run 选择器，不改文件。
    /// </summary>
    public static ScenarioConfig ResolveScenario(
        IntegrationConfig config,
        string scenarioId,
        string? providerOverride = null,
        string? modelOverride = null)
    {
        var scenario = config.Scenarios.FirstOrDefault(
            s => string.Equals(s.Id, scenarioId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"配置中不存在 scenario '{scenarioId}'。");

        var providerId = providerOverride
                         ?? Environment.GetEnvironmentVariable(EnvProviderOverride)
                         ?? scenario.Provider;
        if (!config.Providers.TryGetValue(providerId, out var provider))
            throw new InvalidOperationException(
                $"scenario '{scenarioId}' 引用的 provider '{providerId}' 不存在于配置 providers 段。");

        var model = modelOverride
                    ?? Environment.GetEnvironmentVariable(EnvModelOverride)
                    ?? provider.Model;
        // 按实际生效配置校验: env/参数覆盖可能让云端 provider 的 model 变成空 —
        // 覆盖后的配置也要满足必填规则, 不能只检查文件原样。
        if (RequiresModel(providerId) && string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException(
                $"scenario '{scenarioId}' 实际生效配置不完整: provider '{providerId}' 的 model 为空 "
                + $"(配置文件未填 model 且 {EnvModelOverride} 未提供非空值)。");
        return scenario with { Provider = providerId, Model = model, ProviderConfig = provider };
    }

    /// <summary>
    /// 按 scenario 文件查找 (测试入口用文件名定位)。找不到时回退到 scope 匹配。
    /// </summary>
    public static ScenarioConfig ResolveScenarioByFile(
        IntegrationConfig config,
        string scenarioFile,
        string scope)
    {
        var byFile = config.Scenarios.FirstOrDefault(s =>
            string.Equals(s.File, scenarioFile, StringComparison.OrdinalIgnoreCase));
        var scenario = byFile ?? config.Scenarios.FirstOrDefault(s =>
            string.Equals(s.Scope, scope, StringComparison.OrdinalIgnoreCase));
        if (scenario is null)
            throw new InvalidOperationException(
                $"配置中找不到 file='{scenarioFile}' 或 scope='{scope}' 的 scenario。");
        return ResolveScenario(config, scenario.Id);
    }

    private static string ResolvePath(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            return Path.GetFullPath(path);
        var fromEnv = Environment.GetEnvironmentVariable(EnvPathOverride);
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return Path.GetFullPath(fromEnv);
        return Path.Combine(
            AppContext.BaseDirectory,
            "Integration",
            DefaultFileName);
    }

    private static void Validate(IntegrationConfig config, string sourcePath)
    {
        if (!string.Equals(config.Schema, SchemaVersion, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"integration.config.json schema 版本不匹配: 期望 '{SchemaVersion}', "
                + $"实际 '{config.Schema}' (来源: {sourcePath})。");

        if (config.Emulator is null)
            throw new InvalidOperationException("integration.config.json 缺少 emulator 段。");
        if (string.IsNullOrWhiteSpace(config.Emulator.OutputRoot))
            throw new InvalidOperationException("emulator.outputRoot 不能为空。");
        if (config.Emulator.KeepRuns < 0)
            throw new InvalidOperationException("emulator.keepRuns 不能为负数。");

        // D-7: storage 段 — backend.type 只支持 file (location 复用 emulator.outputRoot)。
        if (config.Storage?.Backend is not null
            && !string.Equals(
                config.Storage.Backend.Type,
                "file",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"storage.backend.type 仅支持 'file'，实际 '{config.Storage.Backend.Type}'。");
        }

        // D-12: logging 段 (可选) — level 值必须合法 (ParseLevelStrict fail-fast),
        // 测试装配期注入 UNICLAW_LOG_LEVEL (已设优先, 同 visionServer env 注入)。
        if (config.Logging is not null)
        {
            try
            {
                LogLevelConfig.ParseLevelStrict(config.Logging.Level);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException(
                    $"integration.config.json logging.level 非法: {ex.Message}", ex);
            }
        }

        if (config.Providers is null || config.Providers.Count == 0)
            throw new InvalidOperationException("integration.config.json 缺少 providers 段。");
        foreach (var (id, provider) in config.Providers)
        {
            if (!KnownProviders.Contains(id, StringComparer.Ordinal))
                throw new InvalidOperationException($"未知 provider '{id}' (已知: {string.Join("/", KnownProviders)})。");
            if (provider is null)
                throw new InvalidOperationException($"provider '{id}' 为空段。");
            // P2.10: model 只对云端消费方必填；local 的 text 模型走 DEEPSEEK_MODEL、
            // mock 不消费模型名 —— 它们填了也行、缺了不算错。
            if (RequiresModel(id) && string.IsNullOrWhiteSpace(provider.Model))
                throw new InvalidOperationException(
                    $"provider '{id}' 缺少 model (云端 provider 的模型是构造参数, Host 侧必填)。");
            if (provider.VisionServer is not null
                && !string.Equals(id, VisionServerOnlyProvider, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"visionServer 只允许挂在 '{VisionServerOnlyProvider}' provider 下 (发现挂在 '{id}')。");
            // P2.7: intentModel 只对 sensenova 有意义 (意图推理走 sensenova 端点)
            if (!string.IsNullOrWhiteSpace(provider.IntentModel)
                && !string.Equals(id, "sensenova", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"intentModel 只允许挂在 sensenova provider 下 (发现挂在 '{id}')。");
            if (provider.VisionServer?.OcrBackend is not null
                && provider.VisionServer.OcrBackend is not ("rapidocr" or "paddleocr"))
                throw new InvalidOperationException(
                    $"provider '{id}' visionServer.ocrBackend 仅支持 rapidocr/paddleocr，"
                    + $"实际 '{provider.VisionServer.OcrBackend}'。");
        }

        if (config.Scenarios is null || config.Scenarios.Count == 0)
            throw new InvalidOperationException("integration.config.json 缺少 scenarios 段。");
        foreach (var scenario in config.Scenarios)
        {
            if (string.IsNullOrWhiteSpace(scenario.Id)
                || string.IsNullOrWhiteSpace(scenario.File)
                || string.IsNullOrWhiteSpace(scenario.Scope))
                throw new InvalidOperationException(
                    "scenario 的 id/file/scope 均必填。");
            if (!config.Providers.ContainsKey(scenario.Provider))
                throw new InvalidOperationException(
                    $"scenario '{scenario.Id}' 引用不存在的 provider '{scenario.Provider}'。");
            if (!KnownModes.Contains(scenario.Mode, StringComparer.Ordinal))
                throw new InvalidOperationException(
                    $"scenario '{scenario.Id}' mode '{scenario.Mode}' 非法 (已知: {string.Join("/", KnownModes)})。");
            if (scenario.TimeoutSeconds <= 0)
                throw new InvalidOperationException(
                    $"scenario '{scenario.Id}' timeoutSeconds 必须为正数。");
        }
    }
}

/// <summary>integration.config.json 根对象。</summary>
public sealed class IntegrationConfig
{
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = "";

    [JsonPropertyName("emulator")]
    public EmulatorConfig Emulator { get; set; } = new();

    /// <summary>D-12: logging 段 (可选) — 测试 run 的最小日志级别, 注入 UNICLAW_LOG_LEVEL。</summary>
    [JsonPropertyName("logging")]
    public LoggingConfig? Logging { get; set; }

    /// <summary>D-7: 写侧资产存储配置 (backend 键；location 复用 emulator.outputRoot)。</summary>
    [JsonPropertyName("storage")]
    public StorageConfig? Storage { get; set; }

    [JsonPropertyName("providers")]
    public Dictionary<string, ProviderConfig> Providers { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("scenarios")]
    public List<ScenarioConfig> Scenarios { get; set; } = [];
}

/// <summary>
/// logging 段 — 测试 run 的最小日志级别 (LogLevelConfig 值域)。
/// 装配期注入 UNICLAW_LOG_LEVEL (手设/CI 已设优先)；非法值 loader fail-fast。
/// </summary>
public sealed class LoggingConfig
{
    [JsonPropertyName("level")]
    public string Level { get; set; } = "information";
}

/// <summary>storage 段 — 写侧资产管道的后端选择。</summary>
public sealed class StorageConfig
{
    [JsonPropertyName("backend")]
    public StorageBackendConfig Backend { get; set; } = new();
}

/// <summary>storage.backend — type 只支持 file；location 复用 emulator.outputRoot。</summary>
public sealed class StorageBackendConfig
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "file";

    [JsonPropertyName("location")]
    public string? Location { get; set; }
}

/// <summary>emulator 段 — 设备 + 产物布局 + 清理/基线策略。</summary>
public sealed class EmulatorConfig
{
    /// <summary>adb serial。"auto" = 单在线设备自动解析 (AdbTestContext.ResolveSerialAsync)。</summary>
    [JsonPropertyName("serial")]
    public string Serial { get; set; } = "auto";

    /// <summary>相对 repo root 的输出根 (scope/scenarioId 目录由测试按此拼接)。</summary>
    [JsonPropertyName("outputRoot")]
    public string OutputRoot { get; set; } = "artifacts/runs/integration";

    /// <summary>run 目录命名格式 (UTC)。</summary>
    [JsonPropertyName("runNaming")]
    public string RunNaming { get; set; } = "yyyyMMddTHHmmssZ";

    /// <summary>清理策略: 每个 scenario 保留最近 N 个成功 run + 全部失败。</summary>
    [JsonPropertyName("keepRuns")]
    public int KeepRuns { get; set; } = 5;

    /// <summary>基线录制模式: true 时测试不对比基线, 写入 recorded-baselines。</summary>
    [JsonPropertyName("recordBaseline")]
    public bool RecordBaseline { get; set; }
}

/// <summary>provider 段 — 按 id 分块, 每块自己的 model + 实现细节。</summary>
public sealed class ProviderConfig
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    /// <summary>
    /// 仅 sensenova (P2.7): 意图推理模型 (CreateIntentExtractor)。
    /// 测试装配期注入 SENSENOVA_MODEL env (已设优先) — config 是真源, env 仍是覆盖通道。
    /// 缺省时 Host 回落 SENSENOVA_MODEL ?? "deepseek-v4-flash"。
    /// </summary>
    [JsonPropertyName("intentModel")]
    public string? IntentModel { get; set; }

    /// <summary>仅 local provider 允许: 本地视觉服务参数。</summary>
    [JsonPropertyName("visionServer")]
    public VisionServerConfig? VisionServer { get; set; }

    /// <summary>D-7: evidence 存储门控 — false (默认) 时 LocalVisionProvider 不注入
    /// 管道 → evidence 提交完全 no-op。</summary>
    [JsonPropertyName("evidenceStorage")]
    public bool EvidenceStorage { get; set; }
}

/// <summary>local provider 的视觉服务参数 — 测试运行时注入环境变量。</summary>
public sealed class VisionServerConfig
{
    [JsonPropertyName("socket")]
    public string? Socket { get; set; }

    [JsonPropertyName("port")]
    public int? Port { get; set; }

    [JsonPropertyName("ompThreads")]
    public int OmpThreads { get; set; } = 4;

    [JsonPropertyName("ocrBackend")]
    public string OcrBackend { get; set; } = "rapidocr";

    [JsonPropertyName("ocrTextScore")]
    public double OcrTextScore { get; set; } = 0.5;

    [JsonPropertyName("yoloModel")]
    public string? YoloModel { get; set; }

    [JsonPropertyName("labelMapping")]
    public string? LabelMapping { get; set; }
}

/// <summary>scenario 段 — 引用 provider id, 换 provider 改一行。</summary>
public sealed record class ScenarioConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("file")]
    public string File { get; set; } = "";

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "";

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "";

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "direct";

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 180;

    /// <summary>解析后的生效值 (ResolveScenario 填充) — 非配置文件字段。</summary>
    [JsonIgnore]
    public string Model { get; set; } = "";

    /// <summary>解析后的 provider 配置 (ResolveScenario 填充) — 非配置文件字段。</summary>
    [JsonIgnore]
    public ProviderConfig? ProviderConfig { get; set; }
}
