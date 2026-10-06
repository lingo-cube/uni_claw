namespace UniClaw.Kernel.Capability;

/// <summary>
/// CAP-006 — Model Routing 的封闭状态词汇（fail-closed）：profile 解析不出
/// 可用 binding 即 <see cref="RoutingUnavailable"/>，不存在静默降级路径。
/// </summary>
public enum ModelRoutingStatus
{
    /// <summary>profile 已解析为有效且可用的冻结 binding。</summary>
    Resolved,

    /// <summary>profile 未知或 binding 不可用（诚实缺能力，调用方自行处置）。</summary>
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
/// slow.binding.v1）。可用性/健康由注册方据证据给出，字符串配置不作数。
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
/// CAP-006 — 产品 Model Management 能力组件（公开缝；ADR-0038 的 Definition 层，
/// 语义自 internal SlowModelManagement 原样冻结）。只负责 logical profile 到冻结
/// binding 的注册与解析：不下载、安装、热切换，也不把任何外部 binding registry
/// （如 DSH）带入 Product Runtime——外部来源只能经 <see cref="Register"/> 注入
/// 快照，解析/fail-closed 语义归产品所有。缺省 realization 由组合根显式声明
/// （当前为借用 DSH 配置的 dsh-model-management；替换只动 adapter）。
/// </summary>
public sealed class ModelManagement
{
    private readonly Dictionary<LogicalProfileId, ModelBindingSnapshot> _bindings = new();

    /// <summary>可选的初始 binding 集合（逐个按 <see cref="Register"/> 规则注入）。</summary>
    public ModelManagement(IEnumerable<ModelBindingSnapshot>? bindings = null)
    {
        if (bindings is null)
            return;
        foreach (var binding in bindings)
            Register(binding);
    }

    /// <summary>当前已注册的冻结快照（拷贝视图）。</summary>
    public IReadOnlyCollection<ModelBindingSnapshot> Bindings => _bindings.Values.ToArray();

    /// <summary>注册/覆盖一个 binding 快照；无效快照 fail-closed。</summary>
    public void Register(ModelBindingSnapshot binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (!binding.IsValid)
            throw new ArgumentException("invalid model binding snapshot", nameof(binding));
        _bindings[binding.LogicalProfile] = binding with { };
    }

    /// <summary>
    /// 解析 logical profile。已注册且可用/健康 → Resolved；显式 fallback 仅在
    /// 调用方提供且自身为同 profile 的有效 binding 时接受（标记 FallbackFrom）；
    /// 其余一律 RoutingUnavailable（诚实缺能力，禁静默降级）。
    /// </summary>
    public ModelBindingResolution Resolve(LogicalProfileId logicalProfile, ModelBindingSnapshot? explicitFallback = null)
    {
        if (_bindings.TryGetValue(logicalProfile, out var binding))
        {
            if (binding.Available && binding.Health && binding.IsValid)
                return new(ModelRoutingStatus.Resolved, binding with { }, null);
            if (explicitFallback is { IsValid: true, Available: true, Health: true }
                && explicitFallback.LogicalProfile == logicalProfile)
            {
                return new(ModelRoutingStatus.Resolved,
                    explicitFallback with { FallbackFrom = binding.ModelId }, null);
            }
            return new(ModelRoutingStatus.RoutingUnavailable, null, "binding unavailable");
        }

        // Fallback is accepted only when the caller explicitly supplies it and it
        // is itself a valid, available binding for the same logical profile.
        if (explicitFallback is { IsValid: true, Available: true, Health: true }
            && explicitFallback.LogicalProfile == logicalProfile)
        {
            return new(ModelRoutingStatus.Resolved,
                explicitFallback with { FallbackFrom = logicalProfile.Value }, null);
        }

        return new(ModelRoutingStatus.RoutingUnavailable, null,
            $"ROUTING_UNAVAILABLE: unknown or unavailable profile '{logicalProfile.Value}'");
    }

    /// <summary>profile 是否已注册且有效/可用/健康。</summary>
    public bool IsAvailable(LogicalProfileId logicalProfile) =>
        _bindings.TryGetValue(logicalProfile, out var binding)
        && binding.IsValid && binding.Available && binding.Health;
}
