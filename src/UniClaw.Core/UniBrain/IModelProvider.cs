namespace UniClaw.Core.UniBrain;

/// <summary>
/// IModelProvider — AI 模型调用抽象。
/// 对齐 Python AIProvider: complete_text / complete_vision / complete_multimodal。
/// 负责: 调用重试、token 预算、超时 (纯传输层)。
/// 不负责: 观测记录 (AICallRecord) — 子接口实现负责 (D-E11)。
/// 消费者: 子接口实现内部注入，不穿过 IUniBrain。
/// </summary>
public interface IModelProvider
{
    /// <summary>Provider 标识 (e.g. "claude", "deepseek")</summary>
    string ProviderId { get; }

    /// <summary>纯文本补全</summary>
    Task<ModelResponse> CompleteTextAsync(ModelRequest request, CancellationToken ct = default);

    /// <summary>视觉补全 (prompt + 截图)</summary>
    Task<ModelResponse> CompleteVisionAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default);

    /// <summary>多模态补全 (prompt + 截图, 同 CompleteVisionAsync 但语义区分)</summary>
    Task<ModelResponse> CompleteMultimodalAsync(ModelRequest request, byte[] imageData, CancellationToken ct = default);
}
