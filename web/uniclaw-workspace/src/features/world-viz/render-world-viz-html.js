'use strict';

// WORLD-VISUALIZATION-001 切片 2：WorldViz 渲染层（纯 vanilla，HTML/SVG 字符串）。
// 输入 = world-viz-view-model 输出；输出 = 单容器 HTML。类名一律 wv- 前缀，
// 避免与宿主（DSH / workspace）样式冲突；状态不只靠颜色（QV7/spec）。

function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

function timeLabel(iso) {
  return iso ? new Date(iso).toISOString().slice(11, 16) : '—';
}

function renderTimeline(viewModel) {
  const cells = viewModel.timeline.revisions.map((rev) => {
    const m = rev.markers;
    const chips = [
      m.navigation && '<span class="wv-chip wv-nav">导航</span>',
      m.correctionApplied && '<span class="wv-chip wv-corr">纠错</span>',
      m.correctionRejected && '<span class="wv-chip wv-corr">拒</span>',
      m.semantic && '<span class="wv-chip wv-sem">语义</span>',
      m.reused && '<span class="wv-chip wv-reused">沿用</span>',
      m.structural && '<span class="wv-chip wv-struct">结构</span>',
    ].filter(Boolean).join('');
    const current = rev.id === viewModel.selection.revisionId ? ' wv-current' : '';
    return `<div class="wv-rev${current}" data-revision="${escapeHtml(rev.id)}" role="button" tabindex="0">
      <div class="wv-no">#${rev.number} ${escapeHtml(rev.id)}</div>
      <div class="wv-time">${timeLabel(rev.captureHighWater)}</div>
      <div class="wv-chips">${chips}</div></div>`;
  }).join('');
  return `<div class="wv-timeline" data-role="timeline">${cells}</div>`;
}

function renderThumb(thumb, extraClass) {
  if (!thumb || thumb.state === 'not-collected') {
    return `<div class="wv-thumb${extraClass ? ' ' + extraClass : ''}"><div class="wv-noshot">无观察片段（未采集）</div></div>`;
  }
  // 优先真实设备截图（captureId 精确映射）；无截图回退页面表达重绘并标注来源。
  if (thumb.screenshot) {
    return `<div class="wv-thumb${extraClass ? ' ' + extraClass : ''}"><img class="wv-shot" src="${escapeHtml(thumb.screenshot)}" loading="lazy" alt="真实设备截图"><span class="wv-shot-tag">实拍</span></div>`;
  }
  if (thumb.state === 'unknown') {
    return `<div class="wv-thumb${extraClass ? ' ' + extraClass : ''}"><div class="wv-noshot">观察片段为空（未知）</div></div>`;
  }
  const w = thumb.width, h = thumb.height;
  const items = thumb.items.map((item) => {
    const left = (item.x1 / w * 100).toFixed(2), top = (item.y1 / h * 100).toFixed(2);
    const width = Math.max(2, ((item.x2 ?? item.x1) - item.x1) / w * 100).toFixed(2);
    const height = Math.max(2, ((item.y2 ?? item.y1) - item.y1) / h * 100).toFixed(2);
    const cls = item.role === 'title' ? 'wv-ti wv-title' : 'wv-ti';
    return `<div class="${cls}" style="left:${left}%;top:${top}%;width:${width}%;height:${height}%" title="${escapeHtml(item.role)}: ${escapeHtml(item.text)}">${escapeHtml(item.text || item.role || '·')}</div>`;
  }).join('');
  return `<div class="wv-thumb${extraClass ? ' ' + extraClass : ''}">${items}</div>`;
}

function layoutGraph(graph) {
  const ids = graph.nodes.map((n) => n.id);
  if (ids.length === 0) return new Map();
  const adjacent = new Map(ids.map((id) => [id, []]));
  for (const edge of [...graph.navigationEdges, ...graph.structuralRelations]) {
    if (adjacent.has(edge.source)) adjacent.get(edge.source).push(edge.target);
  }
  const depth = new Map([[ids[0], 0]]);
  const queue = [ids[0]];
  while (queue.length > 0) {
    const current = queue.shift();
    for (const next of adjacent.get(current) || []) {
      if (!depth.has(next)) { depth.set(next, depth.get(current) + 1); queue.push(next); }
    }
  }
  const maxDepth = Math.max(0, ...[...depth.values()]);
  ids.forEach((id) => { if (!depth.has(id)) depth.set(id, maxDepth + 1); });
  const byDepth = new Map();
  for (const id of ids) {
    const d = depth.get(id);
    if (!byDepth.has(d)) byDepth.set(d, []);
    byDepth.get(d).push(id);
  }
  const positions = new Map();
  for (const [d, group] of byDepth) {
    group.forEach((id, index) => positions.set(id, { x: 130 + d * 290, y: 80 + index * 205 + (d % 2) * 50 }));
  }
  return positions;
}

function renderGraph(graph) {
  const positions = layoutGraph(graph);
  const edges = [];
  for (const relation of graph.structuralRelations) {
    const a = positions.get(relation.source), b = positions.get(relation.target);
    if (a && b) edges.push(`<line x1="${a.x}" y1="${a.y + 40}" x2="${b.x}" y2="${b.y - 60}" stroke="#c9a0ff" stroke-width="2" stroke-dasharray="6 5"/><text x="${(a.x + b.x) / 2}" y="${(a.y + b.y) / 2 - 6}" text-anchor="middle" font-size="10" fill="#c9a0ff">包含</text>`);
  }
  for (const edge of graph.navigationEdges) {
    const a = positions.get(edge.source), b = positions.get(edge.target);
    if (a && b) edges.push(`<line x1="${a.x}" y1="${a.y + 40}" x2="${b.x}" y2="${b.y - 60}" stroke="#7fc4ff" stroke-width="2" marker-end="url(#wv-arrow)"/><text x="${(a.x + b.x) / 2}" y="${(a.y + b.y) / 2 - 6}" text-anchor="middle" font-size="10" fill="#7fc4ff">导航</text>`);
  }
  const nodes = graph.nodes.map((node) => {
    const p = positions.get(node.id);
    const isCurrent = node.id === graph.currentContainerId;
    const isSelected = graph.selectedNodeId === node.id;
    const badges = [
      isCurrent && '<span class="wv-badge wv-current">当前页</span>',
      node.semantic.reused && '<span class="wv-badge wv-reused">沿用</span>',
      node.signatureWithdrawn && '<span class="wv-badge wv-withdrawn">签名被撤回</span>',
    ].filter(Boolean).join('');
    const label = node.semantic.state === 'ok'
      ? `<span class="wv-nlabel-text">${escapeHtml(node.label)}</span>`
      : '<span class="wv-badge wv-unknown">语义未知</span>';
    return `<g transform="translate(${p.x},${p.y})" data-node="${escapeHtml(node.id)}">
      <foreignObject x="-103" y="-90" width="206" height="180">
        <div xmlns="http://www.w3.org/1999/xhtml" class="wv-node${isCurrent ? ' wv-node-current' : ''}${isSelected ? ' wv-node-selected' : ''}">
          ${renderThumb(node.pageThumb)}
          <div class="wv-nlabel">${label}</div>
          <div class="wv-nid">${escapeHtml(node.id.slice(0, 20))}</div>
          <div class="wv-nbadges">${badges}</div>
        </div></foreignObject></g>`;
  }).join('');
  const state = graph.state === 'empty' || graph.state === 'unknown'
    ? `<text x="50%" y="45%" text-anchor="middle" fill="#6f81a0" font-size="14">${graph.state === 'unknown' ? '未知 revision（空投影）' : '本 revision 无节点（空）'}</text>` : '';
  return `<svg class="wv-graph" data-role="graph" viewBox="0 0 1400 760">
    <defs><marker id="wv-arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0 0L10 5L0 10z" fill="#7fc4ff"/></marker></defs>
    ${edges.join('')}${nodes}${state}</svg>`;
}

function renderDetail(viewModel) {
  if (!viewModel.detail) {
    return '<div class="wv-notice">未选中节点。单击图节点查看 Container 详情与页面表达。</div>';
  }
  const node = viewModel.detail;
  const thumb = node.pageThumb;
  const semantic = node.semantic.state === 'ok'
    ? escapeHtml(node.label) + (node.semantic.reused ? ' <span class="wv-badge wv-reused">沿用</span>' : '')
    : '<span class="wv-badge wv-unknown">语义未知（无已采信知识）</span>';
  const items = thumb.state === 'ok'
    ? `<div class="wv-obslist">${thumb.items.map((item) =>
        `<div class="wv-ob"><span class="wv-ob-role">${escapeHtml(item.role)}</span><span class="wv-ob-text">${escapeHtml(item.text ?? '')}</span>${item.state ? `<span class="wv-ob-state">${escapeHtml(item.state)}</span>` : ''}</div>`).join('')}
        <div class="wv-notice">未知声明：${thumb.viewUnknown.map(escapeHtml).join('、') || '—'}</div></div>`
    : `<div class="wv-notice">${escapeHtml(thumb.reason)}</div>`;
  const expression = thumb.state === 'ok'
    ? `重绘自 capture ${escapeHtml(thumb.captureId.slice(0, 12))}${thumb.isCurrent ? '（当前）' : '（最近观察，非当前）'}${thumb.screenshot ? ' · 实拍截图' : ' · 页面表达重绘（无实拍）'}`
    : '<span class="wv-badge wv-unknown">无观察片段</span>';
  return `<div class="wv-kv"><b>Container</b>${escapeHtml(node.id)}</div>
    <div class="wv-kv"><b>稳定语义</b>${semantic}</div>
    <div class="wv-kv"><b>知识来源</b>${escapeHtml(node.semantic.knowledgeSource ?? '未采集')}</div>
    <div class="wv-kv"><b>当前锚</b>${node.id === viewModel.graph.currentContainerId ? '是' : '否'}</div>
    ${node.signatureWithdrawn ? '<div class="wv-kv"><b>纠错状态</b><span class="wv-badge wv-withdrawn">签名被撤回</span><span class="wv-notice">该容器的唯一识别签名已被纠错撤走（无新签名）；后续同签名观察会创建新容器，本节点不再参与匹配。</span></div>' : ''}
    <div class="wv-kv"><b>证据引用</b>${node.semantic.evidenceRefs.slice(0, 4).map((e) => escapeHtml(e.slice(0, 10))).join(' · ') || '—'}</div>
    <div class="wv-kv"><b>页面表达</b>${expression}</div>
    ${renderThumb(thumb, ' wv-detail-thumb')}${items}`;
}

function renderChanges(viewModel) {
  const revision = viewModel.timeline.revisions.find((r) => r.id === viewModel.selection.revisionId);
  if (!revision) return '';
  const m = revision.markers;
  const labels = [
    m.navigation && '导航验证', m.correctionApplied && '纠错(接受)', m.correctionRejected && '纠错(拒绝)',
    m.semantic && '语义知识更新', m.reused && '语义沿用', m.structural && '结构关系变化',
  ].filter(Boolean);
  return `<div class="wv-changes">
    <span>revision <b>${escapeHtml(revision.id)}</b>（#${revision.number}）</span>
    <span>采集时间高水位 <b>${timeLabel(revision.captureHighWater)}</b>（提交时间未记录）</span>
    <span>变更标记 <b>${labels.join('、') || '仅证据并入'}</b></span>
  </div>`;
}

function renderDeferred(viewModel) {
  const entries = (viewModel.deferred || []).map((entry) =>
    `<span class="wv-deferred" title="QV7 缺省标注">缺省 · ${escapeHtml(entry.label)}——${escapeHtml(entry.note)}</span>`).join('');
  return entries ? `<div class="wv-deferred-bar">${entries}</div>` : '';
}

function renderWorldVizHtml(viewModel) {
  if (!viewModel) throw new TypeError('viewModel is required');
  if (viewModel.empty) {
    return `<div class="wv-root" data-role="world-viz">
      <div class="wv-header"><h1>World 模型可视化</h1>
        <span class="wv-source">数据源：${escapeHtml(viewModel.provenance)}</span></div>
      <div class="wv-body"><div class="wv-notice wv-empty-state">无数据（空 World：无 revision 记录）</div></div>
      ${renderDeferred(viewModel)}</div>`;
  }
  if (viewModel.failed) {
    return `<div class="wv-root" data-role="world-viz">
      <div class="wv-header"><h1>World 模型可视化</h1>
        <span class="wv-source">数据源：${escapeHtml(viewModel.provenance)}</span></div>
      <div class="wv-body"><div class="wv-failed-state">加载失败：${escapeHtml(viewModel.error ?? '未知错误')}</div></div>
      ${renderDeferred(viewModel)}</div>`;
  }
  const graph = { ...viewModel.graph, selectedNodeId: viewModel.selection.nodeId };
  return `<div class="wv-root" data-role="world-viz">
    <div class="wv-header"><h1>World 模型可视化</h1>
      <span class="wv-source">数据源：<b>${escapeHtml(viewModel.provenance)}</b> · 节点缩略 = Container 页面表达重绘</span></div>
    ${renderTimeline(viewModel)}
    <div class="wv-main">
      <div class="wv-graph-wrap">
        <div class="wv-legend">
          <label><input type="checkbox" checked data-kind="nav"> 导航（已验证，允许环）</label>
          <label><input type="checkbox" checked data-kind="struct"> Contains 结构关系</label>
          <span class="wv-badge wv-current">当前页</span><span class="wv-badge wv-unknown">语义未知</span><span class="wv-badge wv-reused">沿用</span>
        </div>
        ${renderGraph(graph)}
      </div>
      <div class="wv-detail"><h2>详情</h2>${renderDetail(viewModel)}</div>
    </div>
    ${renderChanges(viewModel)}
    ${renderDeferred(viewModel)}</div>`;
}

module.exports = { renderWorldVizHtml };
