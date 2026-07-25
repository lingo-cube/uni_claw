namespace UniClaw.Core.UniBrain;

/// <summary>
/// IModelRouter — 将 capability 字符串解析为一个已套观测 decorator 的 IModelProvider。
/// 实现在组装期为每个裸 provider 套 ObservingModelProvider，因此经 Resolve 返回的 provider
/// 必然产生 AICallRecord，调用方无法绕过观测。
/// </summary>
public interface IModelRouter
{
    /// <summary>
    /// 按 capability 解析到对应的（已观测）IModelProvider。
    /// capability 未命中且无合法 default 回落时抛 DomainValidationException。
    /// </summary>
    IModelProvider Resolve(string capability);
}
