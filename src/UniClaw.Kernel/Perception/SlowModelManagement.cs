namespace UniClaw.Kernel.Perception;

/// <summary>Model Management 只负责 logical profile 到冻结 binding 的解析。</summary>
internal enum SlowRoutingStatus
{
    Resolved,
    RoutingUnavailable,
}

internal sealed record SlowBindingResolution(
    SlowRoutingStatus Status,
    ModelBindingSnapshot? Binding,
    string? Diagnostic = null)
{
    public bool IsResolved => Status == SlowRoutingStatus.Resolved
        && Binding is { IsValid: true, Available: true };
}

/// <summary>
/// 进程内的窄模型管理 seam。Product 只看到 logical profile；concrete identity
/// 由 realization 注册，解析后得到不可变 snapshot。这里不下载、安装、热切换，
/// 也不把 DSH 的 binding registry 带入 Product Runtime。
/// </summary>
internal sealed class SlowModelManagement
{
    private readonly Dictionary<LogicalProfileId, ModelBindingSnapshot> _bindings = new();

    public SlowModelManagement(IEnumerable<ModelBindingSnapshot>? bindings = null)
    {
        if (bindings is null)
            return;
        foreach (var binding in bindings)
            Register(binding);
    }

    public IReadOnlyCollection<ModelBindingSnapshot> Bindings => _bindings.Values.ToArray();

    public void Register(ModelBindingSnapshot binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (!binding.IsValid)
            throw new ArgumentException("invalid model binding snapshot", nameof(binding));
        _bindings[binding.LogicalProfile] = binding with { };
    }

    public SlowBindingResolution Resolve(LogicalProfileId logicalProfile, ModelBindingSnapshot? explicitFallback = null)
    {
        if (_bindings.TryGetValue(logicalProfile, out var binding))
        {
            if (binding.Available && binding.Health && binding.IsValid)
                return new(SlowRoutingStatus.Resolved, binding with { }, null);
            if (explicitFallback is { IsValid: true, Available: true, Health: true }
                && explicitFallback.LogicalProfile == logicalProfile)
            {
                return new(SlowRoutingStatus.Resolved,
                    explicitFallback with { FallbackFrom = binding.ModelId }, null);
            }
            return new(SlowRoutingStatus.RoutingUnavailable, null, "binding unavailable");
        }

        // Fallback is accepted only when the caller explicitly supplies it and it
        // is itself a valid, available binding for the same logical profile.
        if (explicitFallback is { IsValid: true, Available: true, Health: true }
            && explicitFallback.LogicalProfile == logicalProfile)
        {
            return new(SlowRoutingStatus.Resolved,
                explicitFallback with { FallbackFrom = logicalProfile.Value }, null);
        }

        return new(SlowRoutingStatus.RoutingUnavailable, null,
            $"ROUTING_UNAVAILABLE: unknown or unavailable profile '{logicalProfile.Value}'");
    }

    public bool IsAvailable(LogicalProfileId logicalProfile) =>
        _bindings.TryGetValue(logicalProfile, out var binding)
        && binding.IsValid && binding.Available && binding.Health;
}

/// <summary>常用的 deterministic replay binding；仍标记 experimental。</summary>
internal static class SlowReplayProfiles
{
    public static SlowModelManagement CreateDefault() => new(new[]
    {
        new ModelBindingSnapshot(LogicalProfileId.Text, "replay", "slow-text-replay",
            ConfigId: "replay-v1", PipelineRevision: "1", VariantId: "deterministic",
            Available: true, Experimental: true),
        new ModelBindingSnapshot(LogicalProfileId.Visual, "replay", "slow-visual-replay",
            ConfigId: "replay-v1", PipelineRevision: "1", VariantId: "deterministic",
            Available: true, Experimental: true),
    });
}
