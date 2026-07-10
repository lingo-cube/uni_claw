using System.Collections.Immutable;
using System.Text.Json;
using UniClaw.Core.Domain;

namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// 结构化预期遍历结果定义 (D-E1: sealed record class + JSON 文件)。
/// Schema 契约: record 结构变更走 C-11 constitution change flow。
/// 5 类可验证维度 + 1 个 informational 参考锚点 (D-E4)。
/// </summary>
/// <param name="Scenario">场景名称 (如 "settings-full-traversal")</param>
/// <param name="Description">场景描述</param>
/// <param name="Completion">预期完成状态</param>
/// <param name="PageCoverage">预期页面覆盖率</param>
/// <param name="ElementCoverage">预期元素交互覆盖率</param>
/// <param name="CollisionProof">NodeId 碰撞分辨率验证列表 (JSON 中可为 "auto_derive" sentinel)</param>
/// <param name="DfsProperties">DFS 遍历顺序属性验证</param>
/// <param name="NumericAnchor">数值参考锚点 (informational, 非 CI-blocking)</param>
public sealed partial record class ExpectedBehavior(
    string Scenario,
    string Description,
    CompletionExpectation Completion,
    PageCoverageExpectation PageCoverage,
    ElementCoverageExpectation ElementCoverage,
    ImmutableArray<CollisionProof> CollisionProof,
    DfsPropertiesExpectation DfsProperties,
    NumericAnchor NumericAnchor)
{
    /// <summary>auto_derive sentinel 标识</summary>
    public static readonly string AutoDeriveSentinel = "auto_derive";

    // ── JSON 反序列化 ──────────────────────────────────

    /// <summary>
    /// 从 JSON 文件反序列化 ExpectedBehavior (D-E1: DomainJsonOptions 序列化约定)。
    /// 处理 collision_proof 的 "auto_derive" 字符串/数组双态:
    /// JSON 中 collision_proof 值可以是 "auto_derive" (字符串) 或 CollisionProof 数组。
    /// </summary>
    public static ExpectedBehavior FromJson(string path)
    {
        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<ExpectedBehaviorDto>(json, DomainJsonOptions.Default);
        if (dto == null)
            throw new InvalidOperationException($"Failed to deserialize ExpectedBehavior from {path}");

        // 处理 collision_proof 双态: "auto_derive" 字符串 → 空数组 + sentinel 标记, 数组 → 正常解析
        ImmutableArray<CollisionProof> collisionProofs;
        if (dto.CollisionProofKind == "auto_derive")
        {
            collisionProofs = ImmutableArray.Create<CollisionProof>();
        }
        else if (dto.CollisionProofArray != null)
        {
            collisionProofs = dto.CollisionProofArray
                .Select(c => new CollisionProof(
                    c.Text,
                    c.ExpectedDistinct,
                    c.ParentPages?.ToImmutableArray()))
                .ToImmutableArray();
        }
        else
        {
            collisionProofs = ImmutableArray<CollisionProof>.Empty;
        }

        // 处理 page_coverage.required 中的 "auto_derive" sentinel
        var pageCoverage = new PageCoverageExpectation(
            dto.PageCoverage.Required.ToImmutableArray(),
            dto.PageCoverage.Forbidden.ToImmutableArray());

        // 处理 element_coverage.required 中的 "auto_derive" sentinel
        var elementCoverage = new ElementCoverageExpectation(
            dto.ElementCoverage.Required.ToImmutableArray(),
            dto.ElementCoverage.RequiredRatio);

        return new ExpectedBehavior(
            Scenario: dto.Scenario,
            Description: dto.Description,
            Completion: new CompletionExpectation(
                dto.Completion.Success,
                dto.Completion.Reason,
                dto.Completion.FinalState),
            PageCoverage: pageCoverage,
            ElementCoverage: elementCoverage,
            CollisionProof: collisionProofs,
            DfsProperties: new DfsPropertiesExpectation(
                dto.DfsProperties.RootFirst,
                dto.DfsProperties.ParentBeforeChild,
                dto.DfsProperties.BackAfterForward),
            NumericAnchor: new NumericAnchor(
                dto.NumericAnchor.TotalSteps,
                dto.NumericAnchor.VisitedPagesCount,
                dto.NumericAnchor.ActionHistoryCount,
                dto.NumericAnchor.ElapsedSecondsMax));
    }

    /// <summary>
    /// 检查 CollisionProof 是否为 auto_derive sentinel (空数组意味着需要推导)。
    /// 注: collision_proof 在 JSON 中为 "auto_derive" 时, FromJson 将其解析为空数组。
    /// 调用 WithFixtureDerivation() 后会填充真实值。
    /// </summary>
    public bool HasCollisionProofAutoDerive => CollisionProof.IsDefaultOrEmpty;

    /// <summary>
    /// 检查 PageCoverage.Required 是否包含 auto_derive sentinel。
    /// </summary>
    public bool HasPageCoverageAutoDerive =>
        PageCoverage.Required.Length == 1 &&
        PageCoverage.Required[0] == AutoDeriveSentinel;

    /// <summary>
    /// 检查 ElementCoverage.Required 是否包含 auto_derive sentinel。
    /// </summary>
    public bool HasElementCoverageAutoDerive =>
        ElementCoverage.Required.Length == 1 &&
        ElementCoverage.Required[0] == AutoDeriveSentinel;

    // ── auto_derive 推导 ────────────────────────────────

    /// <summary>
    /// 从 StateFixture 推导替换 "auto_derive" sentinel (D-E3)。
    /// 返回新的 ExpectedBehavior, 只替换 sentinel 字段, 保留显式值。
    /// 推导逻辑:
    /// - page_coverage.required → fixture 页面名 (PageName, 排除 initialPage 的 PageName)
    /// - element_coverage.required → fixture 中所有非-readonly/back_button 元素的 Id
    /// - collision_proof → fixture 中同 Text 不同 PageId 的元素组合
    /// </summary>
    public ExpectedBehavior WithFixtureDerivation(StateFixture fixture)
    {
        // page_coverage: "auto_derive" → fixture 页面名 (排除 initialPage 的页面名)
        // D-E5: 语义标识用页面名而非 page key, VisitedPages Contains 语义匹配 PageName
        var initialPageName = fixture.Pages.TryGetValue(fixture.InitialPage, out var initPage)
            ? initPage.PageName : fixture.InitialPage;
        var pageCoverage = HasPageCoverageAutoDerive
            ? new PageCoverageExpectation(
                fixture.Pages.Values
                    .Select(p => p.PageName)
                    .Where(name => name != initialPageName)
                    .ToImmutableArray(),
                PageCoverage.Forbidden)
            : PageCoverage;

        // element_coverage: "auto_derive" → 所有非-readonly/back_button 元素 Id
        var elementCoverage = HasElementCoverageAutoDerive
            ? new ElementCoverageExpectation(
                fixture.Pages.Values
                    .SelectMany(p => p.Elements)
                    .Where(e => e.Type != "readonly" && e.Type != "back_button")
                    .Select(e => e.Id)
                    .ToImmutableArray(),
                ElementCoverage.RequiredRatio)
            : ElementCoverage;

        // collision_proof: 空 → fixture 中同 Text 不同 PageId 的组合
        var collisionProof = HasCollisionProofAutoDerive
            ? DeriveCollisionProofsFromFixture(fixture)
            : CollisionProof;

        return this with
        {
            PageCoverage = pageCoverage,
            ElementCoverage = elementCoverage,
            CollisionProof = collisionProof,
        };
    }

    /// <summary>
    /// 从 fixture 推导 CollisionProof: 找出同 Text 在不同页面上出现的元素。
    /// </summary>
    private static ImmutableArray<CollisionProof> DeriveCollisionProofsFromFixture(StateFixture fixture)
    {
        // 按 Text 分组: 同 Text 在不同 PageId 上出现的 → 碰撞候选
        var textByPages = fixture.Pages
            .SelectMany(pageKvp => pageKvp.Value.Elements
                .Where(e => e.Type != "readonly" && e.Type != "back_button")
                .Select(e => (PageId: pageKvp.Key, Text: e.Text)))
            .GroupBy(t => t.Text)
            .Where(g => g.Select(t => t.PageId).Distinct().Count() > 1);

        return textByPages
            .Select(g => new CollisionProof(
                Text: g.Key,
                ExpectedDistinct: g.Select(t => t.PageId).Distinct().Count()))
            .ToImmutableArray();
    }

    // ── DTO (仅用于 JSON 反序列化) ─────────────────────

    internal sealed class ExpectedBehaviorDto
    {
        public string Scenario { get; set; } = "";
        public string Description { get; set; } = "";
        public CompletionExpectationDto Completion { get; set; } = new();
        public PageCoverageExpectationDto PageCoverage { get; set; } = new();
        public ElementCoverageExpectationDto ElementCoverage { get; set; } = new();

        /// <summary>
        /// collision_proof 在 JSON 中可为 "auto_derive" 字符串或 CollisionProof 数组。
        /// 用 JsonElement 捕获双态, 手动解析。
        /// </summary>
        public JsonElement CollisionProofRaw { get; set; }

        /// <summary>解析 collision_proof: "auto_derive" 字符串 → CollisionProofKind="auto_derive"; 数组 → CollisionProofArray</summary>
        public string CollisionProofKind =>
            CollisionProofRaw.ValueKind == JsonValueKind.String
                ? CollisionProofRaw.GetString() ?? ""
                : "";

        public CollisionProofDto[]? CollisionProofArray =>
            CollisionProofRaw.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<CollisionProofDto[]>(
                    CollisionProofRaw.GetRawText(), DomainJsonOptions.Default)
                : null;

        public DfsPropertiesExpectationDto DfsProperties { get; set; } = new();
        public NumericAnchorDto NumericAnchor { get; set; } = new();
    }

    internal sealed class CompletionExpectationDto
    {
        public bool Success { get; set; }
        public string Reason { get; set; } = "";
        public string? FinalState { get; set; }
    }

    internal sealed class PageCoverageExpectationDto
    {
        /// <summary>Required 可包含 "auto_derive" sentinel (字符串)</summary>
        public List<string> Required { get; set; } = new();
        public List<string> Forbidden { get; set; } = new();
    }

    internal sealed class ElementCoverageExpectationDto
    {
        /// <summary>Required 可包含 "auto_derive" sentinel (字符串)</summary>
        public List<string> Required { get; set; } = new();
        public double RequiredRatio { get; set; } = 0.95;
    }

    internal sealed class CollisionProofDto
    {
        public string Text { get; set; } = "";
        public int ExpectedDistinct { get; set; }
        public List<string>? ParentPages { get; set; }
    }

    internal sealed class DfsPropertiesExpectationDto
    {
        public bool RootFirst { get; set; }
        public bool ParentBeforeChild { get; set; }
        public bool BackAfterForward { get; set; }
    }

    internal sealed class NumericAnchorDto
    {
        public int TotalSteps { get; set; }
        public int VisitedPagesCount { get; set; }
        public int ActionHistoryCount { get; set; }
        public double ElapsedSecondsMax { get; set; }
    }
}
