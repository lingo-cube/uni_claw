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

    // ── 12.7 (D-199): candidatesNearBottom=0 不再判"到底" —— 视觉单帧无法区分
    // "列表中间态底部空白" 与 "真到底"，误判 is_end_of_list 会让引擎放弃滚动、
    // 漏掉屏外目标 (实测: Settings 列表滚 2 次停在 Accessibility，下方 4 项含
    // 目标全部漏检 → target_page_identity_not_verified)。到底由引擎 seen-set
    // 差分终止 (InterceptionHandler.TryHandleScrollAsync)，代价最多 2 次空滚。

    [Fact(DisplayName = "D-199: candidatesNearBottom=0 且无 scrollbar → 仍可滚动 (is_end_of_list:false)")]
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

        // 有内容即视为可滚动，滚动终止交给引擎差分
        Assert.False(dto.IsEndOfList);
        Assert.True(dto.HasScroll);
    }

    // ── 12.7b (D-191): 近底候选兜底 — 文本类模型小框 (capacity 虚高) 仍视为可滚动 ──

    [Fact(DisplayName = "D-191: candidatesNearBottom>0 (无 scrollbar, total<capacity) → has_scroll:true")]
    public void NearBottomContent_HasScrollTrue()
    {
        using var scope = new ProviderScope();

        // deki-yolo 特征: 大量 ~60px 小框 (Text/ImageView) → 高度中位数失真 →
        // capacity 虚高 (total < capacity) 但屏幕底部仍有内容 (nearBottom>0)。
        var evidence = new LocalVisionEvidence
        {
            Image = new EvidenceImage { Width = 1000, Height = 2000 },
            Candidates = Enumerable.Range(0, 20)
                .Select(i => Candidate("text_block", $"text{i}", 0.5, 0.1 + 0.04 * i, boundsPx: [0, 0, 100, 60]))
                .ToList(),
            ScrollHints = new ScrollHintsData
            {
                TotalCandidates = 20,
                CandidatesNearBottom = 2,
                ScrollbarDetected = false,
            },
        };

        var dto = scope.Provider.MapToPageAnalysisDto(evidence);

        Assert.True(dto.HasScroll);
        Assert.False(dto.IsEndOfList);
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

    // ── 12.10 (V27): yolo_bboxes 透传 — ROI 密度信号 (roi-scroll-detection) ──

    [Fact(DisplayName = "V27: 非 popup 候选的 boundsPx → yolo_bboxes 扁平透传 (popup 排除)")]
    public void MapToPageAnalysisDto_FlattensBoundsPxIntoYoloBboxes()
    {
        using var scope = new ProviderScope();

        var evidence = new LocalVisionEvidence
        {
            Candidates =
            [
                Candidate("button", "OK", 0.5, 0.5, [10, 20, 30, 40]),
                Candidate("text_block", "desc", 0.5, 0.6, [50, 60, 70, 80]),
                // nonItemLabels=["popup"] → 不进 items/menus，bbox 同样排除
                Candidate("popup", "dialog", 0.5, 0.5, [90, 100, 110, 120]),
                // 无 boundsPx 的候选 (OCR-promoted) → 不产生密度信号
                Candidate("list_item", "row", 0.5, 0.7),
            ],
        };

        var dto = scope.Provider.MapToPageAnalysisDto(evidence);

        Assert.Equal([10, 20, 30, 40, 50, 60, 70, 80], dto.YoloBboxes);
        Assert.Equal(3, dto.Items.Count + dto.Level1Menus.Count);
        Assert.True(dto.IsPopup);
    }

    [Fact(DisplayName = "V27b: yolo_bboxes 序列化键名 → snake_case yolo_bboxes, 空候选 → 空数组")]
    public void YoloBboxes_SerializesWithSnakeCaseKey()
    {
        using var scope = new ProviderScope();

        var dto = scope.Provider.MapToPageAnalysisDto(
            new LocalVisionEvidence { Candidates = [Candidate("button", "OK", 0.5, 0.5, [1, 2, 3, 4])] });
        var json = System.Text.Json.JsonSerializer.Serialize(dto, DomainJsonOptions.Default);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("yolo_bboxes", out var el));
        Assert.Equal(JsonValueKind.Array, el.ValueKind);
        Assert.Equal(4, el.GetArrayLength());

        var empty = scope.Provider.MapToPageAnalysisDto(new LocalVisionEvidence { Candidates = [] });
        var emptyJson = System.Text.Json.JsonSerializer.Serialize(empty, DomainJsonOptions.Default);
        using var emptyDoc = JsonDocument.Parse(emptyJson);
        Assert.True(emptyDoc.RootElement.TryGetProperty("yolo_bboxes", out var emptyEl));
        Assert.Equal(0, emptyEl.GetArrayLength());
    }

    // ── P0 (ANR 弹窗文本语义兜底): "Settings isn't responding" → is_popup:true ──

    [Fact(DisplayName = "P0a: ANR 文本 'Settings isn't responding' → is_popup:true + PopupInfo.Title")]
    public void AnrText_DetectedAsPopup()
    {
        using var scope = new ProviderScope();

        // 复刻 E2E ANR 帧: 列表项 + 系统 ANR 弹窗文本 (deki-yolo 不标 popup label)
        var evidence = Evidence(
            Candidate("list_item", "Internet", 0.5, 0.3),
            Candidate("text", "Settings isn't responding", 0.5, 0.45),
            Candidate("button", "Close app", 0.35, 0.6),
            Candidate("button", "Wait", 0.65, 0.6));

        var dto = scope.Provider.MapToPageAnalysisDto(evidence);

        Assert.True(dto.IsPopup, "ANR 文本必须触发 is_popup — 否则 FSM popup 分支永不触发 (E2E 卡死根因)");
        Assert.NotNull(dto.PopupInfo);
        Assert.Equal("Settings isn't responding", dto.PopupInfo!.Title);
        // ANR 文本候选不进 items (与 nonItemLabels 语义一致)
        Assert.DoesNotContain(dto.Items, i => i.Name.Contains("responding"));
        // 列表项仍在 items — 弹窗处理完成后继续遍历
        Assert.Contains(dto.Items, i => i.Name == "Internet");
        // close_button = 最近非 popup 候选 ("Wait" 按钮)
        Assert.NotNull(dto.CloseButton);
    }

    [Fact(DisplayName = "P0b: 无 ANR 文本 → is_popup 保持 false (不误报)")]
    public void NormalPage_NoAnrText_NotPopup()
    {
        using var scope = new ProviderScope();

        var evidence = Evidence(
            Candidate("list_item", "Internet", 0.5, 0.3),
            Candidate("list_item", "T-Mobile", 0.5, 0.4));

        var dto = scope.Provider.MapToPageAnalysisDto(evidence);

        Assert.False(dto.IsPopup);
        Assert.Null(dto.PopupInfo);
    }

    // ── V1 (e2e-dedup-vision-quality): 同排重复 item 去重 ──

    [Fact(DisplayName = "V1: 同排同文本 item (Battery ×3, Y 差 < 0.03) → 只输出一个")]
    public void SameRowDuplicates_MergedToOne()
    {
        using var scope = new ProviderScope();

        // YOLO 对同一元素 (Battery) 产生 3 个重叠 bbox, 几乎相同 Y + 相同文本
        var evidence = Evidence(
            Candidate("list_item", "Battery", 0.3, 0.400),
            Candidate("list_item", "Battery", 0.31, 0.401),
            Candidate("list_item", "Battery", 0.29, 0.402));

        var dto = scope.Provider.MapToPageAnalysisDto(evidence);

        var item = Assert.Single(dto.Items);
        Assert.Equal("Battery", item.Name);
        Assert.Equal("menu_item", item.Type);
    }

    [Fact(DisplayName = "V1: 同文本但 Y 差 ≥ 行高阈值 (0.03) → 两个都保留")]
    public void SameText_DifferentRows_BothKept()
    {
        using var scope = new ProviderScope();

        // 两个真实行 (行距 ≈0.065), 文本相同 → 不是重复检测, 不得合并
        var evidence = Evidence(
            Candidate("list_item", "Storage", 0.3, 0.3),
            Candidate("list_item", "Storage", 0.3, 0.5));

        var dto = scope.Provider.MapToPageAnalysisDto(evidence);

        Assert.Equal(2, dto.Items.Count);
        Assert.All(dto.Items, i => Assert.Equal("menu_item", i.Type));
    }

    [Fact(DisplayName = "V1: 同排包含关系文本 (Storage / Storage details) → 合并为较长者")]
    public void SameRow_ContainingText_MergedToLonger()
    {
        using var scope = new ProviderScope();

        var evidence = Evidence(
            Candidate("list_item", "Storage", 0.3, 0.4),
            Candidate("list_item", "Storage details", 0.3, 0.402));

        var dto = scope.Provider.MapToPageAnalysisDto(evidence);

        var item = Assert.Single(dto.Items);
        Assert.Equal("Storage details", item.Name);
        Assert.Equal("menu_item", item.Type);
    }

    // ── V2 (e2e-dedup-vision-quality): 副标题类型降级 ──

    [Fact(DisplayName = "V2: 主标题 menuItem → 紧邻下方副标题 (Y 差 0.033 < 0.035) 降级为 text")]
    public void SubtitleBelowMenuItem_DowngradedToText()
    {
        using var scope = new ProviderScope();

        // spec 场景: "Storage" Y=0.396, "28% used - 5.72GB free" Y=0.429 (delta 0.033)
        var evidence = Evidence(
            Candidate("list_item", "Storage", 0.3, 0.396),
            Candidate("list_item", "28% used - 5.72GB free", 0.3, 0.429));

        var dto = scope.Provider.MapToPageAnalysisDto(evidence);

        Assert.Equal(2, dto.Items.Count);
        Assert.Equal("Storage", dto.Items[0].Name);
        Assert.Equal("menu_item", dto.Items[0].Type);
        Assert.Equal("28% used - 5.72GB free", dto.Items[1].Name);
        Assert.Equal("text", dto.Items[1].Type);
    }

    [Fact(DisplayName = "V2: 与上方 menuItem 距离 ≥ 阈值 (行距 0.066) → 类型不变")]
    public void SeparateRowItem_NotDowngraded()
    {
        using var scope = new ProviderScope();

        var evidence = Evidence(
            Candidate("list_item", "WLAN", 0.3, 0.4761),
            Candidate("list_item", "Bluetooth", 0.3, 0.5419)); // delta 0.0658 ≥ 0.035

        var dto = scope.Provider.MapToPageAnalysisDto(evidence);

        Assert.Equal(2, dto.Items.Count);
        Assert.Equal("menu_item", dto.Items[0].Type);
        Assert.Equal("menu_item", dto.Items[1].Type);
    }

    // ── V3 (e2e-dedup-vision-quality): OCR 按 bbox 独立识别, 不跨 bbox 拼接 ──

    [Fact(DisplayName = "V3: 三个相邻 bbox (同排, 文本不同) → 三个独立 item, 不拼接")]
    public void AdjacentBboxes_NotConcatenated()
    {
        using var scope = new ProviderScope();

        // spec 场景: 三个相邻 bbox "Dark theme" / "font size" / "brightness"
        var evidence = Evidence(
            Candidate("list_item", "Dark theme", 0.2, 0.4),
            Candidate("list_item", "font size", 0.5, 0.4),
            Candidate("list_item", "brightness", 0.8, 0.4));

        var dto = scope.Provider.MapToPageAnalysisDto(evidence);

        Assert.Equal(3, dto.Items.Count);
        Assert.Equal(
            new[] { "Dark theme", "font size", "brightness" },
            dto.Items.Select(i => i.Name).ToArray());
    }

    // ── V4 (e2e-dedup-vision-quality): 文本归一化 identity ──

    [Fact(DisplayName = "V4: 空格/逗号变体 → 归一化 identity key 相同")]
    public void TextVariants_NormalizeToSameKey()
    {
        var a = LV.NormalizeTextForIdentity("App security,device lock");
        var b = LV.NormalizeTextForIdentity("App security, device lock");
        var c = LV.NormalizeTextForIdentity("App  security ,device lock");

        Assert.Equal(a, b);
        Assert.Equal(b, c);
        Assert.Equal("App security device lock", a);

        // 全角标点变体 → 半角
        Assert.Equal(
            LV.NormalizeTextForIdentity("蓝牙，已连接"),
            LV.NormalizeTextForIdentity("蓝牙,已连接"));
        Assert.Equal(
            LV.NormalizeTextForIdentity("桌面、锁屏与个性化"),
            LV.NormalizeTextForIdentity("桌面 锁屏与个性化"));

        // 空/空白文本 → 空 key
        Assert.Equal("", LV.NormalizeTextForIdentity(null));
        Assert.Equal("", LV.NormalizeTextForIdentity("   "));
    }

    [Fact(DisplayName = "V4: display Name 保持原始 OCR 文本; 同排文本变体 → 归一化 key 相同 → 合并")]
    public void DisplayTextUnchanged_VariantsMergedByNormalizedKey()
    {
        using var scope = new ProviderScope();

        var evidence = Evidence(
            Candidate("list_item", "App security,device lock", 0.3, 0.4),
            Candidate("list_item", "App security, device lock", 0.31, 0.401));

        var dto = scope.Provider.MapToPageAnalysisDto(evidence);

        // 归一化 key 相同 → V1 去重合并为一个; display 保持先出现者的原始 OCR 文本
        var item = Assert.Single(dto.Items);
        Assert.Equal("App security,device lock", item.Name);
        Assert.Equal("menu_item", item.Type);
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

    // ── 12.12 (9.3): CompleteVisionRawAsync mock HTTP — endpoint + headers + graceful 500 ──

    [Fact(DisplayName = "9.3a: CompleteVisionRawAsync → POST /v1/analyze_raw, octet-stream + X-Image-* 头, Success=true")]
    public async Task CompleteVisionRawAsync_PostsToAnalyzeRaw_WithCorrectHeaders()
    {
        using var fixture = new LabelMappingFixture();
        using var recording = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "candidates": [], "scrollHints": {}, "metadata": {} }"""),
        });
        using var http = new HttpClient(recording) { BaseAddress = new Uri("http://localhost") };
        var provider = new LV(http, null, fixture.Path);

        var pixels = new byte[100 * 200 * 4];
        var raw = new RawScreenBuffer(Pixels: pixels, Width: 100, Height: 200, PixelFormat: 1);

        var result = await provider.CompleteVisionRawAsync(
            new ModelRequest("user", "system", null, Capability: "analyze_visual"), raw);

        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.Content));

        var request = recording.LastRequest;
        Assert.NotNull(request);
        Assert.Equal(HttpMethod.Post, request!.Method);
        Assert.Equal("/v1/analyze_raw", request.RequestUri!.AbsolutePath);

        // X-Image-* 头挂在 content headers (ByteArrayContent.Headers.Add) — 随请求发送
        Assert.Equal("application/octet-stream", request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("100", Assert.Single(request.Content.Headers.GetValues("X-Image-Width")));
        Assert.Equal("200", Assert.Single(request.Content.Headers.GetValues("X-Image-Height")));
        Assert.Equal("1", Assert.Single(request.Content.Headers.GetValues("X-Image-Pixel-Format")));

        // body = raw pixels 原样 (无任何 C# 侧像素操作)
        Assert.Equal(pixels, recording.LastRequestBody);
    }

    // ── 12.12b (9.3): HTTP 500 → Success=false, ErrorMessage 含状态码 (不抛) ──

    [Fact(DisplayName = "9.3b: CompleteVisionRawAsync HTTP 500 → Success=false, ErrorMessage 含 500 (graceful)")]
    public async Task CompleteVisionRawAsync_ServerError_ReturnsFailure()
    {
        using var fixture = new LabelMappingFixture();
        using var recording = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("boom"),
        });
        using var http = new HttpClient(recording) { BaseAddress = new Uri("http://localhost") };
        var provider = new LV(http, null, fixture.Path);

        var raw = new RawScreenBuffer(new byte[100 * 200 * 4], Width: 100, Height: 200, PixelFormat: 1);

        var result = await provider.CompleteVisionRawAsync(new ModelRequest("prompt"), raw);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
        Assert.Contains("500", result.ErrorMessage);
    }

    // ── 12.13 (V19): 多词键 snake_case 序列化 ──

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

    /// <summary>
    /// 记录最后一次请求的 mock handler — 用于断言 endpoint / headers / body。
    /// body 在 SendAsync 内同步拷贝 (provider 在返回前已 using 释放 ByteArrayContent,
    /// 测试侧再读会抛 ObjectDisposedException)。
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public RecordingHandler(HttpResponseMessage response) => _response = response;

        public HttpRequestMessage? LastRequest { get; private set; }

        public byte[]? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            return _response;
        }
    }
}
