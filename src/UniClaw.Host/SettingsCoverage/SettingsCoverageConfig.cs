namespace UniClaw.Host.SettingsCoverage;

public sealed record CoverageSessionConfig(string TaskTitle, string Workspace, bool WorkspaceReuse, bool AutoCloseTurn);

public sealed record CoverageBounds(int MaxSteps, int MaxConsultRounds, int MaxScrolls, int MaxConsecutiveFailures, int MaxDirectiveRetries);

public sealed record CoverageRequirements(bool RootPage, string FirstLevelMode, int ScrollDiscoveredEntries, int SecondLevelPages, bool BackNavigation, int RepeatedEntries);

public sealed record CoverageTermination(bool OnCoverageComplete, bool OnMaxSteps, bool OnMaxScrolls, bool OnConsecutiveFailures);

/// <summary>AGT-008：证据工件持久化开关（缺省 true——追溯性默认留存）。</summary>
public sealed record CoverageEvidenceOptions(bool PersistScreenshots = true, bool PersistHierarchies = true);

/// <summary>
/// AGT-009：弹窗识别与清障配置。HostPackage = 结构分类器的 host 包身份；
/// PopupResourceIds = 弹窗结构 resource-id 白名单；ObstacleTargets = 清障
/// 点击的 descriptor 优先序（CANCEL/button2 优先，DISMISS/button1 次之）；
/// MaxObstacleRetries = 有界清障重试（超出即 fail closed bounded stop）。
/// </summary>
public sealed record PopupClearanceConfig(
    string HostPackage = "com.android.settings",
    IReadOnlyList<string>? PopupResourceIds = null,
    IReadOnlyList<string>? ObstacleTargets = null,
    int MaxObstacleRetries = 2)
{
    /// <summary>弹窗结构 resource-id 缺省白名单（e1 真机实证）。</summary>
    public static readonly string[] DefaultPopupResourceIds =
    {
        "android:id/alertTitle", "android:id/parentPanel", "android:id/buttonPanel",
    };

    /// <summary>清障目标缺省优先序。</summary>
    public static readonly string[] DefaultObstacleTargets = { "CANCEL", "button2", "DISMISS", "button1" };

    public IReadOnlyList<string> ResourceIds => PopupResourceIds ?? DefaultPopupResourceIds;
    public IReadOnlyList<string> Targets => ObstacleTargets ?? DefaultObstacleTargets;
}

/// <summary>
/// AGT-009：有界 Slow 触发配置（ADR-0030：Effect Gate 永不调用/等待 Slow）。
/// Enabled 缺省 false——关闭时现有行为逐字节不变；BoundedWaitMs 有界等待；
/// MaxRequestsPerRun 每 run 预算；VisualEnabled 允许携带截图 raw artifact；
/// PopupConsecutiveCycles = PopupConsecutiveFailures 触发的连续周期数 N。
/// </summary>
public sealed record SlowTriggerConfig(
    bool Enabled = false,
    int BoundedWaitMs = 2000,
    int MaxRequestsPerRun = 4,
    bool VisualEnabled = false,
    int PopupConsecutiveCycles = 2);

public sealed record SettingsCoverageConfig(
    string ConfigVersion,
    CoverageSessionConfig Session,
    CoverageBounds Bounds,
    CoverageRequirements Coverage,
    IReadOnlyList<string> TargetPages,
    CoverageTermination Termination,
    string RootRoute,
    string ScrollContainerDescriptor,
    string BackDescriptor,
    CoverageEvidenceOptions? Evidence = null,
    PopupClearanceConfig? Popup = null,
    SlowTriggerConfig? Slow = null)
{
    /// <summary>证据持久化（缺省全开；旧配置无该段时保持缺省）。</summary>
    public CoverageEvidenceOptions EvidenceSettings => Evidence ?? new CoverageEvidenceOptions();

    /// <summary>AGT-009：弹窗/清障配置（缺省 = 记录的缺省词汇与有界重试）。</summary>
    public PopupClearanceConfig PopupSettings => Popup ?? new PopupClearanceConfig();

    /// <summary>AGT-009：Slow 触发配置（缺省关闭——现有行为不变）。</summary>
    public SlowTriggerConfig SlowSettings => Slow ?? new SlowTriggerConfig();

    public const string DefaultConfigRelativePath = ".dsh/profiles/settings-coverage.yaml";
    public const string ConfigPathEnvironmentVariable = "UNICLAW_SETTINGS_COVERAGE_CONFIG";

    public static SettingsCoverageConfig LoadDefault()
    {
        // 环境变量优先（AGT-006 修复：曾声明未读——负路径场景因此静默落回
        // 仓库默认配置，bounds 被默认值覆盖）。空值 = 走仓库默认发现。
        var configured = Environment.GetEnvironmentVariable(ConfigPathEnvironmentVariable);
        return Load(string.IsNullOrWhiteSpace(configured) ? ResolveDefaultPath() : configured);
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
                throw new FileNotFoundException(
                    $"settings-coverage config not found at {DefaultConfigRelativePath} (searched up from {AppContext.BaseDirectory}).");
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"settings-coverage config not found at {DefaultConfigRelativePath} (searched up from {AppContext.BaseDirectory}).");
    }

    public static SettingsCoverageConfig Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var document = YamlDocument.Parse(File.ReadAllLines(path));
        return new SettingsCoverageConfig(
            ConfigVersion: RequireVersion(document, path),
            Session: new CoverageSessionConfig(
                TaskTitle: RequireField(document, path, ["session", "taskTitle"]),
                Workspace: RequireField(document, path, ["session", "workspace"]),
                WorkspaceReuse: RequireBool(document, ["session", "workspaceReuse"], path),
                AutoCloseTurn: RequireBool(document, ["session", "autoCloseTurn"], path)),
            Bounds: ValidateBounds(document, path),
            Coverage: ValidateCoverage(document, path),
            TargetPages: ValidateTargetPages(document, path),
            Termination: ValidateTermination(document, path),
            RootRoute: RequireNamed(document, ["rootRoute"], path),
            Evidence: ReadEvidenceOptions(document, path),
            Popup: ReadPopupOptions(document, path),
            Slow: ReadSlowOptions(document, path),
            ScrollContainerDescriptor: RequireNamed(document, ["scrollContainerDescriptor"], path),
            BackDescriptor: RequireNamed(document, ["backDescriptor"], path));
    }

    private static string RequireVersion(YamlDocument document, string path)
    {
        var version = document.RequireScalar(["configVersion"]);
        if (version != "1")
            throw new InvalidOperationException($"config-version-unsupported:{version} [{path}]");
        return version;
    }

    private static CoverageBounds ValidateBounds(YamlDocument document, string path)
    {
        var maxSteps = RequireInt(document, ["bounds", "maxSteps"], path, minimum: 1);
        var maxConsultRounds = RequireInt(document, ["bounds", "maxConsultRounds"], path, minimum: 1);
        var maxScrolls = RequireInt(document, ["bounds", "maxScrolls"], path, minimum: 0);
        var maxConsecutiveFailures = RequireInt(document, ["bounds", "maxConsecutiveFailures"], path, minimum: 1);
        var maxDirectiveRetries = RequireInt(document, ["bounds", "maxDirectiveRetries"], path, minimum: 0);
        return new CoverageBounds(maxSteps, maxConsultRounds, maxScrolls, maxConsecutiveFailures, maxDirectiveRetries);
    }

    private static CoverageRequirements ValidateCoverage(YamlDocument document, string path)
    {
        var rootPage = RequireBool(document, ["coverage", "rootPage"], path);
        var firstLevelMode = RequireField(document, path, ["coverage", "firstLevelMode"]);
        if (firstLevelMode != "all-visible")
            throw new InvalidOperationException($"config-invalid:coverage.firstLevelMode:{firstLevelMode} [{path}]");
        var scrollDiscoveredEntries = RequireInt(document, ["coverage", "scrollDiscoveredEntries"], path, minimum: 1);
        var secondLevelPages = RequireInt(document, ["coverage", "secondLevelPages"], path, minimum: 1);
        var backNavigation = RequireBool(document, ["coverage", "backNavigation"], path);
        var repeatedEntries = RequireInt(document, ["coverage", "repeatedEntries"], path, minimum: 0);
        return new CoverageRequirements(rootPage, firstLevelMode, scrollDiscoveredEntries, secondLevelPages, backNavigation, repeatedEntries);
    }

    private static IReadOnlyList<string> ValidateTargetPages(YamlDocument document, string path)
    {
        IReadOnlyList<string> pages;
        try
        {
            pages = document.RequireList(["targetPages"]);
        }
        catch (InvalidOperationException error)
        {
            throw new InvalidOperationException($"{error.Message} [{path}]");
        }
        if (pages.Count is 0 || pages.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException($"config-invalid:targetPages [{path}]");
        if (pages.Distinct(StringComparer.Ordinal).Count() != pages.Count)
            throw new InvalidOperationException($"config-invalid:targetPages:duplicate [{path}]");
        return pages;
    }

    private static CoverageTermination ValidateTermination(YamlDocument document, string path)
    {
        var termination = new CoverageTermination(
            OnCoverageComplete: RequireBool(document, ["termination", "onCoverageComplete"], path),
            OnMaxSteps: RequireBool(document, ["termination", "onMaxSteps"], path),
            OnMaxScrolls: RequireBool(document, ["termination", "onMaxScrolls"], path),
            OnConsecutiveFailures: RequireBool(document, ["termination", "onConsecutiveFailures"], path));
        if (termination is { OnCoverageComplete: false, OnMaxSteps: false, OnMaxScrolls: false, OnConsecutiveFailures: false })
            throw new InvalidOperationException($"config-invalid:termination.none-enabled [{path}]");
        return termination;
    }

    private static string RequireField(YamlDocument document, string path, string[] pathSegments)
    {
        try
        {
            return document.RequireScalar(pathSegments);
        }
        catch (InvalidOperationException error)
        {
            throw new InvalidOperationException($"{error.Message} [{path}]");
        }
    }

    /// <summary>AGT-008：可选 evidence 段（缺省全 true；显式非法值 fail closed）。</summary>
    private static CoverageEvidenceOptions? ReadEvidenceOptions(YamlDocument document, string path)
    {
        var screenshots = document.OptionalBool(["evidence", "persistScreenshots"]);
        var hierarchies = document.OptionalBool(["evidence", "persistHierarchies"]);
        if (screenshots is null && hierarchies is null)
            return null; // 段缺失 → 调用方缺省
        return new CoverageEvidenceOptions(
            PersistScreenshots: screenshots ?? true,
            PersistHierarchies: hierarchies ?? true);
    }

    /// <summary>AGT-009：可选 popup 段（缺省 = 记录的缺省词汇；显式值非法 fail closed）。</summary>
    private static PopupClearanceConfig? ReadPopupOptions(YamlDocument document, string path)
    {
        var hostPackage = document.OptionalScalar(["popup", "hostPackage"]);
        var resourceIds = document.OptionalList(["popup", "popupResourceIds"]);
        var obstacleTargets = document.OptionalList(["popup", "obstacleTargets"]);
        var maxRetries = document.OptionalInt(["popup", "maxObstacleRetries"], minimum: 0);
        if (hostPackage is null && resourceIds is null && obstacleTargets is null && maxRetries is null)
            return null; // 段缺失 → 调用方缺省
        return new PopupClearanceConfig(
            HostPackage: hostPackage ?? "com.android.settings",
            PopupResourceIds: resourceIds,
            ObstacleTargets: obstacleTargets,
            MaxObstacleRetries: maxRetries ?? 2);
    }

    /// <summary>AGT-009：可选 slow 段（缺省 Disabled=false——现有行为不变）。</summary>
    private static SlowTriggerConfig? ReadSlowOptions(YamlDocument document, string path)
    {
        var enabled = document.OptionalBool(["slow", "enabled"]);
        var boundedWaitMs = document.OptionalInt(["slow", "boundedWaitMs"], minimum: 1);
        var maxRequests = document.OptionalInt(["slow", "maxRequestsPerRun"], minimum: 0);
        var visualEnabled = document.OptionalBool(["slow", "visualEnabled"]);
        var popupCycles = document.OptionalInt(["slow", "popupConsecutiveCycles"], minimum: 1);
        if (enabled is null && boundedWaitMs is null && maxRequests is null
            && visualEnabled is null && popupCycles is null)
            return null; // 段缺失 → 调用方缺省（关闭）
        return new SlowTriggerConfig(
            Enabled: enabled ?? false,
            BoundedWaitMs: boundedWaitMs ?? 2000,
            MaxRequestsPerRun: maxRequests ?? 4,
            VisualEnabled: visualEnabled ?? false,
            PopupConsecutiveCycles: popupCycles ?? 2);
    }

    private static string RequireNamed(YamlDocument document, string[] pathSegments, string path)
    {
        var value = RequireField(document, path, pathSegments);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"config-missing:{string.Join(".", pathSegments)} [{path}]");
        return value;
    }

    private static int RequireInt(YamlDocument document, string[] pathSegments, string path, int minimum)
    {
        var raw = RequireField(document, path, pathSegments);
        if (!int.TryParse(raw, out var value) || value < minimum)
            throw new InvalidOperationException($"config-invalid:{string.Join(".", pathSegments)}:{raw} [{path}]");
        return value;
    }

    private static bool RequireBool(YamlDocument document, string[] pathSegments, string path)
    {
        var raw = RequireField(document, path, pathSegments);
        if (!bool.TryParse(raw, out var value))
            throw new InvalidOperationException($"config-invalid:{string.Join(".", pathSegments)}:{raw} [{path}]");
        return value;
    }

    private sealed class YamlDocument
    {
        private readonly Dictionary<string, object> _root = new(StringComparer.Ordinal);

        public static YamlDocument Parse(string[] lines)
        {
            var document = new YamlDocument();
            var stack = new List<(int Indent, Dictionary<string, object> Map)> { (0, document._root) };
            for (var index = 0; index < lines.Length; index++)
            {
                var rawLine = lines[index];
                var line = rawLine.TrimEnd();
                if (line.Length is 0 || line.TrimStart().StartsWith('#'))
                    continue;
                var indent = line.Length - line.TrimStart().Length;
                var content = line.TrimStart();
                while (stack.Count > 1 && indent < stack[^1].Indent)
                    stack.RemoveAt(stack.Count - 1);
                if (indent != stack[^1].Indent)
                    throw new InvalidOperationException($"config-syntax:unexpected-indent:{rawLine}");

                var separator = content.IndexOf(':', StringComparison.Ordinal);
                if (separator <= 0)
                    throw new InvalidOperationException($"config-syntax:expected-mapping:{rawLine}");
                var key = content[..separator].Trim();
                var value = content[(separator + 1)..].Trim();
                var map = stack[^1].Map;

                if (value.Length is not 0)
                {
                    map[key] = Unquote(value);
                    continue;
                }

                // Empty value: nested mapping or string list, decided by the
                // next significant line's indentation.
                var childIndent = -1;
                var childContent = "";
                for (var look = index + 1; look < lines.Length; look++)
                {
                    var candidate = lines[look].TrimEnd();
                    if (candidate.Length is 0 || candidate.TrimStart().StartsWith('#'))
                        continue;
                    childIndent = candidate.Length - candidate.TrimStart().Length;
                    childContent = candidate.TrimStart();
                    break;
                }
                if (childContent.StartsWith("- ", StringComparison.Ordinal))
                {
                    var items = new List<string>();
                    var listIndex = index + 1;
                    for (; listIndex < lines.Length; listIndex++)
                    {
                        var itemLine = lines[listIndex].TrimEnd();
                        if (itemLine.Length is 0 || itemLine.TrimStart().StartsWith('#'))
                            continue;
                        var itemIndent = itemLine.Length - itemLine.TrimStart().Length;
                        var item = itemLine.TrimStart();
                        if (itemIndent != childIndent || !item.StartsWith("- ", StringComparison.Ordinal))
                            break;
                        items.Add(Unquote(item["- ".Length..].Trim()));
                    }
                    map[key] = items.ToArray();
                    index = listIndex - 1;
                }
                else if (childIndent > indent)
                {
                    var nested = new Dictionary<string, object>(StringComparer.Ordinal);
                    map[key] = nested;
                    stack.Add((childIndent, nested));
                }
                else
                {
                    // Empty block (key with neither value nor children).
                    map[key] = "";
                }
            }
            return document;
        }

        /// <summary>AGT-008：可选布尔读取——缺路径/缺值 → null；值非严格
        /// 布尔 → fail closed（与必填布尔同一严格度）。</summary>
        public bool? OptionalBool(string[] path)
        {
            if (Walk(path) is not string value)
                return null;
            if (string.IsNullOrWhiteSpace(value))
                return null;
            return bool.TryParse(value, out var parsed)
                ? parsed
                : throw new InvalidOperationException(
                    $"config-invalid:{string.Join(".", path)}:{value} ({path.Last()} 须为布尔)");
        }

        /// <summary>AGT-009：可选标量读取——缺路径/缺值 → null。</summary>
        public string? OptionalScalar(string[] path) =>
            Walk(path) is string value && !string.IsNullOrWhiteSpace(value) ? value : null;

        /// <summary>AGT-009：可选整数读取——缺路径 → null；值存在但非整数/
        /// 低于下限 → fail closed（与必填整数同一严格度）。</summary>
        public int? OptionalInt(string[] path, int minimum)
        {
            if (Walk(path) is not string value || string.IsNullOrWhiteSpace(value))
                return null;
            var display = string.Join(".", path);
            if (!int.TryParse(value, out var parsed) || parsed < minimum)
                throw new InvalidOperationException(
                    $"config-invalid:{display}:{value} ({path.Last()} 须为 ≥{minimum} 的整数)");
            return parsed;
        }

        /// <summary>AGT-009：可选字符串列表读取——缺路径 → null。</summary>
        public IReadOnlyList<string>? OptionalList(string[] path) =>
            Walk(path) is string[] list ? list : null;

        public string RequireScalar(string[] path)
        {
            if (Walk(path) is string value && !string.IsNullOrWhiteSpace(value))
                return value;
            throw new InvalidOperationException($"config-missing:{string.Join(".", path)}");
        }

        public IReadOnlyList<string> RequireList(string[] path)
        {
            if (Walk(path) is string[] list)
                return list;
            throw new InvalidOperationException($"config-invalid:{string.Join(".", path)}");
        }

        private object? Walk(string[] path)
        {
            object? current = _root;
            foreach (var segment in path)
            {
                if (current is Dictionary<string, object> map
                    && map.TryGetValue(segment, out var next))
                {
                    current = next;
                }
                else
                {
                    return null;
                }
            }
            return current;
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2
                && ((value.StartsWith('"') && value.EndsWith('"'))
                    || (value.StartsWith('\'') && value.EndsWith('\''))))
                return value[1..^1];
            return value;
        }
    }
}
