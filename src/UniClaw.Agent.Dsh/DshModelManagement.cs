using UniClaw.Kernel.Capability;

namespace UniClaw.Agent.Dsh;

/// <summary>
/// CAP-006 — Model Management 的缺省（借用 DSH）realization。从 DSH profile
/// 配置（.dsh/profiles/uniagent-prod.yaml 的 modelSelection 块，经
/// <see cref="UniagentProdYaml"/> 加载）推导产品 logical profile 的 binding
/// 快照，注册进公开产品缝 <see cref="ModelManagement"/>。DSH 专有形状
/// （<see cref="ModelConfiguration"/>）不越出 adapter；解析语义（fail-closed、
/// fallback 规则）归产品缝所有。现状映射（CAP-006 D2 冻结）：selected 选择
/// 同时服务 agent.decision 与 slow.semantic.text；slow.semantic.visual 不注册
/// （诚实 NotConfigured，与既有 live 行为一致）。
/// </summary>
public static class DshModelManagement
{
    /// <summary>与 Host 组合根声明一致的 realization 名。</summary>
    public const string RealizationName = "dsh-model-management";

    /// <summary>从 DSH profile 配置构建产品缝：yaml selected → agent.decision 与
    /// slow.semantic.text 双注册。modelNameOverride 透传
    /// UNICLAW_UNIAGENT_PROD_MODEL（沿用既有覆盖行为）。</summary>
    public static ModelManagement FromProfile(
        UniagentProdConfiguration configuration, string? modelNameOverride = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var provider = configuration.Model.Provider;
        var model = string.IsNullOrWhiteSpace(modelNameOverride)
            ? configuration.Model.Name
            : modelNameOverride.Trim();
        var configId = $"uniagent-prod:{configuration.SelectedModelKey ?? "default"}";
        return new ModelManagement(new[]
        {
            new ModelBindingSnapshot(LogicalProfileId.AgentDecision, provider, model,
                ConfigId: configId, VariantId: RealizationName, Available: true, Experimental: false),
            new ModelBindingSnapshot(LogicalProfileId.Text, provider, model,
                ConfigId: configId, VariantId: RealizationName, Available: true, Experimental: false),
        });
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
}
