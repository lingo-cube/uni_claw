namespace UniClaw.Kernel.Capability;

/// <summary>
/// CAP-009 — 感知健康源：一个可命名的拉式探针（组合根注入；fast 资产面、
/// 模型端等）。没有健康接口的源不注入——缺席在聚合里如实表现为
/// Unknown/诊断，不做假健康。
/// </summary>
public sealed record PerceptionHealthSource(
    string SourceName,
    Func<CapabilityHealthReport> Probe)
{
    /// <summary>源名非空且探针非空。</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(SourceName) && Probe is not null;
}

/// <summary>
/// CAP-009 — UniPerception 组合能力的 L2 可执行实现：实现双协议 marker
/// （Semantic Perception + UI Element Perception，协议-接口一致性由 registry
/// ValidateImplementation 执法）并承担**健康聚合 owner**（ICapabilityHealthCheckable，
/// 真实买方=真机运行时诊断）。协议负载词汇（SemanticObservationProposal /
/// PerceptionAssessment）留待下一个感知买方驱动冻结——本类型不预造。
/// 生命周期：纯内存聚合、无资源句柄——RL1 常驻，Registered 即稳态。
/// </summary>
public sealed class UniPerceptionCapability : ISemanticPerception, IUiElementPerception, ICapabilityHealthCheckable
{
    private readonly IReadOnlyList<PerceptionHealthSource> _sources;
    private readonly CapabilityDescription _description;

    /// <summary>组合根以命名健康源构造（缺省无源 → CheckHealth 诚实 Unknown）；
    /// 声明缺省为 canonical（组合根可 WithDescription 附加 realization 角色）。</summary>
    public UniPerceptionCapability(
        IEnumerable<PerceptionHealthSource>? healthSources = null,
        CapabilityDescription? description = null)
    {
        var sources = (healthSources ?? Array.Empty<PerceptionHealthSource>()).ToArray();
        if (sources.Any(source => source is not { IsValid: true }))
            throw new ArgumentException("health sources must be named with a probe", nameof(healthSources));
        if (sources.GroupBy(source => source.SourceName, StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new ArgumentException("duplicate health source name", nameof(healthSources));
        _sources = sources;
        _description = description ?? CanonicalDescription;
    }

    /// <summary>ICapability 自述：不可变声明（缺省 canonical）。</summary>
    public CapabilityDescription Description => _description;

    /// <summary>Kernel 拥有的 canonical Definition：与组合根既有 descriptor 同值
    ///（双协议、依赖 fast.yolo/fast.ocr/slow.text、组合 realization 角色、
    /// 依赖-关系镜像）。</summary>
    public static CapabilityDescription CanonicalDescription { get; } = new(
        "uni.perception", "1.0.0", CapabilityScope.ProductRuntime,
        new[]
        {
            new CapabilityProtocol(PerceptionProtocol.Semantic, PerceptionProtocol.Version),
            new CapabilityProtocol(PerceptionProtocol.UiElement, PerceptionProtocol.Version),
        },
        new[]
        {
            new CapabilityDependency("fast.yolo", "1.0"),
            new CapabilityDependency("fast.ocr", "1.0"),
            new CapabilityDependency("slow.text", "1.0"),
        },
        HealthStatus.Unknown,
        CapabilityCategory.CompositeProductPerception,
        new[]
        {
            new CapabilityRole(CapabilityRoleKind.ProductProtocol, PerceptionProtocol.Semantic),
            new CapabilityRole(CapabilityRoleKind.ProductProtocol, PerceptionProtocol.UiElement),
            new CapabilityRole(CapabilityRoleKind.Realization, "fast-yolo-ocr-to-slow-text"),
        },
        new[]
        {
            new CapabilityRelationship(CapabilityRelationshipKind.Requires, "fast.yolo"),
            new CapabilityRelationship(CapabilityRelationshipKind.Requires, "fast.ocr"),
            new CapabilityRelationship(CapabilityRelationshipKind.Requires, "slow.text"),
        });

    /// <summary>同表视图（ModelManagement.WithDescription 先例）：同一健康源集合
    /// 的不可变重述，仅声明不同。</summary>
    public UniPerceptionCapability WithDescription(CapabilityDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        return new(_sources, description);
    }

    /// <summary>CAP-009 — 拉式健康聚合（worst-of，诚实优先级）：
    /// 任一 Unhealthy → Unhealthy；否则任一 Degraded → Degraded；否则
    /// Healthy+Unknown 混合 → Degraded（部分可观测是降级的确定性）；全部
    /// Healthy → Healthy；无源 → Unknown。诊断逐源列出（缺席源不伪造）。</summary>
    public CapabilityHealthReport CheckHealth()
    {
        if (_sources.Count == 0)
            return new(HealthStatus.Unknown, "no health sources injected");

        var reports = new List<(string Name, CapabilityHealthReport Report)>();
        foreach (var source in _sources)
        {
            CapabilityHealthReport report;
            try
            {
                report = source.Probe() ?? new CapabilityHealthReport(HealthStatus.Unknown, "probe returned null");
            }
            catch (Exception error)
            {
                // 探针异常是观测面故障，不是产品假健康：如实记 Degraded + 诊断。
                report = new CapabilityHealthReport(HealthStatus.Degraded, $"probe threw: {error.Message}");
            }
            reports.Add((source.SourceName, report));
        }

        var status =
            reports.Any(r => r.Report.Status == HealthStatus.Unhealthy) ? HealthStatus.Unhealthy
            : reports.Any(r => r.Report.Status == HealthStatus.Degraded) ? HealthStatus.Degraded
            : reports.Any(r => r.Report.Status == HealthStatus.Unknown) ? HealthStatus.Degraded
            : HealthStatus.Healthy;
        var diagnostic = string.Join("; ", reports.Select(r =>
            $"{r.Name}={r.Report.Status}{(string.IsNullOrWhiteSpace(r.Report.Diagnostic) ? "" : $" ({r.Report.Diagnostic})")}"));
        return new(status, diagnostic);
    }
}
