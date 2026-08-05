using System.Text;
using System.Text.Json;
using UniClaw.Core.Domain;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.UniBrain;
using UniClaw.Host.Runner;

namespace UniClaw.Host.HostServices;

/// <summary>
/// Thin IPageAnalyzer decorator that intercepts AnalyzeCurrentPageAsync to update
/// the shared CurrentPageAnalysisAccessor before returning. All three methods
/// delegate to the inner analyzer; only AnalyzeCurrentPageAsync writes the accessor.
///
/// 分析证据落盘（D-197）: 当传入 run 上下文（pipeline + runDirectory）时, 每次分析成功
/// 返回后把精简快照提交到资产管道（relative path "analysis.jsonl" — runId 由装配注入,
/// V2 落 <c>assets/{runId}/analysis.jsonl</c>）—— append-only JSONL, 一行一个分析。
/// 写入经 <see cref="ITracePipeline"/> 后台批量执行, 不阻塞引擎关键路径,
/// run finalize 时随管道一起 drain。
///
/// 落盘内容为每次分析检测到的条目（name/type/归一化坐标/预期操作）+ 页面摘要
/// （hasScroll/isEndOfList/isPopup/level1 菜单名）。此前集成 run 的 trace 只记
/// item_count 不记条目名, 无法回答"检测到的名字 vs 场景目标名"（matcher/OCR 排查）。
/// 本文件与该 accessor 更新是同一拦截点, 保证写盘快照与引擎消费的是同一分析结果。
/// </summary>
public sealed class AnalysisWritingDecorator : IPageAnalyzer
{
    private readonly IPageAnalyzer _inner;
    private readonly CurrentPageAnalysisAccessor _accessor;
    private readonly ITracePipeline? _pipeline;

    /// <param name="pipeline">Run 资产提交管道；null 时跳过落盘（非 run 单测场景）。</param>
    /// <param name="runDirectory">Run 输出根目录（仅用于与 pipeline 成对校验）。</param>
    public AnalysisWritingDecorator(
        IPageAnalyzer inner,
        CurrentPageAnalysisAccessor accessor,
        ITracePipeline? pipeline = null,
        string? runDirectory = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
        if ((pipeline is null) != (runDirectory is null))
        {
            throw new ArgumentException(
                "pipeline and runDirectory must be provided together.", nameof(pipeline));
        }

        _pipeline = pipeline;
    }

    public async Task<PageAnalysis?> AnalyzeCurrentPageAsync(CancellationToken ct = default)
    {
        var result = await _inner.AnalyzeCurrentPageAsync(ct).ConfigureAwait(false);
        if (result is not null)
        {
            _accessor.Current = result;
            SubmitSnapshot(result);
        }

        return result;
    }

    public Task<AppEntryPoint?> FindAppEntryAsync(string targetApp, CancellationToken ct = default)
        => _inner.FindAppEntryAsync(targetApp, ct);

    public Task<PageTypeVerification> VerifyPageTypeAsync(
        PageAnalysis pageAnalysis,
        string expectedType,
        string? expectedPageName = null,
        CancellationToken ct = default)
        => _inner.VerifyPageTypeAsync(pageAnalysis, expectedType, expectedPageName, ct);

    /// <summary>异步提交分析快照；管道已 complete（post-drain）时静默丢弃。</summary>
    private void SubmitSnapshot(PageAnalysis analysis)
    {
        if (_pipeline is null)
            return;

        var line = JsonSerializer.Serialize(
            AnalysisSnapshot.From(analysis, DateTimeOffset.UtcNow), DomainJsonOptions.Default)
            + Environment.NewLine;
        _pipeline.Submit(new AssetSubmission(
            AssetCategories.AnalysisSnapshot,
            Encoding.UTF8.GetBytes(line),
            "analysis.jsonl",
            append: true));
    }

    /// <summary>analysis.jsonl 的单行记录 —— 一次页面分析的精简快照。</summary>
    private sealed record AnalysisSnapshot(
        string AnalyzedAt,
        int ItemCount,
        bool HasScroll,
        bool IsEndOfList,
        bool IsPopup,
        int Fingerprint,
        string[] Level1MenuNames,
        AnalysisItem[] Items)
    {
        public static AnalysisSnapshot From(PageAnalysis analysis, DateTimeOffset analyzedAt)
            => new(
                analyzedAt.ToString("O"),
                analysis.Items.Length,
                analysis.HasScroll,
                analysis.IsEndOfList,
                analysis.IsPopup,
                analysis.PageFingerprint,
                analysis.Level1Menus.Select(m => m.Name).ToArray(),
                analysis.Items
                    .Select(i => new AnalysisItem(
                        i.Name, i.Type, i.Coordinate.X, i.Coordinate.Y, i.ExpectedAction))
                    .ToArray());
    }

    /// <summary>单条目快照：名字 + 类型 + 归一化坐标 + 预期操作。</summary>
    private sealed record AnalysisItem(
        string Name,
        MenuItemType Type,
        double X,
        double Y,
        ExpectedAction ExpectedAction);
}
