'use strict';

function escapeHtml(value) {
  return String(value == null ? '' : value)
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

function text(value, fallback = '') { return escapeHtml(value == null || value === '' ? fallback : value); }
function attr(name, value) { return value == null || value === '' ? '' : ` ${name}="${escapeHtml(value)}"`; }
function status(value) { return `<span class="workspace-status workspace-status--${escapeHtml(value || 'unknown')}">${text(value, 'unknown')}</span>`; }
function errorText(errors) { return (errors || []).map((error) => `<li class="workspace-notice__item">${text(error.message || error.code || 'Workspace error')}</li>`).join(''); }

function renderWorkspaceHtml(viewModel = {}, options = {}) {
  const vm = viewModel || {};
  const nav = vm.navigation || {};
  const header = vm.taskHeader || {};
  const renderedTaskKeys = new Set();
  const projectsHtml = (nav.projects || []).map((project) => renderProject(project, renderedTaskKeys, nav.selectedProductSessionId)).join('');
  const taskHtml = (nav.taskInstances || [])
    .filter((task) => {
      const key = taskIdentity(task);
      return !key || !renderedTaskKeys.has(key);
    })
    .map((task) => {
      const key = taskIdentity(task);
      if (key) renderedTaskKeys.add(key);
      return renderTask(task, nav.selectedProductSessionId);
    }).join('');
  const root = `workspace workspace--${escapeHtml(vm.status || 'idle')}`;
  return `<div class="${root}" data-workspace-status="${escapeHtml(vm.status || 'idle')}">
  <nav class="workspace-navigation" aria-label="${text(options.navigationLabel, '任务导航')}">
    <div class="workspace-navigation__header"><h1 class="workspace-navigation__title">${text(options.title, 'UniClaw Workspace')}</h1><button type="button" class="workspace-navigation__toggle" data-workspace-action="toggle-navigation" aria-expanded="true" aria-controls="workspace-navigation-content">隐藏任务栏</button></div>
    <div class="workspace-navigation__body" id="workspace-navigation-content"><div class="workspace-navigation__projects">${projectsHtml}</div><div class="workspace-navigation__tasks" data-selected-product-session-id="${escapeHtml(nav.selectedProductSessionId || '')}">${taskHtml}</div></div>
    <div class="workspace-navigation__resize" role="separator" aria-orientation="vertical" aria-label="调整任务栏宽度" data-workspace-resize="navigation" tabindex="0"></div>
  </nav>
  <main class="workspace-main">
    ${renderNotices(vm.notices)}
    <header class="workspace-task-header" data-selected-product-session-id="${escapeHtml(header.productSessionId || '')}">
      <div><p class="workspace-eyebrow">${text(header.source, 'Uni-Agent')}<span class="workspace-header-origin">${text(header.origin, '')}</span></p><h2>${text(header.title, '未选择任务')}</h2></div>
      <button type="button" class="workspace-refresh-action" data-workspace-action="refresh">刷新</button>
      <div class="workspace-task-header__meta">${status(header.status)}<span class="workspace-correlation">${text(header.correlationStatus, 'unselected')}</span></div>
    </header>
    <div class="workspace-content"><div class="workspace-content__primary">
    <section class="workspace-observation-bar" aria-label="任务观察面板">
      <div class="workspace-pane-tabs" role="tablist" aria-label="任务信息标签页">
        ${renderTabButton('trace', 'Trace', vm.activePane)}
        ${renderTabButton('evidence', 'Evidence', vm.activePane)}
        ${renderTabButton('detail', '详情', vm.activePane)}
      </div>
      <div class="workspace-diagnostics-slot"><span><strong>诊断入口</strong><small>Skill 接入后可分析当前任务</small></span><button type="button" class="workspace-diagnostics-action" data-workspace-action="diagnose-task" disabled aria-disabled="true">诊断当前任务</button></div>
    </section>
    <section class="workspace-conversation" aria-labelledby="conversation-title"><h3 id="conversation-title">Uni-Agent 解决过程</h3><div class="workspace-conversation-shell">${renderTimeline(vm.conversationTimeline)}</div></section>
    <section class="workspace-inspector" aria-label="${text(options.panesLabel, '任务信息')}">
      <div class="workspace-inspector__main">
        <div class="workspace-tab-panels">
          ${renderTabPanel('trace', vm.activePane, renderTracePane(vm.tracePane))}
          ${renderTabPanel('evidence', vm.activePane, renderEvidencePane(vm.evidencePane))}
          ${renderTabPanel('detail', vm.activePane, renderDetail(vm.detailActions))}
        </div>
      </div>
    </section>
    </div><aside class="workspace-side-rail" aria-label="任务概览">
      ${renderExecutionPane(vm.executionPane)}
      ${renderMetadataPane(vm.metadataPane)}
    </aside></div>
  </main>
</div>`;
}

function renderProject(project, renderedTaskKeys, selectedProductSessionId) {
  const tasks = (project.tasks || []).filter((task) => {
    const key = taskIdentity(task);
    if (!key) return true;
    if (renderedTaskKeys.has(key)) return false;
    renderedTaskKeys.add(key);
    return true;
  });
  return `<section class="workspace-project" data-project-id="${escapeHtml(project.projectId || '')}"><button type="button" class="workspace-project__action" data-workspace-action="select-project" data-project-id="${escapeHtml(project.projectId || '')}">${text(project.name, '未命名项目')}</button>${tasks.map((task) => renderTask(task, selectedProductSessionId)).join('')}</section>`;
}
function taskIdentity(task = {}) { return task.productSessionId || task.id || null; }
function renderTask(task, selectedProductSessionId = null) {
  const orphan = task.correlationStatus === 'uncorrelated' || !task.productSessionId;
  const selected = !orphan && selectedProductSessionId && selectedProductSessionId === task.productSessionId;
  return `<article class="workspace-task-card${orphan ? ' is-uncorrelated' : ''}${selected ? ' is-selected' : ''}" data-task-id="${escapeHtml(task.id || '')}" data-correlation-status="${escapeHtml(task.correlationStatus || 'unknown')}"><button type="button" class="workspace-task-card__action" data-workspace-action="select-task" data-product-session-id="${escapeHtml(task.productSessionId || '')}"${orphan ? ' disabled aria-disabled="true"' : ''}${selected ? ' aria-current="true"' : ''}><h3>${text(task.title, '未命名任务')}</h3><div class="workspace-task-card__meta"><span>${text(task.productSessionId, orphan ? '未关联 ProductSession' : '未知 ProductSession')}</span>${status(task.status)}</div></button></article>`;
}
function renderTimeline(pane = {}) {
  const rounds = Array.isArray(pane.rounds) ? pane.rounds : [];
  const roundHtml = rounds.map(renderConversationRound).join('');
  const runStageHtml = (pane.runStages || []).map(renderConversationStage).join('');
  return `<div class="workspace-timeline workspace-timeline--conversation" data-status="${escapeHtml(pane.status || 'idle')}" data-conversation-mode="${escapeHtml(pane.mode || 'timeline')}">${roundHtml}${runStageHtml ? `<section class="workspace-conversation-results"><h4>执行与验证</h4>${runStageHtml}</section>` : ''}${roundHtml || runStageHtml ? '' : '<p class="workspace-empty">暂无 Uni-Agent 对话记录</p>'}</div>`;
}
function renderConversationRound(round = {}) { return `<section class="workspace-conversation-round" data-round="${escapeHtml(round.round || '')}" data-status="${escapeHtml(round.status || '')}"><header class="workspace-conversation-round__header"><span>Round ${escapeHtml(round.round || '')}</span>${status(round.status || 'pending')}</header><div class="workspace-dialogue">${(round.stages || []).map(renderConversationStage).join('')}</div></section>`; }
function renderConversationStage(stage = {}) {
  const body = stage.objective || stage.text || stage.justification || '';
  const steps = Array.isArray(stage.steps) && stage.steps.length ? `<ol class="workspace-message__steps">${stage.steps.map((step) => `<li>${text(typeof step === 'string' ? step : step.label || step.description || JSON.stringify(step))}</li>`).join('')}</ol>` : '';
  const meta = [stage.ts, stage.seq != null ? `#${stage.seq}` : ''].filter(Boolean).join(' · ');
  const avatar = stage.role === 'agent' ? 'U' : stage.role === 'requester' ? 'R' : stage.role === 'tool' ? 'T' : '•';
  return `<article class="workspace-message workspace-message--${escapeHtml(stage.role || 'system')}${stage.isError ? ' is-error' : ''}" data-kind="${escapeHtml(stage.kind || '')}"><div class="workspace-message__avatar">${avatar}</div><div class="workspace-message__body"><div class="workspace-message__head"><strong>${text(stage.label, 'Uni-Agent')}</strong>${stage.decisionKind ? `<span class="workspace-chip">${text(stage.decisionKind)}</span>` : ''}${meta ? `<small>${text(meta)}</small>` : ''}</div>${body ? `<p>${text(body)}</p>` : ''}${steps}${stage.detailRef ? `<button type="button" class="workspace-detail-action workspace-detail-action--small" data-workspace-action="resolve-detail" data-detail-ref="${escapeHtml(stage.detailRef.refId || stage.detailRef)}">查看明细</button>` : ''}</div></article>`;
}
function renderTabButton(id, label, active) { return `<button type="button" role="tab" class="workspace-pane-tab${active === id ? ' is-active' : ''}" aria-selected="${active === id ? 'true' : 'false'}" data-workspace-action="select-pane" data-pane-tab="${id}">${text(label)}</button>`; }
function renderTabPanel(id, active, content) { return `<div class="workspace-tab-panel${active === id ? ' is-active' : ''}" role="tabpanel" data-pane-panel="${id}"${active === id ? '' : ' hidden'}>${content}</div>`; }
function renderTracePane(pane = {}) {
  const items = Array.isArray(pane.items) ? pane.items : Object.values(pane.groups || {}).flat();
  // Sequence numbers are local to each recorder. Never compare them across sources.
  const ordered = items.map((item, index) => ({ item, index }));
  const counts = ordered.reduce((result, entry) => { result[entry.item.source || 'unknown'] = (result[entry.item.source || 'unknown'] || 0) + 1; return result; }, {});
  const legend = Object.entries(counts).map(([source, count]) => `<span class="workspace-trace-source workspace-trace-source--${escapeHtml(source)}">${text(traceSourceLabel(source))}<b>${count}</b></span>`).join('');
  const children = new Map();
  const key = (item, id) => `${item.source || 'unknown'}:${id}`;
  const byId = new Map(ordered.filter(({ item }) => item.id).map(({ item }) => [key(item, item.id), item]));
  const roots = [];
  for (const { item } of ordered) {
    const parent = item.parentSpanId && byId.get(key(item, item.parentSpanId));
    // Missing or cyclic parent references remain visible as roots.
    const visited = new Set([item]);
    let ancestor = parent;
    while (ancestor && !visited.has(ancestor)) { visited.add(ancestor); ancestor = ancestor.parentSpanId && byId.get(key(ancestor, ancestor.parentSpanId)); }
    if (!parent || ancestor) { roots.push(item); continue; }
    if (!children.has(parent)) children.set(parent, []);
    children.get(parent).push(item);
  }
  return `<section class="workspace-pane workspace-pane--trace" data-pane="trace"><div class="workspace-pane__heading"><div><h3>Trace</h3><p class="workspace-pane__subheading">保留各来源记录顺序，仅依据明确的父子关系展开</p></div><span class="workspace-pane__count">${ordered.length}</span></div>${ordered.length ? `<details class="workspace-trace-root" open><summary><span>运行轨迹</span><span class="workspace-trace-legend">${legend}</span></summary><ol class="workspace-trace-tree" role="tree">${roots.map((item) => renderTraceCard(item, children, 0)).join('')}</ol></details>` : '<p class="workspace-empty">暂无可展示的 Trace 事件</p>'}</section>`;
}
function traceSourceLabel(source) { return source === 'uniclaw' ? 'UniClaw' : source === 'uniflow' ? 'UniFlow' : String(source).toUpperCase(); }
function renderTraceCard(item, children, depth) {
  const source = item.source || 'unknown';
  const nested = children.get(item) || [];
  const label = item.label || String(item.summary || '未命名事件').split(' · ')[0];
  const detail = item.detailAvailable && item.detailRef ? `<button type="button" class="workspace-detail-action" data-workspace-action="resolve-detail" data-detail-ref="${escapeHtml(item.detailRef.refId || item.detailRef)}">查看明细</button>` : '';
  const content = `<div class="workspace-trace-row__content"><div class="workspace-trace-row__head"><span class="workspace-trace-source workspace-trace-source--${escapeHtml(source)}">${text(traceSourceLabel(source))}</span><strong>${text(label)}</strong><small>${text([item.type, item.ts, item.captureSequence != null ? `#${item.captureSequence}` : item.seq != null ? `#${item.seq}` : ''].filter(Boolean).join(' · '))}</small></div>${item.text && item.text !== label ? `<p>${text(String(item.text).slice(0, 280))}</p>` : ''}</div>`;
  return `<li class="workspace-trace-node" role="treeitem" aria-level="${depth + 1}">${nested.length ? `<details class="workspace-trace-branch"><summary class="workspace-trace-row">${content}<span class="workspace-trace-child-count">${nested.length}</span></summary>${detail ? `<div class="workspace-trace-branch__detail">${detail}</div>` : ''}<ol role="group">${nested.map((child) => renderTraceCard(child, children, depth + 1)).join('')}</ol></details>` : `<div class="workspace-trace-row"><span class="workspace-trace-row__rail" aria-hidden="true"></span>${content}${detail}</div>`}</li>`;
}

function renderEvidencePane(pane = {}) {
  return `<section class="workspace-pane workspace-pane--evidence" data-pane="evidence"><h3>Evidence</h3>${(pane.items || []).map((item) => { const action = item.detailAction || {}; return `<article class="workspace-evidence-card" data-correlation-status="${escapeHtml(item.correlationStatus || '')}"><h4>${text(item.title, '未命名证据')}</h4><p>${text(item.source, '未知来源')}</p>${action.enabled && action.detailRef ? `<button type="button" class="workspace-detail-action" data-workspace-action="resolve-detail" data-detail-ref="${escapeHtml(action.detailRef.refId || action.detailRef)}">查看明细</button>` : `<button type="button" class="workspace-detail-action" disabled aria-disabled="true">${text(action.reason, '详情不可用')}</button>`}${item.error ? `<p class="workspace-error">${text(item.error.message || item.error.code)}</p>` : ''}</article>`; }).join('')}</section>`;
}
function renderMetadataPane(pane = {}) { return `<section class="workspace-pane workspace-pane--metadata" data-pane="metadata"><div class="workspace-pane__heading"><h3>Metadata</h3><span class="workspace-status workspace-status--${escapeHtml(pane.status || 'unknown')}">${text(pane.status, 'unknown')}</span></div><dl>${Object.keys(pane.items || {}).map((key) => `<div><dt>${text(key)}</dt><dd>${typeof pane.items[key] === 'object' && pane.items[key] !== null ? `<details class="workspace-metadata-value"><summary>查看结构化数据</summary><pre>${text(JSON.stringify(pane.items[key], null, 2))}</pre></details>` : text(pane.items[key])}</dd></div>`).join('')}</dl></section>`; }
function renderExecutionPane(pane = {}) { return `<section class="workspace-pane workspace-pane--execution" data-pane="execution"><div class="workspace-pane__heading"><div><h3>执行结果</h3><p class="workspace-pane__subheading">当前运行的结论与证据</p></div><span class="workspace-status workspace-status--${escapeHtml(pane.status || 'empty')}">${text(pane.status === 'ready' ? '已产生' : '待运行', pane.status || 'empty')}</span></div>${(pane.items || []).length ? (pane.items || []).map((item) => `<article class="workspace-execution-card"><div><strong>${text(item.label, '执行结果')}</strong><span class="workspace-chip">${text(item.status, 'unknown')}</span></div><p>${text(item.text, '暂无结果摘要')}</p>${item.evidenceRefs?.length ? `<div class="workspace-reference-list">${item.evidenceRefs.map((ref) => `<button type="button" class="workspace-reference-link" data-workspace-action="resolve-detail" data-detail-ref="${escapeHtml(String(ref).split('/').pop())}">${text(String(ref).split('/').pop())}</button>`).join('')}</div>` : ''}</article>`).join('') : '<p class="workspace-empty workspace-empty--compact">当前 session 尚无 UniClaw runtime 执行产物</p>'}</section>`; }
function renderDetail(detail = {}) { return `<aside class="workspace-detail-drawer" data-detail-status="${escapeHtml(detail.status || 'idle')}" aria-label="详情"><h3>详情</h3>${detail.current ? `<pre>${text(JSON.stringify(detail.current, null, 2))}</pre>` : `<p>${text(detail.status === 'loading' ? '正在加载详情' : '选择记录查看详情')}</p>`}</aside>`; }
function renderNotices(notices = []) {
  if (!notices.length) return '';
  const hasRetry = notices.some((notice) => ['timeout', 'unavailable', 'stale'].includes(notice.code));
  const rows = notices.map((notice) => {
    const count = Number(notice.count) > 1 ? `<span class="workspace-notice__count">${notice.count} 处</span>` : '';
    const source = Array.isArray(notice.sources) && notice.sources.length > 0 ? `<span class="workspace-notice__source">${text(notice.sources.join(' · '))}</span>` : '';
    const details = Number(notice.count) > 1
      ? `<details class="workspace-notice__details"><summary>查看受影响来源</summary><ul>${(notice.occurrences || []).map((item) => `<li>${text(item.source)}：${text(item.message)}</li>`).join('')}</ul></details>`
      : '';
    return `<div class="workspace-notice workspace-notice--${escapeHtml(notice.level || 'warning')}" data-code="${escapeHtml(notice.code || '')}"><div class="workspace-notice__headline"><span>${text(notice.message || notice.code || 'Workspace error')}</span>${count}${source}</div>${details}</div>`;
  }).join('');
  return `<aside class="workspace-notices" role="status" aria-label="Workspace 状态"><div class="workspace-notices__header"><strong>读取状态</strong>${hasRetry ? '<button type="button" class="workspace-notices__retry" data-workspace-action="refresh">重试当前任务</button>' : ''}</div>${rows}</aside>`;
}

module.exports = { renderWorkspaceHtml, escapeHtml };
