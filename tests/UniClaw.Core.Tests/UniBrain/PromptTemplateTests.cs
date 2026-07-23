using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// PromptTemplate 单元测试 — task 2.1, 10 场景覆盖构造校验 + Resolve 替换。
/// 对齐 Python PromptTemplate (src/ai/prompts/manager.py)。
/// </summary>
public class PromptTemplateTests
{
    // ── Resolve: 正常替换 ──

    [Fact(DisplayName = "Resolve: 正常变量替换 → system/user 含替换值")]
    public void Resolve_NormalReplacement_SubstitutesPlaceholders()
    {
        var template = new PromptTemplate(
            Capability: "page_analysis",
            SystemPrompt: "You analyze pages.",
            UserPrompt: "Goal: {goal}\nType: {page_type}",
            Variables: ImmutableArray.Create("goal", "page_type"));

        var result = template.Resolve(new Dictionary<string, string>
        {
            ["goal"] = "Find settings",
            ["page_type"] = "home",
        });

        Assert.Equal("You analyze pages.", result.System);
        Assert.Equal("Goal: Find settings\nType: home", result.User);
    }

    // ── Resolve: 缺失变量 → DVE ──

    [Fact(DisplayName = "Resolve: 缺失必需变量 → 抛DomainValidationException含变量名")]
    public void Resolve_MissingVariable_ThrowsWithVariableName()
    {
        var template = new PromptTemplate(
            Capability: "decide_next_action",
            SystemPrompt: "You are an agent. Context: {ctx}",
            UserPrompt: "Goal: {goal}",
            Variables: ImmutableArray.Create("goal", "ctx"));

        var ex = Assert.Throws<DomainValidationException>(() =>
            template.Resolve(new Dictionary<string, string>
            {
                ["goal"] = "test",
            }));

        Assert.Equal("Resolve", ex.FieldName);
        Assert.Contains("ctx", ex.Message);
    }

    // ── Resolve: 额外变量忽略 ──

    [Fact(DisplayName = "Resolve: 额外变量(未声明) → 忽略不报错")]
    public void Resolve_ExtraVariables_Ignored()
    {
        var template = new PromptTemplate(
            Capability: "page_analysis",
            SystemPrompt: "",
            UserPrompt: "Goal: {goal}",
            Variables: ImmutableArray.Create("goal"));

        var result = template.Resolve(new Dictionary<string, string>
        {
            ["goal"] = "do something",
            ["extra"] = "ignored",
        });

        Assert.Equal("Goal: do something", result.User);
    }

    // ── Resolve: 无变量模板 ──

    [Fact(DisplayName = "Resolve: 无变量模板 → System/User 等于原值")]
    public void Resolve_NoVariables_ReturnsOriginalPrompts()
    {
        var template = new PromptTemplate(
            Capability: "static",
            SystemPrompt: "static prompt",
            UserPrompt: "static user",
            Variables: ImmutableArray<string>.Empty);

        var result = template.Resolve(new Dictionary<string, string>());

        Assert.Equal("static prompt", result.System);
        Assert.Equal("static user", result.User);
    }

    // ── Resolve: 变量在 system prompt ──

    [Fact(DisplayName = "Resolve: 变量出现在SystemPrompt → System含替换值")]
    public void Resolve_VariableInSystemPrompt_SubstitutesInSystem()
    {
        var template = new PromptTemplate(
            Capability: "role_play",
            SystemPrompt: "You are {role}.",
            UserPrompt: "Begin.",
            Variables: ImmutableArray.Create("role"));

        var result = template.Resolve(new Dictionary<string, string>
        {
            ["role"] = "a helpful assistant",
        });

        Assert.Equal("You are a helpful assistant.", result.System);
        Assert.Equal("Begin.", result.User);
    }

    // ── Resolve: 变量名含下划线/数字 ──

    [Fact(DisplayName = "Resolve: 变量名含下划线和数字(user_1) → 正常替换")]
    public void Resolve_UnderscoreAndNumberVariableName_Substitutes()
    {
        var template = new PromptTemplate(
            Capability: "multi_user",
            SystemPrompt: "",
            UserPrompt: "Hello {user_1}",
            Variables: ImmutableArray.Create("user_1"));

        var result = template.Resolve(new Dictionary<string, string>
        {
            ["user_1"] = "Alice",
        });

        Assert.Equal("Hello Alice", result.User);
    }

    // ── Resolve: 同一量重复出现 ──

    [Fact(DisplayName = "Resolve: 同一变量重复出现 → 两处均替换")]
    public void Resolve_RepeatedVariable_BothReplaced()
    {
        var template = new PromptTemplate(
            Capability: "repeat",
            SystemPrompt: "",
            UserPrompt: "{goal} and {goal}",
            Variables: ImmutableArray.Create("goal"));

        var result = template.Resolve(new Dictionary<string, string>
        {
            ["goal"] = "win",
        });

        Assert.Equal("win and win", result.User);
    }

    // ── 构造校验: Capability 空 ──

    [Fact(DisplayName = "构造: Capability空白 → 抛DVE FieldName=Capability")]
    public void Constructor_EmptyCapability_Throws()
    {
        var ex = Assert.Throws<DomainValidationException>(() => new PromptTemplate(
            Capability: "",
            SystemPrompt: "system",
            UserPrompt: "",
            Variables: ImmutableArray<string>.Empty));

        Assert.Equal("Capability", ex.FieldName);
    }

    // ── 构造校验: 两个 prompt 都空 ──

    [Fact(DisplayName = "构造: SystemPrompt+UserPrompt均空 → 抛DVE FieldName=SystemPrompt+UserPrompt")]
    public void Constructor_BothPromptsEmpty_Throws()
    {
        var ex = Assert.Throws<DomainValidationException>(() => new PromptTemplate(
            Capability: "cap",
            SystemPrompt: "",
            UserPrompt: "",
            Variables: ImmutableArray<string>.Empty));

        Assert.Equal("SystemPrompt+UserPrompt", ex.FieldName);
    }

    // ── 构造校验: 声明变量未出现在模板 ──

    [Fact(DisplayName = "构造: 声明变量未出现在模板文本 → 抛DVE FieldName=Variables含变量名")]
    public void Constructor_DeclaredVariableNotInTemplate_Throws()
    {
        var ex = Assert.Throws<DomainValidationException>(() => new PromptTemplate(
            Capability: "cap",
            SystemPrompt: "static prompt no placeholder",
            UserPrompt: "user prompt no placeholder",
            Variables: ImmutableArray.Create("missing_var")));

        Assert.Equal("Variables", ex.FieldName);
        Assert.Contains("missing_var", ex.Message);
    }
}