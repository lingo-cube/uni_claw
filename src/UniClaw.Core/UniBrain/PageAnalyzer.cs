using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Mappings;
using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// PageAnalyzer — IPageAnalyzer 真实实现 (D-8 关键切片)。
/// 组装 IPromptLibrary + IModelProvider + IScreenCapture：截屏 → 取 analyze_visual 模板 →
/// 调 provider 视觉补全 → 解析 JSON 响应为 PageAnalysis (经 ElementTypeMapper 派生 action)。
/// 关键: ctor 注入 **IModelProvider**（不是 IModelRouter）—— 路由装配期完成，本类见不到 router。
/// 对齐 OpenSpec change unibrain-pageanalyzer-vertical-slice (§12-A 派生核心)。
/// </summary>
public sealed class PageAnalyzer : IPageAnalyzer
{
    private readonly IModelProvider _modelProvider;
    private readonly IPromptLibrary _promptLibrary;
    private readonly IScreenCapture _screenCapture;

    /// <summary>
    /// 构造 PageAnalyzer。modelProvider / promptLibrary / screenCapture 为 null → DomainValidationException fail-fast。
    /// </summary>
    /// <param name="modelProvider">已路由/已观测的模型 provider（D-8: router 装配在 ctor 之前完成）</param>
    /// <param name="promptLibrary">prompt 模板库（按 capability 检索）</param>
    /// <param name="screenCapture">屏幕截图捕获抽象（Core 设备 I/O 接缝）</param>
    public PageAnalyzer(
        IModelProvider modelProvider,
        IPromptLibrary promptLibrary,
        IScreenCapture screenCapture)
    {
        _modelProvider = modelProvider ?? throw new DomainValidationException(nameof(modelProvider), modelProvider);
        _promptLibrary = promptLibrary ?? throw new DomainValidationException(nameof(promptLibrary), promptLibrary);
        _screenCapture = screenCapture ?? throw new DomainValidationException(nameof(screenCapture), screenCapture);
    }

    /// <inheritdoc />
    public async Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)
    {
        // 1. 截屏
        byte[] bytes = await _screenCapture.CaptureAsync(ct);

        // 2. 取 analyze_visual 模；缺失 → fail-fast（不发起模型调用）
        var template = _promptLibrary.GetTemplate(ModelCapabilities.AnalyzeVisual);
        if (template is null)
            throw new DomainValidationException(
                nameof(ModelCapabilities.AnalyzeVisual),
                null,
                "analyze_visual prompt template not configured.");

        // 3. 解析模板变量（空字典：截图走 byte 参数，不通过模板占位符）
        var resolved = template.Resolve(new Dictionary<string, string>());

        // 4. 构造 ModelRequest：结构化输出 schema + 语义标签 capability + 收紧 MaxTokens
        var modelRequest = new ModelRequest(
            resolved.User,
            resolved.System,
            Schemas.AnalyzeVisual,
            MaxTokens: 4096,
            Capability: ModelCapabilities.AnalyzeVisual);

        // 5. 调用模型视觉补全（D-8: 不经 router.Resolve，直接调已注入的 provider）
        var resp = await _modelProvider.CompleteVisionAsync(modelRequest, bytes, ct);

        // 6. 模型失败 → fail-fast
        if (!resp.Success)
            throw new DomainValidationException(
                nameof(resp.Content),
                resp.Content,
                $"analyze_visual model call failed: {resp.ErrorMessage}");

        // 7. 解析 JSON 响应为 PageAnalysisDto → 派生 PageAnalysis
        var cleanContent = StripMarkdownFences(resp.Content);
        PageAnalysisDto dto;
        try
        {
            // null 反序列化结果视为无效 JSON，转 JsonException 走统一 fail-fast 通路
            dto = JsonSerializer.Deserialize<PageAnalysisDto>(cleanContent, DomainJsonOptions.Default)
                ?? throw new JsonException("deserialized to null");
        }
        catch (JsonException ex)
        {
            throw new DomainValidationException(
                nameof(resp.Content),
                resp.Content,
                $"analyze_visual response was not valid JSON: {ex.Message}");
        }

        return MapToPageAnalysis(dto);
    }

    /// <inheritdoc />
    public Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default)
        => throw new NotImplementedException("PageAnalyzer.FindAppEntryAsync pending future slice.");

    /// <inheritdoc />
    public Task<PageTypeVerification> VerifyPageTypeAsync(
        PageAnalysis pageAnalysis,
        string expectedType,
        string? expectedPageName = null,
        CancellationToken ct = default)
        => throw new NotImplementedException("PageAnalyzer.VerifyPageTypeAsync pending future slice.");

    /// <summary>
    /// 将宽松承载的 PageAnalysisDto 派生为类型安全的 PageAnalysis（§12-A 派生核心）。
    /// Items null → fail-fast；空列表 → ImmutableArray.Empty。
    /// 每个 ItemDto: Type 空/whitespace → fail-fast；经 ElementTypeMapper 派生 MenuItemType + ExpectedAction；
    /// Coordinate 0-1 校验由 Coordinate 构造器自带 fail-fast。
    /// </summary>
    private static PageAnalysis MapToPageAnalysis(PageAnalysisDto dto)
    {
        // Items null → fail-fast
        if (dto.Items is null)
            throw new DomainValidationException(
                nameof(dto.Items),
                null,
                "analyze_visual response missing required 'items' field.");

        var items = dto.Items.Count == 0
            ? ImmutableArray<MenuItem>.Empty
            : ImmutableArray.CreateRange(dto.Items.Select(MapItem));

        // Menus
        var level1Menus = dto.Level1Menus is null || dto.Level1Menus.Count == 0
            ? ImmutableArray<MenuInfo>.Empty
            : ImmutableArray.CreateRange(dto.Level1Menus.Select(MapMenu));

        var level2Menus = dto.Level2Menus is null || dto.Level2Menus.Count == 0
            ? ImmutableArray<MenuInfo>.Empty
            : ImmutableArray.CreateRange(dto.Level2Menus.Select(MapMenu));

        // Current path
        var currentPath = dto.CurrentPath is null || dto.CurrentPath.Count == 0
            ? ImmutableArray<string>.Empty
            : ImmutableArray.CreateRange(dto.CurrentPath);

        // Directions: PageAnalysis.Level1Dir/Level2Dir 为 non-nullable Direction。
        // DTO 缺省 → Direction.Left (enum default 0)；非空字符串经 Direction.FromValue 校验（非法值抛 DomainValidationException）。
        var level1Dir = dto.Level1Dir is null ? Direction.Left : DirectionExtensions.FromValue(dto.Level1Dir);
        var level2Dir = dto.Level2Dir is null ? Direction.Left : DirectionExtensions.FromValue(dto.Level2Dir);

        // 弹窗信息（可空）
        PopupInfo? popupInfo = null;
        if (dto.PopupInfo is not null)
        {
            popupInfo = new PopupInfo(
                dto.PopupInfo.Title,
                dto.PopupInfo.Content,
                dto.PopupInfo.CloseButton is null ? null : ToCoordinate(dto.PopupInfo.CloseButton));
        }

        Coordinate? closeButton = dto.CloseButton is null ? null : ToCoordinate(dto.CloseButton);
        Coordinate? backButton = dto.BackButton is null ? null : ToCoordinate(dto.BackButton);

        return new PageAnalysis(
            Level1Dir: level1Dir,
            Level2Dir: level2Dir,
            Level1Menus: level1Menus,
            Level2Menus: level2Menus,
            CurrentPath: currentPath,
            Items: items,
            IsPopup: dto.IsPopup,
            PopupInfo: popupInfo,
            CloseButton: closeButton,
            BackButton: backButton,
            HasScroll: dto.HasScroll,
            IsEndOfList: dto.IsEndOfList);
    }

    /// <summary>
    /// ItemDto → MenuItem。Type 空/whitespace → fail-fast；经 ElementTypeMapper 派生 type/action；
    /// DeriveChangeFlags(action) 派生 ExpectsPageChange/ExpectsStateChange。
    /// </summary>
    private static MenuItem MapItem(ItemDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Type))
            throw new DomainValidationException(
                nameof(dto.Type),
                dto.Type,
                "analyze_visual item.type is empty or whitespace.");

        // §12-A: 非法 type → fail-fast（ToMenuItemType/ToExpectedAction 有回落值不会抛，须主动 IsValidType 校验）
        if (!ElementTypeMapper.IsValidType(dto.Type))
            throw new DomainValidationException(
                nameof(dto.Type),
                dto.Type,
                $"analyze_visual item.type '{dto.Type}' is not a recognized type.");

        var coord = ToCoordinate(dto.Coordinate);
        var itemType = ElementTypeMapper.ToMenuItemType(dto.Type);
        var action = ElementTypeMapper.ToExpectedAction(dto.Type);
        var (pageChange, stateChange) = DeriveChangeFlags(action);

        return new MenuItem(
            Name: dto.Name,
            Coordinate: coord,
            Type: itemType,
            Parent: dto.Parent,
            Description: null,
            ExpectedAction: action,
            ExpectsPageChange: pageChange,
            ExpectsStateChange: stateChange);
    }

    /// <summary>
    /// MenuInfoDto → MenuInfo。Coordinate 缺失 → fail-fast。
    /// </summary>
    private static MenuInfo MapMenu(MenuInfoDto dto)
    {
        var coord = ToCoordinate(dto.Coordinate);
        return new MenuInfo(Name: dto.Name, Coordinate: coord, Active: dto.Active);
    }

    /// <summary>
    /// CoordDto → Coordinate。null → fail-fast；0-1 边界校验由 Coordinate 构造器自带。
    /// </summary>
    private static Coordinate ToCoordinate(CoordDto? dto)
    {
        if (dto is null)
            throw new DomainValidationException(
                "coordinate",
                null,
                "analyze_visual coordinate is null.");
        return new Coordinate(dto.X, dto.Y);
    }

    /// <summary>
    /// 派生 ExpectsPageChange / ExpectsStateChange 标志（§12-A 派生核心）。
    /// Navigate / Action → 页面变化；Toggle → 状态变化；None → 均无变化。
    /// </summary>
    private static (bool ExpectsPageChange, bool ExpectsStateChange) DeriveChangeFlags(ExpectedAction action)
        => action switch
        {
            ExpectedAction.Navigate => (true, false),
            ExpectedAction.Action => (true, false),
            ExpectedAction.Toggle => (false, true),
            ExpectedAction.None => (false, false),
            _ => (false, false),
        };

    /// <summary>
    /// 清除 AI 响应中的 markdown 代码围栏 (```json ... ```)。部分模型（如 Claude）
    /// 会在 JSON 外层包裹 markdown 围栏，需剥离后才能反序列化。
    /// 额外清理前后空白和非 JSON 前缀/后缀文本。
    /// </summary>
    private static string StripMarkdownFences(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return content;

        var trimmed = content.Trim();
        // 移除 ```json ... ``` 围栏
        const string jsonFence = "```json";
        const string fenceEnd = "```";
        if (trimmed.StartsWith(jsonFence, StringComparison.OrdinalIgnoreCase))
        {
            var endIdx = trimmed.LastIndexOf(fenceEnd, StringComparison.Ordinal);
            if (endIdx > jsonFence.Length)
                trimmed = trimmed[(jsonFence.Length)..endIdx].Trim();
        }
        // 移除 ``` ... ``` 无语言标注围栏
        else if (trimmed.StartsWith(fenceEnd))
        {
            var endIdx = trimmed.LastIndexOf(fenceEnd, StringComparison.Ordinal);
            if (endIdx > 3)
                trimmed = trimmed[3..endIdx].Trim();
        }
        // 找第一个 { 和最后一个 } — 提取最外层 JSON 对象
        var braceStart = trimmed.IndexOf('{');
        var braceEnd = trimmed.LastIndexOf('}');
        if (braceStart >= 0 && braceEnd > braceStart)
            return trimmed[braceStart..(braceEnd + 1)];

        return trimmed;
    }

    /// <summary>analyze_visual 响应根 DTO（仅用于 JSON 反序列化，不暴露）。
    /// 多词字段显式 [JsonPropertyName] 锚定 AI 契约的 snake_case 键名（DomainJsonOptions.CamelCase
    /// 仅对单词属性生效；level1_dir/level1_menus/current_path/is_popup/popup_info/close_button/
    /// back_button/has_scroll/is_end_of_list 需显式锚定，否则静默回落 default）。</summary>
    private sealed class PageAnalysisDto
    {
        [JsonPropertyName("level1_dir")] public string? Level1Dir { get; init; }
        [JsonPropertyName("level1_menus")] public List<MenuInfoDto>? Level1Menus { get; init; }
        [JsonPropertyName("level2_dir")] public string? Level2Dir { get; init; }
        [JsonPropertyName("level2_menus")] public List<MenuInfoDto>? Level2Menus { get; init; }
        [JsonPropertyName("current_path")] public List<string>? CurrentPath { get; init; }
        public List<ItemDto>? Items { get; init; }
        [JsonPropertyName("is_popup")] public bool IsPopup { get; init; }
        [JsonPropertyName("popup_info")] public PopupInfoDto? PopupInfo { get; init; }
        [JsonPropertyName("close_button")] public CoordDto? CloseButton { get; init; }
        [JsonPropertyName("back_button")] public CoordDto? BackButton { get; init; }
        [JsonPropertyName("has_scroll")] public bool HasScroll { get; init; }
        [JsonPropertyName("is_end_of_list")] public bool IsEndOfList { get; init; }
    }

    /// <summary>单项 DTO（仅用于 JSON 反序列化）。</summary>
    private sealed class MenuInfoDto
    {
        public string Name { get; init; } = "";
        public CoordDto? Coordinate { get; init; }
        public bool Active { get; init; }
    }

    /// <summary>内容项 DTO（仅用于 JSON 反序列化；不含 expected_action/expects_*，§12-A 派生核心）。</summary>
    private sealed class ItemDto
    {
        public string Name { get; init; } = "";
        public string Type { get; init; } = "";
        public CoordDto? Coordinate { get; init; }
        public string? Parent { get; init; }
    }

    /// <summary>坐标 DTO（仅用于 JSON 反序列化）。</summary>
    private sealed class CoordDto
    {
        public double X { get; init; }
        public double Y { get; init; }
    }

    /// <summary>弹窗信息 DTO（仅用于 JSON 反序列化）。</summary>
    private sealed class PopupInfoDto
    {
        public string? Title { get; init; }
        public string? Content { get; init; }
        public CoordDto? CloseButton { get; init; }
    }
}