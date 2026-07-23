namespace UniClaw.Core.UniBrain;

/// <summary>
/// IPromptLibrary — 按 capability key 检索 Prompt 模板 (D-4)。
/// 子接口实现注入此接口获取 prompt，再调用 IModelProvider。
/// 不暴露在 IUniBrain facade 上（prompt 管理是子接口内部关注点）。
/// </summary>
public interface IPromptLibrary
{
    /// <summary>按 capability 获取模板。不存在 → null（不抛异常）。</summary>
    PromptTemplate? GetTemplate(string capability);

    /// <summary>列出所有已注册 capability key（调试/诊断）。</summary>
    IReadOnlyList<string> GetCapabilities();

    /// <summary>诊断方法：capability 是否已注册（不触发热路径, D-4）。</summary>
    bool ValidateCapability(string capability);
}
