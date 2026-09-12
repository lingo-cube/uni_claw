#!/usr/bin/env python3
"""gt_generate.py — FSV-001 WI-4: a11y dump -> candidate GT + overlay visualization.

Reads uia/<sha12>.xml (uiautomator dump) + frames/<sha12>.meta.json, writes:
  gt/<sha12>.json            candidate GT (eval-spec §1 schema, reviewStatus=candidate)
  gt/overlays/<sha12>.jpg    box overlay (long edge <= 1400, JPEG q85)
  gt/overlays/index.html     browsing index for the Leader
  gt/GENERATION-REPORT.md    per-frame stats + systematic a11y bias list + review priorities

Determinism contract: rerunning produces byte-identical outputs. No timestamps,
no unordered iteration in output paths; all traversal is document order, all
lists (elements/expectedTexts/groundingTasks/bias flags/frames) are sorted.

Discipline: reads frames/uia/manifest only; never writes outside gt/.
"""

import argparse
import json
import os
import re
import sys
import glob
from collections import Counter
from statistics import median

import xml.etree.ElementTree as ET

from PIL import Image, ImageDraw, ImageFont

# ----------------------------------------------------------------------------
# constants
# ----------------------------------------------------------------------------
ROOT = os.path.dirname(os.path.abspath(__file__))
FRAMES_DIR = os.path.join(ROOT, "frames")
UIA_DIR = os.path.join(ROOT, "uia")
GT_DIR = os.path.join(ROOT, "gt")
OVERLAYS_DIR = os.path.join(GT_DIR, "overlays")

SCREEN_W, SCREEN_H = 1080, 2400
MIN_AREA = 32 * 32  # skip boxes with area < 32x32 px
EXPECTED_TEXT_MIN_LEN = 2  # expectedTexts entries must be >= 2 chars
OVERLAY_MAX_EDGE = 1400
JPEG_QUALITY = 85
BOX_WIDTH = 3

SCHEMA_VERSION = "uniclaw.fastscreenValset.gt.v1"

# class basename -> canonical perception label (unknown -> list_item/text_block rules)
CLASS_TABLE = {
    "Button": "button",
    "ImageButton": "button",
    "Switch": "switch",
    "CheckBox": "checkbox",
    "RadioButton": "checkbox",
    "SeekBar": "slider",
    "EditText": "input",
    "AutoCompleteTextView": "input",
    "MultiAutoCompleteTextView": "input",
    "Spinner": "input",
    "ImageView": "icon",
    "Toolbar": "toolbar",
    "ActionMenuView": "toolbar",
    "TabItem": "tab",
    "Tab": "tab",
}
# container-ish classes: never emitted as elements themselves (non-interactive);
# only traversed. Interactive ones follow the clickable-row collapse rules.
CONTAINER_CLASSES = {
    "FrameLayout", "LinearLayout", "RelativeLayout", "ViewGroup", "View",
    "ScrollView", "HorizontalScrollView", "ComposeView", "CardView",
    "RadioGroup", "TimePicker", "RadialTimePickerView", "ListView", "GridView",
    "ViewPager", "RecyclerView",
}
# list containers whose node *itself* is skipped per spec (rows still processed)
LIST_SKIP_SELF = {"ViewPager", "RecyclerView", "ListView", "GridView"}

ABBR = {
    "text_block": "tx", "list_item": "li", "button": "btn", "switch": "sw",
    "checkbox": "cb", "slider": "sl", "input": "in", "icon": "ic",
    "toolbar": "tb", "tab": "tab", "image": "img",
}
COLOR = {
    "interactive": (240, 70, 70),
    "text_block": (60, 120, 240),
    "other": (40, 180, 90),
}

# --- static observations gathered during WI-4 analysis (frame-level facts, not
# derived from per-frame stats; kept as data so reruns stay byte-stable) ------
STALE_DUMP_FRAMES = ["9f5d4e04c7cf"]  # meta.label=about-phone, tree=Date&time settings page
SCRIM_DIALOG_FRAMES = ["7b3f715379b0", "4909995071e6", "9297d23b7c9f"]  # a11y covers active window only
TIME_PICKER_DIAL_FRAMES = ["4909995071e6"]  # 12 RadialPickerTouchHelper cd nodes -> text_block
SEARCH_INPUT_FRAMES = ["5df0424f787c"]  # SearchView + AutoCompleteTextView
STATUS_ICON_FRAMES = ["6ea31e4ba9be", "882f15432eca", "9ee712f0f817", "d2a5c3eca70a"]
DARK_FRAMES = ["7e41f85e06e4", "206556c78e8e", "518f41240dd0", "9ee712f0f817"]

REVIEW_PRIORITIES = [
    # (tier, frames, reason)
    ("HIGH", STALE_DUMP_FRAMES,
     "a11y dump stale vs photo (label=about-phone but tree=Date & time settings); overlay may not match pixels"),
    ("HIGH", SCRIM_DIALOG_FRAMES,
     "dialog/overlay: dump covers only the active window; dimmed/scrim background absent by design"),
    ("HIGH", ["6ea31e4ba9be", "882f15432eca", "9ee712f0f817"],
     "QS / notification shade: tile Switch text is a11y-merged ('Bluetooth, On') not literal screen lines; cd has trailing '.'"),
    ("HIGH", ["a86c6501da91"],
     "Developer options deep: recycled Switch node with inverted bounds skipped; adjacent rows may be off-by-one"),
    ("HIGH", ["d8b2a487cdfa"],
     "apps-deep: a11y dump root covers only ~6% of screen (partial/truncated dump); photo likely shows "
     "more rows — verify and add missing elements"),
    ("HIGH", SEARCH_INPUT_FRAMES,
     "Settings search: SearchView itself has no a11y text; box relies on AutoCompleteTextView -> input"),
    ("MEDIUM", DARK_FRAMES,
     "dark frames: verify text_block contrast / text extraction against visible text"),
    ("MEDIUM", ["731aa1393639", "acea3e4e4839", "518f41240dd0", "9f5d4e04c7cf", "5df0424f787c"],
     "text-heavy pages: long TextView bodies, few interactive elements; check paragraph text_blocks"),
    ("MEDIUM", ["c28ad3c4e752"],
     "Storage page: 7 ProgressBar nodes have no a11y text -> skipped as elements (visible bars unlabeled)"),
    ("MEDIUM", None, "container-cd page titles dropped (see bias list) - add text_block for app-bar titles if wanted"),
]


def log(msg):
    print(msg, flush=True)


# ----------------------------------------------------------------------------
# xml -> tree
# ----------------------------------------------------------------------------
def parse_bounds(s):
    m = re.findall(r"\[(-?\d+),(-?\d+)\]\[(-?\d+),(-?\d+)\]", s or "")
    if not m:
        return None
    x1, y1, x2, y2 = (int(v) for v in m[0])
    return x1, y1, x2, y2


def build_tree(xml_path):
    root = ET.parse(xml_path).getroot()
    tree = []

    def add(node, parent):
        idx = len(tree)
        entry = {
            "idx": idx,
            "parent": parent,
            "children": [],
            "class": (node.get("class") or "").split(".")[-1],
            "text": (node.get("text") or "").strip(),
            "cd": (node.get("content-desc") or "").strip(),
            "clickable": node.get("clickable") == "true",
            "checkable": node.get("checkable") == "true",
            "checked": node.get("checked"),
            "scrollable": node.get("scrollable") == "true",
            "naf": node.get("NAF") == "true",
            "visible_to_user": node.get("visible-to-user"),
            "bounds": parse_bounds(node.get("bounds")),
            "valid": True,
        }
        tree.append(entry)
        if parent is not None:
            tree[parent]["children"].append(idx)
        for child in node:
            add(child, idx)

    add(root, None)
    return tree


def mark_invalid(tree, w, h):
    """Visibility filter.

    Two classes:
      dead    - node (and its subtree) must be skipped entirely: NAF, no/inverted/
                degenerate/fully-offscreen bounds (RecyclerView recycled clones).
      invalid - node cannot be an element itself (e.g. full-screen scrim, tiny
                area), but its subtree is still traversed (children may be valid).
    """
    scr = w * h * 0.95
    for e in tree:
        b = e["bounds"]
        if b is None or e["naf"] or e["visible_to_user"] == "false":
            e["dead"] = e["valid"] = False
            continue
        x1, y1, x2, y2 = b
        if x2 <= x1 or y2 <= y1 or x2 <= 0 or y1 >= h or y2 <= 0 or x1 >= w:
            e["dead"] = e["valid"] = False  # degenerate or fully off-screen
            continue
        e["dead"] = True  # bounds are sane; emit may still be barred below
        if ((x2 - x1) * (y2 - y1)) < MIN_AREA:
            e["valid"] = False
            continue
        if (e["clickable"] or e["checkable"]) and (x2 - x1) * (y2 - y1) >= scr:
            e["valid"] = False  # full-screen tap target / scrim / dismiss layer


# ----------------------------------------------------------------------------
# classification / collapse
# ----------------------------------------------------------------------------
def element_class(e, interactive):
    cls = e["class"]
    if cls in LIST_SKIP_SELF:
        return None
    if cls in CLASS_TABLE:
        base = CLASS_TABLE[cls]
        if cls == "TextView":
            return "list_item" if interactive else "text_block"
        return base  # ImageView -> icon even when clickable
    if interactive:
        return "list_item"
    if e["text"] or e["cd"]:
        return "text_block"
    return None


def count_interactive_descendants(tree, idx):
    n = 0
    for c in tree[idx]["children"]:
        e = tree[c]
        if not e["valid"]:
            continue
        if e["clickable"] or e["checkable"]:
            n += 1
        n += count_interactive_descendants(tree, c)
    return n


def collect_tokens(tree, idx, out, exclude_interactive=True):
    """Visible text tokens of non-interactive descendants, document order."""
    for c in tree[idx]["children"]:
        e = tree[c]
        if not e["valid"]:
            continue
        if e["clickable"] or e["checkable"]:
            if exclude_interactive:
                continue
        t = e["text"] or e["cd"]
        if t:
            out.append((t, bool(e["text"])))
        collect_tokens(tree, c, out, exclude_interactive)


def emit(tree, idx, elements, text, src):
    e = tree[idx]
    interactive = e["clickable"] or e["checkable"]
    cls = element_class(e, interactive)
    if cls is None:
        return None
    x1, y1, x2, y2 = e["bounds"]
    state = None
    if e["checkable"]:
        state = "on" if e["checked"] == "true" else "off" if e["checked"] == "false" else None
    el = {
        "gtClass": cls,
        "x1": x1, "y1": y1, "x2": x2, "y2": y2,
        "text": text,
        "src": src,
        "interactive": interactive,
        "state": state,
    }
    elements.append(el)
    return el


def process(idx, tree, elements, skipped):
    e = tree[idx]
    if not e["dead"] or idx in skipped:
        return  # recycled/dead subtree (or already dropped)
    if not e["valid"]:
        # cannot emit (scrim / tiny area / invisible) but children may be valid
        for c in e["children"]:
            process(c, tree, elements, skipped)
        return
    interactive = e["clickable"] or e["checkable"]
    cls = e["class"]
    own = e["text"] or e["cd"]

    if interactive:
        if own:
            # atomic interactive element; labels live in its own text/cd.
            # non-interactive descendants are its label/icon children -> scan for
            # any interactive grandchildren, otherwise drop them.
            emit(tree, idx, elements, own, "text" if e["text"] else "cd")
            for c in e["children"]:
                ce = tree[c]
                if not ce["dead"]:
                    continue
                if ce["clickable"] or ce["checkable"]:
                    process(c, tree, elements, skipped)
                else:
                    scan(c, tree, elements, skipped)
            return
        n_int = count_interactive_descendants(tree, idx)
        if n_int >= 2:
            # multi-interactive container (e.g. panel w/ switch+buttons): group,
            # not an element itself -> recurse children
            for c in e["children"]:
                process(c, tree, elements, skipped)
            return
        # clickable row (0 or 1 interactive descendant): collapse row into one
        # element, keep interactive descendant(s) as separate elements
        tokens = []
        collect_tokens(tree, idx, tokens, exclude_interactive=True)
        text = " ".join(t for t, _ in tokens) if tokens else None
        src = None
        if tokens:
            src = "synth-text" if all(ft for _, ft in tokens) else "synth-cd"
        emit(tree, idx, elements, text, src)
        for c in e["children"]:
            ce = tree[c]
            if not ce["dead"]:
                continue
            if ce["clickable"] or ce["checkable"]:
                process(c, tree, elements, skipped)
            else:
                scan(c, tree, elements, skipped)
        return

    # non-interactive
    if cls in LIST_SKIP_SELF or cls in CONTAINER_CLASSES:
        for c in e["children"]:
            process(c, tree, elements, skipped)
        return
    has_valid_child = any(tree[c]["dead"] for c in e["children"])
    if has_valid_child:
        for c in e["children"]:
            process(c, tree, elements, skipped)
        return
    # leaf
    emit(tree, idx, elements, own or None,
         "text" if e["text"] else ("cd" if e["cd"] else None))


def scan(idx, tree, elements, skipped):
    """Descend into a dropped non-interactive branch: emit any interactive
    element found inside (e.g. a Switch grandchild of a clickable row), but
    never emit non-interactive nodes."""
    e = tree[idx]
    if not e["dead"] or idx in skipped:
        return
    if e["clickable"] or e["checkable"]:
        process(idx, tree, elements, skipped)
        return
    for c in e["children"]:
        scan(c, tree, elements, skipped)


# ----------------------------------------------------------------------------
# per-frame GT
# ----------------------------------------------------------------------------
def screen_order(entries, key):
    return sorted(entries, key=key)


def build_expected_texts(tree):
    texts = []
    for e in tree:
        if not e["valid"]:
            continue
        t = e["text"]
        if not t:
            continue
        t = t.strip()
        if len(t) < EXPECTED_TEXT_MIN_LEN:
            continue
        texts.append((e["bounds"][1], e["bounds"][0], t))
    texts = sorted(texts, key=lambda r: (r[0], r[1]))
    seen = set()
    out = []
    for _y, _x, t in texts:
        if t not in seen:
            seen.add(t)
            out.append(t)
    return out


def build_grounding_tasks(elements, stratum):
    """0-3 tasks per frame, fixed kind order g1..gN (spec §1)."""
    tasks = []

    def add(kind, query, gtId):
        tasks.append({"taskId": "g%d" % (len(tasks) + 1), "kind": kind,
                      "query": query, "expectGtId": gtId})

    # 1. click-selected: any checkable element in state "on"
    sel = [el for el in elements if el["interactive"] and el["state"] == "on"]
    if sel:
        add("click-selected", {"state": "on"}, sel[0]["gtId"])

    # 2. click-nth-item: list/dense/scrollable strata, pick a tappable column
    if stratum in ("list", "dense", "scrollable"):
        rows = [el for el in elements
                if el["interactive"] and el["gtClass"] == "list_item"]
        if rows:
            # cluster rows by x-center into columns
            cols = {}
            for el in rows:
                cx = round((el["x1"] + el["x2"]) / 200) * 100  # 100px buckets
                cols.setdefault(cx, []).append(el)
            col = max(cols.values(), key=len)
            col = sorted(col, key=lambda el: (el["y1"], el["x1"]))
            if len(col) >= 4:
                n = 3 if len(col) >= 5 else 2
                item = col[n - 1]
                add("click-nth-item", {"list": stratum, "nth": n}, item["gtId"])

    # 3. click-text: interactive element whose text is literal visible text
    #    (src text/synth-text), single-line, >=2 chars, unique in frame;
    #    prefer shortest
    cand = [el for el in elements
            if el["interactive"] and el["text"] and len(el["text"]) >= EXPECTED_TEXT_MIN_LEN
            and el["src"] in ("text", "synth-text") and "\n" not in el["text"]]
    counts = Counter(el["text"] for el in cand)
    uniq = [el for el in cand if counts[el["text"]] == 1]
    if uniq:
        best = min(uniq, key=lambda el: (len(el["text"]), el["y1"], el["x1"]))
        add("click-text", best["text"], best["gtId"])

    return tasks[:3]


def frame_bias_flags(tree, elements, fid, meta, w, h):
    """Programmatic per-frame bias flags (compact strings for gt provenance)."""
    flags = []
    elem_texts = {el["text"] for el in elements if el["text"]}
    n_cd_container = 0
    for e in tree:
        if not e["valid"] or e["clickable"] or e["checkable"]:
            continue
        if (e["class"] in CONTAINER_CLASSES and e["cd"]
                and len(e["cd"]) >= 2 and e["cd"] not in elem_texts):
            n_cd_container += 1
    if n_cd_container:
        flags.append("cd-on-container-dropped(%d)" % n_cd_container)
    if any(el["src"] == "cd" for el in elements):
        flags.append("cd-not-visible-text")
    if any(el["gtClass"] == "switch" and el["text"] and "," in el["text"] for el in elements):
        flags.append("qs-merged-tile-text")
    n_naf = sum(1 for e in tree if e["naf"] and not e["dead"])
    if n_naf:
        flags.append("naf-nodes-skipped(%d)" % n_naf)
    if any(e["valid"] and e["text"] and "\n" in e["text"] for e in tree):
        flags.append("multiline-text")
    full = any(e["dead"] and e["bounds"]
               and (e["bounds"][2] - e["bounds"][0]) * (e["bounds"][3] - e["bounds"][1])
               >= 0.9 * w * h for e in tree)
    if not full:
        flags.append("no-fullscreen-root")
    if fid in TIME_PICKER_DIAL_FRAMES:
        flags.append("radial-dial-helper-text_blocks")
    if fid in STALE_DUMP_FRAMES:
        flags.append("stale-dump-vs-frame")
    if fid in SCRIM_DIALOG_FRAMES:
        flags.append("scrim-dialog-background-absent")
    return sorted(flags)


def normalize(bounds, w, h):
    x1, y1, x2, y2 = bounds
    return {
        "x1": round(x1 / w, 6), "y1": round(y1 / h, 6),
        "x2": round(x2 / w, 6), "y2": round(y2 / h, 6),
    }


def write_gt_json(fid, meta, elements, expected, tasks, flags):
    els = []
    for el in elements:
        els.append({
            "gtId": el["gtId"],
            "gtClass": el["gtClass"],
            "bounds": normalize((el["x1"], el["y1"], el["x2"], el["y2"]), SCREEN_W, SCREEN_H),
            "text": el["text"],
            "interactive": el["interactive"],
            "state": el["state"],
        })
    gt = {
        "schemaVersion": SCHEMA_VERSION,
        "frameId": fid,
        "reviewStatus": "candidate",
        "provenance": {
            "method": "a11y-assisted+human-verify",
            "annotator": "wi4-agent+leader",
            "knownBiases": flags,
        },
        "elements": els,
        "expectedTexts": expected,
        "groundingTasks": tasks,
    }
    out = os.path.join(GT_DIR, fid + ".json")
    with open(out, "w", encoding="utf-8") as fh:
        fh.write(json.dumps(gt, indent=2, ensure_ascii=False) + "\n")
    return out


# ----------------------------------------------------------------------------
# overlays
# ----------------------------------------------------------------------------
def render_overlay(fid, meta, elements, frame_path, out_path):
    img = Image.open(frame_path)
    w, h = img.size
    scale = min(1.0, OVERLAY_MAX_EDGE / max(w, h))
    rw, rh = max(1, round(w * scale)), max(1, round(h * scale))
    base = img.convert("RGBA").resize((rw, rh), Image.LANCZOS)
    layer = Image.new("RGBA", (rw, rh), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    font = ImageFont.load_default(size=13)
    for el in elements:
        x1 = round(el["x1"] * scale)
        y1 = round(el["y1"] * scale)
        x2 = round(el["x2"] * scale)
        y2 = round(el["y2"] * scale)
        color = COLOR["interactive"] if el["interactive"] else (
            COLOR["text_block"] if el["gtClass"] == "text_block" else COLOR["other"])
        d.rectangle([x1, y1, x2, y2], outline=color + (255,), width=BOX_WIDTH)
        label = "%s:%s" % (el["gtId"], ABBR.get(el["gtClass"], "?"))
        tw = d.textlength(label, font=font)
        chip_h = 18
        d.rectangle([x1 + 1, y1 + 1, x1 + 1 + tw + 6, y1 + 1 + chip_h],
                    fill=color + (235,))
        d.text((x1 + 4, y1 + 3), label, fill=(255, 255, 255, 255), font=font)
    # top strip: frameId / stratum / element count
    strip_h = 24
    d.rectangle([0, 0, rw - 1, strip_h], fill=(20, 20, 20, 170))
    header = "%s | %s | %d elements | list_item:%d text_block:%d" % (
        fid, meta.get("stratum", "?"), len(elements),
        sum(1 for e in elements if e["gtClass"] == "list_item"),
        sum(1 for e in elements if e["gtClass"] == "text_block"))
    d.text((6, strip_h // 2 - 7), header, fill=(255, 255, 255, 255), font=font)
    out = Image.alpha_composite(base, layer).convert("RGB")
    out.save(out_path, "JPEG", quality=JPEG_QUALITY)


OVERLAY_TABLE_HEADER = """<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>FSV-001 candidate GT overlays (WI-4)</title>
<style>
 body{background:#14161a;color:#e8e8e8;font-family:system-ui,sans-serif;margin:24px}
 table{border-collapse:collapse;width:100%}
 th,td{border:1px solid #333;padding:6px 10px;text-align:left;vertical-align:top}
 th{background:#20242b}
 a{color:#7db4ff}
 .stratum{font-size:12px;color:#9fb3c8;font-weight:600}
 .muted{color:#9aa}
 img.thumb{width:126px;border:1px solid #444;display:block}
 h2{font-size:16px;color:#c9d6e8}
 ul{margin:6px 0 6px 18px}
</style>
</head>
<body>
<h1>FSV-001 candidate GT overlays — generated by <code>gt_generate.py</code> (WI-4, auto)</h1>
<p class="muted">All 39 frames: red = interactive, blue = text_block, green = other.
Every box labeled <code>gtId:abbr</code>. These are <em>candidates</em>
(reviewStatus=candidate, method=a11y-assisted+human-verify) — correct before use:
bounds to match pixels, cd-sourced text to null, add missing page titles / icons.</p>
"""

OVERLAY_TABLE_FOOTER = """
</table>
</body>
</html>
"""


def write_index_html(frames_meta, per_frame):
    rows = []
    for fid in sorted(frames_meta):
        m = per_frame[fid]
        meta = frames_meta[fid]
        rows.append(
            "<tr>"
            '<td><a href="overlays/%s.jpg"><img class="thumb" src="overlays/%s.jpg" '
            'alt="%s"></a></td>'
            '<td><code>%s</code></td>'
            '<td class="stratum">%s</td>'
            "<td>%s</td>"
            "<td>%d</td>"
            '<td><a href="overlays/%s.jpg">overlay</a></td>'
            '<td><a href="%s.json">gt json</a></td>'
            "</tr>"
            % (fid, fid, fid, fid, meta.get("stratum", "?"),
               _esc(meta.get("label") or meta.get("note") or ""), m["n"],
               fid, fid))
    html = OVERLAY_TABLE_HEADER + (
        "<h2>%d frames</h2>\n" % len(frames_meta)
        + "<table><tr>"
        + "<th>thumbnail</th><th>frameId</th><th>stratum</th><th>label/note</th>"
        + "<th>elements</th><th>overlay</th><th>gt</th></tr>\n"
        + "\n".join(rows) + "\n</table>"
        + "<p class=\"muted\">Legend: interactive=red, text_block=blue, other=green. "
        "See GENERATION-REPORT.md for bias list and review priorities.</p>"
        + OVERLAY_TABLE_FOOTER)
    with open(os.path.join(GT_DIR, "index.html"), "w", encoding="utf-8") as fh:
        fh.write(html)
    return os.path.join(GT_DIR, "index.html")


def _esc(s):
    return (s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;"))


# ----------------------------------------------------------------------------
# report
# ----------------------------------------------------------------------------
def build_report(frames_meta, per_frame, biome_counts, bias_by_frame):
    lines = []
    A = lines.append

    A("# FSV-001 WI-4 — candidate GT generation report (auto)")
    A("")
    A("Generated by `gt_generate.py`; deterministic — reruns produce byte-identical outputs.")
    A("")
    A("## 1. Coverage (per frame)")
    A("")
    A("| frameId | stratum | label | elements |")
    A("|---|---|---|---|")
    for fid in sorted(frames_meta):
        m = per_frame[fid]
        meta = frames_meta[fid]
        A("| %s | %s | %s | %d |" % (fid, meta.get("stratum", "?"),
                                     _esc(meta.get("label") or ""), m["n"]))

    counts = [per_frame[f]["n"] for f in per_frame]
    A("")
    A("Per-frame element count: min=%d median=%g max=%d total=%d" % (
        min(counts), median(counts), max(counts), sum(counts)))
    A("")
    A("## 2. Stratum aggregate")
    A("")
    A("| stratum | frames | elements | avg |")
    A("|---|---|---|---|")
    for s in sorted(biome_counts):
        nf = len(biome_counts[s]["frames"])
        ne = biome_counts[s]["elements"]
        A("| %s | %d | %d | %.1f |" % (s, nf, ne, ne / nf if nf else 0))
    A("")
    class_tot = Counter()
    for fid in per_frame:
        for cls in per_frame[fid]["classes"]:
            class_tot[cls] += 1
    A("Class distribution (all frames): %s" % " ".join(
        "%s=%d" % (k, v) for k, v in sorted(class_tot.items())))
    A("")
    A("## 3. Systematic a11y biases (observed)")
    A("")
    for bid, desc, frames in BIAS_ROWS:
        if frames is None:
            A("- **%s**: %s" % (bid, desc))
        else:
            A("- **%s** (%d frames: %s): %s" % (
                bid, len(frames), ", ".join(sorted(set(frames))), desc))
    A("")
    A("## 4. Leader review priorities")
    A("")
    for tier, fr, reason in REVIEW_PRIORITIES:
        if fr is None:
            A("- **%s**: %s" % (tier, reason))
        else:
            A("- **%s** (%d frames: %s): %s" % (
                tier, len(fr), ", ".join(sorted(fr)), reason))
    A("")
    return "\n".join(lines) + "\n"


# bias rows: (id, description, frames-or-None) — frames computed per run
BIAS_ROWS = []


# ----------------------------------------------------------------------------
# main
# ----------------------------------------------------------------------------
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--fid", help="comma list of frame ids to process (default: all)")
    ap.add_argument("--no-overlay", action="store_true")
    args = ap.parse_args()

    os.makedirs(GT_DIR, exist_ok=True)
    os.makedirs(OVERLAYS_DIR, exist_ok=True)

    xmls = sorted(glob.glob(os.path.join(UIA_DIR, "*.xml")))
    wanted = set(args.fid.split(",")) if args.fid else None

    frames_meta = {}
    per_frame = {}
    bias_by_frame = {}
    biome_counts = {}
    skip_stats = Counter()

    for xp in xmls:
        fid = os.path.basename(xp)[:-4]
        if wanted is not None and fid not in wanted:
            continue
        frame_path = os.path.join(FRAMES_DIR, fid + ".png")
        meta_path = os.path.join(FRAMES_DIR, fid + ".meta.json")
        if not os.path.exists(frame_path) or not os.path.exists(meta_path):
            log("skip %s: missing frame/meta" % fid)
            continue
        with open(meta_path, encoding="utf-8") as fh:
            meta = json.load(fh)
        frames_meta[fid] = meta

        try:
            with Image.open(frame_path) as im:
                w, h = im.size
        except Exception as exc:
            log("skip %s: cannot open frame (%s)" % (fid, exc))
            continue
        if (w, h) != (SCREEN_W, SCREEN_H):
            log("warn %s: frame size %dx%d (expected %dx%d)" % (fid, w, h, SCREEN_W, SCREEN_H))

        tree = build_tree(xp)
        mark_invalid(tree, w, h)

        # skip stats (deterministic, for report + debugging)
        n_naf = sum(1 for e in tree if e["naf"] and not e["dead"])
        n_deg = sum(1 for e in tree if not e["dead"] and not e["naf"] and e["bounds"]
                    and (e["bounds"][2] <= e["bounds"][0]
                         or e["bounds"][3] <= e["bounds"][1]
                         or e["bounds"][2] <= 0 or e["bounds"][1] >= h
                         or e["bounds"][3] <= 0 or e["bounds"][0] >= w))
        n_area = sum(1 for e in tree if e["dead"] and not e["valid"] and not e["naf"]
                     and not (e["clickable"] or e["checkable"])
                     and not ((e["bounds"][2] - e["bounds"][0]) * (e["bounds"][3] - e["bounds"][1])
                              >= 0.95 * w * h))
        n_scrim = sum(1 for e in tree if e["dead"] and not e["valid"] and not e["naf"]
                      and (e["clickable"] or e["checkable"]))
        skip_stats[fid] = {"naf": n_naf, "degenerate": n_deg, "area": n_area, "scrim": n_scrim}

        elements = []
        skipped = set()
        for c in tree[0]["children"]:  # root <hierarchy> node has no bounds; walk its children
            process(c, tree, elements, skipped)
        if not elements:
            log("warn %s: no elements" % fid)
            continue

        # gtId assignment: screen order (y1, x1), then class/text for ties
        elements.sort(key=lambda el: (el["y1"], el["x1"], el["gtClass"], el["text"] or ""))
        for i, el in enumerate(elements, 1):
            el["gtId"] = "gt_%d" % i

        expected = build_expected_texts(tree)
        tasks = build_grounding_tasks(elements, meta.get("stratum", ""))
        flags = frame_bias_flags(tree, elements, fid, meta, w, h)
        bias_by_frame[fid] = flags

        # persistence
        out_json = write_gt_json(fid, meta, elements, expected, tasks, flags)
        if not args.no_overlay:
            out_img = os.path.join(OVERLAYS_DIR, fid + ".jpg")
            render_overlay(fid, meta, elements, frame_path, out_img)

        per_frame[fid] = {
            "n": len(elements),
            "classes": Counter(el["gtClass"] for el in elements),
            "interactive": sum(1 for el in elements if el["interactive"]),
            "expected": len(expected),
            "tasks": len(tasks),
            "skipped": skip_stats[fid],
            "json": out_json,
        }
        s = meta.get("stratum", "?")
        bc = biome_counts.setdefault(s, {"frames": [], "elements": 0})
        bc["frames"].append(fid)
        bc["elements"] += len(elements)

        log("%s %-12s n=%2d interactive=%2d expected=%2d tasks=%d skips=%s" % (
            fid, meta.get("stratum", "?"), len(elements),
            per_frame[fid]["interactive"], len(expected), len(tasks),
            skip_stats[fid]))

    if not per_frame:
        log("nothing generated")
        sys.exit(1)

    # bias rows with frame lists (deterministic)
    def fset(pred):
        return sorted(f for f in per_frame if pred(f, per_frame[f], bias_by_frame[f], frames_meta))

    BIAS_ROWS[:] = [
        ("B1", "page/app-bar titles and some labels exist only as content-desc on non-clickable layout "
               "nodes and are NOT emitted (bounds too coarse for IoU scoring); add text_block for page "
               "titles if scoring cares.",
         fset(lambda f, pf, fl, m: any(x.startswith("cd-on-container-dropped") for x in fl))),
        ("B2", "element text sourced from content-desc (e.g. ImageButton 'Navigate up', QS tile labels, "
               "volume sliders): not literal on-screen text; correct to null or visible label.",
         fset(lambda f, pf, fl, m: "cd-not-visible-text" in fl)),
        ("B3", "QS/notification tiles: Switch text is a11y-merged ('Bluetooth, On', "
               "'Internet, T-Mobile, 3G') and cd has trailing '.' ('Bluetooth.' vs visible 'Bluetooth').",
         fset(lambda f, pf, fl, m: "qs-merged-tile-text" in fl)),
        ("B4", "multi-line TextView text kept verbatim (embedded \\n, e.g. factory-reset warnings, legal "
               "pages) — check rendering and OCR baselines.",
         fset(lambda f, pf, fl, m: "multiline-text" in fl)),
        ("B5", "NAF nodes skipped (invisible or stale a11y entries; some are full-screen decor layers).",
         fset(lambda f, pf, fl, m: any(x.startswith("naf-nodes-skipped") for x in fl))),
        ("B6", "degenerate/inverted or fully off-screen bounds skipped (recycled list rows, e.g. "
               "devopts-deep switch with y2<y1).",
         fset(lambda f, pf, fl, m: pf["skipped"]["degenerate"] > 0)),
        ("B7", "tiny nodes (<32x32px, e.g. status-bar icons, QS page dots) dropped by area rule.",
         fset(lambda f, pf, fl, m: pf["skipped"]["area"] > 0)),
        ("B8", "full-screen interactive scrim/dismiss layers removed (background tap targets).",
         fset(lambda f, pf, fl, m: pf["skipped"]["scrim"] > 0)),
        ("B9", "stale a11y dump vs photo (frame %s: meta.label=about-phone but tree = Date & time "
               "settings page)." % ", ".join(STALE_DUMP_FRAMES),
         fset(lambda f, pf, fl, m: "stale-dump-vs-frame" in fl)),
        ("B10", "dialog/overlay frames: a11y dump covers only the active window; dimmed/scrim background "
                "content absent from GT by design.",
         fset(lambda f, pf, fl, m: "scrim-dialog-background-absent" in fl)),
        ("B11", "time-picker radial dial (frame %s): 12 RadialPickerTouchHelper nodes emitted as "
                "text_block from content-desc; a11y coords may not match painted glyph positions."
                % ", ".join(TIME_PICKER_DIAL_FRAMES),
         TIME_PICKER_DIAL_FRAMES[:] if TIME_PICKER_DIAL_FRAMES else []),
        ("B12", "ProgressBar nodes have no a11y text -> not emitted as elements (visible bars unlabeled, "
                "Storage page).",
         ["c28ad3c4e752"]),
        ("B13", "icons without a11y nodes or with tiny bounds are absent from GT (README known "
                "limitation); status bar icons mostly missing.",
         fset(lambda f, pf, fl, m: pf["skipped"]["naf"] > 0 or pf["skipped"]["area"] > 0)),
        ("B14", "no full-screen root node in the dump (partial/truncated window): GT only covers what the "
                "a11y dump exposes; frame %s covers ~6%% of screen." % ("d8b2a487cdfa"),
         fset(lambda f, pf, fl, m: "no-fullscreen-root" in fl)),
    ]

    idx_path = write_index_html(frames_meta, per_frame)
    rep_path = os.path.join(GT_DIR, "GENERATION-REPORT.md")
    with open(rep_path, "w", encoding="utf-8") as fh:
        fh.write(build_report(frames_meta, per_frame, biome_counts, bias_by_frame))

    log("")
    log("wrote %d gt jsons, %d overlays, %s, %s" % (
        len(per_frame), len(per_frame) if not args.no_overlay else 0, idx_path, rep_path))
    log("element counts: min=%d median=%g max=%d total=%d" % (
        min(per_frame[f]["n"] for f in per_frame),
        median(per_frame[f]["n"] for f in per_frame),
        max(per_frame[f]["n"] for f in per_frame),
        sum(per_frame[f]["n"] for f in per_frame)))


if __name__ == "__main__":
    main()