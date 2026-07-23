# Prompt Template Engine — Design Spec

> Date: 2026-07-23
> Status: Approved
> Aligns: Python `src/ai/prompts/manager.py` (PromptTemplate + PromptManager)
> PRD ref: `docs/prd/2026-07-22-prompt-template-engine.md`

## Motivation

`IModelProvider` is pure transport (call + retry + timeout). `ModelRequest` only has raw `Prompt` + `SystemPrompt` strings. Sub-interface implementations (ClaudePageAnalyzer etc.) must manage prompt templates and variable injection themselves, leading to:

- Prompts scattered across implementations, no unified management
- No variable validation (which are required? illegal variables passed?)
- No prompt versioning / A-B testing capability
- Misalignment with Python `PromptManager`

## Design Decisions

### D-1: Variable replacement — declared-variable iteration

**Choice**: Iterate `Variables` list, do `string.Replace("{var_name}", value)` for each declared variable.

**Rationale**: Mirrors Python's `str.replace` approach exactly. Undeclared `{foo}` in template text stays untouched — safe for JSON/code examples with literal braces. Extra input variables are silently ignored (only check required variables are present). No regex needed; the declared list is the authoritative source.

**Rejected alternative**: Regex `\{(\w+)\}` scan of all `{identifier}` patterns — would reject templates containing literal brace content like JSON examples.

### D-2: Constructor validation — declared variables must appear in template text

**Choice**: At construction, verify every name in `Variables` appears at least once in `SystemPrompt` or `UserPrompt` as `{var_name}`.

**Rationale**: Catches typos early (declaring `"goal"` but writing `{gola}`). Fail-fast at construction is the project convention (DomainValidationException). Python has a separate `validate_prompt()` method; C# folds this into construction for zero-escape validation.

### D-3: ResolvedPrompt named return type

**Choice**: `Resolve()` returns `ResolvedPrompt` (sealed record class with `System` + `User` fields) instead of raw `(string, string)` ValueTuple.

**Rationale**: Named types give field names (`System`, `User`) for IDE discoverability and self-documenting API. Aligns with project convention of sealed record class for DTOs. Raw ValueTuple requires positional memory (item1=system, item2=user) with no semantic clarity.

### D-4: IPromptLibrary includes ValidateCapability

**Choice**: `IPromptLibrary` has 3 methods: `GetTemplate`, `GetCapabilities`, `ValidateCapability`.

**Rationale**: `ValidateCapability` returns `bool` (no exception) — diagnostic method for checking capability existence without triggering the hot path. Aligns with Python's `validate_prompt()`. `GetTemplate` returns `PromptTemplate?` (null = not found), caller decides fallback.

## Types

### 1. PromptTemplate

```csharp
namespace UniClaw.Core.UniBrain;

/// <summary>
/// Prompt 模板 — capability + system/user 模板 + 变量占位符。
/// 模板中使用 {variable_name} 占位符（单花括号，对齐 Python）。
/// 构造期 fail-fast 校验：Capability 非空, 至少一个 prompt 非空,
/// 声明变量必须出现在模板文本中。
/// 对齐 Python PromptTemplate (src/ai/prompts/manager.py)。
/// </summary>
public sealed record class PromptTemplate
{
    public string Capability { get; init; }
    public string SystemPrompt { get; init; }
    public string UserPrompt { get; init; }
    public ImmutableArray<string> Variables { get; init; }

    public PromptTemplate(
        string Capability,
        string SystemPrompt,
        string UserPrompt,
        ImmutableArray<string> Variables)
    {
        // C-1: Capability 非空
        if (string.IsNullOrWhiteSpace(Capability))
            throw new DomainValidationException("PromptTemplate.Capability", Capability ?? "");

        // 至少一个 prompt 非空
        if (string.IsNullOrWhiteSpace(SystemPrompt) && string.IsNullOrWhiteSpace(UserPrompt))
            throw new DomainValidationException("PromptTemplate.SystemPrompt+UserPrompt", "(both empty)");

        // 声明变量必须出现在模板文本中 (D-2)
        foreach (var varName in Variables)
        {
            var placeholder = $"{{{varName}}}";
            if (!SystemPrompt.Contains(placeholder) && !UserPrompt.Contains(placeholder))
                throw new DomainValidationException("PromptTemplate.Variables",
                    $"Declared variable '{varName}' not found in template text as '{placeholder}'");
        }

        this.Capability = Capability;
        this.SystemPrompt = SystemPrompt ?? "";
        this.UserPrompt = UserPrompt ?? "";
        this.Variables = Variables;
    }

    /// <summary>
    /// 解析模板 — 遍历 Variables 列表, 逐个 string.Replace({var}, value) (D-1)。
    /// 缺失必需变量 → DomainValidationException。
    /// 额外变量不报错, 被忽略。
    /// 未声明 {foo} 保持原样不动 (对 JSON/code 示例安全)。
    /// 返回 ResolvedPrompt (D-3)。
    /// </summary>
    public ResolvedPrompt Resolve(IReadOnlyDictionary<string, string> variables)
    {
        // 校验缺失变量
        var missing = Variables.Where(v => !variables.ContainsKey(v)).ToList();
        if (missing.Count > 0)
            throw new DomainValidationException("PromptTemplate.Resolve",
                $"Missing required variables: {string.Join(", ", missing)}");

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
```

**Design notes**:
- `sealed record class` — project convention
- Single braces `{var}` — aligns Python, no collision with C# string interpolation (templates are plain strings)
- Constructor double validation (D-2): capability + variable presence
- `Resolve` returns `ResolvedPrompt` (D-3): named type, not raw tuple
- Missing variables → `DomainValidationException` listing all missing names
- Extra variables silently ignored
- Undeclared `{foo}` untouched (safe for JSON examples)

### 2. ResolvedPrompt

```csharp
namespace UniClaw.Core.UniBrain;

/// <summary>
/// ResolvedPrompt — PromptTemplate.Resolve() 的返回类型 (D-3)。
/// 解析后的 system + user prompt，可直接赋值给 ModelRequest。
/// </summary>
public sealed record class ResolvedPrompt(
    string System,
    string User);
```

Minimal — 2 positional fields, no validation (Resolve guarantees non-empty output).

### 3. IPromptLibrary

```csharp
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
```

### 4. PromptLibrary

```csharp
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

    /// <summary>便利构造器 — 从模板数组构建字典（key = Capability）。</summary>
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
```

**Design notes**:
- Immutable after construction (ImmutableDictionary)
- Duplicate capability → `ArgumentException` (fail-fast)
- `sealed class` (not record): holds builder→immutable conversion logic, no value semantics needed
- No built-in business prompts: Host project registers them

## Integration Pattern

```csharp
// ── Host 项目注册模板 ──
var library = new PromptLibrary(
    new PromptTemplate(
        Capability: "page_analysis",
        SystemPrompt: "You are an expert at analyzing mobile app screenshots.",
        UserPrompt: "Goal: {goal}\nPage type: {page_type}\nAnalyze this screen.",
        Variables: ImmutableArray.Create("goal", "page_type")
    ),
    new PromptTemplate(
        Capability: "next_action",
        SystemPrompt: "You are a traversal decision engine.",
        UserPrompt: "Goal: {goal}\nCurrent page: {page_analysis}\nWhat should I do next?",
        Variables: ImmutableArray.Create("goal", "page_analysis")
    )
);

// ── 子接口实现使用 ──
public sealed class ClaudePageAnalyzer : IPageAnalyzer
{
    private readonly IModelProvider _model;
    private readonly IPromptLibrary _prompts;

    public async Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct)
    {
        var template = _prompts.GetTemplate("page_analysis");
        if (template is null) return null;  // capability 不存在 → null fallback

        var resolved = template.Resolve(new Dictionary<string, string>
        {
            ["goal"] = "Find Dark mode setting",
            ["page_type"] = "settings"
        });

        var request = new ModelRequest(
            Prompt: resolved.User,
            SystemPrompt: resolved.System);

        var response = await _model.CompleteVisionAsync(request, imageData, ct);
        // → parse response → PageAnalysis
    }
}
```

**DI chain**: `Host → DI registers PromptLibrary + IModelProvider → sub-interface implementations inject IPromptLibrary + IModelProvider`

## Python Alignment Map

| Python | C# | Phase | Note |
|--------|-----|-------|------|
| `PromptTemplate.format(**kwargs) → str` | `PromptTemplate.Resolve(dict) → ResolvedPrompt` | 1 | Returns named type, not concatenated string |
| `PromptTemplate.capability` | `PromptTemplate.Capability` | 1 | |
| `PromptTemplate.system_prompt` | `PromptTemplate.SystemPrompt` | 1 | |
| `PromptTemplate.user_template` | `PromptTemplate.UserPrompt` | 1 | |
| `PromptTemplate.variables` | `PromptTemplate.Variables` | 1 | ImmutableArray vs List |
| `PromptTemplate.version` | — | defer | Dormant in Python |
| `PromptTemplate.metadata` | — | defer | Not needed in Phase 1 |
| `PromptManager.get_prompt(cap)` | `IPromptLibrary.GetTemplate(cap)` | 1 | Returns null vs raises ValueError |
| `PromptManager.list_capabilities()` | `IPromptLibrary.GetCapabilities()` | 1 | |
| `PromptManager.validate_prompt(cap)` | `IPromptLibrary.ValidateCapability(cap)` | 1 | Returns bool, no exception |
| `PromptManager.inject_variables()` | — | omit | Resolve() is direct, no convenience wrapper needed |
| `PromptManager.list_versions()` | — | defer | Version system dormant |
| `PromptManager.reload_prompts()` | — | defer | Phase 3-B (file-based loading) |
| `PromptManager.get_all_metadata()` | — | defer | Phase 3-B |
| YAML front matter `.md` files | — | defer | Phase 3-B |
| `PromptValidator` class | Constructor validation | 1 | Folded into PromptTemplate ctor (D-2) |

## File List

| File | Purpose |
|------|---------|
| `src/UniClaw.Core/UniBrain/PromptTemplate.cs` | Template class + Resolve method |
| `src/UniClaw.Core/UniBrain/ResolvedPrompt.cs` | Resolve return type |
| `src/UniClaw.Core/UniBrain/IPromptLibrary.cs` | Retrieval interface |
| `src/UniClaw.Core/UniBrain/PromptLibrary.cs` | Default implementation (ImmutableDictionary) |
| `tests/UniClaw.Core.Tests/UniBrain/PromptTemplateTests.cs` | Template tests (10 scenarios) |
| `tests/UniClaw.Core.Tests/UniBrain/PromptLibraryTests.cs` | Library tests (5 scenarios) |

## Test Scenarios

### PromptTemplate (10 scenarios)

1. Normal replacement — all variables correctly replaced in user prompt
2. Missing variable → `DomainValidationException` (lists missing names)
3. Extra variables — no error, silently ignored
4. No-variables template — `Variables=Empty`, returned unchanged
5. Variable in system prompt — replaced in both system and user
6. Variable names with underscore/number — `{page_type}`, `{item_count_2}` correctly matched
7. Repeated variable — all occurrences replaced
8. Empty capability → `DomainValidationException` at construction
9. Both prompts empty → `DomainValidationException` at construction
10. Declared variable not in template text → `DomainValidationException` at construction

### PromptLibrary (5 scenarios)

1. GetTemplate found — returns matching PromptTemplate
2. GetTemplate unknown → null
3. GetCapabilities — returns all registered keys
4. ValidateCapability exists → true
5. ValidateCapability missing → false

## Explicit Defer

- ❌ Markdown/YAML file I/O (→ Phase 3-B)
- ❌ Hot reload (→ Phase 3-B)
- ❌ Version control (→ add `Version` field when needed)
- ❌ Conditional blocks `{#if}` / loop `{#each}` syntax
- ❌ Built-in default business prompts (Host project provides)
- ❌ `IPromptLibrary` on `IUniBrain` facade (prompt management is sub-interface internal concern)

## Verification

```bash
dotnet build src/UniClaw.Core.sln  # 0 errors
dotnet test src/UniClaw.Core.sln   # 849 existing + 15 new = 864 tests pass
```
