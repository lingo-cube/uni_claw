using System.Net;
using System.Net.Http.Headers;
using System.Text;
using UniClaw.Core.Domain;
using UniClaw.Core.UniBrain;
using UniClaw.DeepSeekProvider;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// DeepSeekModelProvider 单元测试 — task 6.3。
/// 无网络为主 (StubHttpHandler); 真实调用为 opt-in (DEEPSEEK_API_KEY 环境变量)。
/// </summary>
public class DeepSeekModelProviderTests
{
    // ── StubHttpHandler: 捕获请求体 + 返回预设响应 ──

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

    private static DeepSeekProviderConfig ValidConfig(double timeoutSeconds = 30.0)
        => new("sk-test-key", "deepseek-chat", "https://api.deepseek.com", RequestTimeoutSeconds: timeoutSeconds);

    private static StringContent JsonContent(string json)
    {
        var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    // ── 成功映射 ──

    [Fact(DisplayName = "CompleteTextAsync: 200 + 标准 body → Success=true, 映射 content/tokens/mode")]
    public async Task CompleteTextAsync_Success_MapsContentAndTokens()
    {
        var stub = new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(@"{""choices"":[{""message"":{""content"":""hi""}}],""usage"":{""prompt_tokens"":5,""completion_tokens"":7}}"),
        }));
        var provider = new DeepSeekModelProvider(new HttpClient(stub), ValidConfig());

        var resp = await provider.CompleteTextAsync(new ModelRequest("hello"), CancellationToken.None);

        Assert.True(resp.Success);
        Assert.Equal("hi", resp.Content);
        Assert.Equal("deepseek", resp.ProviderId);
        Assert.Equal("text", resp.Mode);
        Assert.Equal(5, resp.InputTokens);
        Assert.Equal(7, resp.OutputTokens);
        Assert.Equal("deepseek-chat", resp.Model);
        Assert.Null(resp.ErrorMessage);
    }

    // ── Schema → response_format json_object ──

    [Fact(DisplayName = "CompleteTextAsync: Schema 非 null → 请求体含 response_format=json_object")]
    public async Task CompleteTextAsync_WithSchema_SetsResponseFormatJsonObject()
    {
        var stub = new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(@"{""choices"":[{""message"":{""content"":""{}""}}],""usage"":{""prompt_tokens"":1,""completion_tokens"":1}}"),
        }));
        var provider = new DeepSeekModelProvider(new HttpClient(stub), ValidConfig());

        await provider.CompleteTextAsync(new ModelRequest("hello", Schema: "something"), CancellationToken.None);

        Assert.NotNull(stub.LastRequestBody);
        Assert.Contains("\"response_format\"", stub.LastRequestBody!);
        Assert.Contains("\"json_object\"", stub.LastRequestBody!);
    }

    // ── 无 Schema → 不含 response_format ──

    [Fact(DisplayName = "CompleteTextAsync: 无 Schema → 请求体不含 response_format")]
    public async Task CompleteTextAsync_WithoutSchema_OmitsResponseFormat()
    {
        var stub = new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(@"{""choices"":[{""message"":{""content"":""ok""}}],""usage"":{""prompt_tokens"":1,""completion_tokens"":1}}"),
        }));
        var provider = new DeepSeekModelProvider(new HttpClient(stub), ValidConfig());

        await provider.CompleteTextAsync(new ModelRequest("hello"), CancellationToken.None);

        Assert.NotNull(stub.LastRequestBody);
        Assert.DoesNotContain("\"response_format\"", stub.LastRequestBody!);
    }

    // ── SystemPrompt → messages 含 system role ──

    [Fact(DisplayName = "CompleteTextAsync: SystemPrompt → 请求体含 system message")]
    public async Task CompleteTextAsync_WithSystemPrompt_IncludesSystemMessage()
    {
        var stub = new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(@"{""choices"":[{""message"":{""content"":""ok""}}],""usage"":{""prompt_tokens"":1,""completion_tokens"":1}}"),
        }));
        var provider = new DeepSeekModelProvider(new HttpClient(stub), ValidConfig());

        await provider.CompleteTextAsync(new ModelRequest("hello", SystemPrompt: "be brief"), CancellationToken.None);

        Assert.NotNull(stub.LastRequestBody);
        Assert.Contains("\"role\":\"system\"", stub.LastRequestBody!);
        Assert.Contains("be brief", stub.LastRequestBody!);
    }

    // ── Authorization header ──

    [Fact(DisplayName = "CompleteTextAsync: 请求携带 Authorization Bearer 头")]
    public async Task CompleteTextAsync_SetsAuthorizationBearerHeader()
    {
        var stub = new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent(@"{""choices"":[{""message"":{""content"":""ok""}}],""usage"":{""prompt_tokens"":1,""completion_tokens"":1}}"),
        }));
        var provider = new DeepSeekModelProvider(new HttpClient(stub), ValidConfig());

        await provider.CompleteTextAsync(new ModelRequest("hello"), CancellationToken.None);

        Assert.NotNull(stub.LastRequestHeaders);
        var auth = stub.LastRequestHeaders!.Authorization;
        Assert.NotNull(auth);
        Assert.Equal("Bearer", auth!.Scheme);
        Assert.Equal("sk-test-key", auth.Parameter);
    }

    // ── HTTP 错误 graceful ──

    [Fact(DisplayName = "CompleteTextAsync: 500 → Success=false, 不抛")]
    public async Task CompleteTextAsync_HttpError_ReturnsGracefulFailure()
    {
        var stub = new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = JsonContent(@"{""error"":""boom""}"),
        }));
        var provider = new DeepSeekModelProvider(new HttpClient(stub), ValidConfig());

        var resp = await provider.CompleteTextAsync(new ModelRequest("hello"), CancellationToken.None);

        Assert.False(resp.Success);
        Assert.NotNull(resp.ErrorMessage);
        Assert.Contains("500", resp.ErrorMessage!);
    }

    // ── 超时 graceful ──

    [Fact(DisplayName = "CompleteTextAsync: 超时 → Success=false, 不抛")]
    public async Task CompleteTextAsync_Timeout_ReturnsGracefulFailure()
    {
        var stub = new StubHttpHandler(async (_, ct) =>
        {
            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
            catch (OperationCanceledException) { /* 预期 */ }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(@"{""choices"":[{""message"":{""content"":""late""}}]}"),
            };
        });
        var provider = new DeepSeekModelProvider(new HttpClient(stub), ValidConfig(timeoutSeconds: 0.01));

        var resp = await provider.CompleteTextAsync(new ModelRequest("hello"), CancellationToken.None);

        Assert.False(resp.Success);
        Assert.NotNull(resp.ErrorMessage);
    }

    // ── 用户 CancellationToken 主动取消 → 重抛 (非 graceful) ──

    [Fact(DisplayName = "CompleteTextAsync: 用户 ct 取消 → 抛 OperationCanceledException, 不 graceful")]
    public async Task CompleteTextAsync_UserCancellationToken_ReThrows()
    {
        using var userCts = new CancellationTokenSource();
        var stub = new StubHttpHandler(async (_, ct) =>
        {
            userCts.Cancel(); // 在响应前模拟用户取消
            ct.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var provider = new DeepSeekModelProvider(new HttpClient(stub), ValidConfig(timeoutSeconds: 30.0));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.CompleteTextAsync(new ModelRequest("hello"), userCts.Token));
    }

    // ── Vision + Multimodal 未实现 ──

    [Fact(DisplayName = "CompleteVisionAsync → NotImplementedException")]
    public async Task CompleteVisionAsync_ThrowsNotImplemented()
    {
        var provider = new DeepSeekModelProvider(new HttpClient(new StubHttpHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))), ValidConfig());

        await Assert.ThrowsAsync<NotImplementedException>(() =>
            provider.CompleteVisionAsync(new ModelRequest("hi"), new byte[0]));
    }

    [Fact(DisplayName = "CompleteMultimodalAsync → NotImplementedException")]
    public async Task CompleteMultimodalAsync_ThrowsNotImplemented()
    {
        var provider = new DeepSeekModelProvider(new HttpClient(new StubHttpHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))), ValidConfig());

        await Assert.ThrowsAsync<NotImplementedException>(() =>
            provider.CompleteMultimodalAsync(new ModelRequest("hi"), new byte[0]));
    }

    // ── 构造器 null 校验 ──

    [Fact(DisplayName = "ctor: null HttpClient → DomainValidationException")]
    public void Ctor_NullHttpClient_Throws()
    {
        Assert.Throws<DomainValidationException>(() =>
            new DeepSeekModelProvider(null!, ValidConfig()));
    }

    [Fact(DisplayName = "ctor: null config → DomainValidationException")]
    public void Ctor_NullConfig_Throws()
    {
        Assert.Throws<DomainValidationException>(() =>
            new DeepSeekModelProvider(new HttpClient(), null!));
    }

    // ── opt-in 真实 DeepSeek 调用 (无 DEEPSEEK_API_KEY 则 no-op pass) ──
    // model / base URL 可经 DEEPSEEK_MODEL / DEEPSEEK_BASE_URL 覆盖, 以适配非标准网关。
    // 默认 model=deepseek-v4-flash (本项目 DeepSeek 网关支持 v4-pro/v4-flash; 公共 api.deepseek.com 用 deepseek-chat, 设 DEEPSEEK_MODEL 覆盖)。

    [Fact(DisplayName = "真实 DeepSeek 调用 (opt-in: 需 DEEPSEEK_API_KEY)")]
    public async Task Live_DeepSeek_Call_ReturnsSuccess()
    {
        var key = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        if (key is null)
            return; // 无 key → no-op pass

        var model = Environment.GetEnvironmentVariable("DEEPSEEK_MODEL") ?? "deepseek-v4-flash";
        var baseUrl = Environment.GetEnvironmentVariable("DEEPSEEK_BASE_URL") ?? "https://api.deepseek.com";

        var provider = new DeepSeekModelProvider(
            new HttpClient(),
            new DeepSeekProviderConfig(key, model, baseUrl));

        var resp = await provider.CompleteTextAsync(
            new ModelRequest("Reply with the single word: OK", SystemPrompt: "Be extremely brief.", MaxTokens: 16),
            CancellationToken.None);

        Assert.True(resp.Success, $"Live call failed: {resp.ErrorMessage}");
    }

    // ════════════════════════════════════════
    // ── DeepSeekProviderConfig 构造校验 ──
    // ════════════════════════════════════════

    [Fact(DisplayName = "Config: 合法构造 → 默认 MaxConcurrentRequests=4, RequestTimeoutSeconds=30.0")]
    public void Config_Valid_UsesDefaults()
    {
        var cfg = new DeepSeekProviderConfig("sk-key", "deepseek-chat", "https://api.deepseek.com");

        Assert.Equal("sk-key", cfg.ApiKey);
        Assert.Equal("deepseek-chat", cfg.Model);
        Assert.Equal("https://api.deepseek.com", cfg.BaseUrl);
        Assert.Equal(4, cfg.MaxConcurrentRequests);
        Assert.Equal(30.0, cfg.RequestTimeoutSeconds);
    }

    [Theory(DisplayName = "Config: 空字段 → DomainValidationException, FieldName 正确")]
    [InlineData("", "model", "url", nameof(DeepSeekProviderConfig.ApiKey))]
    [InlineData("key", "", "url", nameof(DeepSeekProviderConfig.Model))]
    [InlineData("key", "model", "", nameof(DeepSeekProviderConfig.BaseUrl))]
    [InlineData("   ", "model", "url", nameof(DeepSeekProviderConfig.ApiKey))]
    public void Config_EmptyField_Throws(string key, string model, string url, string expectedField)
    {
        var ex = Assert.Throws<DomainValidationException>(() =>
            new DeepSeekProviderConfig(key, model, url));
        Assert.Equal(expectedField, ex.FieldName);
    }

    [Fact(DisplayName = "Config: MaxConcurrentRequests <= 0 → DomainValidationException")]
    public void Config_NonPositiveMaxConcurrent_Throws()
    {
        var ex = Assert.Throws<DomainValidationException>(() =>
            new DeepSeekProviderConfig("key", "model", "url", MaxConcurrentRequests: 0));
        Assert.Equal(nameof(DeepSeekProviderConfig.MaxConcurrentRequests), ex.FieldName);
    }

    [Fact(DisplayName = "Config: RequestTimeoutSeconds <= 0 → DomainValidationException")]
    public void Config_NonPositiveTimeout_Throws()
    {
        var ex = Assert.Throws<DomainValidationException>(() =>
            new DeepSeekProviderConfig("key", "model", "url", RequestTimeoutSeconds: 0));
        Assert.Equal(nameof(DeepSeekProviderConfig.RequestTimeoutSeconds), ex.FieldName);
    }
}
