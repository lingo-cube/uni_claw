# PROTOCOL-MACHINE-002 — 状态机可视化出口（第四出口）
# 消费 schemas/protocol/state-machine.yaml 单源，渲染自包含 HTML。
# 纯派生、无时间戳；手改产物会被 --check 型纪律抓出（先例：gen-protocol-machine）。
from __future__ import annotations
import html, sys
from pathlib import Path
import yaml

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "schemas" / "protocol" / "state-machine.yaml"
OUT = ROOT / "docs" / "design" / "state-machine-viz-v0.1.html"

def esc(x): return html.escape(str(x), quote=True)

def tag(v):
    if v in (None, [], ""): return ""
    return v if isinstance(v, list) else [v]

def render() -> str:
    doc = yaml.safe_load(SRC.read_text(encoding="utf-8"))
    machines = doc["machines"]
    parts = []
    parts.append("""<!DOCTYPE html><html lang="zh-CN"><head><meta charset="UTF-8">
<title>UniClaw 协议状态机可视化（单源生成）</title><style>
:root{--ink:#1a2233;--sub:#5a6478;--line:#d7dce6;--red:#dc2626;--green:#059669;--blue:#2563eb;--gold:#b45309}
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:"Microsoft YaHei",sans-serif;color:var(--ink);background:#f5f7fa;line-height:1.6;padding:24px}
h1{font-size:22px} h1 small{display:block;font-size:12px;color:var(--sub);font-weight:400;margin-top:4px}
details{background:#fff;border:1px solid var(--line);border-radius:12px;margin:14px 0;padding:12px 16px}
summary{font-size:15px;font-weight:800;cursor:pointer} summary .meta{font-weight:400;font-size:12px;color:var(--sub)}
.chips{display:flex;flex-wrap:wrap;gap:8px;margin:10px 0}
.chip{border:1.5px solid var(--line);border-radius:8px;padding:3px 10px;font-size:13px;background:#fff}
.chip.initial{border-color:var(--blue);background:#eaf1fe} .chip.initial::before{content:"◉ ";color:var(--blue)}
.chip.terminal{border-color:var(--green);background:#e7f6f0} .chip.terminal::before{content:"▣ ";color:var(--green)}
.chip.absorbing{border-color:var(--red);background:#fdeaea} .chip.absorbing::before{content:"⛔ ";color:var(--red)}
table{width:100%;border-collapse:collapse;font-size:12.5px;margin-top:8px}
th{background:#eef1f6;text-align:left;padding:5px 8px} td{padding:5px 8px;border-bottom:1px solid var(--line);vertical-align:top}
code{font-family:Consolas,monospace;font-size:11.5px;background:#eef1f6;border-radius:3px;padding:1px 5px}
.m{display:inline-block;font-size:10.5px;font-weight:800;border-radius:3px;padding:0 6px;color:#fff;margin-left:4px}
.m-measure{background:var(--gold)} .m-await{background:var(--sub)} .m-edge{background:var(--blue)}
footer{margin-top:20px;color:var(--sub);font-size:12px;border-top:1px solid var(--line);padding-top:10px}
</style></head><body>
<h1>UniClaw 协议状态机可视化<small>单源生成：schemas/protocol/state-machine.yaml · 生成物勿手改 · 语义权威在协议基线与 ADR</small></h1>""")
    total_t = 0
    for key, m in machines.items():
        nodes = m.get("nodes") or []
        init, term, absorbing = m.get("initial"), set(tag(m.get("terminal_nodes"))), set(tag(m.get("absorbing")))
        trans = m.get("transitions") or []
        total_t += len(trans)
        parts.append(f"<details open><summary>{esc(m.get('title', key))} <span class='meta'>· {len(nodes)} 状态 · {len(trans)} 转移</span></summary>")
        parts.append('<div class="chips">')
        for n in nodes:
            cls = "absorbing" if n in absorbing else ("terminal" if n in term else ("initial" if n == init else ""))
            parts.append(f'<span class="chip {cls}" title="{esc(n)}">{esc(n)}</span>')
        parts.append("</div>")
        parts.append('<table><tr><th>转移</th><th>from → to</th><th>触发</th><th>guard</th><th>emits / 标注</th></tr>')
        for t in trans:
            tr = t.get("trigger") or {}
            edge = tr.get("edge"); event = tr.get("event", "")
            trig = (f'<span class="m m-edge">{esc(edge)}</span> ' if edge else "") + esc(event)
            extras = []
            for e in tag(t.get("emits")): extras.append(f"emit: {esc(e)}")
            if t.get("measure"): extras.append(f'<span class="m m-measure">measure: {esc(t["measure"])}</span>')
            if t.get("await"): extras.append(f'<span class="m m-await">await: {esc(t["await"])}</span>')
            parts.append(f"<tr><td><code>{esc(t.get('id',''))}</code></td>"
                         f"<td><b>{esc(t.get('from',''))}</b> → <b>{esc(t.get('to',''))}</b></td>"
                         f"<td>{trig}</td><td>{esc(t.get('guard',''))}</td><td>{' · '.join(extras)}</td></tr>")
        parts.append("</table></details>")
    parts.append(f"<footer>共 {len(machines)} 台机器 · {total_t} 条转移 · 生成器 tools/gen-state-machine-viz.py（PROTOCOL-MACHINE-002）</footer></body></html>")
    return "".join(parts)

if __name__ == "__main__":
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(render(), encoding="utf-8")
    print(f"written: {OUT}")
