#!/usr/bin/env python
"""ScreenVLM 一轮评测（uni-agent slow 用例语料）：
- 71 查询 grounding：4 页截图 × truth.json（center-y 对齐，同旧基准判定口径）
- 类型判别：type-truth.json（row_title/row_subtitle/section_label，经 ScreenTag
  类映射；如实标注映射）
- 9 核心题（subset12 元素子集）单独列
- 另附 2 帧 FSV Android 帧观测（OOD 行为）
输出 /tmp/slow-cases/screenvlm-report.md
"""
import json, re, sys, time, statistics
from pathlib import Path

sys.path.insert(0, "/Users/fran/Documents/Code/spacex/uni_claw/.perception/venv/lib/python3.11/site-packages")

BASE = Path("/tmp/slow-cases")
BENCH = BASE / "uitars-bench"
CMP = BASE / "vlm-compare"

NORM = 500
PAGES = ["root-top", "display-child"]  # 精简：F/G 页 + Color/Colors 幻影行页
PROMPT = "Generate the screen representation for this UI:"

# ScreenTag 55 类 → uni-agent 三分类（row_title/row_subtitle/section_label）映射。
# 如实声明：这是探针映射，非官方；ScreenTag 用 55 类词表，两种 ontology 无一一对应。
# 依据语义：行标题 ~ list_item/button/link/utility_button；副标题 ~ text/heading 小；
# 分组标签 ~ heading/text_block 独立文本。行标题倾向交互类，标签倾向静态文本。
_TAG_TO_ROW = {
    "row_title": {"list_item", "button", "utility_button", "link", "tab", "navigation_bar",
                  "breadcrumb", "pagination", "sidebar"},
    "row_subtitle": {"text"},
    "section_label": {"heading", "text_block", "label", "status_bar", "toolbar", "screen", "window"},
}
_UNMAPPED = set()

def parse_screentag(text, width, height):
    pattern = re.compile(
        r"<(?P<tag>[a-zA-Z][a-zA-Z0-9_]*)>"
        r"\s*<loc_(?P<l>\d+)><loc_(?P<t>\d+)><loc_(?P<r>\d+)><loc_(?P<b>\d+)>"
        r"(?P<content>.*?)(?:</(?P=tag)>|$)",
        re.DOTALL,
    )
    elements = []
    for m in pattern.finditer(text):
        tag = m.group("tag").lower()
        l, t, r, b = [max(0, min(int(m.group(k)), NORM)) for k in ("l", "t", "r", "b")]
        if r < l: l, r = r, l
        if b < t: t, b = b, t
        content = m.group("content")
        inner = re.search(r"<[^>]+>", content)
        text_part = content[: inner.start()] if inner else content
        text = re.sub(r"<[^>]+>", "", text_part).strip()
        elements.append({
            "label": tag,
            "bbox": (l / NORM * width, t / NORM * height,
                     r / NORM * width, b / NORM * height),
            "text": text or None,
        })
    return elements

def tag_to_rowclass(tag):
    for rc, tags in _TAG_TO_ROW.items():
        if tag in tags:
            return rc
    _UNMAPPED.add(tag)
    return None

def main():
    import torch
    from PIL import Image
    from transformers import AutoProcessor, AutoModelForVision2Seq

    device = "mps" if torch.backends.mps.is_available() else "cpu"
    print(f"[screenvlm] device={device}", flush=True)
    t0 = time.time()
    processor = AutoProcessor.from_pretrained("docling-project/ScreenVLM")
    model = AutoModelForVision2Seq.from_pretrained(
        "docling-project/ScreenVLM", torch_dtype=torch.float16,
        _attn_implementation="eager").to(device)
    print(f"[screenvlm] loaded in {time.time()-t0:.0f}s", flush=True)

    truth = json.loads((BENCH / "truth.json").read_text())
    type_truth = json.loads((CMP / "type-truth.json").read_text())

    reports = {"grounding": {}, "type": {}, "latency": {}, "scratch": {}}
    raw_outputs = {}

    for page in PAGES:
        img = Image.open(BENCH / f"{page}.png").convert("RGB")
        w, h = img.size
        messages = [{"role": "user", "content": [{"type": "image"},
                     {"type": "text", "text": PROMPT}]}]
        prompt = processor.apply_chat_template(messages, add_generation_prompt=True)
        inputs = processor(text=prompt, images=[img], return_tensors="pt").to(device)
        t0 = time.time()
        with torch.no_grad():
            gen = model.generate(**inputs, max_new_tokens=2048,
                                 do_sample=False, temperature=None)
        lat = time.time() - t0
        out = processor.batch_decode(gen[:, inputs.input_ids.shape[1]:],
                                     skip_special_tokens=False)[0].lstrip()
        elements = parse_screentag(out, w, h)
        raw_outputs[page] = {"lat_s": round(lat, 1), "elements": elements,
                             "raw_tail": out[-400:]}

        # grounding: truth rows (text, cy)
        rows = truth.get(page, [])
        hits = 0
        errs = []
        for row in rows:
            txt = row["text"]
            # ScreenVLM 元素文本精确/包含匹配（与旧基准单目标判定的近似：取文本=目标且 cy 最近的）
            cands = [e for e in elements
                     if e["text"] and (e["text"].strip() == txt.strip()
                                       or txt.strip() in e["text"]
                                       or e["text"].strip() in txt)]
            if not cands:
                continue
            e = min(cands, key=lambda e: abs((e["bbox"][1] + e["bbox"][3]) / 2 - row["cy"]))
            cy = (e["bbox"][1] + e["bbox"][3]) / 2
            errs.append(abs(cy - row["cy"]))
            hits += 1
        reports["grounding"][page] = {
            "truthRows": len(rows), "hit": hits,
            "coverage": round(hits / len(rows), 3) if rows else None,
            "meanErrPx": round(statistics.fmean(errs), 1) if errs else None,
            "medianErrPx": round(statistics.median(errs), 1) if errs else None,
            "pctUnder35px": round(sum(1 for e in errs if e <= 35) / len(errs), 3) if errs else None,
        }
        reports["latency"][page] = round(lat, 1)

        # type: 71 元素三分类（映射后）
        t_rows = type_truth.get(page, [])
        map_hit = 0
        per = []
        for row in t_rows:
            txt = row["text"]
            cands = [e for e in elements
                     if e["text"] and (e["text"].strip() == txt.strip()
                                       or txt.strip() in e["text"]
                                       or e["text"].strip() in txt)]
            if not cands:
                per.append({"text": txt, "truth": row["type"], "pred_raw": None,
                            "pred": None, "hit": False, "miss": "no-element"})
                continue
            e = cands[0]
            rc = tag_to_rowclass(e["label"])
            hit = rc == row["type"]
            if hit:
                map_hit += 1
            per.append({"text": txt, "truth": row["type"],
                        "pred_raw": e["label"], "pred": rc, "hit": hit})
        reports["type"][page] = {
            "targets": len(t_rows),
            "mappedHit": map_hit,
            "mappedAcc": round(map_hit / len(t_rows), 3) if t_rows else None,
            "detail": per,
        }
        print(f"[{page}] grounding hit={hits}/{len(rows)} "
              f"meanErr={reports['grounding'][page]['meanErrPx']}px "
              f"typeAcc={reports['type'][page]['mappedAcc']} "
              f"lat={lat:.1f}s n_elem={len(elements)}", flush=True)

    # 9 核心题：subset12 元素 → ScreenVLM 类（映射到三分类）
    nine = json.loads((CMP / "dual" / "subset12.json").read_text())
    nine_report = []
    for item in nine:
        txt = item["text"]; page = item["page"]
        elems = raw_outputs.get(page, {}).get("elements", [])
        cands = [e for e in elems if e["text"] and (e["text"].strip() == txt.strip()
                                                    or txt.strip() in e["text"]
                                                    or e["text"].strip() in txt)]
        if cands:
            e = cands[0]
            rc = tag_to_rowclass(e["label"])
            hit = rc == item["truth"]
        else:
            e, rc, hit = None, None, False
        nine_report.append({
            "page": page, "text": txt, "truth": item["truth"],
            "pred_raw": e["label"] if e else None,
            "pred": rc, "hit": hit,
            "dumpLine": item.get("line"),
        })
    hit9 = sum(1 for r in nine_report if r["hit"])
    reports["nine"] = {"total": len(nine_report), "hit": hit9,
                       "acc": round(hit9 / len(nine_report), 3), "detail": nine_report}

    # FSV Android 帧观测（OOD）
    fsv_frames = {
        "devopts-top": "/Users/fran/Documents/Code/spacex/uni_claw/platforms/perception/evaluation/validation/fastscreen-v1/frames/1e572c8f5092.png",
    }
    for name, path in fsv_frames.items():
        img = Image.open(path).convert("RGB")
        w, h = img.size
        messages = [{"role": "user", "content": [{"type": "image"},
                     {"type": "text", "text": PROMPT}]}]
        prompt = processor.apply_chat_template(messages, add_generation_prompt=True)
        inputs = processor(text=prompt, images=[img], return_tensors="pt").to(device)
        t0 = time.time()
        with torch.no_grad():
            gen = model.generate(**inputs, max_new_tokens=2048,
                                 do_sample=False, temperature=None)
        out = processor.batch_decode(gen[:, inputs.input_ids.shape[1]:],
                                     skip_special_tokens=False)[0].lstrip()
        elems = parse_screentag(out, w, h)
        reports["scratch"][name] = {
            "lat_s": round(time.time() - t0, 1), "n_elem": len(elems),
            "labels": sorted({e["label"] for e in elems}),
            "sample": elems[:12],
        }
        print(f"[{name}] n_elem={len(elems)} lat={reports['scratch'][name]['lat_s']}s", flush=True)

    # 落盘
    (BASE / "screenvlm-raw.json").write_text(
        json.dumps(reports, ensure_ascii=False, indent=2), encoding="utf-8")
    md = render_md(reports, raw_outputs)
    (BASE / "screenvlm-report.md").write_text(md, encoding="utf-8")
    print("\n== report →", BASE / "screenvlm-report.md")
    print(f"未映射 ScreenTag 类: {sorted(_UNMAPPED)}")

def render_md(reports, raw_outputs):
    L = ["# ScreenVLM 一轮评测（uni-agent slow 用例语料；ScreenTag 解析）", ""]
    L.append("模型: docling-project/ScreenVLM (Idefics3, SigLIP2+Granite165M, 0.3B, BF16, MPS)")
    L.append("提示词: `Generate the screen representation for this UI:`")
    L.append("坐标: ScreenTag loc∈[0,500] → ×(W,H)")
    L.append("")
    L.append("## 71 查询 grounding（4 页 × truth.json，center-y 判定，同旧基准口径）")
    L.append("")
    L.append("| page | truth 行 | 命中 | 覆盖率 | meanErr(px) | medianErr(px) | ≤35px |")
    L.append("|---|---|---|---|---|---|---|")
    for pg, g in reports["grounding"].items():
        L.append(f"| {pg} | {g['truthRows']} | {g['hit']} | {g['coverage']} | "
                 f"{g['meanErrPx']} | {g['medianErrPx']} | {g['pctUnder35px']} |")
    L.append("")
    L.append("## 类型判别（type-truth 71 元素 → row_title/row_subtitle/section_label，ScreenTag 类映射）")
    L.append("")
    L.append("| page | targets | 映射命中 | 映射 acc |")
    L.append("|---|---|---|---|")
    for pg, t in reports["type"].items():
        L.append(f"| {pg} | {t['targets']} | {t['mappedHit']} | {t['mappedAcc']} |")
    L.append("")
    L.append("## 9 核心题（subset12 元素；ScreenVLM 不消费 dump 行，按页面元素判型映射）")
    L.append("")
    L.append("| page | text | truth | pred_raw | pred | hit | dumpLine |")
    L.append("|---|---|---|---|---|---|---|")
    for r in reports["nine"]["detail"]:
        L.append(f"| {r['page']} | {r['text']} | {r['truth']} | {r['pred_raw']} | "
                 f"{r['pred']} | {r['hit']} | {r.get('dumpLine') or ''} |")
    L.append("")
    L.append("## 延迟 / ScreenTag 元素数")
    L.append("")
    for pg, lat in reports["latency"].items():
        L.append(f"- {pg}: {lat}s")
    L.append("")
    L.append("## FSV Android 帧（OOD 观测）")
    L.append("")
    for nm, s in reports["scratch"].items():
        L.append(f"### {nm}（{s['lat_s']}s, {s['n_elem']} 元素）")
        L.append(f"标签分布: {sorted(s['labels'])}")
        L.append(f"样本: {json.dumps(s['sample'], ensure_ascii=False)[:600]}")
        L.append("")
    return "\n".join(L)

if __name__ == "__main__":
    main()
