namespace UniClaw.Agent.Dsh;

/// <summary>
/// PRF-002/ADR-0041 — uniagent-prod 的 DSH realization 绑定加载结果：
/// CAP-007 的 modelSelection（缺省选择 + choices 目录 + per-profile 偏好序）
/// 与 DSH 服务端点。本类型只含宿主映射数据，产品身份/角色声明在
/// <c>UniClaw.Agent.Profile.UniAgentProfile</c>。
/// </summary>
public sealed record UniagentDshBindings(
    ModelConfiguration Model,
    DshServiceEndpoint Service,
    string? SelectedModelKey = null,
    IReadOnlyDictionary<string, ModelConfiguration>? Choices = null,
    IReadOnlyList<ModelProfileSelection>? ProfileSelections = null)
{
    /// <summary>Explicit construction for tests and fixtures. The composed
    /// runtime loads its bindings from the DSH binding file
    /// (<see cref="UniagentDshBindingsYaml.LoadDefault"/>), never from code
    /// constants.</summary>
    public static UniagentDshBindings Create(
        ModelConfiguration model,
        DshServiceEndpoint service,
        string? selectedModelKey = null,
        IReadOnlyDictionary<string, ModelConfiguration>? choices = null,
        IReadOnlyList<ModelProfileSelection>? profileSelections = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(service);
        model.Validate();
        service.Validate();
        return new UniagentDshBindings(model, service, selectedModelKey, choices, profileSelections);
    }
}
