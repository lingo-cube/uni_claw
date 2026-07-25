using System.Collections.Immutable;
using UniClaw.Core.Domain;
using UniClaw.Core.Observability;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// ModelRouter 单元测试 — task 4.2: 5 scenario 对齐 spec。
/// 对齐 OpenSpec change unibrain-modelprovider-vertical-slice。
/// </summary>
public class ModelRouterTests
{
    private static ImmutableDictionary<string, string> Routing(params (string capability, string providerId)[] entries)
        => entries.ToImmutableDictionary(e => e.capability, e => e.providerId);

    private static ImmutableDictionary<string, IModelProvider> Providers(params (string id, IModelProvider provider)[] entries)
        => entries.ToImmutableDictionary(e => e.id, e => e.provider);

    /// <summary>Spy provider: 记录 CompleteTextAsync 调用次数，返回固定 ModelResponse。</summary>
    private sealed class SpyModelProvider : IModelProvider
    {
        private int _callCount;

        public SpyModelProvider(string providerId)
        {
            ProviderId = providerId;
        }

        public string ProviderId { get; }

        public int CallCount => _callCount;

        public Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _callCount);
            return Task.FromResult(new ModelResponse("ok", ProviderId, "text", 1, 1, 1.0));
        }

        public Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    [Fact(DisplayName = "Resolve: capability 精确命中 → 返回对应已观测 provider")]
    public void Resolve_CapabilityHit_ReturnsRoutedProvider()
    {
        var spy = new SpyModelProvider("deepseek");
        var router = new ModelRouter(
            Routing(("parse_instruction", "deepseek")),
            Providers(("deepseek", spy)),
            new InMemoryTraceRecorder(new InMemoryTraceStorage()),
            "deepseek");

        var resolved = router.Resolve("parse_instruction");

        Assert.NotNull(resolved);
        Assert.Equal("deepseek", resolved.ProviderId);
    }

    [Fact(DisplayName = "Resolve: capability 未命中但 default 合法 → 回落 default")]
    public void Resolve_CapabilityMiss_FallsBackToDefault()
    {
        var spy = new SpyModelProvider("mock");
        var router = new ModelRouter(
            Routing(),
            Providers(("mock", spy)),
            new InMemoryTraceRecorder(new InMemoryTraceStorage()),
            "mock");

        var resolved = router.Resolve("unknown");

        Assert.NotNull(resolved);
        Assert.Equal("mock", resolved.ProviderId);
    }

    [Fact(DisplayName = "Resolve: capability 未命中且 default 不存在 → DomainValidationException")]
    public void Resolve_CapabilityMissAndUnknownDefault_Throws()
    {
        var spy = new SpyModelProvider("mock");
        var router = new ModelRouter(
            Routing(),
            Providers(("mock", spy)),
            new InMemoryTraceRecorder(new InMemoryTraceStorage()),
            "nonexistent");

        Assert.Throws<DomainValidationException>(() => router.Resolve("unknown"));
    }

    [Fact(DisplayName = "ctor: capabilityRouting 引用未知 provider → 构造期 DomainValidationException")]
    public void Ctor_RoutingReferencesUnknownProvider_Throws()
    {
        var spy = new SpyModelProvider("mock");

        Assert.Throws<DomainValidationException>(() => new ModelRouter(
            Routing(("x", "foo")),
            Providers(("mock", spy)),
            new InMemoryTraceRecorder(new InMemoryTraceStorage()),
            "mock"));
    }

    [Fact(DisplayName = "Resolve 返回的 provider 必被观测: 调用后 spy 被调一次且产生 AICallRecord")]
    public async Task Resolve_ReturnsObservedProvider_RecordsAICall()
    {
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);
        var spy = new SpyModelProvider("mock");
        var router = new ModelRouter(
            Routing(("parse_instruction", "mock")),
            Providers(("mock", spy)),
            recorder,
            "mock");

        var resolved = router.Resolve("parse_instruction");
        await resolved.CompleteTextAsync(new ModelRequest("p", Capability: "parse_instruction"));

        Assert.Equal(1, spy.CallCount);
        Assert.NotEmpty(storage.GetAICalls());
    }
}
