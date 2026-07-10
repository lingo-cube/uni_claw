using System.Collections.Immutable;
using System.Linq;
using UniClaw.Core.Traversal;

namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// ExpectedBehavior 验证逻辑 (D-E2: Verify → VerificationReport)。
/// 作为 partial class 与 ExpectedBehavior record 定义分离。
/// 5 类 blocking 验证维度 + 1 informational 参考锚点 (D-E4)。
/// </summary>
public sealed partial record class ExpectedBehavior
{
    // ── 3.7 主调度: Verify(TraversalResult) ────────────

    /// <summary>
    /// 对照 TraversalResult 验证预期结果, 返回 VerificationReport (D-E2)。
    /// 按顺序运行 6 个维度: completion, page_coverage, element_coverage,
    /// collision_proof, dfs_properties, numeric_anchor。
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

        // 3.6 numeric_anchor (informational, 不影响 AllPassed)
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

        // Required: 每个 Required 页面名应出现在 VisitedPages 中 (Contains 语义)
        foreach (var requiredPage in PageCoverage.Required)
        {
            if (requiredPage == AutoDeriveSentinel)
                continue; // auto_derive 应已通过 WithFixtureDerivation 替换

            var found = result.VisitedPages.Any(p => p.Contains(requiredPage));
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
            var found = result.VisitedPages.Any(p => p.Contains(forbiddenPage));
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

    private List<RuleResult> VerifyElementCoverage(TraversalResult result)
    {
        var requiredElements = ElementCoverage.Required
            .Where(e => e != AutoDeriveSentinel)
            .ToList();

        if (requiredElements.Count == 0)
            return new List<RuleResult>();

        // 计算 ActionHistory 中出现的 required 元素覆盖率
        // ActionRecord.Parameters 用 "element_id" (mock executor key), Action 用 "tap"
        var matchedCount = requiredElements.Count(reqId =>
            result.ActionHistory.Any(a =>
                a.Parameters.TryGetValue("element_id", out var val) &&
                val?.ToString()?.Contains(reqId) == true));

        var ratio = (double)matchedCount / requiredElements.Count;
        var passed = ratio >= ElementCoverage.RequiredRatio;
        var actual = $"{matchedCount}/{requiredElements.Count} ({ratio:P1})";

        return new List<RuleResult>
        {
            new RuleResult(
                RuleId: "element_coverage",
                Passed: passed,
                Message: passed
                    ? $"Element coverage {actual} meets threshold {ElementCoverage.RequiredRatio:P0}"
                    : $"Element coverage {actual} below threshold {ElementCoverage.RequiredRatio:P0}",
                Actual: actual)
        };
    }

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

    // ── 3.6 VerifyNumericAnchor ──────────────────────────

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
