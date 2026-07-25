using System.Collections.Immutable;
using System.IO;
using UniClaw.Core.Domain;
using UniClaw.Core.Simulation;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.Simulation;

/// <summary>
/// MockModelProvider 单测 — 传输级 mock 三件套验证。
/// 覆盖 spec 3 场景 (预设命中 / 缺失预设 fail-fast / FromJson 加载) + ProviderId 默认。
/// </summary>
public class MockModelProviderTests
{
    private static MockModelFixture BuildFixture(string capability, MockModelEntry entry)
        => new(ImmutableDictionary<string, MockModelEntry>.Empty.Add(capability, entry));

    [Fact(DisplayName = "预设响应按 capability 返回 Content/Mode=text/tokens/ProviderId")]
    public async Task CompleteTextAsync_Preset_ReturnsByCapability()
    {
        var fixture = BuildFixture(
            "parse_instruction",
            new MockModelEntry("{\"category\":\"open_settings\"}", 12, 24, 5.0));
        var provider = new MockModelProvider(fixture);

        var response = await provider.CompleteTextAsync(
            new ModelRequest("prompt", Capability: "parse_instruction"));

        Assert.Equal("{\"category\":\"open_settings\"}", response.Content);
        Assert.Equal("text", response.Mode);
        Assert.Equal(12, response.InputTokens);
        Assert.Equal(24, response.OutputTokens);
        Assert.Equal(5.0, response.LatencyMs);
        Assert.True(response.Success);
        Assert.Null(response.ErrorMessage);
        Assert.Equal("mock", response.ProviderId);
    }

    [Fact(DisplayName = "缺失预设 → 抛 DomainValidationException")]
    public async Task CompleteTextAsync_MissingPreset_ThrowsDomainValidation()
    {
        var fixture = new MockModelFixture(ImmutableDictionary<string, MockModelEntry>.Empty);
        var provider = new MockModelProvider(fixture);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            provider.CompleteTextAsync(new ModelRequest("prompt", Capability: "unknown")));

        Assert.Equal(nameof(ModelRequest.Capability), ex.FieldName);
    }

    [Fact(DisplayName = "FromJson 加载 parse_instruction.mock.json 后 Resolve 非空")]
    public void FromJson_LoadsParseInstructionPreset()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "parse_instruction.mock.json");
        var json = File.ReadAllText(path);
        var fixture = MockModelFixture.FromJson(json);

        var entry = fixture.Resolve("parse_instruction");
        Assert.NotNull(entry);
        Assert.Equal(12, entry!.InputTokens);
        Assert.Equal(24, entry.OutputTokens);
        Assert.True(entry.Success);
    }

    [Fact(DisplayName = "ProviderId 默认为 mock")]
    public void ProviderId_DefaultsToMock()
    {
        var fixture = new MockModelFixture(ImmutableDictionary<string, MockModelEntry>.Empty);
        var provider = new MockModelProvider(fixture);

        Assert.Equal("mock", provider.ProviderId);
    }
}
