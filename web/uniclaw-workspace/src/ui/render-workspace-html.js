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
  const projectsHtml = (nav.projects || []).map((project) => renderProject(project, renderedTaskKeys)).join('');
  const taskHtml = (nav.taskInstances || [])
    .filter((task) => {
      const key = taskIdentity(task);
      return !key || !renderedTaskKeys.has(key);
    })
    .map((task) => {
      const key = taskIdentity(task);
      if (key) renderedTaskKeys.add(key);
      return renderTask(task);
    }).join('');
  const root = `workspace workspace--${escapeHtml(vm.status || 'idle')}`;
  return `<div class="${root}" data-workspace-status="${escapeHtml(vm.status || 'idle')}">
  <nav class="workspace-navigation" aria-label="${text(options.navigationLabel, '任务导航')}">
    <h1 class="workspace-navigation__title">${text(options.title, 'UniClaw Workspace')}</h1>
    <div class="workspace-navigation__projects">${projectsHtml}</div>
    <div class="workspace-navigation__tasks" data-selected-product-session-id="${escapeHtml(nav.selectedProductSessionId || '')}">${taskHtml}</div>
  </nav>
  <main class="workspace-main">
    ${renderNotices(vm.notices, nav.errors)}
    <header class="workspace-task-header" data-product-session-id="${escapeHtml(header.productSessionId || '')}">
      <div><p class="workspace-eyebrow">${text(header.source, 'Uni-Agent')}</p><h2>${text(header.title, '未选择任务')}</h2></div>
      <div class="workspace-task-header__meta">${status(header.status)}<span class="workspace-correlation">${text(header.correlationStatus, 'unselected')}</span></div>
    </header>
    <section class="workspace-conversation" aria-labelledby="conversation-title"><h3 id="conversation-title">执行过程</h3>${renderTimeline(vm.conversationTimeline)}</section>
    <section class="workspace-panes" aria-label="${text(options.panesLabel, '任务信息')}">
      ${renderTracePane(vm.tracePane)}${renderEvidencePane(vm.evidencePane)}${renderMetadataPane(vm.metadataPane)}
    </section>
    ${renderDetail(vm.detailActions)}
  </main>
</div>`;
}

function renderProject(project, renderedTaskKeys) {
  const tasks = (project.tasks || []).filter((task) => {
    const key = taskIdentity(task);
    if (!key) return true;
    if (renderedTaskKeys.has(key)) return false;
    renderedTaskKeys.add(key);
    return true;
  });
  return `<section class="workspace-project" data-project-id="${escapeHtml(project.projectId || '')}"><h2>${text(project.name, '未命名项目')}</h2>${tasks.map(renderTask).join('')}</section>`;
}
function taskIdentity(task = {}) { return task.productSessionId || task.id || null; }
function renderTask(task) {
  const orphan = task.correlationStatus === 'uncorrelated' || !task.productSessionId;
  return `<article class="workspace-task-card${orphan ? ' is-uncorrelated' : ''}" data-task-id="${escapeHtml(task.id || '')}" data-correlation-status="${escapeHtml(task.correlationStatus || 'unknown')}"><h3>${text(task.title, '未命名任务')}</h3><div class="workspace-task-card__meta"><span>${text(task.productSessionId, orphan ? '未关联 ProductSession' : '未知 ProductSession')}</span>${status(task.status)}</div></article>`;
}
function renderTimeline(pane = {}) {
  return `<div class="workspace-timeline" data-status="${escapeHtml(pane.status || 'idle')}">${['request', 'decision', 'result'].map((kind) => `<section class="workspace-timeline__group workspace-timeline__group--${kind}"><h4>${kind === 'request' ? '请求' : kind === 'decision' ? '决策' : '结果'}</h4>${(pane.groups && pane.groups[kind] || []).map((item) => `<article class="workspace-timeline-card" data-source="${escapeHtml(item.source || '')}" data-status="${escapeHtml(item.status || '')}"><p>${text(item.summary, '无摘要')}</p><small>${text(item.source, '未知来源')} · ${text(item.authority, '未知权威')}</small></article>`).join('')}</section>`).join('')}</div>`;
}
function renderTracePane(pane = {}) {
  return `<section class="workspace-pane workspace-pane--trace" data-pane="trace"><h3>Trace</h3>${Object.keys(pane.groups || {}).map((source) => `<div class="workspace-trace-group" data-source="${escapeHtml(source)}"><h4>${text(source)}</h4>${(pane.groups[source] || []).map((item) => `<article class="workspace-trace-card"><span>${text(item.id, 'trace')}</span>${item.detailAvailable && item.detailRef ? `<button type="button" class="workspace-detail-action" data-detail-ref="${escapeHtml(item.detailRef.refId || item.detailRef)}">查看明细</button>` : ''}</article>`).join('')}</div>`).join('')}</section>`;
}
function renderEvidencePane(pane = {}) {
  return `<section class="workspace-pane workspace-pane--evidence" data-pane="evidence"><h3>Evidence</h3>${(pane.items || []).map((item) => { const action = item.detailAction || {}; return `<article class="workspace-evidence-card" data-correlation-status="${escapeHtml(item.correlationStatus || '')}"><h4>${text(item.title, '未命名证据')}</h4><p>${text(item.source, '未知来源')}</p>${action.enabled && action.detailRef ? `<button type="button" class="workspace-detail-action" data-detail-ref="${escapeHtml(action.detailRef.refId || action.detailRef)}">查看明细</button>` : `<button type="button" class="workspace-detail-action" disabled aria-disabled="true">${text(action.reason, '详情不可用')}</button>`}${item.error ? `<p class="workspace-error">${text(item.error.message || item.error.code)}</p>` : ''}</article>`; }).join('')}</section>`;
}
function renderMetadataPane(pane = {}) { return `<section class="workspace-pane workspace-pane--metadata" data-pane="metadata"><h3>Metadata</h3><dl>${Object.keys(pane.items || {}).map((key) => `<div><dt>${text(key)}</dt><dd>${text(pane.items[key])}</dd></div>`).join('')}</dl></section>`; }
function renderDetail(detail = {}) { return `<aside class="workspace-detail-drawer" data-detail-status="${escapeHtml(detail.status || 'idle')}" aria-label="详情"><h3>详情</h3>${detail.current ? `<pre>${text(JSON.stringify(detail.current, null, 2))}</pre>` : `<p>${text(detail.status === 'loading' ? '正在加载详情' : '选择记录查看详情')}</p>`}</aside>`; }
function renderNotices(notices = [], errors = []) { const all = [...(notices || []), ...(errors || [])]; return all.length ? `<aside class="workspace-notices" role="status">${all.map((notice) => `<div class="workspace-notice workspace-notice--${escapeHtml(notice.level || 'warning')}" data-code="${escapeHtml(notice.code || '')}">${text(notice.message || notice.code || 'Workspace error')}</div>`).join('')}</aside>` : ''; }

module.exports = { renderWorkspaceHtml, escapeHtml };
