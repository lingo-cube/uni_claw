using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Mappings;
using UniClaw.Core.Observability;
using UniClaw.Core.UniBrain;

namespace UniClaw.LocalVisionProvider;

/// <summary>
/// LocalVisionProvider — IModelProvider 实现，把截图 POST 给本地 Python FastAPI 视觉服务
/// (/v1/analyze)，把 YOLO+OCR evidence JSON 经 4 步映射管道 (label mapping → Y 轴聚类 →
/// scroll 门禁 → popup 检测) 转成 PageAnalysisDto JSON 返回。
/// 纯传输 + 映射层: 失败 graceful (Success=false 不抛，对齐 AnthropicModelProvider)；
/// 不引用 Device 层 (无 Process / PythonVisionService)。
/// </summary>
public sealed class LocalVisionProvider : IModelProvider
{
    private readonly HttpClient _http;
    private readonly ITraceRecorder? _traceRecorder;
    private readonly LabelMappingConfig _config;
    private readonly ITracePipeline? _pipeline;
    private readonly ITraceContextProvider? _traceContext;
    private int _evidenceSeq;  // per-step sequence number, guards ai.call retry overwrite

    /// <inheritdoc />
    public string ProviderId => "local-vision";

    /// <summary>
    /// 构造 LocalVisionProvider。构造期加载 + 校验 label-mapping.json (fail-fast)：
    /// schema 版本、spatial 必需段、每个映射值经 <see cref="ElementTypeMapper.IsValidType"/> 校验，
    /// 非法值直接抛 <see cref="DomainValidationException"/>。
    /// 路径由 Host 负责解析并传入绝对路径（不再有 CWD fallback / 环境变量兜底）。
    /// </summary>
    /// <param name="http">HttpClient (BaseAddress 指向 Python 服务，由调用方管理生命周期)</param>
    /// <param name="traceRecorder">可选 trace 记录器 (null → Server-Timing 子 span 静默跳过)</param>
    /// <param name="labelMappingConfigPath">必需的 label-mapping.json 绝对路径（null 或空抛异常）</param>
    /// <param name="pipeline">可选 asset 提交管道 (null → evidence 存储整体 no-op)</param>
    /// <param name="traceContext">可选引擎 step span 上下文 (提供 CurrentSpanId 作 evidence 相对路径锚点)</param>
    public LocalVisionProvider(
        HttpClient http,
        ITraceRecorder? traceRecorder = null,
        string? labelMappingConfigPath = null,
        ITracePipeline? pipeline = null,
        ITraceContextProvider? traceContext = null)
    {
        _http = http ?? throw new DomainValidationException(nameof(http), null);
        _traceRecorder = traceRecorder;
        _pipeline = pipeline;
        _traceContext = traceContext;

        if (string.IsNullOrWhiteSpace(labelMappingConfigPath))
            throw new DomainValidationException(
                nameof(labelMappingConfigPath),
                labelMappingConfigPath,
                "labelMappingConfigPath is required. Host must resolve and pass the absolute path.");

        _config = LoadConfig(Path.GetFullPath(labelMappingConfigPath));
        _config.Validate();
    }

    /// <inheritdoc />
    /// <summary>
    /// 视觉补全: 裸 JPEG bytes POST /v1/analyze → evidence JSON → 4 步映射 → PageAnalysisDto JSON。
    /// HTTP 非 2xx / 传输错误 / 超时 → 返回 Success=false (不抛，除用户 CancellationToken 取消)。
    /// </summary>
    public async Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
    {
        if (request is null)
            throw new DomainValidationException(nameof(request), null);
        if (imageData is null || imageData.Length == 0)
            throw new DomainValidationException(nameof(imageData), imageData?.Length);

        var sw = Stopwatch.StartNew();
        try
        {
            using var content = new ByteArrayContent(imageData);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

            using var httpResp = await _http.PostAsync("/v1/analyze", content, ct).ConfigureAwait(false);
            sw.Stop();

            if (!httpResp.IsSuccessStatusCode)
            {
                var errBody = await httpResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return GracefulError(
                    $"local-vision HTTP {(int)httpResp.StatusCode} {httpResp.StatusCode}: {errBody}",
                    sw.Elapsed.TotalMilliseconds);
            }

            // D-5: timing 走 W3C Server-Timing header，不进 JSON body。解析 → trace 子 span。
            if (httpResp.Headers.TryGetValues("Server-Timing", out var timings))
                await WriteTimingSpansAsync(timings, ct).ConfigureAwait(false);

            var evidenceJson = await httpResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // ── evidence storage (config-gated, default off) ──
            if (_pipeline is not null && _traceContext?.CurrentSpanId is { } stepSpanId)
            {
                var evidenceBytes = Encoding.UTF8.GetBytes(evidenceJson);
                var seq = Interlocked.Increment(ref _evidenceSeq);  // per-step seq guards ai.call retry overwrite
                var relativePath = RunLayoutV2.VisionEvidenceFileName(stepSpanId, seq > 1 ? seq : 0);

                _pipeline.Submit(new AssetSubmission(
                    AssetCategories.VisionEvidence,
                    evidenceBytes,
                    relativePath));

                // Sync reference event — trace is the index, bytes are the payload
                if (_traceRecorder is not null)
                {
                    await _traceRecorder.RecordEventAsync(
                        "ai.evidence",
                        parentSpanId: stepSpanId,
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            [TraceFields.AiEvidencePath] = relativePath,
                            [TraceFields.AiEvidenceType] = "application/json",
                            [TraceFields.AiEvidenceBytes] = evidenceBytes.Length,
                        },
                        ct: ct).ConfigureAwait(false);
                }
            }

            LocalVisionEvidence? evidence;
            try
            {
                evidence = JsonSerializer.Deserialize<LocalVisionEvidence>(evidenceJson, DomainJsonOptions.Default);
            }
            catch (JsonException ex)
            {
                return GracefulError($"local-vision returned invalid evidence JSON: {ex.Message}",
                    sw.Elapsed.TotalMilliseconds);
            }

            if (evidence is null)
                return GracefulError("local-vision returned null/empty evidence JSON.",
                    sw.Elapsed.TotalMilliseconds);

            var dto = MapToPageAnalysisDto(evidence);
            return new ModelResponse(
                Content: JsonSerializer.Serialize(dto, DomainJsonOptions.Default),
                ProviderId: "local-vision",
                Mode: "vision",
                InputTokens: 0,
                OutputTokens: 0,
                LatencyMs: sw.Elapsed.TotalMilliseconds,
                Success: true);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // HttpClient 内部超时 (非用户取消) → graceful
            return GracefulError("local-vision request timed out.", sw.Elapsed.TotalMilliseconds);
        }
        catch (HttpRequestException ex)
        {
            return GracefulError($"local-vision transport error: {ex.GetType().Name}: {ex.Message}",
                sw.Elapsed.TotalMilliseconds);
        }
    }

    /// <inheritdoc />
    /// <summary>本地视觉 provider 不做文本补全。</summary>
    public Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default)
        => throw new NotImplementedException("local-vision provider does not implement text completion.");

    /// <inheritdoc />
    /// <summary>本地视觉 provider 不做多模态补全。</summary>
    public Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default)
        => throw new NotImplementedException("local-vision provider does not implement multimodal completion.");

    // ── 配置加载 ──────────────────────────────────────────────

    private static LabelMappingConfig LoadConfig(string resolvedPath)
    {
        if (!File.Exists(resolvedPath))
            throw new DomainValidationException(nameof(resolvedPath), resolvedPath,
                $"label-mapping.json not found at '{resolvedPath}' (set UNICLAW_LABEL_MAPPING to override).");

        try
        {
            var config = JsonSerializer.Deserialize<LabelMappingConfig>(
                File.ReadAllText(resolvedPath), DomainJsonOptions.Default)
                ?? throw new DomainValidationException(nameof(resolvedPath), resolvedPath,
                    $"label-mapping.json at '{resolvedPath}' deserialized to null.");
            return config;
        }
        catch (JsonException ex)
        {
            throw new DomainValidationException(nameof(resolvedPath), resolvedPath,
                $"label-mapping.json at '{resolvedPath}' is not valid JSON: {ex.Message}");
        }
    }

    // ── Server-Timing → trace 子 span ─────────────────────────

    /// <summary>
    /// 解析 W3C Server-Timing header (格式: "yolo;dur=45.2, ocr;dur=68.7, ...")，
    /// 每段经 RecordEventAsync 记一条点事件 span (spanType = ai.&lt;stage&gt;，DurationMs == 0，
    /// Attributes["ai.latency_ms"] = 时长)。未知 stage 跳过；无 recorder → 静默 no-op。
    /// </summary>
    private async Task WriteTimingSpansAsync(IEnumerable<string> headerValues, CancellationToken ct)
    {
        if (_traceRecorder is null)
            return;

        foreach (var headerValue in headerValues)
        {
            foreach (var entry in headerValue.Split(','))
            {
                var parts = entry.Split(';');
                if (parts.Length < 2)
                    continue;

                var stage = parts[0].Trim();
                var spanType = stage switch
                {
                    "yolo" => SpanTypes.AiYolo,
                    "ocr" => SpanTypes.AiOcr,
                    "fusion" => SpanTypes.AiFusion,
                    "scroll" => SpanTypes.AiScroll,
                    _ => null,
                };
                if (spanType is null)
                    continue;

                var dur = ParseDurationMs(parts[1]);
                if (dur is null)
                    continue;

                await _traceRecorder.RecordEventAsync(
                    spanType,
                    parentSpanId: null,
                    new Dictionary<string, object> { [TraceFields.AiLatencyMs] = dur.Value },
                    ct: ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>解析 "dur=X" 片段为毫秒 double (invariant culture)。</summary>
    private static double? ParseDurationMs(string durPart)
    {
        var trimmed = durPart.Trim();
        if (!trimmed.StartsWith("dur=", StringComparison.OrdinalIgnoreCase))
            return null;
        var value = trimmed[4..].Trim();
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var ms) ? ms : null;
    }

    // ── 4 步映射管道 ──────────────────────────────────────────

    /// <summary>
    /// evidence → PageAnalysisDto 4 步映射管道：
    /// Step 1 YOLO label → AI type (查 label-mapping.json，未知 → "text" + warning)；
    /// Step 2 Y 轴聚类 (center.y &lt; level1MaxY → level1_menus，横向 → left/right，纵向 → top/bottom，无菜单 → null)；
    /// Step 3 scroll 门禁 (保守化: 不确定/空识别偏向可滚动)；
    /// Step 4 popup 检测 (nonItemLabels 候选 → is_popup + 最近非 popup 候选作 close_button)。
    /// </summary>
    public PageAnalysisDto MapToPageAnalysisDto(LocalVisionEvidence evidence)
    {
        var candidates = evidence.Candidates ?? [];
        var nonItem = new HashSet<string>(_config.NonItemLabels, StringComparer.Ordinal);

        var level1Menus = new List<MenuInfoDto>();
        var items = new List<ItemDto>();
        var popupCenters = new List<(double X, double Y)>();
        var nonPopupCenters = new List<(double X, double Y)>();

        foreach (var candidate in candidates)
        {
            var coord = new CoordDto
            {
                X = candidate.Center?.X ?? 0.0,
                Y = candidate.Center?.Y ?? 0.0,
            };

            // Step 4 预检: nonItemLabels 机制 (label-mapping-config 需求) — popup 不进 items/menus
            if (nonItem.Contains(candidate.Type))
            {
                popupCenters.Add((coord.X, coord.Y));
                continue;
            }

            // Step 1: YOLO label → AI type
            var mappedType = _config.Mappings.GetValueOrDefault(candidate.Type);
            if (mappedType is null)
            {
                Trace.TraceWarning(
                    $"local-vision: unknown YOLO label '{candidate.Type}' — defaulting to 'text'.");
                mappedType = "text";
            }

            // Step 2: Y 轴聚类
            if (coord.Y < _config.Spatial!.Level1MaxY)
            {
                level1Menus.Add(new MenuInfoDto
                {
                    Name = candidate.Text,
                    Coordinate = coord,
                    Active = false,
                });
            }
            else
            {
                items.Add(new ItemDto
                {
                    Name = candidate.Text,
                    Type = mappedType,
                    Coordinate = coord,
                    Parent = null, // v1 恒 null: box 包含推断不可靠且引擎无消费者
                });
            }

            nonPopupCenters.Add((coord.X, coord.Y));
        }

        // Step 2 方向: 有菜单 → 横向 (X 方差 > Y 方差) → left/right；纵向 → top/bottom；无菜单 → null
        string? level1Dir = null;
        if (level1Menus.Count > 0)
        {
            var xs = level1Menus.Select(m => m.Coordinate!.X).ToList();
            var ys = level1Menus.Select(m => m.Coordinate!.Y).ToList();
            if (Variance(xs) > Variance(ys))
                level1Dir = xs.Average() < 0.5 ? "left" : "right";
            else
                level1Dir = ys.Average() < 0.5 ? "top" : "bottom";
        }

        // Step 3: scroll 门禁 (保守化, D-7/D-199)
        // 视觉单帧只能证"有内容/可滚"，无法证"到底"：列表滚动中间态常出现底部
        // 空白 (内容在屏外 y>1.0)，此时 candidatesNearBottom==0 会被误判成"到底"，
        // 引擎直接放弃滚动 → 目标漏检 (实测: Settings 列表滚 2 次停在 Accessibility，
        // 下方 4 项含目标全部漏检)。
        // 到底检测唯一可靠来源是引擎侧 seen-set 差分 (swipe 后无新元素连续
        // MaxEmptyScrollRetries+1 次 → 到底, InterceptionHandler.TryHandleScrollAsync)，
        // 代价是最多 2 次空滚 (~20s)，远小于目标漏检。
        var scroll = evidence.ScrollHints;
        var total = scroll?.TotalCandidates ?? 0;
        bool hasScroll;
        bool isEndOfList;
        if (total == 0)
        {
            // 空识别 → 偏向可滚动: 允许一次 swipe 尝试，由引擎 seen-set 差分兜底
            hasScroll = true;
            isEndOfList = false;
        }
        else
        {
            // D-191: deki-yolo 等文本类模型检出大量小框 (Text/ImageView ~60px 高)，
            // 高度中位数失真 → capacity 虚高 → total > capacity 恒 false → 永不滚动，
            // 因此不再用 capacity 判断可滚性。滚动条检测仅作正向信号 (检出→可滚)，
            // 未检出不代表到底。
            hasScroll = true;
            isEndOfList = false;
        }

        // Step 4: popup 检测 — is_popup + 最近非 popup 候选作 close_button
        var closeButton = FindNearestCloseButton(popupCenters, nonPopupCenters);

        return new PageAnalysisDto
        {
            Level1Dir = level1Dir,
            Level1Menus = level1Menus,
            Level2Dir = null,
            Level2Menus = [],
            CurrentPath = [],
            Items = items,
            IsPopup = popupCenters.Count > 0,
            PopupInfo = null,
            CloseButton = closeButton,
            BackButton = null,
            HasScroll = hasScroll,
            IsEndOfList = isEndOfList,
        };
    }

    /// <summary>最近非 popup 候选 → close_button；无 popup 或无其他候选 → null。</summary>
    private static CoordDto? FindNearestCloseButton(
        List<(double X, double Y)> popupCenters,
        List<(double X, double Y)> nonPopupCenters)
    {
        if (popupCenters.Count == 0 || nonPopupCenters.Count == 0)
            return null;

        var best = double.MaxValue;
        (double X, double Y) bestCoord = (0, 0);
        foreach (var popup in popupCenters)
        {
            foreach (var other in nonPopupCenters)
            {
                var dx = popup.X - other.X;
                var dy = popup.Y - other.Y;
                var dist = dx * dx + dy * dy;
                if (dist < best)
                {
                    best = dist;
                    bestCoord = other;
                }
            }
        }
        return new CoordDto { X = bestCoord.X, Y = bestCoord.Y };
    }

    private static double Variance(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
            return 0;
        var mean = values.Average();
        var sumSquares = values.Sum(v => (v - mean) * (v - mean));
        return sumSquares / values.Count;
    }

    private static ModelResponse GracefulError(string message, double latencyMs)
        => new ModelResponse("", "local-vision", "vision", 0, 0, latencyMs) { Success = false, ErrorMessage = message };

    // ── 序列化 DTO (PageAnalysisDto 契约, 对照 PageAnalyzer.PageAnalysisDto + 黄金样本) ──
    // 多词键必须 [JsonPropertyName] 锚定 — DomainJsonOptions.CamelCase 只对单词属性生效。

    /// <summary>PageAnalysisDto — 输出 DTO；与 PageAnalyzer 反序列化契约逐字段对齐。</summary>
    public sealed class PageAnalysisDto
    {
        [JsonPropertyName("level1_dir")]
        public string? Level1Dir { get; set; }

        [JsonPropertyName("level1_menus")]
        public List<MenuInfoDto> Level1Menus { get; set; } = [];

        [JsonPropertyName("level2_dir")]
        public string? Level2Dir { get; set; }

        [JsonPropertyName("level2_menus")]
        public List<MenuInfoDto> Level2Menus { get; set; } = [];

        [JsonPropertyName("current_path")]
        public List<string> CurrentPath { get; set; } = [];

        public List<ItemDto> Items { get; set; } = [];

        [JsonPropertyName("is_popup")]
        public bool IsPopup { get; set; }

        [JsonPropertyName("popup_info")]
        public PopupInfoDto? PopupInfo { get; set; }

        [JsonPropertyName("close_button")]
        public CoordDto? CloseButton { get; set; }

        [JsonPropertyName("back_button")]
        public CoordDto? BackButton { get; set; }

        [JsonPropertyName("has_scroll")]
        public bool HasScroll { get; set; }

        [JsonPropertyName("is_end_of_list")]
        public bool IsEndOfList { get; set; }
    }

    /// <summary>ItemDto — 内容项；不含 active (active 只属于 menus)。parent v1 恒 null。</summary>
    public sealed class ItemDto
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public CoordDto? Coordinate { get; set; }
        public string? Parent { get; set; }
    }

    /// <summary>MenuInfoDto — 菜单项；active 默认 false (YOLO 无法推断选中态)。</summary>
    public sealed class MenuInfoDto
    {
        public string Name { get; set; } = "";
        public CoordDto? Coordinate { get; set; }
        public bool Active { get; set; }
    }

    /// <summary>CoordDto — 归一化坐标。</summary>
    public sealed class CoordDto
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    /// <summary>PopupInfoDto — 弹窗信息 (v1 输出 null)。</summary>
    public sealed class PopupInfoDto
    {
        public string? Title { get; set; }
        public string? Content { get; set; }

        [JsonPropertyName("close_button")]
        public CoordDto? CloseButton { get; set; }
    }
}
