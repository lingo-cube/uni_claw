namespace UniClaw.Core.UniBrain;

/// <summary>
/// ResolvedPrompt — PromptTemplate.Resolve() 的返回类型 (D-3)。
/// 解析后的 system + user prompt，可直接赋值给 ModelRequest。
/// </summary>
public sealed record class ResolvedPrompt(
    string System,
    string User);
