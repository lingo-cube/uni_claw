import {validateDataset, array, short, containerName, changesBetweenFrames, gapNames} from './model.mjs';

const $ = id => document.getElementById(id);
document.querySelectorAll('button,select,input[type=range],input[type=checkbox],#search').forEach(el=>el.disabled=true);
const e = value => String(value ?? '').replace(/[&<>"']/g, c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const json = value => e(JSON.stringify(value ?? {status:'not_collected'}, null, 2));
const raw = (label,value,open=false) => `<details ${open?'open':''}><summary>${e(label)}</summary><pre>${json(value)}</pre></details>`;
const facts = entries => `<dl class="facts">${entries.map(([k,v])=>`<dt>${e(k)}</dt><dd>${e(v ?? '未采集')}</dd>`).join('')}</dl>`;
const relationNames = ['包含','覆盖','导航'];
const unknownNames = {'visit-occurrence-unproven':'访问记录未证','whole-page-coverage-unproven':'整页覆盖未证','identity-unproven':'身份未证','current-semantics-unproven':'当前语义未证','registration-unproven':'空间配准未证'};
const labels = {
  'c1-verified':'旧观察：稳定语义已经验证', 'c2-before-anchor':'新观察：历史可读，当前对应仍待证', 'c2-reused':'当前对应有据：沿用稳定语义',
  'child-unidentified':'局部观察：四类知识缺口', 'child-identified':'页面身份已确认，归属开始建立',
  'contains-confirmed':'当前证据支持上下文包含关系', 'new-capture-no-inherited-relation':'新观察到达：旧关系不自动继承',
  'review-1':'第一次核查：一次执行，两个结果版本', 'review-2':'再次核查：新结果到达，执行次数不变',
  'terminal-recorded':'原终局已经记录', 'terminal-correction-review':'终局后纠正：追加未决影响核查',
  'previous-capture':'旧观察：Wi-Fi 与已验证语义', 'current-capture-unmatched':'新观察：Bluetooth，当前对应仍待证',
  'late-c1-interpretation':'旧观察的解释晚到：当前仍是 C2', 'rejected-signature-correction':'纠正依据不符：保留原归属'
};
let data, manifest=[], frameIndex=0, selected=null, selectedSource=null, containerId=null, scopeIndex=-1, mode='current', tab='inspect', compareIndex=0;
let timer=null, loadToken=0, zoom=1, offset={x:0,y:0}, nodePositions=new Map();
const frame = () => data.frames[frameIndex];
const current = () => frame().revision;
function error(message) { $('error').hidden=false; $('error').textContent=message; }
function clearError() { $('error').hidden=true; $('error').textContent=''; }
function stop() { clearInterval(timer); timer=null; $('play').textContent='▶ 播放'; }
function options(values, chosen) { return values.map(([value,label])=>`<option value="${e(value)}" ${String(value)===String(chosen)?'selected':''}>${e(label)}</option>`).join(''); }
function chooseFrame(index) {
  frameIndex=Math.max(0,Math.min(data.frames.length-1,Number(index)));
  const ids=array(current().containers).map(c=>c.identity.containerId);
  containerId=current().currentPageObservation?.containerId || ids[0] || null;
  selected=null;scopeIndex=-1;offset={x:0,y:0};zoom=1;
  render();
}
function acceptDataset(value, title) {
  const validated = validateDataset(value);
  data=validated;document.querySelectorAll('button,select,input').forEach(el=>el.disabled=false);stop();clearError();frameIndex=0;compareIndex=0;containerId=null;selected=null;mode='current';tab='inspect';
  if (title) data.title=title;
  $('input-class').textContent=data.provenance.inputClass || '来源类别未声明';
  $('boundary-text').textContent='核心 '+(data.provenance.coreCommit || '未声明')+' · '+(data.provenance.liveDeviceOrModelProven?'实况资格见来源记录':'受控或录制资料，未证明当前真机 / 模型');
  $('dataset-meta').textContent=`${data.frames.length} 个导出时点 · 不要求相邻修订连续`;
  $('frame').innerHTML=options(data.frames.map((f,i)=>[i,`${f.revision.revisionId} · ${i+1}`]),0);
  $('time').max=data.frames.length-1;
  chooseFrame(0);
}
async function loadExample(path) {
  stop(); const token=++loadToken;
  try {
    const entry=manifest.find(x=>x.file===path); if (!entry) throw Error('没有找到所选资料。');
    const response=await fetch('examples/'+entry.file); if (!response.ok) throw Error(`资料读取失败（${response.status}）。`);
    const bytes=await response.arrayBuffer();
    if (entry.sha256) {
      const hash=[...new Uint8Array(await crypto.subtle.digest('SHA-256',bytes))].map(x=>x.toString(16).padStart(2,'0')).join('');
      if (hash !== entry.sha256) throw Error('资料校验失败，文件与索引摘要不一致。请重新导出。');
    }
    const value=JSON.parse(new TextDecoder().decode(bytes));
    if (token===loadToken) acceptDataset(value,entry.title);
  } catch(err) { if(token===loadToken) error(err.message+' 已加载的视图仍保留，来源标签对应原资料。'); }
}
function render() {
  const f=frame(), r=current(), cap=r.currentPageObservation?.capture;
  $('event-title').textContent=labels[f.key] || f.label || r.revisionId;
  $('time').value=frameIndex; $('frame').value=frameIndex;
  $('position').textContent=`${frameIndex+1} / ${data.frames.length}`;
  $('prev').disabled=frameIndex===0; $('next').disabled=frameIndex===data.frames.length-1;
  $('version-meta').innerHTML=[['World',r.revisionId],['父版本',r.parentRevisionId],['观察',cap?.captureId],['观察时间',cap?.captureTime],['会话',cap?.sessionCorrelation]].map(([k,v])=>`<span>${e(k)} <b>${e(v??'未采集')}</b></span>`).join('');
  $('events').innerHTML=data.frames.map((f,i)=>`<button data-frame="${i}" aria-current="${i===frameIndex}">${e(f.revision.revisionId)}${f.knowledge?` · ${array(f.knowledge.gaps).length} 缺口`:''}</button>`).join('');
  const ids=array(r.containers).map(c=>c.identity.containerId);
  $('container').innerHTML=options(ids.map(id=>[id,containerName(f,id)]),containerId) || '<option>无已确认页面</option>';
  const slices=f.slices.map((s,i)=>[i,`${s.slice.region?'区域 y='+s.slice.region.bounds.y1+'…'+s.slice.region.bounds.y2:'容器范围'} · ${short(s.sliceId)} · ${array(s.slice.inScopeContainerIds).length} 容器`]).filter(([i])=>f.slices[i].slice.rootContainerId===containerId);
  $('scope').innerHTML=options([[-1,'Owner 页面视图 / 未选择 Slice'],...slices],scopeIndex);
  const diff=changesBetweenFrames(data.frames[compareIndex],f);$('diff-count').textContent=diff.length;
  document.querySelectorAll('[role=tab]').forEach(el=>el.setAttribute('aria-selected',el.dataset.tab===tab));
  renderGraph(); renderPage(); renderDetail();
}
function select(value, sourceRevisionId=current().revisionId) {
  selected=value;
  selectedSource=sourceRevisionId;
  const owner=value?.identity?.containerId || value?.owningContainerId || value?.containerId;
  if (owner && owner!==containerId && array(current().containers).some(c=>c.identity.containerId===owner)) {containerId=owner;scopeIndex=-1;}
  if (tab !== 'inspect') tab='inspect';
  render();
}
function nodeId(value) { return value?.identity?.containerId || value?.occurrenceId || value?.presentationRef || value?.gapId; }
function renderGraph() {
  const r=current(), f=frame(), search=$('search').value.trim().toLowerCase(), show=$('show-elements').checked;
  let cs=array(f.graph?.nodes || r.containers), os=show?array(r.occurrences):[];
  const matches=(v,text)=>!search || (text+' '+JSON.stringify(v)).toLowerCase().includes(search);
  cs=cs.filter(c=>matches(c,containerName(f,c.identity.containerId)));
  os=os.filter(o=>matches(o,o.semanticDescriptor));
  const all=[...cs,...os], visibleOs=os.slice(0,18);
  const width=680;
  $('graph').setAttribute('viewBox',`0 0 680 ${Math.max(420,320+Math.floor((visibleOs.length-1)/5)*66)}`);
  nodePositions=new Map();
  cs.forEach((c,i)=>nodePositions.set(c.identity.containerId,{x:width*(i+1)/(cs.length+1),y:cs.length>3?100+(i%2)*90:115,value:c}));
  visibleOs.forEach((o,i)=>nodePositions.set(o.occurrenceId,{x:70+(i%5)*132,y:260+Math.floor(i/5)*66,value:o}));
  const filter=$('relation-filter').value;
  const edges=array(f.graph?.edges || r.relations).filter(x=>filter==='all'||String(x.kind)===filter);
  let svg='<defs><marker id="arrow" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto"><path d="M0,0 L8,4 L0,8" fill="#6b9580"/></marker></defs><g id="graph-transform">';
  function line(a,b,kind,index,owner=false) {
    if(!a||!b)return '';
    const self=a===b, y=self?a.y-85:(a.y+b.y)/2-35;
    const path=self?`M${a.x-35},${a.y-20} C${a.x-90},${y-45} ${a.x+90},${y-45} ${a.x+35},${a.y-20}`:`M${a.x},${a.y+17} Q${(a.x+b.x)/2+25},${y} ${b.x},${b.y-18}`;
    return `<g ${owner?'':`data-edge="${index}" tabindex="0" role="button" aria-label="${e(kind)}关系"`}><path d="${path}" fill="none" stroke="${owner?'#c5d4c7':'#6b9580'}" stroke-width="${owner?1:1.7}" ${owner?'stroke-dasharray="3 4"':'marker-end="url(#arrow)"'}/>${owner?'':`<text x="${(a.x+b.x)/2+25}" y="${y-7}" font-size="11" text-anchor="middle" fill="#497059">${e(kind)}</text>`}</g>`;
  }
  edges.forEach((x,i)=>svg+=line(nodePositions.get(x.sourceContainerId),nodePositions.get(x.targetContainerId),relationNames[x.kind]||String(x.kind),i));
  visibleOs.forEach(o=>svg+=line(nodePositions.get(o.owningContainerId),nodePositions.get(o.occurrenceId),'',0,true));
  for(const [id,p] of nodePositions) {
    const isContainer=!!p.value.identity, active=nodeId(selected)===id, label=isContainer?containerName(f,id):p.value.semanticDescriptor || p.value.role;
    svg+=`<g data-node="${e(id)}" tabindex="0" role="button" aria-label="${e(label)}" transform="translate(${p.x-58},${p.y-23})"><rect width="116" height="46" rx="${isContainer?10:3}" fill="${active?'#d4ebd7':isContainer?'#f0f6eb':'white'}" stroke="${active?'#137668':isContainer?'#83ac91':'#c7d7c8'}"/><text x="58" y="20" text-anchor="middle" font-size="12" fill="#2d5340">${e(String(label).slice(0,15))}</text><text x="58" y="34" text-anchor="middle" font-size="8" fill="#839780">${isContainer?'页面身份':e(p.value.role||'观察对象')}</text></g>`;
  }
  if(!nodePositions.size) svg+='<text x="340" y="180" text-anchor="middle" fill="#84907f" font-size="14">没有匹配的已确认对象</text>';
  svg+='</g>';$('graph').innerHTML=svg;
  $('graph-count').textContent=`${cs.length} 页 · ${os.length} 对象`;
  $('graph-list').innerHTML=all.map(v=>{const id=nodeId(v);return `<button data-object="${e(id)}" class="${nodeId(selected)===id?'active':''}"><span>${e(v.identity?containerName(f,id):v.semanticDescriptor || v.role)}</span><small>${v.identity?'Container':e(v.role)}</small></button>`}).join('') || '<div class="empty">没有匹配对象。身份未知的观察不会被补造为页面节点。</div>';
  if(os.length>18)$('graph-list').insertAdjacentHTML('afterbegin',`<p class="small">图显示前 18 个对象；下方保留全部 ${os.length} 个，可搜索定位。</p>`);
  $('graph').querySelectorAll('[data-node]').forEach(el=>{
    const action=()=>select(nodePositions.get(el.dataset.node).value);
    el.onclick=action;el.onkeydown=ev=>{if(ev.key==='Enter'||ev.key===' '){ev.preventDefault();action();}};
  });
  $('graph').querySelectorAll('[data-edge]').forEach(el=>{const action=()=>select(edges[Number(el.dataset.edge)]);el.onclick=action;el.onkeydown=ev=>{if(ev.key==='Enter'||ev.key===' '){ev.preventDefault();action();}}});
  transformGraph();
}
function transformGraph() { const g=$('graph-transform');if(g)g.setAttribute('transform',`translate(${offset.x} ${offset.y}) scale(${zoom})`);$('zoom-level').value=Math.round(zoom*100)+'%'; }
function renderPage() {
  const f=frame(), latest=data.frames.at(-1), w=current().revisionId;
  let p=f.pages.find(x=>x.containerId===containerId), shown=f, elements=[], source='Owner 页面视图';
  const sw=f.slices[scopeIndex];
  if(mode==='latest') {
    shown=latest;
    const oldCapture=current().currentPageObservation?.capture?.captureId;
    const oldEvidence=current().currentPageObservation?.evidenceId;
    // A newer page must actually contain the selected old capture; never infer by label.
    p=latest.pages.find(x=>oldEvidence && array(x.observations).some(o=>o.captureEvidenceId===oldEvidence));
    if(p) {
      const o=p.observations.find(o=>o.captureEvidenceId===oldEvidence);
      elements=array(o.occurrences);source=`更正后解释 ${latest.revision.revisionId} / 原观察 ${oldCapture}`;
    }
  } else if(mode==='content') {
    elements=array(p?.accumulatedContent?.items).map(v=>({...v,occurrenceId:v.observationOccurrenceRefs?.[0] || v.presentationRef,locator:v.bounds,state:v.currentState}));
    source='Owner 累计内容投影 / 坐标不可用于执行 · '+array(p?.accumulatedContent?.registrationSourceRefs).length+' 项已采信配准 · '+elements.filter(x=>x.currentPresence!=='observed-current').length+' 项仅历史可见';
  } else if(sw) {
    const presentations=Array.isArray(sw.presentation)?sw.presentation:[sw.presentation].filter(Boolean);
    // Spatial frames are never overlaid. Select one explicit coordinate frame.
    const pf=presentations[Number($('spatial-frame')?.value)||0];
    elements=array(pf?.elements);source=`实际 Slice ${short(sw.sliceId)} · ${pf?.spatialFrameId || elements[0]?.locator?.spatialFrameId || '无坐标'}`;
  } else {
    const observations=array(p?.observations).filter(o=>o.isCurrentCapture);
    elements=observations.flatMap(o=>array(o.occurrences));
    if(!p) elements=array(current().occurrences).filter(o=>o.owningContainerId===containerId);
  }
  $('page-title').textContent=containerId?containerName(f,containerId):'尚未确认页面';
  $('view-note').className='view-note'+(mode==='latest'?' history':'');
  $('view-note').textContent=mode==='latest'?`历史图仍为 ${w}；这里单独显示最新 ${latest.revision.revisionId} 对原观察的解释。${p?'':'未采集对应历史解释。'}`:`${w} · ${source}${!sw&&mode==='current'?' · Slice 未选择':''}`;
  ['current','latest','content'].forEach(v=>$('mode-'+v).setAttribute('aria-pressed',mode===v));
  let located=elements.filter(x=>x.locator && [x.locator.x1,x.locator.y1,x.locator.x2,x.locator.y2].every(Number.isFinite));
  if(mode!=='content'&&located.length) {
    const spatialFrame=located[0].locator.spatialFrameId;
    located=located.filter(x=>x.locator.spatialFrameId===spatialFrame);
  }
  if(!located.length) $('page-stage').innerHTML=`<div class="empty">${mode==='latest'?'没有已导出的对应历史解释。':'当前范围没有可绘制的 Owner 布局。'}<br>没有资料不表示页面为空；可在对象与来源中查看现有记录。</div>`;
  else {
    const minX=Math.min(0,...located.map(v=>v.locator.x1)), minY=Math.min(0,...located.map(v=>v.locator.y1));
    const maxX=Math.max(1,...located.map(v=>v.locator.x2)),maxY=Math.max(1,...located.map(v=>v.locator.y2));
    const scale=280/(maxX-minX), h=Math.max(240,(maxY-minY)*scale+24);
    $('page-stage').innerHTML=`<svg class="page-svg" width="300" height="${h}" viewBox="-10 -10 300 ${h}" role="img" aria-label="页面布局，${e(source)}">${located.map((v,i)=>{
      const b=v.locator,x=(b.x1-minX)*scale,y=(b.y1-minY)*scale,bw=(b.x2-b.x1)*scale,bh=(b.y2-b.y1)*scale,historical=v.currentPresence && v.currentPresence!=='observed-current';
      return `<g data-element="${i}" tabindex="0" role="button" aria-label="${e(v.semanticDescriptor||v.role)}" class="${nodeId(selected)===nodeId(v)?'active':''}"><rect x="${x}" y="${y}" width="${Math.max(bw,1)}" height="${Math.max(bh,1)}" rx="3" fill="${historical?'#fff9e9':'#eff5ed'}" stroke="${historical?'#c4a365':'#b6ccb5'}" ${historical?'stroke-dasharray="4 3"':''}/><clipPath id="clip${i}"><rect x="${x+3}" y="${y}" width="${Math.max(bw-6,1)}" height="${Math.max(bh,1)}"/></clipPath><text x="${x+6}" y="${v.role==='scrollable'?y+bh-8:y+Math.min(bh/2+4,20)}" font-size="${bh<16?8:11}" fill="#315b42" clip-path="url(#clip${i})">${e(v.semanticDescriptor || v.role)}${v.state?' · '+e(v.state):''}${historical?' · 历史':''}</text></g>`;
    }).join('')}</svg>`;
    $('page-stage').querySelectorAll('[data-element]').forEach(el=>{const action=()=>{const value=located[Number(el.dataset.element)];const observation=array(p?.observations).find(o=>array(o.occurrences).some(v=>v.occurrenceId===value.occurrenceId));select(observation?{...value,captureEvidenceId:observation.captureEvidenceId,identityEvidenceId:observation.identityEvidenceId}:value,shown.revision.revisionId);};el.onclick=action;el.onkeydown=ev=>{if(ev.key==='Enter'||ev.key===' '){ev.preventDefault();action();}};});
  }
  const unknown=[...array(p?.unknown),...array(p?.accumulatedContent?.unknown)];
  $('page-unknown').textContent='待证：'+([...new Set(unknown)].map(u=>unknownNames[u]||u).join('；') || '未导出完整覆盖证明。')+(elements.some(v=>v.state==null)?' 动态状态未证。':'');
  const semantics=array(p?.currentSemantics);
  const history=array(p?.semanticReferences).filter(s=>s.isHistorical);
  $('semantic-cards').innerHTML=semantics.map(s=>`<div class="semantic-card"><b>${e(s.reused?'已采信沿用的稳定语义':'当前已采信语义')} · ${e(s.pageName)}</b>${e(s.pagePurpose)}<br>对应来源 ${e(short(s.currentCaptureEvidenceId))} · 动态状态独立待证</div>`).join('')+history.map(s=>`<div class="semantic-card history"><b>历史参考 · ${e(s.meaning?.pageName)}</b>旧信息可读；当前采信须由对应证据支持。<br>${e(short(s.evidenceId))}</div>`).join('');
}
function locateChange(change) {
  const targetIndex=change.after?frameIndex:compareIndex;
  const value=change.after||change.before;
  chooseFrame(targetIndex);
  const owner=value?.slice?.rootContainerId||value?.identity?.containerId||value?.owningContainerId||value?.containerId;
  if(owner)containerId=owner;
  const slices=frame().slices;
  scopeIndex=slices.findIndex(s=>s.sliceId===value?.sliceId);
  if(scopeIndex<0&&value?.occurrenceId)scopeIndex=slices.findIndex(s=>array(s.slice.occurrences).some(o=>o.occurrenceId===value.occurrenceId));
  selected=value;selectedSource=current().revisionId;tab='inspect';mode='current';render();
  $('detail').insertAdjacentHTML('afterbegin',`<p class="small">已定位 ${e(change.type)} · 来源 ${e(current().revisionId)} · ${e(change.field)}。${scopeIndex>=0?'已联动对应实际 Slice。':'本记录未关联实际 Slice，保留未知。'}</p>`);
}
function sources(ids) { return array(ids).map(id=>`<button class="source-link" data-source="${e(id)}">${e(short(id))}</button>`).join(''); }
function renderDetail() {
  const f=frame(),r=current();
  if(tab==='inspect') {
    const v=selected || f.slices[scopeIndex] || f.pages.find(p=>p.containerId===containerId) || r;
    const name=v.identity?containerName(f,v.identity.containerId):v.semanticDescriptor || v.sliceId || (v.gapId?gapNames[v.kind]:v.kind!=null&&relationNames[v.kind]) || (v.containerId?'Owner 页面视图':'选定 World');
    $('detail').innerHTML=`<div class="detail-grid"><div><h3>${e(name)}</h3>${facts([['对象来源版本',selected?selectedSource:r.revisionId],['对象',nodeId(v)||v.sliceId||containerId],['页面归属',v.owningContainerId||v.containerId||v.slice?.rootContainerId],['范围',v.slice?.inScopeContainerIds?.join(', ')||v.scope],['动态状态',v.state??'未知 / 未采集'],['来源说明',v.sliceId?'不可变 Slice；只读表达':selected&&selectedSource!==r.revisionId?'对象来自更正后解释；图仍为 '+r.revisionId:'选定版本 Owner 输出']])}${sources(v.evidenceBasis || [v.captureEvidenceId,v.identityEvidenceId].filter(Boolean))}</div><div><h3>读取路径与原始依据</h3><p>界面只表达正式输出。原始 Ledger 接纳/拒绝记录没有随此例导出时，依赖覆盖保持未知；不能从画面反推执行许可。</p>${raw('展开对象完整记录',v)}${raw('选定 World 的观察来源',r.currentPageObservation)}</div></div>`;
  } else if(tab==='compare') {
    const changes=changesBetweenFrames(data.frames[compareIndex],f);
    $('detail').innerHTML=`<div class="compare-controls"><label>从<select id="compare-from">${options(data.frames.map((f,i)=>[i,`${f.revision.revisionId} · 时点 ${i+1}`]),compareIndex)}</select></label><span>→</span><label>到 <strong>${e(r.revisionId)} · 时点 ${frameIndex+1}</strong></label></div><p class="small">比较实际快照。对象未再观察不证明现实删除；认识纠正与真实环境变化须查看其来源。</p><div class="change-list">${changes.map((c,i)=>`<button class="change-row" data-diff="${i}"><strong>${e(c.type)}</strong><span>${e(short(c.id))}</span><span>${e(c.after?.semanticDescriptor||c.before?.semanticDescriptor||'')}</span></button>`).join('')||'<div class="empty">所选投影没有差异。未导出的领域仍未知。</div>'}</div>`;
    $('compare-from').onchange=ev=>{compareIndex=Number(ev.target.value);renderDetail();$('diff-count').textContent=changesBetweenFrames(data.frames[compareIndex],f).length;};
    document.querySelectorAll('[data-diff]').forEach(el=>el.onclick=()=>{const c=changes[Number(el.dataset.diff)];locateChange(c);});
  } else if(tab==='flow') {
    const gaps=array(f.knowledge?.gaps), recovery=f.recovery;
    const agent=f.actualConsultation || f.agentContext;
    $('detail').innerHTML=`<div class="flow-path"><span>观察 ${e(r.currentPageObservation?.capture?.captureId||'未采集')}</span><span>→ World ${e(r.revisionId)}</span><span>→ ${f.slices.length} 个实际 Slice</span><span>→ Agent ${agent?'有记录':'正文未采集'}</span></div><div class="detail-grid"><div><h3>当前知识缺口 · ${f.knowledge?gaps.length:'未采集'}</h3><div class="gap-list">${gaps.map(g=>`<div class="gap"><strong>${e(gapNames[g.kind]||g.kind)}${g.conflicted?' · 冲突':''}</strong>${e(g.reason)}<br>范围 ${e(g.scope)}<br>对象 ${e(g.objectRef)}${sources(g.evidenceIds)}</div>`).join('')||'<div class="empty">'+(f.knowledge?'此输出没有列出缺口；不证明所有未导出领域已知。':'未采集知识投影，不据此计算零缺口。')+'</div>'}</div>${raw('完整知识输出',f.knowledge)}</div><div><h3>补证与实际消费</h3>${recovery?`<p>需要核查：${recovery.reviewRequired?'是':'否'} · ${e(recovery.reason)}</p>${array(recovery.gaps).map(g=>`<div class="gap ${g.knownExecutionCount==null?'':'resolved'}"><strong>已记录执行 ${g.knownExecutionCount??'未知'} 次</strong>${e(short(g.gapId))} · ${e(g.recordCoverage)}<br>${array(g.executions).map(x=>`${e(x.executionId)} · 结果 v${e(x.resultVersion)} · ${e(x.outcome)}`).join('<br>')}</div>`).join('')}${raw('执行与结果版本历史',recovery)}`:'<p>补证执行记录未采集；不能显示为“从未尝试”。</p>'}${agent?raw('实际消费记录 / 受控咨询正文',agent):'<div class="callout">未采集本帧实际咨询正文。页面上能看到知识，不表示模型已收到它。</div>'}${data.artifacts?raw('本段过程的通道与消费附件（跨时点，非当前事实）',data.artifacts):''}</div></div>`;
  } else if(tab==='effects') {
    const corrections=array(f.knowledge?.corrections).length?f.knowledge.corrections:f.pages.flatMap(p=>array(p.corrections));
    const impacts=array(f.effectImpacts);
    $('detail').innerHTML=`<div class="detail-grid"><div><h3>认识纠正 · ${corrections.length}</h3><p>World 更新对旧观察的解释；原观察、已发出的效果及终局记录独立保留。</p>${corrections.map(c=>`<div class="callout">${e(c.applied?'已应用纠正':'未应用 / 拒绝')} · ${e(c.reason)}${raw('纠正原始记录',c)}</div>`).join('')||'<p>本帧未导出纠正记录。</p>'}${raw('原始观察 / 采集时间不随纠正改写',r.currentPageObservation)}</div><div><h3>实际效果与核查</h3>${f.terminal!=null?`<p><strong>终局已记录：${f.terminal?'是':'否'}</strong></p>`:''}${impacts.map(v=>`<div class="callout"><strong>${e(v.resolution==='Unresolved'?'未决':v.resolution)} · ${e(v.deliveryStatus)}</strong><br>已消费依据被撤销：${array(v.revokedConsumedEvidenceIds).length} 项<br>依赖覆盖 ${e(v.dependencyCoverage)}<br>${v.terminalAlreadyRecorded?'原终局保留；核查不重启运行。':'核查不等于自动恢复。'}${raw('影响核查记录',v)}</div>`).join('')||'<p>本帧没有已导出的影响核查；依赖缺失不等于无影响。</p>'}${raw(f.terminal?'保留的原终局记录':'事件处理结果（未声明为终局）',f.outcome)}${raw('本帧已导出效果核查',f.effectImpacts)}${raw('截至本帧的实际回执',f.receipts)}${raw('截至本帧的实际验证',f.verifications)}${data.artifacts?raw('全段原始回执 / 验证 / 决策（跨时点附件）',data.artifacts):''}</div></div>`;
  } else if(tab==='provenance') {
    $('detail').innerHTML=`<div class="detail-grid"><div><h3>${e(data.title)}</h3>${facts([['核心版本',data.provenance.coreCommit],['输入类别',data.provenance.inputClass],['生成测试',data.provenance.sourceTest],['资料格式',data.schemaVersion],['实况资格',data.provenance.liveDeviceOrModelProven?'见来源声明':'未证明当前真机或模型']])}<ul class="readable-list">${array(data.provenance.limitations).map(x=>`<li>${e(x)}</li>`).join('')}</ul></div><div><h3>来源边界</h3><p>记录来自已发布核心的只读导出。浏览器不配准画面、不判断身份、不授予执行许可。摘要用于校验本地资料一致性，不证明第三方导入的真实性。</p>${raw('来源完整记录',data.provenance)}</div></div>`;
  }
  document.querySelectorAll('[data-source]').forEach(el=>el.onclick=()=>showSource(el.dataset.source));
}
function showSource(id) {
  const r=current(), uses=[];
  const seen=new Set();
  function walk(v,path,depth=0) {
    if(depth>18 || !v || typeof v!=='object') return;
    if(seen.has(v))return;seen.add(v);
    if(Object.values(v).some(x=>x===id || Array.isArray(x)&&x.includes(id))) uses.push({path,record:v});
    for(const [k,x]of Object.entries(v))if(typeof x==='object')walk(x,path+'.'+k,depth+1);
  }
  walk(selectedSource&&selectedSource!==r.revisionId?data.frames.find(x=>x.revision.revisionId===selectedSource)||frame():frame(),'sourceFrame');
  $('detail').innerHTML=`<h3>来源引用 · ${e(short(id))}</h3><p class="small">以下为选定时点的实际引用，不补画缺失的因果。原始 Ledger 记录如未导出，仍属未采集。</p>${facts([['证据标识',id],['选定版本',r.revisionId],['引用记录',uses.length]])}${uses.map(x=>raw(x.path,x.record)).join('')}<button id="back-detail">返回对象</button>`;
  $('back-detail').onclick=renderDetail;
}
$('dataset').onchange=ev=>loadExample(ev.target.value);
$('prev').onclick=()=>{stop();chooseFrame(frameIndex-1)};
$('next').onclick=()=>{stop();chooseFrame(frameIndex+1)};
$('time').oninput=ev=>{stop();chooseFrame(ev.target.value)};
$('frame').onchange=ev=>{stop();chooseFrame(ev.target.value)};
$('events').onclick=ev=>{const b=ev.target.closest('[data-frame]');if(b){stop();chooseFrame(b.dataset.frame)}};
$('play').onclick=()=>{
  if(timer){stop();return;}if(frameIndex===data.frames.length-1)chooseFrame(0);
  $('play').textContent='Ⅱ 暂停';timer=setInterval(()=>{if(frameIndex>=data.frames.length-1){stop();return;}chooseFrame(frameIndex+1);if(frameIndex===data.frames.length-1)stop();},1800);
};
$('container').onchange=ev=>{containerId=ev.target.value;scopeIndex=-1;selected=null;render()};
$('scope').onchange=ev=>{scopeIndex=Number(ev.target.value);selected=null;mode='current';render()};
['current','latest','content'].forEach(v=>$('mode-'+v).onclick=()=>{mode=v;renderPage();renderDetail()});
document.querySelectorAll('[data-tab]').forEach(el=>el.onclick=()=>{tab=el.dataset.tab;render()});
$('provenance').onclick=()=>{tab='provenance';render();$('detail').scrollIntoView({behavior:'smooth',block:'nearest'})};
$('search').oninput=renderGraph;$('show-elements').onchange=renderGraph;$('relation-filter').onchange=renderGraph;
$('graph-list').onclick=ev=>{const b=ev.target.closest('[data-object]');if(b){const id=b.dataset.object;select(array(current().containers).find(c=>c.identity.containerId===id)||array(current().occurrences).find(o=>o.occurrenceId===id));}};
function zoomBy(factor) {zoom=Math.max(.35,Math.min(3,zoom*factor));transformGraph()}
$('zoom-in').onclick=()=>zoomBy(1.2);$('zoom-out').onclick=()=>zoomBy(1/1.2);
$('reset').onclick=()=>{zoom=1;offset={x:0,y:0};$('search').value='';$('relation-filter').value='all';renderGraph()};
$('focus').onclick=()=>{const p=nodePositions.get(nodeId(selected)||containerId);if(p){zoom=1.6;offset={x:340-p.x*zoom,y:210-p.y*zoom};transformGraph()}};
let drag=null;
$('graph').onpointerdown=ev=>{if(ev.target.closest('[data-node],[data-edge]'))return;drag={x:ev.clientX,y:ev.clientY,ox:offset.x,oy:offset.y};$('graph').setPointerCapture(ev.pointerId)};
$('graph').onpointermove=ev=>{if(!drag)return;const scale=680/$('graph').getBoundingClientRect().width;offset={x:drag.ox+(ev.clientX-drag.x)*scale,y:drag.oy+(ev.clientY-drag.y)*scale};transformGraph()};
$('graph').onpointerup=()=>drag=null;$('graph').onpointercancel=()=>drag=null;
$('graph').addEventListener('wheel',ev=>{ev.preventDefault();zoomBy(ev.deltaY<0?1.1:1/1.1)},{passive:false});
$('import').onchange=async ev=>{const file=ev.target.files[0];if(!file)return;stop();++loadToken;try{if(file.size>25*1024*1024)throw Error('文件超过 25 MB，请缩小导出范围。');acceptDataset(JSON.parse(await file.text()));$('dataset').value='';$('dataset-meta').textContent=`本地导入 ${file.name} · ${data.frames.length} 个时点`;}catch(err){error('导入失败：'+err.message+' 原视图保留。')}finally{ev.target.value=''}};
$('download').onclick=()=>{if(!data)return;const blob=new Blob([JSON.stringify(data,null,2)],{type:'application/json'}),a=document.createElement('a');a.href=URL.createObjectURL(blob);a.download='world-view.json';a.click();setTimeout(()=>URL.revokeObjectURL(a.href),1000)};
document.addEventListener('visibilitychange',()=>{if(document.hidden)stop()});
try {
  const response=await fetch('examples/index.json');if(!response.ok)throw Error('读取示例索引失败。');
  const index=await response.json();manifest=Array.isArray(index)?index:(index.examples || index.families);
  if(!manifest?.length)throw Error('没有可读取的示例。请按说明导出。');
  $('dataset').disabled=false;
  $('dataset').innerHTML=options(manifest.map(x=>[x.file,x.title]),manifest[0].file);
  await loadExample(manifest[0].file);
} catch(err) { error(err.message+' 可通过右上角导入符合格式的本地记录。'); }
