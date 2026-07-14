using System.Collections.Immutable;
using UniClaw.Core.Domain;

namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 可复用分页内容生成器 (sealed class, 见设计 §7.1): 用参数 (totalCount/pageSize/fillRatio/namePrefix)
/// 表达密集/稀疏/长/短场景, 无需为每个场景重建静态 fixture 数据。GetPage 是 pageIndex 的纯函数。
/// </summary>
/// <remarks>
/// 稀疏分布 (<c>fillRatio</c> &lt; 1.0) 按全局索引取模确定性留空, 绝不随机。
/// </remarks>
public sealed class PagedItemGenerator : IScrollContentSource
{
    private readonly int? _totalCount;
    private readonly int _pageSize;
    private readonly double _fillRatio;
    private readonly string _namePrefix;

    /// <inheritdoc />
    public int? TotalCount => _totalCount;

    /// <inheritdoc />
    public int PageSize => _pageSize;

    /// <summary>
    /// 创建分页内容生成器。
    /// </summary>
    /// <param name="totalCount">总槽位数 (null = 无限流)</param>
    /// <param name="pageSize">每页槽位数 (≥1)</param>
    /// <param name="fillRatio">填充比例 0.0–1.0 (1.0=密集, &lt;1.0=确定性稀疏, 每页均匀留空)</param>
    /// <param name="namePrefix">元素名前缀</param>
    public PagedItemGenerator(int? totalCount, int pageSize, double fillRatio = 1.0, string namePrefix = "item_")
    {
        if (pageSize <= 0)
            throw new DomainValidationException(nameof(pageSize), pageSize, "pageSize must be >= 1.");
        if (fillRatio < 0.0 || fillRatio > 1.0)
            throw new DomainValidationException(nameof(fillRatio), fillRatio, "fillRatio must be in [0.0, 1.0].");
        if (totalCount is { } tc && tc < 0)
            throw new DomainValidationException(nameof(totalCount), totalCount, "totalCount must be non-negative (or null for infinite).");

        _totalCount = totalCount;
        _pageSize = pageSize;
        _fillRatio = fillRatio;
        _namePrefix = namePrefix ?? "item_";
    }

    /// <inheritdoc />
    public ImmutableArray<MockItem> GetPage(int pageIndex)
    {
        if (pageIndex < 0)
            return ImmutableArray<MockItem>.Empty;

        var builder = ImmutableArray.CreateBuilder<MockItem>(_pageSize);
        double slotSpacing = _pageSize > 1 ? 0.70 / (_pageSize - 1) : 0.0;
        // 每页填充的槽位数 (阈值): fillRatio * pageSize, 向下取整。每页均匀留空 (确定性, 绝不随机)。
        int filledSlots = (int)(_fillRatio * _pageSize);

        for (int slot = 0; slot < _pageSize; slot++)
        {
            int global = pageIndex * _pageSize + slot;

            // 有限总数: 超出则结束 (末页可能不足 PageSize)
            if (_totalCount is { } tc && global >= tc)
                break;

            // 稀疏: 仅前 filledSlots 个槽位填充 (每页相同的确定性稀疏模式)
            if (slot >= filledSlots)
                continue;

            double y = 0.15 + slot * slotSpacing;
            builder.Add(new MockItem($"{_namePrefix}{global}", 0.5, y));
        }

        return builder.ToImmutable();
    }
}
