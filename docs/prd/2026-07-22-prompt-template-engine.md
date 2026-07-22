# Prompt Template Engine — 设计文档

> 日期: 2026-07-22
> 状态: 提案
> 对齐: Python `src/ai/prompts/manager.py`

## 动机

`IModelProvider` 目前是纯传输层（call + retry + timeout），`ModelRequest` 只有裸 `Prompt` + `SystemPrompt` 字符串。子接口实现（ClaudePageAnalyzer 等）需要自己管理 prompt 模板和变量注入，导致：

- Prompt 散落在各实现类中，无法统一管理
- 缺乏变量校验（哪些变量是必需的？传了非法变量？）
- 无法做 prompt 版本控制 / A/B 测试
- 与 Python `PromptManager` 不对齐

## Python 参考

Python `PromptManager`（`src/ai/prompts/manager.py`）：

```python
@dataclass
class PromptTemplate:
    capability: str          # e.g. "analyze_visual"
    version: str             # e.g. "latest", "v1"
    system_prompt: str       # system prompt 模板
    user_template: str       # user prompt 模板，含 {variable} 占位符
    variables: List[str]     # 必需变量列表
    metadata: Dict[str, Any]

    def format(self, **kwargs) -> str:
        # 缺失变量 → ValueError
        # {variable} → str(kwargs[variable])
```

存储格式：Markdown + YAML front matter（`src/ai/prompts/*.md`）:

```markdown
---
capability: analyze_visual
version: 1.0
variables: [image_description, context_info]
system: |
  You are an expert at analyzing...
user: |
  Analyze ... {image_description}
  Context: {context_info}
---
```

`PromptManager` 负责：加载所有 `.md` → 解析 YAML front matter → 按 capability key 检索 → 变量注入 → 版本选择 → hot reload。

## C# 设计

### 对齐范围

| 特性 | Python | C# Phase 1 | 理由 |
|------|--------|-------------|------|
| 变量占位符 `{var}` | ✅ | ✅ | 核心能力 |
| 缺失变量校验 | ✅ | ✅ | fail-fast |
| Capability key 检索 | ✅ | ✅ | 按职责分组 |
| Markdown/YAML 文件 | ✅ | ❌ defer | 先做程序化注册 |
| 版本控制 | ✅ | ❌ defer | 本期不需要 |
| Hot reload | ✅ | ❌ defer | 本期不需要 |
| `{{#if}}` 条件块 | ❌ | ❌ | YAGNI |

### 新增类型

#### 1. `PromptTemplate.cs`

```csharp
namespace UniClaw.Core.UniBrain;

/// <summary>
/// Prompt 模板 — capability + system/user 模板 + 变量占位符。
/// 模板中使用 {variable_name} 占位符（单花括号，对齐 Python）。
/// 构造期 fail-fast 校验。
/// </summary>
public sealed record class PromptTemplate
{
    public string Capability { get; init; }
    public string SystemPrompt { get; init; }
    public string UserPrompt { get; init; }
    public ImmutableArray<string> Variables { get; init; }

    /// <summary>构造模板 — Capability 非空, SystemPrompt/UserPrompt 至少一个非空</summary>
    public PromptTemplate(
        string Capability,
        string SystemPrompt,
        string UserPrompt,
        ImmutableArray<string> Variables);

    /// <summary>解析模板 — {variable} → 对应值。缺失变量 → DomainValidationException。</summary>
    public (string System, string User) Resolve(
        IReadOnlyDictionary<string, string> variables);
}
```

**设计要点**：
- **单花括号 `{var}`**：对齐 Python，方便 prompt 工程师跨语言复用模板
- **`Resolve` 返回 tuple**：`(system, user)` 解构后直接传给 `ModelRequest`
- **缺失变量 → `DomainValidationException`**：对齐项目 fail-fast 惯例
- **额外变量不报错**：只检查必需变量是否缺失，不拒绝多余的（方便调用方传大 context）

#### 2. `IPromptLibrary.cs`

```csharp
namespace UniClaw.Core.UniBrain;

/// <summary>
/// IPromptLibrary — 按 capability key 检索 Prompt 模板。
/// 子接口实现注入此接口获取 prompt，再调用 IModelProvider。
/// </summary>
public interface IPromptLibrary
{
    PromptTemplate? GetTemplate(string capability);
    IReadOnlyList<string> GetCapabilities();
}
```

**设计要点**：
- **极简**：只有 2 个方法
- **返回 `PromptTemplate?`**：capability 不存在返回 null（不抛异常），调用方自行决定 fallback
- **`GetCapabilities()`**：支持调试/诊断（列出所有已注册 capability）

#### 3. `PromptLibrary.cs`

```csharp
namespace UniClaw.Core.UniBrain;

/// <summary>
/// PromptLibrary — IPromptLibrary 默认实现（内存字典）。
/// 模板在构造期注册，无文件 I/O。
/// </summary>
public sealed class PromptLibrary : IPromptLibrary
{
    private readonly ImmutableDictionary<string, PromptTemplate> _templates;

    public PromptLibrary(ImmutableDictionary<string, PromptTemplate> templates);
    public PromptLibrary(params PromptTemplate[] templates);  // 便利构造器

    public PromptTemplate? GetTemplate(string capability);
    public IReadOnlyList<string> GetCapabilities();
}
```

**设计要点**：
- **不可变**：构造后模板集不变（对齐项目 ImmutableArray 惯例）
- **无内置业务模板**：业务 prompt 由 Host 项目（ClaudeProvider/DeepSeekProvider）注册，Core 层不硬编码

### 集成示例

```csharp
// Host 项目注册模板
var templates = new PromptLibrary(
    new PromptTemplate(
        Capability: "page_analysis",
        SystemPrompt: "You are an expert at analyzing mobile app screenshots.",
        UserPrompt: "Goal: {goal}\nPage type: {page_type}\nAnalyze this screen.",
        Variables: ["goal", "page_type"]
    ),
    new PromptTemplate(
        Capability: "next_action",
        SystemPrompt: "You are a traversal decision engine.",
        UserPrompt: "Goal: {goal}\nCurrent page: {page_analysis}\nWhat should I do next?",
        Variables: ["goal", "page_analysis"]
    )
);

// 子接口实现使用
var template = _promptLibrary.GetTemplate("page_analysis");
var (system, user) = template.Resolve(new Dictionary<string, string> {
    ["goal"] = "Find Dark mode setting",
    ["page_type"] = "settings"
});
var request = new ModelRequest(Prompt: user, SystemPrompt: system);
var response = await _modelProvider.CompleteVisionAsync(request, imageData, ct);
```

### 文件清单

| 文件 | 目的 |
|------|------|
| `src/UniClaw.Core/UniBrain/PromptTemplate.cs` | 模板类 + Resolve 方法 |
| `src/UniClaw.Core/UniBrain/IPromptLibrary.cs` | 检索接口 |
| `src/UniClaw.Core/UniBrain/PromptLibrary.cs` | 默认实现（内存字典） |
| `tests/UniClaw.Core.Tests/UniBrain/PromptTemplateTests.cs` | 单元测试（8 场景） |

### 测试场景

1. 正常替换 — 所有变量被正确替换
2. 缺失变量 → `DomainValidationException`
3. 额外变量 — 不报错，被忽略
4. 无变量模板 — `Variables=Empty`，原样返回
5. 变量在 system prompt 中 — 同样替换
6. 变量名含下划线/数字 — `{page_type}`, `{item_count_2}` 正确匹配
7. 重复变量 — 所有出现都被替换
8. 空 capability → 构造期 `DomainValidationException`

### 不做什么（显式 defer）

- ❌ Markdown/YAML 文件 I/O（→ Phase 3-B）
- ❌ Hot reload（→ Phase 3-B）
- ❌ 版本控制（→ 需要时加 `Version` 字段）
- ❌ 条件块 `{#if}` / 循环 `{#each}` 语法
- ❌ 内置默认业务 prompt（由 Host 项目提供）
- ❌ `IPromptLibrary` 放到 `IUniBrain` facade 上（prompt 管理是子接口实现内部关注点，不暴露给引擎）

## 验证

```bash
dotnet build src/UniClaw.Core.sln  # 0 errors
dotnet test src/UniClaw.Core.sln   # 849 + 8 = 857 测试通过
```
