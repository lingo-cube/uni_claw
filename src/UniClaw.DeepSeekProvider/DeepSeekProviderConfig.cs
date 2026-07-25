using UniClaw.Core.Domain;

namespace UniClaw.DeepSeekProvider;

/// <summary>
/// DeepSeekProviderConfig — DeepSeek 传输层配置 (fail-fast 构造期校验)。
/// 仿 UniClaw.Core.UniBrain.PromptTemplate 的 record + 显式构造器 + DomainValidationException 风格。
/// </summary>
public sealed record class DeepSeekProviderConfig
{
    /// <summary>DeepSeek API key (必填)</summary>
    public string ApiKey { get; init; }

    /// <summary>模型名 (e.g. "deepseek-chat")</summary>
    public string Model { get; init; }

    /// <summary>API base URL (e.g. "https://api.deepseek.com")</summary>
    public string BaseUrl { get; init; }

    /// <summary>最大并发请求数 (信号量限流, 默认 4)</summary>
    public int MaxConcurrentRequests { get; init; }

    /// <summary>单请求超时秒数 (默认 30.0)</summary>
    public double RequestTimeoutSeconds { get; init; }

    /// <summary>
    /// 构造 DeepSeekProviderConfig — fail-fast 校验。
    /// </summary>
    /// <param name="ApiKey">API key (非空)</param>
    /// <param name="Model">模型名 (非空)</param>
    /// <param name="BaseUrl">Base URL (非空)</param>
    /// <param name="MaxConcurrentRequests">最大并发 (>0, 默认 4)</param>
    /// <param name="RequestTimeoutSeconds">超时秒 (>0, 默认 30.0)</param>
    public DeepSeekProviderConfig(
        string ApiKey,
        string Model,
        string BaseUrl,
        int MaxConcurrentRequests = 4,
        double RequestTimeoutSeconds = 30.0)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new DomainValidationException(nameof(ApiKey), ApiKey);
        if (string.IsNullOrWhiteSpace(Model))
            throw new DomainValidationException(nameof(Model), Model);
        if (string.IsNullOrWhiteSpace(BaseUrl))
            throw new DomainValidationException(nameof(BaseUrl), BaseUrl);
        if (MaxConcurrentRequests <= 0)
            throw new DomainValidationException(nameof(MaxConcurrentRequests), MaxConcurrentRequests);
        if (RequestTimeoutSeconds <= 0)
            throw new DomainValidationException(nameof(RequestTimeoutSeconds), RequestTimeoutSeconds);

        this.ApiKey = ApiKey;
        this.Model = Model;
        this.BaseUrl = BaseUrl;
        this.MaxConcurrentRequests = MaxConcurrentRequests;
        this.RequestTimeoutSeconds = RequestTimeoutSeconds;
    }
}
