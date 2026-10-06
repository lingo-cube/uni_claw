using UniClaw.Kernel.Capability;

namespace UniClaw.Host.Capability;

/// <summary>
/// CAP-006/008 — Model Management 能力组件的管理面声明（ADR-0038 的 Binding 层）。
/// 产品 Definition 是 Kernel 公开缝 <see cref="ModelManagement"/>；本组合根把缺省
/// realization 显式声明为借用 DSH（dsh-model-management：binding 来自
/// .dsh/profiles/uniagent-prod.yaml 的 modelSelection，经 Agent.Dsh adapter
/// 注入）。将来替换 realization 只改本声明与 adapter，不改 Kernel。
/// CAP-008 起**注册可执行实例**（Register(ICapability)：声明与运行时缝同一事实，
/// registry.Resolve 可取回实例）；描述不含 provider/model 名——具体绑定只存在
/// 于运行时快照。协议常量以 Kernel <see cref="ModelManagementProtocol"/> 为单一
/// 真相（本类常量为兼容别名）。
/// </summary>
public static class ModelManagementCapabilityComposition
{
    /// <summary>能力 id（别名：真相在 <see cref="ModelManagementProtocol.CapabilityId"/>）。</summary>
    public const string ModelManagementId = ModelManagementProtocol.CapabilityId;

    /// <summary>协议名（别名：真相在 <see cref="ModelManagementProtocol.BindingResolution"/>）。</summary>
    public const string ModelBindingResolutionProtocol = ModelManagementProtocol.BindingResolution;

    /// <summary>协议版本（别名：真相在 <see cref="ModelManagementProtocol.Version"/>）。</summary>
    public const string ProtocolVersion = ModelManagementProtocol.Version;

    /// <summary>缺省 realization：借用 DSH 配置/组件/接口的模型管理实现。
    /// 组合根调用处应显式传 adapter 侧的 RealizationName 常量以消除双写。</summary>
    public const string DefaultRealization = "dsh-model-management";

    public const string CompositionSource = "product-composition-root";

    /// <summary>向 Product Registry 注册 uni.model.management **实例**（R1：可执行
    /// 能力必须实例注册，ModelRouting 类别拒绝 description-only）。instance 缺省为
    /// 空 Candidate 表的裸实例（诚实 Unknown，直到 realization 注入）；realization
    /// 显式传入（缺省 DSH）。</summary>
    public static CapabilityRegistry RegisterModelManagement(
        CapabilityRegistry? registry = null,
        ModelManagement? instance = null,
        string? realization = null)
    {
        registry ??= new CapabilityRegistry(TrustDomain.Product);
        var executable = instance ?? new ModelManagement();
        var realizationName = string.IsNullOrWhiteSpace(realization)
            ? DefaultRealization
            : realization.Trim();
        registry.Register(executable.WithDescription(Description(realizationName)), CompositionSource);
        return registry;
    }

    /// <summary>组合根组装的声明：canonical Definition + realization 角色。</summary>
    public static CapabilityDescription Description(string realizationName) => new(
        ModelManagementProtocol.CapabilityId, "1.0.0", CapabilityScope.ProductRuntime,
        new[] { new CapabilityProtocol(ModelManagementProtocol.BindingResolution, ModelManagementProtocol.Version) },
        Array.Empty<CapabilityDependency>(),
        HealthStatus.Unknown,
        CapabilityCategory.ModelRouting,
        new[]
        {
            new CapabilityRole(CapabilityRoleKind.ProductProtocol, ModelManagementProtocol.BindingResolution),
            new CapabilityRole(CapabilityRoleKind.Realization, realizationName),
        },
        Array.Empty<CapabilityRelationship>());
}
