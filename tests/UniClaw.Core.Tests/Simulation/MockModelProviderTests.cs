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

    [Fact(DisplayName = "CompleteVisionAsync 按预设返回 Content/Mode=vision/tokens/ProviderId")]
    public async Task CompleteVisionAsync_ReturnsPreset_WithVisionMode()
    {
        var fixture = BuildFixture(
            "analyze_visual",
            new MockModelEntry("{\"category\":\"open_settings\"}", 12, 24, 5.0));
        var provider = new MockModelProvider(fixture);

        var response = await provider.CompleteVisionAsync(
            new ModelRequest("prompt", Capability: "analyze_visual"),
            Array.Empty<byte>());

        Assert.Equal("{\"category\":\"open_settings\"}", response.Content);
        Assert.Equal("vision", response.Mode);
        Assert.Equal(12, response.InputTokens);
        Assert.Equal(24, response.OutputTokens);
        Assert.Equal(5.0, response.LatencyMs);
        Assert.True(response.Success);
        Assert.Null(response.ErrorMessage);
        Assert.Equal("mock", response.ProviderId);
    }

    [Fact(DisplayName = "CompleteMultimodalAsync 按预设返回 Content/Mode=multimodal/tokens/ProviderId")]
    public async Task CompleteMultimodalAsync_ReturnsPreset_WithMultimodalMode()
    {
        var fixture = BuildFixture(
            "analyze_visual",
            new MockModelEntry("{\"category\":\"open_settings\"}", 12, 24, 5.0));
        var provider = new MockModelProvider(fixture);

        var response = await provider.CompleteMultimodalAsync(
            new ModelRequest("prompt", Capability: "analyze_visual"),
            Array.Empty<byte>());

        Assert.Equal("{\"category\":\"open_settings\"}", response.Content);
        Assert.Equal("multimodal", response.Mode);
        Assert.Equal(12, response.InputTokens);
        Assert.Equal(24, response.OutputTokens);
        Assert.Equal(5.0, response.LatencyMs);
        Assert.True(response.Success);
        Assert.Null(response.ErrorMessage);
        Assert.Equal("mock", response.ProviderId);
    }

    [Fact(DisplayName = "CompleteVisionAsync 缺失预设 → 抛 DomainValidationException (非 NotImplementedException)")]
    public async Task CompleteVisionAsync_MissingPreset_ThrowsDomainValidation()
    {
        var fixture = new MockModelFixture(ImmutableDictionary<string, MockModelEntry>.Empty);
        var provider = new MockModelProvider(fixture);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            provider.CompleteVisionAsync(
                new ModelRequest("prompt", Capability: "foo"),
                Array.Empty<byte>()));

        Assert.Equal(nameof(ModelRequest.Capability), ex.FieldName);
    }

    [Fact(DisplayName = "CompleteMultimodalAsync 缺失预设 → 抛 DomainValidationException (非 NotImplementedException)")]
    public async Task CompleteMultimodalAsync_MissingPreset_ThrowsDomainValidation()
    {
        var fixture = new MockModelFixture(ImmutableDictionary<string, MockModelEntry>.Empty);
        var provider = new MockModelProvider(fixture);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            provider.CompleteMultimodalAsync(
                new ModelRequest("prompt", Capability: "foo"),
                Array.Empty<byte>()));

        Assert.Equal(nameof(ModelRequest.Capability), ex.FieldName);
    }

    [Fact(DisplayName = "同一 capability 预设同时服务 text/vision/multimodal 三种模式 (模式无关 fixture 设计)")]
    public async Task AllThreeModes_SharedFixture_SameCapability()
    {
        var fixture = BuildFixture(
            "analyze_visual",
            new MockModelEntry("{\"category\":\"open_settings\"}", 12, 24, 5.0));
        var provider = new MockModelProvider(fixture);
        var request = new ModelRequest("prompt", Capability: "analyze_visual");

        var text = await provider.CompleteTextAsync(request);
        var vision = await provider.CompleteVisionAsync(request, new byte[] { 1 });
        var multimodal = await provider.CompleteMultimodalAsync(request, new byte[] { 1 });

        Assert.Equal("{\"category\":\"open_settings\"}", text.Content);
        Assert.Equal("{\"category\":\"open_settings\"}", vision.Content);
        Assert.Equal("{\"category\":\"open_settings\"}", multimodal.Content);
        Assert.Equal("text", text.Mode);
        Assert.Equal("vision", vision.Mode);
        Assert.Equal("multimodal", multimodal.Mode);
        Assert.Equal("mock", text.ProviderId);
        Assert.Equal("mock", vision.ProviderId);
        Assert.Equal("mock", multimodal.ProviderId);
    }
}
