// Presentation-only adapter. No identity, registration, permission or effect decisions.
const dictionaries = new Set(['worldState']);
export function normalize(value, dictionary = false) {
  if (Array.isArray(value)) return value.map(v => normalize(v));
  if (!value || typeof value !== 'object') return value;
  return Object.fromEntries(Object.entries(value).map(([key,v]) => {
    const k = dictionary ? key : key[0].toLowerCase() + key.slice(1);
    return [k, normalize(v, dictionaries.has(k))];
  }));
}
export function validateDataset(raw) {
  const data = normalize(raw);
  if (data.schemaVersion !== 'world-visualization.v1') throw Error('无法读取：需要 world-visualization.v1 格式。');
  if (!data.id || !data.title || !data.provenance || !Array.isArray(data.frames) || !data.frames.length) throw Error('资料缺少名称、来源或有效时点。');
  if (data.frames.length > 2000) throw Error('资料超过 2000 个时点，请缩小导出范围。');
  const keys = new Set();
  for (const f of data.frames) {
    const w = f.revision?.revisionId;
    if (!w || !f.key || keys.has(f.key)) throw Error('时点必须有唯一标识和 World 版本。');
    keys.add(f.key);
    for (const field of ['containers','relations','occurrences','semanticKnowledge'])
      if (f.revision[field]!=null && !Array.isArray(f.revision[field])) throw Error('World 对象集合格式无效。');
    if (array(f.revision.containers).some(c=>!c?.identity?.containerId)) throw Error('页面节点缺少身份。');
    if (array(f.revision.occurrences).some(o=>!o?.occurrenceId)) throw Error('观察对象缺少标识。');
    for (const field of ['pages','slices']) if (f[field]!=null && !Array.isArray(f[field])) throw Error('页面或 Slice 集合格式无效。');
    if (f.graph?.sourceRevisionId && f.graph.sourceRevisionId !== w) throw Error('关系图来源版本不一致。');
    for (const p of f.pages || []) if (p.sourceRevisionId !== w) throw Error('页面来源版本与所选 World 不一致。');
    if (f.knowledge?.sourceRevisionId && f.knowledge.sourceRevisionId !== w) throw Error('知识来源版本不一致。');
    // Actual retained slices belong in historicalSlices, never the current selection.
    for (const s of f.slices || []) {
      if (!s.sliceId || s.slice?.sourceRevisionId !== w) throw Error('当前 Slice 来源版本不一致。');
      for (const p of Array.isArray(s.presentation) ? s.presentation : [s.presentation].filter(Boolean)) {
        if (p.sourceRevisionId !== w) throw Error('Slice 表达版本不一致。');
      }
    }
    f.pages ||= []; f.slices ||= [];
  }
  return data;
}
export const array = value => Array.isArray(value) ? value : [];
export const short = value => value == null ? '未采集' : String(value).length > 25 ? String(value).slice(0,17) + '…' + String(value).slice(-5) : String(value);
export const gapNames = ['身份未知','归属未知','关系依据未知','观察覆盖不完整'];
export function containerName(frame, id) {
  const semantic = array(frame.revision.semanticKnowledge).find(s => s.containerId === id);
  const history = frame.pages.find(p => p.containerId === id)?.semanticReferences?.[0];
  return semantic?.pageName || history?.meaning?.pageName || short(id);
}
export function changesBetween(before, after) {
  const types = [
    ['containers', x=>x.identity.containerId, '节点新增', '本版节点已移出'],
    ['occurrences', x=>x.occurrenceId, '观察新增', '本版未再观察'],
    ['relations', x=>x.relationId || JSON.stringify([x.sourceContainerId,x.targetContainerId,x.kind,x.scope]), '关系新增', '本版关系已移出'],
    ['semanticKnowledge', x=>x.containerId, '稳定语义采信', '当前语义已撤销']
  ];
  const result=[];
  for (const [field,key,added,removed] of types) {
    const a = new Map(array(before?.[field]).map(x=>[key(x),x]));
    const b = new Map(array(after?.[field]).map(x=>[key(x),x]));
    for (const id of new Set([...a.keys(),...b.keys()])) {
      if (!a.has(id)) result.push({id,field,type:added,after:b.get(id)});
      else if (!b.has(id)) result.push({id,field,type:removed,before:a.get(id)});
      else if (JSON.stringify(a.get(id)) !== JSON.stringify(b.get(id))) result.push({id,field,type:'值 / 来源发生变化',before:a.get(id),after:b.get(id)});
    }
  }
  return result;
}

export function changesBetweenFrames(before, after) {
  const graphRevision = f => ({...f.revision, relations:f.graph?.edges || f.revision.relations});
  const result=changesBetween(graphRevision(before),graphRevision(after));
  const pairs=[
    ['pages','页面投影',f=>f.pages,x=>x.containerId],
    ['slices','Slice 范围',f=>f.slices,x=>x.sliceId],
    ['knowledge.gaps','知识缺口',f=>f.knowledge?.gaps,x=>x.gapId],
    ['knowledge.semanticReferences','历史参考',f=>f.knowledge?.semanticReferences,x=>x.evidenceId],
    ['knowledge.corrections','认识纠正',f=>f.knowledge?.corrections,x=>x.correctionEvidenceId || JSON.stringify(x)],
    ['recovery','补证核查',f=>f.recovery?.gaps,x=>x.gapId],
    ['effectImpacts','效果核查',f=>f.effectImpacts,x=>x.reviewId],
    ['receipts','实际回执',f=>f.receipts,x=>x.receiptId],
    ['verifications','实际验证',f=>f.verifications,x=>x.verificationId || x.receiptId || JSON.stringify(x)]
  ];
  for(const [field,label,get,key]of pairs) {
    const a=new Map(array(get(before)).map(x=>[key(x),x])),b=new Map(array(get(after)).map(x=>[key(x),x]));
    for(const id of new Set([...a.keys(),...b.keys()]))if(JSON.stringify(a.get(id))!==JSON.stringify(b.get(id)))
      result.push({field,id,type:label+(a.has(id)?b.has(id)?'变化':'移出投影':'新增'),before:a.get(id),after:b.get(id)});
  }
  return result;
}
