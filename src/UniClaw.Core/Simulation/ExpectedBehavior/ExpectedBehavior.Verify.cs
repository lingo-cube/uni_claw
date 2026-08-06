using System.Collections.Immutable;
using System.Linq;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// ExpectedBehavior 验证逻辑 (D-E2: Verify → VerificationReport)。
/// 作为 partial class 与 ExpectedBehavior record 定义分离。
/// 7 类 blocking 验证维度 + 1 informational 参考锚点 (D-E4)。
/// </summary>
public sealed partial record class ExpectedBehavior
{
    // ── 3.7 主调度: Verify(TraversalResult) ────────────

    /// <summary>
    /// 对照 TraversalResult 验证预期结果, 返回 VerificationReport (D-E2)。
    /// 按顺序运行 8 个维度: completion, page_coverage, element_coverage,
    /// collision_proof, dfs_properties, operation_rules, trace_integrity, numeric_anchor。
    /// </summary>
    public VerificationReport Verify(TraversalResult result)
    {
        var details = new List<RuleResult>();

        // 3.1 completion (blocking)
        details.AddRange(VerifyCompletion(result));

        // 3.2 page_coverage (blocking)
        details.AddRange(VerifyPageCoverage(result));

        // 3.3 element_coverage (blocking)
        details.AddRange(VerifyElementCoverage(result));

        // 3.4 collision_proof (blocking)
        details.AddRange(VerifyCollisionProof(result));

        // 3.5 dfs_properties (blocking)
        details.AddRange(VerifyDfsProperties(result));

        // 3.6 operation_rules (blocking)
        details.AddRange(VerifyOperationRules(result));

        // 3.7 trace_integrity (blocking)
        details.AddRange(VerifyTraceIntegrity(result));

        // 3.8 numeric_anchor (informational, 不影响 AllPassed)
        details.AddRange(VerifyNumericAnchor(result));

        var detailsArray = details.ToImmutableArray();

        // AllPassed: 只看非-informational 规则
        var allPassed = detailsArray
            .Where(r => !r.RuleId.StartsWith("numeric_anchor"))
            .All(r => r.Passed);

        // Summary: 逐条规则 PASS/FAIL/INFO
        var summaryParts = detailsArray.Select(r =>
        {
            if (r.RuleId.StartsWith("numeric_anchor"))
                return r.Passed
                    ? $"{r.RuleId}: INFO — {r.Message}"
                    : $"{r.RuleId}: INFO — {r.Message} (outside tolerance)";
            return r.Passed
                ? $"{r.RuleId}: PASS — {r.Message}"
                : $"{r.RuleId}: FAIL — {r.Message}";
        });
        var summary = string.Join(" | ", summaryParts);

        return new VerificationReport(allPassed, summary, detailsArray);
    }

    // ── 3.1 VerifyCompletion ─────────────────────────────

    private List<RuleResult> VerifyCompletion(TraversalResult result)
    {
        var results = new List<RuleResult>();

        // Success 检查
        var successMatch = Completion.Success == result.Success;
        results.Add(new RuleResult(
            RuleId: "completion:success",
            Passed: successMatch,
            Message: successMatch
                ? $"Success={result.Success}"
                : $"Expected Success={Completion.Success}, got {result.Success}",
            Actual: successMatch ? null : $"Success={result.Success}"));

        // Reason 检查
        var reasonMatch = Completion.Reason == result.CompletionReason;
        results.Add(new RuleResult(
            RuleId: "completion:reason",
            Passed: reasonMatch,
            Message: reasonMatch
                ? $"Reason={result.CompletionReason}"
                : $"Expected reason '{Completion.Reason}', got '{result.CompletionReason}'",
            Actual: reasonMatch ? null : $"Reason={result.CompletionReason}"));

        // FinalState 检查 (可选 — Completion.FinalState 是 string?)
        if (Completion.FinalState != null)
        {
            var finalStateName = result.FinalState.ToString();
            var finalStateMatch = finalStateName == Completion.FinalState;
            results.Add(new RuleResult(
                RuleId: "completion:final_state",
                Passed: finalStateMatch,
                Message: finalStateMatch
                    ? $"FinalState={finalStateName}"
                    : $"Expected FinalState='{Completion.FinalState}', got '{finalStateName}'",
                Actual: finalStateMatch ? null : $"FinalState={finalStateName}"));
        }

        return results;
    }

    // ── 3.2 VerifyPageCoverage ────────────────────────────

    private List<RuleResult> VerifyPageCoverage(TraversalResult result)
    {
        var results = new List<RuleResult>();

        // Required: 每个 Required 页面名应出现在 VisitedPages 中 (Contains 语义, 大小写不敏感 —
        // 动态 nodeId 经 NormalizeItemText 归一化为小写)
        foreach (var requiredPage in PageCoverage.Required)
        {
            if (requiredPage == AutoDeriveSentinel)
                continue; // auto_derive 应已通过 WithFixtureDerivation 替换

            var found = result.VisitedPages.Any(p => p.Contains(requiredPage, StringComparison.OrdinalIgnoreCase));
            results.Add(new RuleResult(
                RuleId: $"page_coverage:required:{requiredPage}",
                Passed: found,
                Message: found
                    ? $"Required page '{requiredPage}' visited"
                    : $"Required page '{requiredPage}' NOT visited",
                Actual: found ? null : $"VisitedPages does not contain '{requiredPage}'"));
        }

        // Forbidden: 每个 Forbidden 页面名不应出现在 VisitedPages 中
        foreach (var forbiddenPage in PageCoverage.Forbidden)
        {
            var found = result.VisitedPages.Any(p => p.Contains(forbiddenPage, StringComparison.OrdinalIgnoreCase));
            results.Add(new RuleResult(
                RuleId: $"page_coverage:forbidden:{forbiddenPage}",
                Passed: !found,
                Message: !found
                    ? $"Forbidden page '{forbiddenPage}' not visited"
                    : $"Forbidden page '{forbiddenPage}' was visited",
                Actual: found ? $"VisitedPages contains '{forbiddenPage}'" : null));
        }

        return results;
    }

    // ── 3.3 VerifyElementCoverage ─────────────────────────

    /// <summary>
    /// 元素覆盖维度 (D-E4) — 按 <see cref="ElementCoverageExpectation.Mode"/> 双路分流
    /// (simulation-test-quality-hardening 设计 §3 + elementcoverage-mode-cleanup 移除 legacy_ratio):
    /// <list type="bullet">
    /// <item><b>Exact</b>: 精确集合差 (D-7 等值非子串) — pass iff missed ⊆ AllowedMisses 且 extra=∅。完备性证明唯一权威。</item>
    /// <item><b>Subset</b>: 过游走 guard (D-6) — TargetFound 命中 target tap 后不得再 tap 新元素。</item>
    /// </list>
    /// 单一聚合规则 <c>element_coverage:completeness</c> (设计 §3.2); AllPassed 视为 blocking。
    /// </summary>
    private List<RuleResult> VerifyElementCoverage(TraversalResult result)
    {
        return ElementCoverage.Mode switch
        {
            ElementCoverageMode.Subset => VerifyElementCoverageSubset(result),
            _ => VerifyElementCoverageExact(result),
        };
    }

    /// <summary>
    /// Exact 路径: 精确集合差 (D-7)。tapped 集合用 element_id 等值 HashSet (非子串 Contains);
    /// back_button tap (id 含 "back") 与失败 tap ("none") 排除 — 它们是导航/失败, 非内容覆盖, 不产生假 extra。
    /// pass iff missed ⊆ AllowedMisses.Ids 且 extra=∅ (D-4)。
    /// </summary>
    private List<RuleResult> VerifyElementCoverageExact(TraversalResult result)
    {
        var required = ElementCoverage.Required
            .Where(e => e != AutoDeriveSentinel)
            .ToList();

        // 无 required (subset-only 场景的 exact 兜底) → 空覆盖 vacuously pass
        if (required.Count == 0)
        {
            return new List<RuleResult>
            {
                new(RuleId: "element_coverage:completeness", Passed: true,
                    Message: "Element coverage: no required elements (exact mode, vacuously complete).",
                    Actual: "matched=0/0; missed=[]; extra=[]"),
            };
        }

        var requiredSet = required.ToHashSet(StringComparer.Ordinal);
        var tapped = ExtractTappedElementIds(result);

        var matched = required.Count(req => tapped.Contains(req));
        var missed = required.Where(req => !tapped.Contains(req)).ToList();
        var extra = tapped.Where(id => !requiredSet.Contains(id)).ToList();

        var allowedIds = ElementCoverage.AllowedMisses
            .Select(m => m.Id)
            .ToHashSet(StringComparer.Ordinal);
        var unallowedMissed = missed.Where(m => !allowedIds.Contains(m)).ToList();

        bool passed = unallowedMissed.Count == 0 && extra.Count == 0;

        var missedStr = string.Join(", ", missed);
        var extraStr = string.Join(", ", extra);
        var actual = $"matched={matched}/{required.Count}; missed=[{missedStr}]; extra=[{extraStr}]";

        string message;
        if (passed)
        {
            message = missed.Count > 0
                ? $"Element coverage complete (exact): matched {matched}/{required.Count}, missed [{missedStr}] all within allowedMisses."
                : $"Element coverage complete (exact): matched {matched}/{required.Count}.";
        }
        else
        {
            var parts = new List<string>();
            if (unallowedMissed.Count > 0)
                parts.Add($"missed [{string.Join(", ", unallowedMissed)}] not covered/allowed");
            if (extra.Count > 0)
                parts.Add($"extra [{extraStr}] tapped outside required universe (phantom tap)");
            message = $"Element coverage incomplete (exact): matched {matched}/{required.Count} — {string.Join("; ", parts)}.";
        }

        return new List<RuleResult>
        {
            new(RuleId: "element_coverage:completeness", Passed: passed, Message: message, Actual: actual),
        };
    }

    /// <summary>
    /// Subset 路径 (D-6 过游走 guard): TargetFound 计划命中 target 后不得再 tap 新元素。
    /// 定位 target tap (element_id 规范化含 TargetName), 其后只允许 back/scroll/exit (无 element_id 的 action) 或重 tap target;
    /// 任何对新元素的 tap = 过游走 = FAIL。与 completion:target_found 正交 (证「停对了后没乱动」)。
    /// </summary>
    private List<RuleResult> VerifyElementCoverageSubset(TraversalResult result)
    {
        var targetName = ElementCoverage.TargetName;

        // 无 TargetName: subset guard 无法定位 target → fail-fast 报错 (derivation 应已捕获)
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return new List<RuleResult>
            {
                new(RuleId: "element_coverage:completeness", Passed: false,
                    Message: "Subset over-traversal guard requires TargetName (from CompletionPolicy); none set.",
                    Actual: "target_name=null"),
            };
        }

        var targetNorm = NormalizeForTargetMatch(targetName);
        int targetIndex = -1;
        string? targetId = null;
        for (int i = 0; i < result.ActionHistory.Length; i++)
        {
            if (!result.ActionHistory[i].Parameters.TryGetValue("element_id", out var val))
                continue;
            var id = val?.ToString();
            if (!string.IsNullOrWhiteSpace(id) && id != "none" &&
                NormalizeForTargetMatch(id).Contains(targetNorm, StringComparison.Ordinal))
            {
                targetIndex = i;
                targetId = id;
                break;
            }
        }

        if (targetIndex < 0)
        {
            // MarkAndStop: target found during page analysis but NOT tapped (engine halts without tapping it).
            // Over-traversal is structurally impossible (engine stopped at find), so the guard passes
            // iff completion confirms the target was actually reached. ExecuteThenStop taps the target
            // and takes the targetIndex path above instead.
            bool targetReached = result.CompletionReason == TraversalResult.Reasons.TargetFound;
            return new List<RuleResult>
            {
                new(RuleId: "element_coverage:completeness", Passed: targetReached,
                    Message: targetReached
                        ? $"Subset guard: target '{targetName}' reached via MarkAndStop (not tapped); engine halted — no over-traversal possible."
                        : $"Subset guard: target '{targetName}' was never tapped and completion did not confirm target_found (reason={result.CompletionReason}).",
                    Actual: $"target_name={targetName}; target_tapped=false; completion={result.CompletionReason}"),
            };
        }

        // 扫描 target tap 之后: 任何对新元素的 tap (非 back/scroll/exit, 非重 tap target) = 过游走
        var violators = new List<string>();
        for (int i = targetIndex + 1; i < result.ActionHistory.Length; i++)
        {
            if (!result.ActionHistory[i].Parameters.TryGetValue("element_id", out var val))
                continue; // swipe/back/wait/input_text: 无 element_id → 允许 (导航/滚动)
            var id = val?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(id) || id == "none")
                continue;
            if (id.Contains("back", StringComparison.OrdinalIgnoreCase))
                continue; // back 导航 → 允许
            if (id == targetId)
                continue; // 重 tap target → 允许
            violators.Add(id);
        }

        bool passed = violators.Count == 0;
        var actual = $"target='{targetId}' tapped at step {targetIndex}; post_target_taps=[{string.Join(", ", violators)}]";

        return new List<RuleResult>
        {
            new(RuleId: "element_coverage:completeness", Passed: passed,
                Message: passed
                    ? $"Subset over-traversal guard passed: no new element tap after target '{targetName}'."
                    : $"Subset over-traversal guard FAILED: tapped new element(s) [{string.Join(", ", violators)}] after target '{targetName}' (over-traversal).",
                Actual: actual),
        };
    }

    /// <summary>
    /// 从 ActionHistory 提取实际 tap 过的内容元素 id 精确集合 (D-7 等值, 非子串)。
    /// 排除: 无 element_id 的 action (swipe/back/wait)、失败 tap ("none")、back_button 导航 tap (id 含 "back")。
    /// 后两者非内容覆盖, 不应产生假 extra。
    /// </summary>
    private static HashSet<string> ExtractTappedElementIds(TraversalResult result)
    {
        var tapped = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in result.ActionHistory)
        {
            if (!a.Parameters.TryGetValue("element_id", out var val))
                continue;
            var id = val?.ToString();
            if (string.IsNullOrWhiteSpace(id) || id == "none")
                continue;
            if (id.Contains("back", StringComparison.OrdinalIgnoreCase))
                continue; // back_button 导航 tap, 非内容覆盖
            tapped.Add(id);
        }
        return tapped;
    }

    /// <summary>
    /// 规范化元素 id/名称用于 target 匹配: 去除非字母数字字符 + 小写。
    /// 弥合 element_id ("App_15"/"dark_mode") 与 CompletionPolicy.TargetName ("App15"/"Dark mode") 的命名差异。
    /// </summary>
    private static string NormalizeForTargetMatch(string s)
        => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    // ── 3.4 VerifyCollisionProof ──────────────────────────

    private List<RuleResult> VerifyCollisionProof(TraversalResult result)
    {
        var results = new List<RuleResult>();

        foreach (var proof in CollisionProof)
        {
            // 按 Text 在 VisitedPages 中分组, 计算包含此 Text 的 distinct 页面数量
            var matchingPages = result.VisitedPages
                .Where(p => p.Contains(proof.Text))
                .ToList();

            // 如果有 ParentPages 限制, 只统计指定页面中的
            if (proof.ParentPages != null && !proof.ParentPages.Value.IsDefaultOrEmpty)
            {
                matchingPages = matchingPages
                    .Where(p => proof.ParentPages.Value.Any(pp => p.Contains(pp)))
                    .ToList();
            }

            var distinctCount = matchingPages.Distinct().Count();
            var passed = distinctCount >= proof.ExpectedDistinct;

            results.Add(new RuleResult(
                RuleId: $"collision_proof:{proof.Text}",
                Passed: passed,
                Message: passed
                    ? $"Text '{proof.Text}' has {distinctCount} distinct nodes (expected {proof.ExpectedDistinct})"
                    : $"Expected {proof.ExpectedDistinct} distinct nodes with text '{proof.Text}', found {distinctCount}",
                Actual: passed ? null : $"distinct_count={distinctCount}, expected={proof.ExpectedDistinct}"));
        }

        return results;
    }

    // ── 3.5 VerifyDfsProperties ──────────────────────────

    private List<RuleResult> VerifyDfsProperties(TraversalResult result)
    {
        var results = new List<RuleResult>();
        var pages = result.VisitedPages;

        // RootFirst: VisitedPages[0] 包含 "root"
        if (DfsProperties.RootFirst)
        {
            var rootFirst = pages.Length > 0 && pages[0].Contains("root");
            results.Add(new RuleResult(
                RuleId: "dfs_properties:root_first",
                Passed: rootFirst,
                Message: rootFirst
                    ? $"Root page visited first: '{pages[0]}'"
                    : $"Root page NOT visited first, first page: '{(pages.Length > 0 ? pages[0] : "none")}'",
                Actual: rootFirst ? null : $"first_page={pages[0]}"));
        }

        // ParentBeforeChild: 简化检查 — root/home 在前面, 子页面在后面
        if (DfsProperties.ParentBeforeChild)
        {
            var parentBeforeChild = true;
            var rootIndex = Enumerable.Range(0, pages.Length)
                .FirstOrDefault(i => pages[i].Contains("root") || pages[i].Contains("home"));

            if (rootIndex >= 0 && rootIndex < pages.Length)
            {
                // 所有子页面 (不包含 "root" 或 "home") 必须在 root 之后
                var childBeforeRoot = pages.Take(rootIndex)
                    .Any(p => !p.Contains("root") && !p.Contains("home") && !p.Contains("back"));
                parentBeforeChild = !childBeforeRoot;
            }

            results.Add(new RuleResult(
                RuleId: "dfs_properties:parent_before_child",
                Passed: parentBeforeChild,
                Message: parentBeforeChild
                    ? "Parent pages appear before child pages in sequence"
                    : "Child page appears before parent page in sequence (DFS order violation)",
                Actual: parentBeforeChild ? null : "DFS order violation detected"));
        }

        // BackAfterForward: DFS 中 back 操作应在 forward 操作之后出现
        // ActionRecord 用 Action="tap"/"back" (mock executor), element_id key
        if (DfsProperties.BackAfterForward)
        {
            var forwardActions = result.ActionHistory
                .Where(a => a.Action == "tap" &&
                            a.Parameters.TryGetValue("element_id", out var id) &&
                            id?.ToString()?.Contains("back") == false)
                .ToList();

            var backActions = result.ActionHistory
                .Where(a => a.Action == "back" ||
                            (a.Action == "tap" &&
                             a.Parameters.TryGetValue("element_id", out var id) &&
                             id?.ToString()?.Contains("back") == true))
                .ToList();

            // DFS 中 back 操作数量应接近 forward 操作 (进入子页面再退回)
            var backAfterForward = backActions.Count > 0 && forwardActions.Count > 0;
            results.Add(new RuleResult(
                RuleId: "dfs_properties:back_after_forward",
                Passed: backAfterForward,
                Message: backAfterForward
                    ? $"Forward/back pattern found ({forwardActions.Count} forward, {backActions.Count} back)"
                    : "No forward/back pattern found in action history",
                Actual: backAfterForward ? null : $"forward={forwardActions.Count}, back={backActions.Count}"));
        }

        return results;
    }

    // ── 3.6 VerifyOperationRules ─────────────────────────

    /// <summary>
    /// 验证操作规则 (D-E4: operation_rules 维度)。
    /// <list type="number">
    /// <item><b>depth_first_order</b>: DFS 栈规程检查 — 遍历 ActionHistory，tap(非back元素)=push(+1)，back=pop(-1)，深度永不负数 + 至少一次回退。
    /// 与 dfs_properties:back_after_forward（仅检查两者都存在）正交互补。</item>
    /// <item><b>no_duplicate_actions</b>: 同 element_id 连续重复 ≤ NoDuplicateActionsMax。</item>
    /// </list>
    /// </summary>
    private List<RuleResult> VerifyOperationRules(TraversalResult result)
    {
        var results = new List<RuleResult>();

        if (OperationRules == null)
            return results;

        // depth_first_order: DFS 栈规程检查
        if (OperationRules.DepthFirstOrder)
        {
            var depth = 0;
            var hasBack = false;
            var hasForward = false;
            var underflowAt = -1;

            for (int i = 0; i < result.ActionHistory.Length; i++)
            {
                var a = result.ActionHistory[i];
                var isBackAction = a.Action == "back" ||
                    (a.Action == "tap" &&
                     a.Parameters.TryGetValue("element_id", out var bid) &&
                     bid?.ToString()?.Contains("back") == true);
                var isForwardAction = a.Action == "tap" &&
                    a.Parameters.TryGetValue("element_id", out var fid) &&
                    fid?.ToString()?.Contains("back") == false;

                if (isBackAction)
                {
                    depth--;
                    hasBack = true;
                    if (depth < 0 && underflowAt < 0)
                        underflowAt = i;
                }
                else if (isForwardAction)
                {
                    depth++;
                    hasForward = true;
                }
                // non-tap/non-back actions (swipe, etc.) don't affect depth
            }

            var depthFirstOk = hasForward && hasBack && underflowAt < 0;
            results.Add(new RuleResult(
                RuleId: "operation_rules:depth_first_order",
                Passed: depthFirstOk,
                Message: depthFirstOk
                    ? $"DFS stack discipline ok: depth ended at {depth}, {result.ActionHistory.Count(a => a.Action == "back" || (a.Action == "tap" && a.Parameters.TryGetValue("element_id", out var id) && id?.ToString()?.Contains("back") == true))} back(s)"
                    : !hasForward
                        ? "No forward (tap) actions in history — engine never explored"
                        : !hasBack
                            ? "No back actions in history — engine never returned from any branch (single-branch-only traversal)"
                            : $"Stack underflow at step {underflowAt}: back before forward (DFS violation)",
                Actual: depthFirstOk ? null
                    : !hasForward ? "forward_count=0"
                    : !hasBack ? "back_count=0"
                    : $"underflow_at_step={underflowAt}, depth_went_negative"));
        }

        // no_duplicate_actions: 同 element_id 连续重复 ≤ NoDuplicateActionsMax
        if (OperationRules.NoDuplicateActionsMax > 0)
        {
            var maxConsecutive = 0;
            var currentElement = "";
            var currentCount = 0;
            var worstElement = "";

            foreach (var a in result.ActionHistory)
            {
                if (a.Parameters.TryGetValue("element_id", out var val))
                {
                    var elemId = val?.ToString() ?? "";
                    if (elemId == currentElement)
                    {
                        currentCount++;
                    }
                    else
                    {
                        if (currentCount > maxConsecutive)
                        {
                            maxConsecutive = currentCount;
                            worstElement = currentElement;
                        }
                        currentElement = elemId;
                        currentCount = 1;
                    }
                }
            }
            if (currentCount > maxConsecutive)
            {
                maxConsecutive = currentCount;
                worstElement = currentElement;
            }

            var dupsOk = maxConsecutive <= OperationRules.NoDuplicateActionsMax;
            results.Add(new RuleResult(
                RuleId: "operation_rules:no_duplicate_actions",
                Passed: dupsOk,
                Message: dupsOk
                    ? $"Max consecutive repeats {maxConsecutive} ≤ {OperationRules.NoDuplicateActionsMax}"
                    : $"Max consecutive repeats {maxConsecutive} > {OperationRules.NoDuplicateActionsMax} (element '{worstElement}')",
                Actual: dupsOk ? null : $"max_consecutive={maxConsecutive}, element={worstElement}"));
        }

        return results;
    }

    // ── 3.7 VerifyTraceIntegrity ────────────────────────

    /// <summary>
    /// 验证 Trace 数据完整性 (D-E4: trace_integrity 维度)。
    /// <list type="number">
    /// <item><b>span_types_present</b>: Trace 中必须出现 RequiredSpanTypes 中每个 SpanType。</item>
    /// <item><b>page_transitions_recorded</b>: Trace 中 PageTransitionType != null 的记录数 ≥ MinPageTransitions。</item>
    /// </list>
    /// </summary>
    private List<RuleResult> VerifyTraceIntegrity(TraversalResult result)
    {
        var results = new List<RuleResult>();

        if (TraceIntegrity == null)
            return results;

        // span_types_present: 为 RequiredSpanTypes 中每个类型产出一条 RuleResult
        foreach (var requiredType in TraceIntegrity.RequiredSpanTypes)
        {
            var found = result.Trace.Any(t => t.SpanTypes.Contains(requiredType));
            results.Add(new RuleResult(
                RuleId: $"trace_integrity:span_type:{requiredType}",
                Passed: found,
                Message: found
                    ? $"SpanType.{requiredType} present in trace"
                    : $"SpanType.{requiredType} NOT found in any trace record",
                Actual: found ? null : $"SpanType.{requiredType} missing"));
        }

        // page_transitions_recorded
        if (TraceIntegrity.MinPageTransitions > 0)
        {
            var transitionCount = result.Trace.Count(t => t.PageTransitionType != null);
            var ptOk = transitionCount >= TraceIntegrity.MinPageTransitions;
            results.Add(new RuleResult(
                RuleId: "trace_integrity:page_transitions",
                Passed: ptOk,
                Message: ptOk
                    ? $"Page transitions recorded {transitionCount} ≥ {TraceIntegrity.MinPageTransitions}"
                    : $"Page transitions recorded {transitionCount} < {TraceIntegrity.MinPageTransitions}",
                Actual: ptOk ? null : $"transition_count={transitionCount}, min={TraceIntegrity.MinPageTransitions}"));
        }

        return results;
    }

    // ── 3.8 VerifyNumericAnchor ──────────────────────────

    private List<RuleResult> VerifyNumericAnchor(TraversalResult result)
    {
        var results = new List<RuleResult>();
        const double Tolerance = 0.05; // ±5%

        // TotalSteps
        var stepsExpected = NumericAnchor.TotalSteps;
        var stepsMin = stepsExpected * (1 - Tolerance);
        var stepsMax = stepsExpected * (1 + Tolerance);
        var stepsPassed = result.TotalSteps >= stepsMin && result.TotalSteps <= stepsMax;
        results.Add(new RuleResult(
            RuleId: "numeric_anchor:total_steps",
            Passed: stepsPassed,
            Message: stepsPassed
                ? $"TotalSteps={result.TotalSteps} within ±5% of {stepsExpected}"
                : $"TotalSteps={result.TotalSteps} (expected {stepsExpected} ±5%={stepsMin:F1}~{stepsMax:F1})",
            Actual: $"{result.TotalSteps} (expected {stepsExpected} ±5%={stepsMin:F1}~{stepsMax:F1})"));

        // VisitedPagesCount
        var pagesExpected = NumericAnchor.VisitedPagesCount;
        var pagesMin = pagesExpected * (1 - Tolerance);
        var pagesMax = pagesExpected * (1 + Tolerance);
        var pagesPassed = result.VisitedPages.Length >= pagesMin && result.VisitedPages.Length <= pagesMax;
        results.Add(new RuleResult(
            RuleId: "numeric_anchor:visited_pages_count",
            Passed: pagesPassed,
            Message: pagesPassed
                ? $"VisitedPagesCount={result.VisitedPages.Length} within ±5% of {pagesExpected}"
                : $"VisitedPagesCount={result.VisitedPages.Length} (expected {pagesExpected} ±5%={pagesMin:F1}~{pagesMax:F1})",
            Actual: $"{result.VisitedPages.Length} (expected {pagesExpected} ±5%={pagesMin:F1}~{pagesMax:F1})"));

        // ActionHistoryCount
        var actionsExpected = NumericAnchor.ActionHistoryCount;
        var actionsMin = actionsExpected * (1 - Tolerance);
        var actionsMax = actionsExpected * (1 + Tolerance);
        var actionsPassed = result.ActionHistory.Length >= actionsMin && result.ActionHistory.Length <= actionsMax;
        results.Add(new RuleResult(
            RuleId: "numeric_anchor:action_history_count",
            Passed: actionsPassed,
            Message: actionsPassed
                ? $"ActionHistoryCount={result.ActionHistory.Length} within ±5% of {actionsExpected}"
                : $"ActionHistoryCount={result.ActionHistory.Length} (expected {actionsExpected} ±5%={actionsMin:F1}~{actionsMax:F1})",
            Actual: $"{result.ActionHistory.Length} (expected {actionsExpected} ±5%={actionsMin:F1}~{actionsMax:F1})"));

        // ElapsedSecondsMax
        var elapsedPassed = result.ElapsedSeconds <= NumericAnchor.ElapsedSecondsMax;
        results.Add(new RuleResult(
            RuleId: "numeric_anchor:elapsed_seconds_max",
            Passed: elapsedPassed,
            Message: elapsedPassed
                ? $"ElapsedSeconds={result.ElapsedSeconds:F2}s <= {NumericAnchor.ElapsedSecondsMax}s"
                : $"ElapsedSeconds={result.ElapsedSeconds:F2}s > {NumericAnchor.ElapsedSecondsMax}s max",
            Actual: $"{result.ElapsedSeconds:F2}s (max {NumericAnchor.ElapsedSecondsMax}s)"));

        return results;
    }
}
