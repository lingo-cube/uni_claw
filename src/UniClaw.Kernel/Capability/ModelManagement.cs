using System.Collections.Concurrent;

namespace UniClaw.Kernel.Capability;

/// <summary>
/// CAP-008 — Model Management 能力的协议与身份常量（对齐 <see cref="PerceptionProtocol"/>
/// 先例：Kernel 拥有 Definition 词汇，组合根引用它组装声明）。
/// </summary>
public static class ModelManagementProtocol
{
    /// <summary>能力 id（管理面唯一键）。</summary>
    public const string CapabilityId = "uni.model.management";

    /// <summary>协议名：logical profile → 冻结 binding 的解析契约。</summary>
    public const string BindingResolution = "Model Binding Resolution";

    /// <summary>协议版本。</summary>
    public const string Version = "1.0";
}

/// <summary>
/// CAP-008 — 健康探测报告：拉式探测的结果（复用既有 <see cref="HealthStatus"/>，
/// 语义对齐 .NET 健康检查词汇，不引入外部包）。调用方（组合根/监控）拿到报告后
/// 以 owner 身份决定是否提交事实——Hub 仍然只记账不推断（ADR-0035）。
/// </summary>
public sealed record CapabilityHealthReport(HealthStatus Status, string? Diagnostic = null);

/// <summary>
/// CAP-008 — 可健康探测的能力面（可选 mixin，不进 <see cref="ICapability"/> 根）。
/// 外部可观测可用性的能力必须实现（组件开发纪律 R7）；确定性纯计算能力可豁免
/// （恒 Healthy 的实现只是仪式）。
/// </summary>
public interface ICapabilityHealthCheckable
{
    /// <summary>拉式健康探测：只读，不改管理面状态。</summary>
    CapabilityHealthReport CheckHealth();
}

/// <summary>
/// CAP-006/008 — Model Management 的 L1 协议能力接口（继承位势见组件开发纪律）：
/// 产品拥有解析语义（fail-closed、候选偏好、健康证据），realization 只经
/// Register/RegisterPreference/ApplyHealth 注入数据，不实现本接口。
/// </summary>
public interface IModelManagement : ICapability, ICapabilityHealthCheckable
{
    /// <summary>当前已注册的全部候选（按 profile 分组、偏好序、首选在前；拷贝视图）。</summary>
    IReadOnlyCollection<ModelBindingSnapshot> Bindings { get; }

    /// <summary>注册/覆盖一个 binding 快照为该 profile 的唯一候选；无效快照 fail-closed。</summary>
    void Register(ModelBindingSnapshot binding);

    /// <summary>注册该 profile 的有序候选（首选在前）；非法注册 fail-closed。</summary>
    void RegisterPreference(LogicalProfileId profile, IReadOnlyList<ModelBindingSnapshot> orderedCandidates);

    /// <summary>依偏好序解析 profile；跳过更被偏好的候选时快照标记 FallbackFrom。</summary>
    ModelBindingResolution Resolve(LogicalProfileId logicalProfile, ModelBindingSnapshot? explicitFallback = null);

    /// <summary>profile 是否存在任一可用候选。</summary>
    bool IsAvailable(LogicalProfileId logicalProfile);

    /// <summary>运行期健康证据协议：按 (profile, provider, model) 精确匹配并更新候选。</summary>
    bool ApplyHealth(LogicalProfileId profile, string providerId, string modelId, bool healthy);
}

/// <summary>
/// CAP-006 — Model Routing 的封闭状态词汇（fail-closed）：profile 解析不出
/// 可用 binding 即 <see cref="RoutingUnavailable"/>，不存在静默降级路径。
/// </summary>
public enum ModelRoutingStatus
{
    /// <summary>profile 已解析为有效且可用的冻结 binding。</summary>
    Resolved,

    /// <summary>profile 未知或无可用候选（诚实缺能力，调用方自行处置）。</summary>
    RoutingUnavailable,
}

/// <summary>
/// CAP-006 — 产品拥有的逻辑模型 profile 标识（自 internal 缝公开化，值域冻结）。
/// concrete provider 绑定不在此处决定；Perception 与 Agent 决策共用此词汇。
/// </summary>
public readonly record struct LogicalProfileId(string Value)
{
    /// <summary>Slow 语义文本 profile（UniPerception 组合链的 Slow Text 档）。</summary>
    public static LogicalProfileId Text => new("slow.semantic.text");

    /// <summary>Slow 语义视觉 profile（独立能力，不经 SlowTextGate）。</summary>
    public static LogicalProfileId Visual => new("slow.semantic.visual");

    /// <summary>Agent 决策模型 profile（CAP-006 增：真实买方为决策通道组合根）。</summary>
    public static LogicalProfileId AgentDecision => new("agent.decision");

    /// <summary>值非空即有效。</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    /// <summary>诊断与日志面的稳定表示。</summary>
    public override string ToString() => Value;
}

/// <summary>logical profile 一次解析的结果：封闭状态 + 冻结 binding + 诊断。</summary>
public sealed record ModelBindingResolution(
    ModelRoutingStatus Status,
    ModelBindingSnapshot? Binding,
    string? Diagnostic = null)
{
    /// <summary>仅当 Resolved 且 binding 有效可用时为真。</summary>
    public bool IsResolved => Status == ModelRoutingStatus.Resolved
        && Binding is { IsValid: true, Available: true };
}

/// <summary>
/// realization 在装配时解析出的冻结 concrete binding（不可变快照，schema 沿用
/// slow.binding.v1）。可用性/健康由注册方据证据给出，字符串配置不作数；
/// 运行期健康证据经 <see cref="ModelManagement.ApplyHealth"/> 更新。
/// </summary>
public sealed record ModelBindingSnapshot(
    LogicalProfileId LogicalProfile,
    string ProviderId,
    string ModelId,
    string? ConfigId = null,
    string? PipelineRevision = null,
    string? DeploymentId = null,
    string? VariantId = null,
    bool Available = true,
    bool Experimental = true,
    string? FallbackFrom = null,
    string SchemaVersion = "slow.binding.v1",
    bool Health = true)
{
    /// <summary>profile、provider、model 与 schema 版本齐备即有效。</summary>
    public bool IsValid => LogicalProfile.IsValid && !string.IsNullOrWhiteSpace(ProviderId)
        && !string.IsNullOrWhiteSpace(ModelId)
        && !string.IsNullOrWhiteSpace(SchemaVersion);
}

/// <summary>
/// CAP-006/007/008 — 产品 Model Management 能力组件（L2 实现：IModelManagement 的
/// 语义权威）。只负责 logical profile 到冻结 binding 的注册与解析：不下载、安装、
/// 热切换，也不把任何外部 binding registry（如 DSH）带入 Product Runtime——外部
/// 来源只能经 <see cref="Register"/> 注入快照，解析/fail-closed 语义归产品所有。
/// 候选偏好（RegisterPreference 依序游走 + FallbackFrom 诚实降级轨迹）、健康证据
/// （ApplyHealth）与拉式健康聚合（CheckHealth）见 CAP-007/008。生命周期：进程级
/// 常驻、纯内存、无资源句柄——按纪律 RL1 停在 Registered 即稳态，无 teardown 义务。
/// </summary>
public sealed class ModelManagement : IModelManagement
{
    private readonly ConcurrentDictionary<LogicalProfileId, IReadOnlyList<ModelBindingSnapshot>> _bindings = new();
    private readonly CapabilityDescription _description;

    /// <summary>可选的初始 binding 集合（逐个按 <see cref="Register"/> 规则注入：
    /// 同 profile 后者覆盖前者为唯一候选）+ 可选自述声明（缺省 Canonical）。</summary>
    public ModelManagement(IEnumerable<ModelBindingSnapshot>? bindings = null, CapabilityDescription? description = null)
    {
        _description = description ?? CanonicalDescription;
        if (bindings is null)
            return;
        foreach (var binding in bindings)
            Register(binding);
    }

    /// <summary>同表视图：直接持有传入的活字典（共享候选存储），供 WithDescription。</summary>
    private ModelManagement(
        ConcurrentDictionary<LogicalProfileId, IReadOnlyList<ModelBindingSnapshot>> bindings,
        CapabilityDescription description)
    {
        _bindings = bindings;
        _description = description;
    }

    /// <summary>CAP-008 — ICapability 自述：不可变声明（缺省为 canonical Definition）。</summary>
    public CapabilityDescription Description => _description;

    /// <summary>Kernel 拥有的 canonical Definition 声明（无 realization 角色；
    /// 组合根经 <see cref="WithDescription"/> 附加角色后注册实例）。</summary>
    public static CapabilityDescription CanonicalDescription { get; } = new(
        ModelManagementProtocol.CapabilityId, "1.0.0", CapabilityScope.ProductRuntime,
        new[] { new CapabilityProtocol(ModelManagementProtocol.BindingResolution, ModelManagementProtocol.Version) },
        Array.Empty<CapabilityDependency>(), HealthStatus.Unknown,
        CapabilityCategory.ModelRouting,
        Array.Empty<CapabilityRole>(),
        Array.Empty<CapabilityRelationship>());

    /// <summary>不可变重述：**同一活注册表**的重述视图——与原实例共享候选存储
    ///（Register/ApplyHealth 经任一视图生效、彼此可见，不出现状态分叉），仅自述
    /// 声明不同（组合根附加 realization 角色后注册实例）。G6：不经公开 ctor
    /// 重建，偏好序完整保留。</summary>
    public ModelManagement WithDescription(CapabilityDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        return new(_bindings, description);
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<ModelBindingSnapshot> Bindings =>
        _bindings.Values.SelectMany(candidates => candidates).ToArray();

    /// <inheritdoc/>
    public void Register(ModelBindingSnapshot binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (!binding.IsValid)
            throw new ArgumentException("invalid model binding snapshot", nameof(binding));
        _bindings[binding.LogicalProfile] = new[] { binding with { } };
    }

    /// <inheritdoc/>
    public void RegisterPreference(
        LogicalProfileId profile, IReadOnlyList<ModelBindingSnapshot> orderedCandidates)
    {
        ArgumentNullException.ThrowIfNull(orderedCandidates);
        if (!profile.IsValid)
            throw new ArgumentException("logical profile is required", nameof(profile));
        if (orderedCandidates.Count == 0)
            throw new ArgumentException("at least one candidate is required", nameof(orderedCandidates));
        var frozen = orderedCandidates.ToArray();
        var seenIdentities = new HashSet<(string Provider, string Model)>();
        foreach (var candidate in frozen)
        {
            if (candidate is null || !candidate.IsValid)
                throw new ArgumentException("invalid model binding snapshot", nameof(orderedCandidates));
            if (candidate.LogicalProfile != profile)
                throw new ArgumentException(
                    $"candidate '{candidate.ModelId}' belongs to profile '{candidate.LogicalProfile}'",
                    nameof(orderedCandidates));
            if (!seenIdentities.Add((candidate.ProviderId, candidate.ModelId)))
                throw new ArgumentException(
                    $"duplicate candidate identity '{candidate.ProviderId}/{candidate.ModelId}'",
                    nameof(orderedCandidates));
        }
        _bindings[profile] = frozen.Select(candidate => candidate with { }).ToArray();
    }

    /// <inheritdoc/>
    public ModelBindingResolution Resolve(LogicalProfileId logicalProfile, ModelBindingSnapshot? explicitFallback = null)
    {
        if (_bindings.TryGetValue(logicalProfile, out var candidates))
        {
            ModelBindingSnapshot? preferredSkipped = null;
            foreach (var candidate in candidates)
            {
                if (candidate is { IsValid: true, Available: true, Health: true })
                {
                    var chosen = candidate with { };
                    if (preferredSkipped is { } skipped)
                        chosen = chosen with { FallbackFrom = skipped.ModelId };
                    return new(ModelRoutingStatus.Resolved, chosen, null);
                }
                preferredSkipped ??= candidate;
            }

            // Fallback is accepted only when the caller explicitly supplies it and it
            // is itself a valid, available binding for the same logical profile.
            if (explicitFallback is { IsValid: true, Available: true, Health: true }
                && explicitFallback.LogicalProfile == logicalProfile)
            {
                return new(ModelRoutingStatus.Resolved,
                    explicitFallback with { FallbackFrom = preferredSkipped?.ModelId }, null);
            }

            var tried = string.Join(",", candidates.Select(candidate => candidate.ModelId));
            return new(ModelRoutingStatus.RoutingUnavailable, null,
                $"ROUTING_UNAVAILABLE: no usable candidate for '{logicalProfile.Value}' (tried: {tried})");
        }

        if (explicitFallback is { IsValid: true, Available: true, Health: true }
            && explicitFallback.LogicalProfile == logicalProfile)
        {
            return new(ModelRoutingStatus.Resolved,
                explicitFallback with { FallbackFrom = logicalProfile.Value }, null);
        }

        return new(ModelRoutingStatus.RoutingUnavailable, null,
            $"ROUTING_UNAVAILABLE: unknown or unavailable profile '{logicalProfile.Value}'");
    }

    /// <inheritdoc/>
    public bool IsAvailable(LogicalProfileId logicalProfile) =>
        _bindings.TryGetValue(logicalProfile, out var candidates)
        && candidates.Any(candidate => candidate is { IsValid: true, Available: true, Health: true });

    /// <inheritdoc/>
    public bool ApplyHealth(
        LogicalProfileId profile, string providerId, string modelId, bool healthy)
    {
        if (!profile.IsValid
            || string.IsNullOrWhiteSpace(providerId)
            || string.IsNullOrWhiteSpace(modelId))
            return false;
        if (!_bindings.TryGetValue(profile, out var candidates))
            return false;

        var updated = false;
        var replaced = new List<ModelBindingSnapshot>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (string.Equals(candidate.ProviderId, providerId, StringComparison.Ordinal)
                && string.Equals(candidate.ModelId, modelId, StringComparison.Ordinal))
            {
                replaced.Add(candidate with { Available = healthy, Health = healthy });
                updated = true;
            }
            else
            {
                replaced.Add(candidate);
            }
        }
        if (!updated)
            return false;
        _bindings[profile] = replaced;
        return true;
    }

    /// <summary>CAP-008 — 拉式健康聚合：无任何注册 → Unknown（未配置≠不健康）；
    /// 所有已注册 profile 均有可用候选 → Healthy；部分无 → Degraded（诊断列出）；
    /// 全部无 → Unhealthy。数据源与 ApplyHealth 天然联动。</summary>
    public CapabilityHealthReport CheckHealth()
    {
        var snapshot = _bindings.ToArray();
        if (snapshot.Length == 0)
            return new(HealthStatus.Unknown, "no bindings registered");
        var unusable = new List<string>();
        foreach (var (profile, candidates) in snapshot)
            if (!candidates.Any(candidate => candidate is { IsValid: true, Available: true, Health: true }))
                unusable.Add(profile.Value);
        if (unusable.Count == 0)
            return new(HealthStatus.Healthy, null);
        var diagnostic = "profiles without usable candidate: " + string.Join(",", unusable);
        return new(unusable.Count == snapshot.Length ? HealthStatus.Unhealthy : HealthStatus.Degraded, diagnostic);
    }
}
