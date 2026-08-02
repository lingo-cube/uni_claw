using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using UniClaw.ClaudeProvider;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// IntentExtractor 单元测试 — AI 意图推理的完整生命周期。
/// 使用 StubHttpHandler 模拟 DeepSeek API 响应；不依赖真实网络。
/// </summary>
public class IntentExtractorTests
{
    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public string? LastRequestBody { get; private set; }
        public HttpRequestHeaders? LastRequestHeaders { get; private set; }

        public StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
            => _responder = responder;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            LastRequestHeaders = request.Headers;
            return await _responder(request, cancellationToken).ConfigureAwait(false);
        }
    }

    private static OpenAiCompatibleProviderConfig ValidConfig(double timeoutSeconds = 30.0)
        => new("sk-test-key", "deepseek-v4-flash", "https://token.sensenova.cn", RequestTimeoutSeconds: timeoutSeconds);

    private static StringContent JsonContent(string json)
    {
        var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    /// <summary>
    /// 构造一个模拟 DeepSeek chat/completions 成功响应的 JSON 字符串。
    /// innerJsonContent 是 AI 返回的内容文本（如 {"scope":"target_only",...}），
    /// 会被正确 JSON 转义后嵌入 choices[0].message.content。
    /// </summary>
    private static string ChatResponse(string innerJsonContent, int promptTokens = 50, int completionTokens = 30)
    {
        // innerJsonContent 是 AI 返回的原始字符串，需要用 JsonSerializer 做 JSON 字符串转义
        // （转义 " → \" 等），然后嵌入外层 DeepSeek API 响应 JSON。
        var escapedContent = JsonSerializer.Serialize(innerJsonContent);
        return $"{{\"choices\":[{{\"message\":{{\"content\":{escapedContent}}}}}],\"usage\":{{\"prompt_tokens\":{promptTokens},\"completion_tokens\":{completionTokens}}}}}";
    }

    private static IIntentExtractor CreateExtractor(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        var stub = new StubHttpHandler(responder);
        var provider = new OpenAiCompatibleVisionProvider(new HttpClient(stub), ValidConfig());
        return new IntentExtractor(provider);
    }

    private static IIntentExtractor CreateExtractorWithResponse(string innerJsonContent)
    {
        return CreateExtractor((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(ChatResponse(innerJsonContent)),
            }));
    }

    // ── 成功提取：locate 场景 → target_only + menu_only ──

    [Fact(DisplayName = "ExtractAsync: locate 场景 → scope=target_only, element_handling=menu_only, restore=true")]
    public async Task ExtractAsync_LocateScenario_ReturnsTargetOnlySlots()
    {
        var extractor = CreateExtractorWithResponse(
            @"{""scope"":""target_only"",""element_handling"":""menu_only"",""navigation"":""bounded_settings"",""restore"":true,""completion"":null}");

        var slots = await extractor.ExtractAsync(
            "Locate About phone from the Android Settings home list and verify the destination page.",
            "com.android.settings",
            "About phone",
            2,
            "Settings");

        Assert.Equal("com.android.settings", slots.TargetApp);
        Assert.Equal("target_only", slots.Scope);
        Assert.Equal("About phone", slots.Target);
        Assert.Equal(2, slots.Depth);
        Assert.Equal("menu_only", slots.ElementHandling);
        Assert.Equal("bounded_settings", slots.Navigation);
        Assert.True(slots.Restore);
        Assert.Null(slots.Completion);
        Assert.Equal("Settings", slots.Entry);
    }

    // ── 成功提取：enumerate 场景 → scope=full ──

    [Fact(DisplayName = "ExtractAsync: enumerate 场景 → scope=full, element_handling=menu_only")]
    public async Task ExtractAsync_EnumerateScenario_ReturnsFullScopeSlots()
    {
        var extractor = CreateExtractorWithResponse(
            @"{""scope"":""full"",""element_handling"":""menu_only"",""navigation"":""bounded_settings"",""restore"":true,""completion"":null}");

        var slots = await extractor.ExtractAsync(
            "Enumerate unique first-level Android Settings entries, sample safe read-only pages, and skip dangerous entries.",
            "com.android.settings",
            null,
            2,
            "Settings");

        Assert.Equal("full", slots.Scope);
        Assert.Null(slots.Target);
        Assert.True(slots.Restore);
        Assert.Equal("menu_only", slots.ElementHandling);
    }

    // ── 成功提取：safe_mode + completion override ──

    [Fact(DisplayName = "ExtractAsync: 安全敏感场景 → element_handling=safe_mode, completion=timeout")]
    public async Task ExtractAsync_SafetySensitive_RestrictedInteraction()
    {
        var extractor = CreateExtractorWithResponse(
            @"{""scope"":""full"",""element_handling"":""safe_mode"",""navigation"":""bounded_settings"",""restore"":true,""completion"":""timeout""}");

        var slots = await extractor.ExtractAsync(
            "Safely explore all Settings pages including toggles but never click dangerous items.",
            "com.android.settings",
            null,
            3,
            "Settings");

        Assert.Equal("safe_mode", slots.ElementHandling);
        Assert.Equal("timeout", slots.Completion);
    }

    // ── 成功提取：null element_handling → 默认 full_interaction ──

    [Fact(DisplayName = "ExtractAsync: null element_handling → 通过验证（默认值）")]
    public async Task ExtractAsync_NullElementHandling_PassesValidation()
    {
        var extractor = CreateExtractorWithResponse(
            @"{""scope"":""full"",""element_handling"":null,""navigation"":null,""restore"":false,""completion"":null}");

        var slots = await extractor.ExtractAsync(
            "Explore everything reachable from Settings.",
            "com.android.settings",
            null,
            5,
            "Settings");

        Assert.Equal("full", slots.Scope);
        Assert.Null(slots.ElementHandling);
        Assert.Null(slots.Navigation);
        Assert.False(slots.Restore);
        Assert.Null(slots.Completion);
    }

    // ── 错误处理：HTTP 错误 → InvalidOperationException ──

    [Fact(DisplayName = "ExtractAsync: HTTP 500 → InvalidOperationException")]
    public async Task ExtractAsync_HttpError_ThrowsInvalidOperationException()
    {
        var extractor = CreateExtractor((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = JsonContent(@"{""error"":""internal server error""}"),
            }));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => extractor.ExtractAsync(
                "Locate About phone.",
                "com.android.settings",
                "About phone",
                2,
                "Settings"));

        Assert.Contains("Intent extraction failed", ex.Message);
    }

    // ── 错误处理：无效 JSON → InvalidOperationException ──

    [Fact(DisplayName = "ExtractAsync: 无效 JSON → InvalidOperationException")]
    public async Task ExtractAsync_InvalidJson_ThrowsInvalidOperationException()
    {
        var extractor = CreateExtractorWithResponse("not valid json at all");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => extractor.ExtractAsync(
                "Explore Settings.",
                "com.android.settings",
                null,
                2,
                "Settings"));

        Assert.Contains("invalid JSON", ex.Message);
    }

    // ── 错误处理：未知 scope → InvalidOperationException ──

    [Fact(DisplayName = "ExtractAsync: 未知 scope 值 → InvalidOperationException")]
    public async Task ExtractAsync_UnknownScope_ThrowsInvalidOperationException()
    {
        var extractor = CreateExtractorWithResponse(
            @"{""scope"":""recursive_everything"",""element_handling"":null,""navigation"":null,""restore"":false,""completion"":null}");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => extractor.ExtractAsync(
                "Explore Settings.",
                "com.android.settings",
                null,
                2,
                "Settings"));

        Assert.Contains("recursive_everything", ex.Message);
        Assert.Contains("scope", ex.Message);
    }

    // ── 错误处理：未知 element_handling → InvalidOperationException ──

    [Fact(DisplayName = "ExtractAsync: 未知 element_handling → InvalidOperationException")]
    public async Task ExtractAsync_UnknownElementHandling_ThrowsInvalidOperationException()
    {
        var extractor = CreateExtractorWithResponse(
            @"{""scope"":""full"",""element_handling"":""click_everything_indiscriminately"",""navigation"":null,""restore"":false,""completion"":null}");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => extractor.ExtractAsync(
                "Explore Settings.",
                "com.android.settings",
                null,
                2,
                "Settings"));

        Assert.Contains("element_handling", ex.Message);
        Assert.Contains("click_everything_indiscriminately", ex.Message);
    }

    // ── 错误处理：未知 completion → InvalidOperationException ──

    [Fact(DisplayName = "ExtractAsync: 未知 completion → InvalidOperationException")]
    public async Task ExtractAsync_UnknownCompletion_ThrowsInvalidOperationException()
    {
        var extractor = CreateExtractorWithResponse(
            @"{""scope"":""full"",""element_handling"":null,""navigation"":null,""restore"":false,""completion"":""never_stop""}");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => extractor.ExtractAsync(
                "Explore Settings.",
                "com.android.settings",
                null,
                2,
                "Settings"));

        Assert.Contains("completion", ex.Message);
        Assert.Contains("never_stop", ex.Message);
    }

    // ── 错误处理：空响应 → InvalidOperationException ──

    [Fact(DisplayName = "ExtractAsync: 空响应 → InvalidOperationException")]
    public async Task ExtractAsync_EmptyResponse_ThrowsInvalidOperationException()
    {
        var extractor = CreateExtractorWithResponse("");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => extractor.ExtractAsync(
                "Explore Settings.",
                "com.android.settings",
                null,
                2,
                "Settings"));

        Assert.Contains("empty", ex.Message);
    }

    // ── 空参数校验 ──

    [Fact(DisplayName = "ExtractAsync: 空 description → ArgumentException")]
    public async Task ExtractAsync_EmptyDescription_ThrowsArgumentException()
    {
        var extractor = CreateExtractorWithResponse("{}");

        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.ExtractAsync("", "com.android.settings", null, 2, "Settings"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.ExtractAsync("  ", "com.android.settings", null, 2, "Settings"));
    }

    [Fact(DisplayName = "ExtractAsync: 空 targetApp → ArgumentException")]
    public async Task ExtractAsync_EmptyTargetApp_ThrowsArgumentException()
    {
        var extractor = CreateExtractorWithResponse("{}");

        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.ExtractAsync("Explore Settings.", "", null, 2, "Settings"));
    }

    // ── Prompt 变量替换正确性 ──

    [Fact(DisplayName = "ExtractAsync: prompt 请求包含所有变量替换后的文本")]
    public async Task ExtractAsync_ResolvesAllPromptVariables()
    {
        var stub = new StubHttpHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(ChatResponse(
                    @"{""scope"":""target_only"",""element_handling"":null,""navigation"":null,""restore"":true,""completion"":null}")),
            }));
        var provider = new OpenAiCompatibleVisionProvider(new HttpClient(stub), ValidConfig());
        var extractor = new IntentExtractor(provider);

        await extractor.ExtractAsync(
            "Find Wi-Fi settings.",
            "com.android.settings",
            "Wi-Fi",
            3,
            "Settings");

        Assert.NotNull(stub.LastRequestBody);
        Assert.Contains("Find Wi-Fi settings", stub.LastRequestBody!);
        Assert.Contains("com.android.settings", stub.LastRequestBody!);
        Assert.Contains("Wi-Fi", stub.LastRequestBody!);
        Assert.Contains("3", stub.LastRequestBody!);
        Assert.Contains("Settings", stub.LastRequestBody!);
    }

    // ── null target → "(none - exhaustive traversal)" ──

    [Fact(DisplayName = "ExtractAsync: null target -> prompt 含 exhaustive traversal 标记")]
    public async Task ExtractAsync_NullTarget_PromptMarksExhaustive()
    {
        var stub = new StubHttpHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(ChatResponse(
                    @"{""scope"":""full"",""element_handling"":null,""navigation"":null,""restore"":true,""completion"":null}")),
            }));
        var provider = new OpenAiCompatibleVisionProvider(new HttpClient(stub), ValidConfig());
        var extractor = new IntentExtractor(provider);

        await extractor.ExtractAsync(
            "Explore all Settings pages.",
            "com.android.settings",
            null,
            2,
            "Settings");

        Assert.NotNull(stub.LastRequestBody);
        Assert.Contains("(none - exhaustive traversal)", stub.LastRequestBody!);
    }
}
