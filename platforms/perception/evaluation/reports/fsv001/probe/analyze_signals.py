#!/usr/bin/env python
"""FSV-001 WI-5a 探针分析：对历史失败点（uni-agent real-world-failure-distribution：
K/F/G 类 + scroll continuity）做 arm 级感知信号对比。无 GT → 信号级，非评分级。"""
import json, glob, sys
from pathlib import Path

sys.path.insert(0, "/Users/fran/Documents/Code/spacex/uni_claw/platforms/perception/bench")
import compare_arms as ca

OUT = Path("/tmp/fsv001-probe")
ARMS = ["baseline", "integration", "replacement", "ocr-off"]

FRAME_INFO = {
    "1e572c8f5092": ("dense", "devopts-top", "K 类：Developer options 密集 switch 页"),
    "4a1f3e1d4421": ("dense", "devopts-toggle-off", "K 类：Stay awake toggled OFF（状态帧）"),
    "5ef64e4a384a": ("scrollable", "devopts-mid", "K 类：devopts 滚动中（scroll continuity）"),
    "3682353fffbf": ("list", "apps-top", "scroll continuity：Apps 列表顶部"),
    "9591de5f37de": ("scrollable", "apps-mid", "scroll continuity：Apps 滚动中"),
    "c1667d8b209e": ("list", "settings-home-top", "F/G 类：SettingsRoot 导航候选"),
    "a5d983aa849b": ("list", "network-internet-page", "F/G 类：Network & internet 子页"),
    "c8b2e65f2451": ("list", "bluetooth-page", "F/G 类：Bluetooth 设备列表"),
}

TOGGLE_LABELS = {"switch", "toggle", "checkbox"}

def load_frames():
    out = {}
    for p in sorted(glob.glob(str(OUT / "frames" / "*.json"))):
        d = json.load(open(p))
        out[d["frameId"]] = d
    return out

def resp_of(detail, arm):
    return detail.get("arms", {}).get(arm, {}).get("qualityResponse")

def row_inventory(resp):
    """candidates 中带 text 的行清单：[(y1, text, type)]，按 y1 排序。"""
    rows = []
    for c in resp.get("candidates", []):
        t = str(c.get("text", "")).strip()
        bp = c.get("boundsPx")
        if t and bp:
            rows.append((bp[1], t, c.get("type")))
    rows.sort()
    return rows

def switch_rows(resp):
    """text 关联的 switch/toggle/checkbox 行数 + yolo 该标签数。"""
    cand = sum(1 for c in resp.get("candidates", [])
               if c.get("type") in TOGGLE_LABELS and str(c.get("text", "")).strip())
    yolo = sum(1 for d in resp.get("yolo", []) if d.get("label") in TOGGLE_LABELS)
    return cand, yolo

def stayawake_anchor(resp):
    """K 类核心：click-state('Stay awake') 前端 join —— ocr token 存在 + 最小包含
    yolo 框存在（= 运行时能否把该行解析成可绑定目标）。"""
    yolo, ocr = resp.get("yolo", []), resp.get("ocr", [])
    token = ca._ocr_token_for_query(ocr, "Stay awake")
    if token is None:
        return {"token": False, "box": None}
    center = ca._token_center_px(token)
    box = ca._smallest_containing_yolo(yolo, center) if center else None
    return {"token": True, "box": (box.get("id"), box.get("label")) if box else None}

def ocr_texts(resp):
    return [str(t.get("text", "")).strip() for t in resp.get("ocr", []) if str(t.get("text", "")).strip()]

def main():
    frames = load_frames()
    lines = []
    lines.append("# FSV-001 失败点探针：arm 级感知信号对比（无 GT；信号级非评分级）")
    lines.append("")
    lines.append(f"帧：{len(frames)} ｜ 臂：{', '.join(ARMS)}｜ runs=5/warmup=1｜ 出处："
                 "uni-agent real-world-failure-distribution（K 25% / F 8.3% / G 8.3% / scroll continuity）")
    lines.append("")

    # ── K 类：toggle 行 + Stay awake 锚
    lines.append("## K 类（post-action/post-scroll 页面与 toggle 行解析）")
    lines.append("")
    lines.append("| frame | arm | yolo switch/toggle | text 关联 switch 行 | Stay awake 锚 | Stay awake token |")
    lines.append("|---|---|---|---|---|---|")
    for fid, (stratum, label, note) in FRAME_INFO.items():
        if "devopts" not in label and "apps" not in label:
            continue
        for arm in ARMS:
            resp = resp_of(frames[fid], arm)
            if resp is None:
                lines.append(f"| {label} | {arm} | — | — | — | — |")
                continue
            c, y = switch_rows(resp)
            anc = stayawake_anchor(resp)
            box_s = anc["box"][0] if anc["box"] else "无包含框"
            lines.append(f"| {label} | {arm} | {y} | {c} | {box_s} | {'✓' if anc['token'] else '✗'} |")
    lines.append("")

    # ── F/G 类：SettingsRoot 导航候选行
    lines.append("## F/G 类（SettingsRoot 导航候选 / 行文本可读性）")
    lines.append("")
    lines.append("| frame | arm | ocr tokens | 带文本候选行数 | 前 6 行 (y1, text) |")
    lines.append("|---|---|---|---|---|")
    for fid in ("c1667d8b209e", "a5d983aa849b", "c8b2e65f2451"):
        label = FRAME_INFO[fid][1]
        for arm in ARMS:
            resp = resp_of(frames[fid], arm)
            if resp is None:
                lines.append(f"| {label} | {arm} | — | — | — |")
                continue
            rows = row_inventory(resp)
            ocrn = len(ocr_texts(resp))
            top = "; ".join(f"{y:.0f}:{t[:18]}" for y, t, _ in rows[:6])
            lines.append(f"| {label} | {arm} | {ocrn} | {len(rows)} | {top} |")
    lines.append("")

    # ── Scroll continuity（K 类变体 / EBD normalization）
    lines.append("## Scroll continuity（相邻滚动帧共享行——EBD 归一化 / A7 变体的根）")
    lines.append("")
    lines.append("| 帧对 | arm | 帧A行 | 帧B行 | 共享行 | 共享率 |")
    lines.append("|---|---|---|---|---|---|")
    for pair, names in [(["1e572c8f5092", "5ef64e4a384a"], ("devopts-top", "devopts-mid")),
                        (["3682353fffbf", "9591de5f37de"], ("apps-top", "apps-mid"))]:
        for arm in ARMS:
            ra = resp_of(frames[pair[0]], arm)
            rb = resp_of(frames[pair[1]], arm)
            if ra is None or rb is None:
                lines.append(f"| {names[0]}→{names[1]} | {arm} | — | — | — | — |")
                continue
            ta = {t for _, t, _ in row_inventory(ra)}
            tb = {t for _, t, _ in row_inventory(rb)}
            inter = ta & tb
            rate = len(inter) / len(tb) if tb else 0
            lines.append(f"| {names[0]}→{names[1]} | {arm} | {len(ta)} | {len(tb)} | {len(inter)} | {rate:.2f} |")
    lines.append("")

    # ── 数据表：latency / RSS（来自 harness 聚合报告）
    agg_files = sorted(glob.glob(str(OUT / "fsv001-*.json")))
    if agg_files:
        agg = json.load(open(agg_files[-1]))
        lines.append("## Latency / RSS（跨帧 pooled）")
        lines.append("")
        lines.append("| arm | wall n | wall P50 | wall P95 | RSS max (MB) |")
        lines.append("|---|---|---|---|---|")
        for arm in ARMS:
            if arm not in agg["arms"]:
                continue
            w = agg["arms"][arm]["latency"]["wall"]
            rss = agg["arms"][arm]["rss"]
            lines.append(f"| {arm} | {w['n']} | {ca._fmt(w.get('p50Ms'),1)} | "
                         f"{ca._fmt(w.get('p95Ms'),1)} | {ca._fmt(rss['maxRssMb'],1)} |")
        lines.append("")

    report = "\n".join(lines)
    (OUT / "probe-signals.md").write_text(report, encoding="utf-8")
    print(report)

if __name__ == "__main__":
    main()
