namespace UniClaw.Host.Dsh;

/// <summary>
/// PNL-010：Runtime Host 工具执行的本地配置（产出路径）。落点
/// <c>product/tasks/tool-runtime.yaml</c>（可用 UNICLAW_TOOL_RUNTIME_CONFIG
/// 覆盖），与 uniagent-prod / settings-coverage profile 同款发现规则。
/// 边界（ADR-0039）：调用形态的绑定细节属于 Host adapter，不进共享
/// tool-registry；文件缺失 → 默认值（run-dir/report），存在但非法 →
/// fail-closed，不静默回退。
/// </summary>
public sealed record RuntimeToolOutputConfig(string Base, string Subdir)
{
    public const string BaseRunDir = "run-dir";
    public const string BaseRunsRoot = "runs-root";

    public static RuntimeToolOutputConfig Default { get; } = new(BaseRunDir, "report");

    public bool IsValid =>
        (Base == BaseRunDir || Base == BaseRunsRoot)
        && !string.IsNullOrWhiteSpace(Subdir)
        && !Subdir.Contains('/') && !Subdir.Contains('\\')
        && !Subdir.Contains("..", StringComparison.Ordinal)
        && Path.GetFileName(Subdir) == Subdir;
}

public static class RuntimeToolConfig
{
    public const string DefaultConfigRelativePath = "product/tasks/tool-runtime.yaml";
    public const string ConfigPathEnvironmentVariable = "UNICLAW_TOOL_RUNTIME_CONFIG";

    /// <summary>环境变量路径优先；默认文件不存在时返回默认配置。</summary>
    public static RuntimeToolOutputConfig LoadDefault()
    {
        var configured = Environment.GetEnvironmentVariable(ConfigPathEnvironmentVariable);
        if (configured is { Length: > 0 })
            return Load(configured);
        var path = ResolveDefaultPath();
        return path is null ? RuntimeToolOutputConfig.Default : Load(path);
    }

    /// <summary>从装配目录向上发现仓库默认配置文件；找不到返回 null（用默认值）。</summary>
    public static string? ResolveDefaultPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, DefaultConfigRelativePath);
            if (File.Exists(candidate)) return candidate;
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return null;
            directory = directory.Parent!;
        }
        return null;
    }

    public static RuntimeToolOutputConfig Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("configuration path is required", nameof(path));
        if (!File.Exists(path)) throw new FileNotFoundException("tool-runtime configuration file not found", path);

        string? outputBase = null;
        string? outputSubdir = null;
        var inOutput = false;
        foreach (var line in File.ReadAllLines(path))
        {
            var text = line.Split('#', 2)[0].TrimEnd();
            var content = text.Trim();
            if (content.Length == 0) continue;
            var indent = text.Length - content.Length;
            if (indent == 0)
            {
                inOutput = content == "output:";
                continue;
            }
            if (!inOutput || indent < 2) continue;
            var separator = content.IndexOf(':');
            if (separator <= 0) continue;
            var key = content[..separator].Trim();
            var value = Unquote(content[(separator + 1)..].Trim());
            if (key == "base") outputBase = value;
            if (key == "subdir") outputSubdir = value;
        }
        var config = new RuntimeToolOutputConfig(
            outputBase ?? RuntimeToolOutputConfig.BaseRunDir,
            outputSubdir ?? "report");
        if (!config.IsValid)
            throw new InvalidOperationException($"tool-runtime-config-invalid: base='{config.Base}' subdir='{config.Subdir}' (base ∈ run-dir|runs-root; subdir 为 runs 根下单段目录名)");
        return config;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            return value[1..^1];
        return value;
    }
}
