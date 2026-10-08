namespace UniClaw.Agent.Profile;

/// <summary>一个产品模型角色的装配声明（Q4/ADR-0041）：角色名必须是编译
/// <c>LogicalProfileId</c> 值域成员；<c>Required</c> 决定组合根装配时缺绑定
/// 是拒启（true）还是诚实 NotConfigured（false）。</summary>
public sealed record ModelRoleDeclaration(string Role, bool Required);

/// <summary>产品工件引用（PRF-005/ADR-0041）：路径相对仓库根，hash 是工件
/// 内容指纹；loader 在装配时 fail-closed 校验（存在 + hash 匹配）。</summary>
public sealed record AssemblyReference(string Path, string Hash);

/// <summary>profile 的产品工件装配引用：静态 prompt manifest 与安全 policy。
/// 每个成员可选；一旦声明即执法。</summary>
public sealed record ProfileAssembly(
    AssemblyReference? PromptManifest = null,
    AssemblyReference? SafetyPolicy = null);

/// <summary>host-neutral UniAgent Profile 的加载结果：编译身份 + 装配修订号
/// + 模型角色声明 + 工件引用。不含 provider/model 名、服务端点或任何
/// 运行态（ADR-0041）。</summary>
public sealed record UniAgentProfile(
    ProductProfile Identity,
    int Revision,
    IReadOnlyList<ModelRoleDeclaration> ModelRoles,
    ProfileAssembly? Assembly = null);
