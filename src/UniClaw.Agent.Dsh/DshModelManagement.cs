using UniClaw.Agent.Profile;
using UniClaw.Kernel.Capability;

namespace UniClaw.Agent.Dsh;

/// <summary>
/// CAP-006 — Model Management 的缺省（借用 DSH）realization。PRF-002
/// （ADR-0041）起吃双输入：host-neutral 产品声明
/// （<see cref="UniAgentProfile"/>：模型角色必需性）+ DSH 绑定
/// （<see cref="UniagentDshBindings"/>：CAP-007 modelSelection 与服务端点），
/// 推导产品 logical profile 的 binding 快照，注册进公开产品缝
/// <see cref="ModelManagement"/>。DSH 专有形状不出 adapter；解析语义
/// （fail-closed、偏好序游走、fallback 规则）归产品缝所有。
///
/// CAP-007 选择映射（语义原样，值域校验升级为按产品声明核对）：
/// <list type="bullet">
/// <item><c>modelSelection.selected</c> = 缺省选择，服务所有
/// <c>required=true</c> 且无显式选择的角色（缺省注册 agent.decision 与
/// slow.semantic.text；slow.semantic.visual 维持不注册——诚实
/// NotConfigured，除非显式配置）。</item>
/// <item><c>modelSelection.profiles.&lt;role&gt;: key</c> = 该角色单选；
/// 有序列表 = 偏好序（经 <see cref="ModelManagement.RegisterPreference"/>
/// 注册，首选在前）。role 必须是产品 profile 声明的角色——绑定引用产品
/// 未声明的角色 fail-closed（防死配置）。</item>
/// <item><c>required=true</c> 的角色装配后必须可解析，否则组合根拒启
/// （Profile Boot fail，Q4）；未知 choice 引用一律 fail-closed。</item>
/// <item>环境覆盖 UNICLAW_UNIAGENT_PROD_MODEL 只作用于 selected 派生的
/// 缺省 binding——显式 per-role 选择是更强决策，不被覆盖（D3 冻结）。</item>
/// </list>
/// </summary>
public static class DshModelManagement
{
    /// <summary>产品 logical profile 值域（诊断用；词汇源是
    /// <see cref="LogicalProfileId"/> 静态属性）。</summary>
    public static readonly IReadOnlyList<LogicalProfileId> ProductProfiles = new[]
    {
        LogicalProfileId.AgentDecision,
        LogicalProfileId.Text,
        LogicalProfileId.Visual,
    };

    /// <summary>与 Host 组合根声明一致的 realization 名。</summary>
    public const string RealizationName = "dsh-model-management";

    /// <summary>从「产品声明 + DSH 绑定」构建产品缝（required 缺省 +
    /// per-role 覆盖 + 偏好序 + Q4 装配执法）。modelNameOverride 透传
    /// UNICLAW_UNIAGENT_PROD_MODEL（只覆盖缺省 binding）。</summary>
    public static ModelManagement FromBindings(
        UniagentDshBindings bindings,
        UniAgentProfile profile,
        string? modelNameOverride = null)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(profile);
        var choices = bindings.Choices
            ?? new Dictionary<string, ModelConfiguration>(StringComparer.Ordinal);
        var declaredRoles = ValidateSelectionsAgainstDeclarations(
            bindings.ProfileSelections, profile);

        var registry = new ModelManagement();
        foreach (var declaration in profile.ModelRoles)
        {
            var role = new LogicalProfileId(declaration.Role);
            if (declaredRoles.TryGetValue(role.Value, out var choiceKeys))
            {
                registry.RegisterPreference(role, choiceKeys
                    .Select(key => Snapshot(role, key, ResolveChoice(choices, key)))
                    .ToArray());
                continue;
            }

            if (!declaration.Required)
                continue; // optional 无显式选择 → 诚实 NotConfigured（现状 visual 语义）

            // required 缺省：selected 服务（env 覆盖只在此生效）。
            var defaultName = string.IsNullOrWhiteSpace(modelNameOverride)
                ? bindings.Model.Name
                : modelNameOverride.Trim();
            registry.Register(new ModelBindingSnapshot(
                role, bindings.Model.Provider, defaultName,
                ConfigId: $"uniagent-prod:{bindings.SelectedModelKey ?? "default"}",
                VariantId: RealizationName, Available: true, Experimental: false));
        }

        // Q4 装配执法：required 角色最终必须可解析，否则组合根拒启。
        foreach (var declaration in profile.ModelRoles)
        {
            if (!declaration.Required)
                continue;
            var resolution = registry.Resolve(new LogicalProfileId(declaration.Role));
            if (!resolution.IsResolved || resolution.Binding is null)
                throw new InvalidOperationException(
                    $"PROFILE_BOOT_FAILED: required model role '{declaration.Role}' has no usable "
                    + $"DSH binding ({resolution.Diagnostic})");
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

    /// <summary>CAP-007 选择条目校验：结构合法 + role 必须是产品 profile
    /// 声明的角色（绑定不得引用产品未声明的角色）+ 无重复。</summary>
    private static Dictionary<string, IReadOnlyList<string>> ValidateSelectionsAgainstDeclarations(
        IReadOnlyList<ModelProfileSelection>? selections, UniAgentProfile profile)
    {
        var byRole = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (selections is null)
            return byRole;
        var declared = profile.ModelRoles
            .Select(static role => role.Role)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var selection in selections)
        {
            if (selection is not { IsValid: true })
                throw new InvalidOperationException(
                    "model-selection-invalid: malformed profile selection entry");
            if (!declared.Contains(selection.Profile))
                throw new InvalidOperationException(
                    $"model-selection-invalid: role '{selection.Profile}' is not declared by the "
                    + $"product profile (declared: {string.Join(", ", declared.Order(StringComparer.Ordinal))})");
            if (byRole.ContainsKey(selection.Profile))
                throw new InvalidOperationException(
                    $"model-selection-invalid: duplicate profile '{selection.Profile}'");
            byRole[selection.Profile] = selection.ChoiceKeys;
        }
        return byRole;
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
