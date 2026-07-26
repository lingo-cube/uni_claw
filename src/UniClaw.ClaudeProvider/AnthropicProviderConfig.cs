using UniClaw.Core.Domain;

namespace UniClaw.ClaudeProvider;

/// <summary>
/// AnthropicProviderConfig — Anthropic Claude API 传输层配置 (fail-fast 构造期校验)。
/// </summary>
public sealed record class AnthropicProviderConfig
{
    /// <summary>Anthropic API key (必填)</summary>
    public string ApiKey { get; init; }

    /// <summary>模型名 (e.g. "claude-sonnet-4-20250514")</summary>
    public string Model { get; init; }

    /// <summary>API base URL (默认 "https://api.anthropic.com")</summary>
    public string BaseUrl { get; init; }

    /// <summary>单请求超时秒数 (默认 60.0，视觉请求通常更慢)</summary>
    public double RequestTimeoutSeconds { get; init; }

    public AnthropicProviderConfig(
        string ApiKey,
        string Model,
        string BaseUrl = "https://api.anthropic.com",
        double RequestTimeoutSeconds = 60.0)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new DomainValidationException(nameof(ApiKey), ApiKey);
        if (string.IsNullOrWhiteSpace(Model))
            throw new DomainValidationException(nameof(Model), Model);
        if (string.IsNullOrWhiteSpace(BaseUrl))
            throw new DomainValidationException(nameof(BaseUrl), BaseUrl);
        if (RequestTimeoutSeconds <= 0)
            throw new DomainValidationException(nameof(RequestTimeoutSeconds), RequestTimeoutSeconds);

        this.ApiKey = ApiKey;
        this.Model = Model;
        this.BaseUrl = BaseUrl.TrimEnd('/');
        this.RequestTimeoutSeconds = RequestTimeoutSeconds;
    }
}
