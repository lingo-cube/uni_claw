using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Kernel.Capability;

namespace UniClaw.Agent.Dsh;

public sealed class DshSemanticPerception
{
    public delegate Task<DshSlowResponse> Transport(
        string requestId, string prompt, ModelConfiguration model,
        string reasoningEffort, CancellationToken cancellationToken);

    private readonly Transport _transport;
    private readonly ModelConfiguration _model;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    static DshSemanticPerception() => Json.Converters.Add(new JsonStringEnumConverter());

    public DshSemanticPerception(Transport transport, ModelConfiguration model)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public UniPerceptionCapability CreateCapability(IEnumerable<PerceptionHealthSource>? sources = null) =>
        new UniPerceptionCapability(sources ?? []).WithSemanticExecutor(ExecuteAsync);

    private async ValueTask<SemanticPerceptionResult> ExecuteAsync(
        SemanticPerceptionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var prompt = BuildPrompt(request);
            var response = await _transport(request.RequestId, prompt, _model, "high", cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(response.Error) || string.IsNullOrWhiteSpace(response.Text))
                return SemanticPerceptionResult.Empty(request, CapabilityStatus.Failed,
                    PerceptionDisposition.Unknown, response.Error ?? "empty response");
            if (!HasHighReasoning(response.Diagnostic))
                return SemanticPerceptionResult.Empty(request, CapabilityStatus.Completed,
                    PerceptionDisposition.Unaligned, "reasoning-effort-unproven");
            var answer = JsonSerializer.Deserialize<Answer>(response.Text, Json);
            if (answer is null || !request.Candidates.Any(c => c.Key == answer.PageKey)
                && answer.PageKey != "unknown")
                return SemanticPerceptionResult.Empty(request, CapabilityStatus.Completed,
                    PerceptionDisposition.Unknown, "pageKey outside candidate catalogue");
            if (!string.IsNullOrWhiteSpace(answer.ObservedTitle)
                && !request.Current.Items.Any(i => string.Equals(i.Text, answer.ObservedTitle, StringComparison.Ordinal)))
                return SemanticPerceptionResult.Empty(request, CapabilityStatus.Completed,
                    PerceptionDisposition.Unaligned, "title not observed in current capture");
            if (answer.PageKey == "unknown")
                return SemanticPerceptionResult.Empty(request, CapabilityStatus.Completed,
                    PerceptionDisposition.Unknown, "unknown page");
            return SemanticPerceptionResult.Observed(request, answer.PageKey, answer.ObservedTitle,
                answer.Relation, answer.ExpectedMatch, source: "dsh.semantic");
        }
        catch (OperationCanceledException)
        {
            return SemanticPerceptionResult.Empty(request, CapabilityStatus.Cancelled,
                PerceptionDisposition.Unknown, "cancelled");
        }
        catch (JsonException error)
        {
            return SemanticPerceptionResult.Empty(request, CapabilityStatus.Failed,
                PerceptionDisposition.Unknown, "invalid JSON: " + error.Message);
        }
        catch (Exception error)
        {
            return SemanticPerceptionResult.Empty(request, CapabilityStatus.Failed,
                PerceptionDisposition.Unavailable, error.Message);
        }
    }

    private static bool HasHighReasoning(string? diagnostic)
    {
        try { using var json = JsonDocument.Parse(diagnostic ?? ""); return json.RootElement.GetProperty("reasoningEffort").GetString() == "high"; }
        catch { return false; }
    }

    private static string BuildPrompt(SemanticPerceptionRequest request) =>
        "只使用当前capture中的观察事实判断页面。预期与实际观察必须独立，不能把预期当事实。"
        + "只返回JSON，不要Markdown：{\"pageKey\":\"候选key或unknown\",\"observedTitle\":\"当前文字或null\","
        + "\"relation\":\"Unknown|SamePage|DifferentPage\",\"expectedMatch\":\"Unknown|Match|Mismatch|Conflicted\"}。"
        + JsonSerializer.Serialize(new { request.Current, request.Candidates, request.Correlation, request.Intent, request.Previous, request.Action, request.Budget }, Json);

    private sealed record Answer(string PageKey, string? ObservedTitle,
        SemanticPageRelation Relation, SemanticExpectedMatch ExpectedMatch);
}
