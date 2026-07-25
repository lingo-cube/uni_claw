using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Core.Domain;

namespace UniClaw.Core.Simulation;

/// <summary>
/// MockModelFixture — MockModelProvider 的预设响应表 (capability → MockModelEntry)。
/// 仿 StateFixture: sealed record + 自定义构造器 + FromJson + internal DTO。
/// 构造期对 null responses 做 fail-fast 校验 (StateFixture 无该校验，此处为新增)。
/// </summary>
public sealed record class MockModelFixture
{
    /// <summary>capability → 预设响应映射。不可变。</summary>
    public ImmutableDictionary<string, MockModelEntry> Responses { get; init; }

    /// <summary>
    /// 构造器: responses 为 null → DomainValidationException fail-fast。
    /// </summary>
    public MockModelFixture(ImmutableDictionary<string, MockModelEntry> responses)
    {
        Responses = responses
            ?? throw new DomainValidationException(nameof(Responses), responses);
    }

    /// <summary>
    /// 按 capability 解析预设响应，未匹配返回 null。
    /// </summary>
    public MockModelEntry? Resolve(string capability)
        => Responses.TryGetValue(capability, out var entry) ? entry : null;

    // ── JSON 反序列化 ──────────────────────────────────────────────────

    /// <summary>
    /// 从 JSON 字符串加载 MockModelFixture。
    /// 使用 DomainJsonOptions.Default (camelCase + enum-as-string) (D4: 不本地设 PropertyNameCaseInsensitive)。
    /// 解析失败抛 InvalidOperationException (非构造期校验)。
    /// </summary>
    public static MockModelFixture FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<MockModelFixtureDto>(json, DomainJsonOptions.Default);
        if (dto is null)
            throw new InvalidOperationException("Failed to deserialize MockModelFixture from JSON.");

        var responses = ImmutableDictionary.CreateRange(
            dto.Responses.Select(kvp => new KeyValuePair<string, MockModelEntry>(
                kvp.Key,
                new MockModelEntry(
                    kvp.Value.Content,
                    kvp.Value.InputTokens,
                    kvp.Value.OutputTokens,
                    kvp.Value.LatencyMs,
                    kvp.Value.Success,
                    kvp.Value.ErrorMessage))));

        return new MockModelFixture(responses);
    }

    /// <summary>仅用于 JSON 反序列化的内部 DTO。</summary>
    internal sealed class MockModelFixtureDto
    {
        public Dictionary<string, MockModelEntryDto> Responses { get; init; } = new();
    }

    internal sealed class MockModelEntryDto
    {
        public string Content { get; init; } = "";
        public int InputTokens { get; init; }
        public int OutputTokens { get; init; }
        public double LatencyMs { get; init; }
        public bool Success { get; init; } = true;
        public string? ErrorMessage { get; init; }
    }
}
