#!/usr/bin/env python3
"""Simulation Replay Viewer — 仿真回放 JSON → 自包含可视化 HTML。

用法:
  python3 scripts/sim-replay-viewer.py replay.json
  python3 scripts/sim-replay-viewer.py replay.json -o replay.html
  python3 scripts/sim-replay-viewer.py replay.json --screenshots artifacts/screenshots/
  python3 scripts/sim-replay-viewer.py replay.json --open

输入:
  TraceReplayHarness.ExportReplayJson() 导出的 JSON 文件。

输出:
  自包含 HTML 文件: 手机框 + 截图底图(可选) + 点击闪烁圆圈 + 时间线导航。
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import io
import json
import math
import sys
import webbrowser
from pathlib import Path
from typing import Any

try:
    from PIL import Image, ImageDraw, ImageFont
    HAS_PILLOW = True
except ImportError:
    HAS_PILLOW = False


def load_replay(path: str) -> dict[str, Any]:
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
    for key in ("actionHistory",):
        if key not in data:
            print(f"Error: missing required field '{key}' in replay JSON", file=sys.stderr)
            sys.exit(1)
    return data


def _find_screenshots(screenshots_dir: str | None, data: dict[str, Any],
                      max_steps: int = 0) -> dict[int, dict[str, str]]:
    """扫描截图目录, 返回 stepIndex → {"before": dataURI, "after": dataURI} 映射。

    每个步骤同时嵌入 before.png 和 after.png。
    max_steps > 0 时限制嵌入步数。
    """
    if not screenshots_dir:
        return {}
    sd = Path(screenshots_dir)
    if not sd.is_dir():
        print(f"Warning: screenshots dir not found: {screenshots_dir}", file=sys.stderr)
        return {}

    result: dict[int, dict[str, str]] = {}
    step_dirs = sorted(sd.glob("steps/*"))
    if max_steps > 0:
        step_dirs = step_dirs[:max_steps]

    # 内容去重: 与上一帧同内容的截图不重复嵌入, JS 侧回退到最近可用帧。
    # 实测一屏不变的步骤占比 >90%, 120 步 run 240 张图只有 ~15 张唯一内容。
    cache: dict[str, tuple[str, int, int]] = {}
    prev_hash: dict[str, str | None] = {"before": None, "after": None}

    for step_dir in step_dirs:
        try:
            idx = int(step_dir.name) - 1  # steps are 1-indexed
        except ValueError:
            continue
        entry: dict[str, object] = {}
        for name in ("before", "after"):
            img = step_dir / f"{name}.png"
            if not img.is_file():
                continue
            h = hashlib.md5(img.read_bytes()).hexdigest()
            if h == prev_hash[name]:
                continue  # 与上一帧相同 → 不嵌入, JS 回退到最近可用帧
            prev_hash[name] = h
            if h in cache:
                uri, w, hh = cache[h]
            else:
                uri, w, hh = _image_to_data_uri(img)
                if not uri:
                    continue
                cache[h] = (uri, w, hh)
            entry[name] = uri
            # 记录原图尺寸用于坐标缩放（一个 step 的 before/after 同尺寸，后者覆盖即可）
            entry["imgW"] = w
            entry["imgH"] = hh
        if entry:
            result[idx] = entry
    return result


def _image_to_data_uri(path: Path) -> tuple[str, int, int]:
    """将 PNG 图片编码为 data: URI + 返回宽高。超过 512KB 则跳过。"""
    size = path.stat().st_size
    if size > 512 * 1024:
        return "", 0, 0
    if HAS_PILLOW:
        from PIL import Image as PILImage
        img = PILImage.open(path)
        w, h = img.size
    else:
        w, h = 0, 0
    with open(path, "rb") as f:
        b64 = base64.b64encode(f.read()).decode()
    return f"data:image/png;base64,{b64}", w, h


def _escape_json(obj: Any) -> str:
    raw = json.dumps(obj, ensure_ascii=False, separators=(",", ":"))
    return raw.replace("</", "<\\/")


def _h(s: str) -> str:
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;").replace('"', "&quot;")


# ── trace.jsonl → replay data ─────────────────────────────────────────
# 直接从真实 run 的 trace.jsonl 构建回放数据: 引擎步 (engine step) 为时间轴单位,
# 每步一条 FSM 迁移 + 活动记录; page_transition 构建遍历树; analysis.jsonl 提供坐标。

# 设备动作: 只有这些 safety.* 会出现在时间轴上 (launch/wait 为准备与安全检查)
_ACTION_TYPES = {"click", "scroll", "back", "input_text", "long_press", "swipe"}

# 活动 token 压缩映射: execution action → 短 token (step_start/page_analysis/step_end 不显示)
_ACTIVITY_TOKEN: dict[str, str] = {
    "safety.launch": "launch",
    "safety.wait": "wait",
    "safety.click": "click",
    "safety.scroll": "scroll",
    "safety.back": "back",
    "safety.input_text": "input",
    "precondition_assume_pass": "pc-pass",
    "verification_retry_single": "retry",
    "verification_page_unchanged": "page-unchanged",
    "scroll_revealed_new_elements": "scroll-new",
    "scroll_no_new_elements_end_reached": "scroll-end",
    "scroll_empty_retry_1_of_2": "scroll-empty",
    "scroll_empty_retry_2_of_2": "scroll-empty",
    "generate": "gen",
    "navigation_detected_push_subframe": "push-subframe",
}


def _normalize_name(s: str) -> str:
    """分析 item 名与 targetValue 的归一化匹配: 去空白/大小写/连字符。"""
    return "".join(ch for ch in (s or "").lower() if ch.isalnum())


def _node_label(node_id: str) -> str:
    """节点短标签: dyn_menu_container_T-Mobile_..._root_subframe → 'T-Mobile Network & Internet'"""
    if not node_id:
        return "?"
    if node_id == "root":
        return "root"
    s = node_id.replace("dyn_menu_container_", " ")
    changed = True
    while changed:  # 后缀可能嵌套 (_root_subframe → 两层), 循环剥除
        changed = False
        for suffix in ("_root", "_subframe"):
            if s.endswith(suffix):
                s = s[: -len(suffix)]
                changed = True
    s = " ".join(s.replace("_", " ").split()).title()
    return s or node_id


def _compact_activities(exs: list[dict[str, Any]]) -> list[str]:
    """把一步内 execution 记录压成短 token 序列, 连续相同 token 合并计数 (gen×8)。"""
    tokens: list[str] = []
    for e in exs:
        action = e.get("action", "")
        token = _ACTIVITY_TOKEN.get(action, action)
        if token in ("", "step_start", "step_end", "page_analysis"):
            continue
        if e.get("status") == "deny":
            token += "✗"
        tokens.append(token)

    def _merge(out: list[str], tok: str) -> None:
        if out:
            last = out[-1]
            if "×" in last:
                base, n = last.rsplit("×", 1)
                if base == tok and n.isdigit():
                    out[-1] = f"{base}×{int(n) + 1}"
                    return
            elif last == tok:
                out[-1] = f"{tok}×2"
                return
        out.append(tok)

    out: list[str] = []
    for tok in tokens:
        _merge(out, tok)
    return out


def _match_analysis_coord(analyses: list[dict[str, Any]], timestamp: str,
                          target_value: str) -> tuple[float, float] | None:
    """在全部 analysis 帧里找名称匹配的 item → 归一化坐标, 取最早出现位置。

    targetValue 是引擎的归一化目标名, 元素在同一页面反复出现 (滚动/重分析);
    取最早出现的位置 = 该元素在其主页面的稳定位置 (引擎反复点击同一元素时,
    坐标一致, 与既有回放数据行为一致)。
    """
    import datetime as _dt
    want = _normalize_name(target_value)
    if not analyses or not want:
        return None
    hits: list[tuple[str, dict[str, Any]]] = []
    for rec in analyses:
        for it in rec.get("items", []):
            if _normalize_name(it.get("name", "")) == want:
                hits.append((rec.get("analyzedAt", ""), it))
    if not hits:
        return None
    hits.sort(key=lambda h: h[0])
    return hits[0][1].get("x"), hits[0][1].get("y")


def build_replay_from_run(run_dir: str) -> dict[str, Any]:
    """从 run 目录产物构建回放数据 (trace.jsonl + analysis.jsonl)。

    run_dir 布局:
      trace/<runId>/trace.jsonl
      assets/<runId>/analysis.jsonl
    """
    rd = Path(run_dir)
    trace_files = sorted(rd.glob("trace/*/trace.jsonl"))
    if not trace_files:
        print(f"Error: no trace.jsonl found under {rd / 'trace'}", file=sys.stderr)
        sys.exit(1)
    trace_path = trace_files[0]
    run_id = trace_path.parent.name
    analysis_path = rd / "assets" / run_id / "analysis.jsonl"
    analyses: list[dict[str, Any]] = []
    if analysis_path.is_file():
        with open(analysis_path, encoding="utf-8") as f:
            analyses = [json.loads(l) for l in f if l.strip()]

    execs_by_step: dict[int, list[dict[str, Any]]] = {}
    trans_by_step: dict[int, dict[str, str]] = {}
    global_fsm: list[dict[str, Any]] = []
    page_transitions: list[dict[str, Any]] = []

    with open(trace_path, encoding="utf-8") as f:
        for line in f:
            r = json.loads(line)
            rt = r.get("record_type")
            if rt == "execution":
                ctx = r.get("context", {})
                sn = ctx.get("stepNumber")
                if sn is None:
                    continue
                execs_by_step.setdefault(sn, []).append(r)
            elif rt == "state_transition":
                sn = r.get("context", {}).get("stepNumber")
                if r.get("fsmType") == "TraversalFSM" and sn is not None:
                    trans_by_step[sn] = {
                        "from": r.get("fromState", ""),
                        "to": r.get("toState", ""),
                        "nodeId": r.get("context", {}).get("nodeId", ""),
                    }
                elif r.get("fsmType") == "GlobalFSM":
                    global_fsm.append({
                        "from": r.get("fromState", ""),
                        "to": r.get("toState", ""),
                        "stepNumber": sn,
                        "reason": r.get("reason", ""),
                    })
            elif rt == "page_transition":
                page_transitions.append(r)

    if not execs_by_step:
        print(f"Error: no execution records in {trace_path}", file=sys.stderr)
        sys.exit(1)

    max_step = max(execs_by_step)
    # 每步可能有多条 safety 动作 (scroll×2 + back), 全部保留
    actions_by_step: dict[int, list[dict[str, Any]]] = {}
    for sn, exs in execs_by_step.items():
        for e in exs:
            action = e.get("action", "")
            if action.startswith("safety.") and action.split(".", 1)[1] in _ACTION_TYPES:
                actions_by_step.setdefault(sn, []).append(e)

    # 时间轴: 全部引擎步, 含 action / 活动 / FSM 迁移 / 节点
    timeline: list[dict[str, Any]] = []
    for sn in range(1, max_step + 1):
        exs = execs_by_step.get(sn, [])
        trans = trans_by_step.get(sn, {})
        entry: dict[str, Any] = {
            "stepNumber": sn,
            "action": None,
            "actions": [],
            "x": None,
            "y": None,
            "success": None,
            "fromState": trans.get("from", ""),
            "toState": trans.get("to", ""),
            "nodeId": trans.get("nodeId", ""),
            "activities": _compact_activities(exs),
            "pageIdentity": None,
        }
        for act in actions_by_step.get(sn, []):
            name = act["action"].split(".", 1)[1]
            a = {"name": name,
                 "success": act.get("status") == "allow",
                 "pageIdentity": act.get("metadata", {}).get("pageIdentity"),
                 "x": 0.5, "y": 0.85 if name in ("scroll", "swipe") else 0.5}
            # 仅点击类动作用元素坐标; scroll/back 是触摸手势, 用兜底触摸点
            if name in ("click", "long_press", "input"):
                coord = _match_analysis_coord(analyses, act.get("timestamp", ""),
                                              act.get("targetValue", ""))
                if coord:
                    a["x"], a["y"] = coord
            entry["actions"].append(a)
        if entry["actions"]:
            first = entry["actions"][0]
            entry["action"] = first["name"]
            entry["x"], entry["y"] = first["x"], first["y"]
            entry["success"] = first["success"]
            entry["pageIdentity"] = first["pageIdentity"]
        timeline.append(entry)

    # 遍历树: 用 page_transition 模拟页面栈 (navigation push / press_back pop)
    root: dict[str, Any] = {"id": "root", "label": "root", "children": [], "edge": None}
    stack = [root]
    index = {"root": root}
    for pt in page_transitions:
        fr, to = pt.get("fromPage"), pt.get("toPage")
        tt = pt.get("transitionType", "")
        sn = pt.get("context", {}).get("stepNumber")
        if tt == "press_back":
            while len(stack) > 1 and stack[-1]["id"] != to:
                stack.pop()
            if stack[-1]["id"] != to:
                stack = [root]
            continue
        if not to or to == fr or stack[-1]["id"] == to:
            continue  # 重新识别当前页 → no-op
        node = index.get(to)
        if node is None:
            acts = actions_by_step.get(sn or -1, [])
            action = acts[0]["action"].split(".", 1)[1] if acts else "nav"
            node = {
                "id": to,
                "label": _node_label(to),
                "children": [],
                "edge": {"step": sn, "action": action},
            }
            index[to] = node
            stack[-1]["children"].append(node)
        stack.append(node)

    # 完成原因: GlobalFSM 最后一条迁移的 reason
    reason = (global_fsm[-1].get("reason") if global_fsm else "") or "max_steps"

    return {
        "schemaVersion": 3,
        "runId": run_id,
        "completionReason": reason,
        "totalSteps": max_step,
        "sourceMode": "trace",
        "timeline": timeline,
        "fsmStates": ["NodeSelect", "PreconditionCheck", "Execute", "ResultVerify", "Branch"],
        "globalFsm": global_fsm,
        "tree": root,
        "visitedPages": list(index.keys()),
        "fixture": None,
        "actionHistory": [  # 兼容旧字段: 仅设备动作
            {"action": t["action"], "stepNumber": t["stepNumber"], "success": t["success"],
             "x": t["x"], "y": t["y"], "pageIdentity": t["pageIdentity"],
             "targetValue": t["pageIdentity"], "elementId": t["pageIdentity"]}
            for t in timeline if t["action"]],
        "trace": [  # 兼容旧字段
            {"stepNumber": t["stepNumber"], "fromState": t["fromState"],
             "toState": t["toState"], "nodeId": t["nodeId"]}
            for t in timeline if t["fromState"]],
        "pageTransitions": page_transitions,
    }


# ── mock screenshot generation ──────────────────────────────────────
# When no real screenshots are available, render fixture pages as
# simple device-frame images with labelled controls so the viewer
# always has an image to show.

_SCREEN_W = 720
_SCREEN_H = 1480

# Android Settings-ish color palette
_CLR_BG = (26, 27, 32)           # dark surface
_CLR_HEADER = (34, 37, 44)       # top bar
_CLR_ITEM_BG = (40, 43, 51)      # list item
_CLR_ITEM_BORDER = (55, 58, 68)  # item separator
_CLR_TEXT_PRIMARY = (220, 222, 227)
_CLR_TEXT_SECONDARY = (150, 153, 160)
_CLR_ACCENT = (77, 166, 255)
_CLR_SWITCH_ON = (52, 211, 153)
_CLR_SWITCH_OFF = (80, 83, 91)
_CLR_TOGGLE_ON = (52, 211, 153)
_CLR_BACK = (120, 123, 130)
_CLR_TAB_ACTIVE = (77, 166, 255)
_CLR_TAB_BG = (34, 37, 44)
_CLR_SEARCH_BG = (55, 58, 68)

# Type → color mapping for the left accent bar
_TYPE_COLORS = {
    "menu_item": (230, 168, 23),   # amber
    "button": (96, 165, 250),       # blue
    "switch": (52, 211, 153),       # green
    "toggle": (52, 211, 153),
    "input": (34, 211, 238),        # cyan
    "tab": (167, 139, 250),         # violet
    "back_button": (248, 113, 113),  # red
    "icon": (107, 114, 128),
    "text": (107, 114, 128),
    "readonly": (107, 114, 128),
    "checkbox": (52, 211, 153),
    "slider": (34, 211, 238),
}


def _render_fixture_screenshots(data: dict[str, Any]) -> dict[int, dict[str, str]]:
    """为 fixture 的每个页面生成 mock 截图 PNG (data URI)。

    返回 stepIndex → {"after": dataURI, "imgW": w, "imgH": h} 映射。
    """
    if not HAS_PILLOW:
        return {}

    fixture = data.get("fixture")
    if not fixture:
        return {}

    pages = fixture.get("pages", {})
    if not pages:
        return {}

    # 为每个页面渲染一张截图
    page_images: dict[str, dict[str, object]] = {}
    for page_id, page in pages.items():
        uri = _render_page(page_id, page)
        if uri:
            page_images[page_id] = {"after": uri, "imgW": _SCREEN_W, "imgH": _SCREEN_H}

    # 按 actionHistory 推断每步所在页面, 分配截图
    action_history = data.get("actionHistory", [])
    transitions = fixture.get("transitions", [])
    initial_page = fixture.get("initialPage", "")

    result: dict[int, dict[str, object]] = {}
    current = initial_page
    if current and current in page_images:
        result[-1] = page_images[current]

    for i, action in enumerate(action_history):
        act = action.get("action", "")
        x = action.get("x")
        y = action.get("y")

        if (act == "click" or act == "long_press") and x is not None:
            page = pages.get(current, {})
            elements = page.get("elements", [])
            best = None
            best_dist = float("inf")
            for el in elements:
                d = math.hypot(el["x"] - x, el["y"] - y)
                if d < 0.06 and d < best_dist:
                    best = el
                    best_dist = d
            if best:
                for t in transitions:
                    if t["fromPage"] == current and t["trigger"] == best["id"]:
                        current = t["toPage"]
                        break
        elif act == "back":
            for t in transitions:
                if t["toPage"] == current:
                    current = t["fromPage"]
                    break

        if current and current in page_images:
            result[i] = page_images[current]

    return result


def _render_page(page_id: str, page: dict[str, Any]) -> str:
    """渲染单个页面为 PNG data URI。"""
    img = Image.new("RGB", (_SCREEN_W, _SCREEN_H), _CLR_BG)
    draw = ImageDraw.Draw(img)

    # 状态栏 (顶部)
    draw.rectangle([(0, 0), (_SCREEN_W, 60)], fill=_CLR_HEADER)
    # 时间 (模拟)
    draw.text((30, 18), "12:00", fill=_CLR_TEXT_PRIMARY)

    # 标题栏
    title_h = 100
    draw.rectangle([(0, 60), (_SCREEN_W, 60 + title_h)], fill=_CLR_HEADER)
    page_name = page.get("pageName", page_id)
    # 标题
    try:
        draw.text((60, 78), page_name, fill=_CLR_TEXT_PRIMARY)
    except Exception:
        draw.text((60, 78), page_name, fill=_CLR_TEXT_PRIMARY)

    elements = page.get("elements", [])
    tabs = [e for e in elements if e.get("type") == "tab"]
    back_buttons = [e for e in elements if e.get("type") == "back_button"]
    switches = [e for e in elements if e.get("type") == "switch"]
    toggles = [e for e in elements if e.get("type") == "toggle"]
    content = [e for e in elements if e.get("type") not in ("tab", "back_button", "switch", "toggle")]

    # Tabs (顶部)
    tab_y = 60 + title_h
    if tabs:
        draw.rectangle([(0, tab_y), (_SCREEN_W, tab_y + 56)], fill=_CLR_TAB_BG)
        tab_w = _SCREEN_W // len(tabs)
        for ti, tab in enumerate(tabs):
            tx = ti * tab_w
            cx = tx + tab_w // 2
            draw.text((cx - 20, tab_y + 18), tab.get("text", tab["id"])[:8], fill=_CLR_TAB_ACTIVE)
            draw.line([(tx + 10, tab_y + 50), (tx + tab_w - 10, tab_y + 50)], fill=_CLR_TAB_ACTIVE, width=2)
        content_start_y = tab_y + 60
    else:
        content_start_y = tab_y + 8

    # 返回按钮
    for bb in back_buttons:
        bx = int(bb["x"] * _SCREEN_W)
        by = int(bb["y"] * _SCREEN_H)
        draw.text((bx - 20, by - 14), "←", fill=_CLR_BACK)

    # 搜索框 (在 content 区域顶部)
    search_y = content_start_y
    if any(e.get("type") == "input" for e in content):
        draw.rounded_rectangle(
            [(24, search_y + 8), (_SCREEN_W - 24, search_y + 60)],
            radius=20, fill=_CLR_SEARCH_BG)
        search_text = next((e["text"] for e in content if e.get("type") == "input"), "Search")
        draw.text((50, search_y + 22), search_text, fill=_CLR_TEXT_SECONDARY)
        content_start_y = search_y + 68

    # 内容列表
    item_h = 84
    item_y = content_start_y + 4
    for ei, el in enumerate(content):
        if el.get("type") == "input":
            continue  # already rendered as search box
        el_type = el.get("type", "button")
        accent = _TYPE_COLORS.get(el_type, _CLR_TEXT_SECONDARY)

        # 列表项背景
        draw.rectangle(
            [(0, item_y), (_SCREEN_W, item_y + item_h)],
            fill=_CLR_ITEM_BG, outline=_CLR_ITEM_BORDER)

        # 左侧 accent 条 (menu_item / button)
        if el_type in ("menu_item", "button"):
            draw.rectangle(
                [(0, item_y + 12), (4, item_y + item_h - 12)],
                fill=accent)

        # 右侧 chevron
        if el_type in ("menu_item", "button"):
            cx, cy = _SCREEN_W - 30, item_y + item_h // 2
            draw.line([(cx - 6, cy - 8), (cx, cy), (cx - 6, cy + 8)], fill=_CLR_TEXT_SECONDARY, width=2)

        # 文本
        text = el.get("text", el["id"])
        draw.text((24, item_y + 18), text, fill=_CLR_TEXT_PRIMARY)
        # 副标题 (模拟)
        draw.text((24, item_y + 46), f"{el_type}", fill=_CLR_TEXT_SECONDARY)

        item_y += item_h + 1

    # Switches / Toggles (覆盖在右侧)
    for sw in switches:
        sx = int(sw["x"] * _SCREEN_W)
        sy = int(sw["y"] * _SCREEN_H)
        sw_on = sw.get("text", "").lower() == "on"
        color = _CLR_SWITCH_ON if sw_on else _CLR_SWITCH_OFF
        draw.rounded_rectangle(
            [(sx - 28, sy - 14), (sx + 28, sy + 14)],
            radius=14, fill=color)
        knob_x = sx + 14 if sw_on else sx - 14
        draw.ellipse(
            [(knob_x - 11, sy - 11), (knob_x + 11, sy + 11)],
            fill=(255, 255, 255))

    for tg in toggles:
        tx = int(tg["x"] * _SCREEN_W)
        ty = int(tg["y"] * _SCREEN_H)
        draw.rounded_rectangle(
            [(tx - 24, ty - 12), (tx + 24, ty + 12)],
            radius=12, fill=_CLR_TOGGLE_ON)
        draw.ellipse(
            [(tx + 4, ty - 8), (tx + 20, ty + 8)],
            fill=(255, 255, 255))

    # 底部导航栏
    nav_y = _SCREEN_H - 70
    draw.rectangle([(0, nav_y), (_SCREEN_W, _SCREEN_H)], fill=_CLR_HEADER)
    for ni, icon in enumerate(["◁", "○", "□"]):
        nx = _SCREEN_W // 6 + ni * _SCREEN_W // 3
        draw.text((nx - 8, nav_y + 20), icon, fill=_CLR_TEXT_SECONDARY)

    # 转 data URI
    buf = io.BytesIO()
    img.save(buf, format="PNG", optimize=True)
    return f"data:image/png;base64,{base64.b64encode(buf.getvalue()).decode()}"


def generate_html(data: dict[str, Any], screenshots: dict[int, dict[str, str]] | None = None) -> str:
    # Merge: real screenshots take priority, mock screenshots as fallback
    # Real screenshots now have {"before": ..., "after": ...} per step
    all_shots: dict[int, dict[str, str]] = {}
    if data.get("fixture") and HAS_PILLOW:
        mock = _render_fixture_screenshots(data)
        for k, v in mock.items():
            all_shots[k] = dict(v)  # already {"after": uri, "imgW": w, "imgH": h}
    if screenshots:
        for k, v in screenshots.items():
            all_shots.setdefault(k, {}).update(v)  # merge, real wins

    replay_json = _escape_json(data)
    shots_json = _escape_json(all_shots)

    run_id = data.get("runId", "unknown")
    reason = data.get("completionReason", "—")
    total = data.get("totalSteps", 0)

    return f"""\
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>Simulation Replay — {_h(run_id)}</title>
<style>
:root {{
    --bg: #0a0c0f; --surface: #13161a; --border: #2a2d33;
    --text: #c8ccd4; --text-dim: #6b7280; --accent: #4da6ff;
    --amber: #e6a817; --green: #34d399; --cyan: #22d3ee; --red: #f87171;
}}
*, *::before, *::after {{ box-sizing: border-box; margin: 0; padding: 0; }}
body {{
    font-family: system-ui, -apple-system, sans-serif;
    background: var(--bg); color: var(--text);
    height: 100vh; display: flex; flex-direction: column; overflow: hidden;
}}
.header {{
    padding: 8px 20px; border-bottom: 1px solid var(--border);
    display: flex; align-items: center; gap: 14px;
    background: var(--surface); flex-shrink: 0; font-size: 12px;
}}
.header h1 {{ font-size: 14px; font-weight: 600; }}
.header .meta {{ color: var(--text-dim); font-size: 11px; }}
.header .reason {{
    font-size: 10px; padding: 2px 8px; border-radius: 4px;
    background: #1a1a2e; border: 1px solid var(--border);
}}

/* ── Main ──────────────────────────────── */
.main {{ display: flex; flex: 1; overflow: hidden; }}

/* ── Phone ─────────────────────────────── */
.phone-col {{
    width: 400px; flex-shrink: 0; display: flex;
    flex-direction: column; align-items: center; padding: 16px;
    border-right: 1px solid var(--border); gap: 8px;
}}
.phone-frame {{
    width: 280px; height: 580px; border: 2px solid var(--border);
    border-radius: 8px; background: #111318; position: relative;
    overflow: hidden; box-shadow: 0 0 20px rgba(0,0,0,0.4);
}}
.screen-bg {{
    position: absolute; top: 0; left: 0; width: 100%; height: 100%;
    object-fit: contain;  /* 完整显示不裁剪, 上下留黑边 */
    z-index: 1;
}}
.screen-label {{
    position: absolute; top: 22px; left: 0; right: 0; text-align: center;
    font-size: 10px; color: var(--text-dim); z-index: 15;
    background: rgba(10,12,15,0.8); padding: 2px 0;
}}
/* placeholder when no screenshot */
.screen-placeholder {{
    position: absolute; top: 0; left: 0; width: 100%; height: 100%;
    display: flex; flex-direction: column; align-items: center;
    justify-content: center; color: #1e212a; z-index: 0;
}}
.screen-placeholder .phone-icon {{ font-size: 48px; opacity: 0.3; }}

/* ── Click markers ─────────────────────── */
@keyframes blink-pulse {{
    0%   {{ transform: translate(-50%,-50%) scale(0); opacity: 1; }}
    70%  {{ opacity: 0.6; }}
    100% {{ transform: translate(-50%,-50%) scale(4); opacity: 0; }}
}}
@keyframes blink-pulse-2 {{
    0%   {{ transform: translate(-50%,-50%) scale(0); opacity: 0.7; }}
    100% {{ transform: translate(-50%,-50%) scale(6); opacity: 0; }}
}}
.click-dot {{
    position: absolute; pointer-events: none; z-index: 25;
    width: 14px; height: 14px;
    border: 2.5px solid var(--red); border-radius: 50%;
    animation: blink-pulse 1s ease-out forwards;
}}
.click-dot::after {{
    content: ''; position: absolute; top: -6px; left: -6px;
    width: 22px; height: 22px;
    border: 1.5px solid rgba(248,113,113,0.5); border-radius: 50%;
    animation: blink-pulse-2 1.2s ease-out 0.1s forwards;
}}
.click-dot.back {{ border-color: var(--amber); }}
.click-dot.back::after {{ border-color: rgba(230,168,23,0.5); }}

/* ── Swipe / scroll animation ──────────── */
@keyframes swipe-line {{
    0%   {{ opacity: 1; transform: translate(-50%, 0) scaleY(0); }}
    30%  {{ opacity: 1; transform: translate(-50%, 0) scaleY(1); }}
    80%  {{ opacity: 0.7; }}
    100% {{ opacity: 0; transform: translate(-50%, 0) scaleY(1); }}
}}
@keyframes swipe-arrow {{
    0%   {{ opacity: 0; transform: translate(-50%, -50%); }}
    40%  {{ opacity: 1; }}
    100% {{ opacity: 0; transform: translate(-50%, calc(-50% + 40px)); }}
}}
.swipe-marker {{
    position: absolute; pointer-events: none; z-index: 25;
    top: 0; left: 0; width: 100%; height: 100%; overflow: hidden;
}}
.swipe-line {{
    position: absolute; left: 50%; transform: translateX(-50%);
    width: 3px; background: var(--cyan);
    border-radius: 2px;
    animation: swipe-line 1.2s ease-out forwards;
}}
.swipe-arrow {{
    position: absolute; left: 50%;
    width: 16px; height: 16px;
    border-right: 2.5px solid var(--cyan);
    border-bottom: 2.5px solid var(--cyan);
    transform: translate(-50%, -50%) rotate(45deg);
    animation: swipe-arrow 1.2s ease-out forwards;
}}

/* trail dots (past clicks, faded) */
.trail-dot {{
    position: absolute; pointer-events: none; z-index: 24;
    width: 6px; height: 6px; border-radius: 50%;
    background: rgba(248,113,113,0.35);
    transform: translate(-50%, -50%);
}}

.page-bar-below {{
    font-size: 10px; color: var(--text-dim); text-align: center;
}}
.page-bar-below span {{ padding: 2px 8px; border-radius: 10px; border: 1px solid var(--border); }}
.page-bar-below span.active {{ border-color: var(--accent); color: var(--accent); }}

/* ── Timeline ──────────────────────────── */
.timeline-col {{
    flex: 1; display: flex; flex-direction: column;
    border-right: 1px solid var(--border); min-width: 0;
}}
.timeline-head {{
    padding: 8px 14px; border-bottom: 1px solid var(--border);
    font-size: 10px; font-weight: 600; color: var(--text-dim);
    text-transform: uppercase; letter-spacing: 1px;
    display: flex; justify-content: space-between; flex-shrink: 0;
}}
.timeline-list {{ flex: 1; overflow-y: auto; }}
.timeline-row {{
    padding: 5px 14px; cursor: pointer; font-size: 11px;
    border-left: 2px solid transparent; display: flex; align-items: center; gap: 8px;
}}
.timeline-row:hover {{ background: var(--surface); }}
.timeline-row.active {{ background: var(--surface); border-left-color: var(--accent); }}
.timeline-row .n {{ color: var(--text-dim); font-size: 10px; min-width: 36px; }}
.timeline-row .icon {{ font-size: 12px; min-width: 16px; text-align: center; }}
.timeline-row .icon.click {{ color: var(--red); }}
.timeline-row .icon.back {{ color: var(--amber); }}
.timeline-row .icon.scroll {{ color: var(--cyan); }}
.timeline-row .desc {{ flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }}
.timeline-row .ok {{ color: var(--text-dim); font-size: 10px; }}

/* ── Controls ──────────────────────────── */
.ctrls {{
    padding: 8px 14px; border-top: 1px solid var(--border);
    display: flex; align-items: center; gap: 6px; flex-shrink: 0;
    background: var(--surface);
}}
.ctrls button {{
    background: #1a1d24; border: 1px solid var(--border); color: var(--text);
    font-size: 12px; padding: 4px 10px; border-radius: 4px; cursor: pointer;
}}
.ctrls button:hover {{ background: #252830; }}
.ctrls button.play {{
    background: var(--accent); color: #0a0c0f; border-color: var(--accent);
    font-weight: 600; min-width: 56px;
}}
.ctrls button.play:hover {{ background: #6db8ff; }}
.ctrls .info {{ font-size: 10px; color: var(--text-dim); margin-left: 6px; }}
.ctrls input[type="range"] {{ flex: 1; accent-color: var(--accent); height: 4px; }}

/* ── Detail ─────────────────────────────── */
.detail-col {{
    width: 250px; flex-shrink: 0; padding: 12px; overflow-y: auto;
    display: flex; flex-direction: column; gap: 10px;
}}
.card {{
    background: var(--surface); border: 1px solid var(--border);
    border-radius: 6px; padding: 10px;
}}
.card h3 {{
    font-size: 9px; text-transform: uppercase; letter-spacing: 1px;
    color: var(--text-dim); margin-bottom: 6px;
}}
.card .row {{
    display: flex; justify-content: space-between; font-size: 11px; padding: 2px 0;
}}
.card .row .l {{ color: var(--text-dim); }}
.card .row .v {{ font-weight: 500; }}
.card .row .v.ok {{ color: var(--green); }}
.card .row .v.fail {{ color: var(--red); }}
.fsm-lane {{
    display: flex; align-items: center; justify-content: center;
    flex-wrap: wrap; gap: 2px; padding: 6px 4px;
    background: #1a1a2e; border: 1px solid #2a2d3a; border-radius: 4px;
}}
.fsm-lane .arr {{ color: var(--accent); margin: 0 2px; font-size: 9px; }}
.fsm-global {{
    font-size: 9px; color: var(--text-dim); text-align: center;
    padding: 4px 8px 0; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
}}
.fsm-global b {{ color: var(--text); }}
.state-chip {{
    font-size: 9px; font-weight: 600; padding: 2px 6px; border-radius: 3px;
    border: 1px solid var(--border); color: var(--text-dim);
    background: #13161a; white-space: nowrap;
}}
.state-chip.active {{ color: #0a0c0f; border-color: transparent; }}
.state-chip.c-NodeSelect.active {{ background: var(--cyan); }}
.state-chip.c-PreconditionCheck.active {{ background: var(--amber); }}
.state-chip.c-Execute.active {{ background: var(--green); }}
.state-chip.c-ResultVerify.active {{ background: #a78bfa; }}
.state-chip.c-Branch.active {{ background: var(--accent); }}
.act-chips {{ display: flex; flex-wrap: wrap; gap: 4px; }}
.act-chip {{
    font-size: 9px; padding: 1px 5px; border-radius: 3px;
    border: 1px solid var(--border); color: var(--text-dim); background: #1a1d24;
}}
.act-chip.warn {{ color: var(--red); border-color: var(--red); }}
.tree {{
    max-height: 240px; overflow: auto; font-size: 10px;
    font-family: ui-monospace, Menlo, monospace;
}}
.tree .tnode {{
    display: flex; align-items: center; gap: 6px; white-space: nowrap;
    cursor: pointer; padding: 1px 4px; border-radius: 3px;
}}
.tree .tnode:hover {{ background: var(--surface); }}
.tree .tnode.current {{ background: rgba(77, 166, 255, 0.12); }}
.tree .tlabel {{ color: var(--text); }}
.tree .tlabel.cur {{ color: var(--accent); font-weight: 700; }}
.tree .tbadge {{
    font-size: 9px; color: var(--text-dim); border: 1px solid var(--border);
    border-radius: 3px; padding: 0 4px;
}}

::-webkit-scrollbar {{ width: 5px; }}
::-webkit-scrollbar-track {{ background: transparent; }}
::-webkit-scrollbar-thumb {{ background: var(--border); border-radius: 3px; }}
</style>
</head>
<body>

<div class="header">
    <h1>🎬 Simulation Replay</h1>
    <span class="meta">Run: <span id="runId">{_h(run_id)}</span></span>
    <span class="reason">{_h(reason)}</span>
    <span class="meta">Steps: {total}</span>
</div>

<div class="main">

<!-- Phone -->
<div class="phone-col">
    <div class="phone-frame">
        <img class="screen-bg" id="screenImg" style="display:none" alt="">
        <div class="screen-placeholder" id="screenPlaceholder">
            <span class="phone-icon">📱</span>
        </div>
        <div class="screen-label" id="screenLabel">—</div>
    </div>
    <div class="page-bar-below" id="pageBar"></div>
</div>

<!-- Timeline -->
<div class="timeline-col">
    <div class="timeline-head">
        <span>Timeline</span>
        <span id="timelineCount"></span>
    </div>
    <div class="timeline-list" id="timelineList"></div>
    <div class="ctrls">
        <button onclick="goto(0)">⏮</button>
        <button onclick="goto(current-1)">◀</button>
        <button class="play" id="playBtn" onclick="togglePlay()">▶ Play</button>
        <button onclick="goto(current+1)">▶</button>
        <button onclick="goto(steps.length-1)">⏭</button>
        <button id="beforeBtn" onclick="toggleBefore()" title="Toggle before/after screenshot" style="font-size:10px;padding:3px 6px;">🅰 After</button>
        <input type="range" id="scrub" min="0" max="0" value="0" oninput="goto(+this.value)">
        <span class="info" id="stepInfo">0 / 0</span>
    </div>
</div>

<!-- Detail -->
<div class="detail-col">
    <div class="card">
        <h3>Action</h3>
        <div class="row"><span class="l">Type</span><span class="v" id="dType">—</span></div>
        <div class="row"><span class="l">Position</span><span class="v" id="dPos">—</span></div>
        <div class="row"><span class="l">Element</span><span class="v" id="dElem">—</span></div>
        <div class="row"><span class="l">Success</span><span class="v" id="dOk">—</span></div>
    </div>
    <div class="card">
        <h3>Node</h3>
        <div class="row"><span class="l">Current</span><span class="v" id="dNode">—</span></div>
        <div class="row"><span class="l">Page</span><span class="v" id="dPage">—</span></div>
    </div>
    <div class="card">
        <h3>Activity</h3>
        <div class="act-chips" id="dActs">—</div>
    </div>
    <div class="card">
        <h3>Traversal FSM</h3>
        <div class="fsm-lane" id="fsmLane">—</div>
        <div class="fsm-global" id="fsmGlobal"></div>
    </div>
    <div class="card">
        <h3>Traversal Tree</h3>
        <div class="tree" id="treeBox">—</div>
    </div>
</div>

</div>

<script>
const REPLAY = {replay_json};
const SCREENSHOTS = {shots_json};

function h(s) {{ return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }}

const fixture = REPLAY.fixture || null;
const actions = REPLAY.actionHistory || [];
const visited = REPLAY.visitedPages || [];
const trace = REPLAY.trace || [];
const timeline = REPLAY.timeline;
const fsmStates = REPLAY.fsmStates || [];
const globalFsm = REPLAY.globalFsm || [];
const tree = REPLAY.tree || null;

const stateShort = {{NodeSelect:'NS', PreconditionCheck:'PC', Execute:'EX', ResultVerify:'RV', Branch:'BR'}};

// trace 模式: 全部引擎步; replay JSON 模式: 兼容旧 actionHistory + trace
const steps = timeline ? timeline.map((t, i) => ({{
    index: i,
    stepNumber: t.stepNumber || (i + 1),
    action: t.action || null,
    actions: t.actions || [],
    x: t.x, y: t.y, success: t.success,
    nodeId: t.nodeId || null,
    elementId: t.elementId || null,
    pageIdentity: t.pageIdentity || null,
    fromState: t.fromState || "", toState: t.toState || "",
    activities: t.activities || [],
}})) : actions.map((a, i) => {{
    const t = trace.find(tr => tr.stepNumber === a.stepNumber) || {{}};
    return {{
        index: i,
        stepNumber: a.stepNumber || (i + 1),
        action: a.action,
        actions: a.action ? [{{name: a.action, x: a.x, y: a.y, success: a.success}}] : [],
        x: a.x, y: a.y, success: a.success,
        elementId: a.elementId || null,
        pageIdentity: a.pageIdentity || null,
        fromState: t.fromState || "", toState: t.toState || "",
        pageFrom: t.pageFrom || null, pageTo: t.pageTo || null,
        activities: [],
    }};
}});

let current = -1;
let timer = null;
let currentPageId = fixture ? fixture.initialPage : null;
let trailDots = [];

// ── page tracking ──────────────────────────
function findElement(x, y) {{
    if (!fixture || !fixture.pages || !currentPageId) return null;
    const page = fixture.pages[currentPageId];
    if (!page || !page.elements) return null;
    let best = null, bestDist = Infinity;
    for (const el of page.elements) {{
        const d = Math.hypot(el.x - x, el.y - y);
        if (d < 0.06 && d < bestDist) {{ best = el; bestDist = d; }}
    }}
    return best;
}}

function resolvePage(fromId, elemId) {{
    if (!fixture || !fixture.transitions) return null;
    const t = fixture.transitions.find(tr => tr.fromPage === fromId && tr.trigger === elemId);
    return t ? t.toPage : null;
}}

// ── render ─────────────────────────────────
let showBefore = false;  // toggle: false=after, true=before

// ── tree helpers ───────────────────────────
function findShot(key) {{
    // 内容去重: 无条目时回退到最近可用帧 (内容与上一帧相同)
    for (let k = key; k >= 0; k--) {{ const e = SCREENSHOTS[k]; if (e) return e; }}
    return null;
}}

function treeWalk(fn) {{
    if (!tree) return;
    (function walk(n) {{ fn(n); for (const c of n.children || []) walk(c); }})(tree);
}}

function findNodeLabel(nodeId) {{
    let label = null;
    treeWalk(n => {{ if (n.id === nodeId) label = n.label; }});
    return label;
}}

function findNodeStep(nodeId) {{
    return steps.findIndex(s => s.nodeId === nodeId);
}}

// ── coordinate scaling: normalized (0..1) → pixel in phone frame ──
function scaleCoord(step) {{
    if (step.x == null || step.y == null) return null;
    const shotKey = step ? (step.stepNumber || step.index + 1) - 1 : -1;
    const shotEntry = findShot(shotKey) || {{}};
    const imgW = shotEntry.imgW || 1080;
    const imgH = shotEntry.imgH || 2400;
    const frame = document.querySelector('.phone-frame');
    const fw = frame.clientWidth, fh = frame.clientHeight;
    const imgRatio = imgW / imgH, frameRatio = fw / fh;
    let drawW, drawH, offsetX, offsetY;
    // object-fit: contain → 完整显示不裁剪, 窄边留黑边
    if (imgRatio > frameRatio) {{
        drawW = fw; drawH = fw / imgRatio;
        offsetX = 0; offsetY = (fh - drawH) / 2;
    }} else {{
        drawH = fh; drawW = fh * imgRatio;
        offsetX = (fw - drawW) / 2; offsetY = 0;
    }}
    return {{
        left: ((offsetX + step.x * drawW) / fw * 100).toFixed(2) + '%',
        top: ((offsetY + step.y * drawH) / fh * 100).toFixed(2) + '%',
    }};
}}

function renderScreen(step) {{
    const img = document.getElementById('screenImg');
    const ph = document.getElementById('screenPlaceholder');
    const label = document.getElementById('screenLabel');
    const frame = document.querySelector('.phone-frame');

    // clear dots + swipes
    frame.querySelectorAll('.click-dot,.trail-dot,.swipe-marker').forEach(el => el.remove());

    // screenshot — use stepNumber (1-indexed) → 0-indexed key, 去重回退
    const shotKey = step ? (step.stepNumber || step.index + 1) - 1 : -1;
    const shotEntry = findShot(shotKey);
    if (shotEntry) {{
        const key = showBefore ? 'before' : 'after';
        const src = shotEntry[key] || shotEntry['after'] || shotEntry['before'];
        if (src) {{
            img.src = src; img.style.display = '';
            ph.style.display = 'none';
        }} else {{
            img.style.display = 'none'; ph.style.display = '';
        }}
    }} else {{
        img.style.display = 'none'; ph.style.display = '';
    }}

    // page label
    if (step && step.nodeId) {{
        label.textContent = findNodeLabel(step.nodeId) || step.nodeId;
    }} else if (fixture && fixture.pages && currentPageId) {{
        const page = fixture.pages[currentPageId];
        label.textContent = page ? page.pageName : currentPageId;
    }} else {{
        label.textContent = currentPageId || '—';
    }}

    // trail dots (all past clicks)
    for (const td of trailDots) {{
        const dot = document.createElement('div');
        dot.className = 'trail-dot';
        const tpos = scaleCoord(td);
        if (tpos) {{
            dot.style.left = tpos.left;
            dot.style.top = tpos.top;
            frame.appendChild(dot);
        }}
    }}
}}

function showClickMarker(step) {{
    // 一步可能有多条动作 (scroll×2 + back), 全部标记
    const acts = (step.actions && step.actions.length) ? step.actions
                 : (step.action ? [step] : []);
    const frame = document.querySelector('.phone-frame');
    for (const a of acts) {{
        const action = a.name || a.action;
        if (a.x == null || a.y == null) continue;
        const pos = scaleCoord({{...step, x: a.x, y: a.y}});
        if (!pos) continue;

        if (action === 'scroll' || action === 'swipe') {{
            // vertical swipe indicator: line + arrow at touch point
            const container = document.createElement('div');
            container.className = 'swipe-marker';
            container.style.left = pos.left;
            container.style.top = pos.top;
            container.style.width = '32px';
            container.style.height = '60px';
            container.style.transform = 'translate(-50%, -50%)';

            // vertical line
            const line = document.createElement('div');
            line.className = 'swipe-line';
            line.style.top = '0';
            line.style.height = '100%';
            container.appendChild(line);

            // downward arrow (swipe up to scroll down)
            const arrow = document.createElement('div');
            arrow.className = 'swipe-arrow';
            arrow.style.top = '80%';
            arrow.style.transform = 'translate(-50%, -50%) rotate(45deg)';
            container.appendChild(arrow);

            frame.appendChild(container);
        }} else {{
            const dot = document.createElement('div');
            dot.className = 'click-dot';
            if (action === 'back') dot.classList.add('back');
            dot.style.left = pos.left;
            dot.style.top = pos.top;
            frame.appendChild(dot);
        }}
    }}
}}

function renderTimeline() {{
    const list = document.getElementById('timelineList');
    document.getElementById('timelineCount').textContent = steps.length + ' steps';
    list.innerHTML = '';
    const icons = {{click:'👆', back:'↩', scroll:'↕', swipe:'↕', input:'⌨', long_press:'🖐', wait:'⏳'}};

    for (const s of steps) {{
        const div = document.createElement('div');
        div.className = 'timeline-row' + (s.index === current ? ' active' : '');
        div.onclick = () => goto(s.index);
        // FSM 状态 chip: 该步进入的 toState
        let chip = '';
        if (s.toState && fsmStates.length) {{
            chip = `<span class="state-chip c-${{s.toState}}">${{stateShort[s.toState] || s.toState}}</span>`;
        }}
        // 描述: 有动作显示动作 (可能多条), 否则显示活动摘要
        let desc;
        if (s.actions && s.actions.length > 1) {{
            const counts = {{}};
            for (const a of s.actions) counts[a.name] = (counts[a.name] || 0) + 1;
            desc = Object.entries(counts).map(([n, c]) =>
                `${{icons[n] || '●'}} ${{n}}${{c > 1 ? ' ×' + c : ''}}`).join(' · ');
        }} else if (s.action) {{
            const pos = s.x != null ? ` (${{s.x.toFixed(2)}},${{s.y.toFixed(2)}})` : '';
            desc = `${{icons[s.action] || '●'}} ${{s.action}}${{pos}}`;
        }} else if (s.activities && s.activities.length) {{
            desc = s.activities.join(' · ');
        }} else {{
            desc = 'page analysis';
        }}
        const ok = s.action ? `<span class="ok">${{s.success ? '✓' : '✗'}}</span>` : '';
        div.innerHTML = `<span class="n">#${{s.stepNumber}}</span>${{chip}}
            <span class="desc">${{desc}}</span>${{ok}}`;
        list.appendChild(div);
    }}
    const active = list.querySelector('.timeline-row.active');
    if (active) active.scrollIntoView({{block:'nearest',behavior:'smooth'}});
}}

function renderFsmLane(step) {{
    const lane = document.getElementById('fsmLane');
    if (!fsmStates.length) {{
        lane.innerHTML = '<span style="color:var(--text-dim)">(no fsm data)</span>';
        document.getElementById('fsmGlobal').textContent = '';
        return;
    }}
    let html = '';
    for (let i = 0; i < fsmStates.length; i++) {{
        const s = fsmStates[i];
        if (i) html += '<span class="arr">→</span>';
        const active = step && step.toState === s ? ' active' : '';
        html += `<span class="state-chip c-${{s}}${{active}}" title="${{step && step.toState === s ? step.fromState + ' → ' + step.toState + ' (step ' + step.stepNumber + ')' : s}}">${{stateShort[s] || s}}</span>`;
    }}
    lane.innerHTML = html;
    const g = document.getElementById('fsmGlobal');
    if (globalFsm.length) {{
        g.innerHTML = globalFsm.map((tr, i) => {{
            const reason = tr.reason ? ` (${{tr.reason}})` : '';
            return (i ? ' → ' : '') + `<b>${{tr.to}}</b>${{reason}}`;
        }}).join('');
    }} else {{
        g.innerHTML = '';
    }}
}}

function renderTree() {{
    const box = document.getElementById('treeBox');
    if (!tree) {{
        box.innerHTML = '<span style="color:var(--text-dim)">(no tree data)</span>';
        return;
    }}
    const curNode = current >= 0 ? (steps[current].nodeId || null) : null;
    let html = '';
    (function walk(n, depth) {{
        const isCur = n.id === curNode;
        const badge = n.edge ? ` <span class="tbadge">#${{n.edge.step}} ${{n.edge.action}}</span>` : '';
        html += `<div class="tnode${{isCur ? ' current' : ''}}" style="padding-left:${{depth * 16}}px"
                     onclick="jumpToNode('${{n.id.replace(/'/g, "\\\\'")}}')" title="${{h(n.id)}}">
                    <span class="tlabel${{isCur ? ' cur' : ''}}">${{h(n.label)}}</span>${{badge}}</div>`;
        for (const c of n.children || []) walk(c, depth + 1);
    }})(tree, 0);
    box.innerHTML = html;
    const cur = box.querySelector('.tnode.current');
    if (cur) cur.scrollIntoView({{block:'nearest'}});
}}

function jumpToNode(nodeId) {{
    const i = findNodeStep(nodeId);
    if (i >= 0) goto(i);
}}

function renderDetail(step) {{
    const na = (id) => {{ document.getElementById(id).textContent = '—'; }};
    if (!step) {{
        ['dType','dPos','dElem','dOk','dNode','dPage','dActs'].forEach(na);
        renderFsmLane(null);
        return;
    }}
    document.getElementById('dType').textContent = step.action || '—';
    const pos = step.x != null ? `(${{step.x.toFixed(4)}}, ${{step.y.toFixed(4)}})` : '—';
    document.getElementById('dPos').textContent = pos;
    document.getElementById('dElem').textContent = step.elementId || (step.action || '—');
    const ok = document.getElementById('dOk');
    if (step.action) {{
        ok.textContent = step.success ? '✓ true' : '✗ false';
        ok.className = 'v ' + (step.success ? 'ok' : 'fail');
    }} else {{
        ok.textContent = '—';
        ok.className = 'v';
    }}
    document.getElementById('dNode').textContent =
        step.nodeId ? (findNodeLabel(step.nodeId) || step.nodeId) : '—';
    document.getElementById('dPage').textContent = step.pageIdentity || currentPageId || '—';
    renderFsmLane(step);
    const acts = document.getElementById('dActs');
    if (step.activities && step.activities.length) {{
        acts.innerHTML = step.activities.map(a =>
            `<span class="act-chip${{a.includes('✗') ? ' warn' : ''}}">${{h(a)}}</span>`).join('');
    }} else {{
        acts.textContent = '—';
    }}
}}

function renderPageBar() {{
    const bar = document.getElementById('pageBar');
    if (!fixture || !fixture.pages) {{
        if (tree) {{
            const curNode = current >= 0 ? (steps[current].nodeId || null) : null;
            let html = '';
            treeWalk(n => {{
                const cls = n.id === curNode ? 'active' : '';
                html += `<span class="${{cls}}" onclick="jumpToNode('${{n.id.replace(/'/g, "\\\\'")}}')">${{h(n.label)}}</span> `;
            }});
            bar.innerHTML = html;
            return;
        }}
        bar.innerHTML = '<span style="color:var(--text-dim)">(no fixture)</span>';
        return;
    }}
    const ids = visited.length > 0 ? [...new Set(visited)] : Object.keys(fixture.pages);
    let html = '';
    for (const pid of ids) {{
        const page = fixture.pages[pid];
        const name = page ? page.pageName : pid;
        const cls = pid === currentPageId ? 'active' : '';
        html += `<span class="${{cls}}" onclick="jumpPage('${{pid.replace(/'/g,"\\\\'")}}')">${{h(name)}}</span> `;
    }}
    bar.innerHTML = html;
}}

function jumpPage(pageId) {{
    if (fixture && fixture.pages && fixture.pages[pageId]) {{
        currentPageId = pageId;
        renderScreen(steps[current] || null);
        renderPageBar();
    }}
}}

// ── navigation ─────────────────────────────
function goto(n) {{
    n = Math.max(0, Math.min(steps.length - 1, n));
    if (n === current) return;
    current = n;
    const step = steps[n];

    // page tracking
    if ((step.action === 'click' || step.action === 'long_press') && step.x != null) {{
        const el = findElement(step.x, step.y);
        if (el) {{
            const next = resolvePage(currentPageId, el.id);
            if (next) currentPageId = next;
        }}
    }} else if (step.action === 'back') {{
        if (fixture && fixture.transitions) {{
            const rev = fixture.transitions.find(t => t.toPage === currentPageId);
            if (rev) currentPageId = rev.fromPage;
        }}
    }}

    // trail: all actions up to this step
    trailDots = [];
    for (let i = 0; i <= n; i++) {{
        const s = steps[i];
        if (s.actions && s.actions.length) {{
            for (const a of s.actions) {{
                if (a.x != null) trailDots.push({{x: a.x, y: a.y}});
            }}
        }} else if (s.x != null) {{
            trailDots.push({{x: s.x, y: s.y}});
        }}
    }}

    renderScreen(step);
    showClickMarker(step);
    renderTimeline();
    renderDetail(step);
    renderPageBar();

    document.getElementById('scrub').value = n;
    document.getElementById('stepInfo').textContent = (n+1) + ' / ' + steps.length;
}}

function toggleBefore() {{
    showBefore = !showBefore;
    document.getElementById('beforeBtn').textContent = showBefore ? '🅱 Before' : '🅰 After';
    renderScreen(steps[current] || null);
    if (current >= 0) showClickMarker(steps[current]);
}}

function togglePlay() {{
    const btn = document.getElementById('playBtn');
    if (timer) {{
        clearInterval(timer); timer = null;
        btn.textContent = '▶ Play';
    }} else {{
        if (current >= steps.length - 1) goto(0);
        btn.textContent = '⏸ Pause';
        timer = setInterval(() => {{
            if (current >= steps.length - 1) {{ togglePlay(); return; }}
            goto(current + 1);
        }}, 800);
    }}
}}

document.addEventListener('keydown', e => {{
    if (e.key === 'ArrowRight') goto(current + 1);
    else if (e.key === 'ArrowLeft') goto(current - 1);
    else if (e.key === ' ') {{ e.preventDefault(); togglePlay(); }}
    else if (e.key === 'Home') goto(0);
    else if (e.key === 'End') goto(steps.length - 1);
}});

// init
document.getElementById('scrub').max = Math.max(0, steps.length - 1);
renderTimeline();
renderScreen(null);
renderPageBar();
renderDetail(null);
renderTree();
if (steps.length > 0) goto(0);
</script>
</body>
</html>"""


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Simulation Replay Viewer — 仿真回放 JSON 或 run 目录 → 自包含 HTML")
    parser.add_argument("input", nargs="?", help="replay JSON (TraceReplayHarness.ExportReplayJson 产物)")
    parser.add_argument("--run-dir", help="run 目录 (自动解析 trace.jsonl + analysis.jsonl + 截图)")
    parser.add_argument("-o", "--output", help="输出 HTML 路径 (默认: 输入同名 .html 或 replay.html)")
    parser.add_argument("--screenshots", help="截图目录 (含 steps/N/before.png + after.png); --run-dir 时自动定位")
    parser.add_argument("--max-screenshots", type=int, default=0,
                        help="最多嵌入步数 (默认: 0 = 全部, 内容去重后通常很小)")
    parser.add_argument("--open", action="store_true", dest="open_browser",
                        help="生成后在浏览器中打开")
    args = parser.parse_args()

    if args.run_dir:
        data = build_replay_from_run(args.run_dir)
        output_path = Path(args.output) if args.output else Path("replay.html")
        # 自动定位截图目录: assets/<runId>/ (含 steps/N/before.png)
        if not args.screenshots:
            rd = Path(args.run_dir)
            assets = [p for p in sorted(rd.glob("assets/*"))
                      if (p / "steps").is_dir()]
            if assets:
                args.screenshots = str(assets[-1])
    elif args.input:
        input_path = Path(args.input)
        if not input_path.exists():
            print(f"Error: file not found: {args.input}", file=sys.stderr)
            sys.exit(1)
        data = load_replay(str(input_path))
        output_path = Path(args.output) if args.output else input_path.with_suffix(".html")
    else:
        parser.print_usage(sys.stderr)
        print("Error: 需要 input (replay JSON) 或 --run-dir", file=sys.stderr)
        sys.exit(1)

    shots = _find_screenshots(args.screenshots, data, max_steps=args.max_screenshots)

    html = generate_html(data, shots)
    output_path.write_text(html, encoding="utf-8")
    print(f"✅ Replay HTML written to: {output_path}")
    if shots:
        before_count = sum(1 for v in shots.values() if 'before' in v)
        after_count = sum(1 for v in shots.values() if 'after' in v)
        print(f"   Embedded {len(shots)} steps × ~2 (before={before_count}, after={after_count})")

    if args.open_browser:
        webbrowser.open(output_path.resolve().as_uri())


if __name__ == "__main__":
    main()
