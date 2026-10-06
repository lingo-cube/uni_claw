using UniClaw.Kernel.Capability;

namespace UniClaw.Host.Capability;

/// <summary>
/// CAP-006 — Model Management 能力组件的管理面声明（ADR-0038 的 Binding 层）。
/// 产品 Definition 是 Kernel 公开缝 <see cref="ModelManagement"/>；本组合根把
/// 缺省 realization 显式声明为借用 DSH（dsh-model-management：binding 来自
/// .dsh/profiles/uniagent-prod.yaml 的 modelSelection，经 Agent.Dsh adapter
/// 的 Register 注入）。将来替换 realization 只改本声明与 adapter，不改 Kernel。
/// 描述不含 provider/model 名——具体绑定只存在于运行时快照。
/// </summary>
public static class ModelManagementCapabilityComposition
{
    public const string ModelManagementId = "uni.model.management";
    public const string ModelBindingResolutionProtocol = "Model Binding Resolution";
    public const string ProtocolVersion = "1.0";

    /// <summary>缺省 realization：借用 DSH 配置/组件/接口的模型管理实现。</summary>
    public const string DefaultRealization = "dsh-model-management";

    public const string CompositionSource = "product-composition-root";

    /// <summary>向 Product Registry 注册 uni.model.management descriptor。
    /// realization 显式传入（缺省 DSH）；同 registry 重复注册 fail-closed
    /// （CapabilityRegistry 既有执法）。</summary>
    public static CapabilityRegistry RegisterModelManagement(
        CapabilityRegistry? registry = null, string? realization = null)
    {
        registry ??= new CapabilityRegistry(TrustDomain.Product);
        var realizationName = string.IsNullOrWhiteSpace(realization)
            ? DefaultRealization
            : realization.Trim();
        registry.Register(new CapabilityDescription(
            ModelManagementId, "1.0.0", CapabilityScope.ProductRuntime,
            new[] { new CapabilityProtocol(ModelBindingResolutionProtocol, ProtocolVersion) },
            Array.Empty<CapabilityDependency>(),
            HealthStatus.Unknown,
            CapabilityCategory.Generic,
            new[]
            {
                new CapabilityRole(CapabilityRoleKind.ProductProtocol, ModelBindingResolutionProtocol),
                new CapabilityRole(CapabilityRoleKind.Realization, realizationName),
            },
            Array.Empty<CapabilityRelationship>()), CompositionSource);
        return registry;
    }
}
