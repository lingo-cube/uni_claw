using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using SkiaSharp;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Mappings;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.UniBrain;

namespace UniClaw.LocalVisionProvider;

/// <summary>
/// LocalVisionProvider — IModelProvider 实现，把截图 POST 给本地 Python FastAPI 视觉服务
/// (/v1/analyze)，把 YOLO+OCR evidence JSON 经 4 步映射管道 (label mapping → Y 轴聚类 →
/// scroll 门禁 → popup 检测) + item 质量后处理 (V1 同排去重 / V2 副标题降级 / V4 文本
/// 归一化 identity) 转成 PageAnalysisDto JSON 返回。
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

    /// <summary>
    /// V1 同排去重行高阈值 (归一化坐标)。校准依据 (真实 evidence, Settings 首页):
    /// 列表行距 ≈0.065 (飞行模式 0.4106 → WLAN 0.4761, WLAN → 蓝牙 0.0658)，
    /// 0.03 安全低于行距不会跨行误并；同元素重复 bbox 中心差 &lt; 0.01。
    /// </summary>
    private const double SameRowThreshold = 0.03;

    /// <summary>
    /// V2 副标题降级阈值 (归一化坐标)，略高于 V1：真实副标题垂直距离
    /// "Storage"→"28% used - 5.72GB free" = 0.033 (spec 场景)、"Franjojo"→
    /// "Franjojo 云空间已满，建议升级" = 0.0235 (fixture evidence)，而正常行距 0.065。
    /// 0.035 同时覆盖两个真实副标题样本且远低于行距，不会吞掉下一行。
    /// </summary>
    private const double SubtitleRowThreshold = 0.035;

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

            // D5 迁移 (e2e-dedup-vision-quality): YOLO bbox 像素逆变换在 Python→C# 边界
            // (本 provider) 完成。C# ImageResizer 在发送前已 crop top/bottom + resize 720px,
            // Python (D10 后 crop=0) 返回的 bbox 是 C# 发送图空间; 此处按请求携带的原始
            // 全屏尺寸逆变换回全屏像素空间。ImageOriginalWidth/Height 为 0 (fallback 路径
            // 无原始尺寸) → 透传, 保持迁移前行为。
            var dto = MapToPageAnalysisDto(evidence, BuildPixelTransform(request, imageData));
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
    /// <summary>
    /// Raw RGBA 视觉补全 (IRawScreenVisionProvider): raw pixels 原样 POST /v1/analyze_raw
    /// (application/octet-stream + X-Image-Width/Height/Pixel-Format headers) →
    /// evidence JSON → 4 步映射 → PageAnalysisDto JSON。crop/resize/JPEG 全部在 Python 侧完成,
    /// C# 零像素操作。HTTP 非 2xx / 传输错误 / 超时 → Success=false (不抛, 同 CompleteVisionAsync)。
    /// </summary>
    public async Task<ModelResponse> CompleteVisionRawAsync(
        ModelRequest request, RawScreenBuffer raw, CancellationToken ct = default)
    {
        if (request is null)
            throw new DomainValidationException(nameof(request), null);
        if (raw.Pixels is null || raw.Pixels.Length == 0)
            throw new DomainValidationException(nameof(raw), raw.Pixels?.Length);

        var sw = Stopwatch.StartNew();
        try
        {
            using var content = new ByteArrayContent(raw.Pixels);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Headers.Add("X-Image-Width", raw.Width.ToString(CultureInfo.InvariantCulture));
            content.Headers.Add("X-Image-Height", raw.Height.ToString(CultureInfo.InvariantCulture));
            content.Headers.Add("X-Image-Pixel-Format", raw.PixelFormat.ToString(CultureInfo.InvariantCulture));

            using var httpResp = await _http.PostAsync("/v1/analyze_raw", content, ct).ConfigureAwait(false);
            sw.Stop();

            if (!httpResp.IsSuccessStatusCode)
            {
                var errBody = await httpResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return GracefulError(
                    $"local-vision HTTP {(int)httpResp.StatusCode} {httpResp.StatusCode}: {errBody}",
                    sw.Elapsed.TotalMilliseconds);
            }

            // Server-Timing parsing (same as CompleteVisionAsync)
            if (httpResp.Headers.TryGetValues("Server-Timing", out var timings))
                await WriteTimingSpansAsync(timings, ct).ConfigureAwait(false);

            var evidenceJson = await httpResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // ── evidence storage (same as CompleteVisionAsync) ──
            if (_pipeline is not null && _traceContext?.CurrentSpanId is { } stepSpanId)
            {
                var evidenceBytes = Encoding.UTF8.GetBytes(evidenceJson);
                var seq = Interlocked.Increment(ref _evidenceSeq);
                var relativePath = RunLayoutV2.VisionEvidenceFileName(stepSpanId, seq > 1 ? seq : 0);

                _pipeline.Submit(new AssetSubmission(
                    AssetCategories.VisionEvidence,
                    evidenceBytes,
                    relativePath));

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

            // Raw path: Python 端以全屏 raw RGBA 为基准, _remap_coords 输出即为全屏空间,
            // 无需二次变换。
            var dto = MapToPageAnalysisDto(evidence, null);
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
    /// <paramref name="pixelTransform"/> 非 null → YOLO bbox 像素逆变换回全屏空间 (D5)；
    /// null → 透传 (fallback 路径无原始尺寸)。
    /// </summary>
    public PageAnalysisDto MapToPageAnalysisDto(
        LocalVisionEvidence evidence,
        (double Sx, int CropTopPx)? pixelTransform = null)
    {
        var candidates = evidence.Candidates ?? [];
        var nonItem = new HashSet<string>(_config.NonItemLabels, StringComparer.Ordinal);

        var level1Menus = new List<MenuInfoDto>();
        var items = new List<ItemDto>();
        var popupCenters = new List<(double X, double Y)>();
        string? popupText = null;
        var nonPopupCenters = new List<(double X, double Y)>();
        // ROI 密度信号: 非 popup 候选的像素框。Python (D10 后 crop=0) 返回的 BoundsPx
        // 在 C# 发送图空间 (已由 ImageResizer crop top/bottom + resize)。若 pixelTransform
        // 非 null，在此边界逆变换为全屏像素空间；null (fallback) → 透传。
        var yoloBboxes = new List<int>();

        // P0 预检: 文本语义 ANR 弹窗检测 — 命中候选与 nonItemLabels 同样
        // 不进 items/menus (系统弹窗不是内容区), 坐标并入 popupCenters。
        var anrMatch = DetectAnrPopupText(candidates);
        if (anrMatch is not null)
        {
            popupText = anrMatch.Value.Text;
            popupCenters.Add((anrMatch.Value.X, anrMatch.Value.Y));
        }

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

            // P0: ANR 文本候选跳过 (已计入 popupCenters)
            if (popupText is not null && IsAnrText(candidate.Text))
            {
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
                // V3: OCR 文本按 YOLO bbox 边界独立识别 — 一个 EvidenceCandidate 即
                // 一个 YOLO bbox + 其自身 OCR 文本 (Python 侧 fuse_evidence 每个
                // detection 独立匹配 token, _primary_line_text 只取框内主行), 相邻
                // bbox 文本不会拼接到同一 item Name (spec: 三个相邻 bbox 输出三个
                // item, 而非 "Dark theme,font size,brightness")。
                items.Add(new ItemDto
                {
                    Name = candidate.Text,
                    Type = mappedType,
                    Coordinate = coord,
                    Parent = null, // v1 恒 null: box 包含推断不可靠且引擎无消费者
                });
            }

            if (candidate.BoundsPx is { Length: 4 })
            {
                if (pixelTransform is var (sx, cropTopPx))
                {
                    yoloBboxes.Add((int)Math.Round(candidate.BoundsPx[0] * sx));
                    yoloBboxes.Add((int)Math.Round(candidate.BoundsPx[1] * sx + cropTopPx));
                    yoloBboxes.Add((int)Math.Round(candidate.BoundsPx[2] * sx));
                    yoloBboxes.Add((int)Math.Round(candidate.BoundsPx[3] * sx + cropTopPx));
                }
                else
                {
                    yoloBboxes.AddRange(candidate.BoundsPx);
                }
            }

            nonPopupCenters.Add((coord.X, coord.Y));
        }

        // ── 同排 item 质量后处理 (e2e-dedup-vision-quality V1/V2) ──
        // 先 V2 降级 (类型语义修正) 再 V1 去重 (重复条目合并)：
        // V2 可能把副标题降级为 text, V1 合并时若任一候选为 menu_item 则
        // 代表项保持 menu_item (可点击性不被副标题降级吞噬)。
        DowngradeSubtitleTypes(items);
        DeduplicateSameRowItems(items);
        ExcludeTopBarSearch(items);

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

        // Step 4: popup 检测 — is_popup + 最近非 popup 候选作 close_button。
        // 双通道:
        //   ① nonItemLabels 候选 (YOLO label=popup/image, 已在上方循环收集到 popupCenters)
        //   ② 文本语义兜底 (P0) — 系统 ANR 弹窗 ("Settings isn't responding" 等) 不被
        //      deki-yolo 标为 popup label，但 OCR 文本是确定性的，精确短语匹配安全。
        //      实测: E2E enumerate 卡死 20 分钟 — ANR 文本 105 帧被 OCR 识别，
        //      is_popup 恒 false → FSM popup 分支永不触发 → 引擎点击被弹窗挡住无限循环。
        var closeButton = FindNearestCloseButton(popupCenters, nonPopupCenters);

        return new PageAnalysisDto
        {
            Level1Dir = level1Dir,
            Level1Menus = level1Menus,
            Level2Dir = null,
            Level2Menus = [],
            CurrentPath = [],
            Items = items,
            YoloBboxes = yoloBboxes,
            IsPopup = popupCenters.Count > 0,
            PopupInfo = popupText is null
                ? null
                : new PopupInfoDto { Title = popupText },
            CloseButton = closeButton,
            BackButton = null,
            HasScroll = hasScroll,
            IsEndOfList = isEndOfList,
        };
    }

    /// <summary>
    /// V4: 文本归一化 (identity key 用) — 折叠连续空白为单空格、归一化常见标点变体
    /// (全角逗号/句号 → 半角, 逗号/顿号 → 空格), 两端去空白。只用于 item identity
    /// (V1 同排去重比较 / 跨帧关联 key), display Name 保持原始 OCR 文本不变。
    /// 场景: OCR 空格变体 "App security,device lock" 与 "App security, device lock"
    /// → 归一化后同为 "app security device lock" → 同一 identity。
    /// </summary>
    public static string NormalizeTextForIdentity(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var sb = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var ch in text)
        {
            var mapped = ch switch
            {
                '，' => ' ',
                '。' => '.',
                '、' => ' ',
                ',' => ' ',
                _ => ch,
            };
            // 空白与标点映射出的空格统一进 pendingSpace 折叠, 避免 "x ,y" 产生双空格
            if (char.IsWhiteSpace(ch) || mapped == ' ')
            {
                pendingSpace = true;
                continue;
            }
            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }
            sb.Append(mapped);
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// V2: 副标题类型降级 — 若一个 item 的 Y 坐标与空间上前一个 (Y 升序, 同 Y 保持
    /// evidence 顺序) 的非空文本 menu_item 型 item 之差在 [0, <see cref="SubtitleRowThreshold"/>)
    /// 内, 则把当前 item 的类型降级为 text。语义: 同一 UI 行内主标题下方/同行的说明文字
    /// (如 "Storage" 下方 "28% used - 5.72GB free"、同行值 "WLAN"→"14015G"、
    /// "蓝牙"→"未连接") 被 deki-yolo 误标为可点击 menu_item → 引擎会把它当独立导航项,
    /// 与主标题重复进入同一物理页面 (E2E 诊断: Storage/Display/Security 各被两条
    /// OCR item 导航)。只降级 menu_item → text, 不动 toggle/input/slider (交互元素
    /// 不可能是副标题)。
    /// 两个防御性约束 (基于真实 evidence 校准):
    /// ① 只处理非空文本 item — 副标题必是 OCR 文本; 空文本是 icon/行背景框, 且若
    ///    空 icon 作"前一个 menu_item"锚点, 搜索框标签 ("搜索设置项" 与行内 icon
    ///    Y 差 ~0.0008) 会被误降级;
    /// ② 差值必须 ≥ 0 (当前在下方) — evidence 顺序非 Y 序 (实测 "蓝牙" y=0.5419
    ///    在 "未连接" y=0.5415 之前), 纯顺序相邻 + 负差会误降级上方 item。
    /// </summary>
    private static void DowngradeSubtitleTypes(List<ItemDto> items)
    {
        if (items.Count < 2)
            return;

        ItemDto? prev = null;
        foreach (var item in items
            .OrderBy(i => i.Coordinate?.Y ?? double.MaxValue))
        {
            if (item.Type == "menu_item"
                && !string.IsNullOrWhiteSpace(item.Name)
                && prev?.Type == "menu_item"
                && !string.IsNullOrWhiteSpace(prev.Name)
                && prev.Coordinate is not null
                && item.Coordinate is not null
                && item.Coordinate.Y - prev.Coordinate.Y >= 0
                && item.Coordinate.Y - prev.Coordinate.Y < SubtitleRowThreshold)
            {
                item.Type = "text";
            }

            // prev 只跟踪非空文本 item — 空文本 icon 不作"前一个 menu_item"锚点
            if (!string.IsNullOrWhiteSpace(item.Name))
                prev = item;
        }
    }

    /// <summary>
    /// V1: 同排重复 item 去重 — 近似相同 Y (差 &lt; <see cref="SameRowThreshold"/>) 且
    /// 归一化文本相同或互为包含的 items 合并为一个, 只输出一个代表项。
    /// 真实场景: YOLO 对同一元素产生多个重叠 bbox (Battery 检出 3 次, 几乎相同 Y
    /// 相同文本) → 3 条同名 menu_item, 引擎当作 3 个独立导航项重复进入同一页面。
    /// 代表项选择: 归一化文本更长者 (信息更全, 处理 "Storage" 与 "Storage details"
    /// 这类包含关系); 长度相同取先出现者。类型: 任一候选为 menu_item 则代表项保持
    /// menu_item。空文本 item 不参与 (空串包含于一切文本, 无 identity 意义)。
    /// 输出保持原始 evidence 顺序。
    /// </summary>
    private static void DeduplicateSameRowItems(List<ItemDto> items)
    {
        if (items.Count < 2)
            return;

        var kept = new List<ItemDto>(items.Count);
        foreach (var item in items)
        {
            if (item.Coordinate is null)
            {
                kept.Add(item);
                continue;
            }

            var normalized = NormalizeTextForIdentity(item.Name);
            if (normalized.Length == 0)
            {
                kept.Add(item);
                continue;
            }

            var merged = false;
            for (var i = 0; i < kept.Count; i++)
            {
                var candidate = kept[i];
                if (candidate.Coordinate is null
                    || Math.Abs(item.Coordinate.Y - candidate.Coordinate.Y) >= SameRowThreshold)
                    continue;

                var candidateNorm = NormalizeTextForIdentity(candidate.Name);
                if (candidateNorm.Length == 0
                    || (!normalized.Contains(candidateNorm, StringComparison.Ordinal)
                        && !candidateNorm.Contains(normalized, StringComparison.Ordinal)))
                    continue;

                if (normalized.Length > candidateNorm.Length)
                {
                    // 新 item 文本更完整 → 以新 item 为代表 (信息更多)
                    if (candidate.Type == "menu_item" && item.Type != "menu_item")
                        item.Type = "menu_item";  // 可点击性优先于副标题降级
                    kept[i] = item;
                }
                else if (item.Type == "menu_item" && candidate.Type != "menu_item")
                {
                    // 保留旧代表, 但类型升级为 menu_item (旧代表是副标题被降级的场景)
                    candidate.Type = "menu_item";
                }
                merged = true;
                break;
            }

            if (!merged)
                kept.Add(item);
        }

        items.Clear();
        items.AddRange(kept);
    }

    /// <summary>
    /// V5: 排除顶部搜索框。搜索框固定在页面最顶部 (Y &lt; 0.10)，文本含 "search"。
    /// YOLO 偶发将搜索框标为 button → leaf_action 匹配 → 引擎点击 → 打开搜索页。
    /// 按 Y 坐标 + 文本双重判定，避免误杀顶部标题。
    /// </summary>
    private const double TopBarYThreshold = 0.10;
    private static readonly Regex SearchTextPattern =
        new(@"search|搜索|搜", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static void ExcludeTopBarSearch(List<ItemDto> items)
    {
        items.RemoveAll(item =>
            item.Coordinate is { } coord
            && coord.Y < TopBarYThreshold
            && item.Name is { } text
            && SearchTextPattern.IsMatch(text));
    }

    // ANR 弹窗精确短语 — Android 系统文案稳定，无列表项误报风险
    private static readonly Regex[] AnrTextPatterns =
    {
        new(@"isn't responding", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"is not responding", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"not responding", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"keeps stopping", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"has stopped", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    };

    /// <summary>
    /// 文本语义 popup 兜底检测 — 返回 (文本, 坐标) 或 null。
    /// 命中 ANR 短语的候选不进 items（与 nonItemLabels 语义一致），坐标并入
    /// popupCenters 供 close_button 计算。
    /// </summary>
    private static (string Text, double X, double Y)? DetectAnrPopupText(
        IEnumerable<EvidenceCandidate> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (IsAnrText(candidate.Text))
            {
                var x = candidate.Center?.X ?? 0.0;
                var y = candidate.Center?.Y ?? 0.0;
                return (candidate.Text, x, y);
            }
        }
        return null;
    }

    private static bool IsAnrText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        foreach (var pattern in AnrTextPatterns)
        {
            if (pattern.IsMatch(text))
                return true;
        }
        return false;
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

    /// <summary>
    /// D5 迁移 (e2e-dedup-vision-quality): Python→C# 边界像素逆变换上下文。
    /// 由 ModelRequest 携带的全屏尺寸 + ImageResizer 同源 crop/resize 参数
    /// (env UNICLAW_IMAGE_CROP_TOP / UNICLAW_IMAGE_MAX_WIDTH, fallback ImageResizer 常量)
    /// 派生。null → fallback 路径无原始尺寸, bbox 透传。
    /// </summary>
    private static (double Sx, int CropTopPx)? BuildPixelTransform(ModelRequest request, byte[] imageData)
    {
        if (request.ImageOriginalWidth <= 0 || request.ImageOriginalHeight <= 0)
            return null;

        var maxWidth = int.TryParse(
            Environment.GetEnvironmentVariable("UNICLAW_IMAGE_MAX_WIDTH"),
            out var mw) && mw > 0
                ? mw
                : ImageResizer.DefaultMaxWidth;
        var cropTop = double.TryParse(
            Environment.GetEnvironmentVariable("UNICLAW_IMAGE_CROP_TOP"),
            out var ct) && ct is >= 0 and < 1
                ? ct
                : ImageResizer.DefaultCropTopRatio;

        var sx = (double)request.ImageOriginalWidth / Math.Min(request.ImageOriginalWidth, maxWidth);
        var cropTopPx = (int)Math.Round(cropTop * request.ImageOriginalHeight);
        return (sx, cropTopPx);
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

        /// <summary>ROI 密度信号 — 非 popup 候选像素框，扁平 [x1,y1,x2,y2,...]。</summary>
        [JsonPropertyName("yolo_bboxes")]
        public List<int> YoloBboxes { get; set; } = [];

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
