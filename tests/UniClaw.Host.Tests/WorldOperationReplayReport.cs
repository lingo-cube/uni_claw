namespace UniClaw.Host.Tests;

internal static class WorldOperationReplayReport
{
    public static void Write(string output, string json) => File.WriteAllText(Path.Combine(output, "world-operation-replay.html"), """
<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>World 操作与滚动重放</title>
<style>*{box-sizing:border-box}body{margin:0;background:#eef2f7;color:#26374c;font:15px system-ui}main{max-width:1150px;margin:auto;padding:28px}h1{font-size:27px}p{line-height:1.7}.graph{padding:22px;background:#fff;border-radius:14px;margin:20px 0;font-size:18px}.cards{display:grid;grid-template-columns:1fr 1fr;gap:18px}.card{background:white;border-radius:14px;padding:20px}.views{display:grid;grid-template-columns:1fr 1fr;gap:14px}svg{width:100%;background:#fafbfe;border:1px solid #ccd6e5}h2{font-size:18px}code,small{overflow-wrap:anywhere}table{border-collapse:collapse;width:100%;background:white}td,th{padding:12px;text-align:left;border-bottom:1px solid #dde4ed}small{color:#63748a}a{color:#286da0}@media(max-width:720px){main{padding:15px}.cards{grid-template-columns:1fr}}</style>
<main><h1>World 操作与滚动重放</h1><p>通过真实 Kernel 操作、绑定、Assurance、投递与事后验证链路生成图。外部驱动和页面输入为确定性仿真；本页只展示产品 World 保存的关系与位移，没有另跑配准算法。</p>
<div class="graph" id="graph"></div><div class="cards" id="cards"></div><h2>根据产品偏移还原的内容位置</h2><p>只列有对应锚支持的元素。固定元素保持视口位置；未知关系不累计偏移。坐标是历史诊断，不可用于执行。</p><table><thead><tr><th>元素</th><th>观察版本</th><th>内容 X</th><th>内容 Y</th></tr></thead><tbody id="positions"></tbody></table><p><a href="operation-replay.json">完整图、回执与观察数据</a> · <a href="seq-scroll-apps.json">应用列表录制回放</a> · <a href="seq-scroll-devopts.json">开发者选项录制回放</a></p><small>本回放用于关系与坐标验证；末尾 NoAction 不替代任务完成证明。</small></main>
<script>
const report=__REPORT__,esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
document.querySelector('#graph').textContent=`${report.Graph.Nodes.length} 个页面 · ${report.Graph.Edges.length} 条跨页导航边 · ${report.Graph.ViewportTransitions.length} 条滚动观察关系 · ${report.Receipts.length} 次真实管线投递`;
document.querySelector('#graph').insertAdjacentHTML('beforeend','<h2>产品保存的导航边</h2>'+report.Graph.Edges.map(e=>`<p><code>${esc(e.SourceContainerId)} → ${esc(e.TargetContainerId)}</code><br><small>观察 ${esc(e.SourceRevisionId)} → ${esc(e.TargetRevisionId)}；${esc(e.VerificationSourceRef)}</small></p>`).join(''));
function view(revision){const r=report.Revisions.find(r=>r.RevisionId===revision);return `<svg viewBox="0 0 1000 1000">`+(r.Occurrences??[]).map(o=>{const b=o.Locator;if(!b)return '';return `<rect x="${b.X1*1000}" y="${b.Y1*1000}" width="${(b.X2-b.X1)*1000}" height="${(b.Y2-b.Y1)*1000}" fill="${o.Role==='scrollable'?'none':'#dcecf3'}" stroke="#759bb4"/><text x="${b.X1*1000+6}" y="${b.Y1*1000+28}" font-size="23">${esc(o.SemanticDescriptor)}</text>`}).join('')+'</svg>'}
document.querySelector('#cards').innerHTML=report.Graph.ViewportTransitions.map((e,i)=>`<article class="card"><h2>${i+1}. ${esc(e.EffectClass)} · ${esc(e.AlignmentStatus)}</h2><p>偏移：${e.OffsetY===null?'未知':Math.round(e.OffsetY*e.Space.PixelHeight)+'px'}；静止锚 ${e.Correspondences.filter(p=>p.Stationary).length} 个，移动锚 ${e.Correspondences.filter(p=>!p.Stationary).length} 个</p><small>${esc(e.SourceRevisionId)} → ${esc(e.TargetRevisionId)}<br>操作 ${esc(e.OperationReceiptId)}</small><div class="views">${view(e.SourceRevisionId)}${view(e.TargetRevisionId)}</div></article>`).join('');
document.querySelector('#positions').innerHTML=report.Content.map(p=>`<tr><td>${esc(p.Label)}</td><td>${esc(p.RevisionId)}</td><td>${p.X.toFixed(3)}</td><td>${p.Y.toFixed(3)}</td></tr>`).join('');
</script></html>
""".Replace("__REPORT__", json, StringComparison.Ordinal));
}
