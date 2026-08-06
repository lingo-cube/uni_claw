#!/usr/bin/env python3
"""item_quality_check.py — analysis.jsonl item 质量批量检查 (local-vision-analyzer)

用法: python3 item_quality_check.py <analysis.jsonl> [--jsonl-report out.jsonl]

检查维度:
  1. endOfList / hasScroll / isPopup 分布
  2. type 分布 (含未知 label → 'text' 默认降级的影响)
  3. 同帧重复 item (归一化文本相同或互为包含 + Y 差 < 0.03)
  4. 搜索框候选 (文本含 search/搜索, Y < 0.12)
  5. 副标题候选: 非空文本 menu_item 与前一个非空文本 menu_item 的 Y 差 (V2 0.035 阈值覆盖)
  6. 空文本 item 统计
"""
import json
import sys
from collections import Counter

SAME_ROW = 0.03        # V1 DeduplicateSameRowItems 阈值
SUB_ROW = 0.035        # V2 DowngradeSubtitleTypes 阈值
TOP_BAR_Y = 0.10       # V5 ExcludeTopBarSearch 阈值


def normalize(text: str) -> str:
    out = []
    pending = False
    for ch in text:
        if ch in '，、,':
            ch = ' '
        if ch.isspace():
            pending = True
            continue
        if pending:
            out.append(' ')
            pending = False
        out.append(ch)
    return ''.join(out).strip().lower()


def main():
    path = sys.argv[1]
    report_path = sys.argv[2] if len(sys.argv) > 2 and sys.argv[2] == '--jsonl-report' else None
    report = []
    lines = []
    with open(path) as f:
        for ln, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            rec = json.loads(line)
            lines.append((ln, rec))

    total = len(lines)
    eol = Counter(r['isEndOfList'] for _, r in lines)
    scroll = Counter(r['hasScroll'] for _, r in lines)
    popup = Counter(r['isPopup'] for _, r in lines)
    types = Counter()
    empty_names = 0
    empty_name_frames = set()
    search_items = []      # (line, name, type, x, y, expectedAction)
    subtitle_cands = []    # (line, name, y, prev_name, prev_y, dy)
    dup_events = []        # (line, kept_name, dropped_name, y)
    frame_qsearch = []     # (line, name, type, x, y, expectedAction)
    rows = []              # (line, y, name, type) for subtitle analysis on menu_item

    for ln, rec in lines:
        items = rec.get('items', [])
        # 1-2. 基础统计
        for it in items:
            types[it['type']] += 1
            nm = it.get('name') or ''
            if nm.strip() == '':
                empty_names += 1
                empty_name_frames.add(ln)
            # 4. 搜索框候选
            low = nm.lower()
            if any(k in low for k in ('search', '搜索', '搜')) and it.get('y', 1) < 0.12:
                search_items.append((ln, nm, it['type'], it.get('x'), it.get('y'), it.get('expectedAction')))
            if 'qsearch' in low or '搜索设置' in nm:
                frame_qsearch.append((ln, nm, it['type'], it.get('x'), it.get('y'), it.get('expectedAction')))

        # 3. 同帧重复 (V1 逻辑复现)
        norm_seen = []
        for it in items:
            nm = it.get('name') or ''
            if nm.strip() == '':
                continue
            n = normalize(nm)
            if not n:
                continue
            y = it.get('y')
            for (n0, name0, y0) in norm_seen:
                if y0 is not None and y is not None and abs(y - y0) < SAME_ROW:
                    if n in n0 or n0 in n:
                        dup_events.append((ln, name0, nm, y))
                        break
            else:
                norm_seen.append((n, nm, y))

        # 5. 副标题候选: 非空 menu_item 顺序 Y 差 (V2 复现)
        prev = None
        for it in sorted([i for i in items if (i.get('name') or '').strip()], key=lambda i: i.get('y', 1e9)):
            if it['type'] == 'menu_item' and prev is not None and prev['type'] == 'menu_item':
                dy = it['y'] - prev['y']
                if 0 <= dy < SUB_ROW:
                    subtitle_cands.append((ln, prev['name'], prev['y'], it['name'], it['y'], round(dy, 4)))
            if it.get('name', '').strip():
                prev = it

    print(f"=== {path} ({total} frames) ===")
    print(f"isEndOfList: {dict(eol)}")
    print(f"hasScroll:   {dict(scroll)}")
    print(f"isPopup:     {dict(popup)}")
    print(f"type 分布:   {dict(types)}")
    print(f"空文本 item: {empty_names} (帧数 {len(empty_name_frames)})")
    print(f"\n--- 搜索框候选 (文本含 search/搜索, y<0.12) ---")
    for r in search_items:
        print(f"  L{r[0]} {r[1]!r} type={r[2]} x={r[3]:.3f} y={r[4]:.4f} act={r[5]}")
    print(f"\n--- QSearch/搜索设置 全部出现 ---")
    for r in frame_qsearch:
        print(f"  L{r[0]} {r[1]!r} type={r[2]} x={r[3]:.3f} y={r[4]:.4f} act={r[5]}")
    print(f"\n--- V2 副标题降级候选 (menu_item→menu_item, 0<=dy<0.035) ---")
    for r in subtitle_cands:
        print(f"  L{r[0]} '{r[1]}'(y={r[2]:.4f}) -> '{r[3]}'(y={r[4]:.4f}) dy={r[5]}")
    print(f"\n--- V1 同排重复 (norm 包含, |dy|<0.03) ---")
    for r in dup_events:
        print(f"  L{r[0]} '{r[1]}' ~ '{r[2]}' y={r[3]:.4f}")

    if report_path:
        with open(report_path, 'w') as f:
            json.dump({'endOfList': dict(eol), 'hasScroll': dict(scroll),
                       'isPopup': dict(popup), 'types': dict(types),
                       'emptyNames': empty_names,
                       'searchItems': search_items, 'qsearch': frame_qsearch,
                       'subtitleCandidates': subtitle_cands, 'dupEvents': dup_events},
                      f, ensure_ascii=False, indent=2)


if __name__ == '__main__':
    main()
