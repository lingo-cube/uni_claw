namespace UniClaw.Host.Capability;

/// <summary>
/// CAP-012 D2 — 全局术语白名单加载器（.dsh/profiles/language-inspection.yaml，
/// 沿"单一运行时配置源"纪律：从环境变量或仓库根向上发现；缺文件 → 返回空表
/// 并由调用方透传，能力侧照跑 + 披露影响，D8）。
/// </summary>
public static class LanguageInspectionAllowlist
{
    public const string DefaultConfigRelativePath = ".dsh/profiles/language-inspection.yaml";
    public const string ConfigPathEnvironmentVariable = "UNICLAW_LANGUAGE_INSPECTION_ALLOWLIST";

    /// <summary>加载词条（trim + 去空 + 大小写不敏感去重）；文件缺失返回空表。</summary>
    public static IReadOnlyList<string> LoadDefault()
    {
        var configured = Environment.GetEnvironmentVariable(ConfigPathEnvironmentVariable);
        var path = string.IsNullOrWhiteSpace(configured) ? ResolveDefaultPath() : configured;
        return File.Exists(path) ? Load(path) : Array.Empty<string>();
    }

    /// <summary>解析 allowlist 块（"- 词条" 列表；块缺失 → 空表；条目全空白 → fail-closed）。</summary>
    public static IReadOnlyList<string> Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("allowlist path is required", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException("allowlist file not found", path);

        var terms = new List<string>();
        var inBlock = false;
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.TrimEnd();
            if (line.Length == 0 || line.TrimStart().StartsWith('#'))
                continue;
            var content = line.TrimStart();
            if (!inBlock)
            {
                if (content == "allowlist:")
                    inBlock = true;
                else if (content.Contains(':', StringComparison.Ordinal))
                    throw new InvalidOperationException($"allowlist-syntax:unexpected-section:{rawLine} ({path})");
                continue;
            }
            if (!content.StartsWith("- ", StringComparison.Ordinal))
            {
                if (content.Contains(':', StringComparison.Ordinal))
                    throw new InvalidOperationException($"allowlist-syntax:unexpected-section-after-allowlist:{rawLine} ({path})");
                throw new InvalidOperationException($"allowlist-syntax:expected-list-item:{rawLine} ({path})");
            }
            var term = content["- ".Length..].Trim();
            if (term.Length == 0)
                throw new InvalidOperationException($"allowlist-syntax:empty-term:{rawLine} ({path})");
            terms.Add(term);
        }
        return terms
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveDefaultPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, DefaultConfigRelativePath);
            if (File.Exists(candidate))
                return candidate;
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return candidate; // 仓库根已到：返回期望路径（缺失由 LoadDefault 判空表）
            directory = directory.Parent!;
        }
        return Path.Combine(".", DefaultConfigRelativePath);
    }
}
