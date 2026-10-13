# PROTOCOL-MACHINE-002 — 状态机可视化（泳道分类 + 分支边 + 节点归属 + 点击面板）
from __future__ import annotations
import json, re
from pathlib import Path
import yaml

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "schemas" / "protocol" / "state-machine.yaml"
STEPS = ROOT / "docs" / "design" / "protocol-workflow-steps.generated.js"
OUT = ROOT / "docs" / "design" / "state-machine-viz-v0.1.html"

def lst(v): return [] if v in (None, "") else (v if isinstance(v, list) else [v])
def lab(n): return LABELS.get(n, n)

LABELS = {"created":"创建契约候选","admitted":"契约已准入","activated":"运行已激活","observing":"观察中","deciding":"决策中","acting":"行动中","verifying":"验证中","recovering":"恢复中","terminal":"终局","closed":"已关闭","candidate":"候选绑定","canonical":"正式绑定","consumed":"已消费","derived-invalid":"派生失效","CommittedPending":"已登记未投递","CommitUnknown":"提交未知","Dispatched":"已投递","UnknownOutcome":"结果未知","Completed":"已完成","formed":"已裁决","frozen":"已冻结"}
PHASES = {"run":[("前奏 · 契约与激活",["created","admitted","activated"]),("执行循环 · 认识与改变世界",["observing","deciding","acting","verifying","recovering"]),("终局 · 交卷与关闭",["terminal","closed"])]}
MOWNER = {"run":"Uni Kernel 编排","binding":"Effect Boundary（P10 唯一权威）","judgment":"Assurance（裁决权威）","attempt":"Effect Boundary / 可靠执行源","contract_view":"Run Model（契约视图权威）"}
SOWNER = {"created":"UniAgent→Run Model","admitted":"Run Model","activated":"Uni Kernel·latch","observing":"Evidence Ledger→World Model","deciding":"Control Loop·咨询UniAgent","acting":"Effect Boundary","verifying":"Assurance","recovering":"Control Loop","terminal":"Run Model·冻结","closed":"Kernel·emission / EB·关闭投递"}

def edge_fields():
    try:
        raw = STEPS.read_text(encoding="utf-8")
        arr = json.loads(raw[raw.index("=") + 1:].strip().rstrip(";"))
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
        ts = []
        for t in m.get("transitions") or []:
            tr = t.get("trigger") or {}
            ts.append({"id": t.get("id",""), "from": t.get("from",""), "to": t.get("to",""),
                "edge": tr.get("edge") or "", "event": tr.get("event",""), "guard": (t.get("guard") or "").replace("<","&lt;"),
                "emits": lst(t.get("emits")), "measure": t.get("measure") or "", "await": t.get("await") or ""})
        ms.append({"key": key, "title": m.get("title", key), "initial": m.get("initial",""),
            "nodes": m.get("nodes") or [], "terminal": sorted(lst(m.get("terminal_nodes"))),
            "absorbing": sorted(lst(m.get("absorbing"))), "transitions": ts})
    return ms

TPL = """<!DOCTYPE html><html lang="zh-CN"><head><meta charset="UTF-8">
<title>UniClaw 协议状态机 · 流程/分支/归属/字段</title><style>
:root{--ink:#1a2233;--sub:#5a6478;--line:#d7dce6;--red:#dc2626;--green:#059669;--blue:#2563eb;--gold:#b45309}
*{box-sizing:border-box;margin:0;padding:0}body{font-family:"Microsoft YaHei",sans-serif;color:var(--ink);background:#f5f7fa;line-height:1.6;padding:22px}
h1{font-size:21px}h1 small{display:block;font-size:12px;color:var(--sub);font-weight:400;margin-top:3px}
.mk{margin:18px 0 4px;font-weight:800;font-size:15px}.mk span{font-weight:400;font-size:12px;color:var(--sub)}
.ph{font-size:12.5px;color:var(--sub);font-weight:700;margin:10px 0 4px}
.lane{display:flex;flex-wrap:wrap;gap:6px;align-items:center}
.nd{border:1.5px solid var(--line);border-radius:10px;background:#fff;padding:6px 12px;font-size:13px;font-weight:700;text-align:center;min-width:118px;cursor:pointer;transition:.15s}
.nd:hover{box-shadow:0 3px 10px rgba(16,24,40,.15);transform:translateY(-1px)}.nd.sel{outline:2.5px solid var(--blue)}
.nd.initial{border-color:var(--blue);background:#eaf1fe}.nd.initial::before{content:"◉ ";color:var(--blue)}
.nd.terminal{border-color:var(--green);background:#e7f6f0}.nd.terminal::before{content:"▣ ";color:var(--green)}
.nd.absorbing{border-color:var(--red);background:#fdeaea}.nd.absorbing::before{content:"⛔ "}
.nd .ow{font-size:10.5px;color:var(--sub);font-weight:400;margin-top:1px}
.ar{color:#94a3b8;font-size:15px}
.ed{border-top:1px dashed var(--line);margin-top:8px;padding-top:6px;font-size:12.5px}
.e{display:inline-block;margin:2px 10px 2px 0;padding:2px 8px;border:1px solid var(--line);border-radius:6px;background:#fff;cursor:pointer}
.e:hover{border-color:var(--blue)} .e b{color:var(--blue)}.e .g{color:var(--sub);font-size:11px}
.e.loop{border-color:var(--gold);background:#fdf6e3}.e.abs{border-color:var(--red);background:#fdeaea}
.ov{position:fixed;inset:0;background:rgba(15,27,51,.35);display:none;z-index:40}.ov.open{display:block}
.pn{position:fixed;top:0;right:0;height:100%;width:min(580px,94vw);background:#fff;z-index:50;box-shadow:-6px 0 24px rgba(16,24,40,.18);transform:translateX(105%);transition:.22s;display:flex;flex-direction:column}.pn.open{transform:none}
.phh{padding:14px 18px;border-bottom:1px solid var(--line);background:#fbfcfe}.phh .t{font-size:16px;font-weight:800}.phh .s{font-size:12px;color:var(--sub);margin-top:3px}
.pb{padding:12px 18px 40px;overflow-y:auto}.cl{position:absolute;top:10px;right:12px;border:none;background:#fff;border-radius:6px;font-size:17px;cursor:pointer;color:var(--sub);padding:2px 8px}
h4{font-size:12px;letter-spacing:1.5px;color:var(--sub);margin:14px 0 5px}
table{width:100%;border-collapse:collapse;font-size:12.5px}th{background:#eef1f6;text-align:left;padding:5px 7px}td{padding:5px 7px;border-bottom:1px solid var(--line);vertical-align:top}
code{font-family:Consolas,monospace;font-size:11.5px;background:#eef1f6;border-radius:3px;padding:1px 5px}
.m{display:inline-block;font-size:10.5px;font-weight:800;border-radius:3px;padding:0 6px;color:#fff;margin-left:4px}.me{background:var(--blue)}.mm{background:var(--gold)}.ma{background:var(--sub)}
.bd{margin-top:12px;border-left:4px solid var(--line);background:#eef1f5;border-radius:8px;padding:9px 13px;font-size:13px}
footer{margin-top:22px;color:var(--sub);font-size:12px;border-top:1px solid var(--line);padding-top:9px}</style></head><body>
<h1>UniClaw 协议状态机 · 流程 / 分支 / 归属 / 字段<small>单源生成（state-machine.yaml）· 泳道=流程阶段 · 节点下标归属 · 金边=循环(带终止测度) 红边=进吸收态 · 点击节点或边看明细 · Esc 关闭</small></h1><div id="flow"></div>
<div class="ov" id="ov"></div><aside class="pn" id="pn"><button class="cl" id="cl">✕ 关闭</button><div class="phh"><div class="t" id="pt"></div><div class="s" id="ps"></div></div><div class="pb" id="pbd"></div></aside>
<footer>生成器 tools/gen-state-machine-viz.py（PROTOCOL-MACHINE-002）· 语义权威：协议基线与 ADR</footer>
<script>
const M = %%DATA%%, LABELS = %%LABELS%%, PHASES = %%PHASES%%, MOWNER = %%MOWNER%%, SOWNER = %%SOWNER%%, EF = %%EDGEFIELDS%%;
function closeP(){document.getElementById('pn').classList.remove('open');document.getElementById('ov').classList.remove('open');document.querySelectorAll('.nd.sel,.e.sel').forEach(x=>x.classList.remove('sel'));}
function openP(title, sub, htmlBody){ document.getElementById('pt').innerHTML=title; document.getElementById('ps').innerHTML=sub;
 document.getElementById('pbd').innerHTML=htmlBody; document.getElementById('pn').classList.add('open'); document.getElementById('ov').classList.add('open'); }
// 绑定关闭（先于渲染，任何渲染异常都不影响关闭）
document.getElementById('ov').addEventListener('click', closeP);
document.getElementById('cl').addEventListener('click', closeP);
document.addEventListener('keydown', function(e){ if(e.key==='Escape') closeP(); });
function esc(s){ return String(s==null?'':s).replace(/&/g,'&amp;').replace(/</g,'&lt;'); }
function trow(t, dir){ var trig=(t.edge?'<span class="m me">'+t.edge+'</span>':'')+' '+t.event;
 var ex=(t.emits||[]).map(function(e){return 'emit:'+e}).join(' · ');
 if(t.measure) ex+=' <span class="m mm">measure:'+t.measure+'</span>'; if(t.await) ex+=' <span class="m ma">await:'+t.await+'</span>';
 var other = dir==='out' ? (LABELS[t.to]||t.to)+' <code>'+t.to+'</code>' : (LABELS[t.from]||t.from)+' <code>'+t.from+'</code>';
 return '<tr><td><code>'+t.id+'</code></td><td>'+(dir==='out'?'→ ':'')+other+(dir==='out'?'':' →')+'</td><td>'+trig+'</td><td>'+t.guard+'</td><td>'+ex+'</td></tr>'; }
function fieldsHTML(edges){ var h=''; edges.forEach(function(e){ var fs=EF[e]||[];
  h+='<h4>字段（'+e+'）</h4>'+(fs.length?'<table><tr><th>字段</th><th>必填</th><th>类型</th><th>说明</th></tr>'+fs.map(function(x){return '<tr><td><code>'+x.n+'</code></td><td>'+(x.req||'')+'</td><td>'+(x.t||'')+'</td><td>'+(x.d||'')+'</td></tr>';}).join('')+'</table>':'<div class="bd">字段注记未登记（见 27 步工作流模型）</div>'); }); return h; }
function statePanel(mi, node, el){ try{
 document.querySelectorAll('.sel').forEach(function(x){x.classList.remove('sel')}); el.classList.add('sel');
 var m=M[mi]; var outs=m.transitions.filter(function(t){return t.from===node}), ins=m.transitions.filter(function(t){return t.to===node});
 var badges=[node===m.initial?'◉ 初始':'', m.absorbing.indexOf(node)>=0?'⛔ 吸收（不可逆）':(m.terminal.indexOf(node)>=0?'▣ 终局':'')].filter(Boolean).join(' · ');
 var body='<div class="bd">状态 <code>'+node+'</code> · 归属：<b>'+(SOWNER[node]||MOWNER[m.key]||'')+'</b>'+(badges?' · '+badges:'')+'</div>';
 body+='<h4>入转移（'+ins.length+'）</h4>'+(ins.length?'<table><tr><th>id</th><th>方向</th><th>触发</th><th>guard</th><th>标注</th></tr>'+ins.map(function(t){return trow(t,'in')}).join('')+'</table>':'<div class="bd">无入转移'+(node===m.initial?'（初始态）':'（悬挂点——见形式审计 D4）')+'</div>');
 body+='<h4>出转移（'+outs.length+'）</h4>'+(outs.length?'<table><tr><th>id</th><th>方向</th><th>触发</th><th>guard</th><th>标注</th></tr>'+outs.map(function(t){return trow(t,'out')}).join('')+'</table>':'<div class="bd">无出转移'+(m.absorbing.indexOf(node)>=0?'（吸收态：结构性终点）':'（悬挂点）')+'</div>');
 var edges=[]; ins.concat(outs).forEach(function(t){ if(t.edge && edges.indexOf(t.edge)<0) edges.push(t.edge); });
 body+=fieldsHTML(edges);
 openP((LABELS[node]||node)+' <code style="font-size:11px">'+node+'</code>', m.title, body);
 }catch(err){ openP('明细构建出错','请截图反馈：'+esc(err.message),''); } }
function edgePanel(mi, t, el){ try{
 document.querySelectorAll('.sel').forEach(function(x){x.classList.remove('sel')}); el.classList.add('sel');
 var m=M[mi]; var body='<div class="bd"><code>'+t.id+'</code> · '+(LABELS[t.from]||t.from)+' <b>→</b> '+(LABELS[t.to]||t.to)+' · 归属：<b>'+(MOWNER[m.key]||'')+'</b></div>';
 body+='<table><tr><th>触发</th><th>guard</th></tr><tr><td>'+(t.edge?'<span class="m me">'+t.edge+'</span> ':'')+t.event+'</td><td>'+t.guard+'</td></tr></table>';
 if((t.emits||[]).length) body+='<h4>emits（观察投影）</h4><div class="bd">'+t.emits.join('、')+'</div>';
 if(t.measure) body+='<div class="bd">🟡 进展环 · 终止测度：<b>'+t.measure+'</b>（每循环消耗有限额度——终止性结构保证）</div>';
 if(t.await) body+='<div class="bd">⏸ 合法等待外部：<b>'+t.await+'</b>（不携带测度，非进展）</div>';
 body+=fieldsHTML(t.edge?[t.edge]:[]);
 openP('转移 <code>'+t.id+'</code>', m.title, body);
 }catch(err){ openP('明细构建出错','请截图反馈：'+esc(err.message),''); } }
// 渲染（后于关闭绑定；单机异常不阻断其余）
M.forEach(function(m, mi){ try{
 var sec=document.createElement('div');
 sec.innerHTML='<div class="mk">'+m.title+' <span>· 归属：'+(MOWNER[m.key]||'')+' · ◉初始 ▣终局 ⛔吸收 · '+m.transitions.length+' 条转移</span></div>';
 (PHASES[m.key]||[['', m.nodes]]).forEach(function(g){ var gname=g[0], gs=g[1];
  if(gname){ var gh=document.createElement('div'); gh.className='ph'; gh.textContent='▎'+gname; sec.appendChild(gh); }
  var lane=document.createElement('div'); lane.className='lane';
  gs.forEach(function(n, i){ if(i>0){ var a=document.createElement('span'); a.className='ar'; a.textContent='➜'; lane.appendChild(a); }
   var cls=m.absorbing.indexOf(n)>=0?'absorbing':(m.terminal.indexOf(n)>=0?'terminal':(n===m.initial?'initial':''));
   var d=document.createElement('div'); d.className='nd '+cls;
   d.innerHTML=(LABELS[n]||n)+'<div class="ow">'+(SOWNER[n]||MOWNER[m.key]||'')+'</div>'; d.title=n;
   d.addEventListener('click', function(){ statePanel(mi, n, d); }); lane.appendChild(d); });
  sec.appendChild(lane); });
 var ed=document.createElement('div'); ed.className='ed';
 m.transitions.forEach(function(t){ var sp=document.createElement('span');
  var cls=t.measure?'loop':(m.absorbing.indexOf(t.to)>=0?'abs':'');
  sp.className='e '+cls; sp.title=t.guard;
  sp.innerHTML='<b>'+t.id+'</b> '+(LABELS[t.from]||t.from)+' —['+(t.edge||'')+(t.event?' '+t.event:'')+']→ '+(LABELS[t.to]||t.to)+(t.measure?' <span class="g">· '+t.measure+'</span>':'');
  sp.addEventListener('click', function(){ edgePanel(mi, t, sp); }); ed.appendChild(sp); });
 sec.appendChild(ed); document.getElementById('flow').appendChild(sec);
}catch(err){ var d=document.createElement('div'); d.className='bd'; d.textContent='机器渲染出错：'+err.message; document.getElementById('flow').appendChild(d);} });
</script></body></html>"""

if __name__ == "__main__":
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(TPL
        .replace("%%DATA%%", json.dumps(load(), ensure_ascii=False))
        .replace("%%LABELS%%", json.dumps(LABELS, ensure_ascii=False))
        .replace("%%PHASES%%", json.dumps({k: [list(g) for g in v] for k, v in PHASES.items()}, ensure_ascii=False))
        .replace("%%MOWNER%%", json.dumps(MOWNER, ensure_ascii=False))
        .replace("%%SOWNER%%", json.dumps(SOWNER, ensure_ascii=False))
        .replace("%%EDGEFIELDS%%", json.dumps(edge_fields(), ensure_ascii=False)), encoding="utf-8")
    print(f"written: {OUT}")
