namespace UniClaw.Kernel.Perception.UiHierarchy;

/// <summary>
/// PER-010 §Capture result：五类 outcome，构造期 fail-closed 执法不可折叠规则：
/// <see cref="SourceUnavailable"/> 与 <see cref="Empty"/> 永不可折叠；
/// <see cref="Partial"/> 不可解释为完整页面；<see cref="Malformed"/> 不产生部分节点。
/// </summary>
public enum UiHierarchyCaptureOutcome
{
    /// <summary>树成功取得且声明覆盖完整（可生成完整 observation）。</summary>
    Complete,

    /// <summary>采集成功、树没有节点（空采集 ≠ 世界中元素不存在）。</summary>
    Empty,

    /// <summary>有树/窗口但声明覆盖或字段不完整（带 coverage limitation，不补值）。</summary>
    Partial,

    /// <summary>超时、取消、权限/服务不可用或连接失败（无 observation，带诊断）。</summary>
    SourceUnavailable,

    /// <summary>输入存在但 XML/节点结构不可解析（fail-closed，无部分节点，保留诊断）。</summary>
    Malformed,
}

/// <summary>
/// acquisition adapter 的有界返回值（PER-010 acquisition boundary：timeout /
/// cancellation / source unavailable / malformed / empty / partial 必须可表达）。
/// 构造期用 <see cref="Validate"/> 执法 outcome 与载荷的一致性。
/// </summary>
/// <param name="Outcome">采集结果类别。</param>
/// <param name="Metadata">capture metadata（成功类必带；失败尝试可带已知的部分事实）。</param>
/// <param name="Observation">仅 Complete/Empty/Partial 携带；Malformed/SourceUnavailable 恒 null。</param>
/// <param name="Diagnostic">SourceUnavailable/Malformed 必带的可分类有界诊断。</param>
public sealed record UiHierarchyCaptureResult(
    UiHierarchyCaptureOutcome Outcome,
    CaptureMetadata? Metadata,
    UiHierarchyObservation? Observation,
    string? Diagnostic)
{
    /// <summary>
    /// outcome ↔ 载荷一致性（构造期调用；违反抛
    /// <see cref="ArgumentException"/>，不猜值、不降级）：
    /// Complete ⇒ 有效 observation、非零节点、coverage 完整；
    /// Empty ⇒ 有效 observation、零节点、零 window；
    /// Partial ⇒ 有效 observation、coverage=Partial 且带 limitation；
    /// SourceUnavailable/Malformed ⇒ 无 observation、诊断非空。
    /// </summary>
    public void Validate()
    {
        switch (Outcome)
        {
            case UiHierarchyCaptureOutcome.Complete:
                RequireObservation();
                if (Observation!.Nodes.Count == 0)
                {
                    throw new ArgumentException("Complete capture with zero nodes must be Empty outcome", nameof(Observation));
                }

                if (Observation.Metadata.Coverage.Completeness != CoverageCompleteness.CompleteWithinDeclaredSurface)
                {
                    throw new ArgumentException("Complete outcome requires complete declared coverage", nameof(Observation));
                }

                break;

            case UiHierarchyCaptureOutcome.Empty:
                RequireObservation();
                if (Observation!.Nodes.Count > 0 || Observation.Windows.Count > 0)
                {
                    throw new ArgumentException("Empty capture must carry zero nodes and zero windows", nameof(Observation));
                }

                break;

            case UiHierarchyCaptureOutcome.Partial:
                RequireObservation();
                if (Observation!.Metadata.Coverage.Completeness != CoverageCompleteness.Partial)
                {
                    throw new ArgumentException("Partial outcome requires Partial coverage with limitation", nameof(Observation));
                }

                break;

            case UiHierarchyCaptureOutcome.SourceUnavailable:
            case UiHierarchyCaptureOutcome.Malformed:
                if (Observation is not null)
                {
                    throw new ArgumentException($"{Outcome} outcome must not carry an observation (fail-closed, no partial nodes)", nameof(Observation));
                }

                if (string.IsNullOrWhiteSpace(Diagnostic))
                {
                    throw new ArgumentException($"{Outcome} outcome requires a bounded diagnostic", nameof(Diagnostic));
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(Outcome), Outcome, "unknown capture outcome");
        }

        if (Observation is not null && !Observation.IsValid)
        {
            throw new ArgumentException("observation failed capture-local/validity checks", nameof(Observation));
        }
    }

    private void RequireObservation()
    {
        if (Observation is null || Metadata is null)
        {
            throw new ArgumentException($"{Outcome} outcome requires Metadata and Observation", nameof(Observation));
        }
    }
}
