using System.Collections.Immutable;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// PromptLibrary — IPromptLibrary 默认实现（内存 ImmutableDictionary）。
/// 模板在构造期注册，构造后不可变。无文件 I/O。
/// </summary>
public sealed class PromptLibrary : IPromptLibrary
{
    private readonly ImmutableDictionary<string, PromptTemplate> _templates;

    /// <summary>从 ImmutableDictionary 构造（DI 容器用）。</summary>
    public PromptLibrary(ImmutableDictionary<string, PromptTemplate> templates)
    {
        _templates = templates;
    }

    /// <summary>便利构造器 — 从模板数组构建字典（key = Capability）。重复 capability → ArgumentException。</summary>
    public PromptLibrary(params PromptTemplate[] templates)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, PromptTemplate>();
        foreach (var t in templates)
            builder.Add(t.Capability, t);  // 重复 capability → ArgumentException (fail-fast)
        _templates = builder.ToImmutable();
    }

    /// <inheritdoc/>
    public PromptTemplate? GetTemplate(string capability)
        => _templates.GetValueOrDefault(capability);

    /// <inheritdoc/>
    public IReadOnlyList<string> GetCapabilities()
        => _templates.Keys.ToList();

    /// <inheritdoc/>
    public bool ValidateCapability(string capability)
        => _templates.ContainsKey(capability);
}
