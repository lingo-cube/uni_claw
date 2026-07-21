using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniClaw.Core.Domain;
using UniClaw.Core.Graph.Models;
using UniClaw.Core.Observability;
using UniClaw.Core.Simulation.Scroll;

namespace UniClaw.Core.Simulation.ExpectedBehavior;

/// <summary>
/// 结构化预期遍历结果定义 (D-E1: sealed record class + JSON 文件)。
/// Schema 契约: record 结构变更走 C-11 constitution change flow。
/// 7 类可验证维度 (completion, page_rules, node_coverage, collision_proof, dfs_properties, numeric_anchor, operation_rules) + 1 informational 参考锚点 (numeric_anchor, D-E4) + trace_integrity (D-E4 resolved).
/// </summary>
/// <param name="Scenario">场景名称 (如 "settings-full-traversal")</param>
/// <param name="Description">场景描述</param>
/// <param name="Completion">预期完成状态</param>
/// <param name="PageCoverage">预期页面覆盖率</param>
/// <param name="ElementCoverage">预期元素交互覆盖率</param>
/// <param name="CollisionProof">NodeId 碰撞分辨率验证列表 (JSON 中可为 "auto_derive" sentinel)</param>
/// <param name="DfsProperties">DFS 遍历顺序属性验证</param>
/// <param name="NumericAnchor">数值参考锚点 (informational, 非 CI-blocking)</param>
/// <param name="OperationRules">操作规则验证预期（默认全关，缺失 JSON key 不产出 RuleResult）</param>
/// <param name="TraceIntegrity">Trace 完整性验证预期（默认全关，缺失 JSON key 不产出 RuleResult）</param>
public sealed partial record class ExpectedBehavior(
    string Scenario,
    string Description,
    CompletionExpectation Completion,
    PageCoverageExpectation PageCoverage,
    ElementCoverageExpectation ElementCoverage,
    ImmutableArray<CollisionProof> CollisionProof,
    DfsPropertiesExpectation DfsProperties,
    NumericAnchor NumericAnchor,
    OperationRulesExpectation? OperationRules = null,
    TraceIntegrityExpectation? TraceIntegrity = null)
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

        // 处理 element_coverage.required 中的 "auto_derive" sentinel + C-11 schema (mode/allowedMisses)
        // - JSON 有 "mode" → 显式模式 (exact/subset), 原样保留 (不自动分流)
        // - JSON 无 "mode" → 缺省 Exact (安全回落, 非 ratio; elementcoverage-mode-cleanup 移除了 auto-derive)
        // - AllowedMisses → exact 模式显式豁免 (每项 Id+Reason)
        // - TargetName 不在 JSON 中 (来自计划 CompletionPolicy), 由 WithDerivation 捕获, 此处为 null
        var ecDto = dto.ElementCoverage;
        var mode = ParseElementCoverageMode(ecDto.Mode);
        var allowedMisses = ecDto.AllowedMisses?.Select(m => new ElementMiss(m.Id, m.Reason)).ToImmutableArray()
            ?? ImmutableArray<ElementMiss>.Empty;
        var elementCoverage = new ElementCoverageExpectation(
            ecDto.Required.ToImmutableArray(),
            Mode: mode,
            AllowedMisses: allowedMisses,
            TargetName: null);

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
                dto.NumericAnchor.ElapsedSecondsMax,
                dto.NumericAnchor.ScrollCount,
                dto.NumericAnchor.ScrollDistance,
                dto.NumericAnchor.ScrollUpCount,
                dto.NumericAnchor.FinalProgress),
            OperationRules: dto.OperationRules != null
                ? new OperationRulesExpectation(
                    DepthFirstOrder: dto.OperationRules.DepthFirstOrder,
                    NoDuplicateActionsMax: dto.OperationRules.NoDuplicateActionsMax)
                : null,
            TraceIntegrity: dto.TraceIntegrity != null
                ? BuildTraceIntegrityExpectation(dto.TraceIntegrity)
                : null);
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
    /// 从 StateFixture 推导替换 "auto_derive" sentinel (无滚动场景)。
    /// element_coverage.required 只从 fixture chrome 派生 (不含滚动全集)。
    /// 可选传入 <paramref name="completionPolicy"/>: 仅用于 subset 模式捕获 TargetName;
    /// <c>Mode</c> 取 JSON 显式值, 不自动分流。
    /// </summary>
    public ExpectedBehavior WithFixtureDerivation(StateFixture fixture, CompletionPolicy? completionPolicy = null)
        => Derive(fixture, scrollUniverse: null, completionPolicy);

    /// <summary>
    /// 从 StateFixture ∪ <see cref="SimulatedScreen"/> 滚动全集推导替换 "auto_derive" sentinel
    /// (D-1: 模型定义的完备集, 不必跑引擎即可证明完备)。
    /// element_coverage.required = fixture chrome ∪ 各滚动 source <c>GetPage(0..LastPageIndex)</c> 全集元素 Id。
    /// <c>Mode</c> 取 JSON 显式值 (不自动分流); 无限流 (TotalCount==null) 时
    /// <see cref="SimulatedScreen.GetScrollableUniverse"/> fail-fast 抛 <see cref="DomainValidationException"/> (D-8)。
    /// </summary>
    public ExpectedBehavior WithDerivation(StateFixture fixture, SimulatedScreen screen, CompletionPolicy? completionPolicy = null)
    {
        if (screen == null)
            throw new DomainValidationException(nameof(screen), null, "screen is required.");
        return Derive(fixture, screen.GetScrollableUniverse(), completionPolicy);
    }

    /// <summary>
    /// 共享推导核心: page_coverage / element_coverage (chrome ∪ 可选滚动全集) / collision_proof。
    /// <c>Mode</c> 原样保留 (JSON 显式); subset 的 TargetName 从 CompletionPolicy 捕获。
    /// </summary>
    private ExpectedBehavior Derive(
        StateFixture fixture,
        IEnumerable<(string PageId, string ElementId, string Text)>? scrollUniverse,
        CompletionPolicy? completionPolicy)
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

        // element_coverage: chrome ∪ 可选滚动全集; Mode 分流; TargetName 捕获
        var elementCoverage = DeriveElementCoverage(fixture, scrollUniverse, completionPolicy);

        // collision_proof: 空 (auto_derive) → fixture 中同 Text 不同 PageId 的组合
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
    /// 派生 element_coverage:
    /// <list type="bullet">
    /// <item><b>required</b>: auto_derive → fixture chrome (非 readonly/back_button) ∪ 可选滚动全集元素 Id; 否则保留显式值。</item>
    /// <item><b>Mode</b>: 原样保留 JSON 显式值 (exact/subset); 不自动分流 (elementcoverage-mode-cleanup 移除了 auto-derive)。</item>
    /// <item><b>TargetName</b>: subset 模式从 CompletionPolicy.TargetName 捕获 (Verify 据此定位 target tap)。</item>
    /// <item><b>AllowedMisses</b>: 原样保留 (exact 显式豁免)。</item>
    /// </list>
    /// </summary>
    private ElementCoverageExpectation DeriveElementCoverage(
        StateFixture fixture,
        IEnumerable<(string PageId, string ElementId, string Text)>? scrollUniverse,
        CompletionPolicy? completionPolicy)
    {
        // required 派生: chrome ∪ 可选滚动全集
        ImmutableArray<string> required;
        if (HasElementCoverageAutoDerive)
        {
            var chrome = fixture.Pages.Values
                .SelectMany(p => p.Elements)
                .Where(e => e.Type != "readonly" && e.Type != "back_button")
                .Select(e => e.Id);
            required = scrollUniverse is null
                ? chrome.ToImmutableArray()
                : chrome.Concat(scrollUniverse.Select(t => t.ElementId)).ToImmutableArray();
        }
        else
        {
            required = ElementCoverage.Required;
        }

        // Mode 原样保留; subset 的 TargetName 从 CompletionPolicy 捕获 (无 auto-derive)
        var targetName = ElementCoverage.Mode == ElementCoverageMode.Subset
            ? (ElementCoverage.TargetName ?? completionPolicy?.TargetName)
            : ElementCoverage.TargetName;

        return new ElementCoverageExpectation(
            required,
            Mode: ElementCoverage.Mode,
            AllowedMisses: ElementCoverage.AllowedMisses,
            TargetName: targetName);
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

    /// <summary>
    /// 解析 element_coverage.mode 字符串 (snake_case) → ElementCoverageMode。
    /// JsonStringEnumConverter 默认按 PascalCase, 不匹配 "exact"/"subset"; 故手动解析 (大小写不敏感)。
    /// null/空/未知 → Exact (安全回落, 非 ratio; elementcoverage-mode-cleanup 移除了 legacy_ratio 与 auto-derive)。
    /// </summary>
    private static ElementCoverageMode ParseElementCoverageMode(string? mode)
    {
        // elementcoverage-mode-cleanup: legacy_ratio 已移除; 缺省/未知 → Exact (安全回落, 非 ratio)
        if (string.IsNullOrWhiteSpace(mode))
            return ElementCoverageMode.Exact;
        return mode.Trim().ToLowerInvariant() switch
        {
            "exact" => ElementCoverageMode.Exact,
            "subset" => ElementCoverageMode.Subset,
            // graceful: 未知/旧 legacy_ratio 值回落 Exact (解析期不抛, 与既有策略一致)
            _ => ElementCoverageMode.Exact,
        };
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
        /// 注: [JsonPropertyName("collisionProof")] 必须显式指定 — CamelCase NamingPolicy
        /// 会将 CollisionProofRaw → collisionProofRaw (不匹配 JSON key)。
        /// </summary>
        [JsonPropertyName("collisionProof")]
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
        public OperationRulesExpectationDto? OperationRules { get; set; }
        public TraceIntegrityExpectationDto? TraceIntegrity { get; set; }
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

        /// <summary>"exact" | "subset" (snake_case, 手动解析)。缺省/未知 → Exact (elementcoverage-mode-cleanup: legacy_ratio 已移除)</summary>
        public string? Mode { get; set; }

        /// <summary>exact 模式显式豁免列表 (每项 {id, reason})</summary>
        public List<ElementMissDto>? AllowedMisses { get; set; }
    }

    internal sealed class ElementMissDto
    {
        public string Id { get; set; } = "";
        public string Reason { get; set; } = "";
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

        // Scroll-specific metrics (C-11: jump_* fields removed; pipeline deleted, no data source)
        public int ScrollCount { get; set; }
        public double ScrollDistance { get; set; }
        public int ScrollUpCount { get; set; }
        public double FinalProgress { get; set; }
    }

    internal sealed class OperationRulesExpectationDto
    {
        public bool DepthFirstOrder { get; set; }
        public int NoDuplicateActionsMax { get; set; }
    }

    internal sealed class TraceIntegrityExpectationDto
    {
        public List<string> RequiredSpanTypes { get; set; } = new();
        public int MinPageTransitions { get; set; }
    }

    // ── 辅助方法 ─────────────────────────────────────

    /// <summary>
    /// 安全构造 TraceIntegrityExpectation — Enum.Parse 可能对未知 SpanType 名称抛异常。
    /// 使用 try-catch: 单个 SpanType 解析失败 → 跳过（写 stderr warning），不阻断整体反序列化。
    /// </summary>
    private static TraceIntegrityExpectation BuildTraceIntegrityExpectation(TraceIntegrityExpectationDto dto)
    {
        var spanTypes = ImmutableArray<SpanType>.Empty;
        if (dto.RequiredSpanTypes.Count > 0)
        {
            var parsed = new List<SpanType>();
            foreach (var name in dto.RequiredSpanTypes)
            {
                try
                {
                    parsed.Add(Enum.Parse<SpanType>(name));
                }
                catch (Exception ex) when (ex is ArgumentException || ex is OverflowException)
                {
                    Console.Error.WriteLine($"[WARNING] Unknown SpanType '{name}' in traceIntegrity.requiredSpanTypes — skipping. ({ex.Message})");
                }
            }
            spanTypes = parsed.ToImmutableArray();
        }
        return new TraceIntegrityExpectation(
            RequiredSpanTypes: spanTypes,
            MinPageTransitions: dto.MinPageTransitions);
    }
}
