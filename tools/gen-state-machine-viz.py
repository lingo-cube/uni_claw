# PROTOCOL-MACHINE-002 — 状态机可视化出口（工作流风格：节点点击 → 协议明细面板）
from __future__ import annotations
import html, json
from pathlib import Path
import yaml

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "schemas" / "protocol" / "state-machine.yaml"
OUT = ROOT / "docs" / "design" / "state-machine-viz-v0.1.html"

def lst(v): return [] if v in (None, "") else (v if isinstance(v, list) else [v])

# 呈现层中文标签（仅可视化；单源不承载展示词汇，未映射自动回退原名）
LABELS = {
 "created":"创建契约候选","admitted":"契约已准入","activated":"运行已激活",
 "observing":"观察中","deciding":"决策中","acting":"行动中","verifying":"验证中",
 "recovering":"恢复中","terminal":"终局","closed":"已关闭",
 "candidate":"候选绑定","canonical":"正式绑定","consumed":"已消费","derived-invalid":"派生失效",
 "CommittedPending":"已登记未投递","CommitUnknown":"提交未知","Dispatched":"已投递",
 "UnknownOutcome":"结果未知","Completed":"已完成","formed":"已裁决","frozen":"已冻结",
}
def lab(n): return LABELS.get(n, n)

def edge_fields():
    """第三出口产物（protocol-workflow-steps.generated.js）→ 边号→字段表 连接。"""
    f = ROOT / "docs" / "design" / "protocol-workflow-steps.generated.js"
    try:
        import re, json as _j
        raw = f.read_text(encoding="utf-8")
        arr = _j.loads(raw[raw.index("=") + 1:].strip().rstrip(";"))
        out = {}
        for s in arr:
            m = re.match(r"(P\d+)", s.get("edge", "") or "")
            if m and s.get("fields"): out.setdefault(m.group(1), s["fields"])
        return out
    except Exception:
        return {}

def load():
    doc = yaml.safe_load(SRC.read_text(encoding="utf-8"))
    ms = []
    for key, m in doc["machines"].items():
        term = set(lst(m.get("terminal_nodes"))); ab = set(lst(m.get("absorbing")))
        ts = []
        for t in m.get("transitions") or []:
            tr = t.get("trigger") or {}
            ts.append({"id": t.get("id",""), "from": t.get("from",""), "to": t.get("to",""),
                "edge": tr.get("edge") or "", "event": tr.get("event",""),
                "guard": t.get("guard",""), "emits": lst(t.get("emits")),
                "measure": t.get("measure") or "", "await": t.get("await") or ""})
        ms.append({"key": key, "title": m.get("title", key), "initial": m.get("initial",""),
            "nodes": m.get("nodes") or [], "terminal": sorted(term), "absorbing": sorted(ab), "transitions": ts})
    return ms

TPL = """<!DOCTYPE html><html lang="zh-CN"><head><meta charset="UTF-8">
<title>UniClaw 协议状态机 · 工作流视图（点击节点看协议明细）</title><style>
:root{--ink:#1a2233;--sub:#5a6478;--line:#d7dce6;--red:#dc2626;--green:#059669;--blue:#2563eb;--gold:#b45309}
*{box-sizing:border-box;margin:0;padding:0}body{font-family:"Microsoft YaHei",sans-serif;color:var(--ink);background:#f5f7fa;line-height:1.6;padding:20px}
h1{font-size:21px}h1 small{display:block;font-size:12px;color:var(--sub);font-weight:400;margin-top:3px}
.mk{margin:16px 0 6px;font-weight:800;font-size:15px}.mk span{font-weight:400;font-size:12px;color:var(--sub)}
.lane{display:flex;flex-wrap:wrap;gap:8px;align-items:center}
.nd{border:1.5px solid var(--line);border-radius:10px;background:#fff;padding:8px 13px;cursor:pointer;font-size:13.5px;font-weight:700;transition:.15s;position:relative}
.nd:hover{box-shadow:0 3px 10px rgba(16,24,40,.15);transform:translateY(-1px)}.nd.sel{outline:2.5px solid var(--blue)}
.nd.initial{border-color:var(--blue);background:#eaf1fe}.nd.initial::after{content:"◉";color:var(--blue);margin-left:5px}
.nd.terminal{border-color:var(--green);background:#e7f6f0}.nd.terminal::after{content:"▣";color:var(--green);margin-left:5px}
.nd.absorbing{border-color:var(--red);background:#fdeaea}.nd.absorbing::after{content:"⛔";margin-left:5px}
.ar{color:#94a3b8;font-size:15px}
.ov{position:fixed;inset:0;background:rgba(15,27,51,.35);display:none;z-index:40}.ov.open{display:block}
.pn{position:fixed;top:0;right:0;height:100%;width:min(560px,94vw);background:#fff;z-index:50;box-shadow:-6px 0 24px rgba(16,24,40,.18);transform:translateX(105%);transition:.22s;display:flex;flex-direction:column}.pn.open{transform:none}
.ph{padding:14px 18px;border-bottom:1px solid var(--line);background:#fbfcfe}.ph .t{font-size:16px;font-weight:800}.ph .s{font-size:12px;color:var(--sub);margin-top:3px}
.pb{padding:12px 18px 40px;overflow-y:auto}.cl{position:absolute;top:10px;right:12px;border:none;background:none;font-size:19px;cursor:pointer;color:var(--sub)}
h4{font-size:12px;letter-spacing:1.5px;color:var(--sub);margin:14px 0 5px}
table{width:100%;border-collapse:collapse;font-size:12.5px}th{background:#eef1f6;text-align:left;padding:5px 7px}td{padding:5px 7px;border-bottom:1px solid var(--line);vertical-align:top}
code{font-family:Consolas,monospace;font-size:11.5px;background:#eef1f6;border-radius:3px;padding:1px 5px}
.m{display:inline-block;font-size:10.5px;font-weight:800;border-radius:3px;padding:0 6px;color:#fff;margin-left:4px}.me{background:var(--blue)}.mm{background:var(--gold)}.ma{background:var(--sub)}
.bd{margin-top:12px;border-left:4px solid var(--line);background:#eef1f5;border-radius:8px;padding:9px 13px;font-size:13px}
footer{margin-top:22px;color:var(--sub);font-size:12px;border-top:1px solid var(--line);padding-top:9px}
</style></head><body><h1>UniClaw 协议状态机 · 工作流视图<small>单源生成（state-machine.yaml）· 点击状态节点查看入/出转移的完整协议信息 · 生成物勿手改</small></h1><div id="flow"></div>
<div class="ov" id="ov"></div><aside class="pn" id="pn"><button class="cl" id="cl">✕</button><div class="ph"><div class="t" id="pt"></div><div class="s" id="ps"></div></div><div class="pb" id="pbd"></div></aside>
<footer>生成器 tools/gen-state-machine-viz.py（PROTOCOL-MACHINE-002）· 语义权威：协议基线与 ADR</footer>
<script>
const M = %%DATA%%; const LABELS = %%LABELS%%; const EF = %%EDGEFIELDS%%;
const flow = document.getElementById('flow');
M.forEach((m,mi)=>{ const sec=document.createElement('div');
 sec.innerHTML=`<div class="mk">${m.title} <span>· ${m.nodes.length} 状态 · ${m.transitions.length} 转移 · ◉初始 ▣终局 ⛔吸收</span></div>`;
 const lane=document.createElement('div');lane.className='lane';
 m.nodes.forEach((n,i)=>{ if(i>0){const a=document.createElement('div');a.className='ar';a.textContent='➜';lane.appendChild(a);}
  const cls=m.absorbing.includes(n)?'absorbing':(m.terminal.includes(n)?'terminal':(n===m.initial?'initial':''));
  const d=document.createElement('div');d.className='nd '+cls;d.textContent=LABELS[n]||n;d.title=n;d.onclick=()=>openP(mi,n,d);lane.appendChild(d);});
 sec.appendChild(lane);flow.appendChild(sec);});
function trow(t,dir){ const trig=(t.edge?`<span class="m me">${t.edge}</span>`:'')+` ${t.event}`;
 const ex=t.emits.map(e=>`emit:${e}`).join(' · ')+(t.measure?` · <span class="m mm">measure:${t.measure}</span>`:'')+(t.await?` · <span class="m ma">await:${t.await}</span>`:'');
 const other=dir==='out'?t.to:t.from;
 return `<tr><td><code>${t.id}</code></td><td>${dir==='out'?'→ <b>'+other+'</b>':'<b>'+other+'</b> →'}</td><td>${trig}</td><td>${t.guard}</td><td>${ex}</td></tr>`;}
function openP(mi,node,el){ document.querySelectorAll('.nd.sel').forEach(x=>x.classList.remove('sel'));el.classList.add('sel');
 const m=M[mi]; const outs=m.transitions.filter(t=>t.from===node), ins=m.transitions.filter(t=>t.to===node);
 const badges=[node===m.initial?'◉ 初始':'',m.absorbing.includes(node)?'⛔ 吸收（不可逆）':(m.terminal.includes(node)?'▣ 终局':'')].filter(Boolean).join(' · ');
 document.getElementById('pt').innerHTML=`${LABELS[node]||node} <code style="font-size:11px">${node}</code> <span style="font-size:12px;color:var(--sub)">${badges}</span>`;
 document.getElementById('ps').textContent=m.title;
 let h=`<h4>入转移（${ins.length}）</h4>`; h+=ins.length?'<table><tr><th>id</th><th>方向</th><th>触发</th><th>guard</th><th>标注</th></tr>'+ins.map(t=>trow(t,'in')).join("")+'</table>':'<div class="bd">无入转移（'+(node===m.initial?'初始态':'悬挂点——见形式审计 D4')+'）</div>';
 h+=`<h4>出转移（${outs.length}）</h4>`; h+=outs.length?'<table><tr><th>id</th><th>方向</th><th>触发</th><th>guard</th><th>标注</th></tr>'+outs.map(t=>trow(t,"out")).join("")+'</table>':'<div class="bd">无出转移'+(m.absorbing.includes(node)?'（吸收态：结构性终点）':'（悬挂点）')+'</div>';
 h+=`<div class="bd">协议边引用：${[...new Set([...ins,...outs].map(t=>t.edge).filter(Boolean))].join('、')||'—'}</div>`;
 const edges=[...new Set([...ins,...outs].map(t=>t.edge).filter(Boolean))];
 for(const e of edges){ const fs=EF[e]||[];
  h+=`<h4>字段（${e}）</h4>`+(fs.length?'<table><tr><th>字段</th><th>必填</th><th>类型</th><th>说明</th></tr>'+fs.map(x=>`<tr><td><code>${x.n}</code></td><td>${x.req||''}</td><td>${x.t||''}</td><td>${x.d||''}</td></tr>`).join("")+'</table>':'<div class="bd">字段注记未登记（见 27 步工作流模型）</div>'); }
 document.getElementById('pbd').innerHTML=h; document.getElementById('pn').classList.add('open');document.getElementById('ov').classList.add('open');}
function closeP(){document.getElementById('pn').classList.remove('open');document.getElementById('ov').classList.remove('open');document.querySelectorAll('.nd.sel').forEach(x=>x.classList.remove('sel'));}
document.getElementById('ov').onclick=closeP;document.getElementById('cl').onclick=closeP;
document.addEventListener('keydown',e=>{if(e.key==='Escape')closeP()});
</script></body></html>"""

if __name__ == "__main__":
    data = json.dumps(load(), ensure_ascii=False)
    labels = json.dumps(LABELS, ensure_ascii=False)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(TPL.replace("%%DATA%%", data).replace("%%LABELS%%", labels)
                      .replace("%%EDGEFIELDS%%", json.dumps(edge_fields(), ensure_ascii=False)), encoding="utf-8")
    print(f"written: {OUT}")
