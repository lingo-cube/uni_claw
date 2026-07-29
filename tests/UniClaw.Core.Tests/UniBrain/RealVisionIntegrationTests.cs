using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// RealVisionIntegrationTests — 真实 AI 视觉模型 + 预存截图的端到端测试。
/// 默认跳过（需手动去掉 Skip 运行）。放入截图到 Fixtures/Screenshots/ 后，
/// dotnet test --filter "Category=Integration" 自动运行。
///
/// 测试用 inline OpenAI-compatible IModelProvider 直连 sensenova-6.7-flash-lite
/// （OpenAI 协议 /v1/chat/completions + image_url）。
/// 不走 litellm gateway（glm-5.2 pass-through 吞图片），
/// 不依赖 AnthropicModelProvider（其双协议支持留后续 spec）。
/// </summary>
[Trait("Category", "Integration")]
public sealed class RealVisionIntegrationTests
{
    private static string? FindScreenshot()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Screenshots");
        if (!Directory.Exists(dir)) return null;
        return Directory.GetFiles(dir, "*.png")
            .Concat(Directory.GetFiles(dir, "*.jpg"))
            .Concat(Directory.GetFiles(dir, "*.jpeg"))
            .FirstOrDefault();
    }

    /// <summary>从 ~/.litellm/secrets.json 读 SENSENOVA_API_KEY（不打印明文）。</summary>
    private static string? LoadSensenovaApiKey()
    {
        var home = Environment.GetEnvironmentVariable("HOME") ?? "";
        var secretsPath = Path.Combine(home, ".litellm", "secrets.json");
        if (!File.Exists(secretsPath)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(secretsPath));
            return doc.RootElement.TryGetProperty("SENSENOVA_API_KEY", out var v)
                ? v.GetString()
                : null;
        }
        catch { return null; }
    }

    [Fact(Skip = "集成测试: 需要手动去掉 Skip 运行，dotnet test --filter Category=Integration")]
    public async Task AnalyzeScreenshot_WithSensenovaVision_ReturnsPageAnalysis()
    {
        var screenshotPath = FindScreenshot();
        if (screenshotPath is null)
            return; // 无截图资产时静默跳

        var apiKey = Environment.GetEnvironmentVariable("SENSENOVA_API_KEY")
                     ?? LoadSensenovaApiKey()
                     ?? throw new InvalidOperationException(
                         "SENSENOVA_API_KEY not found in env or ~/.litellm/secrets.json");
        var baseUrl = Environment.GetEnvironmentVariable("SENSENOVA_BASE_URL")
                      ?? "https://token.sensenova.cn";
        var model = Environment.GetEnvironmentVariable("SENSENOVA_MODEL")
                    ?? "sensenova-6.7-flash-lite";

        using var http = new HttpClient();
        var provider = new OpenAICompatVisionProvider(http, apiKey, baseUrl, model);
        var capture = new FileScreenCapture(screenshotPath);
        var analyzer = new PageAnalyzer(
            provider, new PromptLibrary(PromptTemplateRegistry.AnalyzeVisual), capture);

        var page = await analyzer.AnalyzeCurrentPageAsync();

        Assert.NotNull(page);
        // sensenova 对设置界面应能识别出交互元素，验证 items 非空
        Assert.NotEmpty(page.Items);

        // 保存分析结果到 Screenshots 目录
        var resultPath = Path.ChangeExtension(screenshotPath, ".analysis.json");
        var result = new
        {
            provider = provider.ProviderId,
            model,
            level1_dir = page.Level1Dir.ToString(),
            level2_dir = page.Level2Dir.ToString(),
            level1_menus = page.Level1Menus.Select(m => new { name = m.Name, x = m.Coordinate.X, y = m.Coordinate.Y, active = m.Active }),
            current_path = page.CurrentPath,
            items = page.Items.Select(i => new { name = i.Name, type = i.Type.ToString(), action = i.ExpectedAction.ToString(), x = i.Coordinate.X, y = i.Coordinate.Y, parent = i.Parent }),
            is_popup = page.IsPopup,
            has_scroll = page.HasScroll,
            is_end_of_list = page.IsEndOfList,
        };
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(resultPath, json);
    }

    /// <summary>
    /// OpenAI-compatible IModelProvider — 发 OpenAI 协议 /v1/chat/completions + image_url。
    /// 用于直连 sensenova / 其他 OpenAI 兼容 vision 端点。
    /// 仅集成测试用，不进生产层。
    /// </summary>
    private sealed class OpenAICompatVisionProvider : IModelProvider
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly string _model;

        public OpenAICompatVisionProvider(HttpClient http, string apiKey, string baseUrl, string model)
        {
            _http = http;
            _apiKey = apiKey;
            _baseUrl = baseUrl.TrimEnd('/');
            _model = model;
        }

        public string ProviderId => "openai-compat";

        public Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)
            => throw new NotImplementedException("text mode not used in vision integration test");

        public Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
            => SendAsync(request, imageData, ct);

        public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
            => CompleteVisionAsync(request, imageData, ct);

        private async Task<ModelResponse> SendAsync(ModelRequest request, byte[] imageData, CancellationToken ct)
        {
            var base64 = Convert.ToBase64String(imageData);
            var mediaType = DetectMime(imageData);
            var dataUrl = $"data:{mediaType};base64,{base64}";

            var body = new JsonObject
            {
                ["model"] = _model,
                ["max_tokens"] = request.MaxTokens,
                ["stream"] = false,
                ["reasoning_effort"] = "none",
                ["messages"] = new JsonArray(
                    new JsonObject
                    {
                        ["role"] = "system",
                        ["content"] = request.SystemPrompt ?? "",
                    },
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["content"] = new JsonArray(
                            new JsonObject
                            {
                                ["type"] = "text",
                                ["text"] = request.Prompt,
                            },
                            new JsonObject
                            {
                                ["type"] = "image_url",
                                ["image_url"] = new JsonObject { ["url"] = dataUrl },
                            })
                    })
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/chat/completions");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            req.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) Chrome/120.0.0.0 Safari/537.36");
            req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(120));

            using var resp = await _http.SendAsync(req, cts.Token).ConfigureAwait(false);
            var raw = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return new ModelResponse("", ProviderId, "vision", 0, 0, 0, _model) with
                {
                    Success = false,
                    ErrorMessage = $"HTTP {(int)resp.StatusCode}: {raw}",
                };
            }

            var doc = JsonNode.Parse(raw);
            var content = doc?["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";
            var usage = doc?["usage"];
            var inputTok = (int?)(usage?["prompt_tokens"]) ?? 0;
            var outputTok = (int?)(usage?["completion_tokens"]) ?? 0;
            return new ModelResponse(content, ProviderId, "vision", inputTok, outputTok, 0, _model);
        }

        private static string DetectMime(byte[] data)
        {
            if (data.Length < 4) return "image/png";
            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return "image/png";
            if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return "image/jpeg";
            if (data.Length > 12 && data[0] == 0x52 && data[1] == 0x49 && data[8] == 0x57) return "image/webp";
            return "image/png";
        }
    }
}