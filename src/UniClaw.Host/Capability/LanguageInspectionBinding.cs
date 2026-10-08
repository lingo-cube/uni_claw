using System.Security.Cryptography;
using System.Text;
using UniClaw.Kernel.Capability;

namespace UniClaw.Host.Capability;

/// <summary>
/// CAP-013：语言检查的任务要求。任务可以直接给出 FixedCapabilityId，
/// 也可以只给要求，由 uni-agent 从能力集中选择；本类型不包含 Host
/// 侧 enabled 开关。
/// </summary>
public sealed record LanguageInspectionTaskRequest(
    bool Required,
    string ExpectedLanguage,
    IReadOnlyList<string>? IgnoreRoutes = null,
    string? FixedCapabilityId = null)
{
    public IReadOnlyList<string> Routes => NormalizeRoutes(IgnoreRoutes);

    internal static IReadOnlyList<string> NormalizeRoutes(IReadOnlyList<string>? routes)
    {
        var normalized = (routes ?? Array.Empty<string>())
            .Select(route => route?.Trim() ?? string.Empty)
            .ToArray();
        if (normalized.Any(route => route.Length == 0))
            throw new ArgumentException("ignore routes must be non-empty strings.", nameof(routes));
        return normalized.Distinct(StringComparer.Ordinal).ToArray();
    }
}

/// <summary>
/// CAP-013：uni-agent 的能力选择结果。Host 只接受它与任务要求完全匹配的
/// 参数；任务已经写死 capability id 时，任务选择优先，agent 结果不参与代选。
/// </summary>
public sealed record LanguageInspectionSelection(
    string CapabilityId,
    string ExpectedLanguage,
    IReadOnlyList<string>? IgnoreRoutes = null)
{
    public IReadOnlyList<string> Routes => LanguageInspectionTaskRequest.NormalizeRoutes(IgnoreRoutes);
}

/// <summary>
/// CAP-013：Host 校验后的本次任务绑定。runner 只能消费这个对象，不能自己
/// 从 Runtime Integration Registry 再选实例。
/// </summary>
public sealed record LanguageInspectionBinding(
    string BindingId,
    string RunId,
    string CapabilityId,
    string CapabilityVersion,
    ILanguageInspector Inspector,
    string ExpectedLanguage,
    IReadOnlyList<string> IgnoreRoutes);

/// <summary>
/// CAP-013：任务能力绑定工厂。Registry 只提供发现和常驻实例；本工厂把
/// 任务要求、可选 agent 选择和 Runtime identity 固定成一次局部 binding。
/// </summary>
public static class LanguageInspectionBindingFactory
{
    public static LanguageInspectionBinding? Create(
        CapabilityRegistry? registry,
        string runId,
        LanguageInspectionTaskRequest? request,
        LanguageInspectionSelection? agentSelection = null)
    {
        Require(runId, nameof(runId));
        if (request is null)
        {
            if (agentSelection is not null)
                throw new InvalidOperationException(
                    "language-inspection-selection-without-task-request");
            return null;
        }

        var expectedLanguage = Require(request.ExpectedLanguage, nameof(request.ExpectedLanguage));
        var routes = request.Routes;
        var capabilityId = request.FixedCapabilityId?.Trim();
        if (string.IsNullOrWhiteSpace(capabilityId))
        {
            if (agentSelection is null)
            {
                if (request.Required)
                    throw new InvalidOperationException(
                        "language-inspection-required-capability-not-bound");
                return null;
            }

            capabilityId = Require(agentSelection.CapabilityId, nameof(agentSelection.CapabilityId));
            if (!string.Equals(expectedLanguage, agentSelection.ExpectedLanguage?.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "language-inspection-selection-expected-language-mismatch");
            if (!routes.SequenceEqual(agentSelection.Routes, StringComparer.Ordinal))
                throw new InvalidOperationException(
                    "language-inspection-selection-ignore-routes-mismatch");
        }

        if (!string.Equals(capabilityId, LanguageInspectionProtocol.CapabilityId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"language-inspection-capability-unsupported:{capabilityId}");
        if (registry is null)
            throw new InvalidOperationException(
                "language-inspection-capability-registry-unavailable");
        if (registry.Domain != TrustDomain.RuntimeIntegration)
            throw new InvalidOperationException(
                "language-inspection-capability-wrong-trust-domain");

        var capability = registry.Resolve(capabilityId);
        if (capability is not ILanguageInspector inspector)
            throw new InvalidOperationException(
                "language-inspection-capability-not-executable");
        var description = inspector.Description;
        if (description.Scope != CapabilityScope.RuntimeIntegration
            || description.Category != CapabilityCategory.LanguageInspection)
            throw new InvalidOperationException(
                "language-inspection-capability-description-mismatch");

        var bindingId = BindingId(runId, capabilityId, expectedLanguage, routes);
        return new LanguageInspectionBinding(
            bindingId,
            runId.Trim(),
            capabilityId,
            description.Version,
            inspector,
            expectedLanguage,
            routes);
    }

    private static string BindingId(
        string runId,
        string capabilityId,
        string expectedLanguage,
        IReadOnlyList<string> routes)
    {
        var canonical = string.Join('\x1f', new[]
        {
            runId.Trim(), capabilityId, expectedLanguage.Trim().ToLowerInvariant(),
            string.Join('\x1e', routes),
        });
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return $"language-inspection-binding-{digest[..16].ToLowerInvariant()}";
    }

    private static string Require(string? value, string parameter) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A value is required.", parameter)
            : value.Trim();
}
