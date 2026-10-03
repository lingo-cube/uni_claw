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
    <h1 class="workspace-navigation__title">${text(options.title, 'UniClaw Workspace')}</h1>
    <div class="workspace-navigation__projects">${projectsHtml}</div>
    <div class="workspace-navigation__tasks" data-selected-product-session-id="${escapeHtml(nav.selectedProductSessionId || '')}">${taskHtml}</div>
  </nav>
  <main class="workspace-main">
    ${renderNotices(vm.notices)}
    <header class="workspace-task-header" data-selected-product-session-id="${escapeHtml(header.productSessionId || '')}">
      <div><p class="workspace-eyebrow">${text(header.source, 'Uni-Agent')}<span class="workspace-header-origin">${text(header.origin, '')}</span></p><h2>${text(header.title, '未选择任务')}</h2></div>
      <button type="button" class="workspace-refresh-action" data-workspace-action="refresh">刷新</button>
      <div class="workspace-task-header__meta">${status(header.status)}<span class="workspace-correlation">${text(header.correlationStatus, 'unselected')}</span></div>
    </header>
    <section class="workspace-conversation" aria-labelledby="conversation-title"><h3 id="conversation-title">Uni-Agent 解决过程</h3>${renderTimeline(vm.conversationTimeline)}</section>
    <section class="workspace-panes" aria-label="${text(options.panesLabel, '任务信息')}">
      ${renderTracePane(vm.tracePane)}${renderEvidencePane(vm.evidencePane)}${renderMetadataPane(vm.metadataPane)}
    </section>
    ${renderDetail(vm.detailActions)}
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
function renderTracePane(pane = {}) {
  return `<section class="workspace-pane workspace-pane--trace" data-pane="trace"><h3>Trace</h3>${Object.keys(pane.groups || {}).map((source) => `<div class="workspace-trace-group" data-source="${escapeHtml(source)}"><h4>${text(source)}</h4>${(pane.groups[source] || []).map((item) => `<article class="workspace-trace-card"><span>${text(item.id, 'trace')}</span>${item.detailAvailable && item.detailRef ? `<button type="button" class="workspace-detail-action" data-workspace-action="resolve-detail" data-detail-ref="${escapeHtml(item.detailRef.refId || item.detailRef)}">查看明细</button>` : ''}</article>`).join('')}</div>`).join('')}</section>`;
}
function renderEvidencePane(pane = {}) {
  return `<section class="workspace-pane workspace-pane--evidence" data-pane="evidence"><h3>Evidence</h3>${(pane.items || []).map((item) => { const action = item.detailAction || {}; return `<article class="workspace-evidence-card" data-correlation-status="${escapeHtml(item.correlationStatus || '')}"><h4>${text(item.title, '未命名证据')}</h4><p>${text(item.source, '未知来源')}</p>${action.enabled && action.detailRef ? `<button type="button" class="workspace-detail-action" data-workspace-action="resolve-detail" data-detail-ref="${escapeHtml(action.detailRef.refId || action.detailRef)}">查看明细</button>` : `<button type="button" class="workspace-detail-action" disabled aria-disabled="true">${text(action.reason, '详情不可用')}</button>`}${item.error ? `<p class="workspace-error">${text(item.error.message || item.error.code)}</p>` : ''}</article>`; }).join('')}</section>`;
}
function renderMetadataPane(pane = {}) { return `<section class="workspace-pane workspace-pane--metadata" data-pane="metadata"><h3>Metadata</h3><dl>${Object.keys(pane.items || {}).map((key) => `<div><dt>${text(key)}</dt><dd>${text(pane.items[key])}</dd></div>`).join('')}</dl></section>`; }
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
