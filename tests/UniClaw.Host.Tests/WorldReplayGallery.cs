using System.Text.Json;

namespace UniClaw.Host.Tests;

internal static class WorldReplayGallery
{
    public static void WriteScroll(string output, IReadOnlyList<ReplayPage> pages, IReadOnlyList<double> offsets)
    {
        var data = JsonSerializer.Serialize(pages.Select((page, index) => new { page, offset = offsets[index] }));
        File.WriteAllText(Path.Combine(output, "worked-scroll.html"), """
<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>相对坐标滚动演算</title>
<style>body{background:#f0f3f8;color:#263247;font:14px system-ui;margin:30px}p{line-height:1.7}.grid{display:grid;grid-template-columns:repeat(4,minmax(180px,1fr));gap:20px;max-width:1100px}article{background:white;padding:16px;border-radius:14px}svg{width:100%;border:1px solid #dce1e9}h2{font-size:15px}@media(max-width:800px){.grid{grid-template-columns:1fr 1fr}}</style>
<h1>从视口坐标还原内容坐标</h1><p>三个独立 World 修订：向前滚动 200px，再反向滚动 100px。共同锚 Alpha / Beta 位移一致。Header 是明确声明的固定元素，排除在配准之外。<br>内容 Y = 视口 Y + 累计偏移；Gamma 在后两帧还原为 800px；首帧尚未观察到它。此图是带历史来源的诊断拼图，不是当前 World 或可执行定位。</p><div class="grid" id="panels"></div><p><a href="world-replay.html">返回录制页面对照</a> · <a href="worked-scroll.json">配准依据和坐标数据</a></p>
<script>
const data=__DATA__, colors=['#77b5ce','#ecaf74','#a794d4'];
const esc=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
function box(o,offset,color){const b=o.Locator;return `<rect x="${b.X1*1080}" y="${(b.Y1+offset)*1000}" width="${(b.X2-b.X1)*1080}" height="${(b.Y2-b.Y1)*1000}" fill="${color}" opacity=".45" stroke="#265475"/><text x="${b.X1*1080+8}" y="${(b.Y1+offset)*1000+40}" font-size="32">${esc(o.SemanticDescriptor)}</text>`}
let html='';data.forEach(({page,offset},i)=>{html+=`<article><h2>视口 ${i+1} · 偏移 ${(offset*1000).toFixed(0)}px</h2><small>${esc(page.Slice.SourceRevisionId)}</small><svg viewBox="0 0 1080 1100">${page.Slice.Occurrences.map(o=>box(o,0,colors[i])).join('')}</svg></article>`});
const merged=new Map();data.forEach(({page,offset},i)=>page.Slice.Occurrences.filter(o=>o.SemanticDescriptor!=='Header').forEach(o=>merged.set(o.SemanticDescriptor,{o,offset,i,revision:page.Slice.SourceRevisionId})));
html+=`<article><h2>内容坐标 · 历史诊断</h2><small>保留最新观察；未知区域留白</small><svg viewBox="0 0 1080 1100">${[...merged.values()].map(v=>box(v.o,v.offset,colors[v.i])).join('')}</svg>${[...merged.values()].map(v=>`<p>${esc(v.o.SemanticDescriptor)} ← ${esc(v.revision)}</p>`).join('')}</article>`;document.querySelector('#panels').innerHTML=html;
</script></html>
""".Replace("__DATA__", data, StringComparison.Ordinal));
    }

    public static void Write(string output, IReadOnlyList<ReplayPage> pages)
    {
        var json = JsonSerializer.Serialize(pages.Select(page => new
        {
            page.CaptureId, page.Width, page.Height, page.Slice,
            image = Path.GetRelativePath(output, Path.Combine(WorldReplayDiagnostics.Corpus, "frames", page.CaptureId + ".png")).Replace('\\', '/'),
        }));
        File.WriteAllText(Path.Combine(output, "world-replay.html"), Html.Replace("__PAGES__", json, StringComparison.Ordinal));
    }

    private const string Html = """
<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>World 回放诊断 · 39 帧</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#f0f3f8;color:#263247;font:14px system-ui,sans-serif}main{max-width:1120px;margin:auto;padding:28px}
h1{font-size:25px;margin:0 0 12px}p{line-height:1.65;color:#51617a}nav{display:flex;gap:12px;align-items:center;flex-wrap:wrap;margin:22px 0}select,button{font:inherit;padding:10px;border:1px solid #cbd4e2;border-radius:8px;background:white}
.grid{display:grid;grid-template-columns:1fr 1fr;gap:24px}.panel{background:white;padding:18px;border-radius:16px}h2{font-size:16px;margin:0 0 14px}.phone{position:relative;max-width:310px;margin:auto;background:#f9fafc;overflow:hidden;outline:1px solid #dce1e9}.phone img{width:100%;display:block}.phone svg{display:block;width:100%}#meta{font-family:monospace;font-size:12px;overflow-wrap:anywhere}details{margin:22px 0;background:white;border-radius:12px;padding:16px}li{margin:7px 0}#overlay{position:absolute;inset:0;display:none}body.overlay #overlay{display:block}table{width:100%;border-collapse:collapse}td,th{text-align:left;padding:9px;border-bottom:1px solid #e0e6ef}small{color:#738098}@media(max-width:650px){main{padding:15px}.grid{gap:10px}.panel{padding:10px}h2{font-size:13px}}
</style>
<main><h1>World 回放：模型保留了什么？</h1>
<p>39 帧已录制 Android 页面，经产品 World 链路重放。右侧只绘制实际 Slice 中的坐标、标签与状态；未用原图或 XML 补画。矩形和通用开关符号是诊断样式，字体、颜色、图标与图片素材未被 World 建模。</p>
<nav><label for="capture">录制页面</label><select id="capture"></select><button id="toggle">显示原图坐标叠加</button><span id="count"></span></nav>
<p id="meta"></p><div class="grid"><section class="panel"><h2>录制原图</h2><div class="phone"><img id="original" alt="录制原页面"><div id="overlay"></div></div></section><section class="panel"><h2>World 坐标重建 · 单修订</h2><div class="phone" id="world"></div></section></div>
<details open><summary>当前输出的局限</summary><ul><li>产品观察器侧重可交互控件，很多标题、副标题、说明文字和装饰元素不在 Slice 中。</li><li>未知状态标记“?”；录制 XML 的 checked=false 默认能力不足以证明精确 unchecked。</li><li>滚动前后 Slice 属于不同修订，当前 Slice 不包含完整历史长页面。应用列表存在重复标签，开发者选项共同锚不足，均不能可靠证明相对位移。</li></ul></details>
<details><summary>实际元素与坐标</summary><table><thead><tr><th>角色</th><th>标签</th><th>状态</th><th>归一化坐标</th></tr></thead><tbody id="rows"></tbody></table></details>
<p><a href="worked-scroll.html">查看有充分重叠的滚动演算图</a> · <a href="recorded-pages.json">39 帧数据与缺失文字</a> · <a href="seq-scroll-apps.json">应用列表滚动</a> · <a href="seq-scroll-devopts.json">开发者选项滚动</a></p>
<small>非权威诊断 · 仿真/历史录制证据 · 未执行设备动作</small></main>
<script>
const pages=__PAGES__, select=document.querySelector('#capture');
const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
for(const p of pages){const o=document.createElement('option');o.value=p.CaptureId;o.textContent=p.CaptureId;select.append(o)}
function svg(p,overlay){return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${p.Width} ${p.Height}">`+p.Slice.Occurrences.map(o=>{const b=o.Locator;if(!b)return '';const x=b.X1*p.Width,y=b.Y1*p.Height,w=(b.X2-b.X1)*p.Width,h=(b.Y2-b.Y1)*p.Height,scroll=o.Role==='scrollable';return `<g><rect x="${x}" y="${y}" width="${w}" height="${h}" rx="8" fill="${scroll?'none':overlay?'#37b8df22':'#fff'}" stroke="${scroll?'#c9d0da':'#3b92ae'}" stroke-width="3" ${scroll?'stroke-dasharray="12 10"':''}/>`+(scroll||overlay?'':`<foreignObject x="${x+12}" y="${y+6}" width="${Math.max(1,w-24)}" height="${Math.max(1,h-12)}"><div xmlns="http://www.w3.org/1999/xhtml" style="font:45px system-ui;color:#31415c;line-height:1.2;overflow:hidden">${esc(o.SemanticDescriptor||o.Role)}${o.Role==='switch'?'<b style="color:#1884a1"> ['+esc(o.State??'?')+']</b>':''}</div></foreignObject>`)+`</g>`}).join('')+'</svg>'}
function render(){const p=pages.find(x=>x.CaptureId===select.value);document.querySelector('#original').src=p.image;document.querySelector('#world').innerHTML=svg(p,false);document.querySelector('#overlay').innerHTML=svg(p,true);document.querySelector('#meta').textContent=`capture ${p.CaptureId} · ${p.Slice.SourceRevisionId} · ${p.Width}×${p.Height} · ${p.Slice.RootContainerId}`;document.querySelector('#count').textContent=p.Slice.Occurrences.length+' 个 World 元素';document.querySelector('#rows').innerHTML=p.Slice.Occurrences.map(o=>'<tr><td>'+esc(o.Role)+'</td><td>'+esc(o.SemanticDescriptor)+'</td><td>'+esc(o.State??'?')+'</td><td>'+[o.Locator.X1,o.Locator.Y1,o.Locator.X2,o.Locator.Y2].map(x=>x.toFixed(4)).join(', ')+'</td></tr>').join('');}
select.value=pages.some(p=>p.CaptureId==='7e41f85e06e4')?'7e41f85e06e4':pages[0].CaptureId;select.onchange=render;document.querySelector('#toggle').onclick=()=>document.body.classList.toggle('overlay');render();
</script></html>
""";
}
