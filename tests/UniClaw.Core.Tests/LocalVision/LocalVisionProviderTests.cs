using System.Net;
using System.Text.Json;
using UniClaw.Core.Domain;
using UniClaw.Core.UniBrain;
using UniClaw.LocalVisionProvider;
using Xunit;
// 类与命名空间同名 (UniClaw.LocalVisionProvider.LocalVisionProvider) —
// 测试命名空间 UniClaw.Core.Tests.LocalVision 的祖先含该命名空间, 简单名绑定命名空间, 需别名。
using LV = UniClaw.LocalVisionProvider.LocalVisionProvider;

namespace UniClaw.Core.Tests.LocalVision;

/// <summary>
/// LocalVisionProvider 单测 — 覆盖 label-mapping.json 构造期校验 (V1/V2) 与
/// 4 步映射管道 (label mapping → Y 轴聚类 → scroll 门禁 → popup 检测) (V3-V7, V19, V23-V25)
/// + HTTP 非 2xx graceful 失败 (V26)。
/// 使用内存临时 label-mapping.json fixture，不依赖仓库文件系统文件。
/// </summary>
public class LocalVisionProviderTests
{
    // ── 12.1 (V1): 合法 label-mapping.json → 反序列化 + provider 构造成功 ──

    [Fact(DisplayName = "V1: 合法 label-mapping.json → LabelMappingConfig 反序列化 + provider 构造成功")]
    public void ValidLabelMapping_ProviderConstructs()
    {
        using var fixture = new LabelMappingFixture();

        var config = JsonSerializer.Deserialize<LabelMappingConfig>(
            File.ReadAllText(fixture.Path), DomainJsonOptions.Default);

        Assert.NotNull(config);
        Assert.Equal("uniclaw.labelMapping.v1", config!.Schema);
        Assert.Equal(11, config.Mappings.Count);
        Assert.Equal("menu_item", config.Mappings["button"]);
        Assert.Equal(0.08, config.Spatial!.Level1MaxY);
        config.Validate(); // 不抛

        using var scope = new ProviderScope();
        Assert.Equal("local-vision", scope.Provider.ProviderId);
    }

    // ── 12.2 (V2): 非法映射值 → 构造期 DomainValidationException ──

    [Fact(DisplayName = "V2: 非法映射值 → 构造期 DomainValidationException")]
    public void InvalidMappingValue_ThrowsDomainValidationException()
    {
        using var fixture = new LabelMappingFixture(mappingsJson: "\"button\": \"invalid_type\"");
        using var http = new HttpClient();

        var ex = Assert.Throws<DomainValidationException>(() =>
            new LV(http, null, fixture.Path));

        Assert.Equal("invalid_type", ex.FieldName);
    }

    // ── 12.3 (V3): 未知 YOLO label → 默认 "text" ──

    [Fact(DisplayName = "V3: 未知 YOLO label → 默认 'text'")]
    public void UnknownYoloLabel_DefaultsToText()
    {
        using var scope = new ProviderScope();

        var evidence = Evidence(
            Candidate("unknown_widget", "widget", 0.5, 0.5, boundsPx: [0, 0, 10, 10]));

        var dto = scope.Provider.MapToPageAnalysisDto(evidence);

        var item = Assert.Single(dto.Items);
        Assert.Equal("text", item.Type);
        Assert.Equal("widget", item.Name);
        Assert.Empty(dto.Level1Menus);
    }

    // ── 12.4 (V4): 12 候选 → 有效输出 (items + level1_menus) ──

    [Fact(DisplayName = "V4: 12 候选 → MapToPageAnalysisDto 有效输出 (items + level1_menus)")]
    public void TwelveCandidates_ProducesValidOutput()
    {
        using var scope = new ProviderScope();

        var candidates = new List<EvidenceCandidate>
        {
            Candidate("tab", "tab1", 0.2, 0.05),
            Candidate("tab", "tab2", 0.8, 0.05),
        };
        candidates.AddRange(Enumerable.Range(0, 10)
            .Select(i => Candidate("list_item", $"item{i}", 0.5, 0.2 + 0.06 * i)));
        var evidence = Evidence(candidates.ToArray());

        var dto = scope.Provider.MapToPageAnalysisDto(evidence);

        Assert.Equal(2, dto.Level1Menus.Count);
        Assert.Equal(10, dto.Items.Count);
        Assert.Equal(12, dto.Level1Menus.Count + dto.Items.Count);
        Assert.False(dto.IsPopup);
        Assert.NotNull(dto.Level1Dir);
    }

    // ── 12.5 (V5): center.y < 0.08 → level1_menus, 其余 → items ──

    [Fact(DisplayName = "V5: center.y < 0.08 → level1_menus, 其余 → items")]
    public void LowYCandidates_GoToLevel1Menus()
    {
        using var scope = new ProviderScope();

        // level1MaxY=0.08, 严格小于 → Y=0.07 进菜单, Y=0.09 进 items
        var evidence = Evidence(
            Candidate("tab", "tab1", 0.2, 0.07),
            Candidate("tab", "tab2", 0.4, 0.07),
            Candidate("text_block", "item1", 0.5, 0.09));

        var dto = scope.Provider.MapToPageAnalysisDto(evidence);

        Assert.Equal(2, dto.Level1Menus.Count);
        Assert.Single(dto.Items);
        Assert.Equal("tab1", dto.Level1Menus[0].Name);
        Assert.Equal("item1", dto.Items[0].Name);
    }

    // ── 12.6 (V6): scrollHints.totalCandidates=15 + scrollbarDetected=true → has_scroll:true ──

    [Fact(DisplayName = "V6: totalCandidates=15 + scrollbarDetected=true → has_scroll:true")]
    public void ScrollbarDetected_HasScrollTrue()
    {
        using var scope = new ProviderScope();

        // image 高 2000, 候选高 100 → capacity=20 > total=15 → hasScroll 仅由 scrollbarDetected 决定
        var evidence = new LocalVisionEvidence
        {
            Image = new EvidenceImage { Width = 1000, Height = 2000 },
            Candidates = Enumerable.Range(0, 15)
                .Select(i => Candidate("list_item", $"item{i}", 0.5, 0.1 + 0.05 * i, boundsPx: [0, 0, 100, 100]))
                .ToList(),
            ScrollHints = new ScrollHintsData
            {
                TotalCandidates = 15,
                CandidatesNearBottom = 3,
                ScrollbarDetected = true,
            },
        };

        var dto = scope.Provider.MapToPageAnalysisDto(evidence);

        Assert.True(dto.HasScroll);
        Assert.False(dto.IsEndOfList);
    }

    // ── 12.7 (V7): candidatesNearBottom=0 (无 scrollbar) → is_end_of_list:true ──

    [Fact(DisplayName = "V7: candidatesNearBottom=0 且无 scrollbar → is_end_of_list:true")]
    public void ZeroNearBottom_IsEndOfListTrue()
    {
        using var scope = new ProviderScope();

        var evidence = new LocalVisionEvidence
        {
            Image = new EvidenceImage { Width = 1000, Height = 2000 },
            Candidates = Enumerable.Range(0, 5)
                .Select(i => Candidate("list_item", $"item{i}", 0.5, 0.1 + 0.1 * i, boundsPx: [0, 0, 100, 100]))
                .ToList(),
            ScrollHints = new ScrollHintsData
            {
                TotalCandidates = 5,
                CandidatesNearBottom = 0,
                ScrollbarDetected = false,
            },
        };

        var dto = scope.Provider.MapToPageAnalysisDto(evidence);

        Assert.True(dto.IsEndOfList);
        // capacity=20 > total=5 且无 scrollbar → has_scroll=false (门禁完整契约)
        Assert.False(dto.HasScroll);
    }

    // ── 12.8 (V24): 空识别 (totalCandidates=0) → has_scroll:true, is_end_of_list:false ──

    [Fact(DisplayName = "V24: 空识别 (totalCandidates=0) → has_scroll:true, is_end_of_list:false")]
    public void EmptyRecognition_DefaultsToScrollable()
    {
        using var scope = new ProviderScope();

        var evidence = new LocalVisionEvidence
        {
            ScrollHints = new ScrollHintsData { TotalCandidates = 0 },
        };

        var dto = scope.Provider.MapToPageAnalysisDto(evidence);

        Assert.True(dto.HasScroll);
        Assert.False(dto.IsEndOfList);
        Assert.Empty(dto.Items);
        Assert.Empty(dto.Level1Menus);
    }

    // ── 12.9 (V23): 黄金样本契约 — 输出 JSON 与 HostCommands.SettingsAnalysisJson 结构对齐 ──

    [Fact(DisplayName = "V23: 黄金样本契约 — 序列化 JSON 与 SettingsAnalysisJson 结构对齐")]
    public void SerializedOutput_MatchesGoldenSampleContract()
    {
        using var scope = new ProviderScope();

        // 4 个横向菜单 (Y=0.05, X 方差 > Y 方差 → level1_dir=left/right) + 8 个 items
        var candidates = new List<EvidenceCandidate>
        {
            Candidate("tab", "Network & internet", 0.2, 0.05),
            Candidate("tab", "Connected devices", 0.4, 0.05),
            Candidate("tab", "Apps", 0.6, 0.05),
            Candidate("tab", "Notifications", 0.8, 0.05),
        };
        candidates.AddRange(Enumerable.Range(0, 8)
            .Select(i => Candidate("list_item", $"item{i}", 0.5, 0.2 + 0.08 * i, boundsPx: [0, 0, 100, 100])));
        var evidence = new LocalVisionEvidence
        {
            Image = new EvidenceImage { Width = 1000, Height = 2000 },
            Candidates = candidates,
            ScrollHints = new ScrollHintsData
            {
                TotalCandidates = 12,
                CandidatesNearBottom = 2,
                ScrollbarDetected = false,
            },
        };

        var dto = scope.Provider.MapToPageAnalysisDto(evidence);
        var json = JsonSerializer.Serialize(dto, DomainJsonOptions.Default);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // level1_dir 必须是 null|left|right|top|bottom 之一
        Assert.True(root.TryGetProperty("level1_dir", out var dirEl));
        var dirValue = dirEl.ValueKind == JsonValueKind.String ? dirEl.GetString()! : "null";
        Assert.Contains(dirValue, new[] { "null", "left", "right", "top", "bottom" });

        Assert.True(root.TryGetProperty("level1_menus", out var menusEl));
        Assert.Equal(JsonValueKind.Array, menusEl.ValueKind);
        // level2_dir 为 null → WhenWritingNull 省略键 (黄金样本中非 null 时才出现)
        Assert.True(root.TryGetProperty("level2_menus", out var l2El));
        Assert.Equal(JsonValueKind.Array, l2El.ValueKind);
        Assert.True(root.TryGetProperty("current_path", out var pathEl));
        Assert.Equal(JsonValueKind.Array, pathEl.ValueKind);
        Assert.True(root.TryGetProperty("items", out var itemsEl));
        Assert.Equal(JsonValueKind.Array, itemsEl.ValueKind);
        Assert.True(root.TryGetProperty("is_popup", out var popupEl));
        Assert.True(popupEl.ValueKind is JsonValueKind.True or JsonValueKind.False);
        Assert.True(root.TryGetProperty("has_scroll", out var scrollEl));
        Assert.True(scrollEl.ValueKind is JsonValueKind.True or JsonValueKind.False);
        Assert.True(root.TryGetProperty("is_end_of_list", out var endEl));
        Assert.True(endEl.ValueKind is JsonValueKind.True or JsonValueKind.False);

        // items[0] 契约: {name, type, coordinate{x, y}} (黄金样本字段)
        var firstItem = itemsEl.EnumerateArray().First();
        Assert.True(firstItem.TryGetProperty("name", out _));
        Assert.True(firstItem.TryGetProperty("type", out _));
        Assert.True(firstItem.TryGetProperty("coordinate", out var coordEl));
        Assert.True(coordEl.TryGetProperty("x", out _));
        Assert.True(coordEl.TryGetProperty("y", out _));

        // WhenWritingNull 下 null 字段被省略 → DTO 层断言 (黄金样本 popup_info/close_button/back_button 为 null)
        Assert.Null(dto.Level2Dir);
        Assert.Null(dto.PopupInfo);
        Assert.Null(dto.CloseButton);
        Assert.Null(dto.BackButton);
        Assert.False(dto.IsPopup);
        Assert.Empty(dto.CurrentPath);
        // 菜单 X 均值 0.5 (≥0.5) → "right"
        Assert.Equal("right", dto.Level1Dir);
    }

    // ── 12.10 (V25): 横向布局 (X 方差 > Y 方差) → level1_dir 为 left/right ──

    [Fact(DisplayName = "V25: 横向布局 (X 方差 > Y 方差) → level1_dir 为 left/right")]
    public void HorizontalLayout_Level1DirIsLeftOrRight()
    {
        using var scope = new ProviderScope();

        // 同一 Y (0.05, 方差 0), X 分散 (0.1-0.9, 方差 > 0) → 横向 → left/right
        var evidence = Evidence(
            Candidate("tab", "a", 0.1, 0.05),
            Candidate("tab", "b", 0.3, 0.05),
            Candidate("tab", "c", 0.5, 0.05),
            Candidate("tab", "d", 0.7, 0.05),
            Candidate("tab", "e", 0.9, 0.05));

        var dto = scope.Provider.MapToPageAnalysisDto(evidence);

        Assert.Equal(5, dto.Level1Menus.Count);
        Assert.NotNull(dto.Level1Dir);
        Assert.True(dto.Level1Dir is "left" or "right",
            $"level1_dir 应为 left/right, 实际为 '{dto.Level1Dir}'");
    }

    // ── 12.11 (V26): HTTP 非 2xx → Success=false (不抛) ──

    [Fact(DisplayName = "V26: HTTP 非 2xx → ModelResponse.Success=false (不抛)")]
    public async Task HttpNon2xx_ReturnsGracefulFailure()
    {
        using var fixture = new LabelMappingFixture();
        using var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("boom"),
            }))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        var provider = new LV(http, null, fixture.Path);

        var resp = await provider.CompleteVisionAsync(
            new ModelRequest("prompt"), new byte[] { 1, 2, 3 });

        Assert.False(resp.Success);
        Assert.False(string.IsNullOrEmpty(resp.ErrorMessage));
        Assert.Contains("500", resp.ErrorMessage);
    }

    // ── 12.12 (V19): 多词键 snake_case 序列化 ──

    [Fact(DisplayName = "V19: 多词键 snake_case 序列化 (level1_dir/has_scroll/is_end_of_list/...)")]
    public void MultiWordKeys_SerializeAsSnakeCase()
    {
        // 手工构造全字段 DTO (provider 输出 PopupInfo/BackButton 恒 null, 无法覆盖序列化键名)
        var dto = new LV.PageAnalysisDto
        {
            Level1Dir = "left",
            Level1Menus =
            [
                new LV.MenuInfoDto { Name = "m1", Coordinate = new LV.CoordDto { X = 0.5, Y = 0.5 } },
            ],
            Level2Dir = "top",
            Level2Menus = [],
            CurrentPath = ["Settings"],
            Items =
            [
                new LV.ItemDto { Name = "i1", Type = "menu_item" },
            ],
            IsPopup = true,
            PopupInfo = new LV.PopupInfoDto { Title = "dialog" },
            CloseButton = new LV.CoordDto { X = 0.9, Y = 0.1 },
            BackButton = new LV.CoordDto { X = 0.1, Y = 0.1 },
            HasScroll = true,
            IsEndOfList = false,
        };

        var json = JsonSerializer.Serialize(dto, DomainJsonOptions.Default);

        // 多词键必须 [JsonPropertyName] 锚定 (CamelCase 只对单词属性生效)
        foreach (var key in new[]
        {
            "level1_dir", "has_scroll", "is_end_of_list", "is_popup",
            "close_button", "back_button", "popup_info", "current_path",
        })
            Assert.Contains($"\"{key}\":", json);

        // 单词/其余多词键同样锚定
        Assert.Contains("\"items\":", json);
        Assert.Contains("\"level1_menus\":", json);
        Assert.Contains("\"level2_dir\":", json);
        Assert.Contains("\"level2_menus\":", json);
    }

    // ── Baseline: 真实 evidence JSON golden-file 映射测试 ─────────

    /// <summary>
    /// Baseline golden-file 测试: 加载真实 Screenshots 目录下与截图同名的
    /// .local-vision.evidence.json, 经 4 步映射管道生成 PageAnalysisDto,
    /// 与 .local-vision.expected.json 逐字段对比。
    ///
    /// 资产约定 (与 VisionGoldenIntegrationTests 同一目录):
    /// - 输入: Fixtures/Screenshots/&lt;name&gt;.local-vision.evidence.json
    /// - 期望: Fixtures/Screenshots/&lt;name&gt;.local-vision.expected.json
    /// - 每次运行写 .local-vision.actual.json 供 diff
    /// - 校准: UNICLAW_LOCAL_VISION_UPDATE_EXPECTED=1 固化为 golden
    /// </summary>
    [Fact(DisplayName = "Baseline: 真实 evidence JSON → 期望 PageAnalysisDto")]
    public void Baseline_GoldenEvidence_MapsToExpectedDto()
    {
        using var scope = new ProviderScope();

        // Find the first .local-vision.evidence.json in Screenshots
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Screenshots");
        var evidenceFiles = Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.local-vision.evidence.json")
            : Array.Empty<string>();
        if (evidenceFiles.Length == 0)
            throw new FileNotFoundException(
                $"No .local-vision.evidence.json found in {dir}. "
                + "Place a Python evidence output alongside a screenshot.");

        var evidencePath = evidenceFiles.OrderBy(p => p, StringComparer.Ordinal).First();

        // 1. 加载 golden evidence
        var evidence = JsonSerializer.Deserialize<LocalVisionEvidence>(
            File.ReadAllText(evidencePath), DomainJsonOptions.Default)
            ?? throw new InvalidOperationException("Failed to deserialize evidence JSON.");

        // 2. 跑映射管道
        var dto = scope.Provider.MapToPageAnalysisDto(evidence);

        // 3. 序列化实际输出 → .actual.json (人工 diff)
        var actual = JsonSerializer.Serialize(dto, DomainJsonOptions.Default);
        var actualPath = evidencePath.Replace(".evidence.json", ".actual.json");
        File.WriteAllText(actualPath, actual);

        // 4. 校准模式: 固化为 golden
        var expectedPath = evidencePath.Replace(".evidence.json", ".expected.json");
        if (string.Equals(
                Environment.GetEnvironmentVariable("UNICLAW_LOCAL_VISION_UPDATE_EXPECTED"),
                "1", StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllText(expectedPath, actual);
            return;
        }

        if (!File.Exists(expectedPath))
            throw new FileNotFoundException(
                $"Expected golden not found: {expectedPath}. "
                + "Run with UNICLAW_LOCAL_VISION_UPDATE_EXPECTED=1 to generate.");

        // 5. Golds 对比: JSON 语义等价
        var expected = File.ReadAllText(expectedPath);
        using var expectedDoc = JsonDocument.Parse(expected);
        using var actualDoc = JsonDocument.Parse(actual);
        AssertJsonDeepEqual(expectedDoc.RootElement, actualDoc.RootElement, "");
    }

    /// <summary>递归深度断言两个 JsonElement 相等，失败时带路径信息。</summary>
    private static void AssertJsonDeepEqual(JsonElement expected, JsonElement actual, string path)
    {
        Assert.True(expected.ValueKind == actual.ValueKind,
            $"[{path}] 类型不匹配: expected {expected.ValueKind}, actual {actual.ValueKind}");

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in expected.EnumerateObject())
                {
                    var currentPath = string.IsNullOrEmpty(path)
                        ? prop.Name : $"{path}.{prop.Name}";
                    if (!actual.TryGetProperty(prop.Name, out var actualVal))
                    {
                        // WhenWritingNull: serializer omits null fields — treat as match
                        Assert.True(prop.Value.ValueKind == JsonValueKind.Null,
                            $"[{currentPath}] 缺少属性且期望值非 null");
                        continue;
                    }
                    AssertJsonDeepEqual(prop.Value, actualVal, currentPath);
                }
                break;

            case JsonValueKind.Array:
                var expectedArr = expected.EnumerateArray().ToList();
                var actualArr = actual.EnumerateArray().ToList();
                Assert.True(expectedArr.Count == actualArr.Count,
                    $"[{path}] 数组长度不匹配: expected {expectedArr.Count}, actual {actualArr.Count}");
                for (int i = 0; i < expectedArr.Count; i++)
                    AssertJsonDeepEqual(expectedArr[i], actualArr[i], $"{path}[{i}]");
                break;

            case JsonValueKind.String:
                Assert.Equal(expected.GetString(), actual.GetString());
                break;

            case JsonValueKind.Number:
                Assert.Equal(expected.GetDouble(), actual.GetDouble(), 5);
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                Assert.Equal(expected.GetBoolean(), actual.GetBoolean());
                break;

            case JsonValueKind.Null:
                Assert.True(actual.ValueKind == JsonValueKind.Null,
                    $"[{path}] expected null, actual {actual.ValueKind}");
                break;
        }
    }

    // ── helpers ──────────────────────────────────────────────

    /// <summary>provider + 内存配置 fixture + HttpClient 作用域，测试结束统一释放。</summary>
    private sealed class ProviderScope : IDisposable
    {
        public LabelMappingFixture Fixture { get; }
        public HttpClient Http { get; }
        public LV Provider { get; }

        public ProviderScope(string? mappingsJson = null)
        {
            Fixture = new LabelMappingFixture(mappingsJson);
            Http = new HttpClient();
            Provider = new LV(Http, null, Fixture.Path);
        }

        public void Dispose()
        {
            Http.Dispose();
            Fixture.Dispose();
        }
    }

    /// <summary>内存临时 label-mapping.json (默认映射 = tools/local_vision/label-mapping.json)。</summary>
    private sealed class LabelMappingFixture : IDisposable
    {
        public string Path { get; }

        public LabelMappingFixture(string? mappingsJson = null)
        {
            Path = System.IO.Path.GetTempFileName();
            File.WriteAllText(Path, BuildLabelMappingJson(mappingsJson ?? DefaultMappingsJson));
        }

        public void Dispose()
        {
            if (File.Exists(Path))
                File.Delete(Path);
        }

        private const string DefaultMappingsJson =
            """
            "button": "menu_item",
            "list_item": "menu_item",
            "tab": "menu_item",
            "icon": "menu_item",
            "toolbar": "menu_item",
            "back": "menu_item",
            "switch": "toggle",
            "checkbox": "toggle",
            "input": "input",
            "slider": "slider",
            "text_block": "text"
            """;

        private static string BuildLabelMappingJson(string mappingsJson)
            => $$"""
            {
              "schema": "uniclaw.labelMapping.v1",
              "mappings": {
                {{mappingsJson}}
              },
              "nonItemLabels": ["popup"],
              "spatial": {
                "level1MaxY": 0.08,
                "edgeThreshold": 0.92,
                "roiPadding": { "x": 0.15, "y": 0.10, "minPx": 8, "maxPx": 64 }
              },
              "detection": { "confidence": 0.35 }
            }
            """;
    }

    private static LocalVisionEvidence Evidence(params EvidenceCandidate[] candidates)
        => new() { Candidates = candidates.ToList() };

    private static EvidenceCandidate Candidate(
        string type, string text, double x, double y, int[]? boundsPx = null)
        => new()
        {
            Id = $"cand-{text}",
            Type = type,
            Text = text,
            Confidence = 0.9,
            Center = new NormalizedCoord { X = x, Y = y },
            BoundsPx = boundsPx,
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }
}
