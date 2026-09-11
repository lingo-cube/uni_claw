using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace UniClaw.Kernel.Perception;

/// <summary>
/// 感知服务失败分类（ADAPT legacy 诊断码族；§7.3：失败也是显式语义）。
/// 任何非 None 结果 = 零 proposal fail-closed（服务失败 ≠ 空屏幕观察，
/// absence 逐边显式）。
/// </summary>
public enum VisionServiceDiagnostic
{
    None,

    /// <summary>请求超时（transport 层）。</summary>
    Timeout,

    /// <summary>非 2xx / 连接失败（infrastructure）。</summary>
    InfrastructureFailure,

    /// <summary>响应体非法 JSON。</summary>
    MalformedResponse,

    /// <summary>响应缺 yolo/ocr 数组（envelope 不合契约）。</summary>
    SchemaFailure,

    /// <summary>服务端几何校验拒绝（响应 diagnostics 含 INVALID_GEOMETRY）。</summary>
    InvalidGeometry,
}

/// <summary>分析结果：Success = 响应 JSON 原文（确定性锚，逐字节保留）；
/// 失败 = Diagnostic 分类（响应原文为 null）。</summary>
public sealed record VisionServiceResult(
    bool Success,
    string? ResponseJson,
    VisionServiceDiagnostic Diagnostic)
{
    public static VisionServiceResult Ok(string responseJson) => new(true, responseJson, VisionServiceDiagnostic.None);
}

/// <summary>
/// VisionServiceClient — 感知服务 transport 客户端（PER-005 ②的传输半部；
/// ADAPT legacy LocalVisionPerceptionSource 的传输/解析/fail-closed 分类，
/// ZeroCopy 到 /v1/analyze_raw：PNG 解码后的原始 RGBA 直传，无 JPEG 编码器）。
/// 只拥有 transport mechanics + 失败分类；不拥有语义 belief / identity /
/// observation 组装（那是 LiveVisionStrategy 与 FastPerception 的职责）。
/// </summary>
public sealed class VisionServiceClient : IDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http;

    public VisionServiceClient(VisionServiceTransport transport, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _http = transport.CreateHttpClient(timeout ?? DefaultTimeout);
    }

    /// <summary>健康探针（GET /version；host 启动验收用）。</summary>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync("/version", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or SocketException)
        {
            return false;
        }
    }

    /// <summary>分析一帧原始 RGBA（/v1/analyze_raw：X-Image-Width/Height 头
    /// + width×height×4 body）。失败分类见 <see cref="VisionServiceDiagnostic"/>；
    /// 成功返回响应 JSON 原文（后续作为 derived artifact 的确定性锚）。</summary>
    public async Task<VisionServiceResult> AnalyzeAsync(
        byte[] rgba, int width, int height, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "尺寸必须为正");
        if (rgba.Length != width * height * 4)
            throw new ArgumentException($"RGBA 长度不符（{rgba.Length} != {width}×{height}×4）", nameof(rgba));

        try
        {
            // 尺寸是请求头（服务端读 request.headers），不是 content 头——
            // 实测放 content 头服务端 KeyError → 500。
            using var content = new ByteArrayContent(rgba);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/analyze_raw") { Content = content };
            request.Headers.TryAddWithoutValidation(
                "X-Image-Width", width.ToString(System.Globalization.CultureInfo.InvariantCulture));
            request.Headers.TryAddWithoutValidation(
                "X-Image-Height", height.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new VisionServiceResult(false, null, VisionServiceDiagnostic.InfrastructureFailure);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = ParseOrFailure(json, out var malformed);
            if (malformed is not null)
                return malformed;
            var root = document!.RootElement;

            if (!root.TryGetProperty("yolo", out var yolo) || yolo.ValueKind != JsonValueKind.Array
                || !root.TryGetProperty("ocr", out var ocr) || ocr.ValueKind != JsonValueKind.Array)
                return new VisionServiceResult(false, null, VisionServiceDiagnostic.SchemaFailure);

            if (root.TryGetProperty("diagnostics", out var diagnostics)
                && diagnostics.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in diagnostics.EnumerateArray())
                {
                    if (entry.ValueKind == JsonValueKind.Object
                        && entry.TryGetProperty("code", out var code)
                        && code.GetString() == "INVALID_GEOMETRY")
                        return new VisionServiceResult(false, null, VisionServiceDiagnostic.InvalidGeometry);
                }
            }
            return VisionServiceResult.Ok(json);
        }
        catch (Exception exception) when (
            exception is TaskCanceledException or OperationCanceledException)
        {
            return new VisionServiceResult(false, null, VisionServiceDiagnostic.Timeout);
        }
        catch (HttpRequestException)
        {
            return new VisionServiceResult(false, null, VisionServiceDiagnostic.InfrastructureFailure);
        }
    }

    private static JsonDocument? ParseOrFailure(string json, out VisionServiceResult? malformed)
    {
        try
        {
            malformed = null;
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            malformed = new VisionServiceResult(false, null, VisionServiceDiagnostic.MalformedResponse);
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
