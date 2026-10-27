using UniClaw.Kernel.Control;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Assurance;

public sealed partial class RuntimeAssurance
{
    /// <summary>
    /// Consume current belief/evidence before the visual predicate path. This
    /// Owner judges page identity, post-dispatch capture and typed checked state;
    /// null means visual verification still needs its scoped input.
    /// </summary>
    internal PostActionEffectVerification? TryVerifyPostActionFromBelief(
        TargetSpec target,
        PostActionBeliefFacts? current,
        DateTimeOffset? dispatchTime, string? receiptId = null)
    {
        if (receiptId is not null && _effectCorrectionReviews.Any(r => r.ReceiptId == receiptId
            && r.Resolution is "Unresolved" or "DependenciesUnknown"))
            return RecordPostActionVerification(new(current?.RevisionId ?? "unknown", target, false,
                [new("post-action-consumption-basis-not-retracted", false)], "post-action-consumption-basis-retracted"));
        if (current is { PageCaptureTime: { } captureTime } captured)
        {
            var confirmed = !captured.HasObservationNeed && captured.PageContainerId is not null;
            var afterDispatch = dispatchTime is { } dispatch && captureTime > dispatch;
            if (!confirmed || !afterDispatch)
                return RecordPostActionVerification(new PostActionEffectVerification(
                    captured.RevisionId, target, false,
                    new[] { new AssuranceCheck("post-action-page-identity-confirmed", confirmed),
                        new AssuranceCheck("post-action-capture-after-dispatch", afterDispatch) },
                    !confirmed ? "post-action-page-identity-unconfirmed" : "post-action-capture-not-after-dispatch"));
        }

        if (target.DesiredState is null)
            return null;

        var belief = current
            ?? throw new InvalidOperationException("post-action verification requires current belief");
        if (dispatchTime is not { } dispatched)
            return InsufficientTypedVerification(belief.RevisionId, target, "dispatch-time-missing");
        return RecordPostActionVerification(
            VerifyTypedPostActionEffect(target, belief, dispatched));
    }

    private PostActionEffectVerification RecordPostActionVerification(
        PostActionEffectVerification verification)
    {
        ArgumentNullException.ThrowIfNull(verification);
        _postActionVerifications.Add(verification);
        return verification;
    }

    /// <summary>
    /// 不变量 43 的 Assurance 执法点：正确 context 的输入本身不构成验证。
    /// 必须至少有 accepted Evidence、完成一次 reconciliation，并在当前 scoped
    /// Slice 中重新观察到唯一目标；有 DesiredState 时还必须与其相等。
    /// </summary>
    internal PostActionEffectVerification VerifyPostActionEffect(
        PostActionEffectVerificationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var candidates = input.Slice.Occurrences
            .Where(o => o.Role == input.Target.Role
                && (input.Target.SemanticDescriptor is null
                    || o.SemanticDescriptor == input.Target.SemanticDescriptor))
            .ToList();
        var observedState = candidates.Count == 1 ? candidates[0].State : null;
        // A navigation tap legitimately removes the tapped row from the new
        // hierarchy.  The old target is only allowed to disappear when the
        // caller proved it was unique before dispatch and the route fingerprint
        // changed across the post-action reconciliation.  This keeps a stale
        // or incomplete hierarchy fail-closed while making multi-step
        // navigation verifiable.
        var overlayDismissed = input.Expected.Changes.Contains(ChangePredicate.OverlayDismissed)
            && VerifyOverlayDismissal(input);
        var targetObservedOrNavigationTransition = candidates.Count == 1
            || input.TargetDisappearedAfterRouteChange || overlayDismissed;
        // PER-014 R3：DesiredState 已是 typed CheckedState 值域；occurrence
        // presentation state 经严格映射比较（Unknown 不判 satisfied，零折叠）。
        var observedChecked = CheckedSemantics.FromPresentation(observedState);
        var checks = new List<AssuranceCheck>
        {
            new("post-action-evidence-accepted", input.ProcessedObservations.Any(
                r => r.Admission.Decision == AdmissionDecision.Accepted)),
            new("post-action-reconciled", input.ProcessedObservations.Any(
                r => r.ResultingRevision is not null)),
            new("post-action-target-unique", targetObservedOrNavigationTransition),
            new("post-action-desired-state-satisfied",
                input.Target.DesiredState is null
                || (observedChecked is { } observed && observed == input.Target.DesiredState)),
        };
        if (input.Expected.Changes.Contains(ChangePredicate.OverlayDismissed))
            checks.Add(new AssuranceCheck("post-action-overlay-dismissed", overlayDismissed));
        if (input.TargetDisappearedAfterRouteChange)
            checks.Add(new AssuranceCheck("post-action-navigation-transition", true));
        // PROTOCOL-FIX-002 / ADR-0049：判定对照物 = 显式期望谓词求值（非
        // 身份连续、非 effect-class 硬编码）。RouteStable：路由证据两侧均在
        // 场且相等；ContentChanged：可见内容集变化证据为 true；StateBecomes：
        // 目标应处于何态（"present" = 可观察在位，navigation transition 合法
        // 移除目标时豁免）。证据缺席不判满足（fail closed）。
        if (input.Expected.Changes.Contains(ChangePredicate.RouteStable))
            checks.Add(new AssuranceCheck("post-action-route-unchanged",
                input.Change is { } change
                && change.RouteBefore is not null
                && change.RouteAfter is not null
                && string.Equals(change.RouteBefore, change.RouteAfter, StringComparison.Ordinal)));
        if (input.Expected.Changes.Contains(ChangePredicate.ContentChanged))
            checks.Add(new AssuranceCheck("post-action-content-transition",
                input.Change?.VisibleContentChanged == true));
        if (input.Expected.States.Count > 0)
            checks.Add(new AssuranceCheck("post-action-expected-state",
                input.Expected.States.All(s => ExpectedStateSatisfied(s, input))));
        var failed = checks.FirstOrDefault(c => !c.Passed)?.Name;
        var rejection = failed == "post-action-desired-state-satisfied"
            ? "post-action-desired-state-not-satisfied"
            : failed;
        var judgment = new PostActionEffectVerification(
            input.Slice.SourceRevisionId,
            input.Target,
            IsVerified: rejection is null,
            checks,
            rejection);
        _postActionVerifications.Add(judgment);
        return judgment;
    }

    /// <summary>
    /// 状态型期望谓词求值（PROTOCOL-FIX-002 / ADR-0049）。subject = 验证目标
    /// role 时：occurrence presentation state 经 <see cref="CheckedSemantics"/>
    /// 严格映射后与期望值比较（映射不出 = Unknown，不判满足，fail closed）；
    /// 保留字 "present" = 可观察在位（恰一匹配 occurrence；navigation
    /// transition 合法移除目标时由导航证明豁免）；其余 subject → Slice
    /// ScopedClaims 的 claim value 严格相等。
    /// </summary>
    private static bool ExpectedStateSatisfied(
        StateBecomes expectation,
        PostActionEffectVerificationInput input)
    {
        if (expectation.Value == "present")
        {
            if (input.TargetDisappearedAfterRouteChange)
                return true;
            return input.Slice.Occurrences.Count(o =>
                o.Role == expectation.Subject
                && (input.Target.SemanticDescriptor is null
                    || o.SemanticDescriptor == input.Target.SemanticDescriptor)) == 1;
        }

        if (expectation.Subject == input.Target.Role)
        {
            return CheckedSemantics.FromPresentation(
                input.Slice.Occurrences.FirstOrDefault(
                    o => o.Role == expectation.Subject)?.State) is { } observed
                && Enum.TryParse<CheckedState>(expectation.Value, out var expected)
                && observed == expected;
        }

        return input.Slice.ScopedClaims.TryGetValue(expectation.Subject, out var claim)
            && claim == expectation.Value;
    }

    /// <summary>
    /// typed 验证路由执行（PER-014 R2）：经 <see cref="SemanticCheckedResolver"/>
    /// （R1 只读缝）对 post-action belief 解析目标 role 的 typed checked 值。
    /// 四门等价重述：
    /// ① 权威域 = DesiredState ≠ null（Click 型非状态权威域 → 视觉路径）；
    /// ② 身份唯一 = occurrence 解析恰一（缝内零/多候选 → Unknown → insufficient）；
    /// ③ 属性有效 = parser 能力/ExactProof 执法通过，且 resolver 确认同一
    ///    capture/node 的 checkable=Observed(true)；raw checked 单独不足以证明；
    ///    capability 缺席 → Unsupported → fail-closed insufficient；
    /// ④ 时序 = capture timestamp 严格晚于 dispatchTime。
    /// 值判定：CheckedState 与 typed DesiredState 相等才 verified；Partial
    /// 不通过且不回视觉（explicit insufficient-evidence）；Unchecked/Checked
    /// 不等 = 如实未验证（mismatch 留档）。任一 Unknown/Unsupported 或时序
    /// 门失败都返回 insufficient（fail-closed，不猜值）。
    /// </summary>
    private static PostActionEffectVerification VerifyTypedPostActionEffect(
        TargetSpec target,
        PostActionBeliefFacts current,
        DateTimeOffset dispatchTime)
    {
        var resolution = current.Checked;

        // 门②/③：occurrence 唯一解析 + typed claim 在案。Unknown（无 claim /
        // 歧义 / capture 不可判）与 Unsupported（capability 缺席）一律
        // fail-closed 为语义证据不足；不可降级到视觉 State。
        if (resolution is null || !resolution.IsObserved)
            return InsufficientTypedVerification(current.RevisionId, target,
                resolution?.Value.Reason ?? "semantic-checked-unresolved");

        // 门④：时序——capture 必须严格晚于 dispatch（事后新鲜度）。
        if (resolution.CaptureTimestamp is not { } captureTime || captureTime <= dispatchTime)
            return InsufficientTypedVerification(current.RevisionId, target, "capture-not-after-dispatch");

        var resolved = resolution.Value.Value!;
        if (resolved == CheckedState.Partial)
        {
            // Partial：tri-state 语义，不得折叠为 off/true——显式证据不足。
            return new PostActionEffectVerification(
                current.RevisionId, target, IsVerified: false,
                new[]
                {
                    new AssuranceCheck("typed-route-four-gates", true),
                    new AssuranceCheck("typed-checked-matches-desired", false),
                },
                $"typed-route: insufficient-evidence (partial at {resolution.CaptureId})");
        }

        var verified = resolved == target.DesiredState;
        return new PostActionEffectVerification(
            current.RevisionId, target, verified,
            new[]
            {
                new AssuranceCheck("typed-route-four-gates", true),
                new AssuranceCheck("typed-checked-matches-desired", verified),
            },
            verified ? null : "post-action-desired-state-not-satisfied");
    }

    private static PostActionEffectVerification InsufficientTypedVerification(
        string revisionId,
        TargetSpec target,
        string reason) =>
        new(
            revisionId,
            target,
            IsVerified: false,
            new[]
            {
                new AssuranceCheck("typed-route-four-gates", false),
                new AssuranceCheck("typed-checked-matches-desired", false),
            },
            $"typed-route: insufficient-evidence ({reason})");

}
