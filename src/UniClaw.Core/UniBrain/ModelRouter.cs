using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Observability;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// ModelRouter — IModelRouter 默认实现 (task 4.2)。
/// 组装期为每个裸 provider 套 ObservingModelProvider，经 Resolve 返回的实例必然产生 AICallRecord，
/// 调用方无法绕过观测。路由优先级: capability 精确命中 > default 回落 > fail-fast。
/// </summary>
public sealed class ModelRouter : IModelRouter
{
    private readonly ImmutableDictionary<string, string> _capabilityRouting;
    private readonly ImmutableDictionary<string, IModelProvider> _observed;
    private readonly string _defaultProviderId;

    /// <summary>
    /// 构造 ModelRouter。
    /// 校验 capabilityRouting 引用的每个 providerId 必须存在于 providers；
    /// defaultProviderId 的合法性延迟到 Resolve 时校验。
    /// </summary>
    public ModelRouter(
        ImmutableDictionary<string, string> capabilityRouting,
        ImmutableDictionary<string, IModelProvider> providers,
        ITraceRecorder recorder,
        string defaultProviderId)
    {
        foreach (var pid in capabilityRouting.Values)
        {
            if (!providers.ContainsKey(pid))
            {
                throw new DomainValidationException(
                    nameof(providers),
                    pid,
                    $"capabilityRouting references unknown provider '{pid}'.");
            }
        }

        _capabilityRouting = capabilityRouting;
        _observed = ImmutableDictionary.CreateRange(
            providers.Select(kv => new KeyValuePair<string, IModelProvider>(
                kv.Key,
                new ObservingModelProvider(kv.Value, recorder))));
        _defaultProviderId = defaultProviderId;
    }

    /// <inheritdoc/>
    public IModelProvider Resolve(string capability)
    {
        if (_capabilityRouting.TryGetValue(capability, out var pid) && _observed.TryGetValue(pid, out var p))
        {
            return p;
        }

        if (_observed.TryGetValue(_defaultProviderId, out var def))
        {
            return def;
        }

        throw new DomainValidationException(
            nameof(capability),
            capability,
            $"No provider routed for capability '{capability}' and default '{_defaultProviderId}' not configured.");
    }
}
