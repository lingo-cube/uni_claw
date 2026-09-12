#!/usr/bin/env python
"""ScreenVLM 一轮评测（MLX 4bit, M4 Metal）— uni-agent slow 用例语料。"""
import json, re, sys, time, statistics, warnings
from pathlib import Path

warnings.filterwarnings("ignore")
sys.path.insert(0, "/tmp/screenvlm-venv/lib/python3.9/site-packages")

BASE = Path("/tmp/slow-cases")
BENCH = BASE / "uitars-bench"
CMP = BASE / "vlm-compare"
NORM = 500
PROMPT = "Generate the screen representation for this UI:"

_TAG_TO_ROW = {
    "row_title": {"list_item", "button", "utility_button", "link", "tab",
                  "navigation_bar", "breadcrumb", "pagination", "sidebar"},
    "row_subtitle": {"text"},
    "section_label": {"heading", "text_block", "label", "status_bar",
                      "toolbar", "screen", "window"},
}
_UNMAPPED = set()

def parse_screentag(text, width, height):
    pattern = re.compile(
        r"<(?P<tag>[a-zA-Z][a-zA-Z0-9_]*)>"
        r"\s*<loc_(?P<l>\d+)><loc_(?P<t>\d+)><loc_(?P<r>\d+)><loc_(?P<b>\d+)>"
        r"(?P<content>.*?)(?:</(?P=tag)>|$)", re.DOTALL)
    els = []
    for m in pattern.finditer(text):
        tag = m.group("tag").lower()
        l, t, r, b = [max(0, min(int(m.group(k)), NORM)) for k in ("l", "t", "r", "b")]
        if r < l: l, r = r, l
        if b < t: t, b = b, t
        content = m.group("content")
        inner = re.search(r"<[^>]+>", content)
        tp = content[: inner.start()] if inner else content
        txt = re.sub(r"<[^>]+>", "", tp).strip()
        els.append({"label": tag,
                    "bbox": (l / NORM * width, t / NORM * height,
                             r / NORM * width, b / NORM * height),
                    "text": txt or None})
    return els

def tag_to_rowclass(tag):
    for rc, tags in _TAG_TO_ROW.items():
        if tag in tags:
            return rc
    _UNMAPPED.add(tag)
    return None

def find_el(els, txt):
    return [e for e in els if e["text"] and
            (e["text"].strip() == txt.strip() or txt.strip() in e["text"]
             or e["text"].strip() in txt)]

def main():
    from mlx_vlm import load, generate
    from mlx_vlm.prompt_utils import apply_chat_template
    from mlx_vlm.utils import load_config
    from PIL import Image

    model_path = "olragon/ScreenVLM-MLX-4bit"
    t0 = time.time()
    model, processor = load(model_path)
    config = load_config(model_path)
    print(f"[screenvlm-mlx] loaded in {time.time()-t0:.0f}s", flush=True)

    truth = json.loads((BENCH / "truth.json").read_text())
    type_truth = json.loads((CMP / "type-truth.json").read_text())
    reports = {"grounding": {}, "type": {}, "latency": {}, "scratch": {}}

    pages = ["root-top", "root-scrolled", "accessibility", "display-child"]
    for page in pages:
        img_path = str(BENCH / f"{page}.png")
        img = Image.open(img_path).convert("RGB")
        w, h = img.size
        prompt = apply_chat_template(processor, config, PROMPT, num_images=1)
        t0 = time.time()
        out = generate(model, processor, prompt, [img_path], max_tokens=4096,
                       temperature=0.0, verbose=False)
        lat = time.time() - t0
        out_text = out.text if hasattr(out, "text") else str(out)
        els = parse_screentag(out_text, w, h)
        reports["latency"][page] = round(lat, 1)

        rows = truth.get(page, [])
        hits = 0; errs = []
        for row in rows:
            cands = find_el(els, row["text"])
            if not cands:
                continue
            e = min(cands, key=lambda e: abs((e["bbox"][1] + e["bbox"][3]) / 2 - row["cy"]))
            cy = (e["bbox"][1] + e["bbox"][3]) / 2
            errs.append(abs(cy - row["cy"])); hits += 1
        reports["grounding"][page] = {
            "truthRows": len(rows), "hit": hits,
            "coverage": round(hits / len(rows), 3) if rows else None,
            "meanErrPx": round(statistics.fmean(errs), 1) if errs else None,
            "medianErrPx": round(statistics.median(errs), 1) if errs else None,
            "pctUnder35px": round(sum(1 for e in errs if e <= 35) / len(errs), 3) if errs else None,
            "nScreenTag": len(els),
        }
        t_rows = type_truth.get(page, [])
        hit_t = 0; detail_t = []
        for row in t_rows:
            cands = find_el(els, row["text"])
            if not cands:
                detail_t.append({"text": row["text"], "truth": row["type"],
                                 "pred_raw": None, "pred": None, "hit": False,
                                 "miss": "no-element"})
                continue
            e = cands[0]
            rc = tag_to_rowclass(e["label"])
            ok = rc == row["type"]
            hit_t += ok
            detail_t.append({"text": row["text"], "truth": row["type"],
                             "pred_raw": e["label"], "pred": rc, "hit": ok})
        reports["type"][page] = {"targets": len(t_rows), "mappedHit": hit_t,
                                 "mappedAcc": round(hit_t / len(t_rows), 3) if t_rows else None,
                                 "detail": detail_t}
        print(f"[{page}] elem={len(els)} grounding {hits}/{len(rows)} "
              f"meanErr={reports['grounding'][page]['meanErrPx']}px "
              f"typeAcc={reports['type'][page]['mappedAcc']} lat={lat:.1f}s", flush=True)

    nine = json.loads((CMP / "dual" / "subset12.json").read_text())
    nine_out = []
    # 需要各页元素——重跑只在页面里取（root-top/display-child 已算；其他页面无元素不判）
    for item in nine:
        page = item["page"]
        # 从已生成的页面里取（本轮只跑 4 页；subset12 可能含其他页）
        pass
    # 简化：9 题里的元素若属于已跑 4 页则从 type.detail 里统计
    nine_report = []
    for item in nine:
        page = item["page"]
        if page not in reports["type"]:
            nine_report.append({"page": page, "text": item["text"],
                                "truth": item["truth"], "pred_raw": None,
                                "pred": None, "hit": False, "note": "page-not-run"})
            continue
        for d in reports["type"][page]["detail"]:
            if d["text"] == item["text"]:
                nine_report.append({"page": page, "text": item["text"],
                                    "truth": item["truth"],
                                    "pred_raw": d["pred_raw"], "pred": d["pred"],
                                    "hit": d["hit"], "dumpLine": item.get("line")})
                break
        else:
            nine_report.append({"page": page, "text": item["text"],
                                "truth": item["truth"], "pred_raw": None,
                                "pred": None, "hit": False, "note": "element-missing"})
    reports["nine"] = {"total": len(nine_report),
                       "hit": sum(1 for r in nine_report if r["hit"]),
                       "detail": nine_report}

    fsv = {"devopts-top": "/Users/fran/Documents/Code/spacex/uni_claw/platforms/perception/evaluation/validation/fastscreen-v1/frames/1e572c8f5092.png"}
    for name, path in fsv.items():
        img = Image.open(path).convert("RGB"); w, h = img.size
        prompt = apply_chat_template(processor, config, PROMPT, num_images=1)
        t0 = time.time()
        out = generate(model, processor, prompt, [path], max_tokens=4096,
                       temperature=0.0, verbose=False)
        out_text = out.text if hasattr(out, "text") else str(out)
        els = parse_screentag(out_text, w, h)
        reports["scratch"][name] = {"lat_s": round(time.time() - t0, 1),
                                    "n_elem": len(els),
                                    "labels": sorted({e["label"] for e in els}),
                                    "sample": els[:14]}
        print(f"[{name}] elem={len(els)} lat={reports['scratch'][name]['lat_s']}s", flush=True)

    (BASE / "screenvlm-mlx-raw.json").write_text(
        json.dumps(reports, ensure_ascii=False, indent=2), encoding="utf-8")
    md = render(reports)
    (BASE / "screenvlm-report.md").write_text(md, encoding="utf-8")
    print("未映射标签:", sorted(_UNMAPPED))
    print("report →", BASE / "screenvlm-report.md")

def render(reports):
    L = ["# ScreenVLM 一轮评测（MLX 4bit @ M4 Metal；uni-agent slow 用例）", ""]
    L.append("模型: olragon/ScreenVLM-MLX-4bit（docling ScreenVLM v2, Idefics3 0.3B）")
    L.append("## 71 查询 grounding（center-y 判定，同旧基准口径）")
    L.append("")
    L.append("| page | truth | 命中 | 覆盖 | meanErr | medianErr | ≤35px | nScreenTag |")
    L.append("|---|---|---|---|---|---|---|---|")
    for pg, g in reports["grounding"].items():
        L.append(f"| {pg} | {g['truthRows']} | {g['hit']} | {g['coverage']} | "
                 f"{g['meanErrPx']} | {g['medianErrPx']} | {g['pctUnder35px']} | {g['nScreenTag']} |")
    L.append("")
    L.append("## 类型判别（71 元素 → row_title/row_subtitle/section_label；ScreenTag 类映射，如实声明）")
    L.append("")
    L.append("| page | targets | 映射命中 | 映射 acc |")
    L.append("|---|---|---|---|")
    for pg, t in reports["type"].items():
        L.append(f"| {pg} | {t['targets']} | {t['mappedHit']} | {t['mappedAcc']} |")
    L.append("")
    L.append("## 9 核心题（subset12；命中来自对应页 type 判型）")
    L.append("")
    L.append("| page | text | truth | pred_raw | pred | hit |")
    L.append("|---|---|---|---|---|---|")
    for r in reports["nine"]["detail"]:
        L.append(f"| {r['page']} | {r['text']} | {r['truth']} | {r['pred_raw']} | "
                 f"{r['pred']} | {r['hit']} |")
    L.append("")
    L.append("## 延迟")
    L.append("")
    for pg, lat in reports["latency"].items():
        L.append(f"- {pg}: {lat}s")
    L.append("")
    L.append("## FSV Android 帧（OOD 观测）")
    L.append("")
    for nm, s in reports["scratch"].items():
        L.append(f"### {nm}（{s['lat_s']}s, {s['n_elem']} 元素）")
        L.append(f"标签: {sorted(s['labels'])}")
        L.append(f"样本: {json.dumps(s['sample'], ensure_ascii=False)[:500]}")
        L.append("")
    return "\n".join(L)

if __name__ == "__main__":
    main()
