using System.Collections.Immutable;

namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 动态分页滚动内容源 (见设计 §7.1): 按页码按需确定性生成滚动列表内容, 像分页 API。
/// 取代每场景预构的静态 <c>ScrollDataStore</c> 段数据 (配置驱动复用)。
/// </summary>
public interface IScrollContentSource
{
    /// <summary>总条目数 (null = 未知/无限流; 终止由 engine seen-set 驱动, 不依赖此值)</summary>
    int? TotalCount { get; }

    /// <summary>每页条目数</summary>
    int PageSize { get; }

    /// <summary>
    /// 返回指定页的内容。必须是 <paramref name="pageIndex"/> 的纯函数 (确定性, 无随机, 无隐藏状态)
    /// —— 可复现、可缓存。末页可能少于 <see cref="PageSize"/>; TotalCount=null 时任意页均返回满页。
    /// </summary>
    /// <param name="pageIndex">页码 (≥0)</param>
    /// <returns>该页的 <see cref="MockItem"/> 列表 (超出末页返回空)</returns>
    ImmutableArray<MockItem> GetPage(int pageIndex);
}
