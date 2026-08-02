using System.Collections.Immutable;
using UniClaw.Core.Domain;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// Prompt 模板 — capability + system/user 模板 + 变量占位符。
/// 模板中使用 {variable_name} 占位符（单花括号，对齐 Python）。
/// 构造期 fail-fast 校验：Capability 非空, 至少一个 prompt 非空,
/// 声明变量必须出现在模板文本中 (D-2)。
/// 对齐 Python PromptTemplate (src/ai/prompts/manager.py)。
/// </summary>
public sealed record class PromptTemplate
{
    /// <summary>Capability key (e.g. "page_analysis", "decide_next_action")</summary>
    public string Capability { get; init; }

    /// <summary>System prompt 模板 (可含 {variable} 占位符)</summary>
    public string SystemPrompt { get; init; }

    /// <summary>User prompt 模板 (可含 {variable} 占位符)</summary>
    public string UserPrompt { get; init; }

    /// <summary>必需变量列表 — 每个 variable 必须出现在模板文本中</summary>
    public ImmutableArray<string> Variables { get; init; }

    /// <summary>
    /// 响应 token 预算上限（可选）。调用方构造 ModelRequest 时优先使用；
    /// 为 null 时调用方使用各自默认值。用于轻量模板（如 verify 变体）收紧预算。
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// 构造 PromptTemplate — fail-fast 校验。
    /// </summary>
    /// <param name="Capability">Capability key (非空)</param>
    /// <param name="SystemPrompt">System prompt 模板 (与 UserPrompt 至少一个非空)</param>
    /// <param name="UserPrompt">User prompt 模板 (与 SystemPrompt 至少一个非空)</param>
    /// <param name="Variables">必需变量列表 (每个必须出现在模板文本中)</param>
    /// <param name="MaxTokens">响应 token 预算上限（可选，null 表示调用方默认）</param>
    public PromptTemplate(
        string Capability,
        string SystemPrompt,
        string UserPrompt,
        ImmutableArray<string> Variables,
        int? MaxTokens = null)
    {
        // C-1: Capability 非空
        if (string.IsNullOrWhiteSpace(Capability))
            throw new DomainValidationException(nameof(Capability), Capability ?? "");

        // 至少一个 prompt 非空
        if (string.IsNullOrWhiteSpace(SystemPrompt) && string.IsNullOrWhiteSpace(UserPrompt))
            throw new DomainValidationException(
                $"{nameof(SystemPrompt)}+{nameof(UserPrompt)}", "(both empty)",
                $"Domain validation failed: both {nameof(SystemPrompt)} and {nameof(UserPrompt)} are empty. At least one must be non-empty.");

        // 声明变量必须出现在模板文本中 (D-2)
        foreach (var varName in Variables)
        {
            var placeholder = $"{{{varName}}}";
            if (!SystemPrompt.Contains(placeholder) && !UserPrompt.Contains(placeholder))
                throw new DomainValidationException(nameof(Variables), varName,
                    $"Declared variable '{varName}' not found in template text as '{placeholder}'.");
        }

        this.Capability = Capability;
        this.SystemPrompt = SystemPrompt ?? "";
        this.UserPrompt = UserPrompt ?? "";
        this.Variables = Variables;
        this.MaxTokens = MaxTokens;
    }

    /// <summary>
    /// 解析模板 — 遍历 Variables 列表, 逐个 string.Replace({var}, value) (D-1)。
    /// 缺失必需变量 → DomainValidationException。
    /// 额外变量不报错, 被忽略。
    /// 未声明 {foo} 保持原样不动 (对 JSON/code 示例安全)。
    /// 返回 ResolvedPrompt (D-3)。
    /// </summary>
    /// <param name="variables">变量字典 (key = variable name, value = replacement text)</param>
    /// <returns>ResolvedPrompt with resolved System + User</returns>
    public ResolvedPrompt Resolve(IReadOnlyDictionary<string, string> variables)
    {
        // 校验缺失变量
        var missing = Variables.Where(v => !variables.ContainsKey(v)).ToList();
        if (missing.Count > 0)
            throw new DomainValidationException(nameof(Resolve), string.Join(", ", missing),
                $"Missing required variables: {string.Join(", ", missing)}.");

        // 遍历声明变量, 逐个替换 (对齐 Python str.replace)
        var system = SystemPrompt;
        var user = UserPrompt;
        foreach (var varName in Variables)
        {
            var placeholder = $"{{{varName}}}";
            var value = variables[varName];
            system = system.Replace(placeholder, value);
            user = user.Replace(placeholder, value);
        }

        return new ResolvedPrompt(system, user);
    }
}
