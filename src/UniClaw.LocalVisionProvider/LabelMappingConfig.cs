using System.Text.Json.Serialization;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Mappings;

namespace UniClaw.LocalVisionProvider;

/// <summary>
/// LabelMappingConfig — label-mapping.json 反序列化 DTO (schema uniclaw.labelMapping.v1)。
/// C# 与 Python server 共享的单点真源: YOLO label → AI type 映射、spatial 参数、检测阈值。
/// 校验在构造期 fail-fast (见 <see cref="Validate"/>)，非法映射值直接抛 DomainValidationException。
/// </summary>
public sealed class LabelMappingConfig
{
    /// <summary>schema 版本标识，恒为 "uniclaw.labelMapping.v1"</summary>
    [JsonPropertyName("schema")]
    public string Schema { get; init; } = "";

    /// <summary>YOLO normalized label → AI type 字符串映射</summary>
    [JsonPropertyName("mappings")]
    public Dictionary<string, string> Mappings { get; init; } = new(StringComparer.Ordinal);

    /// <summary>非条目 label 列表 (e.g. ["popup"]) — 只设置 is_popup 标志，不进 items 数组</summary>
    [JsonPropertyName("nonItemLabels")]
    public List<string> NonItemLabels { get; init; } = [];

    /// <summary>空间参数 (level1MaxY / edgeThreshold / roiPadding)</summary>
    [JsonPropertyName("spatial")]
    public SpatialConfig? Spatial { get; init; }

    /// <summary>检测阈值 (confidence)</summary>
    [JsonPropertyName("detection")]
    public DetectionConfig? Detection { get; init; }

    /// <summary>
    /// 构造期 fail-fast 校验: schema 版本 + 每个映射值必须通过 ElementTypeMapper.IsValidType()。
    /// 非法值抛 DomainValidationException (FieldName = 非法映射值)。
    /// </summary>
    public void Validate()
    {
        if (!string.Equals(Schema, "uniclaw.labelMapping.v1", StringComparison.Ordinal))
            throw new DomainValidationException(nameof(Schema), Schema,
                $"label-mapping.json schema '{Schema}' is not 'uniclaw.labelMapping.v1'.");

        if (Spatial is null)
            throw new DomainValidationException(nameof(Spatial), null,
                "label-mapping.json is missing required 'spatial' section.");

        foreach (var (label, typeValue) in Mappings)
        {
            if (!ElementTypeMapper.IsValidType(typeValue))
                throw new DomainValidationException(typeValue, typeValue,
                    $"label-mapping.json maps YOLO label '{label}' to invalid AI type '{typeValue}'.");
        }
    }
}

/// <summary>SpatialConfig — spatial 段: level1MaxY (顶部 tab 栏 Y 阈值) / edgeThreshold (底部边缘阈值) / roiPadding</summary>
public sealed class SpatialConfig
{
    /// <summary>Step 2 Y 轴聚类阈值: center.y &lt; level1MaxY 的候选 → level1_menus</summary>
    [JsonPropertyName("level1MaxY")]
    public double Level1MaxY { get; init; }

    /// <summary>底部边缘贴附阈值 (Python 算 candidatesNearBottom 用; C# 侧 scroll 判断不直接消费)</summary>
    [JsonPropertyName("edgeThreshold")]
    public double EdgeThreshold { get; init; }

    /// <summary>ROI 裁剪 padding 配置</summary>
    [JsonPropertyName("roiPadding")]
    public RoiPaddingConfig? RoiPadding { get; init; }

    /// <summary>预处理参数 (raw RGBA 管线: maxWidth / cropTopRatio / cropBottomRatio)，缺失时 Python 侧用默认值</summary>
    [JsonPropertyName("preprocessing")]
    public PreprocessingConfig? Preprocessing { get; init; }
}

/// <summary>RoiPaddingConfig — ROI 裁剪 padding: 比例 x/y + 像素上下限 clamp</summary>
public sealed class RoiPaddingConfig
{
    /// <summary>padding 与 box 宽度的比例</summary>
    [JsonPropertyName("x")]
    public double X { get; init; }

    /// <summary>padding 与 box 高度的比例</summary>
    [JsonPropertyName("y")]
    public double Y { get; init; }

    /// <summary>padding 像素下限</summary>
    [JsonPropertyName("minPx")]
    public int MinPx { get; init; }

    /// <summary>padding 像素上限</summary>
    [JsonPropertyName("maxPx")]
    public int MaxPx { get; init; }
}

/// <summary>
/// PreprocessingConfig — spatial.preprocessing 段: raw RGBA 管线图像预处理参数
/// (PIL resize/crop)。默认值与旧路径 ImageResizer.DefaultMaxWidth(=720) 及
/// 0.0625 顶/底裁剪比例一致；旧 JSON 无此段时保持 null，Python 侧回退默认值。
/// </summary>
public sealed class PreprocessingConfig
{
    /// <summary>resize 目标最大宽度 (默认 720)</summary>
    [JsonPropertyName("maxWidth")]
    public int MaxWidth { get; init; } = 720;

    /// <summary>顶部裁剪比例 (默认 0.0625)</summary>
    [JsonPropertyName("cropTopRatio")]
    public double CropTopRatio { get; init; } = 0.0625;

    /// <summary>底部裁剪比例 (默认 0.0625)</summary>
    [JsonPropertyName("cropBottomRatio")]
    public double CropBottomRatio { get; init; } = 0.0625;
}

/// <summary>DetectionConfig — detection 段: YOLO 检测置信度阈值</summary>
public sealed class DetectionConfig
{
    /// <summary>YOLO 检测置信度阈值 (默认 0.35)</summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }
}
