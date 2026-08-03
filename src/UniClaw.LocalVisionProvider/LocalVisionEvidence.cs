using System.Text.Json.Serialization;

namespace UniClaw.LocalVisionProvider;

/// <summary>
/// LocalVisionEvidence — Python FastAPI /v1/analyze 返回的 evidence JSON DTO
/// (schema uniclaw.localVisionEvidence.v1 + scrollHints + metadata 扩展)。
/// 反序列化用 DomainJsonOptions.Default (camelCase)，多词键显式 [JsonPropertyName] 锚定。
/// </summary>
public sealed class LocalVisionEvidence
{
    /// <summary>原始图像尺寸 (estimatedVisibleCapacity 计算用)</summary>
    [JsonPropertyName("image")]
    public EvidenceImage? Image { get; init; }

    /// <summary>YOLO+OCR 融合后的候选列表</summary>
    [JsonPropertyName("candidates")]
    public List<EvidenceCandidate>? Candidates { get; init; }

    /// <summary>滚动原始观测值 (Python 不做滚动判断，判断在 C#)</summary>
    [JsonPropertyName("scrollHints")]
    public ScrollHintsData? ScrollHints { get; init; }

    /// <summary>版本追踪元数据 (schema / pipeline / models / configHash)</summary>
    [JsonPropertyName("metadata")]
    public EvidenceMetadata? Metadata { get; init; }
}

/// <summary>EvidenceImage — 原始图像宽高</summary>
public sealed class EvidenceImage
{
    [JsonPropertyName("width")]
    public int Width { get; init; }

    [JsonPropertyName("height")]
    public int Height { get; init; }
}

/// <summary>EvidenceCandidate — 单个融合候选 (YOLO 检测 + 关联 OCR 文本)</summary>
public sealed class EvidenceCandidate
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    /// <summary>YOLO label (映射前原始值)</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    /// <summary>关联的 OCR 文本 (可空串)</summary>
    [JsonPropertyName("text")]
    public string Text { get; init; } = "";

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    /// <summary>归一化边界框 (x1/y1/x2/y2，0-1)</summary>
    [JsonPropertyName("bounds")]
    public NormalizedBounds? Bounds { get; init; }

    /// <summary>像素边界框 [x1, y1, x2, y2] (avgItemHeight 中位数计算用)</summary>
    [JsonPropertyName("boundsPx")]
    public int[]? BoundsPx { get; init; }

    /// <summary>归一化中心点</summary>
    [JsonPropertyName("center")]
    public NormalizedCoord? Center { get; init; }

    [JsonPropertyName("centerPx")]
    public int[]? CenterPx { get; init; }

    [JsonPropertyName("riskFlags")]
    public List<string>? RiskFlags { get; init; }
}

/// <summary>NormalizedBounds — 归一化边界框 (0-1)</summary>
public sealed class NormalizedBounds
{
    [JsonPropertyName("x1")]
    public double X1 { get; init; }

    [JsonPropertyName("y1")]
    public double Y1 { get; init; }

    [JsonPropertyName("x2")]
    public double X2 { get; init; }

    [JsonPropertyName("y2")]
    public double Y2 { get; init; }
}

/// <summary>NormalizedCoord — 归一化中心点 (0-1)</summary>
public sealed class NormalizedCoord
{
    [JsonPropertyName("x")]
    public double X { get; init; }

    [JsonPropertyName("y")]
    public double Y { get; init; }
}

/// <summary>ScrollHintsData — 滚动原始观测 (Python 只算原始值，判断在 C# Step 3)</summary>
public sealed class ScrollHintsData
{
    /// <summary>YOLO 检测到的交互元素总数</summary>
    [JsonPropertyName("totalCandidates")]
    public int TotalCandidates { get; init; }

    /// <summary>中心点 Y &gt; spatial.edgeThreshold 的候选数 (Python 用共享配置计算)</summary>
    [JsonPropertyName("candidatesNearBottom")]
    public int CandidatesNearBottom { get; init; }

    /// <summary>YOLO 是否检测到 scrollbar 控件</summary>
    [JsonPropertyName("scrollbarDetected")]
    public bool ScrollbarDetected { get; init; }
}

/// <summary>EvidenceMetadata — 版本追踪元数据 (schema 协议冻结；pipeline/models/configHash 可演进)</summary>
public sealed class EvidenceMetadata
{
    [JsonPropertyName("schema")]
    public string? Schema { get; init; }

    [JsonPropertyName("width")]
    public int Width { get; init; }

    [JsonPropertyName("height")]
    public int Height { get; init; }

    [JsonPropertyName("pipeline")]
    public EvidencePipelineInfo? Pipeline { get; init; }

    [JsonPropertyName("models")]
    public EvidenceModelInfo? Models { get; init; }

    /// <summary>label-mapping.json 内容 SHA-256 (64 位 hex) — C# 侧一致性校验用</summary>
    [JsonPropertyName("configHash")]
    public string? ConfigHash { get; init; }
}

/// <summary>EvidencePipelineInfo — 流水线名称/版本</summary>
public sealed class EvidencePipelineInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }
}

/// <summary>EvidenceModelInfo — 模型标识 (yolo 权重名 / ocr 引擎名)</summary>
public sealed class EvidenceModelInfo
{
    [JsonPropertyName("yolo")]
    public string? Yolo { get; init; }

    [JsonPropertyName("ocr")]
    public string? Ocr { get; init; }
}
