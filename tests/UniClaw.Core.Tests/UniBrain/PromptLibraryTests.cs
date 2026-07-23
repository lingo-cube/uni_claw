using System.Collections.Immutable;
using Xunit;
using UniClaw.Core.UniBrain;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// PromptLibrary 单元测试 — task 2.2: 5 场景 + 1 重复 capability ArgumentException。
/// 对齐 OpenSpec change prompt-template-engine。
/// </summary>
public class PromptLibraryTests
{
    private static PromptTemplate MakeTemplate(string capability, string systemPrompt, string userPrompt, string[]? variables = null)
    {
        var vars = variables is null ? ImmutableArray<string>.Empty : ImmutableArray.Create(variables);
        return new PromptTemplate(capability, systemPrompt, userPrompt, vars);
    }

    private static PromptLibrary BuildLibrary(params PromptTemplate[] templates)
        => new(templates);

    [Fact(DisplayName = "GetTemplate: 库含 page_analysis → 返回对应模板")]
    public void GetTemplate_Found_ReturnsTemplate()
    {
        var template = MakeTemplate("page_analysis", "Analyze {goal}", "", new[] { "goal" });
        var lib = BuildLibrary(template);

        var result = lib.GetTemplate("page_analysis");

        Assert.NotNull(result);
        Assert.Equal("page_analysis", result!.Capability);
    }

    [Fact(DisplayName = "GetTemplate: 不含 unknown → 返回 null")]
    public void GetTemplate_Unknown_ReturnsNull()
    {
        var lib = BuildLibrary(MakeTemplate("page_analysis", "Analyze {goal}", "", new[] { "goal" }));

        var result = lib.GetTemplate("unknown");

        Assert.Null(result);
    }

    [Fact(DisplayName = "GetCapabilities: 库含 a,b → 返回包含两者的列表")]
    public void GetCapabilities_ReturnsAllCapabilities()
    {
        var lib = BuildLibrary(
            MakeTemplate("a", "Sys {x}", "", new[] { "x" }),
            MakeTemplate("b", "Sys {y}", "", new[] { "y" })
        );

        var caps = lib.GetCapabilities();

        Assert.Contains("a", caps);
        Assert.Contains("b", caps);
        Assert.Equal(2, caps.Count);
    }

    [Fact(DisplayName = "ValidateCapability: 含 page_analysis → true")]
    public void ValidateCapability_Exists_ReturnsTrue()
    {
        var lib = BuildLibrary(MakeTemplate("page_analysis", "Analyze {goal}", "", new[] { "goal" }));

        Assert.True(lib.ValidateCapability("page_analysis"));
    }

    [Fact(DisplayName = "ValidateCapability: 不含 unknown → false")]
    public void ValidateCapability_NotExists_ReturnsFalse()
    {
        var lib = BuildLibrary(MakeTemplate("page_analysis", "Analyze {goal}", "", new[] { "goal" }));

        Assert.False(lib.ValidateCapability("unknown"));
    }

    [Fact(DisplayName = "params 构造器: 重复 capability → ArgumentException")]
    public void Constructor_DuplicateCapability_ThrowsArgumentException()
    {
        var t1 = MakeTemplate("page_analysis", "Sys {goal}", "", new[] { "goal" });
        var t2 = MakeTemplate("page_analysis", "Sys {goal} v2", "", new[] { "goal" });

        Assert.Throws<ArgumentException>(() => new PromptLibrary(t1, t2));
    }
}