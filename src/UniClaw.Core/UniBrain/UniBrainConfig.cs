using System.Collections.Immutable;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// UniBrainConfig — 配置驱动组合。
/// 定义: 哪个子接口实现 → 哪个 IModelProvider。
/// 对齐 Python: ai_providers.yaml routing config。
/// 不含 provider credentials/API keys。
/// </summary>
public sealed record class UniBrainConfig(
    string DefaultProvider = "deepseek",
    ImmutableDictionary<string, string>? CapabilityRouting = null,
    bool EnableTrace = true,
    bool UseTwoStagePageAnalyzer = false);
