using System.Text.Json;
using UniClaw.Host.Capability;

namespace UniClaw.Host.SettingsCoverage;

public sealed record CoverageSessionConfig(string TaskTitle, string Workspace, bool WorkspaceReuse, bool AutoCloseTurn);

/// <summary>AGT-019 M5（Q6）：停滞阈值用于 no-progress 事实注入。旧模式
/// 保留滚动额度和旧终止兼容字段；goal-directed模式仅尊重明确的授权总上限。</summary>
public sealed record CoverageBounds(
    int MaxSteps,
    int MaxConsultRounds,
    int MaxScrolls,
    int MaxConsecutiveFailures,
    int MaxDirectiveRetries,
    int StagnationInjectConsults = 3,
    int StagnationStopConsults = 5,
    int? MaxTotalSteps = null,
    int? MaxTotalConsultRounds = null,
    int RenewalThresholdSteps = 0,
    int RenewalThresholdRounds = 0,
    bool GoalDirectedContinuationEnabled = false,
    int? AuthorizedTotalSteps = null,
    int? AuthorizedTotalConsultRounds = null);

public sealed record CoverageRequirements(
    bool RootPage,
    string FirstLevelMode,
    int ScrollDiscoveredEntries,
    int SecondLevelPages,
    bool BackNavigation,
    int RepeatedEntries,
    IReadOnlyList<string>? ScrollCandidateEntries = null)
{
    public IReadOnlyList<string> ExplicitScrollCandidates => ScrollCandidateEntries ?? Array.Empty<string>();
}

public sealed record CoverageTermination(bool OnCoverageComplete, bool OnMaxSteps, bool OnMaxScrolls, bool OnConsecutiveFailures);

/// <summary>可选的二级页深度遍历。只有显式列入 NestedEntries 的条目才可成为目标；
/// 缺段或 Enabled=false 时保持旧的一层遍历行为。</summary>
public sealed record DeepTraversalConfig(
    bool Enabled = false,
    int MaxDepth = 1,
    int MaxEntriesPerPage = 0,
    IReadOnlyList<string>? NestedEntries = null)
{
    public IReadOnlyList<string> Entries => NestedEntries ?? Array.Empty<string>();
}

/// <summary>
/// 根页动态入口策略。旧配置只遍历 targetPages；开启 all-visible 后，Host
/// 会把每次根页观察中满足安全结构条件的 ui.element 纳入 frontier。该策略
/// 只改变 Host 的候选发现，不绕过 Kernel 的 grounding/effect gate，也不把
/// 视觉推断升级成可执行目标。
/// </summary>
public sealed record DynamicRootEntriesConfig(
    bool Enabled = false,
    string Mode = "configured-only",
    IReadOnlyList<string>? ExcludedDescriptors = null)
{
    public bool AllVisible => Enabled && Mode == "all-visible";
    public IReadOnlyList<string> Exclusions => ExcludedDescriptors ?? Array.Empty<string>();
}

/// <summary>非根页动态子入口策略。只消费真实 observation 中可安全导航的
/// ui.element；switch、返回、滚动容器和 framework control 继续留在 skip ledger。
/// MaxDepth计入一级页之后的nested-enter层数：1允许一级页进入其子页。</summary>
public sealed record DynamicNestedEntriesConfig(
    bool Enabled = false,
    int MaxDepth = 1,
    int MaxEntriesPerPage = 2)
{
    public bool IsActive => Enabled && MaxDepth > 0 && MaxEntriesPerPage > 0;
}

/// <summary>AGT-008：证据工件持久化开关（缺省 true——追溯性默认留存）。</summary>
public sealed record CoverageEvidenceOptions(bool PersistScreenshots = true, bool PersistHierarchies = true);

/// <summary>
/// AGT-009：弹窗识别与清障配置。HostPackage = 结构分类器的 host 包身份；
/// PopupResourceIds = 弹窗结构 resource-id 白名单；ObstacleTargets = 清障
/// 点击的 descriptor 优先序（安全侧：cancel/dismiss/negative）；
/// MaxObstacleRetries = 有界清障重试（超出即 fail closed bounded stop）。
/// AGT-019 M4（Q5）：DenyObstacleTargets = 禁手清单（确认侧词汇，命中即
/// 不可中选，无论是否在候选词表）——遍历型任务没有 confirm 权限，候选
/// 词表必须先过禁手清单；无安全词可用时用返回键 dismiss；返回键也未
/// 配置 → bounded stop 暂停+重授权（不变量 47），绝不点 confirm 侧。
/// </summary>
public sealed record PopupClearanceConfig(
    string HostPackage = "com.android.settings",
    IReadOnlyList<string>? PopupResourceIds = null,
    IReadOnlyList<string>? ObstacleTargets = null,
    int MaxObstacleRetries = 2,
    IReadOnlyList<string>? DenyObstacleTargets = null)
{
    /// <summary>弹窗结构 resource-id 缺省白名单（e1 真机实证）。</summary>
    public static readonly string[] DefaultPopupResourceIds =
    {
        "android:id/alertTitle", "android:id/parentPanel", "android:id/buttonPanel",
        // Android Settings dialogs commonly namespace framework panel IDs with
        // com.android.settings even when the dialog is owned by Settings.
        "com.android.settings:id/alertTitle",
        "com.android.settings:id/parentPanel",
        "com.android.settings:id/buttonPanel",
    };

    /// <summary>清障目标缺省优先序（AGT-019 M4：button1 移除——确认侧
    /// 目标永不作为清障候选）。</summary>
    public static readonly string[] DefaultObstacleTargets = { "CANCEL", "button2", "DISMISS" };

    /// <summary>禁手清单缺省值（确认侧词汇；与 policy 工件
    /// android-settings-popup-disposition 的 denyObstacleTargets 同源）。</summary>
    public static readonly string[] DefaultDenyObstacleTargets =
        { "button1", "OK", "CONFIRM", "ALLOW", "ACCEPT", "AGREE", "CONTINUE", "NEXT", "DONE" };

    public IReadOnlyList<string> ResourceIds => PopupResourceIds ?? DefaultPopupResourceIds;
    public IReadOnlyList<string> Targets => ObstacleTargets ?? DefaultObstacleTargets;

    /// <summary>有效禁手清单（缺省 = 确认侧词汇集）。</summary>
    public IReadOnlyList<string> DenyTargets => DenyObstacleTargets ?? DefaultDenyObstacleTargets;
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
    string? BackDescriptor,
    CoverageEvidenceOptions? Evidence = null,
    PopupClearanceConfig? Popup = null,
    SlowTriggerConfig? Slow = null,
    SettingsActionPolicy? ActionPolicy = null,
    bool ActionPolicyRequired = false,
    LanguageInspectionTaskRequest? LanguageInspection = null,
    DeepTraversalConfig? DeepTraversal = null,
    DynamicRootEntriesConfig? DynamicRootEntries = null,
    DynamicNestedEntriesConfig? DynamicNestedEntries = null)
{
    /// <summary>证据持久化（缺省全开；旧配置无该段时保持缺省）。</summary>
    public CoverageEvidenceOptions EvidenceSettings => Evidence ?? new CoverageEvidenceOptions();

    /// <summary>AGT-009：弹窗/清障配置（缺省 = 记录的缺省词汇与有界重试）。</summary>
    public PopupClearanceConfig PopupSettings => Popup ?? new PopupClearanceConfig();

    /// <summary>AGT-009：Slow 触发配置（缺省关闭——现有行为不变）。</summary>
    public SlowTriggerConfig SlowSettings => Slow ?? new SlowTriggerConfig();

    /// <summary>CAP-013：可选语言检查任务要求；缺段 = 本任务不请求该能力。</summary>
    public LanguageInspectionTaskRequest? LanguageInspectionRequest => LanguageInspection;

    public DeepTraversalConfig DeepTraversalSettings => DeepTraversal ?? new DeepTraversalConfig();

    public DynamicRootEntriesConfig DynamicRootEntriesSettings =>
        DynamicRootEntries ?? new DynamicRootEntriesConfig();

    public DynamicNestedEntriesConfig DynamicNestedEntriesSettings =>
        DynamicNestedEntries ?? new DynamicNestedEntriesConfig();

    public const string DefaultConfigRelativePath = "product/tasks/settings-coverage.yaml";
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
            ActionPolicy: ReadActionPolicy(document, path, out var actionPolicyRequired),
            ActionPolicyRequired: actionPolicyRequired,
            LanguageInspection: ReadLanguageInspectionOptions(document, path),
            DeepTraversal: ReadDeepTraversalOptions(document, path),
            DynamicRootEntries: ReadDynamicRootEntriesOptions(document, path),
            DynamicNestedEntries: ReadDynamicNestedEntriesOptions(document, path),
            ScrollContainerDescriptor: RequireNamed(document, ["scrollContainerDescriptor"], path),
            BackDescriptor: RequireNamed(document, ["backDescriptor"], path));
    }

    private static SettingsActionPolicy? ReadActionPolicy(
        YamlDocument document,
        string configPath,
        out bool required)
    {
        required = document.OptionalBool(["actionPolicy", "required"]) ?? false;
        var relativePath = document.OptionalScalar(["actionPolicy", "path"]);
        if (relativePath is null)
        {
            if (required)
                throw new InvalidOperationException(
                    "PROFILE_CONTRACT_NOT_READY: actionPolicy.path is required");
            return null;
        }

        var policyPath = Path.IsPathRooted(relativePath)
            ? relativePath
            : ResolvePolicyPath(configPath, relativePath);
        try
        {
            return SettingsActionPolicy.Load(policyPath);
        }
        catch (InvalidOperationException error) when (error.Message.StartsWith("PROFILE_CONTRACT_NOT_READY", StringComparison.Ordinal))
        {
            if (required)
                throw;
            return null;
        }
    }

    private static string ResolvePolicyPath(string configPath, string relativePath)
    {
        // 锚定到链上最外层（仓库根）AGENTS.md：配置落 product/tasks/ 后，
        // 中间的 product/AGENTS.md 不得截断（否则解析成 product/product/...）。
        var configDirectory = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(configPath))!);
        DirectoryInfo? root = null;
        for (var directory = configDirectory; directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                root = directory;
        }
        if (root is not null)
            return Path.Combine(root.FullName, relativePath);
        return Path.GetFullPath(Path.Combine(configDirectory.FullName, relativePath));
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
        // AGT-019 M5：可选（缺省 3/5——保守初值，标定后冻结）。
        var stagnationInject = document.OptionalInt(["bounds", "stagnationInjectConsults"], minimum: 1) ?? 3;
        var stagnationStop = document.OptionalInt(["bounds", "stagnationStopConsults"], minimum: 1) ?? 5;
        var totalSteps = document.OptionalInt(["bounds", "maxTotalSteps"], minimum: 1);
        var totalRounds = document.OptionalInt(["bounds", "maxTotalConsultRounds"], minimum: 1);
        var thresholdSteps = document.OptionalInt(["bounds", "renewalThresholdSteps"], minimum: 1);
        var thresholdRounds = document.OptionalInt(["bounds", "renewalThresholdRounds"], minimum: 1);
        var goalDirectedContinuation = document.OptionalBool(["bounds", "goalDirectedContinuationEnabled"]) ?? false;
        var authorizedTotalSteps = document.OptionalInt(["bounds", "authorizedTotalSteps"], minimum: 1);
        var authorizedTotalRounds = document.OptionalInt(["bounds", "authorizedTotalConsultRounds"], minimum: 1);
        if (goalDirectedContinuation)
        {
            if (maxConsultRounds < 2)
                throw new InvalidOperationException($"config-invalid:bounds.maxConsultRounds:must-reserve-review-and-correction [{path}]");
            if (thresholdRounds is < 2)
                throw new InvalidOperationException($"config-invalid:bounds.renewalThresholdRounds:must-reserve-review-and-correction [{path}]");
            ValidateGoalDirectedThreshold(thresholdSteps, maxSteps, "renewalThresholdSteps", path);
            ValidateGoalDirectedThreshold(thresholdRounds, maxConsultRounds, "renewalThresholdRounds", path);
        }
        else
        {
            ValidateRenewalDimension(totalSteps, maxSteps, thresholdSteps, "maxTotalSteps", path);
            ValidateRenewalDimension(totalRounds, maxConsultRounds, thresholdRounds, "maxTotalConsultRounds", path);
        }
        ValidateAuthorizedTotal(authorizedTotalSteps, maxSteps, "authorizedTotalSteps", path);
        ValidateAuthorizedTotal(authorizedTotalRounds, maxConsultRounds, "authorizedTotalConsultRounds", path);
        return new CoverageBounds(maxSteps, maxConsultRounds, maxScrolls, maxConsecutiveFailures,
            maxDirectiveRetries, stagnationInject, stagnationStop,
            totalSteps, totalRounds, thresholdSteps ?? 0, thresholdRounds ?? 0,
            goalDirectedContinuation, authorizedTotalSteps, authorizedTotalRounds);
    }

    private static void ValidateAuthorizedTotal(int? total, int initial, string name, string path)
    {
        if (total is { } limit && limit < initial)
            throw new InvalidOperationException($"config-invalid:bounds.{name}:below-initial-allocation [{path}]");
    }

    private static void ValidateGoalDirectedThreshold(int? threshold, int initial, string name, string path)
    {
        // Goal-directed mode treats this as a review signal (including the
        // default ~20% threshold), not as a renewal hard-cap declaration.
        if (threshold is { } value && value >= initial)
            throw new InvalidOperationException($"config-invalid:bounds.{name}:must-be-below-initial-allocation [{path}]");
    }

    private static void ValidateRenewalDimension(int? total, int initial, int? threshold, string name, string path)
    {
        if ((total is null) != (threshold is null)
            || total is { } limit && (limit <= initial || threshold > initial))
            throw new InvalidOperationException($"config-invalid:bounds.{name}:renewal-requires-total-above-initial-and-threshold-within-initial [{path}]");
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
        var candidates = document.OptionalList(["coverage", "scrollCandidateEntries"]);
        if (candidates is { } values && (values.Any(string.IsNullOrWhiteSpace)
            || values.Distinct(StringComparer.Ordinal).Count() != values.Count))
            throw new InvalidOperationException($"config-invalid:coverage.scrollCandidateEntries [{path}]");
        return new CoverageRequirements(rootPage, firstLevelMode, scrollDiscoveredEntries, secondLevelPages, backNavigation, repeatedEntries, candidates);
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

    /// <summary>AGT-009：可选 popup 段（缺省 = 记录的缺省词汇；显式值非法 fail closed）。
    /// AGT-019 M4：新增 denyObstacleTargets（禁手清单）与 dispositionPolicy
    /// （product/policy 弹窗处置工件路径；工件的 denyObstacleTargets 与显式
    /// deny 并集——宁多勿漏；工件缺失/形状不符 fail closed）。</summary>
    private static PopupClearanceConfig? ReadPopupOptions(YamlDocument document, string path)
    {
        var hostPackage = document.OptionalScalar(["popup", "hostPackage"]);
        var resourceIds = document.OptionalList(["popup", "popupResourceIds"]);
        var obstacleTargets = document.OptionalList(["popup", "obstacleTargets"]);
        var denyTargets = document.OptionalList(["popup", "denyObstacleTargets"]);
        var maxRetries = document.OptionalInt(["popup", "maxObstacleRetries"], minimum: 0);
        var dispositionPolicy = document.OptionalScalar(["popup", "dispositionPolicy"]);
        if (hostPackage is null && resourceIds is null && obstacleTargets is null
            && denyTargets is null && maxRetries is null && dispositionPolicy is null)
            return null; // 段缺失 → 调用方缺省
        if (dispositionPolicy is not null)
        {
            var policyPath = Path.IsPathRooted(dispositionPolicy)
                ? dispositionPolicy
                : ResolvePolicyPath(path, dispositionPolicy);
            var artifactDeny = LoadPopupDispositionDenyTargets(policyPath);
            denyTargets = artifactDeny
                .Concat(denyTargets ?? Array.Empty<string>())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
        return new PopupClearanceConfig(
            HostPackage: hostPackage ?? "com.android.settings",
            PopupResourceIds: resourceIds,
            ObstacleTargets: obstacleTargets,
            MaxObstacleRetries: maxRetries ?? 2,
            DenyObstacleTargets: denyTargets);
    }

    /// <summary>AGT-019 M4：装载弹窗处置 policy 工件的禁手清单（fail closed：
    /// 文件缺失/JSON 非法/schema 不符/denyObstacleTargets 缺失或非字符串数组
    /// → PROFILE_CONTRACT_NOT_READY）。</summary>
    private static IReadOnlyList<string> LoadPopupDispositionDenyTargets(string policyPath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(policyPath));
            var root = document.RootElement;
            if (!root.TryGetProperty("schemaVersion", out var version)
                || version.ValueKind != JsonValueKind.String
                || !version.GetString()!.StartsWith(
                    "android-settings-popup-disposition.", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"PROFILE_CONTRACT_NOT_READY: popup disposition policy schema mismatch: {policyPath}");
            if (!root.TryGetProperty("denyObstacleTargets", out var deny)
                || deny.ValueKind != JsonValueKind.Array
                || deny.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
                throw new InvalidOperationException(
                    $"PROFILE_CONTRACT_NOT_READY: popup disposition policy denyObstacleTargets missing/invalid: {policyPath}");
            return deny.EnumerateArray().Select(item => item.GetString()!).ToArray();
        }
        catch (Exception error) when (error is JsonException or IOException)
        {
            throw new InvalidOperationException(
                $"PROFILE_CONTRACT_NOT_READY: popup disposition policy unreadable: {policyPath}", error);
        }
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

    /// <summary>
    /// CAP-013：语言检查由任务提出要求，不提供 Host enabled 开关。段缺失时
    /// 不创建 binding；段存在却缺 expectedLanguage 时 fail closed，避免 agent
    /// 或设备 locale 被迫猜测判定源。
    /// </summary>
    private static LanguageInspectionTaskRequest? ReadLanguageInspectionOptions(
        YamlDocument document,
        string path)
    {
        var required = document.OptionalBool(["languageInspection", "required"]);
        var expectedLanguage = document.OptionalScalar(["languageInspection", "expectedLanguage"]);
        var capabilityId = document.OptionalScalar(["languageInspection", "capabilityId"]);
        var ignoreRoutes = document.OptionalList(["languageInspection", "ignoreRoutes"]);
        if (required is null && expectedLanguage is null && capabilityId is null && ignoreRoutes is null)
            return null;
        if (expectedLanguage is null)
            throw new InvalidOperationException(
                $"config-missing:languageInspection.expectedLanguage [{path}]");
        if (ignoreRoutes is { } routes && routes.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException(
                $"config-invalid:languageInspection.ignoreRoutes [{path}]");
        return new LanguageInspectionTaskRequest(
            Required: required ?? false,
            ExpectedLanguage: expectedLanguage,
            IgnoreRoutes: ignoreRoutes,
            FixedCapabilityId: capabilityId);
    }

    private static DeepTraversalConfig? ReadDeepTraversalOptions(YamlDocument document, string path)
    {
        var enabled = document.OptionalBool(["deepTraversal", "enabled"]);
        var maxDepth = document.OptionalInt(["deepTraversal", "maxDepth"], minimum: 1);
        var maxEntries = document.OptionalInt(["deepTraversal", "maxEntriesPerPage"], minimum: 0);
        var entries = document.OptionalList(["deepTraversal", "nestedEntries"]);
        if (enabled is null && maxDepth is null && maxEntries is null && entries is null)
            return null;
        if (entries is { } values && values.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException($"config-invalid:deepTraversal.nestedEntries [{path}]");
        if (entries is { } syntaxValues && syntaxValues.Any(value => !HasNestedEntrySyntax(value)))
            throw new InvalidOperationException($"config-invalid:deepTraversal.nestedEntries:expected-parent>child [{path}]");
        if (entries is { } duplicateValues && duplicateValues.Distinct(StringComparer.Ordinal).Count() != duplicateValues.Count)
            throw new InvalidOperationException($"config-invalid:deepTraversal.nestedEntries:duplicate [{path}]");
        if (enabled == true && (entries is null || entries.Count == 0))
            throw new InvalidOperationException($"config-missing:deepTraversal.nestedEntries [{path}]");
        if (maxDepth is > 1)
            throw new InvalidOperationException($"config-invalid:deepTraversal.maxDepth:{maxDepth} (当前仅支持一层显式 nested entry) [{path}]");
        return new DeepTraversalConfig(
            Enabled: enabled ?? false,
            MaxDepth: maxDepth ?? 1,
            MaxEntriesPerPage: maxEntries ?? 0,
            NestedEntries: entries);
    }

    private static DynamicRootEntriesConfig? ReadDynamicRootEntriesOptions(YamlDocument document, string path)
    {
        var scalar = document.OptionalScalar(["dynamicRootEntries"]);
        var enabled = document.OptionalBool(["dynamicRootEntries", "enabled"]);
        if (enabled is null && scalar is not null && bool.TryParse(scalar, out var scalarEnabled))
            enabled = scalarEnabled;
        var mode = document.OptionalScalar(["dynamicRootEntries", "mode"])
            ?? (scalar is not null && !bool.TryParse(scalar, out _) ? scalar : null);
        var exclusions = document.OptionalList(["dynamicRootEntries", "excludedDescriptors"]);
        if (enabled is null && mode is null && exclusions is null)
            return null;
        mode ??= enabled == true ? "all-visible" : "configured-only";
        if (mode is not ("configured-only" or "all-visible"))
            throw new InvalidOperationException(
                $"config-invalid:dynamicRootEntries.mode:{mode} [{path}]");
        if (exclusions is { } values &&
            (values.Any(string.IsNullOrWhiteSpace)
             || values.Distinct(StringComparer.Ordinal).Count() != values.Count))
            throw new InvalidOperationException(
                $"config-invalid:dynamicRootEntries.excludedDescriptors [{path}]");
        return new DynamicRootEntriesConfig(
            Enabled: enabled ?? mode == "all-visible",
            Mode: mode,
            ExcludedDescriptors: exclusions);
    }

    private static DynamicNestedEntriesConfig? ReadDynamicNestedEntriesOptions(YamlDocument document, string path)
    {
        var enabled = document.OptionalBool(["dynamicNestedEntries", "enabled"]);
        var maxDepth = document.OptionalInt(["dynamicNestedEntries", "maxDepth"], minimum: 1);
        var maxEntries = document.OptionalInt(["dynamicNestedEntries", "maxEntriesPerPage"], minimum: 1);
        if (enabled is null && maxDepth is null && maxEntries is null)
            return null;
        if (maxDepth is > 1)
            throw new InvalidOperationException($"config-invalid:dynamicNestedEntries.maxDepth:{maxDepth} (当前仅支持一层动态子入口) [{path}]");
        return new DynamicNestedEntriesConfig(
            Enabled: enabled ?? true,
            MaxDepth: maxDepth ?? 1,
            MaxEntriesPerPage: maxEntries ?? 2);
    }

    private static bool HasNestedEntrySyntax(string value)
    {
        var separator = value.IndexOf('>');
        if (separator < 0)
            separator = value.IndexOf("::", StringComparison.Ordinal);
        var width = value.Contains("::", StringComparison.Ordinal) ? 2 : 1;
        return separator > 0 && separator + width < value.Length
            && !string.IsNullOrWhiteSpace(value[..separator])
            && !string.IsNullOrWhiteSpace(value[(separator + width)..]);
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
