using UniClaw.Kernel.Capability;

namespace UniClaw.Agent.Dsh;

/// <summary>
/// CAP-006 — Model Management 的缺省（借用 DSH）realization。从 DSH profile
/// 配置（.dsh/profiles/uniagent-prod.yaml 的 modelSelection 块，经
/// <see cref="UniagentProdYaml"/> 加载）推导产品 logical profile 的 binding
/// 快照，注册进公开产品缝 <see cref="ModelManagement"/>。DSH 专有形状
/// （<see cref="ModelConfiguration"/>）不越出 adapter；解析语义（fail-closed、
/// 偏好序游走、fallback 规则）归产品缝所有。
///
/// CAP-007 — 选择映射：
/// <list type="bullet">
/// <item><c>modelSelection.selected</c> = 缺省选择，服务所有未显式配置的 profile
/// （缺省注册 agent.decision 与 slow.semantic.text；slow.semantic.visual 维持不
/// 注册——诚实 NotConfigured，除非显式配置）。</item>
/// <item><c>modelSelection.profiles.&lt;profile&gt;: key</c> = 该 profile 单选；
/// 有序列表 = 偏好序（经 <see cref="ModelManagement.RegisterPreference"/> 注册，
/// 首选在前）。</item>
/// <item>未知 profile 名 / 引用未知 choice 一律 fail-closed。</item>
/// <item>环境覆盖 UNICLAW_UNIAGENT_PROD_MODEL 只作用于 selected 派生的缺省
/// binding——显式 per-profile 选择是更强决策，不被覆盖（D3 冻结）。</item>
/// </list>
/// </summary>
public static class DshModelManagement
{
    /// <summary>产品 logical profile 值域（供 fail-closed 校验与诊断）。</summary>
    public static readonly IReadOnlyList<LogicalProfileId> ProductProfiles = new[]
    {
        LogicalProfileId.AgentDecision,
        LogicalProfileId.Text,
        LogicalProfileId.Visual,
    };

    /// <summary>与 Host 组合根声明一致的 realization 名。</summary>
    public const string RealizationName = "dsh-model-management";

    /// <summary>从 DSH profile 配置构建产品缝（缺省 + per-profile 覆盖 + 偏好序）。
    /// modelNameOverride 透传 UNICLAW_UNIAGENT_PROD_MODEL（只覆盖缺省 binding）。</summary>
    public static ModelManagement FromProfile(
        UniagentProdConfiguration configuration, string? modelNameOverride = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var choices = configuration.Choices
            ?? new Dictionary<string, ModelConfiguration>(StringComparer.Ordinal);
        var explicitSelections = ValidateProfileSelections(configuration.ProfileSelections);

        var registry = new ModelManagement();
        foreach (var profile in ProductProfiles)
        {
            if (explicitSelections.TryGetValue(profile.Value, out var choiceKeys))
            {
                registry.RegisterPreference(profile, choiceKeys
                    .Select(key => Snapshot(profile, key, ResolveChoice(choices, key)))
                    .ToArray());
                continue;
            }

            // 缺省：selected 服务 agent.decision 与 slow.semantic.text（env 覆盖
            // 只在此生效）；visual 缺省不注册。
            if (profile != LogicalProfileId.AgentDecision && profile != LogicalProfileId.Text)
                continue;
            var defaultName = string.IsNullOrWhiteSpace(modelNameOverride)
                ? configuration.Model.Name
                : modelNameOverride.Trim();
            registry.Register(new ModelBindingSnapshot(
                profile, configuration.Model.Provider, defaultName,
                ConfigId: $"uniagent-prod:{configuration.SelectedModelKey ?? "default"}",
                VariantId: RealizationName, Available: true, Experimental: false));
        }
        return registry;
    }

    /// <summary>产品快照 → DSH 决策/slow 通道的模型形状（DSH 类型不出 adapter）。</summary>
    public static ModelConfiguration ToDshModel(ModelBindingSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new ModelConfiguration(snapshot.ProviderId, snapshot.ModelId);
    }

    /// <summary>经产品缝解析并转换；不可解析时 fail-closed（诊断含
    /// ROUTING_UNAVAILABLE——组合根不静默降级到其他模型）。</summary>
    public static ModelConfiguration ResolveDshModel(ModelManagement models, LogicalProfileId profile)
    {
        ArgumentNullException.ThrowIfNull(models);
        var resolution = models.Resolve(profile);
        if (!resolution.IsResolved || resolution.Binding is null)
            throw new InvalidOperationException(
                $"ROUTING_UNAVAILABLE: profile '{profile}' has no usable DSH binding ({resolution.Diagnostic})");
        return ToDshModel(resolution.Binding);
    }

    private static Dictionary<string, IReadOnlyList<string>> ValidateProfileSelections(
        IReadOnlyList<ModelProfileSelection>? selections)
    {
        var byProfile = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (selections is null)
            return byProfile;
        var knownProfiles = ProductProfiles.Select(profile => profile.Value).ToHashSet(StringComparer.Ordinal);
        foreach (var selection in selections)
        {
            if (selection is not { IsValid: true })
                throw new InvalidOperationException(
                    "model-selection-invalid: malformed profile selection entry");
            if (!knownProfiles.Contains(selection.Profile))
                throw new InvalidOperationException(
                    $"model-selection-invalid: unknown profile '{selection.Profile}' "
                    + $"(valid: {string.Join(", ", ProductProfiles.Select(p => p.Value))})");
            if (byProfile.ContainsKey(selection.Profile))
                throw new InvalidOperationException(
                    $"model-selection-invalid: duplicate profile '{selection.Profile}'");
            byProfile[selection.Profile] = selection.ChoiceKeys;
        }
        return byProfile;
    }

    private static ModelConfiguration ResolveChoice(
        IReadOnlyDictionary<string, ModelConfiguration> choices, string choiceKey)
        => choices.TryGetValue(choiceKey, out var choice)
            ? choice
            : throw new InvalidOperationException(
                $"model-selection-invalid: unknown choice '{choiceKey}' "
                + "(loader 校验后仍不可解析——配置对象与 yaml 脱节)");

    private static ModelBindingSnapshot Snapshot(
        LogicalProfileId profile, string choiceKey, ModelConfiguration choice) =>
        new(profile, choice.Provider, choice.Name,
            ConfigId: $"uniagent-prod:{choiceKey}",
            VariantId: RealizationName, Available: true, Experimental: false);
}
